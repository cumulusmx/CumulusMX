using System;
using System.Globalization;
using System.ServiceProcess;

namespace CumulusMX
{
	partial class CumulusService : ServiceBase
	{
		public CumulusService()
		{
			InitializeComponent();
			CanShutdown = true;
			CanHandlePowerEvent = true;
		}

		protected override void OnStart(string[] args)
		{
			int httpport = 8998;
			bool debug = false;
			string startParams = "";

			for (int i = 0; i < args.Length; i++)
			{
				startParams += args[i] + " ";
				try
				{
					if (args[i] == "-lang" && args.Length >= i)
					{
						var lang = args[++i];
						startParams += args[i] + " ";

						CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(lang);
						CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(lang);
					}
					else if (args[i] == "-port" && args.Length >= i)
					{
						httpport = Convert.ToInt32(args[++i]);
						startParams += args[i] + " ";
					}
					else if (args[i] == "-debug")
					{
						// Switch on debug and and data logging from the start
						debug = true;
					}
					else if (args[i] == "-wsport" && args.Length >= i)
					{
						i++;
						startParams += args[i] + " ";
					}
				}
				catch
				{}
			}

			Program.cumulus = new Cumulus(httpport, debug, startParams);
		}

		protected override void OnStop()
		{
			Program.cumulus.LogMessage("Shutting down due to SERVICE STOP");
			Program.cumulus.LogConsoleMessage("Shutting down due to SERVICE STOP");
			Program.cumulus.Stop();
			Program.exitSystem = true;
		}

		protected override void OnShutdown()
		{
			Program.cumulus.LogMessage("Shutting down due to SYSTEM SHUTDOWN");
			Program.cumulus.LogConsoleMessage("Shutting down due to SYSTEM SHUTDOWN");
			Program.cumulus.Stop();
			Program.exitSystem = true;
			base.OnShutdown();
		}


		protected override bool OnPowerEvent(PowerBroadcastStatus powerStatus)
		{
			switch (powerStatus)
			{
				case PowerBroadcastStatus.ResumeSuspend:
					Program.cumulus.LogMessage("Detected system RESUMING FROM STANDBY");
					Program.cumulus.LogConsoleMessage("Detected system RESUMING FROM STANDBY");
					break;
				case PowerBroadcastStatus.Suspend:
					Program.cumulus.LogMessage("Detected system GOING TO STANDBY");
					Program.cumulus.LogConsoleMessage("Detected system GOING TO STANDBY");
					break;
			}

			Stop();
			return true;
		}
	}
}
