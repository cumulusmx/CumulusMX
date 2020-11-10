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
