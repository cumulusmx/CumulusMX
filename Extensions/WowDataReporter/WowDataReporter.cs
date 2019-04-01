using System;
using System.Net.Http;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace WowDataReporter
{
    public class WowDataReporter : DataReporterBase
    {
        private ILogger _logger;
        public override string ServiceName => "Wow Data Reporter Service";

        public IDataReporterSettings Settings { get; private set; }

        public override void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        public override string Identifier => "TBC"; //TODO

        public WowDataReporter(ILogger logger, DataReporterSettingsGeneric settings, IWeatherDataStatistics data) : base (logger,settings,data)
        {
            _logger = logger;
            Settings = settings as IDataReporterSettings;
        }

        
        public override void Initialise()
        {
        }

        /*
                public async void UpdateWOW(DateTime timestamp)
                {
                    if (!UpdatingWOW)
                    {
                        UpdatingWOW = true;
                        string pwstring;

                        string URL = station.GetWOWURL(out pwstring, timestamp, false);

                        string starredpwstring = "&siteAuthenticationKey=" + new string('*', WOWPW.Length);

                        string LogURL = URL.Replace(pwstring, starredpwstring);
                        LogDebugMessage(LogURL);

                        try
                        {
                            HttpResponseMessage response = await WOWhttpClient.GetAsync(URL);
                            var responseBodyAsText = await response.Content.ReadAsStringAsync();
                            LogDebugMessage("WOW Response: " + response.StatusCode + ": " + responseBodyAsText);
                        }
                        catch (Exception ex)
                        {
                            LogDebugMessage("WOW update: " + ex.Message);
                        }
                        finally
                        {
                            UpdatingWOW = false;
                        }
                    }
                }

                /// <summary>
                /// Process the list of WOW updates created at startup from logger entries
                /// </summary>
                private async void WOWCatchup()
                {
                    UpdatingWOW = true;

                    for (int i = 0; i < WOWList.Count; i++)
                    {
                        LogMessage("Uploading WOW archive #" + (i + 1));
                        try
                        {
                            HttpResponseMessage response = await PWShttpClient.GetAsync(WOWList[i]);
                            var responseBodyAsText = await response.Content.ReadAsStringAsync();
                            LogMessage("WOW Response: " + response.StatusCode + ": " + responseBodyAsText);
                        }
                        catch (Exception ex)
                        {
                            LogMessage("WOW update: " + ex.Message);
                        }
                    }

                    LogMessage("End of WOW archive upload");
                    WOWList.Clear();
                    WOWCatchingUp = false;
                    WOWTimer.Enabled = WOWEnabled && !SynchronisedWOWUpdate;
                    UpdatingWOW = false;
                }

                private void AddToWOWList(DateTime timestamp)
                {
                    if (WOWEnabled && WOWCatchUp)
                    {
                        string pwstring;

                        string URL = station.GetWOWURL(out pwstring, timestamp, true);

                        WOWList.Add(URL);

                        string starredpwstring = "&siteAuthenticationKey=" + new string('*', WOWPW.Length);

                        string LogURL = URL.Replace(pwstring, starredpwstring);

                        LogMessage("Creating WOW URL #" + WOWList.Count);

                        LogMessage(LogURL);
                    }
                }

                public string GetWOWURL(out string pwstring, DateTime timestamp, bool catchup)
                {
                    string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
                    string URL = "http://wow.metoffice.gov.uk/automaticreading?siteid=";

                    pwstring = "&siteAuthenticationKey=" + cumulus.WOWPW;
                    URL += cumulus.WOWID + pwstring + "&dateutc=" + dateUTC;
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

                    Data += "&baromin=" + PressINstr(Pressure) + "&dewptf=" + TempFstr(OutdoorDewpoint);

                    if (cumulus.SendUVToWOW)
                    {
                        Data += "&UV=" + UV.ToString(cumulus.UVFormat);
                    }

                    if (cumulus.SendSRToWOW)
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
