using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using CumulusMX.Extensions;

namespace DavisStation
{
    public abstract class DavisStationInterface
    {
        protected readonly ILogger _log;
        protected readonly log4net.ILog _dataLog = 
            log4net.LogManager.GetLogger("cumulusData", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        protected const int ACK = 6;
        protected const int LOOP_DATA_LENGTH = 99;
        protected const int LOOP_2_DATA_LENGTH = 99;

        protected DateTime _lastReceptionStatsTime;

        internal DavisStationInterface(ILogger log)
        {
            _log = log;
        }

        public bool Connected { get; protected set; } = false;

        internal abstract void Connect();
        internal abstract string GetFirmwareVersion();
        internal abstract void GetReceptionStats();
        internal abstract void SetTime();
        internal abstract DateTime GetTime();
        internal abstract void CloseConnection();
        internal abstract bool SendLoopCommand(string command);

        internal void GetReceptionStatsIfDue()
        {
            if (_lastReceptionStatsTime.AddMinutes(15) < DateTime.Now )
                GetReceptionStats();
        }

        protected void DecodeReceptionStats(string response)
        {
            var lastLF = response.LastIndexOf('\n');

            string receptionStats;
            if (lastLF <= 15) return;
            
            var len = lastLF - 5;
            receptionStats = response.Substring(6, len);
            
            try
            {
                var vals = receptionStats.Split(' ');

                TotalPacketsReceived = Convert.ToInt32(vals[0]);
                TotalPacketsMissed = Convert.ToInt32(vals[1]);
                NumberOfResyncs = Convert.ToInt32(vals[2]);
                MaxInARow = Convert.ToInt32(vals[3]);
                NumberCrcErrors = Convert.ToInt32(vals[4]);
            }
            catch (Exception ex)
            {
                _log.Error("Error Decoding Reception Statistics: " + ex.Message);
            }
        }

        public int NumberCrcErrors { get; set; }
        public int MaxInARow { get; set; }
        public int NumberOfResyncs { get; set; }
        public int TotalPacketsMissed { get; set; }
        public int TotalPacketsReceived { get; set; }

        public string ReceptionStatistics =>
            $@"Total Packets Received: {TotalPacketsReceived:######}
Total Packets Missed  : {TotalPacketsMissed:######}
Number of Resyncs     : {NumberOfResyncs:######}
Maximum in a Row      : {MaxInARow:######}
Number of CRC Errors  : {NumberCrcErrors:######}";

        protected internal bool TimeSetNeeded { get; set; } = false;

        internal void SetTimeIfNeeded()
        {
            if (TimeSetNeeded)
            {
                SetTime();
                TimeSetNeeded = false;
            }
        }

        internal abstract void SendBarRead();
        internal abstract byte[] GetLoop2Data();
        internal abstract byte[] GetLoopData();
        public abstract void Clear();
    }
}
