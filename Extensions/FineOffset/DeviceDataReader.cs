using System;
using System.Collections.Generic;
using System.Text;
using CumulusMX.Extensions;

namespace FineOffset
{
    public class DeviceDataReader
    {
        private const int ADDR_READ_PERIOD = 16;
        private const int ADDR_CURRENT_DATA_POSITION = 30;
        private const int ADDR_RELATIVE_PRESSURE = 32;
        private readonly ILogger _log;
        private readonly FineOffsetDevice _device;
        private readonly bool _hasSolar;

        private const ushort DATA_START_ADDR = 256;
        private readonly ushort ENTRY_SIZE;
        private readonly ushort MAX_ADDRESS;
        private readonly ushort MAX_HISTORY_ENTRIES;

        public DeviceDataReader(ILogger log, FineOffsetDevice device, bool hasSolar)
        {
            this._log = log;
            this._device = device;
            if (!_device.IsOpen)
                _device.OpenDevice();
            this._hasSolar = hasSolar;

            if (_hasSolar)
            {
                ENTRY_SIZE = 20;
                MAX_ADDRESS = 65516;
                MAX_HISTORY_ENTRIES = 3264;
            }
            else
            {
                ENTRY_SIZE = 16;
                MAX_ADDRESS = 65520;
                MAX_HISTORY_ENTRIES = 4080;
            }
        }

        public double GetPressureOffset()
        {
            var data = new byte[32];
            _device.ReadAddress(32, data);
            double relpressure = (((data[1] & 0x3F) * 256) + data[0]) / 10.0f;
            double abspressure = (((data[3] & 0x3F) * 256) + data[2]) / 10.0f;
            return relpressure - abspressure;
        }

        public int GetReadPeriod()
        {
            var data = new byte[32];
            _device.ReadAddress(0, data);
            return data[16];
        }


        public ushort GetCurrentDataPosition()
        {
            var data = new byte[32];
            _device.ReadAddress(0, data);
            ushort address = (ushort)((data[31] * 256) + data[30]);
            return address;
        }

        public ushort GetNextDataPosition(ushort currentPosition)
        {
            ushort nextPosition = (ushort)(currentPosition - ENTRY_SIZE);
            if (nextPosition < DATA_START_ADDR)
                nextPosition = MAX_ADDRESS; // wrap around
            return nextPosition;
        }


        public DataEntry GetDataEntry(int address, DateTime timestamp, double pressureOffset)
        {
            //   Curr Reading Loc
            // 0  Time Since Last Save
            // 1  Hum In
            // 2  Temp In
            // 3  "
            // 4  Hum Out
            // 5  Temp Out
            // 6  "
            // 7  Pressure
            // 8  "
            // 9  Wind Speed m/s
            // 10  Wind Gust m/s
            // 11  Speed and Gust top nibbles (Gust top nibble)
            // 12  Wind Dir
            // 13  Rain counter
            // 14  "
            // 15  status

            // 16 Solar (Lux)
            // 17 "
            // 18 "
            // 19 UV

            var data = new byte[32];
            _device.ReadAddress(address, data);

            var histData = new DataEntry();
            string msg = "Read logger entry for " + timestamp + " address " + address.ToString("X4") + ": ";
            int numBytes = 16;

            for (int i = 0; i < numBytes; i++)
            {
                msg += data[i].ToString("X2");
                msg += " ";
            }
            _log.Debug(msg);

            histData.Timestamp = timestamp;
            histData.Interval = data[0];
            if (data[1] == 255)
            {
                histData.InsideHumidity = 10;
            }
            else
            {
                histData.InsideHumidity = data[1];
            }

            if (data[4] == 255)
            {
                histData.OutsideHumidity = 10;
            }
            else
            {
                histData.OutsideHumidity = data[4];
            }
            double outtemp = ((data[5]) + (data[6] & 0x7F) * 256) / 10.0f;
            var sign = (byte)(data[6] & 0x80);
            if (sign == 0x80) outtemp = -outtemp;
            if (outtemp > -200) histData.OutsideTemperature = outtemp;
            histData.WindGust = (data[10] + ((data[11] & 0xF0) * 16)) / 10.0f;
            histData.WindSpeed = (data[9] + ((data[11] & 0x0F) * 256)) / 10.0f;
            histData.WindBearing = (int)(data[12] * 22.5f);
            histData.RainCounter = data[13] + (data[14] * 256);

            double intemp = ((data[2]) + (data[3] & 0x7F) * 256) / 10.0f;
            sign = (byte)(data[3] & 0x80);
            if (sign == 0x80) intemp = -intemp;
            histData.InsideTemperature = intemp;
            // Get pressure and convert to sea level
            histData.Pressure = (data[7] + ((data[8] & 0x3F) * 256)) / 10.0f + pressureOffset;
            histData.SensorContactLost = (data[15] & 0x40) == 0x40;

            if (_hasSolar)
            {
                histData.UVValue = data[19];
                histData.SolarValue = (data[16] + (data[17] * 256) + (data[18] * 65536)) / 10.0;
            }
            return histData;
        }



    }
}
