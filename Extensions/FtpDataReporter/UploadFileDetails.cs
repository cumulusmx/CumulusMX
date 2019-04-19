using System;
using System.Collections.Generic;
using System.Text;

namespace FtpDataReporter
{
    public struct UploadFileDetails
    {
        public string local;
        public string remote;
        public bool process;
        public bool binary;
        public bool realtime;
        public bool FTP;
        public bool UTF8;
    }
}
