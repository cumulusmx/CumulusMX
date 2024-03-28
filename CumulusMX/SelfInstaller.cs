using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

using Renci.SshNet.Messages;

namespace CumulusMX
{
	public static class SelfInstaller
	{
		// Yuk, yuk, yuk, but the best way I have found to install as a service is to shell sc.exe!
		public static bool InstallWin()
		{
			try
			{
				var path = AppDomain.CurrentDomain.BaseDirectory + "\\CumulusMX.exe";

				if (!IsElevated())
				{
					Console.WriteLine("Restarting as elevated...");
					var exitcode = RunCommand(path, "-install", true, true, true);
					return exitcode == 0;
				}

				Console.WriteLine("Installing as a Windows Service...");

				// sc create CumulusMX binpath=C:\CumulusMX\CumulusMX.dll start= delayed-auto depend= LanmanWorkstation
				var createExitCode = RunCommand("sc.exe", $"create CumulusMX binpath=\"{path}\" start=delayed-auto depend=Netman");

				if (createExitCode != 0)
					return false;

				// Now set the description
				createExitCode = RunCommand("sc.exe", "description CumulusMX \"CumulusMX weather station service\"");

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
				var path = AppDomain.CurrentDomain.BaseDirectory + "\\CumulusMX.exe";

				if (!IsElevated())
				{
					Console.WriteLine("Restarting as elevated...");
					var exitcode = RunCommand(path, "-uninstall", true, true, true);
					return exitcode == 0;
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

		public static bool InstallLinux(string userId, string groupId, string lang, int port, string servicename)
		{
			try
			{
				var user = string.IsNullOrEmpty(userId) ? "root" : userId;
				var group = string.IsNullOrEmpty(groupId) ? user : groupId;
				var name = string.IsNullOrEmpty(servicename) ? "cumulusmx" : servicename;

				Console.WriteLine($"Installing as a systemctld service '{name}' to run as userid {user}...");

				var serviceFile = $"/etc/systemd/system/{name}.service";

				// does the service file exist already?
				if (File.Exists(serviceFile))
				{
					// delete it and start again
					File.Delete(serviceFile);
				}

				// get the location of dotnet.exe - not so simple!
				// we have to get our own process, then find the main module filename

				var dotnetPath = Environment.ProcessPath;


				var appPath = AppDomain.CurrentDomain.BaseDirectory;


				string[] contents = [
					"[Unit]",
					"Description=CumulusMX service",
					"Documentation=https://cumuluswiki.org/a/Main_Page https://cumulus.hosiene.co.uk/",
					"Wants=network-online.target time-sync.target",
					"After=network-online.target time-sync.target",
					"",
					"[Service]",
					$"User={user}",
					$"Group={group}",
					$"WorkingDirectory={appPath[..^1]}",
					$"ExecStart=\"{dotnetPath}\" CumulusMX.dll -service" + (port == 8998 ? "" : " -port " + port) + (string.IsNullOrEmpty(lang) ? "" : " -lang " + lang),
					"Type=simple",
					"",
					"[Install]",
					"WantedBy=multi-user.target"
				];

				File.WriteAllLines(serviceFile, contents);

				return RunCommand("systemctl", "daemon-reload") == 0;
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error creating service: " + ex.Message);
				Console.WriteLine("You must run the service installation as an elevated user - 'sudo dotnet CumulusMX.dll -install -user myuser'");
			}
			return false;
		}

		public static bool UninstallLinux(string servicename)
		{
			var name = string.IsNullOrEmpty(servicename) ? "cumulusmx" : servicename;

			Console.WriteLine($"Uninstalling systemctld service '{name}'...");

			try
			{
				// stop and disable the service if it is running
				RunCommand("systemctl", "stop cumulusmx");
				RunCommand("systemctl", "disable cumulusmx");

				var serviceFile = $"/etc/systemd/system/{name}.service";

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
				if (File.Exists($"/usr/lib/systemd/system/{name}.service"))
				{
					// delete it
					File.Delete($"/usr/lib/systemd/system/{name}.service");
				}

				var ok = RunCommand("systemctl", "daemon-reload") == 0;
				ok = ok && RunCommand("systemctl", "reset-failed") == 0;

				return ok;
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error removing service '{name}': " + ex.Message);
				Console.WriteLine("You must run the service uninstall as an elevated user - 'sudo dotnet CumulusMX.dll -uninstall [-servicename xxxx]'");
				return false;
			}
		}

		public static bool IsElevated()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);
			else
				return Environment.GetEnvironmentVariable("EUID") == "0" || Environment.GetEnvironmentVariable("USER") == "root";
		}

		private static int RunCommand(string exe, string args, bool createWindow = false, bool useshell = false, bool elevated = false)
		{
			try
			{
				var startinfo = new ProcessStartInfo
				{
					FileName = exe,
					UseShellExecute = useshell,
					CreateNoWindow = createWindow,
					Arguments = args
				};

				if (elevated )
				{
					startinfo.Verb = "runas";
				}

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
