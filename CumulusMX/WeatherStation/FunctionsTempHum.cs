using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void DoOutdoorTemp(double temp, DateTime timestamp)
		{
			// Spike removal is in user units
			if (previousTemp < 998 && Math.Abs(temp - previousTemp) > cumulus.Spike.TempDiff)
			{
				cumulus.LogSpikeRemoval("Temp difference greater than spike value; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={temp.ToString(cumulus.TempFormat)} OldVal={previousTemp.ToString(cumulus.TempFormat)} SpikeTempDiff={cumulus.Spike.TempDiff.ToString(cumulus.TempFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Temp difference greater than spike value - NewVal={temp.ToString(cumulus.TempFormat)} OldVal={previousTemp.ToString(cumulus.TempFormat)} SpikeTempDiff={cumulus.Spike.TempDiff.ToString(cumulus.TempFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}
			else if (temp > cumulus.Limit.TempHigh)
			{
				cumulus.LogSpikeRemoval("Temp greater than upper limit; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={temp.ToString(cumulus.TempFormat)} HighLimit={cumulus.Limit.TempHigh.ToString(cumulus.TempFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Temp greater than upper limit - NewVal={temp.ToString(cumulus.TempFormat)} HighLimit={cumulus.Limit.TempHigh.ToString(cumulus.TempFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}
			else if (temp < cumulus.Limit.TempLow)
			{
				cumulus.LogSpikeRemoval("Temp less than lower limit; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={temp.ToString(cumulus.TempFormat)} LowLimit={cumulus.Limit.TempLow.ToString(cumulus.TempFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Temp less than lower limit - NewVal={temp.ToString(cumulus.TempFormat)} LowLimit={cumulus.Limit.TempLow.ToString(cumulus.TempFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousTemp = temp;

			// update global temp
			MetData.Temperature = cumulus.Calib.Temp.Calibrate(temp);

			first_temp = false;

			// Does this reading set any records or trigger any alarms?
			if (MetData.Temperature > Records.AllTime.HighTemp.Val)
				SetAlltime(Records.AllTime.HighTemp, MetData.Temperature, timestamp);

			cumulus.HighTempAlarm.CheckAlarm(MetData.Temperature);

			if (MetData.Temperature < Records.AllTime.LowTemp.Val)
				SetAlltime(Records.AllTime.LowTemp, MetData.Temperature, timestamp);

			cumulus.LowTempAlarm.CheckAlarm(MetData.Temperature);

			CheckMonthlyAlltime("HighTemp", MetData.Temperature, true, timestamp);
			CheckMonthlyAlltime("LowTemp", MetData.Temperature, false, timestamp);

			var writeToday = false;

			if (MetData.Temperature > DailyHighLow.Today.HighTemp)
			{
				DailyHighLow.Today.HighTemp = MetData.Temperature;
				DailyHighLow.Today.HighTempTime = timestamp;
				writeToday = true;
			}

			if (MetData.Temperature < DailyHighLow.Today.LowTemp)
			{
				DailyHighLow.Today.LowTemp = MetData.Temperature;
				DailyHighLow.Today.LowTempTime = timestamp;
				writeToday = true;
			}

			if (MetData.Temperature > DailyHighLow.TodayMidnight.HighTemp)
			{
				DailyHighLow.TodayMidnight.HighTemp = MetData.Temperature;
				DailyHighLow.TodayMidnight.HighTempTime = timestamp;
				writeToday = true;
			}

			if (MetData.Temperature < DailyHighLow.TodayMidnight.LowTemp)
			{
				DailyHighLow.TodayMidnight.LowTemp = MetData.Temperature;
				DailyHighLow.TodayMidnight.LowTempTime = timestamp;
				writeToday = true;
			}

			if (MetData.Temperature > DailyHighLow.Today9am.HighTemp)
			{
				DailyHighLow.Today9am.HighTemp = MetData.Temperature;
				DailyHighLow.Today9am.HighTempTime = timestamp;
				writeToday = true;
			}

			if (MetData.Temperature < DailyHighLow.Today9am.LowTemp)
			{
				DailyHighLow.Today9am.LowTemp = MetData.Temperature;
				DailyHighLow.Today9am.LowTempTime = timestamp;
				writeToday = true;
			}

			if (writeToday)
			{
				WriteTodayFile(timestamp, false);
			}

			if (MetData.Temperature > Records.ThisMonth.HighTemp.Val)
			{
				Records.ThisMonth.HighTemp.Val = MetData.Temperature;
				Records.ThisMonth.HighTemp.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.Temperature < Records.ThisMonth.LowTemp.Val)
			{
				Records.ThisMonth.LowTemp.Val = MetData.Temperature;
				Records.ThisMonth.LowTemp.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.Temperature > Records.ThisYear.HighTemp.Val)
			{
				Records.ThisYear.HighTemp.Val = MetData.Temperature;
				Records.ThisYear.HighTemp.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.Temperature < Records.ThisYear.LowTemp.Val)
			{
				Records.ThisYear.LowTemp.Val = MetData.Temperature;
				Records.ThisYear.LowTemp.Ts = timestamp;
				WriteYearIniFile();
			}

			// Calculate temperature range
			DailyHighLow.Today.TempRange = DailyHighLow.Today.HighTemp - DailyHighLow.Today.LowTemp;

			if ((cumulus.StationOptions.CalculatedDP || cumulus.DavisStation) && MetData.Humidity != 0 && !cumulus.FineOffsetStation)
			{
				// Calculate DewPoint.
				var tempinC = ConvertUnits.UserTempToC(MetData.Temperature);
				MetData.Dewpoint = ConvertUnits.TempCToUser(MeteoLib.DewPoint(tempinC, MetData.Humidity));

				CheckForDewpointHighLow(timestamp);
			}

			// Does the WBGT need updating?
			if (cumulus.StationOptions.CalculatedWBGT)
			{
				CalculateWBGT(timestamp);
			}

			TempReadyToPlot = true;
			HaveReadData = true;
		}

		public void DoApparentTemp(DateTime timestamp)
		{
			// Calculates Apparent Temperature
			// See http://www.bom.gov.au/info/thermal_stress/#atapproximation

			MetData.ApparentTemperature = ConvertUnits.TempCToUser(MeteoLib.ApparentTemperature(ConvertUnits.UserTempToC(MetData.Temperature), ConvertUnits.UserWindToMS(MetData.WindAverage), MetData.Humidity));


			// we will tag on the THW Index here
			MetData.THWIndex = ConvertUnits.TempCToUser(MeteoLib.THWIndex(ConvertUnits.UserTempToC(MetData.Temperature), MetData.Humidity, ConvertUnits.UserWindToKPH(MetData.WindAverage)));

			if (MetData.ApparentTemperature > DailyHighLow.Today.HighAppTemp)
			{
				DailyHighLow.Today.HighAppTemp = MetData.ApparentTemperature;
				DailyHighLow.Today.HighAppTempTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.ApparentTemperature < DailyHighLow.Today.LowAppTemp)
			{
				DailyHighLow.Today.LowAppTemp = MetData.ApparentTemperature;
				DailyHighLow.Today.LowAppTempTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.ApparentTemperature > Records.ThisMonth.HighAppTemp.Val)
			{
				Records.ThisMonth.HighAppTemp.Val = MetData.ApparentTemperature;
				Records.ThisMonth.HighAppTemp.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.ApparentTemperature < Records.ThisMonth.LowAppTemp.Val)
			{
				Records.ThisMonth.LowAppTemp.Val = MetData.ApparentTemperature;
				Records.ThisMonth.LowAppTemp.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.ApparentTemperature > Records.ThisYear.HighAppTemp.Val)
			{
				Records.ThisYear.HighAppTemp.Val = MetData.ApparentTemperature;
				Records.ThisYear.HighAppTemp.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.ApparentTemperature < Records.ThisYear.LowAppTemp.Val)
			{
				Records.ThisYear.LowAppTemp.Val = MetData.ApparentTemperature;
				Records.ThisYear.LowAppTemp.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.ApparentTemperature > Records.AllTime.HighAppTemp.Val)
				SetAlltime(Records.AllTime.HighAppTemp, MetData.ApparentTemperature, timestamp);

			if (MetData.ApparentTemperature < Records.AllTime.LowAppTemp.Val)
				SetAlltime(Records.AllTime.LowAppTemp, MetData.ApparentTemperature, timestamp);

			CheckMonthlyAlltime("HighAppTemp", MetData.ApparentTemperature, true, timestamp);
			CheckMonthlyAlltime("LowAppTemp", MetData.ApparentTemperature, false, timestamp);
		}

		public void DoWindChill(double chillpar, DateTime timestamp)
		{
			var chillvalid = true;

			if (cumulus.StationOptions.CalculatedWC || chillpar < -500)
			{
				// don"t try to calculate wind chill if we haven"t yet had wind and temp readings
				if (TempReadyToPlot && WindReadyToPlot)
				{
					var TempinC = ConvertUnits.UserTempToC(MetData.Temperature);
					var windinKPH = ConvertUnits.UserWindToKPH(MetData.WindAverage);
					// no wind chill below 1.5 m/s = 5.4 km
					if (windinKPH >= 5.4)
					{
						MetData.WindChill = ConvertUnits.TempCToUser(MeteoLib.WindChill(TempinC, windinKPH));
					}
					else
					{
						MetData.WindChill = MetData.Temperature;
					}
				}
				else
				{
					chillvalid = false;
				}
			}
			else
			{
				MetData.WindChill = chillpar;
			}

			if (chillvalid)
			{
				if (MetData.WindChill < DailyHighLow.Today.LowWindChill)
				{
					DailyHighLow.Today.LowWindChill = MetData.WindChill;
					DailyHighLow.Today.LowWindChillTime = timestamp;
					WriteTodayFile(timestamp, false);
				}

				if (MetData.WindChill < Records.ThisMonth.LowChill.Val)
				{
					Records.ThisMonth.LowChill.Val = MetData.WindChill;
					Records.ThisMonth.LowChill.Ts = timestamp;
					WriteMonthIniFile();
				}

				if (MetData.WindChill < Records.ThisYear.LowChill.Val)
				{
					Records.ThisYear.LowChill.Val = MetData.WindChill;
					Records.ThisYear.LowChill.Ts = timestamp;
					WriteYearIniFile();
				}

				// All time wind chill
				if (MetData.WindChill < Records.AllTime.LowChill.Val)
				{
					SetAlltime(Records.AllTime.LowChill, MetData.WindChill, timestamp);
				}

				CheckMonthlyAlltime("LowChill", MetData.WindChill, false, timestamp);
			}
		}

		public void DoFeelsLike(DateTime timestamp)
		{
			MetData.FeelsLike = ConvertUnits.TempCToUser(MeteoLib.FeelsLike(ConvertUnits.UserTempToC(MetData.Temperature), ConvertUnits.UserWindToKPH(MetData.WindAverage), MetData.Humidity));

			if (MetData.FeelsLike > DailyHighLow.Today.HighFeelsLike)
			{
				DailyHighLow.Today.HighFeelsLike = MetData.FeelsLike;
				DailyHighLow.Today.HighFeelsLikeTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.FeelsLike < DailyHighLow.Today.LowFeelsLike)
			{
				DailyHighLow.Today.LowFeelsLike = MetData.FeelsLike;
				DailyHighLow.Today.LowFeelsLikeTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.FeelsLike > Records.ThisMonth.HighFeelsLike.Val)
			{
				Records.ThisMonth.HighFeelsLike.Val = MetData.FeelsLike;
				Records.ThisMonth.HighFeelsLike.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.FeelsLike < Records.ThisMonth.LowFeelsLike.Val)
			{
				Records.ThisMonth.LowFeelsLike.Val = MetData.FeelsLike;
				Records.ThisMonth.LowFeelsLike.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.FeelsLike > Records.ThisYear.HighFeelsLike.Val)
			{
				Records.ThisYear.HighFeelsLike.Val = MetData.FeelsLike;
				Records.ThisYear.HighFeelsLike.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.FeelsLike < Records.ThisYear.LowFeelsLike.Val)
			{
				Records.ThisYear.LowFeelsLike.Val = MetData.FeelsLike;
				Records.ThisYear.LowFeelsLike.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.FeelsLike > Records.AllTime.HighFeelsLike.Val)
				SetAlltime(Records.AllTime.HighFeelsLike, MetData.FeelsLike, timestamp);

			if (MetData.FeelsLike < Records.AllTime.LowFeelsLike.Val)
				SetAlltime(Records.AllTime.LowFeelsLike, MetData.FeelsLike, timestamp);

			CheckMonthlyAlltime("HighFeelsLike", MetData.FeelsLike, true, timestamp);
			CheckMonthlyAlltime("LowFeelsLike", MetData.FeelsLike, false, timestamp);
		}

		public void DoHumidex(DateTime timestamp)
		{
			MetData.Humidex = MeteoLib.Humidex(ConvertUnits.UserTempToC(MetData.Temperature), MetData.Humidity);

			if (MetData.Humidex > DailyHighLow.Today.HighHumidex)
			{
				DailyHighLow.Today.HighHumidex = MetData.Humidex;
				DailyHighLow.Today.HighHumidexTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.Humidex > Records.ThisMonth.HighHumidex.Val)
			{
				Records.ThisMonth.HighHumidex.Val = MetData.Humidex;
				Records.ThisMonth.HighHumidex.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.Humidex > Records.ThisYear.HighHumidex.Val)
			{
				Records.ThisYear.HighHumidex.Val = MetData.Humidex;
				Records.ThisYear.HighHumidex.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.Humidex > Records.AllTime.HighHumidex.Val)
				SetAlltime(Records.AllTime.HighHumidex, MetData.Humidex, timestamp);

			CheckMonthlyAlltime("HighHumidex", MetData.Humidex, true, timestamp);
		}

		public void CheckForDewpointHighLow(DateTime timestamp)
		{
			if (MetData.Dewpoint > DailyHighLow.Today.HighDewPoint)
			{
				DailyHighLow.Today.HighDewPoint = MetData.Dewpoint;
				DailyHighLow.Today.HighDewPointTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (MetData.Dewpoint < DailyHighLow.Today.LowDewPoint)
			{
				DailyHighLow.Today.LowDewPoint = MetData.Dewpoint;
				DailyHighLow.Today.LowDewPointTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (MetData.Dewpoint > Records.ThisMonth.HighDewPoint.Val)
			{
				Records.ThisMonth.HighDewPoint.Val = MetData.Dewpoint;
				Records.ThisMonth.HighDewPoint.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (MetData.Dewpoint < Records.ThisMonth.LowDewPoint.Val)
			{
				Records.ThisMonth.LowDewPoint.Val = MetData.Dewpoint;
				Records.ThisMonth.LowDewPoint.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (MetData.Dewpoint > Records.ThisYear.HighDewPoint.Val)
			{
				Records.ThisYear.HighDewPoint.Val = MetData.Dewpoint;
				Records.ThisYear.HighDewPoint.Ts = timestamp;
				WriteYearIniFile();
			}
			if (MetData.Dewpoint < Records.ThisYear.LowDewPoint.Val)
			{
				Records.ThisYear.LowDewPoint.Val = MetData.Dewpoint;
				Records.ThisYear.LowDewPoint.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.Dewpoint > Records.AllTime.HighDewPoint.Val)
			{
				SetAlltime(Records.AllTime.HighDewPoint, MetData.Dewpoint, timestamp);
			}
			if (MetData.Dewpoint < Records.AllTime.LowDewPoint.Val)
				SetAlltime(Records.AllTime.LowDewPoint, MetData.Dewpoint, timestamp);

			CheckMonthlyAlltime("HighDewPoint", MetData.Dewpoint, true, timestamp);
			CheckMonthlyAlltime("LowDewPoint", MetData.Dewpoint, false, timestamp);
		}

		public void DoBGT(double? temp, DateTime timestamp)
		{
			MetData.BlackGlobeTemp = temp;

			if (!MetData.BlackGlobeTemp.HasValue) return;

			if (MetData.BlackGlobeTemp > DailyHighLow.Today.HighBgt)
			{
				DailyHighLow.Today.HighBgt = MetData.BlackGlobeTemp.Value;
				DailyHighLow.Today.HighBgtTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.BlackGlobeTemp > Records.ThisMonth.HighBgt.Val)
			{
				Records.ThisMonth.HighBgt.Val = MetData.BlackGlobeTemp.Value;
				Records.ThisMonth.HighBgt.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.BlackGlobeTemp > Records.ThisYear.HighBgt.Val)
			{
				Records.ThisYear.HighBgt.Val = MetData.BlackGlobeTemp.Value;
				Records.ThisYear.HighBgt.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.BlackGlobeTemp > Records.AllTime.HighBgt.Val)
				SetAlltime(Records.AllTime.HighBgt, MetData.BlackGlobeTemp.Value, timestamp);

			CheckMonthlyAlltime("HighBgt", MetData.BlackGlobeTemp.Value, true, timestamp);
		}

		public void CalculateWBGT(DateTime ts)
		{
			if (MetData.BlackGlobeTemp.HasValue && MetData.Pressure > 0)
			{
				var wbgt_c = MeteoLib.CalculateWetBulbeGlobeTemp(
					ConvertUnits.UserTempToC(MetData.Temperature),
					ConvertUnits.UserTempToC(MetData.Dewpoint),
					ConvertUnits.UserPressToHpa(MetData.Pressure),
					ConvertUnits.UserTempToC(MetData.BlackGlobeTemp.Value)
					);
				wbgt_c = cumulus.Calib.WetBulb.Calibrate(wbgt_c);

				DoWBGT(ConvertUnits.TempCToUser(wbgt_c), ts);
			}
		}

		public void DoWBGT(double? temp, DateTime timestamp)
		{
			MetData.WetBulbGlobeTemp = temp;

			if (!MetData.WetBulbGlobeTemp.HasValue) return;

			if (MetData.WetBulbGlobeTemp > DailyHighLow.Today.HighWbgt)
			{
				DailyHighLow.Today.HighWbgt = MetData.WetBulbGlobeTemp.Value;
				DailyHighLow.Today.HighWbgtTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.WetBulbGlobeTemp > Records.ThisMonth.HighWbgt.Val)
			{
				Records.ThisMonth.HighWbgt.Val = MetData.WetBulbGlobeTemp.Value;
				Records.ThisMonth.HighWbgt.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.WetBulbGlobeTemp > Records.ThisYear.HighWbgt.Val)
			{
				Records.ThisYear.HighWbgt.Val = MetData.WetBulbGlobeTemp.Value;
				Records.ThisYear.HighWbgt.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.WetBulbGlobeTemp > Records.AllTime.HighWbgt.Val)
				SetAlltime(Records.AllTime.HighWbgt, MetData.WetBulbGlobeTemp.Value, timestamp);

			CheckMonthlyAlltime("HighWbgt", MetData.WetBulbGlobeTemp.Value, true, timestamp);
		}

		public void DoOutdoorDewpoint(double dp, DateTime timestamp)
		{

			if (cumulus.StationOptions.CalculatedDP || dp < -500)
			{
				dp = ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC(MetData.Temperature), MetData.Humidity));

			}

			if (ConvertUnits.UserTempToC(dp) <= cumulus.Limit.DewHigh)
			{
				MetData.Dewpoint = dp;
				CheckForDewpointHighLow(timestamp);
			}
			else
			{
				var msg = $"Dew point greater than limit ({cumulus.Limit.DewHigh.ToString(cumulus.TempFormat)}); reading ignored: {dp.ToString(cumulus.TempFormat)}";
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = msg;
				cumulus.SpikeAlarm.Triggered = true;
				cumulus.LogSpikeRemoval(msg);
			}
		}

		protected void DoWetBulb(double temp, DateTime timestamp) // Supplied in CELSIUS

		{
			MetData.WetBulb = ConvertUnits.TempCToUser(temp);
			MetData.WetBulb = cumulus.Calib.WetBulb.Calibrate(MetData.WetBulb);

			// calculate RH
			var TempDry = ConvertUnits.UserTempToC(MetData.Temperature);
			var Es = MeteoLib.SaturationVapourPressure1980(TempDry);
			var Ew = MeteoLib.SaturationVapourPressure1980(temp);
			var E = Ew - 0.00066 * (1 + 0.00115 * temp) * (TempDry - temp) * 1013;
			var hum = (int) (100 * (E / Es));
			DoOutdoorHumidity(hum, timestamp);
			// calculate DP
			// Calculate DewPoint

			MetData.Dewpoint = ConvertUnits.TempCToUser(MeteoLib.DewPoint(TempDry, hum));

			CheckForDewpointHighLow(timestamp);
		}

		public void DoIndoorTemp(double temp)
		{
			// Spike check
			if (previousInTemp < 998 && Math.Abs(temp - previousInTemp) > cumulus.Spike.InTempDiff)
			{
				cumulus.LogSpikeRemoval("Indoor temperature difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={temp.ToString(cumulus.TempFormat)} OldVal={previousInTemp.ToString(cumulus.TempFormat)} SpikeDiff={cumulus.Spike.InTempDiff.ToString(cumulus.TempFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Indoor temperature difference greater than spike value - NewVal={temp.ToString(cumulus.TempFormat)} OldVal={previousInTemp.ToString(cumulus.TempFormat)} SpikeDiff={cumulus.Spike.InTempDiff.ToString(cumulus.TempFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousInTemp = temp;
			MetData.TemperatureIn = cumulus.Calib.InTemp.Calibrate(temp);
			HaveReadData = true;
		}

		public double TempAvg24Hrs()
		{
			try
			{
				return RecentDataDb.ExecuteScalar<double>("select avg(OutsideTemp) from RecentData where Timestamp >= unixepoch(datetime('now', '-24 hour'))");
			}
			catch (Exception ex)
			{
				cumulus.LogErrorMessage("TempAvg24Hrs: Error querying database: " + ex.Message);
				return 0.0;
			}
		}

		public void UpdateDegreeDays(int interval)
		{
			if (MetData.Temperature < cumulus.NOAAconf.HeatThreshold)
			{
				MetData.HeatingDegreeDays += (cumulus.NOAAconf.HeatThreshold - MetData.Temperature) * interval / 1440;
			}
			if (MetData.Temperature > cumulus.NOAAconf.CoolThreshold)
			{
				MetData.CoolingDegreeDays += (MetData.Temperature - cumulus.NOAAconf.CoolThreshold) * interval / 1440;
			}
		}

		public void DoIndoorHumidity(int hum)
		{
			// Spike check
			if (previousInHum != 999 && Math.Abs(hum - previousInHum) > cumulus.Spike.InHumDiff)
			{
				cumulus.LogSpikeRemoval("Indoor humidity difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={hum} OldVal={previousInHum} SpikeDiff={cumulus.Spike.InHumDiff:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Indoor humidity difference greater than spike value - NewVal={hum} OldVal={previousInHum} SpikeDiff={cumulus.Spike.InHumDiff:F1}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousInHum = hum;
			MetData.HumidityIn = (int) cumulus.Calib.InHum.Calibrate(hum);

			if (MetData.HumidityIn < 0)
			{
				MetData.HumidityIn = 0;
			}
			if (MetData.HumidityIn > 100)
			{
				MetData.HumidityIn = 100;
			}
			HaveReadData = true;
		}

		public void DoOutdoorHumidity(int humpar, DateTime timestamp)
		{
			// Spike check
			if (previousHum < 998 && Math.Abs(humpar - previousHum) > cumulus.Spike.HumidityDiff)
			{
				cumulus.LogSpikeRemoval("Humidity difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={humpar} OldVal={previousHum} SpikeHumidityDiff={cumulus.Spike.HumidityDiff:F1}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Humidity difference greater than spike value - NewVal={humpar} OldVal={previousHum} SpikeHumidityDiff={cumulus.Spike.HumidityDiff:F1}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}
			previousHum = humpar;

			if (humpar >= 98 && cumulus.StationOptions.Humidity98Fix)
			{
				MetData.Humidity = 100;
			}
			else
			{
				MetData.Humidity = (int) cumulus.Calib.Hum.Calibrate(humpar);
			}

			if (MetData.Humidity < 0)
			{
				MetData.Humidity = 0;
			}
			if (MetData.Humidity > 100)
			{
				MetData.Humidity = 100;
			}

			if (MetData.Humidity > DailyHighLow.Today.HighHumidity)
			{
				DailyHighLow.Today.HighHumidity = MetData.Humidity;
				DailyHighLow.Today.HighHumidityTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (MetData.Humidity < DailyHighLow.Today.LowHumidity)
			{
				DailyHighLow.Today.LowHumidity = MetData.Humidity;
				DailyHighLow.Today.LowHumidityTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (MetData.Humidity > Records.ThisMonth.HighHumidity.Val)
			{
				Records.ThisMonth.HighHumidity.Val = MetData.Humidity;
				Records.ThisMonth.HighHumidity.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (MetData.Humidity < Records.ThisMonth.LowHumidity.Val)
			{
				Records.ThisMonth.LowHumidity.Val = MetData.Humidity;
				Records.ThisMonth.LowHumidity.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (MetData.Humidity > Records.ThisYear.HighHumidity.Val)
			{
				Records.ThisYear.HighHumidity.Val = MetData.Humidity;
				Records.ThisYear.HighHumidity.Ts = timestamp;
				WriteYearIniFile();
			}
			if (MetData.Humidity < Records.ThisYear.LowHumidity.Val)
			{
				Records.ThisYear.LowHumidity.Val = MetData.Humidity;
				Records.ThisYear.LowHumidity.Ts = timestamp;
				WriteYearIniFile();
			}
			if (MetData.Humidity > Records.AllTime.HighHumidity.Val)
			{
				SetAlltime(Records.AllTime.HighHumidity, MetData.Humidity, timestamp);
			}
			CheckMonthlyAlltime("HighHumidity", MetData.Humidity, true, timestamp);
			if (MetData.Humidity < Records.AllTime.LowHumidity.Val)
			{
				SetAlltime(Records.AllTime.LowHumidity, MetData.Humidity, timestamp);
			}
			CheckMonthlyAlltime("LowHumidity", MetData.Humidity, false, timestamp);
			HaveReadData = true;
		}
	}
}
