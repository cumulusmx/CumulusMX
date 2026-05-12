using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		internal string GetDiaryData(string date)
		{
			var json = new StringBuilder("{\"Time\":\"", 1024);

			var result = cumulus.DiaryDB.Query<DiaryData>("select * from DiaryData where date(Date) = ? order by Date limit 1", date);

			if (result.Count > 0)
			{
				json.Append($"{result[0].Time.ToString(@"hh\:mm")}:00\",\"Entry\":");
				if (string.IsNullOrEmpty(result[0].Entry))
				{
					json.Append("null");
				}
				else
				{
					json.Append($"\"{result[0].Entry}\"");
				}
				json.Append(",\"Snow24h\":");
				if (result[0].Snow24h.HasValue)
				{
					json.Append(result[0].Snow24h.Value.ToString("F2", CultureInfo.InvariantCulture));
				}
				else
				{
					json.Append("null");
				}
				json.Append(",\"SnowDepth\":");
				if (result[0].SnowDepth.HasValue)
				{
					json.Append(result[0].SnowDepth.Value.ToString("F2", CultureInfo.InvariantCulture));
				}
				else
				{
					json.Append("null");
				}

				// binary fields
				json.Append(",\"Thunder\":" + result[0].Thunder.ToString().ToLower());
				json.Append(",\"Hail\":" + result[0].Hail.ToString().ToLower());
				json.Append(",\"Fog\":" + result[0].Fog.ToString().ToLower());
				json.Append(",\"Gales\":" + result[0].Gales.ToString().ToLower());

				json.Append('}');
			}
			else
			{
				json.Append($"\",\"Time\":\"{cumulus.SnowDepthHour:D2}:00:00\",\"Snow24h\":\"\",\"SnowDepth\":\"\",\"Thunder\":false,\"Hail\":false,\"Fog\":false,\"Gales\":false}}");
			}

			return json.ToString();
		}

		// Fetches all days that have a diary entry
		internal string GetDiarySummary()
		{
			var json = new StringBuilder(1024);

			var result = cumulus.DiaryDB.Query<DiaryData>("select Date from DiaryData order by Date");

			if (result.Count > 0)
			{
				json.Append("{\"dates\":[");
				for (var i = 0; i < result.Count; i++)
				{
					json.Append('"');
					json.Append(result[i].Date.ToString("yyyy-MM-dd"));
					json.Append("\",");
				}
				json.Length--;
				json.Append("]}");
			}
			else
			{
				json.Append("{\"dates\":[]}");
			}

			return json.ToString();
		}

		internal string GetDiaryExport()
		{
			var txt = new StringBuilder(10240);
			var result = cumulus.DiaryDB.Query<DiaryData>("select * from DiaryData order by Date");

			txt.AppendLine("Date,Time,Snow Depth,Snow 24h,Entry,Thunder,Hail,Fog,Gales");

			if (result.Count > 0)
			{
				for (var i = 0; i < result.Count; i++)
				{
					txt.AppendLine(result[i].ToCsvString());
				}
			}

			return txt.ToString();
		}
	}
}
