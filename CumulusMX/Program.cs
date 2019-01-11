using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

namespace CumulusMX
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            int httpport = 8998;
            int wsport = 8002;
            bool runAsService = false;

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionTrapper;

            ConfigureLogging($"MXDiags{Path.DirectorySeparatorChar}/{DateTime.Now.ToString("yyyyMMdd-hhmmss")}.txt");

            var pathToApplicationBase = Path.GetDirectoryName(new System.Uri(Assembly.GetExecutingAssembly().GetName().CodeBase).LocalPath);
            var pathToContentRoot = Directory.GetCurrentDirectory();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-lang" && args.Length >= i)
                {
                    var lang = args[i + 1];

                    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new System.Globalization.CultureInfo(lang);
                    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new System.Globalization.CultureInfo(lang);
                }

                if (args[i] == "-port" && args.Length >= i)
                {
                    httpport = Convert.ToInt32(args[i + 1]);
                }

                if (args[i] == "-wsport" && args.Length >= i)
                {
                    wsport = Convert.ToInt32(args[i + 1]);
                }

                if (args[i] == "-service")
                {
                    runAsService = true;
                    pathToContentRoot = pathToApplicationBase;
                }
            }

            log.Debug("Current culture: " + CultureInfo.CurrentCulture.DisplayName);


            var exitEvent = new ManualResetEvent(false);
            Console.CancelKeyPress += (sender, eventArgs) =>
            {
                eventArgs.Cancel = true;
                exitEvent.Set();
            };


            log.Debug("Current Date: " + DateTime.Now.ToString("G"));
            var cumulusService = new CumulusService(httpport, pathToApplicationBase, pathToContentRoot);

            if (runAsService)
            {
                ServiceBase.Run(new ServiceBase[]
                {
                    cumulusService
                });
            }
            else
            {
                cumulusService.Start();
                log.Debug("Type Ctrl-C to terminate");
                exitEvent.WaitOne();
                log.Debug("\nCumulus terminating");
                cumulusService.Stop();
            }


        }

        private static void UnhandledExceptionTrapper(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                log.Error(e.ExceptionObject);
                Console.WriteLine("**** An error has occurred - please zip up the MXdiags folder and post it in the forum ****");
                Console.WriteLine("Press Enter to terminate");
                Console.ReadLine();
                Environment.Exit(1);
            }
            catch (Exception)
            {

            }
        }



        private static void ConfigureLogging(string logFilePath)
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository("");

            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
            patternLayout.ActivateOptions();

            RollingFileAppender roller = new RollingFileAppender();
            roller.AppendToFile = false;
            roller.File = logFilePath;
            roller.Layout = patternLayout;
            roller.MaxSizeRollBackups = 5;
            roller.MaximumFileSize = "1MB";
            roller.RollingStyle = RollingFileAppender.RollingMode.Size;
            roller.StaticLogFileName = true;
            roller.ActivateOptions();
            hierarchy.Root.AddAppender(roller);

            ConsoleAppender console = new ConsoleAppender();
            console.Layout = patternLayout;
            console.ActivateOptions();
            hierarchy.Root.AddAppender(console);

            hierarchy.Root.Level = Level.Info;
            hierarchy.Configured = true;
        }
    }
}
