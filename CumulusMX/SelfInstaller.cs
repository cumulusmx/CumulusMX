using System.Configuration.Install;
using System.Reflection;

namespace CumulusMX
{
	public class SelfInstaller
	{
		private static readonly string _exePath = Assembly.GetExecutingAssembly().Location;
		public static bool InstallMe()
		{
			bool result;
			try
			{
				ManagedInstallerClass.InstallHelper(new string[] {
					SelfInstaller._exePath
				});
			}
			catch
			{
				result = false;
				return result;
			}
			result = true;
			return result;
		}

		public static bool UninstallMe()
		{
			bool result;
			try
			{
				ManagedInstallerClass.InstallHelper(new string[] {
					"/u", SelfInstaller._exePath
				});
			}
			catch
			{
				result = false;
				return result;
			}
			result = true;
			return result;
		}
	}
}
