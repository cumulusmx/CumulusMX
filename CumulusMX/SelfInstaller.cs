using System.Configuration.Install;
using System.Reflection;

namespace CumulusMX
{
	public class SelfInstaller
	{
		private static readonly string ExePath = Assembly.GetExecutingAssembly().Location;
		public static bool InstallMe()
		{
			try
			{
				ManagedInstallerClass.InstallHelper(new string[] {
					SelfInstaller.ExePath
				});
			}
			catch
			{
				return false;
			}
			return true;
		}

		public static bool UninstallMe()
		{
			try
			{
				ManagedInstallerClass.InstallHelper(new string[] {
					"/u", SelfInstaller.ExePath
				});
			}
			catch
			{
				return false;
			}
			return true;
		}
	}
}
