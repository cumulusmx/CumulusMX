using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		// Use -1 for the average if you want to feedback the current average for a calculated moving average
		public void DoWind(double gustpar, int bearingpar, double speedpar, DateTime timestamp)
		{
			cumulus.LogDebugMessage($"DoWind: latest={gustpar:F1}, speed={speedpar:F1} - Current: gust={MetData.RecentMaxGust:F1}, speed={MetData.WindAverage:F1}");
			// if we have a spike in wind speed or gust, ignore the reading
			// Spike removal in user units
			if (previousGust < 998 && Math.Abs(gustpar - previousGust) > cumulus.Spike.GustDiff)
			{
				cumulus.LogSpikeRemoval("Gust difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Gust: NewVal={gustpar.ToString(cumulus.WindFormat)} OldVal={previousGust.ToString(cumulus.WindFormat)} SpikeGustDiff={cumulus.Spike.GustDiff.ToString(cumulus.WindFormat)}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Gust difference greater than spike value - Gust: NewVal={gustpar.ToString(cumulus.WindFormat)} OldVal={previousGust.ToString(cumulus.WindFormat)} SpikeGustDiff={cumulus.Spike.GustDiff.ToString(cumulus.WindFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}
			else if (gustpar >= cumulus.Limit.WindHigh)
			{
				cumulus.LogSpikeRemoval("Gust greater than upper limit; reading ignored");
				cumulus.LogSpikeRemoval($"Gust: NewVal={gustpar.ToString(cumulus.WindFormat)} HighLimit={cumulus.Limit.WindHigh.ToString(cumulus.WindFormat)}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Gust difference greater than upper limit - Gust: NewVal={gustpar.ToString(cumulus.WindFormat)} HighLimit={cumulus.Limit.WindHigh.ToString(cumulus.WindFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			if (speedpar >= 0 && previousWind < 998 && Math.Abs(speedpar - previousWind) > cumulus.Spike.WindDiff)
			{
				cumulus.LogSpikeRemoval("Wind difference greater than specified; reading ignored");
				cumulus.LogSpikeRemoval($"Wind: NewVal={speedpar.ToString(cumulus.WindAvgFormat)} OldVal={previousWind.ToString(cumulus.WindAvgFormat)} SpikeWindDiff={cumulus.Spike.WindDiff.ToString(cumulus.WindAvgFormat)}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Wind difference greater than spike value -  Wind: NewVal={speedpar.ToString(cumulus.WindAvgFormat)} OldVal={previousWind.ToString(cumulus.WindAvgFormat)} SpikeWindDiff={cumulus.Spike.WindDiff.ToString(cumulus.WindAvgFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}
			else if (speedpar >= cumulus.Limit.WindHigh)
			{
				cumulus.LogSpikeRemoval("Wind greater than upper limit; reading ignored");
				cumulus.LogSpikeRemoval($"Wind: NewVal={speedpar.ToString(cumulus.WindAvgFormat)} HighLimit={cumulus.Limit.WindHigh.ToString(cumulus.WindAvgFormat)}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Wind greater than upper limit -  Wind: NewVal={speedpar.ToString(cumulus.WindAvgFormat)} HighLimit={cumulus.Limit.WindHigh.ToString(cumulus.WindAvgFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousGust = gustpar;
			if (speedpar >= 0)
			{
				previousWind = speedpar;
			}

			calibratedgust = cumulus.Calib.WindGust.Calibrate(gustpar);
			var uncalibratedspeed = speedpar < 0 ? WindAverageUncalibrated : speedpar;
			var calibratedspeed = cumulus.Calib.WindSpeed.Calibrate(uncalibratedspeed);

			// use bearing of zero when calm
			if (Math.Abs(gustpar) < 0.001 && cumulus.StationOptions.UseZeroBearing)
			{
				MetData.WindBearing = 0;
			}
			else
			{
				MetData.WindBearing = (bearingpar + (int) cumulus.Calib.WindDir.Offset) % 360;
				if (MetData.WindBearing < 0)
				{
					MetData.WindBearing = 360 + MetData.WindBearing;
				}

				if (MetData.WindBearing == 0)
				{
					MetData.WindBearing = 360;
				}
			}

			MetData.WindLatest = cumulus.StationOptions.UseSpeedForLatest ? calibratedspeed : calibratedgust;

			windspeeds[nextwindvalue] = calibratedgust;
			windbears[nextwindvalue] = MetData.WindBearing;
			nextwindvalue = (nextwindvalue + 1) % maxwindvalues;

			// Recalculate wind rose data
			for (var i = 0; i < cumulus.NumWindRosePoints; i++)
			{
				windcounts[i] = 0;
			}

			for (var i = 0; i < numwindvalues; i++)
			{
				var j = (windbears[i] * 100 + 1125) % 36000 / (int) Math.Floor(cumulus.WindRoseAngle * 100);
				windcounts[j] += windspeeds[i];
			}

			if (numwindvalues < maxwindvalues)
			{
				numwindvalues++;
			}

			CheckHighGust(calibratedgust, MetData.WindBearing, timestamp);

			// We store uncalibrated gust values, so if we need to calculate the average from them we do not need to uncalibrate
			AddNewWindSample(gustpar, uncalibratedspeed, timestamp);
			AddNewWindVector(MetData.WindBearing, timestamp);

#if DEBUGWIND
			cumulus.LogDebugMessage($"Wind calc using speed: {cumulus.StationOptions.UseSpeedForAvgCalc}");
#endif

			if (cumulus.StationOptions.CalcuateAverageWindSpeed)
			{
				var fromTime = timestamp - cumulus.AvgSpeedTime;

				var avg = GetWindAverageFromArray(fromTime);

				if (avg >= 0 && previousWind < 998 && Math.Abs(avg - previousWind) > cumulus.Spike.WindDiff)
				{
					cumulus.LogSpikeRemoval("Wind difference greater than specified; reading ignored");
					cumulus.LogSpikeRemoval($"Wind: NewVal={speedpar.ToString(cumulus.WindAvgFormat)} OldVal={previousWind.ToString(cumulus.WindAvgFormat)} SpikeWindDiff={cumulus.Spike.WindDiff.ToString(cumulus.WindAvgFormat)}");
					lastSpikeRemoval = timestamp;
					cumulus.SpikeAlarm.LastMessage = $"Wind difference greater than spike value -  Wind: NewVal={avg.ToString(cumulus.WindAvgFormat)} OldVal={previousWind.ToString(cumulus.WindAvgFormat)} SpikeWindDiff={cumulus.Spike.WindDiff.ToString(cumulus.WindAvgFormat)}";
					cumulus.SpikeAlarm.Triggered = true;
					return;
				}
				else if (avg >= cumulus.Limit.WindHigh)
				{
					cumulus.LogSpikeRemoval("Wind greater than upper limit; reading ignored");
					cumulus.LogSpikeRemoval($"Wind: NewVal={avg.ToString(cumulus.WindAvgFormat)} HighLimit={cumulus.Limit.WindHigh.ToString(cumulus.WindAvgFormat)}");
					lastSpikeRemoval = timestamp;
					cumulus.SpikeAlarm.LastMessage = $"Wind greater than upper limit -  Wind: NewVal={avg.ToString(cumulus.WindAvgFormat)} HighLimit={cumulus.Limit.WindHigh.ToString(cumulus.WindAvgFormat)}";
					cumulus.SpikeAlarm.Triggered = true;
					return;
				}

				WindAverageUncalibrated = avg;

				// we want any calibration to be applied from uncalibrated values
				MetData.WindAverage = cumulus.Calib.WindSpeed.Calibrate(avg);

				previousWind = avg;
			}
			else
			{
				MetData.WindAverage = calibratedspeed;
				WindAverageUncalibrated = uncalibratedspeed;
			}


			if (CalcRecentMaxGust)
			{
				// Find recent max gust
				var fromTime = timestamp - cumulus.PeakGustTime;
				var maxgust = GetWindGustFromArray(fromTime);
				// wind gust is stored uncalibrated, so we need to calibrate now
				MetData.RecentMaxGust = cumulus.Calib.WindGust.Calibrate(maxgust);
			}
			else
			{
				MetData.RecentMaxGust = calibratedgust;
			}

			cumulus.LogDebugMessage($"DoWind: New: gust={MetData.RecentMaxGust:F1}, speed={MetData.WindAverage:F1}, latest={MetData.WindLatest:F1}");

			CheckHighAvgSpeed(timestamp);

			// Now add up all the values within the required period
			double totalwindX = 0;
			double totalwindY = 0;
			var diffFrom = 0;
			var diffTo = 0;

			for (var i = 0; i < MaxWindRecent; i++)
			{
				if (timestamp.ToUniversalTime() - WindVec[i].Timestamp < cumulus.AvgBearingTime)
				{
					totalwindX += WindVec[i].X;
					totalwindY += WindVec[i].Y;

					if (WindVec[i].Bearing != 0)
					{
						// this reading was within the last N minutes
						var difference = Utils.GetShortestAngle(MetData.WindAvgBearing, WindVec[i].Bearing);
						if (difference > diffTo)
						{
							diffTo = difference;
							MetData.BearingRangeTo = WindVec[i].Bearing;
						}
						if (difference < diffFrom)
						{
							diffFrom = difference;
							MetData.BearingRangeFrom = WindVec[i].Bearing;
						}
					}
				}
			}
			if (Math.Abs(totalwindX) < 0.001 && Math.Abs(totalwindY) < 0.001)
			{
				MetData.WindAvgBearing = 0;
			}
			else
			{
				MetData.WindAvgBearing = (int) Math.Round(Trig.RadToDeg(Math.Atan(totalwindY / totalwindX)));

				if (totalwindX < 0)
				{
					MetData.WindAvgBearing = 270 - MetData.WindAvgBearing;
				}
				else
				{
					MetData.WindAvgBearing = 90 - MetData.WindAvgBearing;
				}

				if (MetData.WindAvgBearing == 0)
				{
					MetData.WindAvgBearing = 360;
				}
			}

			if (Math.Abs(MetData.WindAverage) < 0.01 && cumulus.StationOptions.UseZeroBearing)
			{
				MetData.WindAvgBearing = 0;
			}

			if (Math.Abs(MetData.WindAverage) < 0.01)
			{
				MetData.BearingRangeFrom = 0;
				MetData.BearingRangeFrom10 = 0;
				MetData.BearingRangeTo = 0;
				MetData.BearingRangeTo10 = 0;
			}
			else
			{
				// Calculate rounded up/down values
				MetData.BearingRangeFrom10 = (int) (Math.Floor(MetData.BearingRangeFrom / 10.0) * 10);
				MetData.BearingRangeTo10 = (int) (Math.Ceiling(MetData.BearingRangeTo / 10.0) * 10) % 360;
				if (cumulus.StationOptions.UseZeroBearing && MetData.BearingRangeFrom10 == 0)
				{
					MetData.BearingRangeFrom10 = 360;
				}
				if (cumulus.StationOptions.UseZeroBearing && MetData.BearingRangeTo10 == 0)
				{
					MetData.BearingRangeTo10 = 360;
				}
			}

			// clean up the cached data
			RemoveOldWindSamples(timestamp);

			WindReadyToPlot = true;
			HaveReadData = true;
		}

		public void AddNewWindVector(int dir, DateTime time)
		{
			lock (recentwindLock)
			{
				WindVec[nextwindvec].X = calibratedgust * Math.Sin(Trig.DegToRad(dir));
				WindVec[nextwindvec].Y = calibratedgust * Math.Cos(Trig.DegToRad(dir));
				// save timestamp of this reading
				WindVec[nextwindvec].Timestamp = time.ToUniversalTime();
				// save bearing
				WindVec[nextwindvec].Bearing = dir; // savedBearing
													// increment index for next reading
				nextwindvec = (nextwindvec + 1) % MaxWindRecent;
			}
		}

		public void AddNewWindSample(double gustUnCal, double speedUncal, DateTime time)
		{
			lock (recentwindLock)
			{
				WindRecent.AddLast(new CWindRecent()
				{
					Gust = gustUnCal,
					Speed = speedUncal,
					DateTime = time.ToUniversalTime()
				});

			}
		}

		private void RemoveOldWindSamples(DateTime time)
		{
			var cutoff = (time.ToUniversalTime() - TimeSpan.FromMinutes(60)).ToUnixTime();

			while (WindRecent.First != null && WindRecent.First.Value.Timestamp < cutoff)
			{
				WindRecent.RemoveFirst();
			}
		}

		public double GetWindAverageFromArray(DateTime fromTime)
		{
			var numvalues = 0;
			double totalwind = 0;
			double avg = 0;
			var cutoff = fromTime.ToUnixTime();

			lock (recentwindLock)
			{
				// Walk backwards until cutoff
				for (var node = WindRecent.Last; node != null; node = node.Previous)
				{
					if (node.Value.Timestamp < cutoff)
						break;
#if DEBUGWIND
//					cumulus.LogDebugMessage($"Wind Time:{node.Value.DateTime.ToLocalTime().ToLongTimeString()} Gust:{node.Value.Gust:F1} Speed:{node.Value.Speed:F1}");
#endif
					numvalues++;
					totalwind += cumulus.StationOptions.UseSpeedForAvgCalc ? node.Value.Speed : node.Value.Gust;
				}
			}

			// average the values, if we have enough samples
			if (numvalues > 10 || cumulus.StationOptions.UseSpeedForAvgCalc)
			{
				avg = totalwind / numvalues;
			}
			else
			{
				// take a log scale third to whole of the gust values
				var div = 3.0 + 7.0 * Math.Pow((Math.Log(numvalues) / Math.Log(10.0)), 1.3);
				avg = totalwind / div;
#if DEBUGWIND
				cumulus.LogDebugMessage($"Wind Samples:{numvalues} Total:{totalwind:F1} Divisor:{div:F2} Avg:{avg:F1}");
#endif
			}

			return avg;
		}

		public int GetWindDirAvgFromArray(DateTime fromTime)
		{
			// Now add up all the values within the required period
			double totalwindX = 0;
			double totalwindY = 0;
			double avgDir = 0;

			lock (recentwindLock)
			{

				for (var i = 0; i < MaxWindRecent; i++)
				{
					if (WindVec[i].Timestamp >= fromTime.ToUniversalTime())
					{
						totalwindX += WindVec[i].X;
						totalwindY += WindVec[i].Y;
					}
				}
				if (Math.Abs(totalwindX) < 0.001 && Math.Abs(totalwindY) < 0.001)
				{
					avgDir = 0;
				}
				else
				{
					avgDir = (int) Math.Round(Trig.RadToDeg(Math.Atan(totalwindY / totalwindX)));

					if (totalwindX < 0)
					{
						avgDir = 270 - avgDir;
					}
					else
					{
						avgDir = 90 - avgDir;
					}

					if (avgDir == 0)
					{
						avgDir = 360;
					}
				}

				if (Math.Abs(MetData.WindAverage) < 0.01)
				{
					avgDir = 0;
				}
			}

			return (int) avgDir;
		}

		public string GetWindCompassAvgFromArray(DateTime fromTime)
		{
			return cumulus.CompassPoint(GetWindDirAvgFromArray(fromTime));
		}

		public double GetWindGustFromArray(DateTime fromTime)
		{
			double maxgust = 0;
			var cutoff = fromTime.ToUnixTime();

			lock (recentwindLock)
			{
				for (var node = WindRecent.Last; node != null; node = node.Previous)
				{
					if (node.Value.Timestamp < cutoff)
						break;

					if (node.Value.Gust > maxgust)
						maxgust = node.Value.Gust;
				}
			}
			return maxgust;
		}

		// called at start-up to initialise the gust and average speeds from the recent data to avoid zero values
		public void InitialiseWind()
		{
			// first the average
			var fromTime = cumulus.LastUpdateTime.Subtract(cumulus.AvgSpeedTime).ToUnixTime();
			var numvalues = 0;
			var totalwind = 0.0;
			var gust = 0.0;

			lock (recentwindLock)
			{
				for (var node = WindRecent.Last; node != null; node = node.Previous)
				{
					if (node.Value.Timestamp < fromTime)
						break;

					numvalues++;
					totalwind += node.Value.Speed;
				}
			}
			// average the values, if we have enough samples
			WindAverageUncalibrated = totalwind / Math.Max(numvalues, 3);
			MetData.WindAverage = cumulus.Calib.WindSpeed.Calibrate(WindAverageUncalibrated);

			// now the gust
			fromTime = cumulus.LastUpdateTime.Subtract(cumulus.PeakGustTime).ToUnixTime();

			lock (recentwindLock)
			{
				for (var node = WindRecent.Last; node != null; node = node.Previous)
				{
					if (node.Value.Timestamp < fromTime)
						break;

					if (node.Value.Gust > gust)
					{
						gust = node.Value.Gust;
					}
				}
			}
			MetData.RecentMaxGust = cumulus.Calib.WindGust.Calibrate(gust);

			cumulus.LogDebugMessage($"InitialiseWind: gust={MetData.RecentMaxGust:F1}, speed={MetData.WindAverage:F1}");
		}

		public void AddValuesToRecentWind(double gust, double speed, int bearing, DateTime start, DateTime end)
		{
			var calGust = cumulus.Calib.WindGust.Calibrate(gust);
			int calBearing;

			// use bearing of zero when calm
			if (Math.Abs(gust) < 0.001 && cumulus.StationOptions.UseZeroBearing)
			{
				calBearing = 0;
			}
			else
			{
				calBearing = (bearing + (int) cumulus.Calib.WindDir.Offset) % 360;
				if (calBearing < 0)
				{
					calBearing = 360 + calBearing;
				}

				if (calBearing == 0)
				{
					calBearing = 360;
				}
			}

			lock (recentwindLock)
			{
				for (var ts = start; ts <= end; ts = ts.AddSeconds(3))
				{
					AddNewWindSample(gust, speed, ts);

					windspeeds[nextwindvalue] = calGust;
					windbears[nextwindvalue] = calBearing;
					nextwindvalue = (nextwindvalue + 1) % maxwindvalues;
				}
			}
		}

		private static int calcavgbear(double x, double y)
		{
			var avg = 90 - (int) Trig.RadToDeg(Math.Atan2(y, x));
			if (avg < 0)
			{
				avg = 360 + avg;
			}

			return avg;
		}

		public void CheckForWindrunHighLow(DateTime timestamp)
		{
			var adjustedtimestamp = cumulus.MeteoDate(timestamp);

			if (MetData.WindRunToday > Records.ThisMonth.HighWindRun.Val)
			{
				Records.ThisMonth.HighWindRun.Val = MetData.WindRunToday;
				Records.ThisMonth.HighWindRun.Ts = adjustedtimestamp;
				WriteMonthIniFile();
			}

			if (MetData.WindRunToday > Records.ThisYear.HighWindRun.Val)
			{
				Records.ThisYear.HighWindRun.Val = MetData.WindRunToday;
				Records.ThisYear.HighWindRun.Ts = adjustedtimestamp;
				WriteYearIniFile();
			}

			if (MetData.WindRunToday > Records.AllTime.HighWindRun.Val)
			{
				SetAlltime(Records.AllTime.HighWindRun, MetData.WindRunToday, adjustedtimestamp);
			}

			CheckMonthlyAlltime("HighWindRun", MetData.WindRunToday, true, adjustedtimestamp);
		}

		public double GetWindRunMonth(int year, int month)
		{
			var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Local);
			var enddate = startDate.AddMonths(1);

			var now = cumulus.MeteoDate();

			if (now.Day == 1 && now.Date == startDate.Date)
			{
				// This month, and first day so no day file entries
				// return windrun so far today
				return MetData.WindRunToday;
			}

			var dayfile = MetData.DayFile.Where(r => r.Date >= startDate && r.Date < enddate).Sum(r => r.WindRun);

			// if the current month add todays windrun
			if (year == now.Year && month == now.Month)
			{
				dayfile += MetData.WindRunToday;
			}

			return dayfile;
		}

		public void CalculateDominantWindBearing(int averageBearing, double averageSpeed, int minutes)
		{
			MetData.DominantWindBearingX += minutes * averageSpeed * Math.Sin(Trig.DegToRad(averageBearing));
			MetData.DominantWindBearingY += minutes * averageSpeed * Math.Cos(Trig.DegToRad(averageBearing));
			MetData.DominantWindBearingMinutes += minutes;

			if (Math.Abs(MetData.DominantWindBearingX) < 0.001 && Math.Abs(MetData.DominantWindBearingY) < 0.001)
			{
				MetData.DominantWindBearing = 0;
			}
			else
			{
				try
				{
					MetData.DominantWindBearing = calcavgbear(MetData.DominantWindBearingX, MetData.DominantWindBearingY);
					if (MetData.DominantWindBearing == 0)
					{
						MetData.DominantWindBearing = 360;
					}
				}
				catch
				{
					cumulus.LogErrorMessage("Error in dominant wind direction calculation");
				}
			}
		}

		// Returns true if the gust value exceeds current RecentMaxGust, false if it fails
		public bool CheckHighGust(double gust, int gustdir, DateTime timestamp)
		{
			if (gust >= cumulus.Limit.WindHigh)
			{
				cumulus.LogSpikeRemoval("Wind Gust greater than the limit; reading ignored");
				cumulus.LogSpikeRemoval($"Gust: NewVal={gust.ToString(cumulus.WindFormat)} HighLimit={cumulus.Limit.WindHigh.ToString(cumulus.WindFormat)}");
				lastSpikeRemoval = timestamp;
				cumulus.SpikeAlarm.LastMessage = $"Wind Gust greater than limit - NewVal={gust.ToString(cumulus.WindFormat)}, OldVal={cumulus.Limit.WindHigh.ToString(cumulus.WindFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return false;
			}

			if (gust > DailyHighLow.Today.HighGust)
			{
				DailyHighLow.Today.HighGust = gust;
				DailyHighLow.Today.HighGustTime = timestamp;
				DailyHighLow.Today.HighGustBearing = gustdir;
				WriteTodayFile(timestamp, false);
			}
			if (gust > Records.ThisMonth.HighGust.Val)
			{
				Records.ThisMonth.HighGust.Val = gust;
				Records.ThisMonth.HighGust.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (gust > Records.ThisYear.HighGust.Val)
			{
				Records.ThisYear.HighGust.Val = gust;
				Records.ThisYear.HighGust.Ts = timestamp;
				WriteYearIniFile();
			}
			// All time high gust?
			if (gust > Records.AllTime.HighGust.Val)
			{
				SetAlltime(Records.AllTime.HighGust, gust, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime("HighGust", gust, true, timestamp);

			cumulus.HighGustAlarm.CheckAlarm(gust);

			return gust > MetData.RecentMaxGust;
		}

		public void CheckHighAvgSpeed(DateTime timestamp)
		{
			if (MetData.WindAverage > DailyHighLow.Today.HighWind)
			{
				DailyHighLow.Today.HighWind = MetData.WindAverage;
				DailyHighLow.Today.HighWindTime = timestamp;
				WriteTodayFile(timestamp, false);
			}
			if (MetData.WindAverage > Records.ThisMonth.HighWind.Val)
			{
				Records.ThisMonth.HighWind.Val = MetData.WindAverage;
				Records.ThisMonth.HighWind.Ts = timestamp;
				WriteMonthIniFile();
			}
			if (MetData.WindAverage > Records.ThisYear.HighWind.Val)
			{
				Records.ThisYear.HighWind.Val = MetData.WindAverage;
				Records.ThisYear.HighWind.Ts = timestamp;
				WriteYearIniFile();
			}

			// All time high wind speed?
			if (MetData.WindAverage > Records.AllTime.HighWind.Val)
			{
				SetAlltime(Records.AllTime.HighWind, MetData.WindAverage, timestamp);
			}

			// check for monthly all time records (and set)
			CheckMonthlyAlltime("HighWind", MetData.WindAverage, true, timestamp);

			cumulus.HighWindAlarm.CheckAlarm(MetData.WindAverage);
		}
	}
}
