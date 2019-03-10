using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using CumulusMX.Extensions;
using System.IO.Ports;
using Force.Crc32;

namespace DavisStation
{
    public class DavisStationInterfaceSerial : DavisStationInterface
    {
        private SerialPort _comPort;
        private DateTime _lastReceptionStatsTime;
        public string ComPortName { get; private set; }

        internal DavisStationInterfaceSerial(ILogger log,string comPortName) : base(log)
        {
            ComPortName = comPortName;
        }

        internal override void Connect()
        {
            _log.Info("Serial device = " + ComPortName);

            _comPort = new SerialPort(ComPortName, 19200, Parity.None, 8, StopBits.One)
                { Handshake = Handshake.None, DtrEnable = true };

            //comport.DataReceived += new SerialDataReceivedEventHandler(portDataReceived);

            try
            {
                //comport.ReadTimeout = cumulus.DavisReadTimeout;
                _comPort.Open();
                _comPort.NewLine = "\n";
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
                //MessageBox.Show(ex.Message);
            }

            Connected = _comPort.IsOpen;

        }


        // In order to conserve battery power, the console spends as much time “asleep” as possible,
        // waking up only when required. Receiving a character on the serial port will cause the console to
        // wake up, but it might not wake up fast enough to read the first character correctly. Because of
        // this, you should always perform a wakeup procedure before sending commands to the console:
        // Console Wakeup procedure:
        //      1. Send a Line Feed character, ‘\n’ (decimal 10, hex 0x0A).
        //      2. Listen for a returned response of Line Feed and Carriage Return characters, (‘\n\r’).
        //      3. If there is no response within a reasonable interval (say 1.2 seconds), then try steps 1 and
        //         2 again up to a total of 3 attempts.
        //      4. If the console has not woken up after 3 attempts, then signal a connection error
        // After the console has woken up, it will remain awake for 2 minutes. Every time the VP
        // receives another character, the 2 minute timer will be reset.
        private bool Wake(SerialPort serialPort)
        {
            int passCount = 1, maxPasses = 4;


            try
            {
                // Clear out both input and output buffers just in case something is in there already
                //_log.Info("bytes to read: "+serialPort.BytesToRead);
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                //_log.Info("Waking VP");
                // Put a newline character ('\n') out the serial port - the Writeline method terminates with a '\n' of its own
                serialPort.WriteLine("");
                //Thread.Sleep(1200);
                //serialPort.WriteLine("");
                // Wait for 1.2 seconds 
                //Thread.Sleep(1200);

                var woken = false;

                for (var i = 0; i < 5; i++)
                {
                    if (serialPort.BytesToRead != 0)
                    {
                        woken = true;
                        //_log.Info("Woken: i="+i);
                        break;
                    }

                    Thread.Sleep(100);
                }

                // Now check and see if anything's been returned.  If nothing, try again with another newline.
                while (!woken && passCount < maxPasses)
                {
                    //_log.Info("No response, retry wake");
                    serialPort.WriteLine("");
                    for (var i = 0; i < 12; i++)
                    {
                        if (serialPort.BytesToRead != 0)
                        {
                            woken = true;
                            //_log.Info("Woken: i=" + i);
                            break;
                        }

                        Thread.Sleep(100);
                    }

                    passCount++;
                }

                // VP found and awakened
                if (woken)
                {
                    // Now that the VP is awake, clean out the input buffer again 
                    //byte[] data = new byte[20];
                    //string str = "";
                    //int n = serialPort.BytesToRead;
                    //_log.Info("bytes to read: " + n);
                    //comport.Read(data, 0, n);
                    //for (int i = 0; i < n; i++)
                    //{
                    //    str = str + data[i].ToString("X2") + " ";
                    //}
                    //_log.Info(str);
                    serialPort.DiscardInBuffer();
                    //_log.Info("Woken");
                    return true;
                }

                _log.Error("Unable to wake Davis station.");
                return false;
            }
            catch (Exception ex)
            {
                _log.Error("Error waking Davis terminal - " + ex);
                return false;
            }
        }


        internal override string GetFirmwareVersion()
        {
            _log.Info("Reading firmware version");
            var response = "";

                var commandString = "NVER";
                if (Wake(_comPort))
                {
                    _comPort.WriteLine(commandString);

                    Thread.Sleep(200);

                    // Read the response
                    var bytesRead = 0;
                    var buffer = new byte[20];
                    try
                    {
                        while (_comPort.BytesToRead > 0)
                        {
                            // Read the current character
                            var ch = _comPort.ReadChar();
                            response += Convert.ToChar(ch);
                            buffer[bytesRead] = (byte)ch;
                            bytesRead++;
                            //_log.Info("Received " + ch.ToString("X2"));
                        }

                        _dataLog.Info("Received 0x" + BitConverter.ToString(buffer));
                    }
                    catch (Exception ex)
                    {
                        _log.Error("GetFirmwareVersion: Error - " + ex.Message);
                    }
                }
            var okIndex = response.IndexOf("OK");

            if (okIndex > -1 && response.Length >= okIndex + 8) return response.Substring(okIndex + 4, 4);

            return "???";
        }

        internal override void GetReceptionStats()
        {
            // e.g. <LF><CR>OK<LF><CR> 21629 15 0 3204 128<LF><CR>
            //       0   1  23 4   5  6 
            _log.Info("Reading reception stats");
            _lastReceptionStatsTime = DateTime.Now;
            var response = "";

                var commandString = "RXCHECK";
                if (Wake(_comPort))
                {
                    _comPort.WriteLine(commandString);

                    Thread.Sleep(200);

                    // Read the response
                    var bytesRead = 0;
                    var buffer = new byte[40];
                    try
                    {
                        while (_comPort.BytesToRead > 0)
                        {
                            // Read the current character
                            var ch = _comPort.ReadChar();
                            response += Convert.ToChar(ch);
                            buffer[bytesRead] = (byte)ch;
                            bytesRead++;
                            //_log.Info("Received " + ch.ToString("X2"));
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Info("GetReceptionStats: Error - " + ex.Message);
                    }

                    _dataLog.Info("Received 0x" + BitConverter.ToString(buffer));
                }

                DecodeReceptionStats(response);

        }


        internal override DateTime GetTime()
        {
            _log.Info("Reading console time");

                var commandString = "GETTIME";
                if (Wake(_comPort))
                {
                    _comPort.WriteLine(commandString);

                    Thread.Sleep(200);

                    // Read the time
                    var bytesRead = 0;
                    var buffer = new byte[8];
                    while (_comPort.BytesToRead > 0 && bytesRead < 9)
                    {
                        // Read the current character
                        var ch = _comPort.ReadChar();
                        if (bytesRead > 0) buffer[bytesRead - 1] = (byte)ch;
                        bytesRead++;
                        //_log.Info("Received " + ch.ToString("X2"));
                    }

                    _dataLog.Info("Received 0x" + BitConverter.ToString(buffer));

                    if (bytesRead != 9)
                        _log.Info("Expected 9 bytes, got " + bytesRead);
                    else
                        try
                        {
                            return new DateTime(buffer[5] + 1900, buffer[4], buffer[3], buffer[2], buffer[1],
                                buffer[0]);
                        }
                        catch (Exception)
                        {
                            _log.Info("Error in time format");
                        }
                }
 
            return DateTime.MinValue;
        }

        internal override void SetTime()
        {
            _log.Info("Setting console time");

                var commandString = "SETTIME";
                if (Wake(_comPort))
                {
                    _comPort.WriteLine(commandString);

                    //Thread.Sleep(200);

                    // wait for the ACK
                    _log.Info("Wait for ACK...");
                    var ch = _comPort.ReadChar();

                    if (ch != ACK)
                    {
                        _log.Info("No ACK, received: 0x" + ch.ToString("X2"));
                        return;
                    }

                    _log.Info("ACK received");

                    var now = DateTime.Now;

                    var buffer = new byte[8];

                    buffer[0] = (byte)now.Second;
                    buffer[1] = (byte)now.Minute;
                    buffer[2] = (byte)now.Hour;
                    buffer[3] = (byte)now.Day;
                    buffer[4] = (byte)now.Month;
                    buffer[5] = (byte)(now.Year - 1900);

                    // calculate and insert CRC

                    var dataCopy = new byte[6];

                    Array.Copy(buffer, dataCopy, 6);
                    var crc = Crc32Algorithm.Compute(dataCopy);

                    buffer[6] = (byte)(crc / 256);
                    buffer[7] = (byte)(crc % 256);

                    // send the data
                    _comPort.Write(buffer, 0, 8);

                    // wait for the ACK
                    _log.Info("Wait for ACK...");
                    ch = _comPort.ReadChar();

                    if (ch != ACK)
                        _log.Warn("No ACK, received: 0x" + ch.ToString("X2"));
                    else
                        _log.Info("ACK received");
                }
        }


    }
}
