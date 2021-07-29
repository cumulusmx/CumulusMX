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
				jan = Math.Round(cumulus.NOAAconf.TempNorms[1],cumulus.TempDPlaces),
				feb = Math.Round(cumulus.NOAAconf.TempNorms[2], cumulus.TempDPlaces),
				mar = Math.Round(cumulus.NOAAconf.TempNorms[3], cumulus.TempDPlaces),
				apr = Math.Round(cumulus.NOAAconf.TempNorms[4], cumulus.TempDPlaces),
				may = Math.Round(cumulus.NOAAconf.TempNorms[5], cumulus.TempDPlaces),
				jun = Math.Round(cumulus.NOAAconf.TempNorms[6], cumulus.TempDPlaces),
				jul = Math.Round(cumulus.NOAAconf.TempNorms[7], cumulus.TempDPlaces),
				aug = Math.Round(cumulus.NOAAconf.TempNorms[8], cumulus.TempDPlaces),
				sep = Math.Round(cumulus.NOAAconf.TempNorms[9], cumulus.TempDPlaces),
				oct = Math.Round(cumulus.NOAAconf.TempNorms[10], cumulus.TempDPlaces),
				nov = Math.Round(cumulus.NOAAconf.TempNorms[11], cumulus.TempDPlaces),
				dec = Math.Round(cumulus.NOAAconf.TempNorms[12], cumulus.TempDPlaces)
			};

			var normalrain = new JsonNOAASettingsNormalRain()
			{
				jan = Math.Round(cumulus.NOAAconf.RainNorms[1], cumulus.RainDPlaces),
				feb = Math.Round(cumulus.NOAAconf.RainNorms[2], cumulus.RainDPlaces),
				mar = Math.Round(cumulus.NOAAconf.RainNorms[3], cumulus.RainDPlaces),
				apr = Math.Round(cumulus.NOAAconf.RainNorms[4], cumulus.RainDPlaces),
				may = Math.Round(cumulus.NOAAconf.RainNorms[5], cumulus.RainDPlaces),
				jun = Math.Round(cumulus.NOAAconf.RainNorms[6], cumulus.RainDPlaces),
				jul = Math.Round(cumulus.NOAAconf.RainNorms[7], cumulus.RainDPlaces),
				aug = Math.Round(cumulus.NOAAconf.RainNorms[8], cumulus.RainDPlaces),
				sep = Math.Round(cumulus.NOAAconf.RainNorms[9], cumulus.RainDPlaces),
				oct = Math.Round(cumulus.NOAAconf.RainNorms[10], cumulus.RainDPlaces),
				nov = Math.Round(cumulus.NOAAconf.RainNorms[11], cumulus.RainDPlaces),
				dec = Math.Round(cumulus.NOAAconf.RainNorms[12], cumulus.RainDPlaces)
			};

			var site = new JsonNOAASettingsSite()
			{
				sitename = cumulus.NOAAconf.Name,
				city = cumulus.NOAAconf.City,
				state = cumulus.NOAAconf.State
			};

			var files = new JsonNOAASettingsOutput()
			{
				monthfileformat = cumulus.NOAAconf.MonthFile,
				yearfileformat = cumulus.NOAAconf.YearFile
			};

			var options = new JsonNOAASettingsOptions()
			{
				timeformat = cumulus.NOAAconf.Use12hour ? 1 : 0,
				utf8 = cumulus.NOAAconf.UseUtf8,
				dotdecimal = cumulus.NOAAconf.UseDotDecimal,
				minmaxavg = cumulus.NOAAconf.UseMinMaxAvg
			};

			var ftp = new JsonNOAASettingsFtpCopy()
			{
				autotransfer = cumulus.NOAAconf.AutoFtp,
				dstfolder = cumulus.NOAAconf.FtpFolder
			};

			var copy = new JsonNOAASettingsFtpCopy()
			{
				autotransfer = cumulus.NOAAconf.AutoCopy,
				dstfolder = cumulus.NOAAconf.CopyFolder
			};

			var thresh = new JsonNOAASettingsThresholds()
			{
				heatingthreshold = Math.Round(cumulus.NOAAconf.HeatThreshold, cumulus.TempDPlaces),
				coolingthreshold = Math.Round(cumulus.NOAAconf.CoolThreshold, cumulus.TempDPlaces),
				maxtempcomp1 = Math.Round(cumulus.NOAAconf.MaxTempComp1, cumulus.RainDPlaces),
				maxtempcomp2 = Math.Round(cumulus.NOAAconf.MaxTempComp2, cumulus.RainDPlaces),
				mintempcomp1 = Math.Round(cumulus.NOAAconf.MinTempComp1, cumulus.RainDPlaces),
				mintempcomp2 = Math.Round(cumulus.NOAAconf.MinTempComp2, cumulus.RainDPlaces),
				raincomp1 = Math.Round(cumulus.NOAAconf.RainComp1, cumulus.RainDPlaces),
				raincomp2 = Math.Round(cumulus.NOAAconf.RainComp2, cumulus.RainDPlaces),
				raincomp3 = Math.Round(cumulus.NOAAconf.RainComp3, cumulus.RainDPlaces)
			};

			var data = new JsonNOAASettingsData()
			{
				accessible = cumulus.ProgramOptions.EnableAccessibility,
				autosave = cumulus.NOAAconf.Create,

				sitedetails = site,
				outputfiles = files,
				options = options,
				ftp = ftp,
				copy = copy,
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
				var msg = "Error de-serializing NOAA Settings JSON: " + ex.Message;
				cumulus.LogMessage(msg);
				cumulus.LogDebugMessage("NOAA Data: " + json);
				context.Response.StatusCode = 500;
				return msg;
			}


			// process the settings
			try
			{
				cumulus.LogMessage("Updating NOAA settings");

				cumulus.NOAAconf.Create = settings.autosave;
				if (cumulus.NOAAconf.Create)
				{
					cumulus.NOAAconf.Name = settings.sitedetails.sitename;
					cumulus.NOAAconf.City = settings.sitedetails.city;
					cumulus.NOAAconf.State = settings.sitedetails.state;

					cumulus.NOAAconf.MonthFile = settings.outputfiles.monthfileformat;
					cumulus.NOAAconf.YearFile = settings.outputfiles.yearfileformat;

					cumulus.NOAAconf.Use12hour = settings.options.timeformat == 1;
					cumulus.NOAAconf.UseUtf8 = settings.options.utf8;
					cumulus.NOAAconf.UseDotDecimal = settings.options.dotdecimal;
					cumulus.NOAAconf.UseMinMaxAvg = settings.options.minmaxavg;

					cumulus.NOAAconf.AutoFtp = settings.ftp.autotransfer;
					cumulus.NOAAconf.FtpFolder = settings.ftp.dstfolder;

					cumulus.NOAAconf.AutoCopy = settings.copy.autotransfer;
					cumulus.NOAAconf.CopyFolder = settings.copy.dstfolder;

					cumulus.NOAAconf.HeatThreshold = settings.thresholds.heatingthreshold;
					cumulus.NOAAconf.CoolThreshold = settings.thresholds.coolingthreshold;
					cumulus.NOAAconf.MaxTempComp1 = settings.thresholds.maxtempcomp1;
					cumulus.NOAAconf.MaxTempComp2 = settings.thresholds.maxtempcomp2;
					cumulus.NOAAconf.MinTempComp1 = settings.thresholds.mintempcomp1;
					cumulus.NOAAconf.MinTempComp2 = settings.thresholds.mintempcomp2;
					cumulus.NOAAconf.RainComp1 = settings.thresholds.raincomp1;
					cumulus.NOAAconf.RainComp2 = settings.thresholds.raincomp2;
					cumulus.NOAAconf.RainComp3 = settings.thresholds.raincomp3;

					// normal mean temps
					cumulus.NOAAconf.TempNorms[1] = settings.normalmeantemps.jan;
					cumulus.NOAAconf.TempNorms[2] = settings.normalmeantemps.feb;
					cumulus.NOAAconf.TempNorms[3] = settings.normalmeantemps.mar;
					cumulus.NOAAconf.TempNorms[4] = settings.normalmeantemps.apr;
					cumulus.NOAAconf.TempNorms[5] = settings.normalmeantemps.may;
					cumulus.NOAAconf.TempNorms[6] = settings.normalmeantemps.jun;
					cumulus.NOAAconf.TempNorms[7] = settings.normalmeantemps.jul;
					cumulus.NOAAconf.TempNorms[8] = settings.normalmeantemps.aug;
					cumulus.NOAAconf.TempNorms[9] = settings.normalmeantemps.sep;
					cumulus.NOAAconf.TempNorms[10] = settings.normalmeantemps.oct;
					cumulus.NOAAconf.TempNorms[11] = settings.normalmeantemps.nov;
					cumulus.NOAAconf.TempNorms[12] = settings.normalmeantemps.dec;

					// normal rain
					cumulus.NOAAconf.RainNorms[1] = settings.normalrain.jan;
					cumulus.NOAAconf.RainNorms[2] = settings.normalrain.feb;
					cumulus.NOAAconf.RainNorms[3] = settings.normalrain.mar;
					cumulus.NOAAconf.RainNorms[4] = settings.normalrain.apr;
					cumulus.NOAAconf.RainNorms[5] = settings.normalrain.may;
					cumulus.NOAAconf.RainNorms[6] = settings.normalrain.jun;
					cumulus.NOAAconf.RainNorms[6] = settings.normalrain.jul;
					cumulus.NOAAconf.RainNorms[8] = settings.normalrain.aug;
					cumulus.NOAAconf.RainNorms[9] = settings.normalrain.sep;
					cumulus.NOAAconf.RainNorms[10] = settings.normalrain.oct;
					cumulus.NOAAconf.RainNorms[11] = settings.normalrain.nov;
					cumulus.NOAAconf.RainNorms[12] = settings.normalrain.dec;
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
		public JsonNOAASettingsFtpCopy ftp { get; set; }
		public JsonNOAASettingsFtpCopy copy { get; set; }
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
		public bool minmaxavg { get; set; }
	}

	public class JsonNOAASettingsFtpCopy
	{
		public bool autotransfer { get; set; }
		public string dstfolder { get; set; }
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
