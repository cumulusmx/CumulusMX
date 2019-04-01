using System;
using System.Net.Http;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace WeatherBugDataReporter
{
    public class WeatherBugDataReporter : DataReporterBase
    {
        public override string ServiceName => "Weatherbug Data Reporter Service";

        public IDataReporterSettings Settings { get; private set; }

        public override void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        public override string Identifier => "TBC"; //TODO

        public WeatherBugDataReporter(ILogger logger, DataReporterSettingsGeneric settings, IWeatherDataStatistics data) : base(logger, settings, data)
        {
            Settings = settings as IDataReporterSettings;
        }

        public override void Initialise()
        {
        }



        /*
        /// <summary>
        /// Process the list of Weatherbug updates created at startup from logger entries
        /// </summary>
        private async void WBCatchup()
        {
            UpdatingWB = true;

            for (int i = 0; i < WeatherbugList.Count; i++)
            {
                LogMessage("Uploading Weatherbug archive #" + (i + 1));
                try
                {
                    HttpResponseMessage response = await WBhttpClient.GetAsync(WeatherbugList[i]);
                    var responseBodyAsText = await response.Content.ReadAsStringAsync();
                    LogMessage("Weatherbug Response: " + response.StatusCode + ": " + responseBodyAsText);
                }
                catch (Exception ex)
                {
                    LogMessage("Wbug update: " + ex.Message);
                }
            }

            LogMessage("End of Weatherbug archive upload");
            WeatherbugList.Clear();
            WeatherbugCatchingUp = false;
            WeatherbugTimer.Enabled = WeatherbugEnabled && !SynchronisedWBUpdate;
            UpdatingWB = false;
        }

        public async void UpdateWeatherbug(DateTime timestamp)
        {
            if (!UpdatingWB)
            {
                UpdatingWB = true;

                string pwstring;

                string URL = station.GetWeatherbugURL(out pwstring, timestamp, false);

                string starredpwstring = "&Key=" + new string('*', WeatherbugPW.Length);

                string LogURL = URL.Replace(pwstring, starredpwstring);
                LogDebugMessage(LogURL);

                try
                {
                    HttpResponseMessage response = await WBhttpClient.GetAsync(URL);
                    var responseBodyAsText = await response.Content.ReadAsStringAsync();
                    LogDebugMessage("Wbug Response: " + response.StatusCode + ": " + responseBodyAsText);
                }
                catch (Exception ex)
                {
                    LogDebugMessage("Wbug update: " + ex.Message);
                }
                finally
                {
                    UpdatingWB = false;
                }
            }
        }

        private void AddToWeatherbugList(DateTime timestamp)
        {
            if (WeatherbugEnabled && WeatherbugCatchUp)
            {
                string pwstring;

                string URL = station.GetWeatherbugURL(out pwstring, timestamp, true);

                WeatherbugList.Add(URL);

                string starredpwstring = "&Key=" + new string('*', WeatherbugPW.Length);

                string LogURL = URL.Replace(pwstring, starredpwstring);

                LogMessage("Creating Weatherbug URL #" + WeatherbugList.Count);

                LogMessage(LogURL);
            }
        }
        public string GetWeatherbugURL(out string pwstring, DateTime timestamp, bool catchup)
        {
            string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
            string URL = "http://data.backyard2.weatherbug.com/data/livedata.aspx?ID=";

            pwstring = "&Key=" + cumulus.WeatherbugPW;
            URL += cumulus.WeatherbugID + pwstring + "&num=" + cumulus.WeatherbugNumber + "&dateutc=" + dateUTC;
            string Data = "";

            // send average speed and bearing
            Data += "&winddir=" + AvgBearing + "&windspeedmph=" + WindMPHStr(WindAverage);

            Data += "&windgustmph=" + WindMPHStr(RecentMaxGust);

            Data += "&humidity=" + OutdoorHumidity + "&tempf=" + TempFstr(OutdoorTemperature) + "&rainin=" + RainINstr(RainLastHour) + "&dailyrainin=";

            if (cumulus.RolloverHour == 0)
            {
                // use today"s rain
                Data += RainINstr(RainToday);
            }
            else
            {
                Data += RainINstr(RainSinceMidnight);
            }

            Data += "&monthlyrainin=" + RainINstr(RainMonth);
            Data += "&yearlyrainin=" + RainINstr(RainYear);

            Data += "&baromin=" + PressINstr(Pressure) + "&dewptf=" + TempFstr(OutdoorDewpoint);

            if (cumulus.SendUVToWeatherbug)
            {
                Data += "&UV=" + UV.ToString(cumulus.UVFormat);
            }

            if (cumulus.SendSRToWeatherbug)
            {
                Data += "&solarradiation=" + SolarRad.ToString("F0");
            }

            Data += "&softwaretype=Cumulus%20v" + cumulus.Version + "&action=updateraw";

            Data = cumulus.ReplaceCommas(Data);
            URL = URL + Data;

            return URL;
        }

*/
    }
}
