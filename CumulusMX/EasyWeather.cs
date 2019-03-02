using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

namespace CumulusMX
{
    /*
     * EasyWeather.dat file format (* indicates fields used by Cumulus)
     * 1    Record number       int
     * 2    Transfer Date       yyy-mmm-dd hh:mm:ss
     * 3*   Reading Date        yyy-mmm-dd hh:mm:ss
     * 4    Reading interval    int     (minutes since previous reading)
     * 5*   Indoor humidity     int
     * 6*   Indoor temp         float   Celcius
     * 7*   Outdoor humidity    int
     * 8*   Outdoor temp        float   Celcius
     * 9*   Dew point           float   Celcius
     * 10*  Wind chill          float   Celcius
     * 11   Absolute press      float   mB/hPa
     * 12*  Relative press      float   mB/hPa
     * 13*  Wind average        float   m/s
     * 14   Wind average        int     Beaufort
     * 15*  Wind gust           float   m/s
     * 16   Wind gust           int     Beaufort
     * 17   Wind direction      int     0 - 15. 0 = North, 1 = NNE etc
     * 18*  Wind direction      str     Oddly ENE appears as NEE, and ESE appears as SEE
     * 19   Rain ticks          int
     * 20   Rain total          float   mm
     * 21   Rain since last     float   mm
     * 22*  Rain in last hour   float   mm
     * 23   Rain in last 24h    float   mm
     * 24   Rain in last 7d     float   mm
     * 25   Rain in last 30d    float   mm
     * 26*  Rain total          float   mm
     * 27*  Light reading       int     Lux
     * 28*  UV Index            int
     * 29   Status bit #0       int     0|1
     * 30   Status bit #1       int     0|1
     * 31   Status bit #2       int     0|1
     * 32   Status bit #3       int     0|1
     * 33   Status bit #4       int     0|1
     * 34   Status bit #5       int     0|1
     * 35   Status bit #6       int     0|1 - Outdoor readings invalid = 1
     * 36   Status bit #7       int     0|1
     * 37   Data address        6 digit hex
     * 38   Raw data            16x 2-digit hex
    */
    internal class EasyWeather : WeatherStation
    {
        private Timer tmrDataRead;

        private const int EW_READING_DATE = 3;
        private const int EW_READING_TIME = 4;
        private const int EW_INDOOR_HUM = 6;
        private const int EW_INDOOR_TEMP = 7;
        private const int EW_OUTDOOR_HUM = 8;
        private const int EW_OUTDOOR_TEMP = 9;
        private const int EW_DEW_POINT = 10;
        private const int EW_WIND_CHILL = 11;
        private const int EW_REL_PRESSURE = 13;
        private const int EW_AVERAGE_WIND = 14;
        private const int EW_WIND_GUST = 16;
        private const int EW_WIND_BEARING_CP = 19;
        private const int EW_RAIN_LAST_HOUR = 23;
        private const int EW_RAIN_LAST_YEAR = 27;
        private const int EW_LIGHT = 28;
        private const int EW_UV = 29;

        private string lastTime = "";
        private string lastDate = "";

        public EasyWeather(Cumulus cumulus) : base(cumulus)
        {
            tmrDataRead = new Timer();
        }

        public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public override void Start()
        {
            tmrDataRead.Elapsed += EWGetData;
            tmrDataRead.Interval = cumulus.EWInterval*60*1000;
            tmrDataRead.Enabled = true;

            DoDayResetIfNeeded();
            DoTrendValues(DateTime.Now);
            
            if (File.Exists(cumulus.EWFile))
            {
                EWGetData(null, null);
                cumulus.StartTimers();
            }
        }

        public override void Stop()
        {
        }

        private void EWGetData(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (File.Exists(cumulus.EWFile))
            {
                try
                {
                    string Line;

                    using (var sr = new StreamReader(cumulus.EWFile))
                    {
                        do
                        {
                            Line = sr.ReadLine();
                        } while (!sr.EndOfStream);

                        cumulus.LogDataMessage("Data: " + Line);

                        // split string on commas and spaces
                        char[] charSeparators = new char[] { ',', ' ' };

                        var st = Line.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);

                        string datestr = st[EW_READING_DATE];
                        string timestr = st[EW_READING_TIME];

                        DateTime Now = DateTime.Now;

                        if ((datestr != lastDate) || (timestr != lastTime))
                        {
                            lastDate = datestr;
                            lastTime = timestr;
                        }

                        DoWind(ConvertWindMSToUser(GetConvertedValue(st[EW_WIND_GUST])), CPtoBearing(st[EW_WIND_BEARING_CP]),
                            ConvertWindMSToUser(GetConvertedValue(st[EW_AVERAGE_WIND])), Now);

                        DoWindChill(ConvertTempCToUser(GetConvertedValue(st[EW_WIND_CHILL])), Now);

                        DoIndoorHumidity(Convert.ToInt32(st[EW_INDOOR_HUM]));
                        DoOutdoorHumidity(Convert.ToInt32(st[EW_OUTDOOR_HUM]), Now);

                        DoOutdoorDewpoint(ConvertTempCToUser(GetConvertedValue(st[EW_DEW_POINT])), Now);

                        DoPressure(ConvertPressMBToUser(GetConvertedValue(st[EW_REL_PRESSURE])), Now);

                        UpdatePressureTrendString();

                        DoIndoorTemp(ConvertTempCToUser(GetConvertedValue(st[EW_INDOOR_TEMP])));
                        DoOutdoorTemp(ConvertTempCToUser(GetConvertedValue(st[EW_OUTDOOR_TEMP])), Now);

                        DoRain(ConvertRainMMToUser(GetConvertedValue(st[EW_RAIN_LAST_YEAR])), // use year as total
                            ConvertRainMMToUser(GetConvertedValue(st[EW_RAIN_LAST_HOUR])), // use last hour as current rate
                            Now);

                        DoApparentTemp(Now);

                        DoForecast("", false);

                        if (cumulus.LogExtraSensors)
                        {
                            var LightReading = GetConvertedValue(st[EW_LIGHT]);

                            if ((LightReading >= 0) && (LightReading <= 300000))
                            {
                                DoSolarRad((int)(LightReading * cumulus.LuxToWM2), Now);
                                LightValue = LightReading;
                            }

                            var UVreading = GetConvertedValue(st[EW_UV]);

                            if (UVreading == 255)
                            {
                                // ignore
                            }
                            else if (UVreading < 0)
                            {
                                DoUV(0, Now);
                            }
                            else if (UVreading > 16)
                            {
                                DoUV(16, Now);
                            }
                            else
                            {
                                DoUV(UVreading, Now);
                            }
                        }

                        UpdateStatusPanel(Now);
                    }
                }
                catch (Exception ex)
                {
                    cumulus.LogMessage("Error while processing easyweather file: " + ex.Message);
                }
            } else
            {
                cumulus.LogDebugMessage("Easyweather file not found");
            }
        }

        private int CPtoBearing(string cp)
        {
            switch (cp)
            {
                case "N":
                    return 360;
                case "NNE":
                    return 22;
                case "NE":
                    return 45;
                case "NEE":
                    return 67;
                case "ENE":
                    return 67;
                case "E":
                    return 90;
                case "SEE":
                    return 112;
                case "EES":
                    return 112;
                case "ESE":
                    return 112;
                case "SE":
                    return 135;
                case "SSE":
                    return 157;
                case "S":
                    return 180;
                case "SSW":
                    return 202;
                case "SW":
                    return 225;
                case "SWW":
                    return 247;
                case "WSW":
                    return 247;
                case "W":
                    return 270;
                case "NWW":
                    return 292;
                case "WNW":
                    return 292;
                case "NW":
                    return 315;
                case "NNW":
                    return 337;
                default:
                    return 0;
            }
        }

        private string ConvertPeriodToSystemDecimal(string AStr)
        {
            return AStr.Replace(".", cumulus.DecimalSeparator);
        }

        private double GetConvertedValue(string AStr)
        {
            return Convert.ToDouble(ConvertPeriodToSystemDecimal(AStr));
        }
    }
}