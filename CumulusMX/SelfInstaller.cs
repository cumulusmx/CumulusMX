using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace CumulusMX
{
	public class SelfInstaller
	{

		[DllImport("libc")]
		private static extern uint getuid();

		private const string serviceFile = "/etc/systemd/system/cumulusmx.service";


		// Yuk, yuk, yuk, but the best way I have found to install as a service is to shell sc.exe!
		public static bool InstallWin()
		{
			try
			{
				if (!IsElevated())
				{
					Console.WriteLine("You must run the service installation as an elevated user - 'Run as Administrator'");
					return false;
				}

				Console.WriteLine("Installing as a Windows Service...");

				// sc create CumulusMX binpath=C:\CumulusMX\CumulusMX.dll start= delayed-auto depend= LanmanWorkstation

				var path = AppDomain.CurrentDomain.BaseDirectory + "\\CumulusMX.exe";

				var startinfo = new ProcessStartInfo
				{
					FileName = "sc.exe",
					UseShellExecute = false,
					CreateNoWindow = true,
					Arguments = $"create CumulusMX binpath=\"{path}\" start=delayed-auto depend=Netman"
				};

				var sc = new Process
				{
					StartInfo = startinfo
				};

				sc.Start();
				sc.WaitForExit();

				var createExitCode = RunCommand("sc.exe", $"create CumulusMX binpath =\"{path}\" start=delayed-auto depend=Netman");

				// Now set the description
				RunCommand("sc.exe", "description CumulusMX \"CumulusMX weather station service\"");

				if (createExitCode == 0)
					return true;
				else
					return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error creating service: " + ex.Message);
				return false;
			}
		}
		public static bool UninstallWin()
		{
			try
			{
				if (!IsElevated())
				{
					Console.WriteLine("You must run the service uninstall process as an elevated user - 'Run as Administrator'");
					return false;
				}

				Console.WriteLine("Uninstalling as a Windows Service...");

				// sc delete CumulusMX

				if (RunCommand("sc.exe", "delete CumulusMX") == 0)
					return true;
				else
					return false;
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error removing service: " + ex.Message);
				return false;
			}
		}

		public static bool InstallLinux(string userId)
		{
			try
			{
				if (!IsElevated())
				{
					Console.WriteLine("You must run the service installation as an elevated user - 'sudo dotnet CumulusMX -install'");
					return false;
				}

				var user = string.IsNullOrEmpty(userId) ? "root" : userId;

				Console.WriteLine($"Installing as a systemctld service to run as userid {user}...");


				// does the service file exist already?
				if (File.Exists(serviceFile))
				{
					// delete it and start again
					File.Delete(serviceFile);
				}

				// get the location of dotnet.exe - not so simple!
				// we have to get our own process, then find the main module filename

				var dotnetPath = Process.GetProcessById(Environment.ProcessId).MainModule.FileName;

				var appPath = AppDomain.CurrentDomain.BaseDirectory;


				string[] contents = {
					"[Unit]",
					"Description=CumulusMX service",
					"Documentation=https://cumuluswiki.org/a/Main_Page https://cumulus.hosiene.co.uk/",
					"Wants=network-online.target",
					"After=network-online.target",
					"",
					"[Service]",
					$"User={user}",
					$"Group={user}",
					$"WorkingDirectory={appPath}",
					$"ExecStart=\"{dotnetPath}\" CumulusMX.dll -service",
					"Type=simple",
					"",
					"[Install]",
					"WantedBy=multi-user.target"
				};

				File.WriteAllLines(serviceFile, contents);

				return RunCommand("systemctl", "daemon-reload") == 0;

			}
			catch (Exception ex)
			{
				Console.WriteLine("Error creating service: " + ex.Message);
				return false;
			}
		}

		public static bool UninstallLinux()
		{
			try
			{
				if (!IsElevated())
				{
					Console.WriteLine("You must run the service uninstall process as an elevated user - 'Run as Administrator'");
					return false;
				}

				// stop and disable the service if it is running
				RunCommand("systemctl", "stop cumulusmx");
				RunCommand("systemctl", "disable cumulusmx");

				// does the service file exist?
				if (File.Exists(serviceFile))
				{
					// delete it
					File.Delete(serviceFile);
				}
				else
				{
					Console.WriteLine("Error removing service, the service unit file was not found - " + serviceFile);
				}

				// does a sym link exist
				if (File.Exists("/usr/lib/systemd/system/cumulusmx.service"))
				{
					// delete it
					File.Delete("/usr/lib/systemd/system/cumulusmx.service");
				}

				var ok = RunCommand("systemctl", "daemon-reload") == 0;
				ok = ok && RunCommand("systemctl", "reset-failed") == 0;

				return ok;
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error removing service: " + ex.Message);
				return false;
			}
		}

		public static bool IsElevated()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
			else
				return getuid() == 0;
		}

		private static int RunCommand(string exe, string args)
		{
			try
			{
				var startinfo = new ProcessStartInfo
				{
					FileName = exe,
					UseShellExecute = false,
					CreateNoWindow = true,
					Arguments = args
				};

				var sc = new Process
				{
					StartInfo = startinfo
				};

				sc.Start();
				sc.WaitForExit();

				return sc.ExitCode;
			}
			catch
			{
				return 999;
			}
		}

	}
}
