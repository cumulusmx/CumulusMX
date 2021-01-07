using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CumulusMX.Common;
using CumulusMX.Extensions;
using System.Diagnostics;
using Force.Crc32;
using System.Linq;

namespace DavisStation
{
    public class DavisStationInterfaceIp : DavisStationInterface
    {
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly int _disconnectInterval;
        private TcpClient _socket;
        private readonly int _responseTime;
        private readonly int _initWaitTime;
        private readonly Stopwatch awakeStopWatch = new Stopwatch();

        internal DavisStationInterfaceIp(ILogger log,IPAddress ipAddress,int port,int disconnectInterval,int responseTime,int initWaitTime) : base(log)
        {

            _ipAddress = ipAddress;
            _port = port;
            _disconnectInterval = disconnectInterval;
            _responseTime = responseTime;
            _initWaitTime = initWaitTime;
        }

        internal override void Connect()
        {
            awakeStopWatch.Stop();
            do
            {
                try
                {
                    if (_socket != null && _socket.Connected)
                        _socket.Close();
                }
                catch { }

                _log.Info("ConnectTCP: Connecting to the station");
                _log.Info("ConnectTCP: IP address = " + _ipAddress + " Port = " + _port);
                _log.Info("ConnectTCP: periodic disconnect = " + _disconnectInterval);

                _socket = OpenTcpPort();

                if (_socket == null)
                {
                    _log.Info("ConnectTCP: Failed to connect to the station, waiting 30 seconds before trying again");
                    Thread.Sleep(30000);
                }
            } while (_socket == null || !_socket.Connected);

            try
            {
                _log.Debug("ConnectTCP: Flushing input stream");
                NetworkStream stream = _socket.GetStream();
                stream.ReadTimeout = 2500;
                stream.WriteTimeout = 2500;

                // stop loop data
                stream.WriteByte(0x0A);

                Thread.Sleep(_initWaitTime);

                byte[] buffer1 = new byte[1000];
                byte[] buffer2 = new byte[buffer1.Length];

                while (stream.DataAvailable)
                {
                    // Read the current character
                    stream.ReadByte();
                    Thread.Sleep(10);
                }

                // now we have purged any data, test the connection
                int tryCount = 1;
                do
                {
                    var idx = 0;
                    // write TEST, we expect to get "TEST\n\r" back
                    _log.Debug($"ConnectTCP: Sending TEST ({tryCount}) command");
                    stream.Write(Encoding.ASCII.GetBytes("TEST\n"), 0, 5);

                    Thread.Sleep(_initWaitTime);

                    while (stream.DataAvailable)
                    {
                        var ch = stream.ReadByte();
                        if (idx < buffer1.Length)
                        {
                            buffer1[idx++] = (byte)ch;
                        }
                        else
                        {
                            Array.Copy(buffer1, 1, buffer2, 0, buffer1.Length);
                            buffer2[9] = (byte)ch;
                            Array.Copy(buffer2, buffer1, buffer1.Length);
                        }
                        Thread.Sleep(50);
                    }

                    var resp = Encoding.ASCII.GetString(buffer1);
                    _log.Debug($"ConnectTCP: TEST ({tryCount}) received - '{BitConverter.ToString(buffer1.Take(idx).ToArray())}'");

                    if (resp.Contains("TEST"))
                    {
                        _log.Debug($"ConnectTCP: TEST ({tryCount}) successful");
                        break;
                    }
                    tryCount++;
                } while (tryCount < 5);

                if (tryCount < 5)
                {
                    awakeStopWatch.Restart();
                    _log.Info("ConnectTCP: Connection confirmed");
                }
            }
            catch (Exception ex)
            {
                _log.Error("ConnectTCP: Error", ex);
            }
        }

        // Open a TCP socket.
        private TcpClient OpenTcpPort()
        {
            TcpClient client = null;
            var attempt = 0;

            // Creating the new TCP socket effectively opens it - specify IP address or domain name and port
            while (attempt < 5 && client == null)
            {
                attempt++;
                _log.Info("OpenTcpPort: Logger Connect attempt " + attempt);
                try
                {
                    client = new TcpClient(_ipAddress.ToString(), _port);

                    if (!client.Connected) client = null;

                    Thread.Sleep(1000);
                }
                catch(Exception ex)
                {
                    _log.Error("OpenTcpPort: Error", ex);
                }
            }

            // Set the timeout of the underlying stream
            if (client != null)
            {
                client.GetStream().ReadTimeout = _responseTime;
                client.GetStream().WriteTimeout = _responseTime;
                client.ReceiveTimeout = _responseTime;
                client.SendTimeout = _responseTime;
                _log.Info("OpenTcpPort: Logger reconnected");
            }
            else
            {
                _log.Error("OpenTcpPort: Logger connect failed");
            }

            return client;
        }

        private void Init()
        {
            try
            {
                _log.Info("Flushing input stream");
                var stream = _socket.GetStream();

                // stop loop data
                stream.WriteByte(0x0D);

                Thread.Sleep(_initWaitTime);

                while (stream.DataAvailable)
                {
                    // Read the current character
                    var ch = stream.ReadByte();
                    _dataLog.Info("Received 0x" + ch.ToString("X2"));

                    Thread.Sleep(200);
                }
            }
            catch (Exception ex)
            {
                _log.Info("init: Error - " + ex.Message);
            }
        }

        private bool Wake()
        {
            const int maxPasses = 3;
            int retryCount = 0;

            // Check if we haven't sent a command within the last two minutes - use 1:50 () to be safe
            if (awakeStopWatch.IsRunning && awakeStopWatch.ElapsedMilliseconds < 110000)
            {
                _log.Debug("Wake: Not required");
                awakeStopWatch.Restart();
                return true;
            }

            _log.Debug("Wake: Starting");

            try
            {
                NetworkStream stream;
                try
                {
                    stream = _socket.GetStream();
                }
                catch (Exception exStream)
                {
                    // There is a problem with the connection, try to disconnect/connect
                    // refer back to the socket field of this class
                    try
                    {
                        _log.Error("Wake: Problem with TCP connection - " + exStream.Message);
                        _socket.Client.Close(0);
                    }
                    finally
                    {
                        // Wait a second
                        Thread.Sleep(1000);
                        _socket = null;

                        _log.Debug("Wake: Attempting reconnect to logger");

                        // open a new connection
                        Init();
                    }

                    if (_socket == null)
                    {
                        return (false);
                    }

                    if (_socket.Connected)
                    {
                        stream = _socket.GetStream();
                    }
                    else
                    {
                        return false;
                    }
                }
                stream.ReadTimeout = 2500;
                stream.WriteTimeout = 2500;

                // Pause to allow any data to come in
                Thread.Sleep(250);

                // First flush the stream
                int cnt = 0;
                while (stream.DataAvailable)
                {
                    // Read the current character
                    stream.ReadByte();
                    cnt++;
                }
                if (cnt > 0)
                {
                    _log.Debug($"Wake: Flushed {cnt} suprious characters from input stream");
                }


                while (retryCount < 1)
                {
                    var passCount = 1;
                    int lastChar = 0;

                    while (passCount <= maxPasses)
                    {
                        try
                        {
                            _log.Debug($"Wake: Sending newline ({passCount}/{maxPasses})");
                            stream.WriteByte(LF);

                            Thread.Sleep(_responseTime);

                            int thisChar;
                            do
                            {
                                thisChar = stream.ReadByte();
                                if (thisChar == CR && lastChar == LF)
                                {
                                    // start the stopwatch
                                    awakeStopWatch.Restart();
                                    return true;
                                }

                                lastChar = thisChar;
                            } while (thisChar > -1);
                        }
                        catch (System.IO.IOException ex)
                        {
                            if (ex.Message.Contains("did not properly respond after a period"))
                            {
                                _log.Debug("Wake: Timed out waiting for a response");
                                passCount++;
                            }
                            else
                            {
                                _log.Debug("Wake: Problem with TCP connection " + ex.Message);
                                _log.Debug("Wake: Attempting reconnect to logger");
                                Init();
                                _log.Debug("Wake: Reconnected to logger");
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Debug("Wake: Problem with TCP connection", ex);
                            _log.Debug("Wake: Attempting reconnect to logger");
                            Init();
                            _log.Debug("Wake: Reconnected to logger");
                            return true;
                        }
                    }

                    // we only get here if we did not receive a LF/CR
                    // try reconnecting the TCP session
                    _log.Debug("Wake: Attempting reconnect to logger");
                    Init();
                    _log.Debug("Wake: Reconnected to logger");

                    retryCount++;

                    // Wait a second
                    Thread.Sleep(1000);
                }

                _log.Info("Wake: *** Console Not woken");
                return (false);
            }
            catch (Exception ex)
            {
                _log.Debug("Wake: Error - " + ex.Message);
                return (false);
            }
        }


        internal override string GetFirmwareVersion()
        {
            _log.Info("Reading firmware version");
            string response = "";
            string data = "";
            int ch;

            // expected response - <LF><CR>OK<LF><CR>1.73<LF><CR>

            var commandString = "NVER\n";
            if (Wake())
            {
                try
                {
                    NetworkStream stream = _socket.GetStream();
                    stream.ReadTimeout = _responseTime;
                    stream.WriteTimeout = _responseTime;

                    stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                    if (WaitForOK(stream))
                    {
                        do
                        {
                            // Read the current character
                            ch = stream.ReadByte();
                            response += Convert.ToChar(ch);
                            data += ch.ToString("X2") + "-";
                        } while (ch != CR);

                        data = data.Remove(data.Length - 1);
                    }
                }
                catch (System.IO.IOException ex)
                {
                    if (ex.Message.Contains("did not properly respond after a period of time"))
                    {
                       _log.Error("GetFirmwareVersion: Timed out waiting for a response");
                    }
                    else
                    {
                        _log.Error("GetFirmwareVersion: Error", ex);
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("GetFirmwareVersion: Error - " + ex.Message);
                    _log.Debug("GetFirmwareVersion: Attempting to reconnect to logger");
                    Init();
                    _log.Debug("GetFirmwareVersion: Reconnected to logger");
                }
            }

            _log.Data("GetFirmwareVersion: Received - " + data);

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

            var commandString = "RXCHECK\n";



            if (Wake())
            {
                try
                {
                    NetworkStream stream = _socket.GetStream();
                    stream.ReadTimeout = _responseTime;
                    stream.WriteTimeout = _responseTime;

                    stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                    if (WaitForOK(stream))
                    {
                        // Read the response -  21629 15 0 3204 128<LF><CR>
                        do
                        {
                            // Read the current character
                            ch = stream.ReadByte();
                            response += Convert.ToChar(ch);
                            readBuffer[bytesRead] = (byte)ch;
                            bytesRead++;
                        } while (ch != CR);
                    }
                }
                catch (System.IO.IOException ex)
                {
                    if (ex.Message.Contains("did not properly respond after a period"))
                    {
                        _log.Info("GetReceptionStats: Timed out waiting for a response");
                    }
                    else
                    {
                        _log.Error("GetReceptionStats: Error - " + ex.Message);
                        _log.Debug("GetReceptionStats: Attempting to reconnect to logger");
                        Init();
                        _log.Debug("GetReceptionStats: Reconnected to logger");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("GetReceptionStats: Error - " + ex.Message);
                    _log.Debug("GetReceptionStats: Attempting to reconnect to logger");
                    Init();
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

            const string commandString = "EEBRD 2D 01\n";

            if (Wake())
            {
                try
                {
                    NetworkStream stream = _socket.GetStream();
                    stream.ReadTimeout = _responseTime;
                    stream.WriteTimeout = _responseTime;

                    stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                    if (!WaitForACK(stream))
                    {
                        _log.Error("CheckLoggerInterval: No ACK in response to requesting logger interval");
                        return;
                    }

                    do
                    {
                        // Read the current character
                        var ch = stream.ReadByte();
                        readBuffer[bytesRead] = (byte)ch;
                        bytesRead++;
                        //cumulus.LogMessage("Received " + ch.ToString("X2"));
                    } while (bytesRead < 3);
                }
                catch (System.IO.IOException ex)
                {
                    if (ex.Message.Contains("did not properly respond after a period"))
                    {
                        _log.Error("CheckLoggerInterval: Timed out waiting for a response");
                    }
                    else
                    {
                        _log.Error("CheckLoggerInterval: Error - " + ex.Message);
                        awakeStopWatch.Stop();
                    }
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

        private bool WaitForOK(NetworkStream stream)
        {
            // Waits for OK<LF><CR>
            var readBuffer = new StringBuilder();

            _log.Debug("WaitForOK: Wait for OK");
            Thread.Sleep(_responseTime);

            do
            {
                try
                {
                    // Read the current character
                    readBuffer.Append((char)stream.ReadByte());
                }
                catch (System.IO.IOException ex)
                {
                    if (ex.Message.Contains("did not properly respond after a period"))
                    {
                        _log.Debug("WaitForOK: Timed out");
                        _log.Debug($"WaitForOK: Received - {BitConverter.ToString(Encoding.UTF8.GetBytes(readBuffer.ToString()))}");
                        return false;
                    }

                    _log.Debug($"WaitForOK: Error - {ex.Message}");
                    _log.Debug($"WaitForOK: Received - {BitConverter.ToString(Encoding.UTF8.GetBytes(readBuffer.ToString()))}");
                    _log.Debug("WaitForOK: Attempting to reconnect to logger");
                    Init();
                    _log.Debug("WaitForOK: Reconnected to logger");
                    return false;
                }
                catch (Exception ex)
                {
                    _log.Error($"WaitForOK: Error", ex);
                    _log.Debug("WaitForOK: Attempting to reconnect to logger");
                    Init();
                    _log.Debug("WaitForOK: Reconnected to logger");
                    return false;
                }

            } while (readBuffer.ToString().IndexOf("OK\n\r") == -1);
            _log.Debug("WaitForOK: Found OK");
            return true;
        }

        private bool WaitForACK(NetworkStream stream, int timeoutMs = -1)
        {
            int tryCount = 0;

            // Wait for the VP to acknowledge the the receipt of the command - sometimes we get a '\n\r'
            // in the buffer first or no response is given.  If all else fails, try again.
            _log.Debug("WaitForACK: Starting");

            Thread.Sleep(_responseTime);

            if (timeoutMs > -1)
            {
                stream.ReadTimeout = timeoutMs;
            }

            do
            {
                try
                {
                    tryCount++;
                    // Read the current character
                    var currChar = stream.ReadByte();
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
                            _log.Debug("WaitForACK: Discarding CR or LF - " + currChar.ToString("X2"));
                            tryCount--;
                            break;
                        default:
                            _log.Debug("WaitForACK: Received - " + currChar.ToString("X2"));
                            break;
                    }
                }
                catch (System.IO.IOException ex)
                {
                    if (ex.Message.Contains("did not properly respond after a period"))
                    {
                        _log.Debug($"WaitForAck: timed out, attempt {tryCount}");
                    }
                    else
                    {
                        _log.Debug($"WaitForAck: {tryCount} Error - {ex.Message}");
                        _log.Debug("WaitForAck: Attempting to reconnect to logger");
                        Init();
                        _log.Debug("WaitForAck: Reconnected to logger");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error($"WaitForAck: {tryCount} Error", ex);
                    _log.Debug("WaitForAck: Attempting to reconnect to logger");
                    Init();
                    _log.Debug("WaitForAck: Reconnected to logger");
                }
                finally
                {
                    if (timeoutMs > -1)
                    {
                        stream.ReadTimeout = 2500;
                    }
                }
            } while (tryCount < 2);

            _log.Debug("WaitForAck: Timed out");
            return false;
        }


        internal override DateTime GetTime()
        {
            byte[] readBuffer = new byte[8];
            var bytesRead = 0;

            // Expected resonse - <ACK><42><17><15><28><11><98><2 Bytes of CRC>
            //                     06   ss  mm  hh  dd  MM  yy

            _log.Info("Reading console time");

            const string commandString = "GETTIME\n";

            if (Wake())
            {
                try
                {
                    NetworkStream stream = _socket.GetStream();
                    stream.ReadTimeout = 2500;
                    stream.WriteTimeout = 2500;

                    stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                    if (!WaitForACK(stream))
                    {
                        _log.Error("GetTime: No ACK - wait a little longer");
                        if (!WaitForACK(stream))
                        {
                            _log.Error("GetTime: No ACK, returning");
                            return DateTime.MinValue;
                        }
                    }

                    // Read the time
                    do
                    {
                        // Read the current character
                        readBuffer[bytesRead] = (byte)stream.ReadByte();
                        bytesRead++;
                    } while (bytesRead < 8);
                }
                catch (System.IO.IOException ex)
                {
                    if (ex.Message.Contains("did not properly respond after a period"))
                    {
                        _log.Error("GetTime: Timed out waiting for a response");
                    }
                    else
                    {
                        _log.Error("GetTime: Error - " + ex.Message);
                    }
                    return DateTime.MinValue;
                }
                catch (Exception ex)
                {
                    _log.Error("GetTime: Error - " + ex.Message);
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
            NetworkStream stream = null;

            _log.Info("Setting console time");

            var commandString = "SETTIME\n";

            try
            {
                if (Wake())
                {
                    stream = _socket.GetStream();
                    stream.ReadTimeout = 2500;
                    stream.WriteTimeout = 2500;
                    stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                    // wait for the ACK
                    if (!WaitForACK(stream))
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

                var dataCopy = new byte[6];

                Array.Copy(buffer, dataCopy, 6);
                var crc = Crc32Algorithm.Compute(dataCopy);

                buffer[6] = (byte)(crc / 256);
                buffer[7] = (byte)(crc % 256);

                if (stream != null)
                {
                    stream.Write(buffer, 0, buffer.Length);

                    if (WaitForACK(stream))
                    {
                        _log.Info("SetTime: Console time set OK");
                    }
                    else
                    {
                        _log.Error("SetTime: Error, console time set failed");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error("SetTime: Error", ex);
            }
        }

        internal override bool SendLoopCommand(string commandString)
        {
            bool foundAck = false;
            int passCount = 1;
            const int maxPasses = 4;

            _log.Info("SendLoopCommand: Starting - " + commandString.Replace("\n", ""));

            try
            {
                //TODO: How is a program stop signalled?
                //if (_comPort.IsOpen && !stop)

                if (!_socket.Connected)
                {
                    _log.Error("SendLoopCommand: Error, TCP not connected!");
                    _log.Debug("SendLoopCommand: Attempting to reconnect to logger");
                    Init();
                    _log.Debug("SendLoopCommand: Reconnected to logger");
                    return false;
                }

                var stream = _socket.GetStream();

                // flush the input stream
                stream.WriteByte(10);

                Thread.Sleep(_responseTime);

                while (stream.DataAvailable)
                {
                    stream.ReadByte();
                }

                // Try the command until we get a clean ACKnowledge from the VP.  We count the number of passes since
                // a timeout will never occur reading from the sockets buffer.  If we try a few times (maxPasses) and
                // we get nothing back, we assume that the connection is broken

                //while (!foundAck && passCount < maxPasses && !stop)
                while (!foundAck && passCount < maxPasses)
                {
                    // send the LOOP n command
                    _log.Debug("SendLoopCommand: Sending command - " + commandString.Replace("\n", "") + ", attempt " + passCount);
                    stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                    _log.Debug("SendLoopCommand: Wait for ACK");
                    // Wait for the VP to acknowledge the the receipt of the command - sometimes we get a '\n\r'
                    // in the buffer first or no response is given.  If all else fails, try again.
                    foundAck = WaitForACK(stream);
                    passCount++;
                }

                if (foundAck) return true;

                // Failed to get a response from the loop command after all the retries, try resetting the connection
                _log.Debug($"SendLoopCommand: Failed to get a response after {passCount - 1} trys, reonnecting the station");
                Init();
                _log.Debug("SendLoopCommand: Reconnected to station");

            }
            catch (Exception ex)
            {
                //if (stop) return false;

                _log.Error("SendLoopCommand: Error sending LOOP command [" + commandString.Replace("\n", "") + "]: " + ex.Message);
                _log.Debug("SendLoopCommand: Attempting to reconnect to station");
                Init();
                _log.Debug("SendLoopCommand: Reconnected to station");
                return false;
            }

            // if we get here it has failed
            return false;
        }

        internal override void SendBarRead()
        {
            _log.Debug("Sending BARREAD");

            string response = "";
            var bytesRead = 0;
            byte[] readBuffer = new byte[64];

            // Expected response = "\n\rOK\n\rNNNNN\n\r" - Where NNNNN = ASCII pressure, inHg * 1000

            const string commandString = "BARREAD\n";
            if (Wake())
            {
                try
                {
                    var stream = _socket.GetStream();

                    stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                    if (WaitForOK(stream))
                    {
                        do
                        {
                            // Read the current character
                            var ch = stream.ReadByte();
                            response += Convert.ToChar(ch);
                            readBuffer[bytesRead] = (byte)ch;
                            bytesRead++;
                            //cumulus.LogMessage("Received " + ch.ToString("X2"));
                        } while (stream.DataAvailable);
                    }
                }
                catch (System.IO.IOException ex)
                {
                    if (ex.Message.Contains("did not properly respond after a period"))
                    {
                        _log.Error("SendBarRead: Timed out waiting for a response");
                    }
                    else
                    {
                        _log.Error("SendBarRead: Error - " + ex.Message);
                        _log.Debug("SendBarRead: Attempting to reconnect to logger");
                        Init();
                        _log.Debug("SendBarRead: Reconnected to logger");
                    }
                }
                catch (Exception ex)
                {
                    _log.Error("SendBarRead: Error - " + ex.Message);
                    _log.Debug("SendBarRead: Attempting to reconnect to logger");
                    Init();
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
                // wait for the buffer to fill
                using (CommTimer tmrComm = new CommTimer())
                {
                    tmrComm.Start(3000);

                    while (_socket.Available < LOOP_2_DATA_LENGTH && !tmrComm.timedout)
                    {
                        Thread.Sleep(10);
                    }
                    tmrComm.Stop();
                }

                if (_socket.Available < LOOP_2_DATA_LENGTH)
                {
                    _log.Warn($"LOOP2: Expected data not received, expected 99 bytes got {_socket.Available}");
                    return new byte[] { };
                }

                // Read the first 99 bytes of the buffer into the array
                _socket.GetStream().Read(loopString, 0, LOOP_2_DATA_LENGTH);
            }
            catch (System.IO.IOException ex)
            {
                if (ex.Message.Contains("did not properly respond after a period"))
                {
                    _log.Error("LOOP2: Timed out waiting for LOOP2 data");
                }

                _log.Error("LOOP2: Data: Error - " + ex.Message);
                _log.Debug("LOOP2: Attempting to reconnect to logger");
                Init();
                _log.Debug("LOOP2: Reconnected to logger");
                return new byte[] {};
            }
            catch (Exception ex)
            {
                _log.Error("LOOP2: Data: Error - " + ex.Message);
                _log.Debug("LOOP2: Attempting to reconnect to logger");
                Init();
                _log.Debug("LOOP2: Reconnected to logger");
                return new byte[] {};
            }

            return loopString;
        }

        internal override byte[] GetLoopData()
        {
            var loopString = new byte[LOOP_DATA_LENGTH];

            try
            {
                DoPeriodicDisconnect();
            }
            catch (Exception ex)
            {
                _log.Error("Error during periodic disconnect. Error: " + ex);
                return new byte[] { };
            }

            try
            {
                using (var tmrComm = new CommTimer())
                {
                    tmrComm.Start(3000);
                    while (_socket.Available < LOOP_DATA_LENGTH && !tmrComm.timedout)
                    {
                        Thread.Sleep(10);
                    }
                    tmrComm.Stop();
                }

                if (_socket.Available < LOOP_DATA_LENGTH)
                {
                    _log.Error($"LOOP: Expected data not received, expected 99 bytes, got {_socket.Available}");
                    return new byte[] {};
                }

                // Read the first 99 bytes of the buffer into the array
                _socket.GetStream().Read(loopString, 0, LOOP_DATA_LENGTH);
            }
            catch (System.IO.IOException ex)
            {
                if (ex.Message.Contains("did not properly respond after a period"))
                {
                    _log.Error("LOOP: Timed out waiting for LOOP data");
                }
                else
                {
                    _log.Error("LOOP: Receive error - " + ex);
                    _log.Debug("LOOP: Reconnecting to station");
                    Init();
                    _log.Debug("LOOP: Reconnected to station");
                }
                return new byte[] {};
            }
            catch (Exception ex)
            {
                _log.Error("LOOP: Receive error - " + ex);
                _log.Debug("LOOP: Reconnecting to station");
                Init();
                _log.Debug("LOOP: Reconnected to station");
                return new byte[] {};
            }

            _log.Data($"LOOP: Data: {BitConverter.ToString(loopString)}");

            return loopString;
        }

        public override void Clear()
        {
                _socket.GetStream().WriteByte(10);
                Thread.Sleep(3000);
                // read off all data in the pipeline
                var avail = _socket.Available;
                _log.Debug("Discarding bytes from pipeline: " + avail);
                for (var b = 0; b < avail; b++)
                    _socket.GetStream().ReadByte();


        }

        private void DoPeriodicDisconnect()
        {
            int previousMinuteDisconnect = -1;
            int min;
            // See if we need to disconnect to allow Weatherlink IP to upload
            if (_disconnectInterval > 0)
            {
                min = DateTime.Now.Minute;

                if (min != previousMinuteDisconnect)
                {
                    try
                    {
                        previousMinuteDisconnect = min;

                        _log.Debug("Periodic disconnect from logger");
                        // time to disconnect - first stop the loop data by sending a newline
                        _socket.GetStream().WriteByte(10);
                        //socket.Client.Shutdown(SocketShutdown.Both);
                        //socket.Client.Disconnect(false);
                    }
                    catch (Exception ex)
                    {
                        _log.Info("Periodic disconnect: " + ex.Message);
                    }
                    finally
                    {
                        _socket.Client.Close(0);
                    }

                    // Wait
                    Thread.Sleep(_disconnectInterval * 1000);

                    _log.Debug("Attempting reconnect to logger");
                    // open a new connection
                    _socket = OpenTcpPort();
                    if (_socket == null)
                    {
                        _log.Error("Unable to reconnect to logger");
                        throw new Exception("Unable to reconnect to logger.");
                    }
                }
            }
        }


        internal override void CloseConnection()
        {
            try
            {
                _socket.GetStream().WriteByte(10);
                _socket.Close();
            }
            catch
            { }
        }
    }
}
