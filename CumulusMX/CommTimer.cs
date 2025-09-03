using System;
using System.Timers;

using Timer = System.Timers.Timer;

namespace CumulusMX
{
	internal abstract partial class WeatherStation
	{
		public class CommTimer : IDisposable
		{
			public Timer tmrComm = new();
			public bool timedout = false;
			public CommTimer()
			{
				timedout = false;
				tmrComm.AutoReset = false;
				tmrComm.Enabled = false;
				tmrComm.Interval = 1000; //default to 1 second
				tmrComm.Elapsed += new ElapsedEventHandler(OnTimedCommEvent);
			}

			public void OnTimedCommEvent(object source, ElapsedEventArgs e)
			{
				timedout = true;
				tmrComm.Stop();
			}

			public void Start(double timeoutperiod)
			{
				tmrComm.Interval = timeoutperiod;             //time to time out in milliseconds
				tmrComm.Stop();
				timedout = false;
				tmrComm.Start();
			}

			public void Stop()
			{
				tmrComm.Stop();
				timedout = true;
			}

			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			protected virtual void Dispose(bool disposing)
			{
				tmrComm.Close();
				tmrComm.Dispose();
			}
		}
	}
}
