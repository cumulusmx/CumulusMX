using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Unosquare.Labs.EmbedIO;

namespace CumulusMX
{
    internal class DataEditor
    {
        private WeatherStation station;
        private Cumulus cumulus;

        internal DataEditor(Cumulus cumulus, WeatherStation station)
        {
            this.station = station;
            this.cumulus = cumulus;

        }

		//internal string EditRainToday(HttpListenerContext context)
		internal string EditRainToday(IHttpContext context)
		{
			var InvC = new CultureInfo("");
            var request = context.Request;
            string text;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                text = reader.ReadToEnd();
            }

            string[] kvPair = text.Split('=');
            string key = kvPair[0];
            string raintodaystring = kvPair[1];

            if (!String.IsNullOrEmpty(raintodaystring))
            {
                try
                {
                    double raintoday = Double.Parse(raintodaystring, CultureInfo.InvariantCulture);
                    cumulus.LogMessage("Before rain today edit, raintoday="+station.RainToday.ToString(cumulus.RainFormat) + " Raindaystart=" + station.raindaystart.ToString(cumulus.RainFormat));
                    station.RainToday = raintoday;
                    station.raindaystart = station.Raincounter - (station.RainToday / cumulus.RainMult);
                    cumulus.LogMessage("After rain today edit,  raintoday=" + station.RainToday.ToString(cumulus.RainFormat) + " Raindaystart=" + station.raindaystart.ToString(cumulus.RainFormat));
                }
                catch (Exception ex)
                {
                    cumulus.LogMessage("Edit rain today: " + ex.Message);
                }
            }

            var json = "{\"raintoday\":\"" + station.RainToday.ToString(cumulus.RainFormat, InvC) +
                "\",\"raincounter\":\"" + station.Raincounter.ToString(cumulus.RainFormat, InvC) +
                "\",\"startofdayrain\":\"" + station.raindaystart.ToString(cumulus.RainFormat, InvC) +
                "\",\"rainmult\":\"" + cumulus.RainMult.ToString("F3", InvC) + "\"}";

            return json;
        }

        internal string GetRainTodayEditData()
        {
            var InvC = new CultureInfo("");
            string step = (cumulus.RainDPlaces == 1 ? "0.1" : "0.01");
            var json = "{\"raintoday\":\"" + station.RainToday.ToString(cumulus.RainFormat, InvC) +
                "\",\"raincounter\":\"" + station.Raincounter.ToString(cumulus.RainFormat, InvC) +
                "\",\"startofdayrain\":\"" + station.raindaystart.ToString(cumulus.RainFormat, InvC) +
                "\",\"rainmult\":\"" + cumulus.RainMult.ToString("F3", InvC) +
                "\",\"step\":\"" + step + "\"}";

            return json;
        }

		internal string EditDiary(IHttpContext context)
		{
			try
			{
				var request = context.Request;
				string text;

				using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
				{
					text = reader.ReadToEnd();
				}

				var newData = JsonConvert.DeserializeObject<DiaryData>(text);

				// write new/updated entry to the database
				var result = cumulus.DiaryDB.InsertOrReplace(newData);

				return "{\"result\":\"" + ((result == 1) ? "Success" : "Failed") +"\"}";

			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Edit Diary: " + ex.Message);
				return "{\"result\":\"Failed\"}";
			}
		}

		internal string DeleteDiary(IHttpContext context)
		{
			try
			{
				var request = context.Request;
				string text;

				using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
				{
					text = reader.ReadToEnd();
				}

				var record = JsonConvert.DeserializeObject<DiaryData>(text);

				// Delete the corresponding entry from the database
				var result = cumulus.DiaryDB.Delete(record);

				return "{\"result\":\"" + ((result == 1) ? "Success" : "Failed") + "\"}";

			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Delete Diary: " + ex.Message);
				return "{\"result\":\"Failed\"}";
			}

		}
	}

    internal class JsonEditRainData
    {
        public double raintoday { get; set; }
        public double raincounter { get; set; }
        public double startofdayrain { get; set; }
        public double rainmult { get; set; }
    }
}
