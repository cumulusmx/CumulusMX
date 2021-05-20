using System;
using System.Globalization;
using System.IO;
using System.Net;
using ServiceStack;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
	public class NOAASettings
	{
		private readonly Cumulus cumulus;

		public NOAASettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		public string GetAlpacaFormData()
		{
			//var InvC = new CultureInfo("");
			var normalmeantemps = new JsonNOAASettingsNormalMeanTemps()
			{
				jan = Math.Round(cumulus.NOAATempNorms[1],cumulus.TempDPlaces),
				feb = Math.Round(cumulus.NOAATempNorms[2], cumulus.TempDPlaces),
				mar = Math.Round(cumulus.NOAATempNorms[3], cumulus.TempDPlaces),
				apr = Math.Round(cumulus.NOAATempNorms[4], cumulus.TempDPlaces),
				may = Math.Round(cumulus.NOAATempNorms[5], cumulus.TempDPlaces),
				jun = Math.Round(cumulus.NOAATempNorms[6], cumulus.TempDPlaces),
				jul = Math.Round(cumulus.NOAATempNorms[7], cumulus.TempDPlaces),
				aug = Math.Round(cumulus.NOAATempNorms[8], cumulus.TempDPlaces),
				sep = Math.Round(cumulus.NOAATempNorms[9], cumulus.TempDPlaces),
				oct = Math.Round(cumulus.NOAATempNorms[10], cumulus.TempDPlaces),
				nov = Math.Round(cumulus.NOAATempNorms[11], cumulus.TempDPlaces),
				dec = Math.Round(cumulus.NOAATempNorms[12], cumulus.TempDPlaces)
			};

			var normalrain = new JsonNOAASettingsNormalRain()
			{
				jan = Math.Round(cumulus.NOAARainNorms[1], cumulus.RainDPlaces),
				feb = Math.Round(cumulus.NOAARainNorms[2], cumulus.RainDPlaces),
				mar = Math.Round(cumulus.NOAARainNorms[3], cumulus.RainDPlaces),
				apr = Math.Round(cumulus.NOAARainNorms[4], cumulus.RainDPlaces),
				may = Math.Round(cumulus.NOAARainNorms[5], cumulus.RainDPlaces),
				jun = Math.Round(cumulus.NOAARainNorms[6], cumulus.RainDPlaces),
				jul = Math.Round(cumulus.NOAARainNorms[7], cumulus.RainDPlaces),
				aug = Math.Round(cumulus.NOAARainNorms[8], cumulus.RainDPlaces),
				sep = Math.Round(cumulus.NOAARainNorms[9], cumulus.RainDPlaces),
				oct = Math.Round(cumulus.NOAARainNorms[10], cumulus.RainDPlaces),
				nov = Math.Round(cumulus.NOAARainNorms[11], cumulus.RainDPlaces),
				dec = Math.Round(cumulus.NOAARainNorms[12], cumulus.RainDPlaces)
			};

			var site = new JsonNOAASettingsSite()
			{
				sitename = cumulus.NOAAname,
				city = cumulus.NOAAcity,
				state = cumulus.NOAAstate
			};

			var files = new JsonNOAASettingsOutput()
			{
				monthfileformat = cumulus.NOAAMonthFileFormat,
				yearfileformat = cumulus.NOAAYearFileFormat
			};

			var options = new JsonNOAASettingsOptions()
			{
				timeformat = cumulus.NOAA12hourformat ? 1 : 0,
				utf8 = cumulus.NOAAUseUTF8,
				dotdecimal = cumulus.NOAAUseDotDecimal
			};

			var ftp = new JsonNOAASettingsFtp()
			{
				autoftp = cumulus.NOAAAutoFTP,
				ftpdirectory = cumulus.NOAAFTPDirectory
			};

			var thresh = new JsonNOAASettingsThresholds()
			{
				heatingthreshold = Math.Round(cumulus.NOAAheatingthreshold,cumulus.TempDPlaces),
				coolingthreshold = Math.Round(cumulus.NOAAcoolingthreshold,cumulus.TempDPlaces),
				maxtempcomp1 = Math.Round(cumulus.NOAAmaxtempcomp1,cumulus.RainDPlaces),
				maxtempcomp2 = Math.Round(cumulus.NOAAmaxtempcomp2,cumulus.RainDPlaces),
				mintempcomp1 = Math.Round(cumulus.NOAAmintempcomp1,cumulus.RainDPlaces),
				mintempcomp2 = Math.Round(cumulus.NOAAmintempcomp2,cumulus.RainDPlaces),
				raincomp1 = Math.Round(cumulus.NOAAraincomp1,cumulus.RainDPlaces),
				raincomp2 = Math.Round(cumulus.NOAAraincomp2,cumulus.RainDPlaces),
				raincomp3 = Math.Round(cumulus.NOAAraincomp3, cumulus.RainDPlaces)
			};

			var data = new JsonNOAASettingsData()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				autosave = cumulus.NOAAAutoSave,

				sitedetails = site,
				outputfiles = files,
				options = options,
				ftp = ftp,
				thresholds = thresh,
				normalmeantemps = normalmeantemps,
				normalrain = normalrain
			};

			return data.ToJson();
		}

		//public string UpdateNoaaConfig(HttpListenerContext context)
		public string UpdateConfig(IHttpContext context)
		{
			var json = "";
			JsonNOAASettingsData settings;

			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				settings = json.FromJson<JsonNOAASettingsData>();
			}
			catch (Exception ex)
			{
				var msg = "Error deserializing NOAA Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("NOAA Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}


			// process the settings
			try
			{
				cumulus.LogMessage("Updating NOAA settings");

				cumulus.NOAAAutoSave = settings.autosave;
				if (cumulus.NOAAAutoSave)
				{
					cumulus.NOAAname = settings.sitedetails.sitename;
					cumulus.NOAAcity = settings.sitedetails.city;
					cumulus.NOAAstate = settings.sitedetails.state;

					cumulus.NOAAMonthFileFormat = settings.outputfiles.monthfileformat;
					cumulus.NOAAYearFileFormat = settings.outputfiles.yearfileformat;

					cumulus.NOAA12hourformat = settings.options.timeformat == 1;
					cumulus.NOAAUseUTF8 = settings.options.utf8;
					cumulus.NOAAUseDotDecimal = settings.options.dotdecimal;

					cumulus.NOAAAutoFTP = settings.ftp.autoftp;
					cumulus.NOAAFTPDirectory = settings.ftp.ftpdirectory;

					cumulus.NOAAheatingthreshold = settings.thresholds.heatingthreshold;
					cumulus.NOAAcoolingthreshold = settings.thresholds.coolingthreshold;
					cumulus.NOAAmaxtempcomp1 = settings.thresholds.maxtempcomp1;
					cumulus.NOAAmaxtempcomp2 = settings.thresholds.maxtempcomp2;
					cumulus.NOAAmintempcomp1 = settings.thresholds.mintempcomp1;
					cumulus.NOAAmintempcomp2 = settings.thresholds.mintempcomp2;
					cumulus.NOAAraincomp1 = settings.thresholds.raincomp1;
					cumulus.NOAAraincomp2 = settings.thresholds.raincomp2;
					cumulus.NOAAraincomp3 = settings.thresholds.raincomp3;

					// normal mean temps
					cumulus.NOAATempNorms[1] = settings.normalmeantemps.jan;
					cumulus.NOAATempNorms[2] = settings.normalmeantemps.feb;
					cumulus.NOAATempNorms[3] = settings.normalmeantemps.mar;
					cumulus.NOAATempNorms[4] = settings.normalmeantemps.apr;
					cumulus.NOAATempNorms[5] = settings.normalmeantemps.may;
					cumulus.NOAATempNorms[6] = settings.normalmeantemps.jun;
					cumulus.NOAATempNorms[7] = settings.normalmeantemps.jul;
					cumulus.NOAATempNorms[8] = settings.normalmeantemps.aug;
					cumulus.NOAATempNorms[9] = settings.normalmeantemps.sep;
					cumulus.NOAATempNorms[10] = settings.normalmeantemps.oct;
					cumulus.NOAATempNorms[11] = settings.normalmeantemps.nov;
					cumulus.NOAATempNorms[12] = settings.normalmeantemps.dec;

					// normal rain
					cumulus.NOAARainNorms[1] = settings.normalrain.jan;
					cumulus.NOAARainNorms[2] = settings.normalrain.feb;
					cumulus.NOAARainNorms[3] = settings.normalrain.mar;
					cumulus.NOAARainNorms[4] = settings.normalrain.apr;
					cumulus.NOAARainNorms[5] = settings.normalrain.may;
					cumulus.NOAARainNorms[6] = settings.normalrain.jun;
					cumulus.NOAARainNorms[6] = settings.normalrain.jul;
					cumulus.NOAARainNorms[8] = settings.normalrain.aug;
					cumulus.NOAARainNorms[9] = settings.normalrain.sep;
					cumulus.NOAARainNorms[10] = settings.normalrain.oct;
					cumulus.NOAARainNorms[11] = settings.normalrain.nov;
					cumulus.NOAARainNorms[12] = settings.normalrain.dec;
				}

				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				var msg = "Error processing NOAA settings: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("NOAA data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}
			return "success";
		}
	}

	public class JsonNOAASettingsData
	{
		public bool accessible { get; set; }
		public bool autosave {get; set; }
		public JsonNOAASettingsSite sitedetails { get; set; }
		public JsonNOAASettingsOutput outputfiles { get; set; }
		public JsonNOAASettingsOptions options { get; set; }
		public JsonNOAASettingsFtp ftp { get; set; }
		public JsonNOAASettingsThresholds thresholds { get; set; }
		public JsonNOAASettingsNormalMeanTemps normalmeantemps {get; set; }
		public JsonNOAASettingsNormalRain normalrain {get; set; }
	}

	public class JsonNOAASettingsSite
	{
		public string sitename {get; set; }
		public string city {get; set; }
		public string state {get; set; }
	}

	public class JsonNOAASettingsOutput
	{
		public string monthfileformat {get; set; }
		public string yearfileformat {get; set; }
	}

	public class JsonNOAASettingsOptions
	{
		public int timeformat { get; set; }
		public bool utf8 { get; set; }
		public bool dotdecimal { get; set; }
	}

	public class JsonNOAASettingsFtp
	{
		public bool autoftp { get; set; }
		public string ftpdirectory { get; set; }
	}

	public class JsonNOAASettingsThresholds
	{
		public double heatingthreshold { get; set; }
		public double coolingthreshold { get; set; }
		public double maxtempcomp1 { get; set; }
		public double maxtempcomp2 { get; set; }
		public double mintempcomp1 { get; set; }
		public double mintempcomp2 { get; set; }
		public double raincomp1 { get; set; }
		public double raincomp2 { get; set; }
		public double raincomp3 { get; set; }
	}

	public class JsonNOAASettingsNormalMeanTemps
	{
		public double jan {get; set; }
		public double feb {get; set; }
		public double mar {get; set; }
		public double apr {get; set; }
		public double may {get; set; }
		public double jun {get; set; }
		public double jul {get; set; }
		public double aug {get; set; }
		public double sep {get; set; }
		public double oct {get; set; }
		public double nov {get; set; }
		public double dec {get; set; }
	}

	public class JsonNOAASettingsNormalRain
	{
		public double jan {get; set; }
		public double feb {get; set; }
		public double mar {get; set; }
		public double apr {get; set; }
		public double may {get; set; }
		public double jun {get; set; }
		public double jul {get; set; }
		public double aug {get; set; }
		public double sep {get; set; }
		public double oct {get; set; }
		public double nov {get; set; }
		public double dec {get; set; }
	}
}
