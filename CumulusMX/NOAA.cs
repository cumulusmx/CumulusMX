using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CumulusMX
{
	internal class NOAA
	{
		public struct Tdaysummary
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

		public struct Tmonthsummary
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
		private bool LessThanOrEqual(double value1, double value2)
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
		private bool GreaterThanOrEqual(double value1, double value2)
		{
			int intvalue1 = Convert.ToInt32(value1*1000);
			int intvalue2 = Convert.ToInt32(value2*1000);
			return (intvalue1 >= intvalue2);
		}

		private double DegToRad(int degrees)
		{
			return degrees*(Math.PI/180.0);
		}

		private  string CompassPoint(int bearing)
		{
			return cumulus.compassp[(((bearing * 100) + 1125) % 36000) / 2250];
		}

		private double RadToDeg(double radians)
		{
			return radians*(180.0/Math.PI);
		}

		private double frac(double num)
		{
			return num - Math.Floor(num);
		}

		private void DecodeLatLong(Double latLong, out int deg, out int min, out int sec)
		{
			deg = (int) Math.Floor(latLong);
			latLong = frac(latLong) * 60;
			min = (int)Math.Floor(latLong);
			latLong = frac(latLong) * 60;
			sec = (int) Math.Round(latLong);
		}

		public double GetAverageWindSpeed(int month, int year, out int domdir)
		{
			string Line;
			string Date;
			int linenum = 0;
			int windsamples = 0;
			double windspeed;
			double totalwindspeed;
			double avgwindspeed;
			int winddir;
			double totalwinddirX;
			double totalwinddirY;
			totalwinddirX = 0;
			totalwinddirY = 0;
			totalwindspeed = 0;

			// Use the second of the month to allow for 9am rollover
			var LogFile = cumulus.GetLogFileName(new DateTime(year, month, 2));
			if (File.Exists(LogFile))
			{
				try
				{
					using (var sr = new StreamReader(LogFile))
					{
						linenum = 0;
						windsamples = 0;
						do
						{
							// now process each record in the file

							Line = sr.ReadLine();
							linenum ++;
							var st = new List<string>(Regex.Split(Line, CultureInfo.CurrentCulture.TextInfo.ListSeparator));
							Date = st[0];
							windspeed = Convert.ToSingle(st[5]);
							// add in wind speed sample for whole month
							windsamples ++;
							totalwindspeed += windspeed;
							// add in direction if not done already
							winddir = Convert.ToInt32(st[7]);
							totalwinddirX += (windspeed*Math.Sin((winddir*(Math.PI/180))));
							totalwinddirY += (windspeed*Math.Cos((winddir*(Math.PI/180))));
							//@ Unsupported property or method(C): 'EndOfStream'
						} while (!(sr.EndOfStream));
					}
				}
				catch (Exception e)
				{
					cumulus.LogMessage("Error at line " + linenum + " of " + LogFile + " : " + e.Message);
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
				domdir = calcavgbear(totalwinddirX, totalwinddirY);
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

		private int calcavgbear(double x, double y)
		{
			var avg = 90 - (int)(RadToDeg(Math.Atan2(y, x)));
			if (avg < 0)
			{
				avg = 360 + avg;
			}

			return avg;
		}

		public List<string> CreateMonthlyReport(DateTime thedate)
		{
			var output = new List<string>();

			Tdaysummary[] DayList = new Tdaysummary[32];

			for (int i = 1; i < 32; i++)
			{
				DayList[i].valid = false;
				DayList[i].totalwindspeed = 0;
				DayList[i].windsamples = 0;
				DayList[i].winddomdir = 0;
				DayList[i].totalwinddirX = 0;
				DayList[i].totalwinddirY = 0;
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
			string Line;

			string listSep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;

			try
			{
				using (var sr = new StreamReader(cumulus.DayFile))
				{
					do
					{
						Line = sr.ReadLine();
						linenum++;
						var st = new List<string>(Regex.Split(Line, listSep));
						string DateStr = st[0];

						if ((Convert.ToInt32(DateStr.Substring(3, 2)) == month) && (Convert.ToInt32(DateStr.Substring(6, 2))+2000 == year))
						{
							// entry is for this month (month and year match)
							int daynumber = Convert.ToInt32(DateStr.Substring(0, 2));

							if (DayList[daynumber].valid)
							{
								// already had this date - error!
								cumulus.LogMessage("Duplicate entry at line " + linenum + " of dayfile.txt: " + DateStr + ". Please correct this by editing the file");
							}
							else
							{
								// havent had this entry yet

								// mean temp
								if ((st.Count > 15) && (st[15] != ""))
								{
									double meantemp = Double.Parse(st[15]);
									totalmeantemp += meantemp;
									DayList[daynumber].meantemp = meantemp;

									// heating degree days
									if ((st.Count > 40) && (st[40] != ""))
									{
										// read hdd from dayfile.txt
										DayList[daynumber].heatingdegdays = Double.Parse(st[40]);
										totalheating += Double.Parse(st[40]);
									}
									else if (meantemp < cumulus.NOAAheatingthreshold)
									{
										DayList[daynumber].heatingdegdays = cumulus.NOAAheatingthreshold - meantemp;
										totalheating += cumulus.NOAAheatingthreshold - meantemp;
									}
									else
									{
										DayList[daynumber].heatingdegdays = 0;
									}

									// cooling degree days
									if ((st.Count > 41) && (st[41] != string.Empty))
									{
										// read hdd from dayfile.txt
										DayList[daynumber].coolingdegdays = Double.Parse(st[41]);
										totalcooling += Double.Parse(st[41]);
									}
									else if (meantemp > cumulus.NOAAcoolingthreshold)
									{
										DayList[daynumber].coolingdegdays = meantemp - cumulus.NOAAcoolingthreshold;
										totalcooling += meantemp - cumulus.NOAAcoolingthreshold;
									}
									else
									{
										DayList[daynumber].coolingdegdays = 0;
									}
								}
								else
								{
									// average temp field not present
									DayList[daynumber].meantemp = -1000;
									DayList[daynumber].heatingdegdays = 0;
									DayList[daynumber].coolingdegdays = 0;
								}

								// max temp
								DayList[daynumber].maxtemp = Double.Parse(st[6]);
								string timestr = st[7];
								int hour = Convert.ToInt32(timestr.Substring(0, 2));
								int minute = Convert.ToInt32(timestr.Substring(3, 2));
								DayList[daynumber].maxtemptimestamp = DateTime.MinValue.Date.Add(new TimeSpan(hour, minute, 0));
								if (DayList[daynumber].maxtemp > maxtemp)
								{
									maxtemp = DayList[daynumber].maxtemp;
									maxtempday = daynumber;
								}
								if (GreaterThanOrEqual(DayList[daynumber].maxtemp, cumulus.NOAAmaxtempcomp1))
								{
									maxtempcount1++;
								}
								if (LessThanOrEqual(DayList[daynumber].maxtemp, cumulus.NOAAmaxtempcomp2))
								{
									maxtempcount2++;
								}

								// min temp
								DayList[daynumber].mintemp = Double.Parse(st[4]);
								timestr = st[5];
								hour = Convert.ToInt32(timestr.Substring(0, 2));
								minute = Convert.ToInt32(timestr.Substring(3, 2));
								DayList[daynumber].mintemptimestamp = DateTime.MinValue.Date.Add(new TimeSpan(hour, minute, 0));
								if (DayList[daynumber].mintemp < mintemp)
								{
									mintemp = DayList[daynumber].mintemp;
									mintempday = daynumber;
								}
								if (LessThanOrEqual(DayList[daynumber].mintemp, cumulus.NOAAmintempcomp1))
								{
									mintempcount1++;
								}
								if (LessThanOrEqual(DayList[daynumber].mintemp, cumulus.NOAAmintempcomp2))
								{
									mintempcount2++;
								}

								// rain
								DayList[daynumber].rain = Double.Parse(st[14]);
								totalrain += Double.Parse(st[14]);
								if (DayList[daynumber].rain > maxrain)
								{
									maxrain = DayList[daynumber].rain;
									maxrainday = daynumber;
								}

								if (GreaterThanOrEqual(DayList[daynumber].rain, cumulus.NOAAraincomp1))
								{
									raincount1++;
								}
								if (GreaterThanOrEqual(DayList[daynumber].rain, cumulus.NOAAraincomp2))
								{
									raincount2++;
								}
								if (GreaterThanOrEqual(DayList[daynumber].rain, cumulus.NOAAraincomp3))
								{
									raincount3++;
								}

								// high wind speed
								DayList[daynumber].highwindspeed = Double.Parse(st[1]);
								timestr = st[3];
								hour = Convert.ToInt32(timestr.Substring(0, 2));
								minute = Convert.ToInt32(timestr.Substring(3, 2));
								DayList[daynumber].highwindtimestamp = DateTime.MinValue.Date.Add(new TimeSpan(hour, minute, 0));
								if (DayList[daynumber].highwindspeed > highwind)
								{
									highwind = DayList[daynumber].highwindspeed;
									highwindday = daynumber;
								}

								// dominant wind bearing
								if ((st.Count > 39) && (st[39] != string.Empty))
								{
									DayList[daynumber].winddomdir = Convert.ToInt32((st[39]));
								}

								daycount++;
								DayList[daynumber].valid = true;
							}
						}
					} while (!(sr.EndOfStream));
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error at line " + linenum + " of dayfile.txt: " + ex.Message);
				cumulus.LogMessage("Please edit the file to correct the error");
			}

			// Calculate average wind speed from log file
			// Use the second of the month in case of 9am rollover
			var LogFile = cumulus.GetLogFileName(new DateTime(thedate.Year, thedate.Month, 2));

			if (File.Exists(LogFile))
				try
				{
					using (var sr = new StreamReader(LogFile))
					{
						linenum = 0;

						// now process each record in the file
						do
						{
							Line = sr.ReadLine();
							linenum++;
							var st = new List<string>(Regex.Split(Line, listSep));

							int entryday = Convert.ToInt32(st[0].Substring(0, 2));
							int entrymonth = Convert.ToInt32(st[0].Substring(3, 2));
							int entryyear = Convert.ToInt32(st[0].Substring(6, 2));
							int entryhour = Convert.ToInt32(st[1].Substring(0, 2));
							int entryminute = Convert.ToInt32(st[1].Substring(3, 2));

							DateTime Entrydate = new DateTime(entryyear, entrymonth, entryday, entryhour, entryminute, 0);

							Entrydate = Entrydate.AddHours(cumulus.GetHourInc(Entrydate));

							int daynumber = Entrydate.Day;

							if (DayList[daynumber].valid)
							{
								double windspeed = Double.Parse(st[5]);

								// add in wind speed sample for this day
								DayList[daynumber].windsamples++;
								DayList[daynumber].totalwindspeed = DayList[daynumber].totalwindspeed + windspeed;

								// add in wind speed sample for whole month
								windsamples++;
								totalwindspeed += windspeed;

								// add in direction if (not done already
								if (DayList[daynumber].winddomdir == 0)
								{
									int winddir = Convert.ToInt32(st[7]);
									DayList[daynumber].totalwinddirX = DayList[daynumber].totalwinddirX + (windspeed*Math.Sin(DegToRad(winddir)));
									DayList[daynumber].totalwinddirY = DayList[daynumber].totalwinddirY + (windspeed*Math.Cos(DegToRad(winddir)));
								}
							}
						} while (!(sr.EndOfStream));
					}
				}
				catch (Exception ex)
				{
					cumulus.LogMessage("Error at line " + linenum + " of " + LogFile + " : " + ex.Message);
					cumulus.LogMessage("Please edit the file to correct the error");
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
				if (DayList[i].windsamples > 0)
					DayList[i].avgwindspeed = DayList[i].totalwindspeed/DayList[i].windsamples;
				else
					DayList[i].avgwindspeed = -1000;

				// calculate dominant wind bearing if (required
				if (DayList[i].winddomdir == 0)
				{
					if (DayList[i].totalwinddirX == 0)
						DayList[i].winddomdir = 0;
					else
					{
						try
						{
							DayList[i].winddomdir = calcavgbear(DayList[i].totalwinddirX, DayList[i].totalwinddirY);// 90 - (int)Math.Floor(RadToDeg(Math.Atan2(DayList[i].totalwinddirY, DayList[i].totalwinddirX)));
								//(int)Math.Floor(RadToDeg(Math.Atan(DayList[i].totalwinddirY / DayList[i].totalwinddirX)));
						}
						catch
						{
							cumulus.LogMessage("Error in NOAA dominant wind direction calculation ");
						}

						if (DayList[i].winddomdir == 0)
						{
							DayList[i].winddomdir = 360;
						}
					}
				}

				// add up vectors for overall dom dir
				if (DayList[i].windsamples > 0)
					// there"s an average speed available
				{
					totalwinddirX += (DayList[i].avgwindspeed*Math.Sin(DegToRad(DayList[i].winddomdir)));
					totalwinddirY += (DayList[i].avgwindspeed*Math.Cos(DegToRad(DayList[i].winddomdir)));
				}
			}

			int overalldomdir;
			try
			{
				overalldomdir = calcavgbear(totalwinddirX, totalwinddirY);

				if (overalldomdir == 0)
					overalldomdir = 360;
			}
			catch
			{
				cumulus.LogMessage("Error in NOAA dominant wind direction calculation ");
				overalldomdir = 0;
			}

			// Now output everything

			output.Add("                   Monthly Climatological Summary for " + thedate.ToString("MMM") + " " + year);
			output.Add("");
			output.Add("Name: " + cumulus.NOAAname + "   City: " + cumulus.NOAAcity + "   State: " + cumulus.NOAAstate);
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

			string lathem;
			if (cumulus.Latitude > 0)
			{
				lathem = "N";
			}
			else
			{
				lathem = "S";
			}
			string lonhem;
			if (cumulus.Longitude > 0)
			{
				lonhem = "E";
			}
			else
			{
				lonhem = "W";
			}

			latdeg = Math.Abs(latdeg);
			londeg = Math.Abs(londeg);

			output.Add("Elevation: " + elev + "  Lat: " + String.Format("{0} {1,2:D2} {2,2:D2} {3,2:D2}", lathem, latdeg, latmin, latsec) + "   Lon: " +
					   String.Format("{0} {1,3:D3} {2,2:D2} {3,2:D2}", lonhem, londeg, lonmin, lonsec));
			output.Add("");
			output.Add("                  Temperature (" + cumulus.TempUnitText + "), Rain (" + cumulus.RainUnitText + "), Wind Speed (" + cumulus.WindUnitText + ")");
			output.Add("");
			output.Add("                                      Heat  Cool        Avg");
			output.Add("    Mean                              Deg   Deg         Wind                 Dom");
			output.Add("Day Temp  High   Time   Low    Time   Days  Days  Rain  Speed High   Time    Dir");
			output.Add("----------------------------------------------------------------------------------");

			for (int i = 1; i <= DateTime.DaysInMonth(year, month); i++)
			{
				if (DayList[i].valid)
				{
					Line = i.ToString("D2");
					if (DayList[i].meantemp < -999)
					{
						Line += "  ----";
					}
					else
					{
						Line += String.Format("{0,6:F1}",DayList[i].meantemp);
					}
					;
					Line += String.Format("{0,6:F1}", DayList[i].maxtemp);
					string timestr;
					if (cumulus.NOAA12hourformat)
					{
						timestr = DayList[i].maxtemptimestamp.ToString("h:mmtt");
					}
					else
					{
						timestr = DayList[i].maxtemptimestamp.ToString("HH:mm");
					}
					Line += String.Format("{0,8}", timestr);
					Line += String.Format("{0,6:F1}",DayList[i].mintemp);
					if (cumulus.NOAA12hourformat)
					{
						timestr = DayList[i].mintemptimestamp.ToString("h:mmtt");
					}
					else
					{
						timestr = DayList[i].mintemptimestamp.ToString("HH:mm");
					}
					Line += String.Format("{0,8}", timestr);

					if (DayList[i].meantemp < -999)
					{
						Line += "  ----";
					}
					else
					{
						Line += String.Format("{0,6:F1}", DayList[i].heatingdegdays);
						Line += String.Format("{0,6:F1}", DayList[i].coolingdegdays);
					}
					Line += String.Format("{0,6}", DayList[i].rain.ToString(cumulus.RainFormat));

					if (DayList[i].avgwindspeed < -999)
						Line += "  ----";
					else
						Line += String.Format("{0,6:F1}", DayList[i].avgwindspeed);

					Line += String.Format("{0,6:F1}", DayList[i].highwindspeed);
					if (cumulus.NOAA12hourformat)
					{
						timestr = DayList[i].highwindtimestamp.ToString("h:mmtt");
					}
					else
					{
						timestr = DayList[i].highwindtimestamp.ToString("HH:mm");
					}
					Line += String.Format("{0,8}", timestr);
					Line += String.Format("{0,6}", CompassPoint(DayList[i].winddomdir));
					output.Add(Line);
				}
			}
			output.Add("----------------------------------------------------------------------------------");

			// Build summary line
			if (daycount == 0)
			{
				Line = ("    ----");
			}
			else
			{
				Line = String.Format("{0,8:F1}", totalmeantemp/daycount);
			}

			if (maxtempday == 0)
			{
				Line += "  ----    --";
			}
			else
			{
				Line += String.Format("{0,6:F1}", maxtemp);
				Line += String.Format("{0,6:D}", maxtempday);
			}

			if (mintempday == 0)
			{
				Line += "    ----    --";
			}
			else
			{
				Line += String.Format("{0,8:F1}", mintemp);
				Line += String.Format("{0,6:D}", mintempday);
			}

			Line += String.Format("{0,8:F1}", totalheating);
			Line += String.Format("{0,6:F1}", totalcooling);

			Line += String.Format("{0,6}", totalrain.ToString(cumulus.RainFormat));

			if (avgwindspeed < -999)
			{
				Line += "  ----";
			}
			else
			{
				Line += String.Format("{0,6:F1}", avgwindspeed);
			}
			;

			Line += String.Format("{0,6:F1}", highwind);
			Line += String.Format("{0,6:D}", highwindday);

			Line += String.Format("{0,8}", CompassPoint(overalldomdir));

			output.Add(Line);

			output.Add("");

			// now do the max/min/days of rain items
			output.Add(String.Format("Max >={0,6:F1}{1,3:D}", cumulus.NOAAmaxtempcomp1, maxtempcount1));
			output.Add(String.Format("Max <={0,6:F1}{1,3:D}", cumulus.NOAAmaxtempcomp2, maxtempcount2));
			output.Add(String.Format("Min <={0,6:F1}{1,3:D}", cumulus.NOAAmintempcomp1, mintempcount1));
			output.Add(String.Format("Min <={0,6:F1}{1,3:D}", cumulus.NOAAmintempcomp2, mintempcount2));

			output.Add("Max Rain: " + maxrain.ToString(cumulus.RainFormat) + " on day " + maxrainday);

			output.Add("Days of Rain: " + raincount1 + " (>= " + cumulus.NOAAraincomp1.ToString(cumulus.RainFormat) + " " + cumulus.RainUnitText + ")  " + raincount2 + " (>= " +
					   cumulus.NOAAraincomp2.ToString(cumulus.RainFormat) + " " + cumulus.RainUnitText + ")  " + raincount3 + " (>= " +
					   cumulus.NOAAraincomp3.ToString(cumulus.RainFormat) + " " + cumulus.RainUnitText + ")");
			output.Add("Heat Base: " + cumulus.NOAAheatingthreshold.ToString(cumulus.TempFormat) + "  Cool Base: " + cumulus.NOAAcoolingthreshold.ToString(cumulus.TempFormat) +
					   "  Method: Integration");

			return output;
		}

		public List<string> CreateYearlyReport(DateTime thedate)
		{
			var output = new List<string>();

			string listSep = CultureInfo.CurrentCulture.TextInfo.ListSeparator;
			string Line;
			int linenum = 0;

			double[] TempNorms = new double[13];
			double[] RainNorms = new double[13];
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

			// set up norms
			TempNorms[1] = cumulus.NOAATempNormJan;
			TempNorms[2] = cumulus.NOAATempNormFeb;
			TempNorms[3] = cumulus.NOAATempNormMar;
			TempNorms[4] = cumulus.NOAATempNormApr;
			TempNorms[5] = cumulus.NOAATempNormMay;
			TempNorms[6] = cumulus.NOAATempNormJun;
			TempNorms[7] = cumulus.NOAATempNormJul;
			TempNorms[8] = cumulus.NOAATempNormAug;
			TempNorms[9] = cumulus.NOAATempNormSep;
			TempNorms[10] = cumulus.NOAATempNormOct;
			TempNorms[11] = cumulus.NOAATempNormNov;
			TempNorms[12] = cumulus.NOAATempNormDec;
			RainNorms[1] = cumulus.NOAARainNormJan;
			RainNorms[2] = cumulus.NOAARainNormFeb;
			RainNorms[3] = cumulus.NOAARainNormMar;
			RainNorms[4] = cumulus.NOAARainNormApr;
			RainNorms[5] = cumulus.NOAARainNormMay;
			RainNorms[6] = cumulus.NOAARainNormJun;
			RainNorms[7] = cumulus.NOAARainNormJul;
			RainNorms[8] = cumulus.NOAARainNormAug;
			RainNorms[9] = cumulus.NOAARainNormSep;
			RainNorms[10] = cumulus.NOAARainNormOct;
			RainNorms[11] = cumulus.NOAARainNormNov;
			RainNorms[12] = cumulus.NOAARainNormDec;

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
				using (var sr = new StreamReader(cumulus.DayFile))
				{
					do
					{
						Line = sr.ReadLine();
						linenum++;
						var st = new List<string>(Regex.Split(Line, listSep));
						string DateStr = st[0];

						if (Convert.ToInt32(DateStr.Substring(6, 2))+2000 == year)
						{
							// entry is for this year
							var day = Convert.ToInt32(DateStr.Substring(0, 2));
							month = Convert.ToInt32(DateStr.Substring(3, 2));
							MonthList[month].valid = true;
							MonthList[month].samples ++;
							var meantemp = Convert.ToDouble(st[15]);
							MonthList[month].totaltemp += meantemp;
							var maxval = Convert.ToDouble(st[6]);
							var minval = Convert.ToDouble(st[4]);
							MonthList[month].totalmaxtemp += maxval;
							MonthList[month].totalmintemp += minval;
							// Max temp?
							if (maxval > MonthList[month].maxtemp)
							{
								MonthList[month].maxtemp = maxval;
								MonthList[month].maxtempday = day;
							}
							if (GreaterThanOrEqual(maxval, cumulus.NOAAmaxtempcomp1))
							{
								MonthList[month].maxtempcount1 ++;
							}
							if (LessThanOrEqual(maxval, cumulus.NOAAmaxtempcomp2))
							{
								MonthList[month].maxtempcount2 ++;
							}
							// Min temp?
							if (minval < MonthList[month].mintemp)
							{
								MonthList[month].mintemp = minval;
								MonthList[month].mintempday = day;
							}
							if (LessThanOrEqual(minval, cumulus.NOAAmintempcomp1))
							{
								MonthList[month].mintempcount1 ++;
							}
							if (LessThanOrEqual(minval, cumulus.NOAAmintempcomp2))
							{
								MonthList[month].mintempcount2 ++;
							}
							// heating degree days
							if ((st.Count > 40) && (st[40] != ""))
							{
								// read hdd from dayfile.txt
								MonthList[month].heatingdegdays = MonthList[month].heatingdegdays + Convert.ToDouble(st[40]);
								totalheating += Convert.ToDouble(st[40]);
							}
							else if (meantemp < cumulus.NOAAheatingthreshold)
							{
								MonthList[month].heatingdegdays = MonthList[month].heatingdegdays + cumulus.NOAAheatingthreshold - meantemp;
								totalheating += cumulus.NOAAheatingthreshold - meantemp;
							}
							// cooling degree days
							if ((st.Count > 41) && (st[41] != ""))
							{
								// read hdd from dayfile.txt
								MonthList[month].coolingdegdays = MonthList[month].coolingdegdays + Convert.ToDouble(st[41]);
								totalcooling += Convert.ToDouble(st[41]);
							}
							else if (meantemp > cumulus.NOAAcoolingthreshold)
							{
								MonthList[month].coolingdegdays = MonthList[month].coolingdegdays + meantemp - cumulus.NOAAcoolingthreshold;
								totalcooling += meantemp - cumulus.NOAAcoolingthreshold;
							}
							// Rain days
							var rainvalue = Convert.ToDouble(st[14]);
							MonthList[month].totrain = MonthList[month].totrain + rainvalue;
							if (GreaterThanOrEqual(rainvalue, cumulus.NOAAraincomp1))
							{
								MonthList[month].raincount1 ++;
							}
							if (GreaterThanOrEqual(rainvalue, cumulus.NOAAraincomp2))
							{
								MonthList[month].raincount2 ++;
							}
							if (GreaterThanOrEqual(rainvalue, cumulus.NOAAraincomp3))
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
						}
					} while (!(sr.EndOfStream));
				}
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error at line " + linenum + " of dayfile.txt: " + ex.Message);
				cumulus.LogMessage("Please edit the file to correct the error");
			}

			// Now output everything
			output.Clear();
			output.Add("                   Annual Climatological Summary for " + year);
			output.Add("");
			output.Add("Name: " + cumulus.NOAAname + "   City: " + cumulus.NOAAcity + "   State: " + cumulus.NOAAstate);
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

			string lathem;
			if (cumulus.Latitude > 0)
			{
				lathem = "N";
			}
			else
			{
				lathem = "S";
			}
			string lonhem;
			if (cumulus.Longitude > 0)
			{
				lonhem = "E";
			}
			else
			{
				lonhem = "W";
			}

			latdeg = Math.Abs(latdeg);
			londeg = Math.Abs(londeg);

			output.Add("Elevation: " + elev + "  Lat: " + String.Format("{0} {1,2:D2} {2,2:D2} {3,2:D2}", lathem, latdeg, latmin, latsec) + "   Lon: " +
					   String.Format("{0} {1,3:D3} {2,2:D2} {3,2:D2}", lonhem, londeg, lonmin, lonsec));
			output.Add("");
			output.Add("                  Temperature (" + cumulus.TempUnitText + "), Heat Base: " + cumulus.NOAAheatingthreshold.ToString(cumulus.TempFormat) + "  Cool Base: " + cumulus.NOAAcoolingthreshold.ToString(cumulus.TempFormat));
			output.Add("                          Dep.  Heat  Cool                       Max  Max  Min  Min");
			output.Add("        Mean  Mean        From  Deg   Deg                        >=   <=   <=   <=");
			//@ Unsupported function or procedure: 'Format'
			output.Add(" YR MO  Max   Min   Mean  Norm  Days  Days  Hi  Date  Low  Date" +
					   String.Format("{0,5:F1}{1,5:F1}{2,5:F1}{3,6:F1}", cumulus.NOAAmaxtempcomp1, cumulus.NOAAmaxtempcomp2, cumulus.NOAAmintempcomp1, cumulus.NOAAmintempcomp2));
			output.Add("------------------------------------------------------------------------------------");
			for (month = 1; month <= 12; month++)
			{
				Line = String.Format("{0,3}{1,3:D}", twodigityear, month);
				if (MonthList[month].valid)
				{
					if (MonthList[month].samples == 0)
					{
						Line += "  ----  ----  ---";
					}
					else
					{
						MonthList[month].meanmaxtemp = MonthList[month].totalmaxtemp/MonthList[month].samples;
						MonthList[month].meanmintemp = MonthList[month].totalmintemp/MonthList[month].samples;
						MonthList[month].meantemp = MonthList[month].totaltemp/MonthList[month].samples;
						Line += String.Format("{0,6:F1}{1,6:F1}{2,6:F1}", MonthList[month].meanmaxtemp, MonthList[month].meanmintemp, MonthList[month].meantemp);
					}
					if (TempNorms[month] < -999)
					{
						// dummy value for 'departure from norm'
						Line += "   0.0";
					}
					else
					{
						Line += String.Format("{0,6}", (MonthList[month].meantemp - TempNorms[month]).ToString(cumulus.TempFormat));
						totalnormtemp += TempNorms[month];
						normtempsamples++;
					}
					Line += String.Format("{0,6:D}{1,6:D}", Convert.ToInt64(MonthList[month].heatingdegdays), Convert.ToInt64(MonthList[month].coolingdegdays));
					Line += String.Format("{0,6:F1}{1,4:D}{2,6:F1}{3,5:D}", MonthList[month].maxtemp, MonthList[month].maxtempday, MonthList[month].mintemp,
						MonthList[month].mintempday);
					Line += String.Format("{0,5:D}{1,5:D}{2,5:D}{3,5:D}", MonthList[month].maxtempcount1, MonthList[month].maxtempcount2, MonthList[month].mintempcount1,
						MonthList[month].mintempcount2);
				}
				output.Add(Line);
			}
			output.Add("------------------------------------------------------------------------------------");

			// now do the summary

			for (m = 1; m < 13; m++)
			{
				if (MonthList[m].valid)
				{
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
			}

			if (samples > 0)
			{
				double meanmax = totalmeanmaxtemp/samples;
				double meanmin = totalmeanmintemp/samples;
				double meantemp = totalmeantemp/samples;
				Line = String.Format("{0,12:F1}{1,6:F1}{2,6:F1}", meanmax, meanmin, meantemp);
				if (normtempsamples == 0)
					// dummy value for "departure from norm"
					Line += "   0.0";
				else
				{
					Line += String.Format("{0,6}", (meantemp - (totalnormtemp/normtempsamples)).ToString(cumulus.TempFormat));
				}
				Line += String.Format("{0,6:D}{1,6:D}", (int) (totalheating), (int) (totalcooling));
				if (maxtempmonth == 0)
				{
					Line += String.Format("{0,6:F1}{1,4}", maxtemp, "---");
				}
				else
				{
					Line += String.Format("{0,6:F1}{1,4}", maxtemp, CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(maxtempmonth));
				}
				if (mintempmonth == 0)
					Line += String.Format("{0,6:F1}{1,5}", mintemp, "---");
				else
				{
					Line += String.Format("{0,6:F1}{1,5}", mintemp, CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(mintempmonth));
				}
				Line += String.Format("{0,5:D}{1,5:D}{2,5:D}{3,5:D}", maxtempcount1, maxtempcount2, mintempcount1, mintempcount2);
				output.Add(Line);
			}
			else
			{
				output.Add("");
			}

			// Rain section header
			output.Add("");
			output.Add("                                Precipitation (" + cumulus.RainUnitText + ")");
			output.Add("");
			output.Add("              Dep.   Max        Days of Rain");
			output.Add("              From   Obs.           >=");
			output.Add(" YR MO Total  Norm   Day Date" +
					   String.Format("{0,5}{1,5}{2,5}", cumulus.NOAAraincomp1.ToString(cumulus.RainFormat), cumulus.NOAAraincomp2.ToString(cumulus.RainFormat),
						   cumulus.NOAAraincomp3.ToString(cumulus.RainFormat)));
			output.Add("---------------------------------------------");

			// Rain section details
			for (m = 1; m < 13; m++)
			{
				Line = String.Format("{0,3}{1,3:D}", twodigityear, m);

				if (MonthList[m].valid)
				{
					Line += String.Format("{0,6}", MonthList[m].totrain.ToString(cumulus.RainFormat));
					totalrain += MonthList[m].totrain;

					if (MonthList[m].maxrain > maxrain)
					{
						maxrain = MonthList[m].maxrain;
						maxrainmonth = m;
					}

					if (RainNorms[m] < -999)
						// dummy value for "departure from norm"
						Line += "   0.0";
					else
					{
						Line += String.Format("{0,6}", (MonthList[m].totrain - RainNorms[m]).ToString(cumulus.RainFormat));
						totalnormrain += RainNorms[m];
					}

					Line += String.Format("{0,6}", MonthList[m].maxrain.ToString(cumulus.RainFormat));
					Line += String.Format("{0,4:D}", MonthList[m].maxrainday);
					Line += String.Format("{0,6:D}{1,5:D}{2,5:D}", MonthList[m].raincount1, MonthList[m].raincount2, MonthList[m].raincount3);

					raincount1 += MonthList[m].raincount1;
					raincount2 += MonthList[m].raincount2;
					raincount3 += MonthList[m].raincount3;
					{
						output.Add(Line);
					}
				}
			}

			output.Add("---------------------------------------------");

			// rain summary
			if (samples > 0)
			{
				Line = String.Format("{0,12}", totalrain.ToString(cumulus.RainFormat));

				if (totalnormrain == 0)
				{
					// dummy value for "departure from norm"
					Line += "   0.0";
				}
				else
				{
					Line += String.Format("{0,6}", (totalrain - totalnormrain).ToString(cumulus.RainFormat));
				}

				Line += String.Format("{0,6}", maxrain.ToString(cumulus.RainFormat));
				if (maxrainmonth == 0)
				{
					Line += String.Format("{0,5}", "---");
				}
				else
				{
					Line += String.Format("{0,5}", CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(maxrainmonth));
				}

				Line += String.Format("{0,5:D}{1,5:D}{2,5:D}", raincount1, raincount2, raincount3);

				output.Add(Line);
			}
			else
			{
				output.Add("");
			}

			output.Add("");
			output.Add("                                Wind Speed (" + cumulus.WindUnitText + ")");
			output.Add("                          Dom");
			output.Add(" YR MO   Avg.  Hi   Date  Dir");
			output.Add("------------------------------");

			// Wind section details
			for (m = 1; m < 13; m++)
			{
				Line = String.Format("{0,3}{1,3:D}", twodigityear, m);

				if (MonthList[m].valid)
				{
					// calculate average wind speed
					MonthList[m].avgwindspeed = GetAverageWindSpeed(m, year, out domdir);
					MonthList[m].winddomdir = domdir;
					if (MonthList[m].avgwindspeed < 0)
					{
						// no valid average
						Line += "  ----";
					}
					else
					{
						// String.Format the average into the display line
						Line += String.Format("{0,6:F1}", MonthList[m].avgwindspeed);
						totalavgwind += MonthList[m].avgwindspeed * MonthList[m].samples;
						avgwindcount += MonthList[m].samples;
					}

					// String.Format the high wind speed and dominant direction into the display line
					Line += String.Format("{0,6:F1}{1,5:D}", MonthList[m].highwindspeed, MonthList[m].highwindday);
					Line += String.Format("{0,6}", CompassPoint(MonthList[m].winddomdir));

					// check for highest annual wind speed
					if (MonthList[m].highwindspeed > highwind)
					{
						highwind = MonthList[m].highwindspeed;
						highwindmonth = m;
					}

					// increment the total wind vectors for the annual calculation
					totalwinddirX += (MonthList[m].avgwindspeed*Math.Sin(DegToRad(domdir))) * MonthList[m].samples;
					totalwinddirY += (MonthList[m].avgwindspeed*Math.Cos(DegToRad(domdir))) * MonthList[m].samples;
				}
				output.Add(Line);
			}

			output.Add("------------------------------");

			// wind section summary
			if (samples > 0)
			{
				if (avgwindcount == 0)
					Line = "        ----";
				else
					Line = String.Format("{0,12:F1}", totalavgwind / avgwindcount);

				Line += String.Format("{0,6:F1}", highwind);
				if (highwindmonth == 0)
				{
					Line += String.Format("{0,5}", "---");
				}
				else
				{
					Line += String.Format("{0,5}", CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(highwindmonth));
				}

				try
				{
					domdir = calcavgbear(totalwinddirX, totalwinddirY);

					if (domdir == 0)
						domdir = 360;
				}
				catch (Exception ex)

				{
					cumulus.LogMessage("Error in NOAA dominant wind direction calculation: " + ex.Message);
					domdir = 0;
				}

				Line += String.Format("{0,6}", CompassPoint(domdir));

				output.Add(Line);
			}
			return output;
		}
	}
}
