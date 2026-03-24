using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CumulusMX.LogFiles
{
	internal class AirLinkLogFileRec
	{
		public string DateTimeStr { get; set; }

		public long UnixTimestamp;
		public DateTime DateTime
		{
			get
			{
				return UnixTimestamp.LocalFromUnixTime();
			}
			set
			{
				UnixTimestamp = value.ToUnixTime();
				DateTimeStr = value.ToString("dd/MM/yy HH:mm", CultureInfo.InvariantCulture);
			}
		}
		public double? IndoorTemperature { get; set; }
		public int? IndoorHumidity { get; set; }
		public double? IndoorPm1 { get; set; }
		public double? IndoorPm2p5 { get; set; }
		public double? IndoorPm2p5_1hr { get; set; }
		public double? IndoorPm2p5_3hr { get; set; }
		public double? IndoorPm2p5_24hr { get; set; }
		public double? IndoorPm2p5_Nowcast { get; set; }
		public double? IndoorPm10 { get; set; }
		public double? IndoorPm10_1hr { get; set; }
		public double? IndoorPm10_3hr { get; set; }
		public double? IndoorPm10_24hr { get; set; }
		public double? IndoorPm10_Nowcast { get; set; }
		public int? IndoorPercent1hr { get; set; }
		public int? IndoorPercent3hr { get; set; }
		public int? IndoorPercent24hr { get; set; }
		public int? IndoorPercentNowcast { get; set; }
		public double? IndoorPm2p5Aqi { get; set; }
		public double? IndoorPm2p5Aqi_1hr { get; set; }
		public double? IndoorPm2p5Aqi_3hr { get; set; }
		public double? IndoorPm2p5Aqi_24hr { get; set; }
		public double? IndoorPm2p5Aqi_Nowcast { get; set; }
		public double? IndoorPm10Aqi { get; set; }
		public double? IndoorPm10Aqi_1hr { get; set; }
		public double? IndoorPm10Aqi_3hr { get; set; }
		public double? IndoorPm10Aqi_24hr { get; set; }
		public double? IndoorPm10Aqi_Nowcast { get; set; }
		public double? OutdoorTemperature { get; set; }
		public int? OutdoorHumidity { get; set; }
		public double? OutdoorPm1 { get; set; }
		public double? OutdoorPm2p5 { get; set; }
		public double? OutdoorPm2p5_1hr { get; set; }
		public double? OutdoorPm2p5_3hr { get; set; }
		public double? OutdoorPm2p5_24hr { get; set; }
		public double? OutdoorPm2p5_Nowcast { get; set; }
		public double? OutdoorPm10 { get; set; }
		public double? OutdoorPm10_1hr { get; set; }
		public double? OutdoorPm10_3hr { get; set; }
		public double? OutdoorPm10_24hr { get; set; }
		public double? OutdoorPm10_Nowcast { get; set; }
		public int? OutdoorPercent1hr { get; set; }
		public int? OutdoorPercent3hr { get; set; }
		public int? OutdoorPercent24hr { get; set; }
		public int? OutdoorPercentNowcast { get; set; }
		public double? OutdoorPm2p5Aqi { get; set; }
		public double? OutdoorPm2p5Aqi_1hr { get; set; }
		public double? OutdoorPm2p5Aqi_3hr { get; set; }
		public double? OutdoorPm2p5Aqi_24hr { get; set; }
		public double? OutdoorPm2p5Aqi_Nowcast { get; set; }
		public double? OutdoorPm10Aqi { get; set; }
		public double? OutdoorPm10Aqi_1hr { get; set; }
		public double? OutdoorPm10Aqi_3hr { get; set; }
		public double? OutdoorPm10Aqi_24hr { get; set; }
		public double? OutdoorPm10Aqi_Nowcast { get; set; }

		public AirLinkLogFileRec()
		{
		}

		public AirLinkLogFileRec(string csv)
		{
			ParseCsvRec(csv);
		}


		// errors are caught by the caller
		public void ParseCsvRec(string data)
		{
			// 0  Date/Time in the form dd/mm/yy hh:mm
			// 1  Unix Timestamp
			// 2  Indoor Temperature
			// 3  Indoor Humidity
			// 4  Indoor PM 1
			// 5  Indoor PM 2.5
			// 6  Indoor PM 2.5 1-hour
			// 7  Indoor PM 2.5 3-hour
			// 8  Indoor PM 2.5 24-hour
			// 9  Indoor PM 2.5 nowcast
			// 10 Indoor PM 10
			// 11 Indoor PM 10 1-hour
			// 12 Indoor PM 10 3-hour
			// 13 Indoor PM 10 24-hour
			// 14 Indoor PM 10 nowcast
			// 15 Indoor Percent received 1-hour
			// 16 Indoor Percent received 3-hour
			// 17 Indoor Percent received 24-hour
			// 18 Indoor Percent received nowcast
			// 19 Indoor AQI PM2.5
			// 20 Indoor AQI PM2.5 1-hour
			// 21 Indoor AQI PM2.5 3-hour
			// 22 Indoor AQI PM2.5 24-hour
			// 23 Indoor AQI PM2.5 nowcast
			// 24 Indoor AQI PM10
			// 25 Indoor AQI PM10 1-hour
			// 26 Indoor AQI PM10 3-hour
			// 27 Indoor AQI PM10 24-hour
			// 28 Indoor AQI PM10 nowcast
			// 29 Outdoor Temperature
			// 30 Outdoor Humidity
			// 31 Outdoor PM 1
			// 32 Outdoor PM 2.5
			// 33 Outdoor PM 2.5 1-hour
			// 34 Outdoor PM 2.5 3-hour
			// 35 Outdoor PM 2.5 24-hour
			// 36 Outdoor PM 2.5 nowcast
			// 37 Outdoor PM 10
			// 38 Outdoor PM 10 1-hour
			// 39 Outdoor PM 10 3-hour
			// 40 Outdoor PM 10 24-hour
			// 41 Outdoor PM 10 nowcast
			// 42 Outdoor Percent received 1-hour
			// 43 Outdoor Percent received 3-hour
			// 44 Outdoor Percent received 24-hour
			// 45 Outdoor Percent received nowcast
			// 46 Outdoor AQI PM2.5
			// 47 Outdoor AQI PM2.5 1-hour
			// 48 Outdoor AQI PM2.5 3-hour
			// 49 Outdoor AQI PM2.5 24-hour
			// 50 Outdoor AQI PM2.5 nowcast
			// 51 Outdoor AQI PM10
			// 52 Outdoor AQI PM10 1-hour
			// 53 Outdoor AQI PM10 3-hour
			// 54 Outdoor AQI PM10 24-hour
			// 55 Outdoor AQI PM10 nowcast


			var inv = CultureInfo.InvariantCulture;
			var st = new List<string>(data.Split(','));
			double resultDbl;
			int resultInt;

			try
			{
				DateTimeStr = st[0];
				UnixTimestamp = Convert.ToInt64(st[1]);
				OutdoorTemperature = Convert.ToDouble(st[2], inv);
				OutdoorHumidity = Convert.ToInt32(Convert.ToDouble(st[3], inv));

				IndoorTemperature = double.TryParse(st[12], inv, out resultDbl) ? resultDbl : null;
				IndoorHumidity = int.TryParse(st[13], out resultInt) ? resultInt : null;
			}
			catch (Exception ex)
			{
				Program.cumulus.LogExceptionMessage(ex, $"AirLinkFileParseCsvRec: Error");
				throw;
			}
		}

		public string ToCsv(Cumulus cumulus)
		{
			var inv = CultureInfo.InvariantCulture;
			return string.Join(',',
				DateTimeStr,
				UnixTimestamp.ToString(inv),
				IndoorTemperature.ToFixed("F1"),
				IndoorHumidity.HasValue ? IndoorHumidity.Value : "",
				IndoorPm1.ToFixed("F1"),
				IndoorPm2p5.ToFixed("F1"),
				IndoorPm2p5_1hr.ToFixed("F1"),
				IndoorPm2p5_3hr.ToFixed("F1"),
				IndoorPm2p5_24hr.ToFixed("F1"),
				IndoorPm2p5_Nowcast.ToFixed("F1"),
				IndoorPm2p5.ToFixed("F1"),
				IndoorPm10_1hr.ToFixed("F1"),
				IndoorPm10_3hr.ToFixed("F1"),
				IndoorPm10_24hr.ToFixed("F1"),
				IndoorPm10_Nowcast.ToFixed("F1"),
				IndoorPercent1hr.HasValue ? IndoorPercent1hr.Value : "",
				IndoorPercent3hr.HasValue ? IndoorPercent3hr.Value : "",
				IndoorPercent24hr.HasValue ? IndoorPercent24hr.Value : "",
				IndoorPercentNowcast.HasValue ? IndoorPercent1hr.Value : "",
				IndoorPm2p5Aqi.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm2p5Aqi.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm2p5Aqi.Value) : "",
				IndoorPm2p5Aqi_1hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm2p5Aqi_1hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm2p5Aqi_1hr.Value) : "",
				IndoorPm2p5Aqi_3hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm2p5Aqi_3hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm2p5Aqi_1hr.Value) : "",
				IndoorPm2p5Aqi_24hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm2p5Aqi_24hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm2p5Aqi_1hr.Value) : "",
				IndoorPm2p5Aqi_Nowcast.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm2p5Aqi_Nowcast.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm2p5Aqi_Nowcast.Value) : "",
				IndoorPm10Aqi.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm10Aqi.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm10Aqi.Value) : "",
				IndoorPm10Aqi_1hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm10Aqi_1hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm10Aqi_1hr.Value) : "",
				IndoorPm10Aqi_3hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm10Aqi_3hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm10Aqi_1hr.Value) : "",
				IndoorPm10Aqi_24hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm10Aqi_24hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm10Aqi_1hr.Value) : "",
				IndoorPm10Aqi_Nowcast.HasValue ? (cumulus.AirQualityDPlaces > 0 ? IndoorPm10Aqi_Nowcast.Value.ToString(cumulus.AirQualityFormat, inv) : (int) IndoorPm10Aqi_Nowcast.Value) : "",
				OutdoorTemperature.HasValue ? OutdoorTemperature.Value.ToString("F1", inv) : "",
				OutdoorHumidity.HasValue ? OutdoorHumidity.Value : "",
				OutdoorPm1.ToFixed("F1"),
				OutdoorPm2p5.ToFixed("F1"),
				OutdoorPm2p5_1hr.ToFixed("F1"),
				OutdoorPm2p5_3hr.ToFixed("F1"),
				OutdoorPm2p5_24hr.ToFixed("F1"),
				OutdoorPm2p5_Nowcast.ToFixed("F1"),
				OutdoorPm2p5.ToFixed("F1"),
				OutdoorPm10_1hr.ToFixed("F1"),
				OutdoorPm10_3hr.ToFixed("F1"),
				OutdoorPm10_24hr.ToFixed("F1"),
				OutdoorPm10_Nowcast.ToFixed("F1"),
				OutdoorPercent1hr.HasValue ? OutdoorPercent1hr.Value : "",
				OutdoorPercent3hr.HasValue ? OutdoorPercent3hr.Value : "",
				OutdoorPercent24hr.HasValue ? OutdoorPercent24hr.Value : "",
				OutdoorPercentNowcast.HasValue ? OutdoorPercent1hr.Value : "",
				OutdoorPm2p5Aqi.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm2p5Aqi.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm2p5Aqi.Value) : "",
				OutdoorPm2p5Aqi_1hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm2p5Aqi_1hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm2p5Aqi_1hr.Value) : "",
				OutdoorPm2p5Aqi_3hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm2p5Aqi_3hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm2p5Aqi_1hr.Value) : "",
				OutdoorPm2p5Aqi_24hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm2p5Aqi_24hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm2p5Aqi_1hr.Value) : "",
				OutdoorPm2p5Aqi_Nowcast.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm2p5Aqi_Nowcast.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm2p5Aqi_Nowcast.Value) : "",
				OutdoorPm10Aqi.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm10Aqi.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm10Aqi.Value) : "",
				OutdoorPm10Aqi_1hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm10Aqi_1hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm10Aqi_1hr.Value) : "",
				OutdoorPm10Aqi_3hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm10Aqi_3hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm10Aqi_1hr.Value) : "",
				OutdoorPm10Aqi_24hr.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm10Aqi_24hr.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm10Aqi_1hr.Value) : "",
				OutdoorPm10Aqi_Nowcast.HasValue ? (cumulus.AirQualityDPlaces > 0 ? OutdoorPm10Aqi_Nowcast.Value.ToString(cumulus.AirQualityFormat, inv) : (int) OutdoorPm10Aqi_Nowcast.Value) : ""
			);
		}

		public static string CurrentToCsv(DateTime timestamp, Cumulus cumulus)
		{
			var inv = CultureInfo.InvariantCulture;
			var sep = ",";

			var sb = new StringBuilder(256);

			sb.Append(timestamp.ToString("dd/MM/yy HH:mm", inv));
			sb.Append(sep + timestamp.ToUnixTime());

			if (cumulus.AirLinkInEnabled && cumulus.airLinkDataIn != null && cumulus.airLinkDataIn.dataValid)
			{
				sb.Append(sep + cumulus.airLinkDataIn.temperature.ToFixed("F1"));
				sb.Append(sep + cumulus.airLinkDataIn.humidity.ToString());
				sb.Append(sep + cumulus.airLinkDataIn.pm1.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm2p5.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm2p5_1hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm2p5_3hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm2p5_24hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm2p5_nowcast.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm10.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm10_1hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm10_3hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm10_24hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pm10_nowcast.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataIn.pct_1hr.ToString());
				sb.Append(sep + cumulus.airLinkDataIn.pct_3hr.ToString());
				sb.Append(sep + cumulus.airLinkDataIn.pct_24hr.ToString());
				sb.Append(sep + cumulus.airLinkDataIn.pct_nowcast.ToString());
				if (cumulus.AirQualityDPlaces > 0)
				{
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm2p5.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm2p5_1hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm2p5_3hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm2p5_24hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm2p5_nowcast.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm10.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm10_1hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm10_3hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm10_24hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataIn.aqiPm10_nowcast.ToString(cumulus.AirQualityFormat, inv));
			}
				else // Zero decimals - truncate value rather than round
				{
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm2p5);
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm2p5_1hr);
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm2p5_3hr);
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm2p5_24hr);
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm2p5_nowcast);
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm10);
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm10_1hr);
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm10_3hr);
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm10_24hr);
					sb.Append(sep + (int) cumulus.airLinkDataIn.aqiPm10_nowcast);
				}
			}
			else
			{
				// write zero values
				sb.Append(new String(sep[0], 27));
			}

			if (cumulus.AirLinkOutEnabled && cumulus.airLinkDataOut != null && cumulus.airLinkDataOut.dataValid)
			{
				sb.Append(sep + cumulus.airLinkDataOut.temperature.ToFixed("F1"));
				sb.Append(sep + cumulus.airLinkDataOut.humidity.ToString());
				sb.Append(sep + cumulus.airLinkDataOut.pm1.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm2p5.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm2p5_1hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm2p5_3hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm2p5_24hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm2p5_nowcast.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm10.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm10_1hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm10_3hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm10_24hr.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pm10_nowcast.ToString("F1", inv));
				sb.Append(sep + cumulus.airLinkDataOut.pct_1hr.ToString());
				sb.Append(sep + cumulus.airLinkDataOut.pct_3hr.ToString());
				sb.Append(sep + cumulus.airLinkDataOut.pct_24hr.ToString());
				sb.Append(sep + cumulus.airLinkDataOut.pct_nowcast.ToString());
				if (cumulus.AirQualityDPlaces > 0)
				{
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm2p5.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm2p5_1hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm2p5_3hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm2p5_24hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm2p5_nowcast.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm10.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm10_1hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm10_3hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm10_24hr.ToString(cumulus.AirQualityFormat, inv));
					sb.Append(sep + cumulus.airLinkDataOut.aqiPm10_nowcast.ToString(cumulus.AirQualityFormat, inv));
				}
				else // Zero decimals - truncate value rather than round
				{
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm2p5);
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm2p5_1hr);
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm2p5_3hr);
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm2p5_24hr);
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm2p5_nowcast);
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm10);
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm10_1hr);
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm10_3hr);
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm10_24hr);
					sb.Append(sep + (int) cumulus.airLinkDataOut.aqiPm10_nowcast);
				}
			}
			else
			{
				// write null values
				sb.Append(new String(sep[0], 27));
			}
			sb.Append(Environment.NewLine);
			return sb.ToString();
		}
	}
}
