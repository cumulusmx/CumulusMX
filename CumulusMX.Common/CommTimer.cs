using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;

namespace CumulusMX.Common
{
	public class CommTimer : IDisposable
	{
		public Timer tmrComm = new Timer();
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
			tmrComm.Close();
			tmrComm.Dispose();
		}
	}
}
