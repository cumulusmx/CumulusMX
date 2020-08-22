using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.ServiceProcess;
using System.IO;

namespace CumulusMX
{
    internal class Program
    {
        public static Cumulus cumulus;
        public static bool exitSystem = false;
        public static bool service = false;
        public static TextWriterTraceListener svcTextListener;
        const string appGuid = "57190d2e-7e45-4efb-8c09-06a176cef3f3";
        public static Mutex appMutex;

        private static void Main(string[] args)
        {
            var Windows = Type.GetType("Mono.Runtime") == null;
            //var ci = new CultureInfo("en-GB");
            //System.Threading.Thread.CurrentThread.CurrentCulture = ci;

            if (!Windows)
            {
                // Use reflection, so no attempt to load Mono dll on Windows
                Assembly _posixAsm;
                Type _unixSignalType, _signumType;
                MethodInfo _unixSignalWaitAny;

                _posixAsm = Assembly.Load("Mono.Posix, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
                _unixSignalType = _posixAsm.GetType("Mono.Unix.UnixSignal");
                _unixSignalWaitAny = _unixSignalType.GetMethod("WaitAny", new[] { _unixSignalType.MakeArrayType() });
                _signumType = _posixAsm.GetType("Mono.Unix.Native.Signum");

                Array _signals = Array.CreateInstance(_unixSignalType, 1);
                _signals.SetValue(Activator.CreateInstance(_unixSignalType, _signumType.GetField("SIGTERM").GetValue(null)), 0);

                Thread signal_thread = new Thread(delegate ()
                {
                    while (!exitSystem)
                    {
                        // Wait for a signal to be delivered
                        var id = (int)_unixSignalWaitAny.Invoke(null, new object[] { _signals });

                        cumulus.LogConsoleMessage("\nExiting system due to external SIGTERM signal");

                        exitSystem = true;
                    }
                });

                signal_thread.Start();

                // Now we need to catch the console Ctrl-C
                Console.CancelKeyPress += (s, ev) =>
                {
                    cumulus.LogConsoleMessage("Ctrl+C pressed");
                    cumulus.LogConsoleMessage("\nCumulus terminating");
                    cumulus.Stop();
                    Trace.WriteLine("Cumulus has shutdown");
                    ev.Cancel = true;
                    exitSystem = true;
                };

            }
            else
            {
                // set the working path to the exe location
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            }

            int httpport = 8998;
            bool debug = false;

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;


            for (int i = 0; i < args.Length; i++)
            {
                try
                {
                    if (args[i] == "-lang" && args.Length >= i)
                    {
                        var lang = args[++i];

                        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(lang);
                        CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(lang);
                    }
                    else if (args[i] == "-port" && args.Length >= i)
                    {
                        httpport = Convert.ToInt32(args[++i]);
                    }
                    else if (args[i] == "-debug")
                    {
                        // Switch on debug and and data logging from the start
                        debug = true;
                    }
                    else if (args[i] == "-wsport")
                    {
                        i++;
                        Console.WriteLine("The use of the -wsport command line parameter is deprecated");
                    }
                    else if (args[i] == "-install")
                    {
                        if (Windows)
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
                        }
                        else
                        {
                            Console.WriteLine("You can only install Cumulus MX as a service in Windows");
                            Environment.Exit(1);
                        }
                    }
                    else if (args[i] == "-uninstall")
                    {
                        if (Windows)
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
                        }
                        else
                        {
                            Console.WriteLine("You can only uninstall Cumulus MX as a service in Windows");
                            Environment.Exit(1);
                        }
                    }
                    else if (args[i] == "-service")
                    {
                        service = true;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid command line argument \"{args[i]}\"");
                        usage();
                    }
                }
                catch
                {
                    usage();
                }
            }

#if DEBUG
            debug = true;
            //Debugger.Launch();
#endif

            using (appMutex = new Mutex(false, "Global\\" + appGuid))
            {
                // Interactive seems to be always false under mono :(
                // So we need the no service flag & mono
                if (Environment.UserInteractive || (!service && !Windows))
                {
                    service = false;
                    RunAsAConsole(httpport, debug);
                }
                else
                {
                    var logfile = "MXdiags" + Path.DirectorySeparatorChar + "ServiceConsoleLog.txt";
                    svcTextListener = new TextWriterTraceListener(logfile);
                    service = true;
                    if (File.Exists(logfile))
                    {
                        File.Delete(logfile);
                    }
                    svcTextListener = new TextWriterTraceListener(logfile);
                    RunAsAService();
                }

                while (!exitSystem)
                {
                    Thread.Sleep(500);
                }

                Environment.Exit(0);
            }
        }

        private static void usage()
        {
            Console.WriteLine();
            Console.WriteLine("Valid arugments are:");
            Console.WriteLine(" -port <http_portnum> - Sets the HTTP port Cumulus will use (default 8998)");
            Console.WriteLine(" -lang <culture_name> - Sets the Language Cumulus will use (defaults to current user language)");
            Console.WriteLine(" -debug               - Switches on debug and data logging from Cumulus start");
            Console.WriteLine(" -install             - Installs Cumulus as a system service (Windows only)");
            Console.WriteLine(" -uninstall           - Removes Cumulus as a system service (Windows only)");
            Console.WriteLine(" -service             - Must be used when running as a mono-service (Linux only)");
            Console.WriteLine("\nCumulus terminating");
            Environment.Exit(1);
        }

        static void RunAsAConsole(int port, bool debug)
        {
            //Console.WriteLine("Current culture: " + CultureInfo.CurrentCulture.DisplayName);
            if (Type.GetType("Mono.Runtime") == null)
            {
                _ = new exitHandler();
            }

            cumulus = new Cumulus(port, debug, "");

            Console.WriteLine(DateTime.Now.ToString("G"));

            Console.WriteLine("Type Ctrl-C to terminate");
        }

        static void RunAsAService()
        {
            ServiceBase[] servicesToRun = new ServiceBase[]
            {
                new CumulusService()
            };
            ServiceBase.Run(servicesToRun);
        }


        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Trace.WriteLine("!!! Unhandled Exception !!!");
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

    public class exitHandler
    {
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        //private Program program;

        private delegate bool EventHandler(CtrlType sig);

        private static EventHandler _handler;

        private enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        public exitHandler()
        {
            _handler += new EventHandler(Handler);
            SetConsoleCtrlHandler(_handler, true);
        }

        private static bool Handler(CtrlType sig)
        {
            var reason = new string[] { "Ctrl-C", "Ctrl-Break", "Close Main Window", "unknown", "unknown", "User Logoff", "System Shutdown" };
            //Console.WriteLine("Cumulus terminating");
            Program.cumulus.LogConsoleMessage("Cumulus terminating");

            Trace.WriteLine("Exiting system due to external: " + reason[(int)sig]);

            Program.cumulus.Stop();

            Trace.WriteLine("Cumulus has shutdown");
            Console.WriteLine("Cumulus stopped");

            //allow main to run off
            Thread.Sleep(200);
            Program.exitSystem = true;

            return true;
        }
    }
}
