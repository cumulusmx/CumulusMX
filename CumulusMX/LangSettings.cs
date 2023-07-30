using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web.UI;
using EmbedIO;
using Org.BouncyCastle.Crypto;
using ServiceStack;
using ServiceStack.Text;
using SQLite;

namespace CumulusMX
{
	internal class LangSettings
	{
		private readonly Cumulus cumulus;

		public LangSettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

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
				sensorAvg = cumulus.Trans.AirQualityAvgCaptions
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
				Pm10_24hr = cumulus.Trans.CO2_pm10_24hrCaption
			};

			var alarms = new AlarmEmails()
			{
				subject = cumulus.Trans.AlarmEmailSubject,
				preamble = cumulus.Trans.AlarmEmailPreamble,
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
				httpStopped = cumulus.ThirdPartyAlarm.EmailMsg,
				mySqlStopped = cumulus.MySqlUploadAlarm.EmailMsg,
				newRecord = cumulus.NewRecordAlarm.EmailMsg,
				ftpStopped =cumulus.FtpAlarm.EmailMsg
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
				alarms= alarms
			};


			return JsonSerializer.SerializeToString(settings);
		}

		public string UpdateConfig(IHttpContext context)
		{
			var errorMsg = "";
			var json = "";
			Settings settings;
			context.Response.StatusCode = 200;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<Settings>();
			}
			catch (Exception ex)
			{
				var msg = "Error de-serializing Localisation Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
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
					cumulus.LogMessage(msg);
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
					cumulus.LogMessage(msg);
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
					cumulus.LogMessage(msg);
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
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// air quality
				try
				{
					cumulus.Trans.AirQualityCaptions = settings.airQuality.sensor;
					cumulus.Trans.AirQualityAvgCaptions = settings.airQuality.sensorAvg;
				}
				catch (Exception ex)
				{
					var msg = "Error processing Air Quality settings: " + ex.Message;
					cumulus.LogMessage(msg);
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
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Davis forecast
				// The first elment of forecast2 & 3 is always blank and not returned by the settings page (it isn't sent to it in the first place)
				try
				{
					cumulus.Trans.DavisForecast1 = settings.davisForecast.forecast1;
					for (var i = 1; i <= settings.davisForecast.forecast2.Length; i++)
						cumulus.Trans.DavisForecast2[i] = settings.davisForecast.forecast2[i-1].Trim();
					for (var i = 1; i <= settings.davisForecast.forecast3.Length; i++)
						cumulus.Trans.DavisForecast3[i] = settings.davisForecast.forecast3[i-1].Trim();
				}
				catch (Exception ex)
				{
					var msg = "Error processing Davis Forecast settings: " + ex.Message;
					cumulus.LogMessage(msg);
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
					cumulus.LogMessage(msg);
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
				}
				catch (Exception ex)
				{
					var msg = "Error processing CO2 settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Alarms
				try
				{
					cumulus.Trans.AlarmEmailSubject = settings.alarms.subject.Trim();
					cumulus.Trans.AlarmEmailPreamble = settings.alarms.preamble.Trim();
					cumulus.HighGustAlarm.EmailMsg = settings.alarms.windGustAbove.Trim();
					cumulus.HighPressAlarm.EmailMsg = settings.alarms.pressureAbove.Trim();
					cumulus.HighTempAlarm.EmailMsg = settings.alarms.tempAbove.Trim();
					cumulus.LowPressAlarm.EmailMsg = settings.alarms.pressBelow.Trim();
					cumulus.LowTempAlarm.EmailMsg = settings.alarms.tempBelow.Trim();
					cumulus.PressChangeAlarm.EmailMsgDn = settings.alarms.pressDown.Trim();
					cumulus.PressChangeAlarm.EmailMsgUp = settings.alarms.pressUp.Trim();
					cumulus.HighRainTodayAlarm.EmailMsg = settings.alarms.rainAbove.Trim();
					cumulus.HighRainRateAlarm.EmailMsg = settings.alarms.rainRateAbove.Trim();
					cumulus.SensorAlarm.EmailMsg = settings.alarms.sensorLost.Trim();
					cumulus.TempChangeAlarm.EmailMsgDn = settings.alarms.tempDown.Trim();
					cumulus.TempChangeAlarm.EmailMsgUp = settings.alarms.tempUp.Trim();
					cumulus.HighWindAlarm.EmailMsg = settings.alarms.windAbove.Trim();
					cumulus.DataStoppedAlarm.EmailMsg = settings.alarms.dataStopped.Trim();
					cumulus.BatteryLowAlarm.EmailMsg = settings.alarms.batteryLow.Trim();
					cumulus.SpikeAlarm.EmailMsg = settings.alarms.dataSpike.Trim();
					cumulus.UpgradeAlarm.EmailMsg = settings.alarms.upgrade.Trim();
					cumulus.ThirdPartyAlarm.EmailMsg = settings.alarms.httpStopped.Trim();
					cumulus.MySqlUploadAlarm.EmailMsg = settings.alarms.mySqlStopped.Trim();
					cumulus.NewRecordAlarm.EmailMsg = settings.alarms.newRecord.Trim();
					cumulus.FtpAlarm.EmailMsg = settings.alarms.ftpStopped.Trim();
				}
				catch (Exception ex)
				{
					var msg = "Error processing Alarm settings: " + ex.Message;
					cumulus.LogMessage(msg);
					errorMsg += msg + "\n\n";
					context.Response.StatusCode = 500;
				}

				// Save the settings
				cumulus.WriteStringsFile();
			}
			catch (Exception ex)
			{
				var msg = "Error processing Localisation settings: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("Localisation Data: " + json);
				errorMsg += msg;
				context.Response.StatusCode = 500;
			}

			return context.Response.StatusCode == 200 ? "success" : errorMsg;

		}


		private class ForecastStrings
		{
			public string notavailable { get; set; }
			public string exceptional { get; set; }
			public string[] forecast { get; set; }
		}

		private class MoonPhaseStrings
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

		private class BeaufortStrings
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

		private class TrendsStrings
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

		private class AirQuality
		{
			public string[] sensor { get; set; }
			public string[] sensorAvg { get; set; }
		}

		private class Solar
		{
			public string lessdaylight { get; set; }
			public string moredaylight { get; set; }
		}

		private class DavisForecast
		{
			public string[] forecast1 { get; set; }
			public string[] forecast2 { get; set; }
			public string[] forecast3 { get; set; }
		}

		private class Co2Captions
		{
			public string Current { get; set; }
			public string Hr24 { get; set; }
			public string Pm2p5 { get; set; }
			public string Pm2p5_24hr { get; set; }
			public string Pm10 { get; set; }
			public string Pm10_24hr { get; set; }
		}

		private class AlarmEmails
		{
			public string subject { get; set; }
			public string preamble { get; set; }
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
			public string httpStopped { get; set; }
			public string mySqlStopped { get; set; }
			public string newRecord { get; set; }
			public string ftpStopped { get; set; }
		}

		private class Settings
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
			public AlarmEmails alarms { get; set; }
		}
	}
}
