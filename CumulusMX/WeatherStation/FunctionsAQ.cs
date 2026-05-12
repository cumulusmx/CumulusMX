using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void DoAirQuality(double? value, int index)
		{
			MetData.AirQuality[index] = value;
			MetData.AirQualityIdx[index] = GetAqi(AqMeasure.pm2p5, value);
		}

		public void DoAirQualityAvg(double? value, int index)
		{
			MetData.AirQualityAvg[index] = value;
			MetData.AirQualityAvgIdx[index] = GetAqi(AqMeasure.pm2p5h24, value);
		}

		public void DoAirQuality10(double? value, int index)
		{
			MetData.AirQuality10[index] = value;
			MetData.AirQuality10Idx[index] = GetAqi(AqMeasure.pm10, value);
		}

		public void DoAirQuality10Avg(double? value, int index)
		{
			MetData.AirQuality10Avg[index] = value;
			MetData.AirQuality10AvgIdx[index] = GetAqi(AqMeasure.pm10h24, value);
		}

		public void UpdateAirQualityDb()
		{
			if (null == (MetData.AirQuality[1] ?? MetData.AirQuality[2] ?? MetData.AirQuality[3] ?? MetData.AirQuality[4]))
			{
				// no data available
				return;
			}

			var rec = new RecentAqData
			{
				DateTime = DateTime.Now,
				Pm2p5_1 = MetData.AirQuality[1],
				Pm2p5_2 = MetData.AirQuality[2],
				Pm2p5_3 = MetData.AirQuality[3],
				Pm2p5_4 = MetData.AirQuality[4],
				Pm10_1 = MetData.AirQuality10[1],
				Pm10_2 = MetData.AirQuality10[2],
				Pm10_3 = MetData.AirQuality10[3],
				Pm10_4 = MetData.AirQuality10[4]
			};

			try
			{
				RecentDataDb.InsertOrReplace(rec);

				for (var i = 0; i < 4; i++)
				{
					if (!string.IsNullOrEmpty(cumulus.PurpleAirIpAddress[i]))
					{
						// Get the average from the database
						GetAqAvgFromDb(i + 1);
					}
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "UpdateAirQualityDb: Error inserting recent AQ data into database");
			}
		}

		public void GetAqAvgFromDb(int idx)
		{
			if (idx < 1 || idx > 4)
			{
				cumulus.LogErrorMessage("GetAqAvgFromDb: Index out of range, idx=" + idx);
				return;
			}

			if (string.IsNullOrEmpty(cumulus.PurpleAirIpAddress[idx - 1]))
				return;

			try
			{
				var ret = RecentDataDb.ExecuteScalar<double?>($"SELECT AVG(Pm2p5_{idx}) FROM RecentAqData WHERE Timestamp > unixepoch(DATETIME('NOW', '-24 HOURS'))");
				if (ret != null)
				{
					DoAirQualityAvg(ret, idx);
				}

				ret = RecentDataDb.ExecuteScalar<double?>($"SELECT AVG(Pm10_{idx}) FROM RecentAqData WHERE Timestamp > unixepoch(DATETIME('NOW', '-24 HOURS'))");
				if (ret != null)
				{
					DoAirQuality10Avg(ret, idx);
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "GetAqAvgFromDb: Error processing AQ average from database");
			}
		}



		public enum AqMeasure
		{
			pm2p5,
			pm2p5h24,
			pm10,
			pm10h24
		}

		public double? GetAqi(AqMeasure type, double? value)
		{
			if (!value.HasValue) return null;

			var val = value.Value;

			switch (cumulus.airQualityIndex)
			{
				case 0: // US EPA
					if (type == AqMeasure.pm2p5 || type == AqMeasure.pm2p5h24)
						return AirQualityIndices.US_EPApm2p5(val);
					else
						return AirQualityIndices.US_EPApm10(val);

				case 1: // UK COMEAP
					if (type == AqMeasure.pm2p5 || type == AqMeasure.pm2p5h24)
						return AirQualityIndices.UK_COMEAPpm2p5(val);
					else
						return AirQualityIndices.UK_COMEAPpm10(val);

				case 2: // EU AQI
					return type switch
					{
						AqMeasure.pm2p5 => AirQualityIndices.EU_AQIpm2p5h1(val),
						AqMeasure.pm2p5h24 => AirQualityIndices.EU_AQI2p5h24(val),
						AqMeasure.pm10 => AirQualityIndices.EU_AQI10h1(val),
						AqMeasure.pm10h24 => AirQualityIndices.EU_AQI10h24(val),
						_ => 0
					};

				case 3: // EU CAQI
					return type switch
					{
						AqMeasure.pm2p5 => AirQualityIndices.EU_CAQI2p5h1(val),
						AqMeasure.pm2p5h24 => AirQualityIndices.EU_CAQI2p5h24(val),
						AqMeasure.pm10 => AirQualityIndices.EU_CAQI10h1(val),
						AqMeasure.pm10h24 => AirQualityIndices.EU_CAQI10h24(val),
						_ => 0
					};

				case 4: // Canada AQHI
						// return AirQualityIndices.CA_AQHI(value)
					return -1;

				case 5: // Australia NEPM
					if (type == AqMeasure.pm2p5 || type == AqMeasure.pm2p5h24)
						return AirQualityIndices.AU_NEpm2p5(val);
					else
						return AirQualityIndices.AU_NEpm10(val);

				case 6: // Netherlands LKI
					if (type == AqMeasure.pm2p5 || type == AqMeasure.pm2p5h24)
						return AirQualityIndices.NL_LKIpm2p5(val);
					else
						return AirQualityIndices.NL_LKIpm10(val);

				case 7: // Belgium BelAQI
					if (type == AqMeasure.pm2p5 || type == AqMeasure.pm2p5h24)
						return AirQualityIndices.BE_BelAQIpm2p5(val);
					else
						return AirQualityIndices.BE_BelAQIpm10(val);

				default:
					cumulus.LogErrorMessage($"GetAqi: Invalid AQI formula value set [cumulus.airQualityIndex]");
					return -1;
			}

		}
	}
}
