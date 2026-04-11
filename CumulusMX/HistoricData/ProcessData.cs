using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX.HistoricData
{
	internal void ProcessData(List<DataRecord> history, WeatherStation station, Cumulus cumulus)
	{
		int rollHour = Math.Abs(cumulus.GetHourInc(station.LastDataReadTime));
		station.LastDataReadTime = cumulus.LastUpdateTime;
		var luhour = station.LastDataReadTime.Hour;
		var rolloverdone = luhour == rollHour;


		foreach (var rec in history)
		{
			if (rec.Timestamp < station.LastDataReadTime)
			{
				cumulus.LogMessage("ProcessData: Ignoring old archive data");
				continue;
			}

			cumulus.LogMessage("ProcessData: Processing archive record for " + rec.Timestamp);

			station.DataDateTime = rec.Timestamp;

			rollHour = Math.Abs(cumulus.GetHourInc(rec.Timestamp));

			if (rec.Timestamp.Hour != rollHour)
			{
				rolloverdone = false;
			}

		}
	}
}
