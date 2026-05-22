using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void DoTrendValues(DateTime ts, bool rollover = false)
		{
			double trendval;
			List<RecentData> retVals;
			var recTs = ts;
			var recDtTm = ts;

			// if this is the special case of rollover processing, we want the High today record to on the previous day at 23:59 or 08:59
			if (rollover)
			{
				recTs = recTs.Date.AddHours(recTs.Hour);
				recDtTm = recTs.AddMinutes(-1);  // set record date/time at at 23:59 or 08:59 at rollover to avoid confusion with start of day at 00:00 or 09:00
			}

			// Do 3 hour trends
			try
			{
				retVals = RecentDataDb.Query<RecentData>("select OutsideTemp, Pressure from RecentData where Timestamp >= ? order by Timestamp limit 1", recTs.AddHours(-3).ToUnixTime());

				if (retVals.Count != 1)
				{
					MetData.TempTrendVal = 0;
					MetData.PressTrendVal = 0;
				}
				else
				{
					if (TempReadyToPlot)
					{
						// calculate and display the temp trend
						MetData.TempTrendVal = (MetData.Temperature - retVals[0].OutsideTemp) / 3.0F;
						cumulus.TempChangeAlarm.CheckAlarm(MetData.TempTrendVal);
					}

					if (PressReadyToPlot)
					{
						// calculate and display the pressure trend
						MetData.PressTrendVal = (MetData.Pressure - retVals[0].Pressure) / 3.0;
						cumulus.PressChangeAlarm.CheckAlarm(MetData.PressTrendVal);
					}
				}
			}
			catch
			{
				MetData.TempTrendVal = 0;
				MetData.PressTrendVal = 0;
			}

			try
			{
				// Do 1 hour trends
				retVals = RecentDataDb.Query<RecentData>("select OutsideTemp, raincounter from RecentData where Timestamp >= ? order by Timestamp limit 1", recTs.AddHours(-1).ToUnixTime());

				if (retVals.Count != 1)
				{
					MetData.TempChangeLastHour = 0;
					MetData.RainLastHour = 0;
				}
				else
				{
					// Calculate Temperature change in the last hour
					MetData.TempChangeLastHour = MetData.Temperature - retVals[0].OutsideTemp;


					// calculate and display rainfall in last hour
					if (MetData.RainCounter < retVals[0].raincounter)
					{
						// rain total is not available or has gone down, assume it was reset to zero, just use zero
						MetData.RainLastHour = 0;
					}
					else
					{
						// normal case
						trendval = MetData.RainCounter - retVals[0].raincounter;

						// Round value as some values may have been read from log file and already rounded
						trendval = Math.Round(trendval, cumulus.RainDPlaces);

						var tempRainLastHour = trendval * cumulus.Calib.Rain.Mult;

						if (tempRainLastHour > cumulus.Spike.MaxHourlyRain)
						{
							// ignore
							cumulus.LogSpikeRemoval("Max hourly rainfall spike value exceed");
							lastSpikeRemoval = DateTime.Now;
							cumulus.SpikeAlarm.LastMessage = $"Max hourly rainfall greater than spike value - Value={tempRainLastHour.ToString(cumulus.RainFormat)} SpikeValue={cumulus.Spike.MaxHourlyRain.ToString(cumulus.RainFormat)}";
							cumulus.SpikeAlarm.Triggered = true;
						}
						else
						{
							MetData.RainLastHour = tempRainLastHour;

							if (MetData.RainLastHour > Records.AllTime.HourlyRain.Val)
							{
								SetAlltime(Records.AllTime.HourlyRain, MetData.RainLastHour, recDtTm);
							}

							CheckMonthlyAlltime("HourlyRain", MetData.RainLastHour, true, recDtTm);

							if (MetData.RainLastHour > DailyHighLow.Today.HighHourlyRain)
							{
								DailyHighLow.Today.HighHourlyRain = MetData.RainLastHour;
								DailyHighLow.Today.HighHourlyRainTime = recDtTm;
								WriteTodayFile(ts, false);
							}

							if (MetData.RainLastHour > Records.ThisMonth.HourlyRain.Val)
							{
								Records.ThisMonth.HourlyRain.Val = MetData.RainLastHour;
								Records.ThisMonth.HourlyRain.Ts = recDtTm;
								WriteMonthIniFile();
							}

							if (MetData.RainLastHour > Records.ThisYear.HourlyRain.Val)
							{
								Records.ThisYear.HourlyRain.Val = MetData.RainLastHour;
								Records.ThisYear.HourlyRain.Ts = recDtTm;
								WriteYearIniFile();
							}
						}
					}
				}
			}
			catch
			{
				MetData.TempChangeLastHour = 0;
				MetData.RainLastHour = 0;
			}


			if (calculaterainrate)
			{
				// Station doesn't supply rain rate, calculate one based on rain in last 5 minutes

				try
				{
					retVals = RecentDataDb.Query<RecentData>("select raincounter from RecentData where Timestamp >= ? order by Timestamp limit 1", recTs.AddMinutes(-5.5).ToUnixTime());

					if (retVals.Count != 1 || MetData.RainCounter < retVals[0].raincounter)
					{
						MetData.RainRate = 0;
					}
					else
					{
						var raindiff = Math.Round(MetData.RainCounter - retVals[0].raincounter, cumulus.RainDPlaces);

						var timediffhours = 1.0 / 12.0;

						// Scale the counter values
						var tempRainRate = Math.Round(raindiff / timediffhours * cumulus.Calib.Rain.Mult, cumulus.RainDPlaces);

						if (tempRainRate < 0)
						{
							tempRainRate = 0;
						}

						if (tempRainRate > cumulus.Spike.MaxRainRate)
						{
							// ignore
							cumulus.LogSpikeRemoval("Max rainfall rate spike value exceed");
							cumulus.LogSpikeRemoval($"Rate value={tempRainRate.ToString(cumulus.RainFormat)} SpikeMaxRainRate={cumulus.Spike.MaxRainRate.ToString(cumulus.RainFormat)}");
							lastSpikeRemoval = DateTime.Now;
							cumulus.SpikeAlarm.LastMessage = $"Max rainfall rate greater than spike value - Value={tempRainRate.ToString(cumulus.RainFormat)} SpikeMaxRainRate={cumulus.Spike.MaxRainRate.ToString(cumulus.RainFormat)}";
							cumulus.SpikeAlarm.Triggered = true;

						}
						else
						{
							MetData.RainRate = tempRainRate;

							if (MetData.RainRate > Records.AllTime.HighRainRate.Val)
							{
								SetAlltime(Records.AllTime.HighRainRate, MetData.RainRate, recDtTm);
							}

							CheckMonthlyAlltime("HighRainRate", MetData.RainRate, true, recDtTm);

							cumulus.HighRainRateAlarm.CheckAlarm(MetData.RainRate);

							if (MetData.RainRate > DailyHighLow.Today.HighRainRate)
							{
								DailyHighLow.Today.HighRainRate = MetData.RainRate;
								DailyHighLow.Today.HighRainRateTime = recDtTm;
								WriteTodayFile(ts, false);
							}

							if (MetData.RainRate > Records.ThisMonth.HighRainRate.Val)
							{
								Records.ThisMonth.HighRainRate.Val = MetData.RainRate;
								Records.ThisMonth.HighRainRate.Ts = recDtTm;
								WriteMonthIniFile();
							}

							if (MetData.RainRate > Records.ThisYear.HighRainRate.Val)
							{
								Records.ThisYear.HighRainRate.Val = MetData.RainRate;
								Records.ThisYear.HighRainRate.Ts = recDtTm;
								WriteYearIniFile();
							}
						}
					}
				}
				catch
				{
					MetData.RainRate = 0;
				}
			}


			// calculate and display rainfall in last 24 hour
			try
			{
				retVals = RecentDataDb.Query<RecentData>("select raincounter from RecentData where Timestamp >= ? order by Timestamp limit 1", recTs.AddDays(-1).ToUnixTime());

				if (retVals.Count != 1 || MetData.RainCounter < retVals[0].raincounter)
				{
					MetData.RainLast24Hour = 0;
				}
				else
				{
					trendval = Math.Round(MetData.RainCounter - retVals[0].raincounter, cumulus.RainDPlaces);

					if (trendval < 0)
					{
						trendval = 0;
					}

					MetData.RainLast24Hour = trendval * cumulus.Calib.Rain.Mult;

					if (MetData.RainLast24Hour > DailyHighLow.Today.HighRain24h)
					{
						DailyHighLow.Today.HighRain24h = MetData.RainLast24Hour;
						DailyHighLow.Today.HighRain24hTime = recTs;
						WriteTodayFile(recTs, false);
					}

					if (MetData.RainLast24Hour > Records.AllTime.HighRain24Hours.Val)
					{
						SetAlltime(Records.AllTime.HighRain24Hours, MetData.RainLast24Hour, recDtTm);
					}

					CheckMonthlyAlltime("HighRain24Hours", MetData.RainLast24Hour, true, recDtTm);

					if (MetData.RainLast24Hour > Records.ThisMonth.HighRain24Hours.Val)
					{
						Records.ThisMonth.HighRain24Hours.Val = MetData.RainLast24Hour;
						Records.ThisMonth.HighRain24Hours.Ts = recDtTm;
						WriteMonthIniFile();
					}

					if (MetData.RainLast24Hour > Records.ThisYear.HighRain24Hours.Val)
					{
						Records.ThisYear.HighRain24Hours.Val = MetData.RainLast24Hour;
						Records.ThisYear.HighRain24Hours.Ts = recDtTm;
						WriteYearIniFile();
					}
				}
			}
			catch
			{
				// Unable to retrieve rain counter from 24 hours ago
				MetData.RainLast24Hour = 0;
			}
		}

		public void DoET(double value, DateTime timestamp)
		{
			// Value is annual total

			if (noET)
			{
				// Start of day ET value not yet set
				cumulus.LogMessage("*** First ET reading. Set startofdayET to total: " + value);
				MetData.StartofdayET = value;
				noET = false;
			}

			if (Math.Round(value, 3) < Math.Round(MetData.StartofdayET, 3)) // change b3046
			{
				// ET reset
				cumulus.LogMessage(string.Format("*** ET Reset *** AnnualET: {0:0.000}, StartofdayET: {1:0.000}, StationET: {2:0.000}, CurrentET: {3:0.000}", MetData.AnnualETTotal, MetData.StartofdayET, value, MetData.ET));
				MetData.AnnualETTotal = value; // add b3046
											   // set the start of day figure so it reflects the ET
											   // so far today
				MetData.StartofdayET = MetData.AnnualETTotal - MetData.ET;
				WriteTodayFile(timestamp, false);
				cumulus.LogMessage(string.Format("New ET values. AnnualET: {0:0.000}, StartofdayET: {1:0.000}, StationET: {2:0.000}, CurrentET: {3:0.000}", MetData.AnnualETTotal, MetData.StartofdayET, value, MetData.ET));
			}
			else
			{
				MetData.AnnualETTotal = value;
			}

			MetData.ET = MetData.AnnualETTotal - MetData.StartofdayET;

			HaveReadData = true;
		}

		public double? VapourPressureDeficit(int sensor)
		{
			int hum;
			double tempC;

			if (sensor == 0)
			{
				hum = MetData.Humidity;
				tempC = ConvertUnits.UserTempToC(MetData.Temperature);
			}
			else if (sensor <= 8 && MetData.ExtraHum[sensor].HasValue && MetData.ExtraTemp[sensor].HasValue)
			{
				hum = (int) MetData.ExtraHum[sensor].Value;
				tempC = ConvertUnits.UserTempToC(MetData.ExtraTemp[sensor].Value);
			}
			else
			{
				return null;
			}

			return ConvertUnits.PressMBToUser(MeteoLib.VapourPressureDeficit(tempC, hum));
		}

		// Calculates evapotranspiration based on the data for the last hour and updates the running annual total.
		public void CalculateEvapotranspiration(DateTime date)
		{
			cumulus.LogDebugMessage("Calculating ET from data");

			var dateFrom = date.AddHours(-1);

			// get the min and max temps, humidity, pressure, and mean solar rad and wind speed for the last hour
			var result = RecentDataDb.Query<EtData>("select avg(OutsideTemp) avgTemp, avg(Humidity) avgHum, avg(Pressure) avgPress, avg(SolarRad) avgSol, avg(SolarMax) avgSolMax, avg(WindSpeed) avgWind from RecentData where Timestamp >= ? order by Timestamp", dateFrom.ToUnixTime());

			// finally calculate the ETo
			var newET = MeteoLib.Evapotranspiration(
				ConvertUnits.UserTempToC(result[0].avgTemp),
				result[0].avgHum,
				result[0].avgSol,
				result[0].avgSolMax,
				ConvertUnits.UserWindToMS(result[0].avgWind),
				ConvertUnits.UserPressToHpa(result[0].avgPress) / 10
			);

			// convert to user units
			newET = ConvertUnits.RainMMToUser(newET);
			cumulus.LogDebugMessage($"Calculated ET for the last hour = {newET:F3}");

			// DoET expects the running annual total to be sent
			DoET(MetData.AnnualETTotal + newET, date);
		}

		public void DoCloudBaseHeatIndex(DateTime timestamp)
		{
			var tempinF = ConvertUnits.UserTempToF(MetData.Temperature);
			var tempinC = ConvertUnits.UserTempToC(MetData.Temperature);

			// Calculate cloud base
			MetData.CloudBase = (int) Math.Floor((tempinF - ConvertUnits.UserTempToF(MetData.Dewpoint)) / 4.4 * 1000 / (cumulus.CloudBaseInFeet ? 1 : 3.2808399));
			if (MetData.CloudBase < 0)
				MetData.CloudBase = 0;

			MetData.HeatIndex = ConvertUnits.TempCToUser(MeteoLib.HeatIndex(tempinC, MetData.Humidity));

			if (MetData.HeatIndex > DailyHighLow.Today.HighHeatIndex)
			{
				DailyHighLow.Today.HighHeatIndex = MetData.HeatIndex;
				DailyHighLow.Today.HighHeatIndexTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.HeatIndex > Records.ThisMonth.HighHeatIndex.Val)
			{
				Records.ThisMonth.HighHeatIndex.Val = MetData.HeatIndex;
				Records.ThisMonth.HighHeatIndex.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.HeatIndex > Records.ThisYear.HighHeatIndex.Val)
			{
				Records.ThisYear.HighHeatIndex.Val = MetData.HeatIndex;
				Records.ThisYear.HighHeatIndex.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.HeatIndex > Records.AllTime.HighHeatIndex.Val)
				SetAlltime(Records.AllTime.HighHeatIndex, MetData.HeatIndex, timestamp);

			CheckMonthlyAlltime("HighHeatIndex", MetData.HeatIndex, true, timestamp);


			// Find estimated wet bulb temp. First time this is called, required variables may not have been set up yet
			try
			{
				MetData.WetBulb = ConvertUnits.TempCToUser(MeteoLib.CalculateWetBulbC(tempinC, ConvertUnits.UserTempToC(MetData.Dewpoint), ConvertUnits.UserPressToMB(MetData.Pressure)));
			}
			catch
			{
				MetData.WetBulb = MetData.Temperature;
			}
		}

		public void DoForecast(string forecast, bool hourly)
		{
			// store weather station forecast if available
			MetData.WsForecast = forecast;

			if (cumulus.ForecastSource == 3)
			{
				MetData.ForecastStr = string.Empty;
			}
			else if (cumulus.ForecastSource == 2)
			{
				if ((DateTime.UtcNow - cumulus.LastForecastDotTxtReadTime).TotalMinutes > 10)
				{
					cumulus.GetForecastTextFromFile();
					cumulus.LastForecastDotTxtReadTime = DateTime.UtcNow;
				}
			}
			else if (cumulus.ForecastSource == 0)
			{
				// user wants to display station forecast
				MetData.ForecastStr = MetData.WsForecast;
			}

			// 1 = cumulus forecast

			// determine whether we need to update the Cumulus forecast; user may have chosen to only update once an hour, but
			// we still need to do that once to get an initial forecast
			if (!FirstForecastDone || !cumulus.HourlyForecast || hourly && cumulus.HourlyForecast)
			{
				int bartrend;
				if (MetData.PressTrendVal >= -cumulus.FCPressureThreshold && MetData.PressTrendVal <= cumulus.FCPressureThreshold)
					bartrend = 0;
				else if (MetData.PressTrendVal < 0)
					bartrend = 2;
				else
					bartrend = 1;

				string windDir;
				if (MetData.WindAverage < 0.1)
				{
					windDir = "calm";
				}
				else
				{
					windDir = MetData.AvgBearingText;
				}

				double lp;
				double hp;
				if (cumulus.FCpressinMB)
				{
					lp = cumulus.FClowpress;
					hp = cumulus.FChighpress;
				}
				else
				{
					lp = cumulus.FClowpress / 0.0295333727;
					hp = cumulus.FChighpress / 0.0295333727;
				}

				MetData.CumulusForecast = BetelCast(ConvertUnits.UserPressToHpa(MetData.Pressure), DateTime.Now.Month, windDir, bartrend, cumulus.Latitude > 0, hp, lp);

				// user wants to display Cumulus forecast
				if (cumulus.ForecastSource == 1)
				{
					MetData.ForecastStr = MetData.CumulusForecast;
				}
			}

			FirstForecastDone = true;
			HaveReadData = true;
		}
	}
}
