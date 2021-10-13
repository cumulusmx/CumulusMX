using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CumulusMX
{
	internal class NOAA
	{
		private struct Tdaysummary
		{
			public bool valid;
			public double meantemp;
			public double maxtemp;
			public DateTime maxtemptimestamp;
			public double mintemp;
			public DateTime mintemptimestamp;
			public double heatingdegdays;
			public double coolingdegdays;
			public double rain;
			public double totalwindspeed;
			public int windsamples;
			public double avgwindspeed;
			public double highwindspeed;
			public DateTime highwindtimestamp;
			public int winddomdir;
			public double totalwinddirX;
			public double totalwinddirY;
		}

		private struct Tmonthsummary
		{
			public bool valid;
			public double totaltemp;
			public double totalmaxtemp;
			public double totalmintemp;
			public double meantemp;
			public double maxtemp;
			public int maxtempday;
			public double mintemp;
			public int mintempday;
			public double meanmaxtemp;
			public double meanmintemp;
			public double heatingdegdays;
			public double coolingdegdays;
			public double totrain;
			public double totalwindspeed;
			public int samples;
			public double avgwindspeed;
			public double highwindspeed;
			public int highwindday;
			public int winddomdir;
			//public double totalwinddirX;
			//public double totalwinddirY;
			public int raincount1;
			public int raincount2;
			public int raincount3;
			public double maxrain;
			public int maxrainday;
			public int maxtempcount1;
			public int maxtempcount2;
			public int mintempcount1;
			public int mintempcount2;
		} // end Tmonthsummary

		private readonly Cumulus cumulus;

		public NOAA(Cumulus cumulus)
		{
			this.cumulus = cumulus;
		}

		/// <summary>
		/// checks whether first value is LE second to 3dp
		/// </summary>
		/// <param name="value1"></param>
		/// <param name="value2"></param>
		/// <returns></returns>
		private static bool LessThanOrEqual(double value1, double value2)
		{
			int intvalue1 = Convert.ToInt32(value1*1000);
			int intvalue2 = Convert.ToInt32(value2*1000);
			return (intvalue1 <= intvalue2);
		}

		/// <summary>
		/// checks whether first value is GE second to 3dp
		/// </summary>
		/// <param name="value1"></param>
		/// <param name="value2"></param>
		/// <returns></returns>
		private static bool GreaterThanOrEqual(double value1, double value2)
		{
			int intvalue1 = Convert.ToInt32(value1*1000);
			int intvalue2 = Convert.ToInt32(value2*1000);
			return (intvalue1 >= intvalue2);
		}

		private  string CompassPoint(int bearing)
		{
			return cumulus.compassp[(((bearing * 100) + 1125) % 36000) / 2250];
		}

		private static double Frac(double num)
		{
			return num - Math.Floor(num);
		}

		private void DecodeLatLong(double latLong, out int deg, out int min, out int sec)
		{
			deg = (int) Math.Floor(latLong);
			latLong = Frac(latLong) * 60;
			min = (int)Math.Floor(latLong);
			latLong = Frac(latLong) * 60;
			sec = (int) Math.Round(latLong);
		}

		private double GetAverageWindSpeed(int month, int year, out int domdir)
		{
			int linenum = 0;
			int windsamples = 0;
			double avgwindspeed;
			double totalwinddirX = 0;
			double totalwinddirY = 0;
			double totalwindspeed = 0;

			// Use the second of the month to allow for 9am roll-over
			var logFile = cumulus.GetLogFileName(new DateTime(year, month, 2));
			if (File.Exists(logFile))
			{
				var idx = 0;
				var st = new List<string>();
				try
				{
					var lines = File.ReadAllLines(logFile);
					linenum = 0;
					windsamples = 0;

					foreach (var line in lines)
					{
						// now process each record in the file

						linenum++;
						st = new List<string>(line.Split(cumulus.ListSeparator[0]));
						idx = 5;
						double windspeed = Convert.ToSingle(st[idx]);
						// add in wind speed sample for whole month
						windsamples++;
						totalwindspeed += windspeed;
						// add in direction if not done already
						idx = 7;
						var winddir = Convert.ToInt32(st[idx]);
						totalwinddirX += (windspeed * Math.Sin((winddir * (Math.PI / 180))));
						totalwinddirY += (windspeed * Math.Cos((winddir * (Math.PI / 180))));
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage($"Error at line {linenum}, column {idx}, value '{(st.Count >= idx ? st[idx] : "")}' of {logFile} : {e}");
					cumulus.LogMessage("Please edit the file to correct the error");
				}
			}
			if (windsamples > 0)
			{
				avgwindspeed = totalwindspeed/windsamples;
			}
			else
			{
				avgwindspeed = -1000;
			}
			try
			{
				//domdir = 90 - (int)Math.Floor(RadToDeg(Math.Atan2(totalwinddirY, totalwinddirX))); //(int) Convert.ToInt64((Math.Atan(totalwinddirY/totalwinddirX)*(180/Math.PI)));
				domdir = CalcAvgBearing(totalwinddirX, totalwinddirY);
				if (domdir == 0)
				{
					domdir = 360;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error in NOAA dominant wind direction calculation: " + ex.Message);
				domdir = 0;
			}
			return avgwindspeed;
		}

		private static int CalcAvgBearing(double x, double y)
		{
			var avg = 90 - (int)(Trig.RadToDeg(Math.Atan2(y, x)));
			if (avg < 0)
			{
				avg = 360 + avg;
			}

			return avg;
		}

		public List<string> CreateMonthlyReport(DateTime thedate)
		{
			var output = new List<string>();

			CultureInfo culture = cumulus.NOAAconf.UseDotDecimal ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;

			Tdaysummary[] dayList = new Tdaysummary[32];

			for (int i = 1; i < 32; i++)
			{
				dayList[i].valid = false;
				dayList[i].totalwindspeed = 0;
				dayList[i].windsamples = 0;
				dayList[i].winddomdir = 0;
				dayList[i].totalwinddirX = 0;
				dayList[i].totalwinddirY = 0;
			}

			double totalheating = 0;
			double totalcooling = 0;
			double totalmeantemp = 0;
			double totalrain = 0;
			double totalwindspeed = 0;
			int windsamples = 0;
			int daycount = 0;
			int maxtempday = 0;
			int mintempday = 0;
			int highwindday = 0;
			double maxtemp = -999;
			double mintemp = 999;
			double meantemp;
			double highwind = 0;
			double totalwinddirX = 0;
			double totalwinddirY = 0;
			int maxtempcount1 = 0;
			int maxtempcount2 = 0;
			int mintempcount1 = 0;
			int mintempcount2 = 0;
			double maxrain = 0;
			int maxrainday = 0;
			int raincount1 = 0;
			int raincount2 = 0;
			int raincount3 = 0;

			int month = thedate.Month;
			int year = thedate.Year;
			int linenum = 0;
			int idx = 0;
			List<string> st = new List<string>();

			try
			{
				var lines = File.ReadAllLines(cumulus.DayFileName);

				foreach (var line in lines)
				{
					linenum++;
					st = new List<string>(line.Split(cumulus.ListSeparator[0]));
					idx = 0;

					var dateStr = st[0];
					var dat = Utils.ddmmyyStrToDate(dateStr);
					if (dat.Month != month || dat.Year != year)
						continue;

					// entry is for this month (month and year match)
					int daynumber = dat.Day;

					if (dayList[daynumber].valid)
					{
						// already had this date - error!
						cumulus.LogMessage($"Duplicate entry at line {linenum} of dayfile.txt: {dateStr}. Please correct this by editing the file");
						continue;
					}

					// haven't had this entry yet

					// max temp
					idx = 6;
					dayList[daynumber].maxtemp = double.Parse(st[idx]);
					idx = 7;
					string timestr = st[idx];
					int hour = Convert.ToInt32(timestr.Substring(0, 2));
					int minute = Convert.ToInt32(timestr.Substring(3, 2));
					dayList[daynumber].maxtemptimestamp = DateTime.MinValue.Date.Add(new TimeSpan(hour, minute, 0));
					if (dayList[daynumber].maxtemp > maxtemp)
					{
						maxtemp = dayList[daynumber].maxtemp;
						maxtempday = daynumber;
					}
					if (GreaterThanOrEqual(dayList[daynumber].maxtemp, cumulus.NOAAconf.MaxTempComp1))
					{
						maxtempcount1++;
					}
					if (LessThanOrEqual(dayList[daynumber].maxtemp, cumulus.NOAAconf.MaxTempComp2))
					{
						maxtempcount2++;
					}

					// min temp
					idx = 4;
					dayList[daynumber].mintemp = double.Parse(st[idx]);
					idx = 5;
					timestr = st[idx];
					hour = Convert.ToInt32(timestr.Substring(0, 2));
					minute = Convert.ToInt32(timestr.Substring(3, 2));
					dayList[daynumber].mintemptimestamp = DateTime.MinValue.Date.Add(new TimeSpan(hour, minute, 0));
					if (dayList[daynumber].mintemp < mintemp)
					{
						mintemp = dayList[daynumber].mintemp;
						mintempday = daynumber;
					}
					if (LessThanOrEqual(dayList[daynumber].mintemp, cumulus.NOAAconf.MinTempComp1))
					{
						mintempcount1++;
					}
					if (LessThanOrEqual(dayList[daynumber].mintemp, cumulus.NOAAconf.MinTempComp2))
					{
						mintempcount2++;
					}

					// mean temp
					if (cumulus.NOAAconf.UseMinMaxAvg)
					{
						meantemp = (dayList[daynumber].maxtemp + dayList[daynumber].mintemp) / 2.0;
						totalmeantemp += meantemp;
						dayList[daynumber].meantemp = meantemp;
					}
					else if ((st.Count > 15) && (st[15].Length > 0))
					{
						idx = 15;
						meantemp = double.Parse(st[idx]);
						totalmeantemp += meantemp;
						dayList[daynumber].meantemp = meantemp;
					}
					else
					{
						// average temp field not present
						meantemp = -1000;
						dayList[daynumber].meantemp = -1000;
						dayList[daynumber].heatingdegdays = 0;
						dayList[daynumber].coolingdegdays = 0;
					}

					if (meantemp > -1000)
					{
						// heating degree day
						idx = 40;
						if ((st.Count > idx) && (st[idx].Length > 0))
						{
							// read HDD from dayfile.txt
							dayList[daynumber].heatingdegdays = double.Parse(st[idx]);
							totalheating += double.Parse(st[idx]);
						}
						else if (meantemp < cumulus.NOAAconf.HeatThreshold)
						{
							dayList[daynumber].heatingdegdays = cumulus.NOAAconf.HeatThreshold - meantemp;
							totalheating += cumulus.NOAAconf.HeatThreshold - meantemp;
						}
						else
						{
							dayList[daynumber].heatingdegdays = 0;
						}

						// cooling degree days
						idx = 41;
						if ((st.Count > idx) && (st[idx] != string.Empty))
						{
							// read HDD from dayfile.txt
							dayList[daynumber].coolingdegdays = double.Parse(st[idx]);
							totalcooling += double.Parse(st[idx]);
						}
						else if (meantemp > cumulus.NOAAconf.CoolThreshold)
						{
							dayList[daynumber].coolingdegdays = meantemp - cumulus.NOAAconf.CoolThreshold;
							totalcooling += meantemp - cumulus.NOAAconf.CoolThreshold;
						}
						else
						{
							dayList[daynumber].coolingdegdays = 0;
						}
					}

					// rain
					idx = 14;
					dayList[daynumber].rain = double.Parse(st[idx]);
					totalrain += double.Parse(st[idx]);
					if (dayList[daynumber].rain > maxrain)
					{
						maxrain = dayList[daynumber].rain;
						maxrainday = daynumber;
					}

					if (GreaterThanOrEqual(dayList[daynumber].rain, cumulus.NOAAconf.RainComp1))
					{
						raincount1++;
					}
					if (GreaterThanOrEqual(dayList[daynumber].rain, cumulus.NOAAconf.RainComp2))
					{
						raincount2++;
					}
					if (GreaterThanOrEqual(dayList[daynumber].rain, cumulus.NOAAconf.RainComp3))
					{
						raincount3++;
					}

					// high wind speed
					idx = 1;
					dayList[daynumber].highwindspeed = double.Parse(st[idx]);
					idx = 3;
					timestr = st[idx];
					hour = Convert.ToInt32(timestr.Substring(0, 2));
					minute = Convert.ToInt32(timestr.Substring(3, 2));
					dayList[daynumber].highwindtimestamp = DateTime.MinValue.Date.Add(new TimeSpan(hour, minute, 0));
					if (dayList[daynumber].highwindspeed > highwind)
					{
						highwind = dayList[daynumber].highwindspeed;
						highwindday = daynumber;
					}

					// dominant wind bearing
					idx = 39;
					if ((st.Count > idx) && (st[idx] != string.Empty))
					{
						dayList[daynumber].winddomdir = Convert.ToInt32((st[idx]));
					}

					daycount++;
					dayList[daynumber].valid = true;
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"Error at line {linenum}, column {idx}, value '{(st.Count >= idx ? st[idx] : "")}' of dayfile.txt: " + ex.Message);
				cumulus.LogMessage("Please edit the file to correct the error");
			}

			// Calculate average wind speed from log file
			// Use the second of the month in case of 9am roll-over
			var logFile = cumulus.GetLogFileName(new DateTime(thedate.Year, thedate.Month, 2));

			if (File.Exists(logFile))
			{
				idx = 0;
				int daynumber = 1;

				try
				{
					linenum = 0;
					var lines = File.ReadAllLines(logFile);

					foreach (var line in lines)
					{
						// now process each record in the file
						linenum++;
						st = new List<string>(line.Split(cumulus.ListSeparator[0]));

						idx = 0;
						int entryday = Convert.ToInt32(st[idx].Substring(0, 2));
						int entrymonth = Convert.ToInt32(st[idx].Substring(3, 2));
						int entryyear = Convert.ToInt32(st[idx].Substring(6, 2));
						idx = 1;
						int entryhour = Convert.ToInt32(st[idx].Substring(0, 2));
						int entryminute = Convert.ToInt32(st[idx].Substring(3, 2));

						DateTime entrydate = new DateTime(entryyear, entrymonth, entryday, entryhour, entryminute, 0);

						entrydate = entrydate.AddHours(cumulus.GetHourInc(entrydate));

						daynumber = entrydate.Day;

						if (!dayList[daynumber].valid)
							continue;

						idx = 5;
						double windspeed = double.Parse(st[idx]);

						// add in wind speed sample for this day
						dayList[daynumber].windsamples++;
						dayList[daynumber].totalwindspeed += windspeed;

						// add in wind speed sample for whole month
						windsamples++;
						totalwindspeed += windspeed;

						// add in direction if (not done already
						if (dayList[daynumber].winddomdir == 0)
						{
							idx = 7;
							int winddir = Convert.ToInt32(st[idx]);
							dayList[daynumber].totalwinddirX += (windspeed * Math.Sin(Trig.DegToRad(winddir)));
							dayList[daynumber].totalwinddirY += (windspeed * Math.Cos(Trig.DegToRad(winddir)));
						}
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage($"Error at line {linenum}, column {idx}, value '{(st.Count >= idx ? st[idx] : "")}' of {logFile} : {ex}");
					cumulus.LogMessage("Please edit the file to correct the error");
					// set the days after this error as invalid
					for (var i = daynumber; i < dayList.Length - 1; i++)
					{
						dayList[i].valid = false;
					}
				}
			}

			double avgwindspeed;
			if (windsamples > 0)
			{
				avgwindspeed = totalwindspeed/windsamples;
			}
			else
			{
				avgwindspeed = -1000;
			}

			for (int i = 1; i < 32; i++)
			{
				if (dayList[i].windsamples > 0)
					dayList[i].avgwindspeed = dayList[i].totalwindspeed / dayList[i].windsamples;
				else
					dayList[i].avgwindspeed = -1000;

				// calculate dominant wind bearing if (required
				if (dayList[i].winddomdir == 0)
				{
					if (dayList[i].totalwinddirX == 0)
						dayList[i].winddomdir = 0;
					else
					{
						try
						{
							dayList[i].winddomdir = CalcAvgBearing(dayList[i].totalwinddirX, dayList[i].totalwinddirY);// 90 - (int)Math.Floor(RadToDeg(Math.Atan2(DayList[i].totalwinddirY, DayList[i].totalwinddirX)));
								//(int)Math.Floor(RadToDeg(Math.Atan(DayList[i].totalwinddirY / DayList[i].totalwinddirX)));
						}
						catch
						{
							cumulus.LogMessage("Error in NOAA dominant wind direction calculation ");
						}

						if (dayList[i].winddomdir == 0)
						{
							dayList[i].winddomdir = 360;
						}
					}
				}

				// add up vectors for overall dom dir
				if (dayList[i].windsamples > 0)
					// there"s an average speed available
				{
					totalwinddirX += (dayList[i].avgwindspeed*Math.Sin(Trig.DegToRad(dayList[i].winddomdir)));
					totalwinddirY += (dayList[i].avgwindspeed*Math.Cos(Trig.DegToRad(dayList[i].winddomdir)));
				}
			}

			int overalldomdir;
			try
			{
				overalldomdir = CalcAvgBearing(totalwinddirX, totalwinddirY);

				if (overalldomdir == 0)
					overalldomdir = 360;
			}
			catch
			{
				cumulus.LogMessage("Error in NOAA dominant wind direction calculation ");
				overalldomdir = 0;
			}

			// Now output everything

			output.Add($"                   Monthly Climatological Summary for {thedate:MMM} {year}");
			output.Add("");
			output.Add($"Name: {cumulus.NOAAconf.Name}   City: {cumulus.NOAAconf.City}   State: {cumulus.NOAAconf.State}");
			string elev;
			if (cumulus.AltitudeInFeet)
			{
				elev = cumulus.Altitude + " ft";
			}
			else
			{
				elev = cumulus.Altitude + " m";
			}

			int latdeg;
			int latmin;
			int latsec;
			DecodeLatLong(Math.Abs(cumulus.Latitude), out latdeg, out latmin, out latsec);
			int londeg;
			int lonmin;
			int lonsec;
			DecodeLatLong(Math.Abs(cumulus.Longitude), out londeg, out lonmin, out lonsec);

			var lathem = cumulus.Latitude > 0 ? "N" : "S";
			var lonhem = cumulus.Longitude > 0 ? "E" : "W";

			latdeg = Math.Abs(latdeg);
			londeg = Math.Abs(londeg);

			output.Add($"Elevation: {elev}  Lat: {string.Format("{0} {1,2:D2}° {2,2:D2}' {3,2:D2}\"", lathem, latdeg, latmin, latsec)}   Lon: {string.Format("{0} {1,3:D3}° {2,2:D2}' {3,2:D2}\"", lonhem, londeg, lonmin, lonsec)}");
			output.Add("");
			output.Add($"                  Temperature ({cumulus.Units.TempText}), Rain ({cumulus.Units.RainText}), Wind Speed ({cumulus.Units.WindText})");
			output.Add("");
			output.Add("                                      Heat  Cool        Avg");
			output.Add("    Mean                              Deg   Deg         Wind                 Dom");
			output.Add("Day Temp  High   Time   Low    Time   Days  Days  Rain  Speed High   Time    Dir");
			output.Add("----------------------------------------------------------------------------------");

			var repLine = new StringBuilder(200);

			var timeFormat = cumulus.NOAAconf.Use12hour ? "h:mmtt" : "HH:mm";

			for (int i = 1; i <= DateTime.DaysInMonth(year, month); i++)
			{
				if (dayList[i].valid)
				{
					repLine.Clear();
					repLine.Append(i.ToString("D2"));
					if (dayList[i].meantemp < -999)
					{
						repLine.Append("  ----");
					}
					else
					{
						repLine.Append(string.Format(culture, "{0,6:F1}",dayList[i].meantemp));
					}
					;
					repLine.Append(string.Format(culture, "{0,6:F1}", dayList[i].maxtemp));
					string timestr = dayList[i].maxtemptimestamp.ToString(timeFormat);
					repLine.Append(string.Format("{0,8}", timestr));
					repLine.Append(string.Format(culture, "{0,6:F1}", dayList[i].mintemp));
					timestr = dayList[i].mintemptimestamp.ToString(timeFormat);
					repLine.Append(string.Format("{0,8}", timestr));

					if (dayList[i].meantemp < -999)
					{
						repLine.Append("  ----");
					}
					else
					{
						repLine.Append(string.Format(culture, "{0,6:F1}", dayList[i].heatingdegdays));
						repLine.Append(string.Format(culture, "{0,6:F1}", dayList[i].coolingdegdays));
					}
					repLine.Append(string.Format("{0,6}", dayList[i].rain.ToString(cumulus.RainFormat, culture)));

					if (dayList[i].avgwindspeed < -999)
						repLine.Append("  ----");
					else
						repLine.Append(string.Format(culture, "{0,6:F1}", dayList[i].avgwindspeed));

					repLine.Append(string.Format(culture, "{0,6:F1}", dayList[i].highwindspeed));
					timestr = dayList[i].highwindtimestamp.ToString(timeFormat);
					repLine.Append(string.Format("{0,8}", timestr));
					repLine.Append(string.Format("{0,6}", CompassPoint(dayList[i].winddomdir)));
					output.Add(repLine.ToString());
				}
			}
			output.Add("----------------------------------------------------------------------------------");

			// Build summary line
			repLine.Clear();
			if (daycount == 0)
			{
				repLine.Append("    ----");
			}
			else
			{
				repLine.Append(string.Format(culture, "{0,8:F1}", totalmeantemp/daycount));
			}

			if (maxtempday == 0)
			{
				repLine.Append("  ----    --");
			}
			else
			{
				repLine.Append(string.Format(culture, "{0,6:F1}", maxtemp));
				repLine.Append(string.Format(culture, "{0,6:D}", maxtempday));
			}

			if (mintempday == 0)
			{
				repLine.Append("    ----    --");
			}
			else
			{
				repLine.Append(string.Format(culture, "{0,8:F1}", mintemp));
				repLine.Append(string.Format(culture, "{0,6:D}", mintempday));
			}

			repLine.Append(string.Format(culture, "{0,8:F1}", totalheating));
			repLine.Append(string.Format(culture, "{0,6:F1}", totalcooling));

			repLine.Append(string.Format("{0,6}", totalrain.ToString(cumulus.RainFormat, culture)));

			if (avgwindspeed < -999)
			{
				repLine.Append("  ----");
			}
			else
			{
				repLine.Append(string.Format(culture, "{0,6:F1}", avgwindspeed));
			}
			;

			repLine.Append(string.Format(culture, "{0,6:F1}", highwind));
			repLine.Append(string.Format(culture, "{0,6:D}", highwindday));

			repLine.Append(string.Format("{0,8}", CompassPoint(overalldomdir)));

			output.Add(repLine.ToString());

			output.Add("");

			// now do the max/min/days of rain items
			output.Add(string.Format(culture, "Max >={0,6:F1}{1,3:D}", cumulus.NOAAconf.MaxTempComp1, maxtempcount1));
			output.Add(string.Format(culture, "Max <={0,6:F1}{1,3:D}", cumulus.NOAAconf.MaxTempComp2, maxtempcount2));
			output.Add(string.Format(culture, "Min <={0,6:F1}{1,3:D}", cumulus.NOAAconf.MinTempComp1, mintempcount1));
			output.Add(string.Format(culture, "Min <={0,6:F1}{1,3:D}", cumulus.NOAAconf.MinTempComp2, mintempcount2));

			output.Add($"Max Rain: {maxrain.ToString(cumulus.RainFormat, culture)} on day {maxrainday}");

			output.Add($"Days of Rain: {raincount1} (>= {cumulus.NOAAconf.RainComp1.ToString(cumulus.RainFormat, culture)} {cumulus.Units.RainText})  {raincount2} (>= {cumulus.NOAAconf.RainComp2.ToString(cumulus.RainFormat, culture)} {cumulus.Units.RainText})  {raincount3} (>= {cumulus.NOAAconf.RainComp3.ToString(cumulus.RainFormat, culture)} {cumulus.Units.RainText})");
			output.Add($"Heat Base: {cumulus.NOAAconf.HeatThreshold.ToString(cumulus.TempFormat, culture)}  Cool Base: {cumulus.NOAAconf.CoolThreshold.ToString(cumulus.TempFormat, culture)}  Method: Integration");

			return output;
		}

		public List<string> CreateYearlyReport(DateTime thedate)
		{
			var output = new List<string>();

			CultureInfo culture = cumulus.NOAAconf.UseDotDecimal ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture;

			string listSep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
			StringBuilder repLine = new StringBuilder(200);
			int linenum = 0;

			Tmonthsummary[] MonthList = new Tmonthsummary[13];

			int month;
			int year = thedate.Year;
			string twodigityear = thedate.ToString("yy");

			int m;
			int domdir;

			int samples = 0;
			double totalheating = 0;
			double totalcooling = 0;
			double totalmeantemp = 0;
			double totalmeanmaxtemp = 0;
			double totalmeanmintemp = 0;
			int mintempmonth = 0;
			int maxtempmonth = 0;
			double maxtemp = -999;
			double mintemp = 999;
			int maxtempcount1 = 0;
			int maxtempcount2 = 0;
			int mintempcount1 = 0;
			int mintempcount2 = 0;
			double totalrain = 0;
			int raincount1 = 0;
			int raincount2 = 0;
			int raincount3 = 0;
			double maxrain = 0;
			int maxrainmonth = 0;
			double totalavgwind = 0;
			int avgwindcount = 0;
			double highwind = 0;
			int highwindmonth = 0;
			double totalnormtemp = 0;
			int normtempsamples = 0;
			double totalnormrain = 0;
			double totalwinddirX = 0;
			double totalwinddirY = 0;

			for (m = 1; m <= 12; m++)
			{
				MonthList[m].valid = false;
				MonthList[m].samples = 0;
				MonthList[m].heatingdegdays = 0;
				MonthList[m].coolingdegdays = 0;
				MonthList[m].maxtempcount1 = 0;
				MonthList[m].maxtempcount2 = 0;
				MonthList[m].mintempcount1 = 0;
				MonthList[m].mintempcount2 = 0;
				MonthList[m].totrain = 0;
				MonthList[m].highwindspeed = 0;
				MonthList[m].totaltemp = 0;
				MonthList[m].totalmaxtemp = 0;
				MonthList[m].totalmintemp = 0;
				MonthList[m].raincount1 = 0;
				MonthList[m].raincount2 = 0;
				MonthList[m].raincount3 = 0;
				MonthList[m].maxtemp = -999;
				MonthList[m].mintemp = 999;
				MonthList[m].meantemp = 0;
				MonthList[m].meanmaxtemp = 0;
				MonthList[m].meanmintemp = 0;
				MonthList[m].totalwindspeed = 0;
				MonthList[m].avgwindspeed = 0;
				MonthList[m].maxrain = 0;
			}
			try
			{
				using (FileStream fs = new FileStream(cumulus.DayFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
				using (var sr = new StreamReader(fs))
				{
					do
					{
						var line = sr.ReadLine();
						linenum++;
						var st = new List<string>(Regex.Split(line, listSep));
						string dateStr = st[0];
						var dat = Utils.ddmmyyStrToDate(dateStr);
						if (dat.Year != year)
							continue;

						// entry is for this year
						var day = dat.Day;
						month = dat.Month;
						var maxval = Convert.ToDouble(st[6]);
						var minval = Convert.ToDouble(st[4]);
						MonthList[month].totalmaxtemp += maxval;
						MonthList[month].totalmintemp += minval;
						var meantemp = cumulus.NOAAconf.UseMinMaxAvg ? (maxval + minval) / 2.0 : Convert.ToDouble(st[15]);
						MonthList[month].valid = true;
						MonthList[month].samples ++;
						MonthList[month].totaltemp += meantemp;
						// Max temp?
						if (maxval > MonthList[month].maxtemp)
						{
							MonthList[month].maxtemp = maxval;
							MonthList[month].maxtempday = day;
						}
						if (GreaterThanOrEqual(maxval, cumulus.NOAAconf.MaxTempComp1))
						{
							MonthList[month].maxtempcount1 ++;
						}
						if (LessThanOrEqual(maxval, cumulus.NOAAconf.MaxTempComp2))
						{
							MonthList[month].maxtempcount2 ++;
						}
						// Min temp?
						if (minval < MonthList[month].mintemp)
						{
							MonthList[month].mintemp = minval;
							MonthList[month].mintempday = day;
						}
						if (LessThanOrEqual(minval, cumulus.NOAAconf.MinTempComp1))
						{
							MonthList[month].mintempcount1 ++;
						}
						if (LessThanOrEqual(minval, cumulus.NOAAconf.MinTempComp2))
						{
							MonthList[month].mintempcount2 ++;
						}
						// heating degree days
						if ((st.Count > 40) && (st[40].Length > 0))
						{
							// read HDD from dayfile.txt
							MonthList[month].heatingdegdays = MonthList[month].heatingdegdays + Convert.ToDouble(st[40]);
							totalheating += Convert.ToDouble(st[40]);
						}
						else if (meantemp < cumulus.NOAAconf.HeatThreshold)
						{
							MonthList[month].heatingdegdays = MonthList[month].heatingdegdays + cumulus.NOAAconf.HeatThreshold - meantemp;
							totalheating += cumulus.NOAAconf.HeatThreshold - meantemp;
						}
						// cooling degree days
						if ((st.Count > 41) && (st[41].Length > 0))
						{
							// read HDD from dayfile.txt
							MonthList[month].coolingdegdays = MonthList[month].coolingdegdays + Convert.ToDouble(st[41]);
							totalcooling += Convert.ToDouble(st[41]);
						}
						else if (meantemp > cumulus.NOAAconf.CoolThreshold)
						{
							MonthList[month].coolingdegdays = MonthList[month].coolingdegdays + meantemp - cumulus.NOAAconf.CoolThreshold;
							totalcooling += meantemp - cumulus.NOAAconf.CoolThreshold;
						}
						// Rain days
						var rainvalue = Convert.ToDouble(st[14]);
						MonthList[month].totrain = MonthList[month].totrain + rainvalue;
						if (GreaterThanOrEqual(rainvalue, cumulus.NOAAconf.RainComp1))
						{
							MonthList[month].raincount1 ++;
						}
						if (GreaterThanOrEqual(rainvalue, cumulus.NOAAconf.RainComp2))
						{
							MonthList[month].raincount2 ++;
						}
						if (GreaterThanOrEqual(rainvalue, cumulus.NOAAconf.RainComp3))
						{
							MonthList[month].raincount3 ++;
						}
						// Max Rain?
						if (rainvalue > MonthList[month].maxrain)
						{
							MonthList[month].maxrain = rainvalue;
							MonthList[month].maxrainday = day;
						}
						// Max Gust?
						if (Convert.ToDouble(st[1]) > MonthList[month].highwindspeed)
						{
							MonthList[month].highwindspeed = Convert.ToDouble(st[1]);
							MonthList[month].highwindday = day;
						}
					} while (!(sr.EndOfStream));
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage($"Error at line {linenum} of dayfile.txt: { ex.Message}");
				cumulus.LogMessage("Please edit the file to correct the error");
			}

			// Now output everything
			try
			{
				output.Clear();
				output.Add($"                   Annual Climatological Summary for {year}");
				output.Add("");
				output.Add($"Name: {cumulus.NOAAconf.Name}   City: {cumulus.NOAAconf.City}   State: {cumulus.NOAAconf.State}");
				string elev;
				if (cumulus.AltitudeInFeet)
				{
					elev = cumulus.Altitude + " ft";
				}
				else
				{
					elev = cumulus.Altitude + " m";
				}
				int latdeg;
				int latmin;
				int latsec;
				DecodeLatLong(Math.Abs(cumulus.Latitude), out latdeg, out latmin, out latsec);
				int londeg;
				int lonmin;
				int lonsec;
				DecodeLatLong(Math.Abs(cumulus.Longitude), out londeg, out lonmin, out lonsec);

				var lathem = cumulus.Latitude > 0 ? "N" : "S";
				var lonhem = cumulus.Longitude > 0 ? "E" : "W";

				latdeg = Math.Abs(latdeg);
				londeg = Math.Abs(londeg);

				output.Add($"Elevation: {elev}  Lat: {string.Format("{0} {1,2:D2}° {2,2:D2}' {3,2:D2}\"", lathem, latdeg, latmin, latsec)}   Lon: {string.Format("{0} {1,3:D3}° {2,2:D2}' {3,2:D2}\"", lonhem, londeg, lonmin, lonsec)}");
				output.Add("");
				output.Add($"                  Temperature ({cumulus.Units.TempText}), Heat Base: {cumulus.NOAAconf.HeatThreshold.ToString(cumulus.TempFormat, culture)}  Cool Base: {cumulus.NOAAconf.CoolThreshold.ToString(cumulus.TempFormat, culture)}");
				output.Add("                          Dep.  Heat  Cool                       Max  Max  Min  Min");
				output.Add("        Mean  Mean        From  Deg   Deg                        >=   <=   <=   <=");
				//@ Unsupported function or procedure: 'Format'
				output.Add($" YR MO  Max   Min   Mean  Norm  Days  Days  Hi  Date  Low  Date{string.Format(culture, "{0,5:F1}{1,5:F1}{2,5:F1}{3,6:F1}", cumulus.NOAAconf.MaxTempComp1, cumulus.NOAAconf.MaxTempComp2, cumulus.NOAAconf.MinTempComp1, cumulus.NOAAconf.MinTempComp2)}");
				output.Add("------------------------------------------------------------------------------------");
				for (month = 1; month <= 12; month++)
				{
					repLine.Clear();
					repLine.Append(string.Format("{0,3}{1,3:D}", twodigityear, month));
					if (MonthList[month].valid)
					{
						if (MonthList[month].samples == 0)
						{
							repLine.Append("  ----  ----  ---");
						}
						else
						{
							MonthList[month].meanmaxtemp = MonthList[month].totalmaxtemp / MonthList[month].samples;
							MonthList[month].meanmintemp = MonthList[month].totalmintemp / MonthList[month].samples;
							MonthList[month].meantemp = MonthList[month].totaltemp / MonthList[month].samples;
							repLine.Append(string.Format(culture, "{0,6:F1}{1,6:F1}{2,6:F1}", MonthList[month].meanmaxtemp, MonthList[month].meanmintemp, MonthList[month].meantemp));
						}
						if (cumulus.NOAAconf.TempNorms[month] < -999)
						{
							// dummy value for 'departure from norm'
							repLine.Append("   0.0");
						}
						else
						{
							repLine.Append(string.Format(culture, "{0,6:F1}", (MonthList[month].meantemp - cumulus.NOAAconf.TempNorms[month])));
							totalnormtemp += cumulus.NOAAconf.TempNorms[month];
							normtempsamples++;
						}
						repLine.Append(string.Format(culture, "{0,6:D}{1,6:D}", Convert.ToInt64(MonthList[month].heatingdegdays), Convert.ToInt64(MonthList[month].coolingdegdays)));
						repLine.Append(string.Format(culture, "{0,6:F1}{1,4:D}{2,6:F1}{3,5:D}", MonthList[month].maxtemp, MonthList[month].maxtempday, MonthList[month].mintemp, MonthList[month].mintempday));
						repLine.Append(string.Format(culture, "{0,5:D}{1,5:D}{2,5:D}{3,5:D}", MonthList[month].maxtempcount1, MonthList[month].maxtempcount2, MonthList[month].mintempcount1, MonthList[month].mintempcount2));
					}
					output.Add(repLine.ToString());
				}
				output.Add("------------------------------------------------------------------------------------");

				// now do the summary

				for (m = 1; m < 13; m++)
				{
					if (!MonthList[m].valid)
						continue;

					samples += MonthList[m].samples;
					totalmeanmaxtemp += MonthList[m].meanmaxtemp * MonthList[m].samples;
					totalmeanmintemp += MonthList[m].meanmintemp * MonthList[m].samples;
					totalmeantemp += MonthList[m].meantemp * MonthList[m].samples;

					if (MonthList[m].maxtemp > maxtemp)
					{
						maxtemp = MonthList[m].maxtemp;
						maxtempmonth = m;
					}

					if (MonthList[m].mintemp < mintemp)
					{
						mintemp = MonthList[m].mintemp;
						mintempmonth = m;
					}

					maxtempcount1 += MonthList[m].maxtempcount1;
					maxtempcount2 += MonthList[m].maxtempcount2;
					mintempcount1 += MonthList[m].mintempcount1;
					mintempcount2 += MonthList[m].mintempcount2;
				}

				if (samples > 0)
				{
					repLine.Clear();
					double meanmax = totalmeanmaxtemp / samples;
					double meanmin = totalmeanmintemp / samples;
					double meantemp = totalmeantemp / samples;
					repLine.Append(string.Format(culture, "{0,12:F1}{1,6:F1}{2,6:F1}", meanmax, meanmin, meantemp));
					if (normtempsamples == 0)
						// dummy value for "departure from norm"
						repLine.Append("   0.0");
					else
					{
						repLine.Append(string.Format(culture, "{0,6:F1}", (meantemp - (totalnormtemp / normtempsamples))));
					}
					repLine.Append(string.Format(culture, "{0,6:D}{1,6:D}", (int)(totalheating), (int)(totalcooling)));
					if (maxtempmonth == 0)
					{
						repLine.Append(string.Format(culture, "{0,6:F1}{1,4}", maxtemp, "---"));
					}
					else
					{
						repLine.Append(string.Format(culture, "{0,6:F1}{1,4}", maxtemp, CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(maxtempmonth)));
					}
					if (mintempmonth == 0)
						repLine.Append(string.Format(culture, "{0,6:F1}{1,5}", mintemp, "---"));
					else
					{
						repLine.Append(string.Format(culture, "{0,6:F1}{1,5}", mintemp, CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(mintempmonth)));
					}
					repLine.Append(string.Format("{0,5:D}{1,5:D}{2,5:D}{3,5:D}", maxtempcount1, maxtempcount2, mintempcount1, mintempcount2));
					output.Add(repLine.ToString());
				}
				else
				{
					output.Add("");
				}

				// Rain section header
				output.Add("");
				output.Add("                                Precipitation (" + cumulus.Units.RainText + ")");
				output.Add("");
				output.Add("              Dep.   Max        Days of Rain");
				output.Add("              From   Obs.           >=");
				output.Add(" YR MO Total  Norm   Day Date" +
						   string.Format("{0,5}{1,5}{2,5}", cumulus.NOAAconf.RainComp1.ToString(cumulus.RainFormat, culture), cumulus.NOAAconf.RainComp2.ToString(cumulus.RainFormat, culture),
							   cumulus.NOAAconf.RainComp3.ToString(cumulus.RainFormat, culture)));
				output.Add("---------------------------------------------");

				// Rain section details
				for (m = 1; m < 13; m++)
				{
					repLine.Clear();
					repLine.Append(string.Format("{0,3}{1,3:D}", twodigityear, m));

					if (!MonthList[m].valid)
						continue;

					repLine.Append(string.Format("{0,6}", MonthList[m].totrain.ToString(cumulus.RainFormat, culture)));
					totalrain += MonthList[m].totrain;

					if (MonthList[m].maxrain > maxrain)
					{
						maxrain = MonthList[m].maxrain;
						maxrainmonth = m;
					}

					if (cumulus.NOAAconf.RainNorms[m] < -999)
						// dummy value for "departure from norm"
						repLine.Append("   0.0");
					else
					{
						repLine.Append(string.Format("{0,6}", (MonthList[m].totrain - cumulus.NOAAconf.RainNorms[m]).ToString(cumulus.RainFormat, culture)));
						totalnormrain += cumulus.NOAAconf.RainNorms[m];
					}

					repLine.Append(string.Format("{0,6}", MonthList[m].maxrain.ToString(cumulus.RainFormat, culture)));
					repLine.Append(string.Format("{0,4:D}", MonthList[m].maxrainday));
					repLine.Append(string.Format("{0,6:D}{1,5:D}{2,5:D}", MonthList[m].raincount1, MonthList[m].raincount2, MonthList[m].raincount3));

					raincount1 += MonthList[m].raincount1;
					raincount2 += MonthList[m].raincount2;
					raincount3 += MonthList[m].raincount3;
					{
						output.Add(repLine.ToString());
					}
				}

				output.Add("---------------------------------------------");

				// rain summary
				if (samples > 0)
				{
					repLine.Clear();
					repLine.Append(string.Format("{0,12}", totalrain.ToString(cumulus.RainFormat, culture)));

					if (totalnormrain == 0)
					{
						// dummy value for "departure from norm"
						repLine.Append("   0.0");
					}
					else
					{
						repLine.Append(string.Format("{0,6}", (totalrain - totalnormrain).ToString(cumulus.RainFormat, culture)));
					}

					repLine.Append(string.Format("{0,6}", maxrain.ToString(cumulus.RainFormat, culture)));
					if (maxrainmonth == 0)
					{
						repLine.Append(string.Format("{0,5}", "---"));
					}
					else
					{
						repLine.Append(string.Format("{0,5}", CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(maxrainmonth)));
					}

					repLine.Append(string.Format("{0,5:D}{1,5:D}{2,5:D}", raincount1, raincount2, raincount3));

					output.Add(repLine.ToString());
				}
				else
				{
					output.Add("");
				}

				output.Add("");
				output.Add($"                                Wind Speed ({cumulus.Units.WindText})");
				output.Add("                          Dom");
				output.Add(" YR MO   Avg.  Hi   Date  Dir");
				output.Add("------------------------------");

				// Wind section details
				for (m = 1; m < 13; m++)
				{
					try
					{
						repLine.Clear();
						repLine.Append(string.Format("{0,3}{1,3:D}", twodigityear, m));

						if (MonthList[m].valid)
						{
							// calculate average wind speed
							MonthList[m].avgwindspeed = GetAverageWindSpeed(m, year, out domdir);
							MonthList[m].winddomdir = domdir;
							if (MonthList[m].avgwindspeed < 0)
							{
								// no valid average
								repLine.Append("  ----");
							}
							else
							{
								// String.Format the average into the display line
								repLine.Append(string.Format(culture, "{0,6:F1}", MonthList[m].avgwindspeed));
								totalavgwind += MonthList[m].avgwindspeed * MonthList[m].samples;
								avgwindcount += MonthList[m].samples;
							}

							// String.Format the high wind speed and dominant direction into the display line
							repLine.Append(string.Format(culture, "{0,6:F1}{1,5:D}", MonthList[m].highwindspeed, MonthList[m].highwindday));
							repLine.Append(string.Format("{0,6}", CompassPoint(MonthList[m].winddomdir)));

							// check for highest annual wind speed
							if (MonthList[m].highwindspeed > highwind)
							{
								highwind = MonthList[m].highwindspeed;
								highwindmonth = m;
							}

							// increment the total wind vectors for the annual calculation
							totalwinddirX += (MonthList[m].avgwindspeed * Math.Sin(Trig.DegToRad(domdir))) * MonthList[m].samples;
							totalwinddirY += (MonthList[m].avgwindspeed * Math.Cos(Trig.DegToRad(domdir))) * MonthList[m].samples;
						}
						output.Add(repLine.ToString());
					}
					catch (Exception e)
					{
						cumulus.LogMessage($"CreateYearlyReport: Error creating wind section: {e.Message}");
						cumulus.LogDebugMessage("CreateYearlyReport: Last line generated was...");
						cumulus.LogDebugMessage($"CreateYearlyReport: \"{repLine}\"");
						throw;
					}
				}

				output.Add("------------------------------");
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"CreateYearlyReport: Error creating the report: {e.Message}");
				cumulus.LogDebugMessage("CreateYearlyReport: Output generated so far was...");
				cumulus.LogDebugMessage(string.Join("\n",output));
				throw;
			}

			// wind section summary
			try
			{
				if (samples <= 0)
					return output;

				repLine.Clear();
				if (avgwindcount == 0)
					repLine.Append("        ----");
				else
					repLine.Append(string.Format(culture, "{0,12:F1}", totalavgwind / avgwindcount));

				repLine.Append(string.Format(culture, "{0,6:F1}", highwind));
				if (highwindmonth == 0)
				{
					repLine.Append(string.Format("{0,5}", "---"));
				}
				else
				{
					repLine.Append(string.Format("{0,5}", CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(highwindmonth)));
				}
			}
			catch (Exception e)
			{
				cumulus.LogMessage($"CreateYearlyReport: Error creating wind summary: {e.Message}");
				cumulus.LogDebugMessage("CreateYearlyReport: Last line generated was...");
				cumulus.LogDebugMessage($"CreateYearlyReport: \"{repLine}\"");
				throw;
			}

			try
			{
				domdir = CalcAvgBearing(totalwinddirX, totalwinddirY);

				if (domdir == 0)
					domdir = 360;
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error in NOAA dominant wind direction calculation: " + ex.Message);
				domdir = 0;
			}

			repLine.Append(string.Format("{0,6}", CompassPoint(domdir)));

			output.Add(repLine.ToString());
			return output;
		}
	}
}
