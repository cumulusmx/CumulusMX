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
		private readonly string noaaOptionsFile;
		private readonly string noaaSchemaFile;

		public NOAASettings(Cumulus cumulus)
		{
			this.cumulus = cumulus;
			noaaOptionsFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "NoaaOptions.json";
			noaaSchemaFile = cumulus.AppDir + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "NoaaSchema.json";
		}

		public string GetNoaaAlpacaFormData()
		{
			//var InvC = new CultureInfo("");
			var normalmeantemps = new JsonNOAASettingsNormalMeanTemps()
								  {
									  jan = Math.Round(cumulus.NOAATempNormJan,cumulus.TempDPlaces),
									  feb = Math.Round(cumulus.NOAATempNormFeb,cumulus.TempDPlaces),
									  mar = Math.Round(cumulus.NOAATempNormMar,cumulus.TempDPlaces),
									  apr = Math.Round(cumulus.NOAATempNormApr,cumulus.TempDPlaces),
									  may = Math.Round(cumulus.NOAATempNormMay,cumulus.TempDPlaces),
									  jun = Math.Round(cumulus.NOAATempNormJun,cumulus.TempDPlaces),
									  jul = Math.Round(cumulus.NOAATempNormJul,cumulus.TempDPlaces),
									  aug = Math.Round(cumulus.NOAATempNormAug,cumulus.TempDPlaces),
									  sep = Math.Round(cumulus.NOAATempNormSep,cumulus.TempDPlaces),
									  oct = Math.Round(cumulus.NOAATempNormOct,cumulus.TempDPlaces),
									  nov = Math.Round(cumulus.NOAATempNormNov,cumulus.TempDPlaces),
									  dec = Math.Round(cumulus.NOAATempNormDec, cumulus.TempDPlaces)
								  };

			var normalrain = new JsonNOAASettingsNormalRain()
							 {
									  jan = Math.Round(cumulus.NOAARainNormJan,cumulus.RainDPlaces),
									  feb = Math.Round(cumulus.NOAARainNormFeb,cumulus.RainDPlaces),
									  mar = Math.Round(cumulus.NOAARainNormMar,cumulus.RainDPlaces),
									  apr = Math.Round(cumulus.NOAARainNormApr,cumulus.RainDPlaces),
									  may = Math.Round(cumulus.NOAARainNormMay,cumulus.RainDPlaces),
									  jun = Math.Round(cumulus.NOAARainNormJun,cumulus.RainDPlaces),
									  jul = Math.Round(cumulus.NOAARainNormJul,cumulus.RainDPlaces),
									  aug = Math.Round(cumulus.NOAARainNormAug,cumulus.RainDPlaces),
									  sep = Math.Round(cumulus.NOAARainNormSep,cumulus.RainDPlaces),
									  oct = Math.Round(cumulus.NOAARainNormOct,cumulus.RainDPlaces),
									  nov = Math.Round(cumulus.NOAARainNormNov,cumulus.RainDPlaces),
									  dec = Math.Round(cumulus.NOAARainNormDec,cumulus.RainDPlaces)
								  };

			var data = new JsonNOAASettingsData()
					   {
						   sitename = cumulus.NOAAname,
						   city = cumulus.NOAAcity,
						   state = cumulus.NOAAstate,
						   timeformat = cumulus.NOAA12hourformat?0:1,
						   monthfileformat = cumulus.NOAAMonthFileFormat,
						   yearfileformat = cumulus.NOAAYearFileFormat,
						   utf8 = cumulus.NOAAUseUTF8,
						   autosave = cumulus.NOAAAutoSave,
						   autoftp = cumulus.NOAAAutoFTP,
						   ftpdirectory = cumulus.NOAAFTPDirectory,
						   heatingthreshold = Math.Round(cumulus.NOAAheatingthreshold,cumulus.TempDPlaces),
						   coolingthreshold = Math.Round(cumulus.NOAAcoolingthreshold,cumulus.TempDPlaces),
						   maxtempcomp1 = Math.Round(cumulus.NOAAmaxtempcomp1,cumulus.RainDPlaces),
						   maxtempcomp2 = Math.Round(cumulus.NOAAmaxtempcomp2,cumulus.RainDPlaces),
						   mintempcomp1 = Math.Round(cumulus.NOAAmintempcomp1,cumulus.RainDPlaces),
						   mintempcomp2 = Math.Round(cumulus.NOAAmintempcomp2,cumulus.RainDPlaces),
						   raincomp1 = Math.Round(cumulus.NOAAraincomp1,cumulus.RainDPlaces),
						   raincomp2 = Math.Round(cumulus.NOAAraincomp2,cumulus.RainDPlaces),
						   raincomp3 = Math.Round(cumulus.NOAAraincomp3, cumulus.RainDPlaces),
						   normalmeantemps = normalmeantemps,
						   normalrain = normalrain
					   };

			return data.ToJson();
		}

		public string GetNoaaAlpacaFormOptions()
		{
			using (StreamReader sr = new StreamReader(noaaOptionsFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		public string GetNoaaAlpacaFormSchema()
		{
			using (StreamReader sr = new StreamReader(noaaSchemaFile))
			{
				string json = sr.ReadToEnd();
				return json;
			}
		}

		//public string UpdateNoaaConfig(HttpListenerContext context)
		public string UpdateNoaaConfig(IHttpContext context)
		{
			try
			{
				var data = new StreamReader(context.Request.InputStream).ReadToEnd();

				// Start at char 5 to skip the "json:" prefix
				var json = WebUtility.UrlDecode(data.Substring(5));

				// de-serialize it to the settings structure
				var settings = json.FromJson<JsonNOAASettingsData>();
				// process the settings
				cumulus.LogMessage("Updating NOAA settings");
				var InvC = new CultureInfo("");

				cumulus.NOAAname = settings.sitename;
				cumulus.NOAAcity = settings.city;
				cumulus.NOAAstate = settings.state;
				cumulus.NOAA12hourformat = settings.timeformat == 0;
				cumulus.NOAAMonthFileFormat = settings.monthfileformat;
				cumulus.NOAAYearFileFormat = settings.yearfileformat;
				cumulus.NOAAUseUTF8 = settings.utf8;
				cumulus.NOAAAutoSave = settings.autosave;
				cumulus.NOAAAutoFTP = settings.autoftp;
				cumulus.NOAAFTPDirectory = settings.ftpdirectory;
				cumulus.NOAAheatingthreshold = settings.heatingthreshold;
				cumulus.NOAAcoolingthreshold = settings.coolingthreshold;
				cumulus.NOAAmaxtempcomp1 = settings.maxtempcomp1;
				cumulus.NOAAmaxtempcomp2 = settings.maxtempcomp2;
				cumulus.NOAAmintempcomp1 = settings.mintempcomp1;
				cumulus.NOAAmintempcomp2 = settings.mintempcomp2;
				cumulus.NOAAraincomp1 = settings.raincomp1;
				cumulus.NOAAraincomp2 = settings.raincomp2;
				cumulus.NOAAraincomp3 = settings.raincomp3;

				// normal mean temps
				cumulus.NOAATempNormJan = settings.normalmeantemps.jan;
				cumulus.NOAATempNormFeb = settings.normalmeantemps.feb;
				cumulus.NOAATempNormMar = settings.normalmeantemps.mar;
				cumulus.NOAATempNormApr = settings.normalmeantemps.apr;
				cumulus.NOAATempNormMay = settings.normalmeantemps.may;
				cumulus.NOAATempNormJun = settings.normalmeantemps.jun;
				cumulus.NOAATempNormJul = settings.normalmeantemps.jul;
				cumulus.NOAATempNormAug = settings.normalmeantemps.aug;
				cumulus.NOAATempNormSep = settings.normalmeantemps.sep;
				cumulus.NOAATempNormOct = settings.normalmeantemps.oct;
				cumulus.NOAATempNormNov = settings.normalmeantemps.nov;
				cumulus.NOAATempNormDec = settings.normalmeantemps.dec;

				// normal rain
				cumulus.NOAARainNormJan = settings.normalrain.jan;
				cumulus.NOAARainNormFeb = settings.normalrain.feb;
				cumulus.NOAARainNormMar = settings.normalrain.mar;
				cumulus.NOAARainNormApr = settings.normalrain.apr;
				cumulus.NOAARainNormMay = settings.normalrain.may;
				cumulus.NOAARainNormJun = settings.normalrain.jun;
				cumulus.NOAARainNormJul = settings.normalrain.jul;
				cumulus.NOAARainNormAug = settings.normalrain.aug;
				cumulus.NOAARainNormSep = settings.normalrain.sep;
				cumulus.NOAARainNormOct = settings.normalrain.oct;
				cumulus.NOAARainNormNov = settings.normalrain.nov;
				cumulus.NOAARainNormDec = settings.normalrain.dec;

				// Save the settings
				cumulus.WriteIniFile();

				context.Response.StatusCode = 200;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				context.Response.StatusCode = 500;
				return ex.Message;
			}
			return "success";
		}
	}

	public class JsonNOAASettingsData
	{
		public string sitename {get; set; }
		public string city {get; set; }
		public string state {get; set; }
		public int timeformat {get; set; }
		public string monthfileformat {get; set; }
		public string yearfileformat {get; set; }
		public bool utf8 {get; set; }
		public bool autosave {get; set; }
		public bool autoftp {get; set; }
		public string ftpdirectory {get; set; }
		public double heatingthreshold {get; set; }
		public double coolingthreshold {get; set; }
		public double maxtempcomp1 {get; set; }
		public double maxtempcomp2 {get; set; }
		public double mintempcomp1 {get; set; }
		public double mintempcomp2 {get; set; }
		public double raincomp1 {get; set; }
		public double raincomp2 {get; set; }
		public double raincomp3 {get; set; }
		public JsonNOAASettingsNormalMeanTemps normalmeantemps {get; set; }
		public JsonNOAASettingsNormalRain normalrain {get; set; }
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
