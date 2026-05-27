using System;
using System.Collections.Generic;
using System.Globalization;

namespace CumulusMX
{
	public partial class Cumulus
	{
		internal void ApplyHistoricData(List<HistoricDataRecord> history, WeatherStation station)
		{
			station.LastDataReadTime = LastUpdateTime;
			var luhour = station.LastDataReadTime.Hour;
			var snowhourdone = luhour == SnowDepthHour;


			foreach (var rec in history)
			{
				if (Program.ExitSystemToken.IsCancellationRequested)
				{
					return;
				}

				if (rec.Timestamp < station.LastDataReadTime)
				{
					LogMessage("ApplyHistoricData: Ignoring old archive data");
					continue;
				}

				if (rec.Timestamp > DateTime.Now)
				{
					// do no process reocrds from the future!
					LogDebugMessage("ApplyHistoricData: Warning - Skipping record with a future date: " + rec.Timestamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
					continue;
				}


				LogMessage("ApplyHistoricData: Processing archive record for " + rec.Timestamp);

				station.DataDateTime = rec.Timestamp;

				// Not in snow hour, snow yet to be done
				if (rec.Timestamp.Hour != SnowDepthHour)
				{
					snowhourdone = false;
				}
				else if (!snowhourdone)
				{
					// snowhour items
					if (SnowAutomated > 0)
					{
						station.CreateNewSnowRecord(rec.Timestamp);
					}

					// reset the accumulated snow depth(s)
					for (var i = 0; i < MetData.Snow24h.Length; i++)
					{
						MetData.Snow24h[i] = MetData.LaserDepth[i].HasValue ? 0 : null;
					}

					snowhourdone = true;
				}

				// === Wind ==
				// WindGust = max for period
				// WindSpd = avg for period
				// WindDir = avg for period
				if (rec.StationIndex == SensorMaps.Wind)
				{
					try
					{
						if (rec.WindGust.HasValue && rec.WindSpeed.HasValue && rec.WindBearing.HasValue)
						{
							station.DoWind(rec.WindGust.Value, rec.WindBearing.Value, rec.WindSpeed.Value, rec.Timestamp);
						}
						else
						{
							LogWarningMessage("ApplyHistoricData: Insufficient data process wind");
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage("ApplyHistoricData: Error in Wind data - " + ex.Message);
					}
				}

				// === Indoor Humidity ===
				if (rec.StationIndex == SensorMaps.IndoorHum)
				{
					try
					{
						if (rec.IndoorHum.HasValue)
						{
							station.DoIndoorHumidity(rec.IndoorHum.Value);

							// user has mapped indoor humidity to outdoor
							if (SensorMaps.PrimaryTempHum == 99)
							{
								station.DoOutdoorHumidity(rec.IndoorHum.Value, rec.Timestamp);
							}
						}
						else
						{
							LogWarningMessage("ApplyHistoricData: Missing indoor humidity data");
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage("ApplyHistoricData: Error in indoor Humidity data - " + ex.Message);
					}
				}

				// === Humidity ===
				if (rec.StationIndex == SensorMaps.Humidity)
				{
					try
					{
						if (SensorMaps.PrimaryTempHum == 0)
						{
							if (rec.Humidity.HasValue)
							{
								station.DoOutdoorHumidity(rec.Humidity.Value, rec.Timestamp);
							}
							else
							{
								LogWarningMessage("ApplyHistoricData: Missing outdoor humidity data");
							}
						}

					}
					catch (Exception ex)
					{
						LogErrorMessage("ApplyHistoricData: Error in Humidity data - " + ex.Message);
					}
				}

				// === Pressure ===
				if (rec.StationIndex == SensorMaps.Pressure)
				{
					try
					{
						if (rec.Pressure.HasValue)
						{
							if (!StationOptions.CalculateSLP)
							{
								var pressVal = (double) rec.Pressure;
								station.DoPressure(pressVal, rec.Timestamp);
							}
						}
						else
						{
							LogWarningMessage("ApplyHistoricData: Missing relative pressure data");
						}

						if (rec.StationPressure.HasValue)
						{
							station.DoStationPressure((double) rec.StationPressure);
							// Leave CMX calculated SLP until the end as it uses Temperature
						}
						else
						{
							LogWarningMessage("ApplyHistoricData: Missing absolute pressure data");
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage("ApplyHistoricData: Error in Pressure data - " + ex.Message);
					}
				}

				// === Indoor temp ===
				if (rec.StationIndex == SensorMaps.IndoorTemp)
				{
					try
					{
						if (rec.IndoorTemp.HasValue)
						{
							var tempVal = (double) rec.IndoorTemp;
							// user has mapped indoor temperature to outdoor
							if (SensorMaps.PrimaryTempHum == 99)
							{
								station.DoOutdoorTemp(tempVal, rec.Timestamp);
							}

							if (SensorMaps.PrimaryIndoorTempHum == 0)
							{
								station.DoIndoorTemp(tempVal);
							}
						}
						else
						{
							LogWarningMessage("ApplyHistoricData: Missing indoor temperature data");
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage("ApplyHistoricData: Error in Indoor temp data - " + ex.Message);
					}
				}
				// === Outdoor temp ===
				if (rec.StationIndex == SensorMaps.Temperature)
				{
					try
					{
						if (SensorMaps.PrimaryTempHum == 0)
						{
							if (rec.Temperature.HasValue)
							{
								station.DoOutdoorTemp(rec.Temperature.Value, rec.Timestamp);
							}
							else
							{
								LogWarningMessage("ApplyHistoricData: Missing outdoor temperature data");
							}
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage("ApplyHistoricData: Error in Outdoor temp data - " + ex.Message);
					}
				}

				// === Rain ===
				if (rec.StationIndex == SensorMaps.Rain)
				{
					try
					{
						double rRate = 0;
						if (rec.RainRate.HasValue)
						{
							// we have a rain rate, so we will NOT calculate it
							station.calculaterainrate = false;
							rRate = (double) rec.RainRate;
						}
						else
						{
							// No rain rate, so we will calculate it
							station.calculaterainrate = true;
						}

						if (rec.RainCounter.HasValue)
						{
							var rainVal = (double) rec.RainCounter;
							var rateVal = rRate;
							station.DoRain(rainVal, rateVal, rec.Timestamp);
						}
						else
						{
							LogWarningMessage("ApplyHistoricData: Missing rain data");
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage("ApplyHistoricData: Error in Rain data - " + ex.Message);
					}
				}

				// === Solar ===
				try
				{
					if (rec.StationIndex == SensorMaps.Solar)
					{
						station.DoSolarRad(rec.Solar, rec.Timestamp);
					}
				}
				catch (Exception ex)
				{
					LogErrorMessage("ApplyHistoricData: Error in Solar data - " + ex.Message);
				}

				// === UVI ===
				try
				{
					if (rec.StationIndex == SensorMaps.UV)
					{
						station.DoUV(rec.UV, rec.Timestamp);
					}
				}
				catch (Exception ex)
				{
					LogErrorMessage("ApplyHistoricData: Error in Solar data - " + ex.Message);
				}

				// === Black Globe Temperature ===
				try
				{
					if (rec.StationIndex == SensorMaps.BlackGlobe)
					{
						station.DoBGT(rec.BGT, rec.Timestamp);
						station.DoWBGT(rec.WBGT.Value, rec.Timestamp);
					}
				}
				catch (Exception ex)
				{
					LogErrorMessage("ApplyHistoricData: Error in BGT data - " + ex.Message);
				}

				// multi-sensor values

				// === Extra Temp/Hum ===
				for (var i = 0; i < rec.ExtraTemp.Length; i++)
				{
					var chan = i + 1;

					// Extra Humidity first in case it is mapped to Outdoor and needed for dewpoint calculation
					try
					{
						if (rec.StationIndex == SensorMaps.ExtraTempHum[i])
						{
							WeatherStation.DoExtraHum(rec.ExtraHum[i], chan);

							if (rec.ExtraHum[i].HasValue)
							{
								if (SensorMaps.PrimaryTempHum == chan)
								{
									station.DoOutdoorHumidity(rec.ExtraHum[i].Value, rec.Timestamp);
								}

								if (SensorMaps.PrimaryIndoorTempHum == chan)
								{
									station.DoIndoorHumidity(rec.ExtraHum[i].Value);
								}
							}
							else if (SensorMaps.PrimaryTempHum == chan)
							{
								LogErrorMessage($"ApplyHistoricData: Missing Extra humidity #{chan} mapped to outdoor humidity data");
							}
							else if (SensorMaps.PrimaryIndoorTempHum == chan)
							{
								LogErrorMessage($"ApplyHistoricData: Missing Extra humidity #{chan} mapped to indoor humidity data");
							}
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"ApplyHistoricData: Error in extra humidity data - {ex.Message}");
					}

					// === Extra Temperature ===
					try
					{
						if (rec.StationIndex == SensorMaps.ExtraTempHum[i])
						{
							var tempVal = rec.ExtraTemp[i];

							WeatherStation.DoExtraTemp(tempVal, chan);

							if (tempVal.HasValue)
							{
								if (SensorMaps.PrimaryTempHum == chan)
								{
									station.DoOutdoorTemp(tempVal.Value, rec.Timestamp);
								}

								if (SensorMaps.PrimaryIndoorTempHum == chan)
								{
									station.DoIndoorTemp(tempVal.Value);
								}
							}
							else if (SensorMaps.PrimaryTempHum == chan)
							{
								LogErrorMessage($"ApplyHistoricData: Missing Extra temperature #{chan} mapped to outdoor temperature data");
							}
							else if (SensorMaps.PrimaryIndoorTempHum == chan)
							{
								LogErrorMessage($"ApplyHistoricData: Missing Extra temperature #{chan} mapped to indoor temperature data");
							}
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"ApplyHistoricData: Error in extra temperature data - {ex.Message}");
					}

					// === Extra Dewpoint ===
					if (rec.ExtraTemp[i].HasValue && rec.ExtraHum[i].HasValue)
					{
						WeatherStation.DoExtraDP(ConvertUnits.TempCToUser(MeteoLib.DewPoint(ConvertUnits.UserTempToC(rec.ExtraTemp[i].Value), rec.ExtraHum[i].Value)), chan);
					}
					else
					{
						WeatherStation.DoExtraDP(null, chan);
					}
				}

				// === User Temperature ===
				for (var i = 0; i < rec.UserTemp.Length;)
				{
					try
					{
						if (rec.StationIndex == SensorMaps.UserTemp[i])
						{
							var chan = i + 1;
							if (EcowittMapWN34[i] == 0)
							{
								WeatherStation.DoUserTemp(rec.UserTemp[i], chan);
							}
							else
							{
								WeatherStation.DoSoilTemp(rec.UserTemp[i], EcowittMapWN34[i]);
							}
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"ApplyHistoricData: Error in extra user temperature data - {ex.Message}");
					}
				}

				// === Leaf Wetness ===
				for (var i = 0; i < rec.LeafWet.Length; i++)
				{
					try
					{
						if (rec.StationIndex == SensorMaps.LeafWet[i])
						{
							var chan = i + 1;
							station.DoLeafWetness(rec.LeafWet[i], chan);
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"ApplyHistoricData: Error in leaf wetness data - {ex.Message}");
					}
				}

				// === Soil Moisture ===
				for (var i = 0; i < rec.SoilMoist.Length; i++)
				{
					try
					{
						if (rec.StationIndex == SensorMaps.SoilMoist[i])
						{
							var chan = i + 1;
							WeatherStation.DoSoilMoisture((double) rec.SoilMoist[i], chan);
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"ApplyHistoricData: Error in soil moisture data - {ex.Message}");
					}
				}

				// === Soil Temperature ===
				for (var i = 0; i < rec.SoilTemp.Length; i++)
				{
					try
					{
						if (rec.StationIndex == SensorMaps.SoilMoist[i])
						{
							var chan = i + 1;
							WeatherStation.DoSoilTemp((double) rec.SoilTemp[i], chan);
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"ApplyHistoricData: Error in soil temperature data - {ex.Message}");
					}
				}

				// === Soil EC ===
				for (var i = 0; i < rec.SoilEc.Length; i++)
				{
					try
					{
						if (rec.StationIndex == SensorMaps.SoilMoist[i])
						{
							var chan = i + 1;
							WeatherStation.DoSoilEc(rec.SoilEc[i], chan);
						}
					}
					catch (Exception ex)
					{
						LogErrorMessage($"ApplyHistoricData: Error in soil EC data - {ex.Message}");
					}
				}

			}
		}
	}

	internal class HistoricDataRecord
	{
		public DateTime Timestamp;
		public int Interval;
		public int StationIndex;
		public double? WindGust;
		public double? WindSpeed;
		public int? WindBearing;
		public double? Temperature;
		public double? DewPoint;
		public double? FeelsLike;
		public double? BGT;
		public double? WBGT;
		public int? Humidity;
		public double? Pressure;
		public double? StationPressure;
		public double? RainRate;
		public double? RainCounter;
		public int? Solar;
		public double? UV;
		public double? IndoorTemp;
		public int? IndoorHum;
		// extra sensors
		public double?[] ExtraTemp = new double?[16];
		public int?[] ExtraHum = new int?[16];
		public double?[] ExtraDewPoint = new double?[16];
		public double?[] SoilTemp = new double?[16];
		public int?[] SoilMoist = new int?[16];
		public int?[] SoilEc = new int?[16];
		public double?[] UserTemp = new double?[8];
		public double?[] LeafWet = new double?[8];
		public double?[] Pm2p5 = new double?[4];
		public double?[] Pm2p5Avg = new double?[4];
		public double?[] Pm10 = new double?[4];
		public double?[] Pm10Avg = new double?[4];
		public int? CO2;
		public int? CO2hr24;
		public double? CO2Pm2p5;
		public double? CO2Pm2p5Avg;
		public double? CO2Pm10;
		public double? CO2Pm10Avg;
		public double? CO2Temp;
		public int? CO2Hum;
		public int? IndoorCo2;
		public int? IndoorCo2hr24;
		public double?[] LaserDist = new double?[4];
		public double?[] LaserDepth = new double?[4];
		public int? LightningCount;
		public double? LightningDist;
		public DateTime LightningTime;
	}
}
