using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.TagDetails;
using Xunit;

namespace CumulusMXTest
{
    public class TagTests
    {
        [Fact]
        public void TestSystemTagsErrors()
        {
            var tags = new SystemDetails();
            var regName = tags.RegistrationName;
            var version = tags.Version;
            var build = tags.Build;
            var buildDate = tags.BuildDate;
            var osVersion = tags.OsVersion;
            var os = tags.OperatingSystem;
            var osPlatform = tags.OsPlatform;
            var osType = tags.OsType;
            var osLanguage = tags.OsLanguage;
            var cpus = tags.CpuCount;
            var allocMem = tags.AllocatedMemory;
            var diskSize = tags.DiskSize;
            var diskFree = tags.DiskFree;
            var programUpTime = tags.ProgramUpTime;
            
        }
    }
}
