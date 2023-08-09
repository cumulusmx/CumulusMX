using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace CumulusMX
{
	internal class Program
	{
		public static Cumulus cumulus;
		public static bool exitSystem = false;
		public static bool service = false;
		public static TextWriterTraceListener svcTextListener;
		public const string AppGuid = "57190d2e-7e45-4efb-8c09-06a176cef3f3";
		public static DateTime StartTime;

		public static int httpport = 8998;
		public static bool debug = false;

		private static void Main(string[] args)
		{
			StartTime = DateTime.Now;
			var windows = Type.GetType("Mono.Runtime") == null;
			//var ci = new CultureInfo("en-GB");
			//System.Threading.Thread.CurrentThread.CurrentCulture = ci;

			// force the current folder to be CumulusMX folder
			Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));

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
			svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Starting on " + (windows ? "Windows" : "Linux"));
			svcTextListener.Flush();

			if (!windows)
			{
				// Use reflection, so no attempt to load Mono dll on Windows
				svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Creating SIGTERM monitor");
				svcTextListener.Flush();

				var posixAsm = Assembly.Load("Mono.Posix, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
				var unixSignalType = posixAsm.GetType("Mono.Unix.UnixSignal");
				var unixSignalWaitAny = unixSignalType.GetMethod("WaitAny", new[] { unixSignalType.MakeArrayType() });
				var signumType = posixAsm.GetType("Mono.Unix.Native.Signum");

				var signals = Array.CreateInstance(unixSignalType, 1);
				signals.SetValue(Activator.CreateInstance(unixSignalType, signumType.GetField("SIGTERM").GetValue(null)), 0);

				Thread signalThread = new Thread(delegate ()
				{
					while (!exitSystem)
					{
						// Wait for a signal to be delivered
						unixSignalWaitAny?.Invoke(null, new object[] {signals});

						var msg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Exiting system due to external SIGTERM signal";
						Console.WriteLine(msg);
						svcTextListener.WriteLine(msg);

						if (cumulus != null)
						{
							msg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus terminating";
							Console.WriteLine(msg);
							svcTextListener.WriteLine(msg);
							cumulus.LogMessage("Exiting system due to external SIGTERM signal");
							cumulus.LogMessage("Cumulus terminating");
							cumulus.Stop();
							//allow main to run off
							Thread.Sleep(500);
							msg = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus has shutdown";
							Console.WriteLine(msg);
							svcTextListener.WriteLine(msg);
						}

						exitSystem = true;
					}
				});

				signalThread.Start();

				// Now we need to catch the console Ctrl-C
				Console.CancelKeyPress += (s, ev) =>
				{
					if (cumulus != null)
					{
						cumulus.LogConsoleMessage("Ctrl+C pressed");
						cumulus.LogConsoleMessage("\nCumulus terminating");
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
				};

			}

			AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

#if DEBUG
			debug = true;
			//Debugger.Launch();
#endif

			for (int i = 0; i < args.Length; i++)
			{
				try
				{
					switch (args[i])
					{
						case "-lang" when args.Length >= i:
						{
							var lang = args[++i];

							CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(lang);
							CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(lang);
							break;
						}
						case "-port" when args.Length >= i:
							httpport = Convert.ToInt32(args[++i]);
							break;
						case "-debug":
							// Switch on debug and data logging from the start
							debug = true;
							break;
						case "-wsport":
							i++;
							Console.WriteLine("The use of the -wsport command line parameter is deprecated");
							break;
						case "-install" when windows:
						{
							if (SelfInstaller.InstallMe())
							{
								Console.WriteLine("Cumulus MX is now installed to run as service");
								Environment.Exit(0);
							}
							else
							{
								Console.WriteLine("Cumulus MX failed to install as service");
								Environment.Exit(1);
							}

							break;
						}
						case "-install":
							Console.WriteLine("You can only install Cumulus MX as a service in Windows");
							Environment.Exit(1);
							break;
						case "-uninstall" when windows:
						{
							if (SelfInstaller.UninstallMe())
							{
								Console.WriteLine("Cumulus MX is no longer installed to run as service");
								Environment.Exit(0);
							}
							else
							{
								Console.WriteLine("Cumulus MX failed uninstall itself as service");
								Environment.Exit(1);
							}

							break;
						}
						case "-uninstall":
							Console.WriteLine("You can only uninstall Cumulus MX as a service in Windows");
							Environment.Exit(1);
							break;
						case "-service":
							service = true;
							break;
						default:
							Console.WriteLine($"Invalid command line argument \"{args[i]}\"");
							Usage();
							break;
					}
				}
				catch
				{
					Usage();
				}
			}

			// Interactive seems to be always false under mono :(

			if (windows && !Environment.UserInteractive)
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
				if (Environment.UserInteractive ||(!windows && !service))
				{
					// Windows interactive or Linux and no service flag
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Running interactively");
					service = false;
				}
				else
				{
					// Must be a Linux service
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Running as a Linux service");
					service = true;
				}
				svcTextListener.Flush();
				// Launch normally - Linux Service runs like this too
				RunAsAConsole(httpport, debug);
			}

			while (!exitSystem)
			{
				Thread.Sleep(500);
			}

			Environment.Exit(0);
		}

		private static void Usage()
		{
			Console.WriteLine();
			Console.WriteLine("Valid arguments are:");
			Console.WriteLine(" -port <http_portnum> - Sets the HTTP port Cumulus will use (default 8998)");
			Console.WriteLine(" -lang <culture_name> - Sets the Language Cumulus will use (defaults to current user language)");
			Console.WriteLine(" -debug               - Switches on debug and data logging from Cumulus start");
			Console.WriteLine(" -install             - Installs Cumulus as a system service (Windows only)");
			Console.WriteLine(" -uninstall           - Removes Cumulus as a system service (Windows only)");
			Console.WriteLine(" -service             - Must be used when running as a mono-service (Linux only)");
			Console.WriteLine("\nCumulus terminating");
			Environment.Exit(1);
		}

		private static void RunAsAConsole(int port, bool debug)
		{
			//Console.WriteLine("Current culture: " + CultureInfo.CurrentCulture.DisplayName);
			if (Type.GetType("Mono.Runtime") == null)
			{
				svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Creating Windows Exit Handler");
				svcTextListener.Flush();

				_ = new ExitHandler();
			}

			cumulus = new Cumulus(port, debug, "");

			if (!exitSystem)
			{
				Console.WriteLine(DateTime.Now.ToString("G"));
				Console.WriteLine("Type Ctrl-C to terminate");
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
			}
		}
	}


	// Windows ExitHandler
	public class ExitHandler
	{
		[DllImport("Kernel32")]
		private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

		//private Program program;

		private delegate bool EventHandler(CtrlType sig);

		private static EventHandler handler;

		private enum CtrlType
		{
			CTRL_C_EVENT = 0,
			CTRL_BREAK_EVENT = 1,
			CTRL_CLOSE_EVENT = 2,
			CTRL_LOGOFF_EVENT = 5,
			CTRL_SHUTDOWN_EVENT = 6
		}

		public ExitHandler()
		{
			handler += Handler;
			SetConsoleCtrlHandler(handler, true);
		}

		private static bool Handler(CtrlType sig)
		{
			var reason = new[] { "Ctrl-C", "Ctrl-Break", "Close Main Window", "unknown", "unknown", "User Logoff", "System Shutdown" };

			Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Exiting system due to external: " + reason[(int)sig]);

			if (Program.cumulus != null)
			{
				Program.cumulus.LogConsoleMessage("Cumulus terminating");
				Program.cumulus.Stop();
				//allow main to run off
				Thread.Sleep(500);
			}
			else
			{
				Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus has not finished initialising, a clean exit is not possible, forcing exit");
				Environment.Exit(2);
			}

			Trace.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus has shutdown");
			Console.WriteLine("Cumulus stopped");

			Program.exitSystem = true;

			return true;
		}
	}
}
