using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void DoRain(double total, double rate, DateTime timestamp)
		{
			var readingTS = cumulus.MeteoDate(timestamp);

			if (CurrentDate != readingTS.Date)
			{
				// A reading has apparently arrived at the start of a new day, but before we have done the roll-over
				// Ignore it, as otherwise it may cause a new monthly record to be logged using last month's total
				// Problem: NoSensorCheck means we continue processing even when no data is coming in. So all we can do is ignore the check in this case
				cumulus.LogDebugMessage("DoRain: A reading arrived at the start of a new day, but before we have done the roll-over. Ignoring it");
				return;
			}

			// Spike removal
			if (rate > cumulus.Spike.MaxRainRate)
			{
				cumulus.LogSpikeRemoval("Rain rate greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Rate value = {rate.ToString(cumulus.RainFormat)} SpikeMaxRainRate = {cumulus.Spike.MaxRainRate.ToString(cumulus.RainFormat)}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Rain rate greater than spike value - value = {rate.ToString(cumulus.RainFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			var previoustotal = MetData.RainCounter;

			MetData.RainCounter = total;

			if (initialiseRainDayStart || initialiseMidnightRain)
			{

				if (initialiseRainDayStart)
				{
					MetData.RainCounterDayStart = MetData.RainCounter;
					cumulus.LogMessage(" First rain data, raindaystart = " + MetData.RainCounterDayStart);
					initialiseRainDayStart = false;
				}

				if (initialiseMidnightRain)
				{
					MetData.MidnightRainCount = MetData.RainCounter;
					initialiseMidnightRain = false;
				}

				WriteTodayFile(timestamp, false);
				HaveReadData = true;
				return;
			}

			// Has the rain total in the Stations been reset?
			// raindaystart greater than current total, allow for rounding
			// or current has jumped by more than 40 mm/1.5 inch
			var maxIncrement = cumulus.Units.Rain == 0 ? 40 : 1.5;
			var counterReset = Math.Round(MetData.RainCounterDayStart, cumulus.RainDPlaces) - Math.Round(MetData.RainCounter, cumulus.RainDPlaces) > 0;
			var counterJumped = Math.Round(MetData.RainCounter, cumulus.RainDPlaces) - previoustotal > maxIncrement;

			// Davis VP2 console loses todays rainfall when it is power cycled
			// so check if the current value is less than previous and has returned to the previous midnight value
			if (Math.Round(MetData.RainCounter, cumulus.RainDPlaces) < Math.Round(previoustotal, cumulus.RainDPlaces) &&
				Math.Abs(MetData.RainCounter - MetData.MidnightRainCount) < Math.Pow(10, -cumulus.RainDPlaces) &&
				cumulus.StationType == StationTypes.VantagePro2)
			{
				var counterLost = previoustotal - MetData.MidnightRainCount;
				MetData.RainCounterDayStart -= counterLost;
				MetData.MidnightRainCount -= counterLost;

				cumulus.LogWarningMessage($" ****Rain counter reset to previous midnight value (VP2 console power cycled?), lost {counterLost} counts");
				cumulus.LogWarningMessage($"     New values:  RaindayStart = {MetData.RainCounterDayStart}, MidnightRainCount = {MetData.MidnightRainCount}, Raincounter = {MetData.RainCounter}");

				// update any data in the recent data db
				//var counterChange = RainCounter - prevraincounter
				RecentDataDb.Execute("update RecentData set raincounter=raincounter-?", counterLost);

			}
			else if (counterReset || counterJumped)
			{
				if (SecondChanceRainReset)
				// second consecutive reading with reset value
				{
					if (counterReset)
					{
						cumulus.LogWarningMessage(" ****Rain counter reset confirmed: RaindayStart = " + MetData.RainCounterDayStart + ", Raincounter = " + MetData.RainCounter);
					}
					else
					{
						cumulus.LogWarningMessage(" ****Rain counter jump confirmed: Previous Value = " + previoustotal + ", Raincounter = " + MetData.RainCounter);
					}

					// set the start of day figure so it reflects the rain
					// so far today
					MetData.RainCounterDayStart = MetData.RainCounter - (previoustotal - MetData.RainCounterDayStart);
					cumulus.LogMessage("Setting RaindayStart to " + MetData.RainCounterDayStart);

					MetData.MidnightRainCount = MetData.RainCounter;
					previoustotal = total;

					// update any data in the recent data db
					var counterChange = MetData.RainCounter - prevraincounter;
					RecentDataDb.Execute("update RecentData set raincounter=raincounter+?", counterChange);

					SecondChanceRainReset = false;
					rainResetCount = 0;
				}
				else
				{
					if (counterReset)
					{
						cumulus.LogMessage(" ****Rain reset? RaindayStart = " + MetData.RainCounterDayStart + ", Raincounter = " + MetData.RainCounter);
					}
					else
					{
						cumulus.LogWarningMessage(" ****Rain counter jump? Previous Value = " + previoustotal + ", Raincounter = " + MetData.RainCounter);
					}

					// reset the counter to ignore this reading
					MetData.RainCounter = previoustotal;
					cumulus.LogMessage("Leaving counter at " + MetData.RainCounter);

					// stash the previous rain counter
					prevraincounter = MetData.RainCounter;

					rainResetCount++;

					if (rainResetCount >= 2)
					{
						SecondChanceRainReset = true;
					}
				}
			}
			else
			{
				SecondChanceRainReset = false;
				rainResetCount = 0;
			}

			if (rate > -1)
			// Do rain rate
			{
				// scale rainfall rate
				MetData.RainRate = rate * cumulus.Calib.Rain.Mult;

				if (cumulus.StationOptions.UseRainForIsRaining == 1 && !cumulus.EcowittIsRainingUsePiezo)
				{
					MetData.IsRaining = MetData.RainRate > 0;
					cumulus.IsRainingAlarm.Triggered = MetData.IsRaining;
				}

				if (MetData.RainRate > Records.AllTime.HighRainRate.Val)
					SetAlltime(Records.AllTime.HighRainRate, MetData.RainRate, timestamp);

				CheckMonthlyAlltime("HighRainRate", MetData.RainRate, true, timestamp);

				cumulus.HighRainRateAlarm.CheckAlarm(MetData.RainRate);

				if (MetData.RainRate > DailyHighLow.Today.HighRainRate)
				{
					DailyHighLow.Today.HighRainRate = MetData.RainRate;
					DailyHighLow.Today.HighRainRateTime = timestamp;
					WriteTodayFile(timestamp, false);
				}

				if (MetData.RainRate > Records.ThisMonth.HighRainRate.Val)
				{
					Records.ThisMonth.HighRainRate.Val = MetData.RainRate;
					Records.ThisMonth.HighRainRate.Ts = timestamp;
					WriteMonthIniFile();
				}

				if (MetData.RainRate > Records.ThisYear.HighRainRate.Val)
				{
					Records.ThisYear.HighRainRate.Val = MetData.RainRate;
					Records.ThisYear.HighRainRate.Ts = timestamp;
					WriteYearIniFile();
				}
			}

			if (rainResetCount == 0)
			{
				// Has a tip occurred?
				if (Math.Round(total, cumulus.RainDPlaces) - Math.Round(previoustotal, cumulus.RainDPlaces) > 0)
				{
					// rain has occurred
					MetData.LastRainTip = timestamp.ToString("yyyy-MM-dd HH:mm");

					if (cumulus.StationOptions.UseRainForIsRaining == 1 && !cumulus.EcowittIsRainingUsePiezo)
					{
						MetData.IsRaining = true;
						cumulus.IsRainingAlarm.Triggered = true;
					}
				}
				else if (cumulus.StationOptions.UseRainForIsRaining == 1 && !cumulus.EcowittIsRainingUsePiezo && MetData.RainRate <= 0)
				{
					MetData.IsRaining = false;
					cumulus.IsRainingAlarm.Triggered = false;
				}

				// Calculate today's rainfall
				MetData.RainToday = (MetData.RainCounter - MetData.RainCounterDayStart) * cumulus.Calib.Rain.Mult;
				// Allow for rounding errors
				if (MetData.RainToday < 0) MetData.RainToday = 0;

				// Calculate rain since midnight for Wunderground etc
				var trendval = MetData.RainCounter - MetData.MidnightRainCount;

				// Round value as some values may have been read from log file and already rounded
				trendval = Math.Round(trendval, cumulus.RainDPlaces);

				if (trendval < 0)
				{
					MetData.RainSinceMidnight = 0;
				}
				else
				{
					MetData.RainSinceMidnight = trendval * cumulus.Calib.Rain.Mult;
				}

				// rain this week so far
				MetData.RainWeek = RainThisWeek + MetData.RainToday;

				// rain this month so far
				MetData.RainMonth = RainThisMonth + MetData.RainToday;

				// get correct date for rain records
				var offsetdate = cumulus.MeteoDate(timestamp);

				// rain this year so far
				MetData.RainYear = RainThisYear + MetData.RainToday;

				if (MetData.RainToday > Records.AllTime.DailyRain.Val)
					SetAlltime(Records.AllTime.DailyRain, MetData.RainToday, offsetdate);

				CheckMonthlyAlltime("DailyRain", MetData.RainToday, true, timestamp);

				if (MetData.RainToday > Records.ThisMonth.DailyRain.Val)
				{
					Records.ThisMonth.DailyRain.Val = MetData.RainToday;
					Records.ThisMonth.DailyRain.Ts = offsetdate;
					WriteMonthIniFile();
				}

				if (MetData.RainToday > Records.ThisYear.DailyRain.Val)
				{
					Records.ThisYear.DailyRain.Val = MetData.RainToday;
					Records.ThisYear.DailyRain.Ts = offsetdate;
					WriteYearIniFile();
				}

				if (MetData.RainMonth > Records.ThisYear.MonthlyRain.Val)
				{
					Records.ThisYear.MonthlyRain.Val = MetData.RainMonth;
					Records.ThisYear.MonthlyRain.Ts = offsetdate;
					WriteYearIniFile();
				}

				if (MetData.RainMonth > Records.AllTime.MonthlyRain.Val)
					SetAlltime(Records.AllTime.MonthlyRain, MetData.RainMonth, offsetdate);

				CheckMonthlyAlltime("MonthlyRain", MetData.RainMonth, true, timestamp);

				cumulus.HighRainTodayAlarm.CheckAlarm(MetData.RainToday);
			}
			HaveReadData = true;
		}


	}
}
