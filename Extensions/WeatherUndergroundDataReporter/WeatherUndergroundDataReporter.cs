using System;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace WeatherUndergroundDataReporter
{
    public class WeatherUndergroundDataReporter : IDataReporter
    {
        public string ServiceName => "Weather Underground";

        public IDataReporterSettings Settings { get; private set; }

        public void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        public string Identifier => throw new NotImplementedException();

        private ILogger _logger;

        public WeatherUndergroundDataReporter()
        {
            //TODO: Implement
        }

        public void Initialise(ILogger logger, ISettings settings)
        {
            _logger = logger;
            Settings = settings as IDataReporterSettings;
        }


        /*
        internal async void UpdateWunderground(DateTime timestamp)
        {
            if (!UpdatingWU)
            {
                UpdatingWU = true;

                string pwstring;

                string URL = station.GetWundergroundURL(out pwstring, timestamp, false);

                string starredpwstring = "&PASSWORD=" + new string('*', WundPW.Length);

                string LogURL = URL.Replace(pwstring, starredpwstring);
                if (!WundRapidFireEnabled)
                {
                    LogDebugMessage(LogURL);
                }

                try
                {
                    HttpResponseMessage response = await WUhttpClient.GetAsync(URL);
                    var responseBodyAsText = await response.Content.ReadAsStringAsync();
                    if (!WundRapidFireEnabled)
                    {
                        LogMessage("WU Response: " + response.StatusCode + ": " + responseBodyAsText);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage("WU update: " + ex.Message);
                }
                finally
                {
                    UpdatingWU = false;
                }
            }
        }

        /// <summary>
        /// Process the list of WU updates created at startup from logger entries
        /// </summary>
        private async void WundCatchup()
        {
            UpdatingWU = true;
            for (int i = 0; i < WundList.Count; i++)
            {
                LogMessage("Uploading WU archive #" + (i + 1));
                try
                {
                    HttpResponseMessage response = await WUhttpClient.GetAsync(WundList[i]);
                    LogMessage("WU Response: " + response.StatusCode + ": " + response.ReasonPhrase);
                }
                catch (Exception ex)
                {
                    LogMessage("WU update: " + ex.Message);
                }
            }


            LogMessage("End of WU archive upload");
            WundList.Clear();
            WundCatchingUp = false;
            WundTimer.Enabled = WundEnabled && !SynchronisedWUUpdate;
            UpdatingWU = false;
        }

        /// <summary>
        /// Add an archive entry to the WU 'catchup' list for sending to WU
        /// </summary>
        /// <param name="timestamp"></param>
        private void AddToWundList(DateTime timestamp)
        {
            if (WundEnabled && WundCatchUp)
            {
                string pwstring;

                string URL = station.GetWundergroundURL(out pwstring, timestamp, true);

                WundList.Add(URL);

                string starredpwstring = "&PASSWORD=" + new string('*', WundPW.Length);

                string LogURL = URL.Replace(pwstring, starredpwstring);

                LogMessage("Creating WU URL #" + WundList.Count);

                LogMessage(LogURL);
            }
        }
        public string GetWundergroundURL(out string pwstring, DateTime timestamp, bool catchup)
        {
            string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
            string URL;
            if (cumulus.WundRapidFireEnabled && !catchup)
            {
                URL = "http://rtupdate.wunderground.com/weatherstation/updateweatherstation.php?ID=";
            }
            else
            {
                URL = "http://weatherstation.wunderground.com/weatherstation/updateweatherstation.php?ID=";
            }

            pwstring = "&PASSWORD=" + cumulus.WundPW;
            URL = URL + cumulus.WundID + pwstring + "&dateutc=" + dateUTC;
            string Data = "";
            if (cumulus.WundSendAverage)
            {
                // send average speed and bearing
                Data = Data + "&winddir=" + AvgBearing + "&windspeedmph=" + WindMPHStr(WindAverage);
            }
            else
            {
                // send "instantaneous" speed (i.e. latest) and bearing
                Data = Data + "&winddir=" + Bearing + "&windspeedmph=" + WindMPHStr(WindLatest);
            }
            Data = Data + "&windgustmph=" + WindMPHStr(RecentMaxGust);
            // may not strictly be a 2 min average!
            Data = Data + "&windspdmph_avg2m=" + WindMPHStr(WindAverage);
            Data = Data + "&winddir_avg2m=" + AvgBearing;
            Data = Data + "&humidity=" + OutdoorHumidity + "&tempf=" + TempFstr(OutdoorTemperature) + "&rainin=" + RainINstr(RainLastHour) + "&dailyrainin=";
            if (cumulus.RolloverHour == 0)
            {
                // use today"s rain
                Data = Data + RainINstr(RainToday);
            }
            else
            {
                Data = Data + RainINstr(RainSinceMidnight);
            }
            Data = Data + "&baromin=" + PressINstr(Pressure) + "&dewptf=" + TempFstr(OutdoorDewpoint);
            if (cumulus.SendUVToWund)
                Data = Data + "&UV=" + UV.ToString(cumulus.UVFormat);
            if (cumulus.SendSRToWund)
                Data = Data + "&solarradiation=" + SolarRad.ToString("F0");
            if (cumulus.SendIndoorToWund)
                Data = Data + "&indoortempf=" + TempFstr(IndoorTemperature) + "&indoorhumidity=" + IndoorHumidity;
            // Davis soil and leaf sensors
            if (cumulus.SendSoilTemp1ToWund)
                Data = Data + "&soiltempf=" + TempFstr(SoilTemp1);
            if (cumulus.SendSoilTemp2ToWund)
                Data = Data + "&soiltempf2=" + TempFstr(SoilTemp2);
            if (cumulus.SendSoilTemp3ToWund)
                Data = Data + "&soiltempf3=" + TempFstr(SoilTemp3);
            if (cumulus.SendSoilTemp4ToWund)
                Data = Data + "&soiltempf4=" + TempFstr(SoilTemp4);

            if (cumulus.SendSoilMoisture1ToWund)
                Data = Data + "&soilmoisture=" + SoilMoisture1;
            if (cumulus.SendSoilMoisture2ToWund)
                Data = Data + "&soilmoisture2=" + SoilMoisture2;
            if (cumulus.SendSoilMoisture3ToWund)
                Data = Data + "&soilmoisture3=" + SoilMoisture3;
            if (cumulus.SendSoilMoisture4ToWund)
                Data = Data + "&soilmoisture4=" + SoilMoisture4;

            if (cumulus.SendLeafWetness1ToWund)
                Data = Data + "&leafwetness=" + LeafWetness1;
            if (cumulus.SendLeafWetness2ToWund)
                Data = Data + "&leafwetness2=" + LeafWetness2;

            Data = Data + "&softwaretype=Cumulus%20v" + cumulus.Version + "&action=updateraw";
            if (cumulus.WundRapidFireEnabled && !catchup)
                Data = Data + "&realtime=1&rtfreq=5";
            //MainForm.SystemLog.WriteLogString(TimeToStr(Now) + " Updating Wunderground");
            Data = cumulus.ReplaceCommas(Data);
            URL = URL + Data;

            return URL;
        }

*/
    }
}
