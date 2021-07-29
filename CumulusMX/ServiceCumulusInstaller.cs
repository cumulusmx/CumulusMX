using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;


namespace CumulusMX
{
	[RunInstaller(true)]
	public class ServiceCumulusInstaller : Installer
	{
		public ServiceCumulusInstaller()
		{
			ServiceProcessInstaller serviceProcessInstaller = new ServiceProcessInstaller();
			ServiceInstaller serviceInstaller = new ServiceInstaller();

			// Setup the Service Account type per your requirement
			serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
			serviceProcessInstaller.Username = null;
			serviceProcessInstaller.Password = null;

			serviceInstaller.ServiceName = "CumulusMX";
			serviceInstaller.DisplayName = "Cumulus MX Service";
			serviceInstaller.StartType = ServiceStartMode.Automatic;
			serviceInstaller.DelayedAutoStart = true;
			serviceInstaller.Description = "Runs Cumulus MX as a system service";

			Installers.Add(serviceProcessInstaller);
			Installers.Add(serviceInstaller);
		}
	}
}
