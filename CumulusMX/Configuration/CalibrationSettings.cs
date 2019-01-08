using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unosquare.Labs.EmbedIO;

namespace Cumulus4.Configuration
{
    public class CalibrationSettings
    {
        private Cumulus cumulus;
        private string calibrationOptionsFile;
        private string calibrationSchemaFile;

        public CalibrationSettings(Cumulus cumulus)
        {
            this.cumulus = cumulus;
            calibrationOptionsFile = AppDomain.CurrentDomain.BaseDirectory + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "CalibrationOptions.json";
            calibrationSchemaFile = AppDomain.CurrentDomain.BaseDirectory + "interface"+Path.DirectorySeparatorChar+"json" + Path.DirectorySeparatorChar + "CalibrationSchema.json";
        }

		//public string UpdateCalibrationConfig(HttpListenerContext context)
		public string UpdateCalibrationConfig(IHttpContext context)
		{
			try
            {
                var InvC = new CultureInfo("");
                var data = new StreamReader(context.Request.InputStream).ReadToEnd();

                var json = WebUtility.UrlDecode(data.Substring(5));

                // de-serialize it to the settings structure
                var settings = JsonConvert.DeserializeObject<JsonCalibrationSettingsData>(json);
                // process the settings
                cumulus.LogMessage("Updating calibration settings");

                // offsets
                cumulus.PressOffset = Convert.ToDouble(settings.offsets.pressure,InvC);
                cumulus.TempOffset = Convert.ToDouble(settings.offsets.temperature, InvC);
                cumulus.InTempoffset = Convert.ToDouble(settings.offsets.indoortemp, InvC);
                cumulus.HumOffset = settings.offsets.humidity;
                cumulus.WindDirOffset = settings.offsets.winddir;
                cumulus.UVOffset = Convert.ToDouble(settings.offsets.uv, InvC);
                cumulus.WetBulbOffset = Convert.ToDouble(settings.offsets.wetbulb, InvC);

                // multipliers
                cumulus.WindSpeedMult = Convert.ToDouble(settings.multipliers.windspeed, InvC);
                cumulus.WindGustMult = Convert.ToDouble(settings.multipliers.windgust, InvC);
                cumulus.TempMult = Convert.ToDouble(settings.multipliers.outdoortemp, InvC);
                cumulus.HumMult = Convert.ToDouble(settings.multipliers.humidity, InvC);
                cumulus.RainMult = Convert.ToDouble(settings.multipliers.rainfall, InvC);
                cumulus.UVMult = Convert.ToDouble(settings.multipliers.uv, InvC);
                cumulus.WetBulbMult = Convert.ToDouble(settings.multipliers.wetbulb, InvC);

                // spike removal
                cumulus.EWtempdiff = Convert.ToDouble(settings.spikeremoval.outdoortemp, InvC);
                cumulus.EWhumiditydiff = Convert.ToDouble(settings.spikeremoval.humidity, InvC);
                cumulus.EWwinddiff = Convert.ToDouble(settings.spikeremoval.windspeed, InvC);
                cumulus.EWgustdiff = Convert.ToDouble(settings.spikeremoval.windgust, InvC);
                cumulus.EWmaxHourlyRain = Convert.ToDouble(settings.spikeremoval.maxhourlyrain, InvC);
                cumulus.EWmaxRainRate = Convert.ToDouble(settings.spikeremoval.maxrainrate, InvC);
                cumulus.EWpressurediff = Convert.ToDouble(settings.spikeremoval.pressure, InvC);
                cumulus.ErrorLogSpikeRemoval = settings.spikeremoval.log;

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

        public string GetCalibrationAlpacaFormData()
        {
            var InvC = new CultureInfo("");
            var offsets = new JsonCalibrationSettingsOffsets()
                          {
                              pressure = cumulus.PressOffset,
                              temperature = cumulus.TempOffset,
                              indoortemp = cumulus.InTempoffset,
                              humidity = cumulus.HumOffset,
                              winddir = cumulus.WindDirOffset,
                              uv = cumulus.UVOffset,
                              wetbulb = cumulus.WetBulbOffset
                          };
            var multipliers = new JsonCalibrationSettingsMultipliers()
                              {
                                  windspeed = cumulus.WindSpeedMult,
                                  windgust = cumulus.WindGustMult,
                                  humidity = cumulus.HumMult,
                                  outdoortemp = cumulus.TempMult,
                                  rainfall = cumulus.RainMult,
                                  uv = cumulus.UVMult,
                                  wetbulb = cumulus.WetBulbMult
                              };

            var spikeremoval = new JsonCalibrationSettingsSpikeRemoval()
                                {
                                    humidity = cumulus.EWhumiditydiff,
                                    windgust = cumulus.EWgustdiff,
                                    windspeed = cumulus.EWwinddiff,
                                    outdoortemp = cumulus.EWtempdiff,
                                    maxhourlyrain = cumulus.EWmaxHourlyRain,
                                    maxrainrate = cumulus.EWmaxRainRate,
                                    pressure = cumulus.EWpressurediff,
                                    log = cumulus.ErrorLogSpikeRemoval
                                };

            var data = new JsonCalibrationSettingsData()
                       {
                           offsets = offsets,
                           multipliers = multipliers,
                           spikeremoval = spikeremoval
                       };

            return JsonConvert.SerializeObject(data);
        }

        public string GetCalibrationAlpacaFormOptions()
        {
            using (StreamReader sr = new StreamReader(calibrationOptionsFile))
            {
                string json = sr.ReadToEnd();
                return json;
            }
        }

        public string GetCalibrationAlpacaFormSchema()
        {
            using (StreamReader sr = new StreamReader(calibrationSchemaFile))
            {
                string json = sr.ReadToEnd();
                return json;
            }
        }
    }

    public class JsonCalibrationSettingsData
    {
        public JsonCalibrationSettingsOffsets offsets { get; set; }
        public JsonCalibrationSettingsMultipliers multipliers { get; set; }
        public JsonCalibrationSettingsSpikeRemoval spikeremoval { get; set; }
    }

    public class JsonCalibrationSettingsOffsets
    {
        public double pressure { get; set; }
        public double temperature { get; set; }
        public double indoortemp { get; set; }
        public int humidity { get; set; }
        public int winddir { get; set; }
        public double uv { get; set; }
        public double wetbulb { get; set; }
    }

    public class JsonCalibrationSettingsMultipliers
    {
        public double windspeed { get; set; }
        public double windgust { get; set; }
        public double outdoortemp { get; set; }
        public double humidity { get; set; }
        public double rainfall { get; set; }
        public double uv { get; set; }
        public double wetbulb { get; set; }
    }

    public class JsonCalibrationSettingsSpikeRemoval
    {
        public double windspeed { get; set; }
        public double windgust { get; set; }
        public double outdoortemp { get; set; }
        public double humidity { get; set; }
        public double pressure { get; set; }
        public double maxrainrate { get; set; }
        public double maxhourlyrain { get; set; }
        public bool log { get; set; }
    }
}
