using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;

using EmbedIO;


namespace CumulusMX.Settings
{
	internal class LangSettings(Cumulus cumulus)
	{
		private readonly Cumulus cumulus = cumulus;

		public string GetAlpacaFormData()
		{
			// Build the settings data, convert to JSON, and return it

			var forecast = new ForecastStrings()
			{
				notavailable = cumulus.Trans.ForecastNotAvailable,
				exceptional = cumulus.Trans.Exceptional,
				forecast = cumulus.Trans.zForecast
			};

			var moonPhase = new MoonPhaseStrings()
			{
				Newmoon = cumulus.Trans.NewMoon,
				WaxingCrescent = cumulus.Trans.WaningCrescent,
				FirstQuarter = cumulus.Trans.FirstQuarter,
				WaxingGibbous = cumulus.Trans.WaxingGibbous,
				Fullmoon = cumulus.Trans.FullMoon,
				WaningGibbous = cumulus.Trans.WaningGibbous,
				LastQuarter = cumulus.Trans.LastQuarter,
				WaningCrescent = cumulus.Trans.WaningCrescent
			};

			var beaufort = new BeaufortStrings()
			{
				f0 = cumulus.Trans.Calm,
				f1 = cumulus.Trans.Lightair,
				f2 = cumulus.Trans.Lightbreeze,
				f3 = cumulus.Trans.Gentlebreeze,
				f4 = cumulus.Trans.Moderatebreeze,
				f5 = cumulus.Trans.Freshbreeze,
				f6 = cumulus.Trans.Strongbreeze,
				f7 = cumulus.Trans.Neargale,
				f8 = cumulus.Trans.Gale,
				f9 = cumulus.Trans.Stronggale,
				f10 = cumulus.Trans.Storm,
				f11 = cumulus.Trans.Violentstorm,
				f12 = cumulus.Trans.Hurricane
			};

			var trends = new TrendsStrings()
			{
				Risingveryrapidly = cumulus.Trans.Risingveryrapidly,
				Risingquickly = cumulus.Trans.Risingquickly,
				Rising = cumulus.Trans.Rising,
				Risingslowly = cumulus.Trans.Risingslowly,
				Steady = cumulus.Trans.Steady,
				Fallingslowly = cumulus.Trans.Fallingslowly,
				Falling = cumulus.Trans.Falling,
				Fallingquickly = cumulus.Trans.Fallingquickly,
				Fallingveryrapidly = cumulus.Trans.Fallingveryrapidly
			};

			var airQuality = new AirQuality()
			{
				sensor = cumulus.Trans.AirQualityCaptions,
				sensorAvg = cumulus.Trans.AirQualityAvgCaptions,
				sensor10 = cumulus.Trans.AirQuality10Captions,
				sensor10Avg = cumulus.Trans.AirQuality10AvgCaptions

			};

			var solar = new Solar()
			{
				lessdaylight = cumulus.Trans.thereWillBeMinSLessDaylightTomorrow,
				moredaylight = cumulus.Trans.thereWillBeMinSMoreDaylightTomorrow
			};

			// The first elment of forecast2 & 3 is always blank
			var davisForecast = new DavisForecast()
			{
				forecast1 = cumulus.Trans.DavisForecast1,
				forecast2 = cumulus.Trans.DavisForecast2.Skip(1).ToArray(),
				forecast3 = cumulus.Trans.DavisForecast3.Skip(1).ToArray()
			};

			var co2 = new Co2Captions()
			{
				Current = cumulus.Trans.CO2_CurrentCaption,
				Hr24 = cumulus.Trans.CO2_24HourCaption,
				Pm2p5 = cumulus.Trans.CO2_pm2p5Caption,
				Pm2p5_24hr = cumulus.Trans.CO2_pm2p5_24hrCaption,
				Pm10 = cumulus.Trans.CO2_pm10Caption,
				Pm10_24hr = cumulus.Trans.CO2_pm10_24hrCaption,
				Temperature = cumulus.Trans.CO2_TemperatureCaption,
				Humidity = cumulus.Trans.CO2_HumidityCaption
			};

			var alarmNames = new AlarmStrings()
			{
				windGustAbove = cumulus.HighGustAlarm.Name,
				pressureAbove = cumulus.HighPressAlarm.Name,
				tempAbove = cumulus.HighTempAlarm.Name,
				pressBelow = cumulus.LowPressAlarm.Name,
				tempBelow = cumulus.LowTempAlarm.Name,
				pressDown = cumulus.PressChangeAlarm.NameDown,
				pressUp = cumulus.PressChangeAlarm.NameUp,
				rainAbove = cumulus.HighRainTodayAlarm.Name,
				rainRateAbove = cumulus.HighRainRateAlarm.Name,
				sensorLost = cumulus.SensorAlarm.Name,
				tempDown = cumulus.TempChangeAlarm.NameDown,
				tempUp = cumulus.TempChangeAlarm.NameUp,
				windAbove = cumulus.HighWindAlarm.Name,
				dataStopped = cumulus.DataStoppedAlarm.Name,
				batteryLow = cumulus.BatteryLowAlarm.Name,
				dataSpike = cumulus.SpikeAlarm.Name,
				upgrade = cumulus.UpgradeAlarm.Name,
				firmware = cumulus.FirmwareAlarm.Name,
				httpStopped = cumulus.ThirdPartyAlarm.Name,
				mySqlStopped = cumulus.MySqlUploadAlarm.Name,
				newRecord = cumulus.NewRecordAlarm.Name,
				ftpStopped = cumulus.FtpAlarm.Name,
				genError = cumulus.ErrorAlarm.Name
			};

			var alarmEmail = new AlarmStrings()
			{
				windGustAbove = cumulus.HighGustAlarm.EmailMsg,
				pressureAbove = cumulus.HighPressAlarm.EmailMsg,
				tempAbove = cumulus.HighTempAlarm.EmailMsg,
				pressBelow = cumulus.LowPressAlarm.EmailMsg,
				tempBelow = cumulus.LowTempAlarm.EmailMsg,
				pressDown = cumulus.PressChangeAlarm.EmailMsgDn,
				pressUp = cumulus.PressChangeAlarm.EmailMsgUp,
				rainAbove = cumulus.HighRainTodayAlarm.EmailMsg,
				rainRateAbove = cumulus.HighRainRateAlarm.EmailMsg,
				sensorLost = cumulus.SensorAlarm.EmailMsg,
				tempDown = cumulus.TempChangeAlarm.EmailMsgDn,
				tempUp = cumulus.TempChangeAlarm.EmailMsgUp,
				windAbove = cumulus.HighWindAlarm.EmailMsg,
				dataStopped = cumulus.DataStoppedAlarm.EmailMsg,
				batteryLow = cumulus.BatteryLowAlarm.EmailMsg,
				dataSpike = cumulus.SpikeAlarm.EmailMsg,
				upgrade = cumulus.UpgradeAlarm.EmailMsg,
				firmware = cumulus.FirmwareAlarm.EmailMsg,
				httpStopped = cumulus.ThirdPartyAlarm.EmailMsg,
				mySqlStopped = cumulus.MySqlUploadAlarm.EmailMsg,
				newRecord = cumulus.NewRecordAlarm.EmailMsg,
				ftpStopped = cumulus.FtpAlarm.EmailMsg,
				genError = cumulus.ErrorAlarm.EmailMsg
			};

			var alarmSettings = new AlarmSettings()
			{
				subject = cumulus.Trans.AlarmEmailSubject,
				preamble = cumulus.Trans.AlarmEmailPreamble,
				names = alarmNames,
				email = alarmEmail
			};

			var webtags = new WebTags()
			{
				gentimedate = cumulus.Trans.WebTagGenTimeDate,
				gendate = cumulus.Trans.WebTagGenDate,
				gentime = cumulus.Trans.WebTagGenTime,
				recdate = cumulus.Trans.WebTagRecDate,
				rectimedate = cumulus.Trans.WebTagRecTimeDate,
				recwetdrytimedate = cumulus.Trans.WebTagRecDryWetDate,
				elapsedtime = cumulus.Trans.WebTagElapsedTime
			};

			var snow = new Snow()
			{
				snowdepth = cumulus.Trans.SnowDepth,
				snow24h = cumulus.Trans.Snow24h
			};

			var settings = new Settings()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				forecast = forecast,
				moonPhase = moonPhase,
				beaufort = beaufort,
				trends = trends,
				compass = cumulus.Trans.compassp,
				extraTemp = cumulus.Trans.ExtraTempCaptions,
				extraHum = cumulus.Trans.ExtraHumCaptions,
				extraDP = cumulus.Trans.ExtraDPCaptions,
				userTemp = cumulus.Trans.UserTempCaptions,
				soilTemp = cumulus.Trans.SoilTempCaptions,
				soilMoist = cumulus.Trans.SoilMoistureCaptions,
				leafWet = cumulus.Trans.LeafWetnessCaptions,
				airQuality = airQuality,
				solar = solar,
				davisForecast = davisForecast,
				co2 = co2,
				alarms = alarmSettings,
				webtags = webtags,
				snow = snow,
				laser = cumulus.Trans.LaserCaptions
			};

			return JsonSerializer.Serialize(settings);
		}

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = string.Empty;
			var json = string.Empty;
			Settings settings;
			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data[5..]);

				// de-serialize it to the settings structure
				settings = JsonSerializer.Deserialize<Settings>(json);
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Localisation Settings JSON: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Localisation Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}

			// process the settings
			try
			{
				cumulus.LogMessage("Updating localisation settings");

				// Zambretti forecast settings
				try
				{
					cumulus.Trans.ForecastNotAvailable = settings.forecast.notavailable.Trim();
					cumulus.Trans.Exceptional = settings.forecast.exceptional.Trim();
					cumulus.Trans.zForecast = settings.forecast.forecast;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Zambretti forecast settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Moon Phase
				try
				{
					cumulus.Trans.NewMoon = settings.moonPhase.Newmoon.Trim();
					cumulus.Trans.WaningCrescent = settings.moonPhase.WaxingCrescent.Trim();
					cumulus.Trans.FirstQuarter = settings.moonPhase.FirstQuarter.Trim();
					cumulus.Trans.WaxingGibbous = settings.moonPhase.WaxingGibbous.Trim();
					cumulus.Trans.FullMoon = settings.moonPhase.Fullmoon.Trim();
					cumulus.Trans.WaningGibbous = settings.moonPhase.WaningGibbous.Trim();
					cumulus.Trans.LastQuarter = settings.moonPhase.LastQuarter.Trim();
					cumulus.Trans.WaningCrescent = settings.moonPhase.WaningCrescent.Trim();
				}
				catch (Exception ex)
				{
					var msg = "Error processing Moon phase settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Beaufort
				try
				{
					cumulus.Trans.Calm = settings.beaufort.f0.Trim();
					cumulus.Trans.Lightair = settings.beaufort.f1.Trim();
					cumulus.Trans.Lightbreeze = settings.beaufort.f2.Trim();
					cumulus.Trans.Gentlebreeze = settings.beaufort.f3.Trim();
					cumulus.Trans.Moderatebreeze = settings.beaufort.f4.Trim();
					cumulus.Trans.Freshbreeze = settings.beaufort.f5.Trim();
					cumulus.Trans.Strongbreeze = settings.beaufort.f6.Trim();
					cumulus.Trans.Neargale = settings.beaufort.f7.Trim();
					cumulus.Trans.Gale = settings.beaufort.f8.Trim();
					cumulus.Trans.Stronggale = settings.beaufort.f9.Trim();
					cumulus.Trans.Storm = settings.beaufort.f10.Trim();
					cumulus.Trans.Violentstorm = settings.beaufort.f11.Trim();
					cumulus.Trans.Hurricane = settings.beaufort.f12.Trim();
				}
				catch (Exception ex)
				{
					var msg = "Error processing Beaufort settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// trends
				try
				{
					cumulus.Trans.Risingveryrapidly = settings.trends.Risingveryrapidly.Trim();
					cumulus.Trans.Risingquickly = settings.trends.Risingquickly.Trim();
					cumulus.Trans.Rising = settings.trends.Rising.Trim();
					cumulus.Trans.Risingslowly = settings.trends.Risingslowly.Trim();
					cumulus.Trans.Steady = settings.trends.Steady.Trim();
					cumulus.Trans.Fallingslowly = settings.trends.Fallingslowly.Trim();
					cumulus.Trans.Falling = settings.trends.Falling.Trim();
					cumulus.Trans.Fallingquickly = settings.trends.Fallingquickly.Trim();
					cumulus.Trans.Fallingveryrapidly = settings.trends.Fallingveryrapidly.Trim();
				}
				catch (Exception ex)
				{
					var msg = "Error processing Trend settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// air quality
				try
				{
					cumulus.Trans.AirQualityCaptions = settings.airQuality.sensor;
					cumulus.Trans.AirQualityAvgCaptions = settings.airQuality.sensorAvg;
					cumulus.Trans.AirQuality10Captions = settings.airQuality.sensor10;
					cumulus.Trans.AirQuality10AvgCaptions = settings.airQuality.sensor10Avg;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Air Quality settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// solar
				try
				{
					cumulus.Trans.thereWillBeMinSLessDaylightTomorrow = settings.solar.lessdaylight.Trim();
					cumulus.Trans.thereWillBeMinSMoreDaylightTomorrow = settings.solar.moredaylight.Trim();
				}
				catch (Exception ex)
				{
					var msg = "Error processing Solar settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Davis forecast
				// The first elment of forecast2 & 3 is always blank and not returned by the settings page (it isn't sent to it in the first place)
				try
				{
					cumulus.Trans.DavisForecast1 = settings.davisForecast.forecast1;
					for (var i = 1; i <= settings.davisForecast.forecast2.Length; i++)
						cumulus.Trans.DavisForecast2[i] = settings.davisForecast.forecast2[i - 1].Trim();
					for (var i = 1; i <= settings.davisForecast.forecast3.Length; i++)
						cumulus.Trans.DavisForecast3[i] = settings.davisForecast.forecast3[i - 1].Trim();
				}
				catch (Exception ex)
				{
					var msg = "Error processing Davis Forecast settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Extra Sensors
				try
				{
					cumulus.Trans.ExtraTempCaptions = settings.extraTemp;
					cumulus.Trans.ExtraHumCaptions = settings.extraHum;
					cumulus.Trans.ExtraDPCaptions = settings.extraDP;
					cumulus.Trans.UserTempCaptions = settings.userTemp;
					cumulus.Trans.SoilTempCaptions = settings.soilTemp;
					cumulus.Trans.SoilMoistureCaptions = settings.soilMoist;
					cumulus.Trans.LeafWetnessCaptions = settings.leafWet;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Extra Sensor Names: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// CO2
				try
				{
					cumulus.Trans.CO2_CurrentCaption = settings.co2.Current.Trim();
					cumulus.Trans.CO2_24HourCaption = settings.co2.Hr24.Trim();
					cumulus.Trans.CO2_pm2p5Caption = settings.co2.Pm2p5.Trim();
					cumulus.Trans.CO2_pm2p5_24hrCaption = settings.co2.Pm2p5_24hr.Trim();
					cumulus.Trans.CO2_pm10Caption = settings.co2.Pm10.Trim();
					cumulus.Trans.CO2_pm10_24hrCaption = settings.co2.Pm10_24hr.Trim();
					cumulus.Trans.CO2_TemperatureCaption = settings.co2.Temperature.Trim();
					cumulus.Trans.CO2_HumidityCaption = settings.co2.Humidity.Trim();
				}
				catch (Exception ex)
				{
					var msg = "Error processing CO2 settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Alarms
				try
				{
					cumulus.Trans.AlarmEmailSubject = settings.alarms.subject.Trim();
					cumulus.Trans.AlarmEmailPreamble = settings.alarms.preamble.Trim();
					// Names
					cumulus.HighGustAlarm.Name = settings.alarms.names.windGustAbove.Trim();
					cumulus.HighPressAlarm.Name = settings.alarms.names.pressureAbove.Trim();
					cumulus.HighTempAlarm.Name = settings.alarms.names.tempAbove.Trim();
					cumulus.LowPressAlarm.Name = settings.alarms.names.pressBelow.Trim();
					cumulus.LowTempAlarm.Name = settings.alarms.names.tempBelow.Trim();
					cumulus.PressChangeAlarm.NameDown = settings.alarms.names.pressDown.Trim();
					cumulus.PressChangeAlarm.NameUp = settings.alarms.names.pressUp.Trim();
					cumulus.HighRainTodayAlarm.Name = settings.alarms.names.rainAbove.Trim();
					cumulus.HighRainRateAlarm.Name = settings.alarms.names.rainRateAbove.Trim();
					cumulus.SensorAlarm.Name = settings.alarms.names.sensorLost.Trim();
					cumulus.TempChangeAlarm.NameDown = settings.alarms.names.tempDown.Trim();
					cumulus.TempChangeAlarm.NameUp = settings.alarms.names.tempUp.Trim();
					cumulus.HighWindAlarm.Name = settings.alarms.names.windAbove.Trim();
					cumulus.DataStoppedAlarm.Name = settings.alarms.names.dataStopped.Trim();
					cumulus.BatteryLowAlarm.Name = settings.alarms.names.batteryLow.Trim();
					cumulus.SpikeAlarm.Name = settings.alarms.names.dataSpike.Trim();
					cumulus.UpgradeAlarm.Name = settings.alarms.names.upgrade.Trim();
					cumulus.FirmwareAlarm.Name = settings.alarms.names.firmware.Trim();
					cumulus.ThirdPartyAlarm.Name = settings.alarms.names.httpStopped.Trim();
					cumulus.MySqlUploadAlarm.Name = settings.alarms.names.mySqlStopped.Trim();
					cumulus.NewRecordAlarm.Name = settings.alarms.names.newRecord.Trim();
					cumulus.FtpAlarm.Name = settings.alarms.names.ftpStopped.Trim();
					cumulus.ErrorAlarm.Name = settings.alarms.names.genError.Trim();
					// Email
					cumulus.HighGustAlarm.EmailMsg = settings.alarms.email.windGustAbove.Trim();
					cumulus.HighPressAlarm.EmailMsg = settings.alarms.email.pressureAbove.Trim();
					cumulus.HighTempAlarm.EmailMsg = settings.alarms.email.tempAbove.Trim();
					cumulus.LowPressAlarm.EmailMsg = settings.alarms.email.pressBelow.Trim();
					cumulus.LowTempAlarm.EmailMsg = settings.alarms.email.tempBelow.Trim();
					cumulus.PressChangeAlarm.EmailMsgDn = settings.alarms.email.pressDown.Trim();
					cumulus.PressChangeAlarm.EmailMsgUp = settings.alarms.email.pressUp.Trim();
					cumulus.HighRainTodayAlarm.EmailMsg = settings.alarms.email.rainAbove.Trim();
					cumulus.HighRainRateAlarm.EmailMsg = settings.alarms.email.rainRateAbove.Trim();
					cumulus.SensorAlarm.EmailMsg = settings.alarms.email.sensorLost.Trim();
					cumulus.TempChangeAlarm.EmailMsgDn = settings.alarms.email.tempDown.Trim();
					cumulus.TempChangeAlarm.EmailMsgUp = settings.alarms.email.tempUp.Trim();
					cumulus.HighWindAlarm.EmailMsg = settings.alarms.email.windAbove.Trim();
					cumulus.DataStoppedAlarm.EmailMsg = settings.alarms.email.dataStopped.Trim();
					cumulus.BatteryLowAlarm.EmailMsg = settings.alarms.email.batteryLow.Trim();
					cumulus.SpikeAlarm.EmailMsg = settings.alarms.email.dataSpike.Trim();
					cumulus.UpgradeAlarm.EmailMsg = settings.alarms.email.upgrade.Trim();
					cumulus.FirmwareAlarm.EmailMsg = settings.alarms.email.firmware.Trim();
					cumulus.ThirdPartyAlarm.EmailMsg = settings.alarms.email.httpStopped.Trim();
					cumulus.MySqlUploadAlarm.EmailMsg = settings.alarms.email.mySqlStopped.Trim();
					cumulus.NewRecordAlarm.EmailMsg = settings.alarms.email.newRecord.Trim();
					cumulus.FtpAlarm.EmailMsg = settings.alarms.email.ftpStopped.Trim();
					cumulus.ErrorAlarm.EmailMsg = settings.alarms.email.genError.Trim();
				}
				catch (Exception ex)
				{
					var msg = "Error processing Alarm settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// web tags
				try
				{
					cumulus.Trans.WebTagGenTimeDate = settings.webtags.gentimedate;
					cumulus.Trans.WebTagGenDate = settings.webtags.gendate;
					cumulus.Trans.WebTagGenTime = settings.webtags.gentime;
					cumulus.Trans.WebTagRecDate = settings.webtags.recdate;
					cumulus.Trans.WebTagRecTimeDate = settings.webtags.rectimedate;
					cumulus.Trans.WebTagRecDryWetDate = settings.webtags.recwetdrytimedate;
					cumulus.Trans.WebTagElapsedTime = settings.webtags.elapsedtime;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Web Tag settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// snow
				try
				{
					cumulus.Trans.SnowDepth = settings.snow.snowdepth;
					cumulus.Trans.Snow24h = settings.snow.snow24h;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Snow settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// laser
				try
				{
					cumulus.Trans.LaserCaptions = settings.laser;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Laser settings: " + ex.Message;
					cumulus.LogErrorMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}


				// Save the settings
				cumulus.WriteStringsFile();
			}
			catch (Exception ex)
			{
				var msg = "Error processing Localisation settings: " + ex.Message;
				cumulus.LogErrorMessage(msg);
				cumulus.LogDebugMessage("Localisation Data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;

		}


		private sealed class ForecastStrings
		{
			public string notavailable { get; set; }
			public string exceptional { get; set; }
			public string[] forecast { get; set; }
		}

		private sealed class MoonPhaseStrings
		{
			public string Newmoon { get; set; }
			public string WaxingCrescent { get; set; }
			public string FirstQuarter { get; set; }
			public string WaxingGibbous { get; set; }
			public string Fullmoon { get; set; }
			public string WaningGibbous { get; set; }
			public string LastQuarter { get; set; }
			public string WaningCrescent { get; set; }
		}

		private sealed class BeaufortStrings
		{
			public string f0 { get; set; }
			public string f1 { get; set; }
			public string f2 { get; set; }
			public string f3 { get; set; }
			public string f4 { get; set; }
			public string f5 { get; set; }
			public string f6 { get; set; }
			public string f7 { get; set; }
			public string f8 { get; set; }
			public string f9 { get; set; }
			public string f10 { get; set; }
			public string f11 { get; set; }
			public string f12 { get; set; }
		}

		private sealed class TrendsStrings
		{
			public string Risingveryrapidly { get; set; }
			public string Risingquickly { get; set; }
			public string Rising { get; set; }
			public string Risingslowly { get; set; }
			public string Steady { get; set; }
			public string Fallingslowly { get; set; }
			public string Falling { get; set; }
			public string Fallingquickly { get; set; }
			public string Fallingveryrapidly { get; set; }
		}

		private sealed class AirQuality
		{
			public string[] sensor { get; set; }
			public string[] sensorAvg { get; set; }
			public string[] sensor10 { get; set; }
			public string[] sensor10Avg { get; set; }
		}

		private sealed class Solar
		{
			public string lessdaylight { get; set; }
			public string moredaylight { get; set; }
		}

		private sealed class DavisForecast
		{
			public string[] forecast1 { get; set; }
			public string[] forecast2 { get; set; }
			public string[] forecast3 { get; set; }
		}

		private sealed class Co2Captions
		{
			public string Current { get; set; }
			public string Hr24 { get; set; }
			public string Pm2p5 { get; set; }
			public string Pm2p5_24hr { get; set; }
			public string Pm10 { get; set; }
			public string Pm10_24hr { get; set; }
			public string Temperature { get; set; }
			public string Humidity { get; set; }
		}

		private sealed class AlarmSettings
		{
			public string subject { get; set; }
			public string preamble { get; set; }
			public AlarmStrings names { get; set; }
			public AlarmStrings email { get; set; }
		}

		private sealed class AlarmStrings
		{
			public string windGustAbove { get; set; }
			public string pressureAbove { get; set; }
			public string tempAbove { get; set; }
			public string pressBelow { get; set; }
			public string tempBelow { get; set; }
			public string pressDown { get; set; }
			public string pressUp { get; set; }
			public string rainAbove { get; set; }
			public string rainRateAbove { get; set; }
			public string sensorLost { get; set; }
			public string tempDown { get; set; }
			public string tempUp { get; set; }
			public string windAbove { get; set; }
			public string dataStopped { get; set; }
			public string batteryLow { get; set; }
			public string dataSpike { get; set; }
			public string upgrade { get; set; }
			public string firmware { get; set; }
			public string httpStopped { get; set; }
			public string mySqlStopped { get; set; }
			public string newRecord { get; set; }
			public string ftpStopped { get; set; }
			public string genError { get; set; }
		}

		private sealed class WebTags
		{
			public string gentimedate { get; set; }
			public string gentime { get; set; }
			public string gendate { get; set; }
			public string recdate { get; set; }
			public string rectimedate { get; set; }
			public string recwetdrytimedate { get; set; }
			public string elapsedtime {  get; set; }
		}

		private sealed class Snow
		{
			public string snowdepth { get; set; }
			public string snow24h { get; set; }
		}

		private sealed class Settings
		{
			public bool accessible { get; set; }
			public ForecastStrings forecast { get; set; }
			public MoonPhaseStrings moonPhase { get; set; }
			public BeaufortStrings beaufort { get; set; }
			public TrendsStrings trends { get; set; }
			public string[] compass { get; set; }
			public string[] extraTemp { get; set; }
			public string[] extraHum { get; set; }
			public string[] extraDP { get; set; }
			public string[] userTemp { get; set; }
			public string[] soilTemp { get; set; }
			public string[] soilMoist { get; set; }
			public string[] leafWet { get; set; }
			public AirQuality airQuality { get; set; }
			public Solar solar { get; set; }
			public DavisForecast davisForecast { get; set; }
			public Co2Captions co2 { get; set; }
			public AlarmSettings alarms { get; set; }
			public WebTags webtags { get; set; }
			public Snow snow { get; set; }
			public string[] laser { get; set; }
		}
	}
}
