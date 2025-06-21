using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32;

//#error version

namespace CumulusMX
{
	internal static class Program
	{
		public static Cumulus cumulus { get; set; }
		public static bool exitSystem { get; set; } = false;
		public static bool service { get; set; } = false;
		public static TextWriterTraceListener svcTextListener { get; set; }
		public const string AppGuid = "57190d2e-7e45-4efb-8c09-06a176cef3f3";
		public static DateTime StartTime { get; set; }
		public static byte[] InstanceId { get; set; }
		public static int Httpport { get; set; } = 8998;

		private static bool RunningOnWindows;
		public static bool debug { get; set; } = false;

		public static Random RandGenerator { get; } = new Random();

		private static async Task Main(string[] args)
		{
			StartTime = DateTime.Now;
			RunningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

			// force the current folder to be CumulusMX folder
			Directory.SetCurrentDirectory(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));

			try
			{
				if (!Directory.Exists("MXdiags"))
				{
					Directory.CreateDirectory("MXdiags");
				}
			}
			catch (UnauthorizedAccessException)
			{
				Console.WriteLine("Error, no permission to read/create folder /MXdiags");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error while attempting to read/create folder /MXdiags, error message: {ex.Message}");
			}


			var logfile = "MXdiags" + Path.DirectorySeparatorChar + "ServiceConsoleLog.txt";
			var logfileOld = "MXdiags" + Path.DirectorySeparatorChar + "ServiceConsoleLog-Old.txt";
			try
			{
				if (File.Exists(logfileOld))
					File.Delete(logfileOld);

				if (File.Exists(logfile))
					File.Move(logfile, logfileOld);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Failed to roll-over the Console log file: " + ex.Message);
			}

			svcTextListener = new TextWriterTraceListener(logfile);
			svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Starting on " + (RunningOnWindows ? "Windows" : "Linux"));
			svcTextListener.Flush();

			// Add an exit handler
			AppDomain.CurrentDomain.ProcessExit += (s, e) =>
			{
				if (cumulus != null && Environment.ExitCode != 999)
				{
					Cumulus.LogConsoleMessage("Cumulus terminating", ConsoleColor.Red);
					cumulus.LogMessage("Cumulus terminating");
					cumulus.Stop();
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus has shutdown");
					svcTextListener.Flush();
					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.WriteLine("Cumulus stopped");
					Console.ResetColor();
					exitSystem = true;
				}
				else
				{
					Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus terminating");
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus terminating");
				}

				svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Exit code = " + Environment.ExitCode);

				if (!service)
				{
					Console.CursorVisible = true;
				}
			};


			// Now we need to catch the console Ctrl-C
			Console.CancelKeyPress += (s, ev) =>
			{
				if (cumulus != null)
				{
					Cumulus.LogConsoleMessage("Ctrl+C pressed", ConsoleColor.Red);
					cumulus.LogMessage("Ctrl + C pressed");
					cumulus.Stop();
					//allow main to run off
					Thread.Sleep(500);
				}
				else
				{
					Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Ctrl+C pressed");
				}
				Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus has shutdown");
				ev.Cancel = true;
				exitSystem = true;
				if (!service)
				{
					Console.CursorVisible = true;
				}
			};

			AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

#if DEBUG
			debug = true;
#endif

			var install = false;
			var uninstall = false;
			var user = string.Empty;
			var group = string.Empty;
			var lang = string.Empty;
			var servicename = string.Empty;

			try
			{
				int i = 0;
				while (i < args.Length)
				{
					switch (args[i])
					{
						case "-lang" when args.Length >= i:
							{
								lang = args[++i];
								// some people enter the code as eg en_GB, it should use dash en-GB
								lang = lang.Replace('_', '-');

								CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(lang);
								CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(lang);
								Thread.CurrentThread.CurrentCulture = new CultureInfo(lang);
								CultureInfo.CurrentCulture = new CultureInfo(lang);
								CultureInfo.CurrentUICulture = new CultureInfo(lang);
								break;
							}
						case "-port" when args.Length >= i:
							Httpport = Convert.ToInt32(args[++i]);
							break;
						case "-debug":
							// Switch on debug and data logging from the start
							debug = true;
							break;
						case "-wsport":
							i++;
							Console.WriteLine("The use of the -wsport command line parameter is deprecated");
							svcTextListener.WriteLine("The use of the -wsport command line parameter is deprecated");
							break;
						case "-install":
							install = true;
							break;
						case "-uninstall":
							uninstall = true;
							break;
						case "-user" when args.Length >= i:
							user = args[++i];
							break;
						case "-group" when args.Length >= i:
							group = args[++i];
							break;
						case "-service":
							service = true;
							break;
						case "-servicename":
							servicename = args[++i];
							break;
						default:
							Console.WriteLine($"Invalid command line argument \"{args[i]}\"");
							svcTextListener.WriteLine($"Invalid command line argument \"{args[i]}\"");
							Usage();
							break;
					}

					i++;
				}
			}
			catch
			{
				Usage();
			}


			// we want to install as a service?
			if (install)
			{
				if (RunningOnWindows)
				{
					if (SelfInstaller.InstallWin())
					{
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("\nCumulus MX is now installed to run as service\n");
						Console.ResetColor();
						Environment.Exit(0);
					}
				}
				else
				{
					if (string.IsNullOrEmpty(user))
					{
						Console.ForegroundColor = ConsoleColor.Yellow;
						Console.WriteLine("\nYou must supply a user name when installing the service\n");
						Console.ResetColor();
						Environment.Exit(0);

					}

					if (SelfInstaller.InstallLinux(user, group, lang, Httpport, servicename))
					{
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("\nCumulus MX is now installed to run as service\n");
						Console.ResetColor();
						Environment.Exit(0);
					}
				}

				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("\nCumulus MX failed to install as service\n");
				Console.ResetColor();
				Environment.Exit(1);
			}

			// we want to uninstall the service?
			if (uninstall)
			{
				if (RunningOnWindows)
				{
					if (SelfInstaller.UninstallWin())
					{
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("\nCumulus MX is no longer installed to run as service\n");
						Console.ResetColor();
						Environment.Exit(0);
					}
				}
				else
				{
					if (SelfInstaller.UninstallLinux(servicename))
					{
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("\nCumulus MX is no longer installed to run as service\n");
						Console.ResetColor();
						Environment.Exit(0);
					}
				}

				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("\nCumulus MX failed uninstall itself as service\n");
				Console.ResetColor();
				Environment.Exit(1);
			}

			// detect system sleeping/hibernating - Windows only
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				// Windows 10 or later uses Modern Standby
				if (Environment.OSVersion.Version.Major >= 10)
				{
					IntPtr registrationHandle = new nint();
					DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient = new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS();
					recipient.Callback = DrviceNotifyCallBack;
					recipient.Context = IntPtr.Zero;

					IntPtr pRecipient = Marshal.AllocHGlobal(Marshal.SizeOf(recipient));
					Marshal.StructureToPtr(recipient, pRecipient, false);

					uint result = PowerRegisterSuspendResumeNotification(DEVICE_NOTIFY_CALLBACK, ref recipient, ref registrationHandle);

					if (result != 0)
					{
						Console.WriteLine("Failed to register for power mode changes, error code: " + result);
						svcTextListener.WriteLine("Failed to register for power mode changes on Windows, error code: " + result);
					}
					else
					{
						svcTextListener.WriteLine("Registered for power mode changes on Windows");
					}
				}
				else // Windows 7 or earlier
				{
					SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(OnPowerModeChanged);
				}
			}

			// Interactive seems to be always true on Linux :(
			if (RunningOnWindows && !Environment.UserInteractive)
			{
				// Windows and not interactive - must be a service
				svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Running as a Windows service");
				svcTextListener.Flush();
				service = true;
				// Launch as a Windows Service
				ServiceBase.Run(new CumulusService());
			}
			else
			{
				if (Environment.UserInteractive || (!RunningOnWindows && !service))
				{
					// Windows interactive or Linux and no service flag
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Running interactively");
				}
				else
				{
					// Must be a Linux service
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Running as a Linux service");
					service = true;
				}
				svcTextListener.Flush();
				// Launch normally - Linux Service runs like this too
				RunAsAConsole(Httpport, debug);
			}

			while (!exitSystem)
			{
				await Task.Delay(500);
			}

			if (!service)
			{
				Console.CursorVisible = true;
			}
		}

		private static void Usage()
		{
			Console.WriteLine();
			Console.WriteLine("Valid arguments are:");
			Console.WriteLine(" -port <http_portnum> - Sets the HTTP port Cumulus will use (default 8998)");
			Console.WriteLine(" -lang <culture_name> - Sets the Language Cumulus will use (defaults to current user language)");
			Console.WriteLine(" -debug               - Switches on debug and data logging from Cumulus start");
			Console.WriteLine(" -install             - Installs Cumulus as a system service (Windows or Linux)");
			Console.WriteLine(" -uninstall           - Removes Cumulus as a system service (Windows or Linux)");
			Console.WriteLine(" -user                - Specifies the user to run the service under (Linux only)");
			Console.WriteLine(" -service             - Must be used when running as service (Linux only)");
			Console.WriteLine("\nCumulus terminating");
			try
			{
				Console.CursorVisible = true;
			}
			catch
			{
				// this errors when running as a service and there is no console - ignore it
			}
			Environment.Exit(1);
		}

		private static void RunAsAConsole(int port, bool debug)
		{
			cumulus = new Cumulus();

			cumulus.Initialise(port, debug, "");

			if (!exitSystem)
			{
				Console.WriteLine(DateTime.Now.ToString("G"));
				Console.WriteLine("Type Ctrl-C to terminate\n");
			}
		}

		private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
		{
			try
			{
				Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "!!! Unhandled Exception !!!");
				Trace.WriteLine(e.ExceptionObject.ToString());

				if (service)
				{
					svcTextListener.WriteLine(e.ExceptionObject.ToString());
					svcTextListener.WriteLine("**** An error has occurred - please zip up the MXdiags folder and post it in the forum ****");
					svcTextListener.Flush();
				}
				else
				{
					Console.CursorVisible = true;
					Console.WriteLine(e.ExceptionObject.ToString());
					Console.WriteLine("**** An error has occurred - please zip up the MXdiags folder and post it in the forum ****");
					Console.WriteLine("Press Enter to terminate");
					Console.ReadLine();
				}

				Thread.Sleep(1000);
				Environment.Exit(1);
			}
			catch (Exception)
			{
				// do nothing
			}
		}

		public static bool CheckInstanceId(bool create)
		{
			// check if instance file exists, if it exists, read the contents
			if (File.Exists("UniqueId.txt"))
			{
				string txt;
				using (var sr = File.OpenText("UniqueId.txt"))
					txt = sr.ReadLine();

				// Check the length, and ends in "="
				if (txt != null && (txt.Length > 30 || txt[^1] == '='))
				{
					InstanceId = Convert.FromBase64String(txt);
					return true;
				}

				if (create && string.IsNullOrEmpty(txt))
				{
					// otherwise, create it with a newly generated id
					InstanceId = Crypto.GenerateKey();
					File.WriteAllText("UniqueId.txt", Convert.ToBase64String(InstanceId));
					return true;
				}
			}
			else if (create)
			{
				// otherwise, create it with a newly generated id
				InstanceId = Crypto.GenerateKey();
				File.WriteAllText("UniqueId.txt", Convert.ToBase64String(InstanceId));
				return true;
			}

			return false;
		}


		private const int PBT_APMSUSPEND = 0x04;
		private const int PBT_APMRESUMESUSPEND = 0x07;
		private const int PBT_APMRESUMECRITICAL = 0x06;
		private const int DEVICE_NOTIFY_CALLBACK = 0x02;

		private static int DrviceNotifyCallBack(IntPtr context, int type, IntPtr setting)
		{
			switch (type)
			{
				case PBT_APMSUSPEND:
					// The system is suspending operation.
					if (cumulus != null)
					{
						cumulus.LogCriticalMessage("*** Shutting down due to computer going to modern standby");
						Console.WriteLine("*** Shutting down due to computer going to sleep");
					}
					cumulus.Stop();
					Program.exitSystem = true;
					return 1; // handled

				case PBT_APMRESUMESUSPEND:
				case PBT_APMRESUMECRITICAL:
					// The system is resuming operation after being suspended.
					// check if already shutting down...
					if (Program.exitSystem)
					{
						if (cumulus != null)
						{
							cumulus.LogMessage("*** Resuming from modern standby, but already shutting down, no action");
						}
					}
					else
					{
						if (cumulus != null)
						{
							cumulus.LogCriticalMessage("*** Shutting down due to computer resuming from modern standby");
							Console.WriteLine("*** Shutting down due to computer resuming from standby");
						}
						Environment.Exit(999);
					}
					return 1; // handled
			}

			return 0; // not handled
		}

		private delegate int DeviceNotifyCallBackRoutine(IntPtr context, int type, IntPtr setting);

		[StructLayout(LayoutKind.Sequential)]
		private struct DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
		{
			public DeviceNotifyCallBackRoutine Callback;
			public IntPtr Context;
		}

		[DllImport("Powrprof.dll", SetLastError = true)]
		static extern uint PowerRegisterSuspendResumeNotification(
			uint flags,
			ref DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient,
			ref IntPtr RegistrationHandle);



		// Windows 7 power management
		private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
		{
#pragma warning disable CA1416 // Validate platform compatibility
			if (e.Mode == PowerModes.Suspend)
			{
				if (cumulus != null)
				{
					cumulus.LogCriticalMessage("Shutting down due to computer going to sleep");
					Console.WriteLine("*** Shutting down due to computer going to sleep");
					cumulus.Stop();
				}

				Program.exitSystem = true;
			}
#pragma warning restore CA1416 // Validate platform compatibility
		}
	}
}
