using System;
using System.Globalization;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using CumulusMX.Extensions;
using UnitsNet;

namespace CumulusMX.TagDetails
{
    public class SystemDetails : ITagDetails
    {
        public string RegistrationName => "System";

        public string OsType => Enum.GetName(typeof(PlatformID), Environment.OSVersion.Platform);
        public string OsVersion => Environment.OSVersion.Version.ToString();
        public string OperatingSystem => RuntimeInformation.OSDescription;
        public string OsLanguage => CultureInfo.CurrentCulture.DisplayName;
        //public TimeSpan SystemUpTime => throw new NotImplementedException(); // Don't think this is possible cross platform
        public TimeSpan ProgramUpTime => DateTime.Now - Process.GetCurrentProcess().StartTime;
        public string OsPlatform => Enum.GetName(typeof(Architecture), RuntimeInformation.OSArchitecture);
        public int CpuCount => Environment.ProcessorCount;
        //public string MemoryStatus =>  // Don't think this is possible cross platform
        //public string DisplayMode => throw new NotImplementedException(); // Don't think this is possible cross platform
        public Information AllocatedMemory => Information.FromBytes(Environment.WorkingSet);
        public Information DiskSize => Information.FromBytes(new DriveInfo(new FileInfo(Assembly.GetEntryAssembly().FullName).Directory.Root.FullName).TotalSize);
        public Information DiskFree => Information.FromBytes(new DriveInfo(new FileInfo(Assembly.GetEntryAssembly().FullName).Directory.Root.FullName).AvailableFreeSpace);
        public string LatestError => throw new NotImplementedException();
        public DateTime LatestErrorDate => throw new NotImplementedException();
        public bool ErrorLight => throw new NotImplementedException();
        public string Version => Assembly.GetEntryAssembly().GetName().Version.ToString();
        public string Build => Assembly.GetEntryAssembly().GetName().Version.Build.ToString();
        public DateTime BuildDate => new FileInfo(Assembly.GetExecutingAssembly().Location).LastWriteTime;
        public int DaysSince30Dec1899 => (int)(DateTime.Today - new DateTime(1899,12,30)).TotalDays);
    }
}
