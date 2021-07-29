using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using CumulusMX.Extensions;
using System.IO.Ports;
using Force.Crc32;
using System.Linq;
using System.Diagnostics;
using CumulusMX.Common;

namespace DavisStation
{
    public class DavisStationInterfaceSerial : DavisStationInterface
    {
        private readonly Stopwatch awakeStopWatch = new Stopwatch();

        private SerialPort _comPort;
        public string ComPortName { get; private set; }

        internal DavisStationInterfaceSerial(ILogger log,string comPortName) : base(log)
        {
            ComPortName = comPortName;
        }

        internal override void Connect()
        {
            byte[] readBuffer = new byte[1000];
            int bytesRead = 0;

            // Stop the awake timer
            awakeStopWatch.Stop();

            do
            {
                try
                {
                    if (_comPort != null && _comPort.IsOpen)
                    {
                        _comPort.Close();
                        _comPort.Dispose();
                    }
                }
                catch { }

                _log.Debug($"ConnectSerial: Connecting to the station on {ComPortName}");

                try
                {
                    _comPort = new SerialPort(ComPortName, 19200, Parity.None, 8, StopBits.One)
                    {
                        Handshake = Handshake.None,
                        DtrEnable = true,
                        ReadTimeout = 1000,
                        WriteTimeout = 1000
                    };

                    _comPort.Open();
                    _comPort.NewLine = "\n";
                }
                catch (IOException ex)
                {
                    _log.Error("ConnectSerial: Error opening communications port", ex);
                    throw new IOException("Failed to initialise COM port", ex);
                }
                catch (Exception ex)
                {
                    _log.Error(ex.Message);
                    //MessageBox.Show(ex.Message);
                }

                if (_comPort == null || !_comPort.IsOpen)
                {
                    _log.Info("ConnectSerial: Failed to connect to the station, waiting 30 seconds before trying again");
                    Thread.Sleep(30000);
                }

            } while (_comPort == null || !_comPort.IsOpen);

            try
            {
                // stop any loop data that may still be active
                _comPort.WriteLine("");
                Thread.Sleep(500);

                _log.Debug("ConnectSerial: Flushing input stream");
                _comPort.DiscardInBuffer();
                _comPort.DiscardOutBuffer();

                // now we have purged any data, test the connection
                int tryCount = 1;
                do
                {
                    // write TEST, we expect to get "\n\rTEST\n\r" back
                    _log.Debug($"ConnectSerial:: Sending TEST ({tryCount}) command");
                    _comPort.WriteLine("TEST");

                    // pause to allow time for a resonse
                    Thread.Sleep(500);
                    try
                    {
                        do
                        {
                            // Read the current byte
                            var ch = _comPort.ReadByte();
                            readBuffer[bytesRead] = (byte)ch;
                            bytesRead++;
                        } while (_comPort.BytesToRead > 0 || bytesRead < 8);

                        var resp = Encoding.ASCII.GetString(readBuffer);
                        _log.Data($"ConnectSerial: TEST ({tryCount}) received - '{BitConverter.ToString(readBuffer.Take(bytesRead).ToArray())}'");

                        if (resp.Contains("TEST"))
                        {
                            _log.Debug($"ConnectSerial: TEST ({tryCount}) successful");
                            break;
                        }
                    }
                    catch (TimeoutException)
                    {
                        _log.Debug($"ConnectSerial: Timed out waiting for a response to TEST ({tryCount})");
                    }
                    catch (Exception ex)
                    {
                        _log.Debug("InitSerial: Error", ex);
                    }
                    tryCount++;

                } while (tryCount < 5);

                if (tryCount < 5)
                {
                    //awakeStopWatch.Restart();
                    _log.Info("ConnectSerial: Connection confirmed");
                }
            }
            catch (Exception ex)
            {
                _log.Info("ConnectSerial: Error", ex);
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
        private bool Wake()
        {
            // Check if we haven't sent a command within the last two minutes - use 1:50 (110,000 ms) to be safe
            if (awakeStopWatch.IsRunning && awakeStopWatch.ElapsedMilliseconds < 110000)
            {
                _log.Debug("Wake: Not required");
                awakeStopWatch.Restart();
                return true;
            }

            try
            {
                _log.Debug("Wake: Starting");

                // Clear out both input and output buffers just in case something is in there already
                //_log.Info("bytes to read: "+serialPort.BytesToRead);
                _comPort.DiscardInBuffer();
                _comPort.DiscardOutBuffer();

                //_log.Info("Waking VP");
                // Put a newline character ('\n') out the serial port - the Writeline method terminates with a '\n' of its own
                _comPort.WriteLine("");
                //Thread.Sleep(1200);
                //serialPort.WriteLine("");
                // Wait for 1.2 seconds
                //Thread.Sleep(1200);

                var woken = false;
                int i = 1;
                int lastChar = 0;


                while (!woken && (i < 5 || _comPort.BytesToRead > 0))
                {
                    _log.Debug($"Wake: Sending wake-up newline ({i}/4)");

                    try
                    {
                        _comPort.DiscardInBuffer();

                        // Put a newline character ('\n') out the serial port - the Writeline method terminates with a '\n' of its own
                        _comPort.WriteLine("");

                        int thisChar;
                        do
                        {
                            thisChar = _comPort.ReadByte();

                            if (thisChar == CR && lastChar == LF)
                            {
                                woken = true;
                                break;
                            }

                            lastChar = thisChar;
                        } while (thisChar > -1);
                    }
                    catch (TimeoutException)
                    {
                        _log.Debug("Wake: Timed out waiting for response");
                        i++;
                    }
                }

                // VP found and awakened
                if (woken)
                {
                    // start the stopwatch
                    awakeStopWatch.Restart();

                    // Now that the VP is awake, clean out the input buffer again
                    _comPort.DiscardInBuffer();
                    _comPort.DiscardOutBuffer();

                    _log.Debug("Wake: Woken");
                    return (true);
                }

                _log.Info("Wake: *** VP2 Not woken");
                return (false);
            }
            catch (Exception ex)
            {
                _log.Info("WakeVP: Error", ex);
                return (false);
            }
        }


        internal override string GetFirmwareVersion()
        {
            _log.Info("Reading firmware version");
            var response = "";
            string data = "";
            int ch;

            // expected response - <LF><CR>OK<LF><CR>1.73<LF><CR>


            var commandString = "NVER";
            if (Wake())
            {
                try
                {
                    _comPort.DiscardInBuffer();
                    _comPort.WriteLine(commandString);

                    if (WaitForOK())
                    {
                        // Read the response
                        do
                        {
                            // Read the current character
                            ch = _comPort.ReadChar();
                            response += Convert.ToChar(ch);
                            data += ch.ToString("X2") + "-";
                        } while (ch != CR);

                        data = data.Remove(data.Length - 1);
                    }
                }
                catch (TimeoutException)
                {
                    _log.Info("GetFirmwareVersion: Timed out waiting for a response");
                }
                catch (Exception ex)
                {
                    _log.Info("GetFirmwareVersion: Error", ex);
                    _log.Debug("GetFirmwareVersion: Attempting to reconnect to logger");
                    Connect();
                    _log.Debug("GetFirmwareVersion: Reconnected to logger");
                }
            }

            _log.Data($"GetFirmwareVersion: Received - {data}");

            return response.Length >= 5 ? response[0..^2] : "???";
        }

        internal override void GetReceptionStats()
        {
            // e.g. <LF><CR>OK<LF><CR> 21629 15 0 3204 128<LF><CR>
            //       0   1  23 4   5  6
            _log.Info("Reading reception stats");
            _lastReceptionStatsTime = DateTime.Now;
            var response = "";
            var bytesRead = 0;
            byte[] readBuffer = new byte[40];
            int ch;

            const string commandString = "RXCHECK";
            if (Wake())
            {
                try
                {
                    _comPort.WriteLine(commandString);

                    if (WaitForOK())
                    {
                        // Read the response -  21629 15 0 3204 128<LF><CR>
                        do
                        {
                            // Read the current character
                            ch = _comPort.ReadChar();
                            response += Convert.ToChar(ch);
                            readBuffer[bytesRead] = (byte)ch;
                            bytesRead++;
                        } while (ch != CR);
                    }
                }
                catch (TimeoutException)
                {
                    _log.Info("GetReceptionStats: Timed out waiting for a response");
                }
                catch (Exception ex)
                {
                    _log.Info("GetReceptionStats: Error - " + ex.Message);
                    _log.Debug("GetReceptionStats: Attempting to reconnect to logger");
                    Connect();
                    _log.Debug("GetReceptionStats: Reconnected to logger");
                }
            }

            _log.Data("GetReceptionStats: Received - " + BitConverter.ToString(readBuffer.Take(bytesRead).ToArray()));

            response = response.Length > 10 ? response.Substring(0, response.Length - 2) : "0 0 0 0 0";

            _log.Debug($"GetReceptionStats: {response}");

            DecodeReceptionStats(response);
        }

        internal override void CheckLoggerInterval()
        {
            _log.Info("CheckLoggerInterval: Reading logger interval");
            var bytesRead = 0;
            byte[] readBuffer = new byte[40];

            // response should be (5 mins):
            // ACK  VAL CKS1 CKS2
            // 0x06-05-50-3F

            const string commandString = "EEBRD 2D 01";

            if (Wake())
            {
                try
                {
                    _comPort.WriteLine(commandString);

                    if (!WaitForACK())
                    {
                        _log.Error("CheckLoggerInterval: No ACK in response to requesting logger interval");
                        return;
                    }

                    // Read the response
                    do
                    {
                        // Read the current character
                        var ch = _comPort.ReadChar();
                        readBuffer[bytesRead] = (byte)ch;
                        bytesRead++;
                    } while (bytesRead < 3);
                }
                catch (TimeoutException)
                {
                    _log.Error("CheckLoggerInterval: Timed out waiting for a response");
                }
                catch (Exception ex)
                {
                    _log.Error("CheckLoggerInterval: Error - " + ex.Message);
                    awakeStopWatch.Stop();
                }
            }

            _log.Data("CheckLoggerInterval: Received - " + BitConverter.ToString(readBuffer.Take(bytesRead).ToArray()));

            _log.Debug($"CheckLoggerInterval: Station logger interval is {readBuffer[0]} minutes");

            //TODO: Needs the log file data reporter interval
            /*
            if (bytesRead > 0 && readBuffer[0] !=  ReportInterval)
            {
                var msg = $"** WARNING: Your station logger interval {readBuffer[0]} mins does not match your Cumulus MX loggung interval {ReportInterval} mins";
                //cumulus.LogConsoleMessage(msg);
                _log.Info("CheckLoggerInterval: " + msg);
            }
            */
        }


        private bool WaitForOK()
        {
            // Waits for OK<LF><CR>
            var readBuffer = new StringBuilder();

            _log.Debug("WaitForOK: Wait for OK");
            do
            {
                try
                {
                    // Read the current character
                    readBuffer.Append((char)_comPort.ReadChar());
                }
                catch (TimeoutException)
                {
                    _log.Debug("WaitForOK: Timed out");
                    return false;
                }
                catch (Exception ex)
                {
                    _log.Debug($"WaitForOK: Error - {ex.Message}");
                    _log.Debug("WaitForOK: Attempting to reconnect to logger");
                    Connect();
                    _log.Debug("WaitForOK: Reconnected to logger");
                    return false;
                }

            } while (readBuffer.ToString().IndexOf("OK\n\r") == -1);

            _log.Debug("WaitForOK: Found OK");
            return true;
        }

        private bool WaitForACK(int timeoutMs = -1)
        {
            int tryCount = 0;
            // Wait for the VP to acknowledge the the receipt of the command - sometimes we get a '\n\r'
            // in the buffer first or no response is given.  If all else fails, try again.
            _log.Debug("WaitForACK: Wait for ACK");

            _comPort.ReadTimeout = timeoutMs > -1 ? timeoutMs : 1000;

            do
            {
                try
                {
                    tryCount++;
                    // Read the current character
                    var currChar = _comPort.ReadChar();
                    switch (currChar)
                    {
                        case ACK:
                            _log.Debug("WaitForACK: ACK received");
                            return true;
                        case NACK:
                            _log.Debug("WaitForACK: NACK received");
                            return false;
                        case CANCEL:
                            _log.Debug("WaitForACK: CANCEL received");
                            return false;
                        case LF:
                        case CR:
                            _log.Data("WaitForACK: Discarding CR or LF - " + currChar.ToString("X2"));
                            tryCount--;
                            break;
                        default:
                            _log.Data($"WaitForACK: ({tryCount}) Received - {currChar:X2}");
                            break;
                    }
                }
                catch (TimeoutException)
                {
                    _log.Debug($"WaitForAck: ({tryCount}) Timed out");
                }
                catch (Exception ex)
                {
                    _log.Debug($"WaitForAck: {tryCount} Error - {ex.Message}");
                    _log.Debug("WaitForAck: Attempting to reconnect to logger");
                    Connect();
                    _log.Debug("WaitForAck: Reconnected to logger");
                }
            } while (tryCount < 2);

            _log.Debug("WaitForAck: timed out");
            return false;
        }


        internal override DateTime GetTime()
        {
            byte[] readBuffer = new byte[8];
            var bytesRead = 0;

            // Expected resonse - <ACK><42><17><15><28><11><98><2 Bytes of CRC>
            //                     06   ss  mm  hh  dd  MM  yy

            _log.Info("Reading console time");

            const string commandString = "GETTIME";

            if (Wake())
            {
                try
                {
                    _comPort.WriteLine(commandString);

                    if (!WaitForACK())
                    {
                        _log.Error("GetTime: No ACK");
                        return DateTime.MinValue;
                    }

                    // Read the time
                    do
                    {
                        // Read the current character
                        var ch = _comPort.ReadChar();
                        readBuffer[bytesRead] = (byte)ch;
                        bytesRead++;
                    } while (bytesRead < 8);
                }
                catch (TimeoutException)
                {
                    _log.Error("GetTime: Timed out waiting for a response");
                    return DateTime.MinValue;
                }
                catch (Exception ex)
                {
                    _log.Info("GetTime: Error - " + ex.Message);
                    return DateTime.MinValue;
                }
            }

            _log.Data("GetTime: Received - " + BitConverter.ToString(readBuffer.Take(bytesRead).ToArray()));
            if (bytesRead != 8)
            {
                _log.Error("GetTime: Expected 8 bytes, got " + bytesRead);
            }
            // CRC doesn't seem to compute?
            //else if (!crcOK(buffer))
            //{
            //	cumulus.LogMessage("getTime: Invalid CRC!");
            //}
            else
            {
                try
                {
                    return new DateTime(readBuffer[5] + 1900, readBuffer[4], readBuffer[3], readBuffer[2], readBuffer[1], readBuffer[0]);
                }
                catch (Exception)
                {
                    _log.Error("GetTime: Error in time format");
                }
            }
            return DateTime.MinValue;
        }

        internal override void SetTime()
        {
            _log.Info("Setting console time");

            var commandString = "SETTIME";

            try
            {
                if (Wake())
                {
                    _comPort.WriteLine(commandString);

                    //Thread.Sleep(200);

                    // wait for the ACK
                    // wait for the ACK
                    if (!WaitForACK())
                    {
                        _log.Error("SetTime: No ACK to SETTIME - Not setting the time");
                        return;
                    }
                }

                var now = DateTime.Now;

                var buffer = new byte[8];

                buffer[0] = (byte)now.Second;
                buffer[1] = (byte)now.Minute;
                buffer[2] = (byte)now.Hour;
                buffer[3] = (byte)now.Day;
                buffer[4] = (byte)now.Month;
                buffer[5] = (byte)(now.Year - 1900);

                // calculate and insert CRC

                //TODO: SetTime is failing, possibly a bad CRC being applied?

                var dataCopy = new byte[6];

                Array.Copy(buffer, dataCopy, 6);
                var crc = Crc32Algorithm.Compute(dataCopy);

                buffer[6] = (byte)(crc / 256);
                buffer[7] = (byte)(crc % 256);

                // send the data
                _comPort.Write(buffer, 0, 8);

                // wait for the ACK
                if (WaitForACK())
                {
                    _log.Info("SetTime: Console time set OK");
                }
                else
                {
                    _log.Error("SetTime: Error, console time set failed");
                }
            }
            catch (Exception ex)
            {
                _log.Debug("SetTime: Error", ex);
            }
        }

        internal override bool SendLoopCommand(string commandString)
        {
            bool foundAck = false;

            _log.Info("SendLoopCommand: Starting - " + commandString);

            try
            {
                //TODO: How is a program stop signalled?
                //if (_comPort.IsOpen && !stop)
                if (_comPort.IsOpen)
                {
                    Wake();

                    int passCount = 1;
                    const int maxPasses = 4;

                    // Clear the input buffer
                    _comPort.DiscardInBuffer();
                    // Clear the output buffer
                    _comPort.DiscardOutBuffer();

                    // Try the command until we get a clean ACKnowledge from the VP.  We count the number of passes since
                    // a timeout will never occur reading from the sockets buffer.  If we try a few times (maxPasses) and
                    // we get nothing back, we assume that the connection is broken

                    //TODO: How is a program stop signalled?
                    //while (!foundAck && passCount < maxPasses && !stop)
                    while (!foundAck && passCount < maxPasses)
                    {
                        // send the LOOP n command
                        _log.Debug("SendLoopCommand: Sending command " + commandString + ",  attempt " + passCount);
                        _comPort.WriteLine(commandString);

                        _log.Debug("SendLoopCommand: Wait for ACK");
                        // Wait for the VP to acknowledge the the receipt of the command - sometimes we get a '\n\r'
                        // in the buffer first or no response is given.  If all else fails, try again.
                        foundAck = WaitForACK();
                        passCount++;
                    }

                    // return result to indicate success or otherwise
                    if (foundAck)
                        return true;

                    // Failed to get a response from the loop command after all the retries, try resetting the connection
                    _log.Debug($"SendLoopCommand: Failed to get a response after {passCount - 1} trys, reconnecting the station");
                    Connect();
                    _log.Debug("SendLoopCommand: Reconnected to station");
                }
            }
            catch (Exception ex)
            {
                //TODO: how is a program stop signalled?
                //if (stop)
                //    return false;

                _log.Info("SendLoopCommand: Error sending LOOP command [" + commandString.Replace("\n", "") + "]: " + ex.Message);
                _log.Debug("SendLoopCommand: Attempting to reonnect to station");
                Connect();
                _log.Debug("SendLoopCommand: Reconnected to station");
                return false;
            }
            // if we get here it must have failed
            return false;
        }

        internal override void SendBarRead()
        {
            _log.Debug("Sending BARREAD");

            string response = "";
            var bytesRead = 0;
            byte[] readBuffer = new byte[64];

            // Expected response = "\n\rOK\n\rNNNNN\n\r" - Where NNNNN = ASCII pressure, inHg * 1000

            const string commandString = "BARREAD";

            if (Wake())
            {
                try
                {
                    _comPort.WriteLine(commandString);

                    if (WaitForOK())
                    {
                        // Read the response
                        do
                        {
                            // Read the current character
                            var ch = _comPort.ReadChar();
                            response += Convert.ToChar(ch);
                            readBuffer[bytesRead] = (byte)ch;
                            bytesRead++;

                        } while (bytesRead < 7);
                    }
                }
                catch (TimeoutException)
                {
                    _log.Error("SendBarRead: Timed out waiting for a response");
                }
                catch (Exception ex)
                {
                    _log.Error("SendBarRead: Error - " + ex.Message);
                    _log.Debug("SendBarRead: Attempting to reconnect to logger");
                    Connect();
                    _log.Debug("SendBarRead: Reconnected to logger");
                }
            }

            _log.Data("BARREAD Received - " + BitConverter.ToString(readBuffer.Take(bytesRead).ToArray()));
            if (response.Length > 2)
            {
                _log.Debug("BARREAD Received - " + response.Substring(0, response.Length - 2));
            }
        }

        internal override byte[] GetLoop2Data()
        {
            _log.Debug("LOOP2: Waiting for LOOP2 data");

            var loopString = new byte[LOOP_2_DATA_LENGTH];
            try
            {
                using (CommTimer tmrComm = new CommTimer())
                {
                    tmrComm.Start(3000);

                    while (_comPort.BytesToRead < LOOP_2_DATA_LENGTH && !tmrComm.timedout)
                    {
                        // Wait a short period to allow more data into the buffer
                        Thread.Sleep(10);
                    }
                    tmrComm.Stop();
                }

                if (_comPort.BytesToRead < LOOP_2_DATA_LENGTH )
                {
                    // all data not received
                    _log.Warn($"LOOP2: Expected data not received, expected 99 bytes got {_comPort.BytesToRead}");
                    return new byte[]{};
                }

                // Read the data from the buffer into the array
                _comPort.Read(loopString, 0, LOOP_2_DATA_LENGTH);
            }
            catch (TimeoutException)
            {
                _log.Error("LOOP2: Timed out waiting for LOOP2 data");
                return new byte[] {};
            }
            catch (Exception ex)
            {
                _log.Error("LOOP2: Error - " + ex);
                _log.Debug("LOOP2: Attempting to reconnect to logger");
                Connect();
                _log.Debug("LOOP2: Reconnected to logger");
                return new byte[] {};
            }

            return loopString;
        }

        internal override byte[] GetLoopData()
        {
            var loopString = new byte[LOOP_DATA_LENGTH];

            // Wait until the buffer is full - we've received all the characters from the LOOP response,
            // including the final '\n'

            try
            {
                if (!_comPort.IsOpen)
                {
                    _log.Error("LOOP: Comm port is closed");
                    _log.Debug("LOOP: Attempting to reconnect to station");
                    Connect();
                    _log.Debug("LOOP: Reconnected to station");
                    return new byte[] {};
                }

                // wait for the buffer to fill
                using (var tmrComm = new CommTimer())
                {
                    tmrComm.Start(3000);
                    while (_comPort.BytesToRead < LOOP_DATA_LENGTH && !tmrComm.timedout)
                    {
                        Thread.Sleep(10);
                    }
                    tmrComm.Stop();
                }

                if (_comPort.BytesToRead < LOOP_DATA_LENGTH)
                {
                    // all data not received
                    _log.Info($"LOOP: Expected data not received, expected {LOOP_DATA_LENGTH + 1} bytes, got {_comPort.BytesToRead}");
                    return new byte[]{};
                }

                // Read the data from the buffer into the array
                _comPort.Read(loopString, 0, LOOP_DATA_LENGTH);
            }
            catch (TimeoutException)
            {
                _log.Error($"LOOP: Timed out waiting for LOOP data");
                return new byte[] {};
            }
            catch (Exception ex)
            {
                _log.Error("LOOP: Exception - " + ex);
                _log.Debug("LOOP: Attempting to reconnect to station");
                Connect();
                _log.Debug("LOOP: Reconnected to station");
                return new byte[] {};
            }

            _log.Data($"LOOP: Data: {BitConverter.ToString(loopString)}");

            return loopString;
        }

        public override void Clear()
        {
            _comPort.WriteLine("");
            Thread.Sleep(3000);
            // read off all data in the pipeline

            _log.Debug("Discarding bytes from pipeline: " + _comPort.BytesToRead);
            while (_comPort.BytesToRead > 0) _comPort.ReadByte();
        }

        internal override void CloseConnection()
        {
            try
            {
                _comPort.WriteLine("");
                _comPort.Close();
            }
            catch
            { }
        }
    }
}
