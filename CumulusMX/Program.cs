using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace CumulusMX
{
    internal class Program
    {
        public static Cumulus cumulus;
        public static bool exitSystem = false;
        //private exitHandler ctrlchandler;

        private static void Main(string[] args)
        {
            //var ci = new CultureInfo("en-GB");
            //System.Threading.Thread.CurrentThread.CurrentCulture = ci;

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                // Use reflection, so no attempt to load Mono dll on Windows
                Assembly _posixAsm;
                Type _unixSignalType, _signumType;
                MethodInfo _unixSignalWaitAny;

                _posixAsm = Assembly.Load("Mono.Posix, Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756");
                _unixSignalType = _posixAsm.GetType("Mono.Unix.UnixSignal");
                _unixSignalWaitAny = _unixSignalType.GetMethod("WaitAny", new[] { _unixSignalType.MakeArrayType() });
                _signumType = _posixAsm.GetType("Mono.Unix.Native.Signum");

                Array _signals = Array.CreateInstance(_unixSignalType, 2);
                _signals.SetValue(Activator.CreateInstance(_unixSignalType, _signumType.GetField("SIGINT").GetValue(null)), 0);
                _signals.SetValue(Activator.CreateInstance(_unixSignalType, _signumType.GetField("SIGTERM").GetValue(null)), 1);

                Thread signal_thread = new Thread(delegate()
                                                  {
                                                      while (true)
                                                      {
                                                          // Wait for a signal to be delivered
                                                          var id = (int)_unixSignalWaitAny.Invoke(null, new object[] { _signals });

                                                          // Notify the main thread that a signal was received,
                                                          // you can use things like:
                                                          //    Application.Invoke () for Gtk#
                                                          //    Control.Invoke on Windows.Forms
                                                          //    Write to a pipe created with UnixPipes for server apps.
                                                          //    Use an AutoResetEvent

                                                          exitSystem = true;

                                                          //AppDomain.CurrentDomain.UnhandledException -= UnhandledExceptionTrapper;
                                                      }
                                                  });

                signal_thread.Start();
            }
            else
            {
                var exithandler = new exitHandler();
            }

            int httpport = 8998;

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-lang" && args.Length >= i)
                {
                    var lang = args[i + 1];

                    CultureInfo.DefaultThreadCurrentCulture = new CultureInfo(lang);
                    CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo(lang);
                }

                if (args[i] == "-port" && args.Length >= i)
                {
                    httpport = Convert.ToInt32(args[i + 1]);
                }
            }

            //System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo("en-GB");
            //System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new System.Globalization.CultureInfo("en-GB");
            Console.WriteLine("Current culture: " + CultureInfo.CurrentCulture.DisplayName);

            cumulus = new Cumulus(httpport);

            DateTime now = DateTime.Now;

            Console.WriteLine(DateTime.Now.ToString("G"));

            Console.WriteLine("Type Ctrl-C to terminate");
            while (!exitSystem)
            {
                Thread.Sleep(500);
            }

            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                Console.WriteLine("\nCumulus terminating");
                cumulus.Stop();
                Console.WriteLine("Program exit");
                Environment.Exit(0);
            }
        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                cumulus.LogMessage(e.ExceptionObject.ToString());
                Trace.Flush();
                Console.WriteLine(e.ExceptionObject.ToString());
                Console.WriteLine("**** An error has occurred - please zip up the MXdiags folder and post it in the forum ****");
                Console.WriteLine("Press Enter to terminate");
                Console.ReadLine();
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
            Console.WriteLine("Cumulus terminating");
            Trace.WriteLine("Exiting system due to external CTRL-C, or process kill, or shutdown");

            //allow main to run off
            Program.exitSystem = true;

            Program.cumulus.Stop();
            Environment.Exit(0);

            return true;
        }
    }
}
