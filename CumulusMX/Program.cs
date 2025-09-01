using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32;

using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;

using ServiceStack;

//#error version

namespace CumulusMX
{
	internal static class Program
	{
		public static Cumulus cumulus { get; set; }
		public static bool service { get; set; } = false;
		public static TextWriterTraceListener svcTextListener { get; set; }
		public const string AppGuid = "57190d2e-7e45-4efb-8c09-06a176cef3f3";
		public static DateTime StartTime { get; set; }
		public static byte[] InstanceId { get; set; }
		public static int Httpport { get; set; } = 8998;

		private static bool RunningOnWindows;
		public static bool debug { get; set; } = false;

		public static Logger MxLogger { get; private set; }

		public static Random RandGenerator { get; } = new Random();

		public static readonly CancellationTokenSource ExitSystemTokenSource = new();
		public static readonly CancellationToken ExitSystemToken = ExitSystemTokenSource.Token;

		public static ConfigFile configFile { get; } = ReadConfigFile();

		private static nint powerNotificationRegistrationHandle = new();
		private static PosixSignalRegistration posixSigTermRegistrationHandle;


		private static async Task Main(string[] args)
		{
			// force the current folder to be CumulusMX folder
			Environment.CurrentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			Directory.SetCurrentDirectory(Environment.CurrentDirectory);

			StartTime = DateTime.Now;
			RunningOnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

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
				Environment.Exit(5);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error while attempting to read/create folder /MXdiags, error message: {ex.Message}");
			}

			SetupLogging();

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

			MxLogger.Info("Starting Cumulus MX on " + (RunningOnWindows ? "Windows" : "Linux"));

			// Add an exit handler
			AppDomain.CurrentDomain.ProcessExit += ProcessExit;


			if (RunningOnWindows)
			{
				// Now we need to catch WINDOWS ONLY console and shutdown events
				// Register the handler
				SetConsoleCtrlHandler(ConsoleCtrlCheck, true);
			}
			else
			{
				// On Linux, Ctrl-C is handled by the Console.CancelKeyPress event
				// Now we need to catch the console Ctrl-C CROSS PLATFORM
				Console.CancelKeyPress += (s, ev) =>
				{
					MxLogger.Warn("**** Ctrl-C pressed ****");
					Console.WriteLine("**** Ctrl-C pressed ****", ConsoleColor.Red);

					ev.Cancel = true;

					if (!service)
					{
						Console.CursorVisible = true;
					}

					Program.ExitSystemTokenSource.Cancel();
				};
			}

			// Register the routine for unhandled exceptions
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
				MxLogger.Info("Command line: " + Environment.CommandLine);

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
							MxLogger.Warn("The use of the -wsport command line parameter is deprecated");
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
							MxLogger.Error($"Invalid command line argument \"{args[i]}\"");
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
						MxLogger.Info("Cumulus MX is now installed to run as service");
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
						MxLogger.Warn("You must supply a user name when installing the service");
						Environment.Exit(0);

					}

					if (SelfInstaller.InstallLinux(user, group, lang, Httpport, servicename))
					{
						Console.ForegroundColor = ConsoleColor.Green;
						Console.WriteLine("\nCumulus MX is now installed to run as service\n");
						Console.ResetColor();
						MxLogger.Info("Cumulus MX is now installed to run as service");
						Environment.Exit(0);
					}
				}

				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("\nCumulus MX failed to install as service\n");
				Console.ResetColor();
				MxLogger.Error("Cumulus MX failed to install as service");
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
						MxLogger.Info("Cumulus MX is no longer installed to run as service");
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
						MxLogger.Info("Cumulus MX is no longer installed to run as service");
						Environment.Exit(0);
					}
				}

				Console.ForegroundColor = ConsoleColor.Red;
				Console.WriteLine("\nCumulus MX failed uninstall itself as service\n");
				Console.ResetColor();
				MxLogger.Error("Cumulus MX failed to uninstall itself as service");
				Environment.Exit(1);
			}

			// detect system sleeping/hibernating - Windows only
			if (RunningOnWindows)
			{
				// Windows 10 or later uses Modern Standby
				if (Environment.OSVersion.Version.Major >= 10)
				{
					DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient = new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
					{
						Callback = DeviceNotifyCallBack,
						Context = IntPtr.Zero
					};

					IntPtr pRecipient = Marshal.AllocHGlobal(Marshal.SizeOf(recipient));
					Marshal.StructureToPtr(recipient, pRecipient, false);

					uint result = PowerRegisterSuspendResumeNotification(DEVICE_NOTIFY_CALLBACK, ref recipient, ref powerNotificationRegistrationHandle);

					if (result != 0)
					{
						Console.WriteLine("Failed to register for power mode changes, error code: " + result);
						svcTextListener.WriteLine("Failed to register for modern power mode changes on Windows, error code: " + result);
						MxLogger.Error("Failed to register for modern power mode changes on Windows, error code: " + result);
					}
					else
					{
						svcTextListener.WriteLine("Registered for modern power mode changes on Windows");
						MxLogger.Info("Registered for modern power mode changes on Windows");
					}
				}
				else // Windows 7 or earlier
				{
					SystemEvents.PowerModeChanged += new PowerModeChangedEventHandler(OnPowerModeChanged);
					MxLogger.Info("Registered for legacy power mode changes on Windows");
				}
			}

			// Interactive seems to be always true on Linux :(
			if (RunningOnWindows)
			{   if (Environment.UserInteractive)
				{
					// Windows interactive
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Running interactively");
					svcTextListener.Flush();
					MxLogger.Info("We are running interactively");
					RunAsAConsole(Httpport, debug);
				}
				else
				{
					// Windows and not interactive - must be a service
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Running as a Windows service");
					svcTextListener.Flush();
					MxLogger.Info("We are running as a Windows service");
					service = true;
					// Launch as a Windows Service
					ServiceBase.Run(new CumulusService());
				}
			}
			else
			{
				// Must be Linux/macOS
				if (service)
				{
					// Must be a Linux service
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Running as a Linux service");
					MxLogger.Info("We are running as a Linux service");
				}
				else
				{
					svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Running interactively");
					MxLogger.Info("We are running interactively");
				}

				svcTextListener.Flush();

				// add the SIGTERM handler
				SigTermSignalHandler();

				// Launch normally - Linux Service runs like this too
				RunAsAConsole(Httpport, debug);
			}

			try
			{
				await Task.Delay(Timeout.Infinite, ExitSystemTokenSource.Token);
			}
			catch (TaskCanceledException)
			{
				// do nothing, we are exiting
			}

			MxLogger.Info("Process exiting");

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

			Console.WriteLine(DateTime.Now.ToString("G"));
			Console.WriteLine("Type Ctrl-C to terminate\n");
		}

		private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
		{
			try
			{
				MxLogger.Fatal("An error has occurred - please zip up the MXdiags folder and post it in the forum");
				MxLogger.Fatal("!!! Unhandled Exception !!!");
				MxLogger.Fatal(e.ExceptionObject.ToString());

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

		private static void ProcessExit(object s, EventArgs e)
		{
			MxLogger.Info("Cumulus termination started");

			if (!ExitSystemTokenSource.IsCancellationRequested)
				ExitSystemTokenSource.Cancel();

			if (cumulus != null && Environment.ExitCode != 999)
			{
				Console.WriteLine("Cumulus terminating", ConsoleColor.Red);
				cumulus.Stop();
				MxLogger.Info("Cumulus has shutdown");
				svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus has shutdown");
				svcTextListener.Flush();
				Console.ForegroundColor = ConsoleColor.Yellow;
				Console.WriteLine("Cumulus stopped");
				Console.ResetColor();
			}
			else
			{
				Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus terminating");
				svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus terminating");
			}

			svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Cumulus has shutdown");
			svcTextListener.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + "Exit code = " + Environment.ExitCode);
			svcTextListener.Flush();
			MxLogger.Info("Cumulus exit code = " + Environment.ExitCode);
			LogManager.Flush();
			LogManager.Shutdown();

			if (!service)
			{
				Console.CursorVisible = true;
			}

			if (powerNotificationRegistrationHandle != 0)
			{
				// Unregister the power notification
				_ = PowerUnregisterSuspendResumeNotification(powerNotificationRegistrationHandle);
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
					MxLogger.Info("UniqueId.txt exists, and contains a valid instance ID");
					InstanceId = Convert.FromBase64String(txt);
					return true;
				}

				if (create && string.IsNullOrEmpty(txt))
				{
					// otherwise, create it with a newly generated id
					MxLogger.Info("UniqueId.txt exists but the contents is invalid, creating a new instance ID");

					InstanceId = Crypto.GenerateKey();
					File.WriteAllText("UniqueId.txt", Convert.ToBase64String(InstanceId));
					return true;
				}
			}
			else if (create)
			{
				// otherwise, create it with a newly generated id
				MxLogger.Info("UniqueId.txt does not exist, creating a new instance ID");
				InstanceId = Crypto.GenerateKey();
				File.WriteAllText("UniqueId.txt", Convert.ToBase64String(InstanceId));
				return true;
			}

			return false;
		}

		private static void SetupLogging()
		{
			// Log file target
			var logfile = new FileTarget()
			{
				Name = "logfile",
				FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MXdiags", "MxDiags.log"),
				ArchiveSuffixFormat = "{1:-yyMMdd-HHmmss}",
				ArchiveAboveSize = configFile.runtimeOptions.configProperties.LogFileSize,
				ArchiveOldFileOnStartup = true,
				MaxArchiveFiles = configFile.runtimeOptions.configProperties.LogFileCount,
				Layout = "${longdate}|${level}| ${message}",
				Footer = "------ LOG CLOSED ${longdate} ------"
			};

			// Async wrapper
			var asyncLogFile = new AsyncTargetWrapper()
			{
				WrappedTarget = logfile,
				Name = "MxDiags",
				OverflowAction = AsyncTargetWrapperOverflowAction.Discard,
				QueueLimit = 10000,
				BatchSize = 200,
				TimeToSleepBetweenBatches = 1
			};

			// Config
			var config = new LoggingConfiguration();
			config.AddRule(LogLevel.Trace, LogLevel.Fatal, asyncLogFile, "CMX", !Debugger.IsAttached);

			// Debugging?
			if (Debugger.IsAttached)
			{
				// debugger
				var debugger = new DebuggerTarget()
				{
					Layout = "${time} ${message}"
				};
				config.AddRule(LogLevel.Trace, LogLevel.Fatal, debugger, "CMX", true);
			}

			//NLog.Common.InternalLogger.LogLevel = LogLevel.Trace;
			//NLog.Common.InternalLogger.LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MXdiags", "nlog-internal.txt");

			// Apply configuration
			LogManager.Configuration = config;
			LogManager.AutoShutdown = false;

			MxLogger = LogManager.GetLogger("CMX");

			MxLogger.Info("------ Created initial log file for this run of Cumulus MX ------");
		}


		public static ConfigFile ReadConfigFile()
		{
			if (File.Exists("CumulusMX.runtimeconfig.json"))
			{
				try
				{
					return (File.ReadAllText("CumulusMX.runtimeconfig.json")).FromJson<ConfigFile>();
				}
				catch
				{
					// do nothing
				}
			}

			return new ConfigFile()
			{
				runtimeOptions = new ConfigFileRunTime()
				{
					configProperties = new ConfigFileProperties()
					{
						PhpMaxConnections = 3,
						RealtimeFtpWatchDogInterval = 300,
						LogFileSize = 2096970
					}
				}
			};
		}

		public sealed class ConfigFile
		{
			public ConfigFileRunTime runtimeOptions { get; set; }
		}
		public sealed class ConfigFileRunTime
		{
			public ConfigFileProperties configProperties { get; set; }
		}

		[DataContract]
		public sealed class ConfigFileProperties
		{
			[DataMember(Name = "User.PhpMaxConnections")]
			public int PhpMaxConnections { get; set; }

			[DataMember(Name = "User.RealtimeFtpWatchDogInterval")]
			public int RealtimeFtpWatchDogInterval { get; set; }

			[DataMember(Name = "User.LogFileSize")]
			public int LogFileSize { get; set; }

			[DataMember(Name = "User.LogfileCount")]
			public int LogFileCount { get; set; }
		}


		// Ctrl-C and Windows Power events
		[DllImport("Kernel32")]
		static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate handler, bool add);

		delegate bool ConsoleCtrlDelegate(CtrlTypes ctrlType);

		enum CtrlTypes
		{
			CTRL_C_EVENT = 0,
			CTRL_BREAK_EVENT = 1,
			CTRL_CLOSE_EVENT = 2,
			CTRL_LOGOFF_EVENT = 5,
			CTRL_SHUTDOWN_EVENT = 6
		}

		static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
		{
			if (ctrlType == CtrlTypes.CTRL_C_EVENT)
			{
				// Handle Ctrl-C
				MxLogger.Warn("**** Ctrl-C pressed ****");
				Console.WriteLine("**** Ctrl-C pressed ****", ConsoleColor.Red);
			}
			if (ctrlType == CtrlTypes.CTRL_BREAK_EVENT)
			{
				// Handle Ctrl-C or Ctrl-Break
				MxLogger.Warn("**** Ctrl-Break pressed ****");
				Console.WriteLine("**** Ctrl-Break pressed ****", ConsoleColor.Red);
			}
			else if (ctrlType == CtrlTypes.CTRL_CLOSE_EVENT)
			{
				// Handle console close event
				MxLogger.Warn("**** Console close event received ****");
				Console.WriteLine("**** Console close event received ****", ConsoleColor.Red);
			}
			else if (ctrlType == CtrlTypes.CTRL_SHUTDOWN_EVENT)
			{
				// Handle shutdown or logoff
				MxLogger.Warn("**** System Shutdown event received ****");
				Console.WriteLine("**** System Shutdown event received ****", ConsoleColor.Red);
			}

			// Ignore log-off (it may be another user)
			if (ctrlType != CtrlTypes.CTRL_LOGOFF_EVENT)
			{
				ExitSystemTokenSource.Cancel();

				if (!service)
				{
					Console.CursorVisible = true;
					Thread.Sleep(500);
					return true; // we have handled the event
				}
			}

			// we have NOT handled the event
			return false;
		}



		// Windows 10 or later uses Modern Standby
		private const int PBT_APMSUSPEND = 0x04;
		private const int PBT_APMRESUMESUSPEND = 0x07;
		private const int PBT_APMRESUMECRITICAL = 0x06;
		private const int DEVICE_NOTIFY_CALLBACK = 0x02;

		private static int DeviceNotifyCallBack(IntPtr context, int type, IntPtr setting)
		{
			switch (type)
			{
				case PBT_APMSUSPEND:
					// The system is suspending operation.
					MxLogger.Fatal("**** Shutting down due to computer going to modern standby ****");
					Console.WriteLine("**** Shutting down due to computer going to sleep ****");
					ExitSystemTokenSource.Cancel();
					Thread.Sleep(500);
					return 1; // handled

				case PBT_APMRESUMESUSPEND:
				case PBT_APMRESUMECRITICAL:
					// The system is resuming operation after being suspended.
					// check if already shutting down...
					if (ExitSystemTokenSource.IsCancellationRequested)
					{
						MxLogger.Info("**** Resuming from modern standby, but already shutting down, no action ****");
					}
					else
					{
						MxLogger.Warn("**** Shutting down due to computer resuming from modern standby ****");
						Console.WriteLine("**** Shutting down due to computer resuming from standby ****");
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
		private static extern uint PowerRegisterSuspendResumeNotification(
			uint flags,
			ref DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS recipient,
			ref IntPtr RegistrationHandle
		);

		[DllImport("Powrprof.dll", SetLastError = true)]
		private static extern uint PowerUnregisterSuspendResumeNotification(IntPtr registrationHandle);


		// Windows 7 power management
		private static void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
		{
#pragma warning disable CA1416 // Validate platform compatibility
			if (e.Mode == PowerModes.Suspend)
			{
				MxLogger.Fatal("*** Shutting down due to computer going to sleep");
				Console.WriteLine("*** Shutting down due to computer going to sleep");

				ExitSystemTokenSource.Cancel();
			}
#pragma warning restore CA1416 // Validate platform compatibility
		}

		// Linux signal handling
		private static void SigTermSignalHandler()
		{
			MxLogger.Info("Registering for SIGTERM");

			posixSigTermRegistrationHandle = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
			{
				MxLogger.Warn("**** SIGTERM received ****");
				Console.WriteLine("**** SIGTERM received ****", ConsoleColor.Red);
				svcTextListener.WriteLine("**** SIGTERM received ****");
				Environment.ExitCode = 0;
				ExitSystemTokenSource.Cancel();
				context.Cancel = true;
			});
		}

	}
}
