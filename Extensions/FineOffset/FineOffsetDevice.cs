using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using CumulusMX.Extensions;
using HidSharp;

namespace FineOffset
{
    public class FineOffsetDevice
    {
        private const int READ_TIME = 150;
        private HidDevice hidDevice;
        private HidStream stream;
        private readonly ILogger _log;
        private readonly int _vendorId;
        private readonly int _productId;
        private readonly bool _isOsx;

        public FineOffsetDevice(ILogger log, int vendorId, int productId, bool isOsx)
        {
            this._log = log;
            this._vendorId = vendorId;
            this._productId = productId;
            this._isOsx = isOsx;
        }


        public bool IsOpen => stream != null;

        public void OpenDevice()
        {
            try
            {
                var devicelist = DeviceList.Local;

                _log.Info("Looking for Fine Offset station, VendorID=0x" + _vendorId.ToString("X4") + " ProductID=0x" + _productId.ToString("X4"));

                hidDevice = devicelist.GetHidDeviceOrNull(vendorID: _vendorId, productID: _productId);

                _log.Info("Fine Offset station found");
                _log.Debug("Opening stream");
                stream = hidDevice.Open();

                _log.Debug("Stream opened");
                _log.Info("Connected to station");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to open device", ex);
            }
        }


        /// <summary>
        /// Read the bytes starting at 'address'
        /// </summary>
        /// <param name="address">The address of the data</param>
        /// <param name="buff">Where to return the data</param>
        public void ReadAddress(int address, byte[] buff)
        {
            _log.Debug("Reading address: " + address.ToString("X6"));
            var lowbyte = (byte)(address & 0xFF);
            var highbyte = (byte)(address >> 8);

            byte[] request;
            var response = new byte[9];
            int responseLength;
            int startByte;

            if (_isOsx)
            {
                request = new byte[] { 0xa1, highbyte, lowbyte, 0x20, 0xa1, highbyte, lowbyte, 0x20 };
                responseLength = 8;
                startByte = 0;
            }
            else
            {
                request = new byte[] { 0, 0xa1, highbyte, lowbyte, 0x20, 0xa1, highbyte, lowbyte, 0x20 };
                responseLength = 9;
                startByte = 1;
            }

            int ptr = 0;
            String rec_data = "";


            if (hidDevice != null)
            {
                stream.Write(request);
                Thread.Sleep(READ_TIME);
                for (int i = 1; i < 5; i++)
                {
                    //cumulus.LogMessage("Reading 8 bytes");
                    try
                    {
                        int count = stream.Read(response, 0, responseLength);
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Error reading data from station - it may need resetting", ex);
                    }

                    rec_data = " Data" + i + ": ";
                    for (int j = startByte; j < responseLength; j++)
                    {
                        rec_data += response[j].ToString("X2");
                        rec_data += " ";
                        buff[ptr++] = response[j];
                    }
                    _log.Debug("Received " + rec_data);
                }
            }
        }

    }
}
