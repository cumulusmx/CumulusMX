using System;
using System.Threading;

namespace CumulusMX
{
	internal class Simulator : WeatherStation
	{
		private bool stop;

		private readonly DataSet currData;

		private readonly int dataUpdateRate = 5000; // 5 second data update rate

		private new readonly Random random;
		private bool solarIntialised;

		public Simulator(Cumulus cumulus) : base(cumulus)
		{

			cumulus.LogMessage("Station type = Simulator");

			cumulus.LogMessage("Last update time = " + cumulus.LastUpdateTime);

			random = new Random();

			currData = new DataSet();

			cumulus.StationOptions.CalculatedDP = true;
			cumulus.StationOptions.CalculatedET = true;
			cumulus.StationOptions.CalculatedWC = true;
			cumulus.StationOptions.CalcuateAverageWindSpeed = true;
			cumulus.StationOptions.UseCumulusPresstrendstr = true;
			cumulus.StationOptions.UseSpeedForAvgCalc = false;

			WindAverage = 0;

			timerStartNeeded = true;
			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);

		}


		public override void Start()
		{
			while (!stop)
			{
				try
				{
					var now = DateTime.Now;

					currData.SetNewData(now);

					applyData(now);

					DoForecast(string.Empty, false);

					UpdateStatusPanel(now);
					UpdateMQTT();

					if (cumulus.cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(dataUpdateRate)))
					{
						break;
					}
				}
				catch (ThreadAbortException) // Catch the ThreadAbortException
				{
					cumulus.LogMessage("Simulator Start: ThreadAbortException");
					// and exit
					stop = true;
				}
				catch (Exception ex)
				{
					// any others, log them and carry on
					cumulus.LogErrorMessage("Simulator Start: Exception = " + ex.Message);
				}
			}

			cumulus.LogMessage("Ending normal reading loop");
		}

		public override void Stop()
		{
			StopMinuteTimer();

			cumulus.LogMessage("Stopping data generation task");
			try
			{
				cumulus.LogMessage("Waiting for data generation to complete");
			}
			catch (Exception ex)
			{
				cumulus.LogMessage("Error stopping the simulator" + ex.Message);
			}
		}


		private void applyData(DateTime recDate)
		{
			cumulus.LogDataMessage($"Simulated data: temp={ConvertUnits.TempCToUser(currData.tempVal):f1}, hum={currData.humVal}, gust={ConvertUnits.WindMPHToUser(currData.windSpeedVal):f2}, dir={currData.windBearingVal}, press={ConvertUnits.PressMBToUser(currData.pressureVal):f2}, r.rate={ConvertUnits.RainMMToUser(currData.rainRateVal):f2}");

			DoWind(ConvertUnits.WindMPHToUser(currData.windSpeedVal), currData.windBearingVal, -1, recDate);

			var rain = RainCounter + ConvertUnits.RainMMToUser(currData.rainRateVal * dataUpdateRate / 1000 / 3600);

			DoRain(rain, ConvertUnits.RainMMToUser(currData.rainRateVal), recDate);

			DoIndoorTemp(ConvertUnits.TempCToUser(currData.tempInVal));
			DoIndoorHumidity(currData.humInVal);

			DoOutdoorHumidity(currData.humVal, recDate);
			DoOutdoorTemp(ConvertUnits.TempCToUser(currData.tempVal), recDate);

			DoPressure(ConvertUnits.PressMBToUser(currData.pressureVal), recDate);
			UpdatePressureTrendString();

			doSolar(recDate);

			DoOutdoorDewpoint(-999, recDate);
			DoWindChill(-999, recDate);
			DoHumidex(recDate);
			DoApparentTemp(recDate);
			DoFeelsLike(recDate);
			DoCloudBaseHeatIndex(recDate);
		}

		private void doSolar(DateTime recDate)
		{
			// For the solar random walk we are chasing the theoretical solat max value
			double solar = SolarRad;

			// if we are starting up, set the intial solar rad value to 90% of theoretical
			if (!solarIntialised)
			{
				CurrentSolarMax = AstroLib.SolarMax(recDate, (double) cumulus.Longitude, (double) cumulus.Latitude, AltitudeM(cumulus.Altitude), out SolarElevation, cumulus.SolarOptions);
				solar = CurrentSolarMax * 0.9;
				solarIntialised = true;
			}

			// aim for 85% of theoretical in the morning, 75% after local noon
			double factor;
			if (recDate.IsDaylightSavingTime())
			{
				factor = recDate.Hour < 13 ? 0.85 : 0.75;
			}
			else
			{
				factor = recDate.Hour < 12 ? 0.85 : 0.75;
			}

			// If it's raining, make it dull!
			if (RainRate > 0)
			{
				factor = 0.3;
			}

			var volatility = CurrentSolarMax * 0.05;
			if (volatility < 2)
				volatility = 2;
			else if (volatility > 30)
				volatility = 30;

			solar -= (solar - CurrentSolarMax * factor) * 0.02;
			solar += volatility * (2 * random.NextDouble() - 1);
			if (solar < 0 || CurrentSolarMax == 0)
				solar = 0;
			DoSolarRad((int) solar, recDate);
		}


		private class DataSet
		{
			private readonly MeanRevertingRandomWalk temperature;
			private readonly MeanRevertingRandomWalk humidity;
			private readonly MeanRevertingRandomWalk windSpeed;
			private readonly MeanRevertingRandomWalk windDirection;
			private readonly MeanRevertingRandomWalk insideTemp;
			private readonly MeanRevertingRandomWalk insideHum;
			private readonly MeanRevertingRandomWalk pressure;
			private readonly MeanRevertingRandomWalk rainRate;

			public double tempVal { get; set; }
			public int humVal { get; set; }
			public double windSpeedVal { get; set; }
			public int windBearingVal { get; set; }
			public double rainRateVal { get; set; }
			public double pressureVal { get; set; }
			public double tempInVal { get; set; }
			public int humInVal { get; set; }


			public DataSet()
			{
				// Temperature - both annual and daily variations, daily offset by 0.1 of a day
				var tempMean = new Func<DateTime, double>((x) => 15 + 10 * Math.Cos(x.DayOfYear / 365.0 * 2 * Math.PI) - 10 * Math.Cos((x.TimeOfDay.TotalDays - 0.1) * 2 * Math.PI));
				// Wind - daily variation, offset by 0.1 of a day
				var windMean = new Func<DateTime, double>((x) => 10 - 9.5 * Math.Cos((x.TimeOfDay.TotalDays - 0.1) * 2 * Math.PI));
				var windVolatility = new Func<DateTime, double>((x) => 2 - 1.5 * Math.Cos((x.TimeOfDay.TotalDays - 0.1) * 2 * Math.PI));
				// Humidity - daily variation, offset by 0.1 of a day
				var humMean = new Func<DateTime, double>((x) => 60 + 30 * Math.Cos((x.TimeOfDay.TotalDays - 0.1) * 2 * Math.PI));
				// Pressure - vary the range over a two day period
				var pressMean = new Func<DateTime, double>((x) => 1010 + 25 * Math.Cos((x.DayOfYear + x.TimeOfDay.TotalDays + 0.2) % 4 / 4.0 * 2 * Math.PI) - 6 * Math.Cos((x.TimeOfDay.TotalDays + 0.65) * 2 * Math.PI));
				// Inside Temp - assume heating between 07:00 and 23:00
				var inTempMean = new Func<DateTime, double>((x) => (x.Hour < 7 || x.Hour > 22) ? 16 : 21);
				// RainRate - lets try two blocks of rain per day, determined by day of the year, mean rate to be 3 mm/hr
				var rainRateMean = new Func<DateTime, double>((x) => x.Hour == x.DayOfYear % 24 || x.Hour == (x.DayOfYear + 1) % 24 || x.Hour == (x.DayOfYear + 12) % 24 || x.Hour == (x.DayOfYear + 13) % 24 ? 3 : -100);

				temperature = new MeanRevertingRandomWalk(tempMean, (x) => 0.1, 0.01, -10, 30);
				humidity = new MeanRevertingRandomWalk(humMean, (x) => 1, 0.01, 10, 100);
				windSpeed = new MeanRevertingRandomWalk(windMean, windVolatility, 0.02, 0, 50);
				windDirection = new MeanRevertingRandomWalk((x) => 191, (x) => 10, 0.005, 0, 720);
				pressure = new MeanRevertingRandomWalk(pressMean, (x) => .1, 0.05, 950, 1050);
				rainRate = new MeanRevertingRandomWalk(rainRateMean, (x) => 5, 0.05, 0, 30);
				insideTemp = new MeanRevertingRandomWalk(inTempMean, (x) => 0.1, 0.01, 15, 25);
				insideHum = new MeanRevertingRandomWalk((x) => 50, (x) => 0.5, 0.005, 35, 75);
			}

			public void SetNewData(DateTime readTime)
			{
				tempVal = temperature.GetValue(readTime);
				humVal = (int) humidity.GetValue(readTime);
				windSpeedVal = Math.Round(windSpeed.GetValue(readTime), 1);
				if (windSpeedVal > 0)
				{
					windBearingVal = ((int) windDirection.GetValue(readTime) % 360) + 1;
				}
				rainRateVal = rainRate.GetValue(readTime);
				pressureVal = pressure.GetValue(readTime);
				tempInVal = insideTemp.GetValue(readTime);
				humInVal = (int) insideHum.GetValue(readTime);
			}
		}

		private class MeanRevertingRandomWalk
		{
			private readonly Func<DateTime, double> _meanCurve;
			private readonly Func<DateTime, double> _volatility;
			private readonly double _meanReversion;
			private readonly double _cropMin;
			private readonly double _cropMax;

			private double _value;
			private bool _initialised = false;
			private readonly Random _random;

			public MeanRevertingRandomWalk(Func<DateTime, double> meanCurve, Func<DateTime, double> volatility, double meanReversion, double cropMin, double cropMax)
			{
				_meanCurve = meanCurve;
				_volatility = volatility;
				_meanReversion = meanReversion;
				_cropMin = cropMin;
				_cropMax = cropMax;
				_random = new Random();
			}

			public double GetValue(DateTime date)
			{
				if (!_initialised)
				{
					_value = _meanCurve(date);
					_initialised = true;
				}


				_value -= (_value - _meanCurve(date)) * _meanReversion;
				_value += _volatility(date) * (2 * _random.NextDouble() - 1);
				if (_value < _cropMin) return _cropMin;
				if (_value > _cropMax) return _cropMax;
				return _value;
			}
		}
	}
}
