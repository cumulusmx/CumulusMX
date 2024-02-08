using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace CumulusMX.ThirdParty
{
	internal class WebUploadAprs : WebUploadServiceBase
	{
		public bool HumidityCutoff;

		internal WebUploadAprs(Cumulus cumulus, string name) : base(cumulus, name)
		{
		}


		internal override async Task DoUpdate(DateTime timestamp)
		{
			if (station.DataStopped)
			{
				// No data coming in, do nothing
				return;
			}

			cumulus.LogDebugMessage("Updating CWOP");
			try
			{
				using var client = new TcpClient(cumulus.APRS.Server, cumulus.APRS.Port);
				using var ns = client.GetStream();
				using (StreamWriter writer = new StreamWriter(ns))
				{
					StringBuilder message = new StringBuilder(256);
					message.Append($"user {cumulus.APRS.ID} pass {cumulus.APRS.PW} vers Cumulus {cumulus.Version}");

					//Byte[] data = Encoding.ASCII.GetBytes(message.ToString());

					cumulus.LogDebugMessage("Sending user and pass to CWOP");

					await writer.WriteLineAsync(message.ToString());
					writer.Flush();

					await Task.Delay(3000);

					string timeUTC = DateTime.Now.ToUniversalTime().ToString("ddHHmm");

					message.Clear();
					message.Append($"{cumulus.APRS.ID}>APRS,TCPIP*:@{timeUTC}z{APRSLat(cumulus)}/{APRSLon(cumulus)}");
					// bearing _nnn
					if (station.AvgBearing>= 0)
						message.Append($"_{station.AvgBearing:D3}");
					// wind speed mph /nnn
					if (station.WindAverage >= 0)
						message.Append($"/{APRSwind(station.WindAverage)}");
					// wind gust last 5 mins mph gnnn
					if (station.RecentMaxGust >= 0)
						message.Append($"g{APRSwind(station.RecentMaxGust)}");
					// temp F tnnn
					if (station.OutdoorTemperature > Cumulus.DefaultHiVal)
						message.Append($"t{APRStemp(station.OutdoorTemperature, cumulus.Units.Temp)}");
					// rain last hour 0.01 inches rnnn
					message.Append($"r{APRSrain(station.RainLastHour)}");
					// rain last 24 hours 0.01 inches pnnn
					message.Append($"p{APRSrain(station.RainLast24Hour)}");
					message.Append('P');
					if (cumulus.RolloverHour == 0)
					{
						// use today"s rain for safety
						message.Append(APRSrain(station.RainToday));
					}
					else
					{
						// 0900 day, use midnight calculation
						message.Append(APRSrain(station.RainSinceMidnight));
					}
					if ((!cumulus.APRS.HumidityCutoff) || (ConvertUnits.UserTempToC(station.OutdoorTemperature) >= -10) && station.OutdoorHumidity >= 0)
					{
						// humidity Hnn
						message.Append($"h{APRShum(station.OutdoorHumidity)}");
					}
					// bar 0.1mb Bnnnnn
					if (station.AltimeterPressure >= 0)
					{
						message.Append($"b{APRSpress(station.AltimeterPressure)}");
					}
					if (cumulus.APRS.SendSolar)
					{
						message.Append(APRSsolarradStr(Convert.ToInt32(station.SolarRad)));
					}

					// station type e<string>
					message.Append($"eCumulus{cumulus.APRSstationtype[cumulus.StationType]}");

					cumulus.LogDebugMessage($"Sending: {message}");

					//data = Encoding.ASCII.GetBytes(message.ToString());

					await writer.WriteLineAsync(message.ToString());
					writer.Flush();

					await Task.Delay(3000);
					writer.Close();
				}
				cumulus.LogDebugMessage("End of CWOP update");
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "CWOP error");
			}

		}

		internal override string GetURL(out string pwstring, DateTime timestamp)
		{
			pwstring = null;
			return null;
		}


		private void TimerTick(object sender, ElapsedEventArgs e)
		{
			if (!string.IsNullOrEmpty(ID))
			{
				_ = DoUpdate(DateTime.Now);
			}
		}

		/// <summary>
		/// Takes latitude in degrees and converts it to APRS format ddmm.hhX:
		/// (hh = hundredths of a minute)
		/// e.g. 5914.55N
		/// </summary>
		/// <returns></returns>
		private static string APRSLat(Cumulus cumulus)
		{
			string dir;
			decimal lat;
			int d, m, s;
			if (cumulus.Latitude < 0)
			{
				lat = -cumulus.Latitude;
				dir = "S";
			}
			else
			{
				lat = cumulus.Latitude;
				dir = "N";
			}

			Trig.DegToDMS(lat, out d, out m, out s);
			int hh = (int) Math.Round(s * 100 / 60.0);

			return String.Format("{0:D2}{1:D2}.{2:D2}{3}", d, m, hh, dir);
		}

		/// <summary>
		/// Takes longitude in degrees and converts it to APRS format dddmm.hhX:
		/// (hh = hundredths of a minute)
		/// e.g. 15914.55W
		/// </summary>
		/// <returns></returns>
		private static string APRSLon(Cumulus cumulus)
		{
			string dir;
			decimal lon;
			int d, m, s;
			if (cumulus.Longitude < 0)
			{
				lon = -cumulus.Longitude;
				dir = "W";
			}
			else
			{
				lon = cumulus.Longitude;
				dir = "E";
			}

			Trig.DegToDMS(lon, out d, out m, out s);
			int hh = (int) Math.Round(s * 100 / 60.0);

			return String.Format("{0:D3}{1:D2}.{2:D2}{3}", d, m, hh, dir);
		}

		/// <summary>
		/// input is in Units.Wind units, convert to mph for APRS
		/// and return 3 digits
		/// </summary>
		/// <param name="wind"></param>
		/// <returns></returns>
		private static string APRSwind(double wind)
		{
			var windMPH = Convert.ToInt32(ConvertUnits.UserWindToMPH(wind));
			return windMPH.ToString("D3");
		}

		/// <summary>
		/// input is in Units.Press units, convert to tenths of mb for APRS
		/// return 5 digit string
		/// </summary>
		/// <param name="press"></param>
		/// <returns></returns>
		private static string APRSpress(double press)
		{
			var press10mb = Convert.ToInt32(ConvertUnits.UserPressToMB(press) * 10);
			return press10mb.ToString("D5");
		}

		/// <summary>
		/// return humidity as 2-digit string
		/// represent 100 by 00
		/// send 1 instead of zero
		/// </summary>
		/// <param name="hum"></param>
		/// <returns></returns>
		private static string APRShum(int hum)
		{
			if (hum == 100)
			{
				return "00";
			}

			if (hum == 0)
			{
				return "01";
			}

			return hum.ToString("D2");
		}

		/// <summary>
		/// input is in RainUnit units, convert to hundredths of inches for APRS
		/// and return 3 digits
		/// </summary>
		/// <param name="rain"></param>
		/// <returns></returns>
		private static string APRSrain(double rain)
		{
			var rain100IN = Convert.ToInt32(ConvertUnits.UserRainToIN(rain) * 100);
			return rain100IN.ToString("D3");
		}

		internal static string APRSsolarradStr(double solarRad)
		{
			if (solarRad < 1000)
			{
				return 'L' + (Convert.ToInt32(solarRad)).ToString("D3");
			}
			else
			{
				return 'l' + (Convert.ToInt32(solarRad - 1000)).ToString("D3");
			}
		}

		internal static string APRStemp(double temp, int units)
		{
			// input is in TempUnit units, convert to F for APRS
			// and return three digits
			int num;

			if (units == 0)
			{
				num = Convert.ToInt32(((temp * 1.8) + 32));
			}
			else
			{
				num = Convert.ToInt32(temp);
			}

			if (num < 0)
			{
				num = -num;
				return '-' + num.ToString("00");
			}
			else
			{
				return num.ToString("000");
			}
		}
	}
}
