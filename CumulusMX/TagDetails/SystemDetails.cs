using System;
using CumulusMX.Extensions;

namespace CumulusMX.TagDetails
{
    public class SystemDetails : ITagDetails
    {
        public string RegistrationName => "System";

        public string OsVersion => throw new NotImplementedException();
        public string OsLanguage => throw new NotImplementedException();
        public string SystemUpTime => throw new NotImplementedException();
        public string ProgramUpTime => throw new NotImplementedException();
        public string CpuName => throw new NotImplementedException();
        public string CpuCount => throw new NotImplementedException();
        public string MemoryStatus => throw new NotImplementedException();
        public string DisplayMode => throw new NotImplementedException();
        public string AllocatedMemory => throw new NotImplementedException();
        public string DiskSize => throw new NotImplementedException();
        public string DiskFree => throw new NotImplementedException();
        public string LatestError => throw new NotImplementedException();
        public string LatestErrorDate => throw new NotImplementedException();
        public string LatestErrorTime => throw new NotImplementedException();
        public string ErrorLight => throw new NotImplementedException();
        public string Version => throw new NotImplementedException();
        public string Build => throw new NotImplementedException();
        public string Update => throw new NotImplementedException();
        
    }
}
