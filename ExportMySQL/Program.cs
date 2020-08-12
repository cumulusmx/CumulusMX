using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using CumulusMX;
using Devart.Data.MySql;

namespace ExportMySQL
{
    internal class Program
    {
        private static string MySqlHost;
        private static int MySqlPort;
        private static string MySqlUser;
        private static string MySqlPass;
        private static string MySqlDatabase;
        private static string MySqlMonthlyTable;
        private static string MySqlDayfileTable;

        private static string[] compassp = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };

        private static void Main(string[] args)
        {
            string param = "";

            if (args.Length == 0)
            {
                Console.WriteLine("Specify 'dayfile', 'monthly', or the path to a monthly log file");
                Environment.Exit(0);
            }
            else
            {
                param = args[0];
                Console.WriteLine("Parameter: "+param);
            }

            if (!File.Exists("Cumulus.ini"))
            {
                Console.WriteLine("Cannot find Cumulus.ini");
                Environment.Exit(0);
            }

            IniFile ini = new IniFile("Cumulus.ini");

            MySqlHost = ini.GetValue("MySQL", "Host", "127.0.0.1");
            MySqlPort = ini.GetValue("MySQL", "Port", 3306);
            MySqlUser = ini.GetValue("MySQL", "User", "");
            MySqlPass = ini.GetValue("MySQL", "Pass", "");
            MySqlDatabase = ini.GetValue("MySQL", "Database", "database");
            MySqlMonthlyTable = ini.GetValue("MySQL", "MonthlyTable", "Monthly");
            MySqlDayfileTable = ini.GetValue("MySQL", "DayfileTable", "Dayfile");

            if (File.Exists("strings.ini"))
            {
                IniFile iniStrs = new IniFile("strings.ini");
                compassp[0] = iniStrs.GetValue("Compass", "N", "N");
                compassp[1] = iniStrs.GetValue("Compass", "NNE", "NNE");
                compassp[2] = iniStrs.GetValue("Compass", "NE", "NE");
                compassp[3] = iniStrs.GetValue("Compass", "ENE", "ENE");
                compassp[4] = iniStrs.GetValue("Compass", "E", "E");
                compassp[5] = iniStrs.GetValue("Compass", "ESE", "ESE");
                compassp[6] = iniStrs.GetValue("Compass", "SE", "SE");
                compassp[7] = iniStrs.GetValue("Compass", "SSE", "SSE");
                compassp[8] = iniStrs.GetValue("Compass", "S", "S");
                compassp[9] = iniStrs.GetValue("Compass", "SSW", "SSW");
                compassp[10] = iniStrs.GetValue("Compass", "SW", "SW");
                compassp[11] = iniStrs.GetValue("Compass", "WSW", "WSW");
                compassp[12] = iniStrs.GetValue("Compass", "W", "W");
                compassp[13] = iniStrs.GetValue("Compass", "WNW", "WNW");
                compassp[14] = iniStrs.GetValue("Compass", "NW", "NW");
                compassp[15] = iniStrs.GetValue("Compass", "NNW", "NNW");
            }

            if (param.ToLower().Equals("dayfile"))
            {
                doDayfileExport();
            }
            else if (param.ToLower().Equals("monthly"))
            {
                doMonthlyExport();
            }
            else
            {
                if (File.Exists(param))
                {
                    doSingleMonthlyExport(param);
                }
                else
                {
                    Console.WriteLine("Cannot find file: " + param);
                }
            }

            Console.WriteLine();
        }

        private static void doSingleMonthlyExport(string filename)
        {
            var mySqlConn = new MySqlConnection
            {
                Host = MySqlHost,
                Port = MySqlPort,
                UserId = MySqlUser,
                Password = MySqlPass,
                Database = MySqlDatabase
            };

            MySqlCommand cmd = new MySqlCommand
            {
                Connection = mySqlConn
            };

            var StartOfMonthlyInsertSQL = "INSERT IGNORE INTO " + MySqlMonthlyTable + " (LogDateTime,Temp,Humidity,Dewpoint,Windspeed,Windgust,Windbearing,RainRate,TodayRainSoFar,Pressure,Raincounter,InsideTemp,InsideHumidity,LatestWindGust,WindChill,HeatIndex,UVindex,SolarRad,Evapotrans,AnnualEvapTran,ApparentTemp,MaxSolarRad,HrsSunShine,CurrWindBearing,RG11rain,RainSinceMidnight,FeelsLike,Humidex,WindbearingSym,CurrWindBearingSym)";

            try
            {
                mySqlConn.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error encountered opening MySQL connection");
                Console.WriteLine(ex.Message);
                Environment.Exit(0);
            }

            var InvC = new CultureInfo("");

            using (var sr = new StreamReader(filename))
            {
                int linenum = 0;
                do
                {
                    // now process each record in the file

                    try
                    {
                        var line = sr.ReadLine();
                        linenum++;
                        var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

                        var logfiledate = st[0];
                        // 01234567
                        // dd/mm/yy

                        var logfiletime = st[1];
                        // 01234
                        // hh:mm

                        //Console.WriteLine(st[0]);

                        string sqldate = logfiledate.Substring(6, 2) + '-' + logfiledate.Substring(3, 2) + '-' + logfiledate.Substring(0, 2) + ' ' + logfiletime.Substring(0,2) + ':'+ logfiletime.Substring(3,2);

                        Console.Write(sqldate+"\r");
                        StringBuilder sb = new StringBuilder(StartOfMonthlyInsertSQL + " Values('" + sqldate + "',");

                        for (int i = 2; i < 29; i++)
                        {
                            if (i < st.Count && !String.IsNullOrEmpty(st[i]))
                            {
                                sb.Append("'" + st[i] + "',");
                            }
                            else
                            {
                                sb.Append("NULL,");
                            }
                        }
                        sb.Append("'" + CompassPoint(Convert.ToInt32(st[7])) + "',");
                        if (st.Count > 24 && !String.IsNullOrEmpty(st[24]))
                        {
                            sb.Append("'" + CompassPoint(Convert.ToInt32(st[24])) + "')");
                        }
                        else
                        {
                            sb.Append("NULL)");
                        }

                        cmd.CommandText = sb.ToString();
                        //Console.WriteLine(sb.ToString());

                        int aff = cmd.ExecuteNonQuery();

                        //Console.WriteLine();


                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                } while (!(sr.EndOfStream));

            }

            mySqlConn.Close();

        }

        private static void doMonthlyExport()
        {
            for (int y = 0; y < 100; y++)
            {
                for (int m = 1; m < 12; m++)
                {
                    DateTime logfiledate = new DateTime(y+2000,m,1);

                    var datestring = logfiledate.ToString("MMMyy");

                    var filename = "data" + Path.DirectorySeparatorChar + datestring + "log.txt";

                    if (File.Exists(filename))
                    {
                        doSingleMonthlyExport(filename);
                    }
                }
            }
        }

        private static void doDayfileExport()
        {
            var mySqlConn = new MySqlConnection
            {
                Host = MySqlHost,
                Port = MySqlPort,
                UserId = MySqlUser,
                Password = MySqlPass,
                Database = MySqlDatabase
            };

            MySqlCommand cmd = new MySqlCommand
            {
                Connection = mySqlConn
            };

            var filename = "data" + Path.DirectorySeparatorChar + "dayfile.txt";

            Console.WriteLine("Exporting dayfile: "+filename);

            if (File.Exists(filename))
            {
                Console.WriteLine("Dayfile exists, beginning export");
                string StartOfDayfileInsertSQL = "INSERT IGNORE INTO " + MySqlDayfileTable + " (LogDate,HighWindGust,HWindGBear,THWindG,MinTemp,TMinTemp,MaxTemp,TMaxTemp,MinPress,TMinPress,MaxPress,TMaxPress,MaxRainRate,TMaxRR,TotRainFall,AvgTemp,TotWindRun,HighAvgWSpeed,THAvgWSpeed,LowHum,TLowHum,HighHum,THighHum,TotalEvap,HoursSun,HighHeatInd,THighHeatInd,HighAppTemp,THighAppTemp,LowAppTemp,TLowAppTemp,HighHourRain,THighHourRain,LowWindChill,TLowWindChill,HighDewPoint,THighDewPoint,LowDewPoint,TLowDewPoint,DomWindDir,HeatDegDays,CoolDegDays,HighSolarRad,THighSolarRad,HighUV,THighUV,MaxFeelsLike,TMaxFeelsLike,MinFeelsLike,TMinFeelsLike,MaxHumidex,TMaxHumidex,HWindGBearSym,DomWindDirSym)";

                try
                {
                    mySqlConn.Open();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error encountered opening MySQL connection");
                    Console.WriteLine(ex.Message);
                    Environment.Exit(0);
                }

                int linenum = 0;

                using (var sr = new StreamReader(filename))
                {
                    Console.WriteLine("Dayfile opened");

                    do
                    {
                        // now process each record in the file

                        try
                        {
                            var line = sr.ReadLine();
                            linenum++;
                            var st = new List<string>(Regex.Split(line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));

                            var dayfiledate = st[0];
                            // 01234567
                            // dd/mm/yy

                            string sqldate = dayfiledate.Substring(6, 2) + '-' + dayfiledate.Substring(3, 2) + '-' + dayfiledate.Substring(0, 2);

                            Console.Write(sqldate + "\r");

                            StringBuilder sb = new StringBuilder(StartOfDayfileInsertSQL + " Values('" + sqldate + "',");

                            for (int i = 1; i < 52; i++)
                            {
                                if (i < st.Count && !String.IsNullOrEmpty(st[i]))
                                {
                                    sb.Append("'" + st[i] + "',");
                                }
                                else
                                {
                                    sb.Append("NULL,");
                                }
                            }
                            sb.Append("'" + CompassPoint(Convert.ToInt32(st[2])) + "',");
                            if (st.Count > 39 && !String.IsNullOrEmpty(st[39]))
                            {
                                sb.Append("'" + CompassPoint(Convert.ToInt32(st[39])) + "')");
                            }
                            else
                            {
                                sb.Append("NULL)");
                            }

                            cmd.CommandText = sb.ToString();
                            //Console.WriteLine(sb.ToString());

                            int aff = cmd.ExecuteNonQuery();

                            //Console.WriteLine();


                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    } while (!(sr.EndOfStream));

                }

                Console.WriteLine();
                Console.WriteLine(linenum+" entries processed");

                mySqlConn.Close();
            }
        }

        private static string CompassPoint(int bearing)
        {
            return bearing == 0 ? "-" : compassp[(((bearing * 100) + 1125) % 36000) / 2250];
        }
    }
}
