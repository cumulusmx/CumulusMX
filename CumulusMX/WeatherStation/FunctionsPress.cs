using System;
using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal partial class WeatherStation
	{
		public void DoPressure(double sl, DateTime timestamp)
		{
			// Spike removal is in user units
			if (previousPress < 9998 && Math.Abs(sl - previousPress) > cumulus.Spike.PressDiff)
			{
				cumulus.LogSpikeRemoval("Pressure difference greater than spike value; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={sl.ToString(cumulus.PressFormat)} OldVal={previousPress.ToString(cumulus.PressFormat)} SpikePressDiff={cumulus.Spike.PressDiff.ToString(cumulus.PressFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Pressure difference greater than spike value - NewVal={sl.ToString(cumulus.PressFormat)} OldVal={previousPress.ToString(cumulus.PressFormat)} SpikePressDiff={cumulus.Spike.PressDiff.ToString(cumulus.PressFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}
			else if (sl > cumulus.Limit.PressHigh)
			{
				cumulus.LogSpikeRemoval("Pressure greater than upper limit; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={sl.ToString(cumulus.PressFormat)} HighLimit={cumulus.Limit.PressHigh.ToString(cumulus.PressFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Pressure greater than upper limit - NewVal={sl.ToString(cumulus.PressFormat)} HighLimit={cumulus.Limit.PressHigh.ToString(cumulus.PressFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}
			else if (sl < cumulus.Limit.PressLow)
			{
				cumulus.LogSpikeRemoval("Pressure less than lower limit; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={sl.ToString(cumulus.PressFormat)} LowLimit={cumulus.Limit.PressLow.ToString(cumulus.PressFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Pressure less than lower limit - NewVal={sl.ToString(cumulus.PressFormat)} LowLimit={cumulus.Limit.PressLow.ToString(cumulus.PressFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
				return;
			}

			previousPress = sl;

			// If we calculate SLP, then the calibration is applied to the station pressure
			MetData.Pressure = cumulus.StationOptions.CalculateSLP ? sl : cumulus.Calib.Press.Calibrate(sl);

			first_press = false;

			if (MetData.Pressure > Records.AllTime.HighPress.Val)
			{
				SetAlltime(Records.AllTime.HighPress, MetData.Pressure, timestamp);
			}

			cumulus.HighPressAlarm.CheckAlarm(MetData.Pressure);

			if (MetData.Pressure < Records.AllTime.LowPress.Val)
			{
				SetAlltime(Records.AllTime.LowPress, MetData.Pressure, timestamp);
			}

			cumulus.LowPressAlarm.CheckAlarm(MetData.Pressure);
			CheckMonthlyAlltime("LowPress", MetData.Pressure, false, timestamp);
			CheckMonthlyAlltime("HighPress", MetData.Pressure, true, timestamp);

			if (MetData.Pressure > DailyHighLow.Today.HighPress)
			{
				DailyHighLow.Today.HighPress = MetData.Pressure;
				DailyHighLow.Today.HighPressTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.Pressure < DailyHighLow.Today.LowPress)
			{
				DailyHighLow.Today.LowPress = MetData.Pressure;
				DailyHighLow.Today.LowPressTime = timestamp;
				WriteTodayFile(timestamp, false);
			}

			if (MetData.Pressure > Records.ThisMonth.HighPress.Val)
			{
				Records.ThisMonth.HighPress.Val = MetData.Pressure;
				Records.ThisMonth.HighPress.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.Pressure < Records.ThisMonth.LowPress.Val)
			{
				Records.ThisMonth.LowPress.Val = MetData.Pressure;
				Records.ThisMonth.LowPress.Ts = timestamp;
				WriteMonthIniFile();
			}

			if (MetData.Pressure > Records.ThisYear.HighPress.Val)
			{
				Records.ThisYear.HighPress.Val = MetData.Pressure;
				Records.ThisYear.HighPress.Ts = timestamp;
				WriteYearIniFile();
			}

			if (MetData.Pressure < Records.ThisYear.LowPress.Val)
			{
				Records.ThisYear.LowPress.Val = MetData.Pressure;
				Records.ThisYear.LowPress.Ts = timestamp;
				WriteYearIniFile();
			}

			DoPressTrend("Enable Cumulus pressure trend");

			PressReadyToPlot = true;
			HaveReadData = true;
		}

		protected void DoPressTrend(string trend)
		{
			if (cumulus.StationOptions.UseCumulusPresstrendstr)
			{
				UpdatePressureTrendString();
			}
			else
			{
				MetData.PressTrendStr = trend;
			}
		}

		public void DoStationPressure(double sp)
		{
			// Spike removal is in user units
			if (previousPressStation < 9998 && Math.Abs(sp - previousPressStation) > cumulus.Spike.PressDiff)
			{
				cumulus.LogSpikeRemoval("Station Pressure difference greater than spike value; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={sp.ToString(cumulus.PressFormat)} OldVal={previousPressStation.ToString(cumulus.PressFormat)} SpikePressDiff={cumulus.Spike.PressDiff.ToString(cumulus.PressFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Station Pressure difference greater than spike value - NewVal={sp.ToString(cumulus.PressFormat)} OldVal={previousPressStation.ToString(cumulus.PressFormat)} SpikePressDiff={cumulus.Spike.PressDiff.ToString(cumulus.PressFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
			}
			else if (sp > cumulus.Limit.StationPressHigh)
			{
				cumulus.LogSpikeRemoval("Station Pressure greater than upper limit; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={sp.ToString(cumulus.PressFormat)} HighLimit={cumulus.Limit.StationPressHigh.ToString(cumulus.PressFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Station Pressure greater than upper limit - NewVal={sp.ToString(cumulus.PressFormat)} HighLimit={cumulus.Limit.PressHigh.ToString(cumulus.PressFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
			}
			else if (sp < cumulus.Limit.StationPressLow)
			{
				cumulus.LogSpikeRemoval("Station Pressure less than lower limit; reading ignored");
				cumulus.LogSpikeRemoval($"NewVal={sp.ToString(cumulus.PressFormat)} LowLimit={cumulus.Limit.PressLow.ToString(cumulus.PressFormat)}");
				lastSpikeRemoval = DateTime.Now;
				cumulus.SpikeAlarm.LastMessage = $"Station Pressure less than lower limit - NewVal={sp.ToString(cumulus.PressFormat)} LowLimit={cumulus.Limit.StationPressLow.ToString(cumulus.PressFormat)}";
				cumulus.SpikeAlarm.Triggered = true;
			}
			else
			{
				// all good!
				previousPressStation = sp;
				MetData.StationPressure = cumulus.Calib.PressStn.Calibrate(sp);
				MetData.AltimeterPressure = ConvertUnits.PressMBToUser(MeteoLib.StationToAltimeter(ConvertUnits.UserPressToHpa(MetData.StationPressure), ConvertUnits.AltitudeM(cumulus.Altitude)));
			}
		}


	}
}
