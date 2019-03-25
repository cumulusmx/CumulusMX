using System;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace WCloudDataReporter
{
    public class WCloudDataReporter : DataReporterBase
    {
        public override string ServiceName => "WCloud Data Reporter Service";

        public IDataReporterSettings Settings { get; private set; }

        public override void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        public override string Identifier => "TBC"; //TODO

        public WCloudDataReporter(ILogger logger, DataReporterSettingsGeneric settings, IWeatherDataStatistics data) : base(logger, settings, data)
        {
            Settings = settings as IDataReporterSettings;
        }
        
        public override void Initialise()
        {
        }

        /*
        internal async void UpdateWCloud(DateTime timestamp)
        {
            if (!UpdatingWCloud)
            {
                UpdatingWCloud = true;

                string pwstring;

                string URL = station.GetWCloudURL(out pwstring, timestamp);

                string starredpwstring = "<key>";

                string LogURL = URL.Replace(pwstring, starredpwstring);

                LogDebugMessage(LogURL);

                try
                {
                    HttpResponseMessage response = await WCloudhttpClient.GetAsync(URL);
                    var responseBodyAsText = await response.Content.ReadAsStringAsync();
                    LogMessage("WeatherCloud Response: " + response.StatusCode + ": " + responseBodyAsText);
                }
                catch (Exception ex)
                {
                    LogMessage("WeatherCloud update: " + ex.Message);
                }
                finally
                {
                    UpdatingWCloud = false;
                }
            }
        }

        public string GetWCloudURL(out string pwstring, DateTime timestamp)
        {
            pwstring = cumulus.WCloudKey;

            StringBuilder sb = new StringBuilder($"http://api.weathercloud.net/v01/set?wid={cumulus.WCloudWid}&key={cumulus.WCloudKey}");

            //Temperature
            sb.Append("&tempin=" + (int)Math.Round(ConvertUserTempToC(IndoorTemperature) * 10));
            sb.Append("&temp=" + (int)Math.Round(ConvertUserTempToC(OutdoorTemperature) * 10));
            sb.Append("&chill=" + (int)Math.Round(ConvertUserTempToC(WindChill) * 10));
            sb.Append("&dew=" + (int)Math.Round(ConvertUserTempToC(OutdoorDewpoint) * 10));
            sb.Append("&heat=" + (int)Math.Round(ConvertUserTempToC(HeatIndex) * 10));

            // Humidity
            sb.Append("&humin=" + IndoorHumidity);
            sb.Append("&hum=" + OutdoorHumidity);

            // Wind
            sb.Append("&wspd=" + (int)Math.Round(ConvertUserWindToMS(WindLatest) * 10));
            sb.Append("&wspdhi=" + (int)Math.Round(ConvertUserWindToMS(RecentMaxGust) * 10));
            sb.Append("&wspdavg=" + (int)Math.Round(ConvertUserWindToMS(WindAverage) * 10));

            // Wind Direction
            sb.Append("&wdir=" + Bearing);
            sb.Append("&wdiravg=" + AvgBearing);

            // Pressure
            sb.Append("&bar=" + (int)Math.Round(ConvertUserPressToMB(Pressure) * 10));

            // rain
            sb.Append("&rain=" + (int)Math.Round(ConvertUserRainToMM(RainToday) * 10));
            sb.Append("&rainrate=" + (int)Math.Round(ConvertUserRainToMM(RainRate) * 10));

            // ET
            if (cumulus.SendSolarToWCloud && cumulus.Manufacturer == cumulus.DAVIS)
            {
                sb.Append("&et=" + (int)Math.Round(ConvertUserRainToMM(ET) * 10));
            }

            // solar
            if (cumulus.SendSolarToWCloud)
            {
                sb.Append("&solarrad=" + (int)Math.Round(SolarRad * 10));
            }

            // uv
            if (cumulus.SendUVToWCloud)
            {
                sb.Append("&uvi=" + (int)Math.Round(UV * 10));
            }

            // time
            sb.Append("&time=" + timestamp.ToString("HHmm"));

            // date
            sb.Append("&date=" + timestamp.ToString("yyyyMMdd"));

            // software identification
            sb.Append("&type=291&ver=" + cumulus.Version);

            return sb.ToString();
        }

*/
    }
}
