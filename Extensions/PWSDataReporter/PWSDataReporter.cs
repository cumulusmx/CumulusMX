using System;
using System.Net.Http;
using CumulusMX.Extensions;
using CumulusMX.Extensions.DataReporter;
using CumulusMX.Extensions.Station;

namespace PwsDataReporter
{
    public class PwsDataReporter : DataReporterBase
    {
        public override string ServiceName => "Pws Data Reporter Service";

        public IDataReporterSettings Settings { get; private set; }

        public override void DoReport(IWeatherDataStatistics currentData)
        {
            throw new NotImplementedException();
        }

        public override string Identifier => "TBC"; //TODO

        public PwsDataReporter(ILogger logger, DataReporterSettingsGeneric settings, IWeatherDataStatistics data) : base(logger, settings, data)
        {
            Settings = settings as IDataReporterSettings;
        }

        public override void Initialise()
        {
        }


        //public async void UpdatePWSweather(DateTime timestamp)
        //{
        //    if (!UpdatingPWS)
        //    {
        //        UpdatingPWS = true;

        //        string pwstring;

        //        string URL = station.GetPWSURL(out pwstring, timestamp, false);

        //        string starredpwstring = "&PASSWORD=" + new string('*', PWSPW.Length);

        //        string LogURL = URL.Replace(pwstring, starredpwstring);
        //        LogDebugMessage(LogURL);

        //        try
        //        {
        //            HttpResponseMessage response = await PWShttpClient.GetAsync(URL);
        //            var responseBodyAsText = await response.Content.ReadAsStringAsync();
        //            LogDebugMessage("PWS Response: " + response.StatusCode + ": " + responseBodyAsText);
        //        }
        //        catch (Exception ex)
        //        {
        //            LogDebugMessage("PWS update: " + ex.Message);
        //        }
        //        finally
        //        {
        //            UpdatingPWS = false;
        //        }
        //    }
        //}


        ///// <summary>
        ///// Process the list of PWS Weather updates created at startup from logger entries
        ///// </summary>
        //private async void PWSCatchup()
        //{
        //    UpdatingPWS = true;

        //    for (int i = 0; i < PWSList.Count; i++)
        //    {
        //        LogMessage("Uploading PWS archive #" + (i + 1));
        //        try
        //        {
        //            HttpResponseMessage response = await PWShttpClient.GetAsync(PWSList[i]);
        //            var responseBodyAsText = await response.Content.ReadAsStringAsync();
        //            LogMessage("PWS Response: " + response.StatusCode + ": " + responseBodyAsText);
        //        }
        //        catch (Exception ex)
        //        {
        //            LogMessage("PWS update: " + ex.Message);
        //        }
        //    }

        //    LogMessage("End of PWS archive upload");
        //    PWSList.Clear();
        //    PWSCatchingUp = false;
        //    PWSTimer.Enabled = PWSEnabled && !SynchronisedPWSUpdate;
        //    UpdatingPWS = false;
        //}
        //private void AddToPWSList(DateTime timestamp)
        //{
        //    if (PWSEnabled && PWSCatchUp)
        //    {
        //        string pwstring;

        //        string URL = station.GetPWSURL(out pwstring, timestamp, true);

        //        PWSList.Add(URL);

        //        string starredpwstring = "&PASSWORD=" + new string('*', PWSPW.Length);

        //        string LogURL = URL.Replace(pwstring, starredpwstring);

        //        LogMessage("Creating PWS URL #" + PWSList.Count);

        //        LogMessage(LogURL);
        //    }
        //}
        //public string GetPWSURL(out string pwstring, DateTime timestamp, bool catchup)
        //{
        //    string dateUTC = timestamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'+'HH'%3A'mm'%3A'ss");
        //    string URL = "http://www.pwsweather.com/pwsupdate/pwsupdate.php?ID=";

        //    pwstring = "&PASSWORD=" + cumulus.PWSPW;
        //    URL += cumulus.PWSID + pwstring + "&dateutc=" + dateUTC;
        //    string Data = "";

        //    // send average speed and bearing
        //    Data += "&winddir=" + AvgBearing + "&windspeedmph=" + WindMPHStr(WindAverage);

        //    Data += "&windgustmph=" + WindMPHStr(RecentMaxGust);

        //    Data += "&humidity=" + OutdoorHumidity + "&tempf=" + TempFstr(OutdoorTemperature) + "&rainin=" + RainINstr(RainLastHour) + "&dailyrainin=";

        //    if (cumulus.RolloverHour == 0)
        //    {
        //        // use today"s rain
        //        Data += RainINstr(RainToday);
        //    }
        //    else
        //    {
        //        Data += RainINstr(RainSinceMidnight);
        //    }

        //    Data += "&baromin=" + PressINstr(Pressure) + "&dewptf=" + TempFstr(OutdoorDewpoint);

        //    if (cumulus.SendUVToPWS)
        //    {
        //        Data += "&UV=" + UV.ToString(cumulus.UVFormat);
        //    }

        //    if (cumulus.SendSRToPWS)
        //    {
        //        Data += "&solarradiation=" + SolarRad.ToString("F0");
        //    }

        //    Data += "&softwaretype=Cumulus%20v" + cumulus.Version + "&action=updateraw";

        //    Data = cumulus.ReplaceCommas(Data);
        //    URL = URL + Data;

        //    return URL;
        //}


    }
}
