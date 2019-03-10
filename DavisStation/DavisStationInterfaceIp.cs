using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using CumulusMX.Extensions;
using Force.Crc32;

namespace DavisStation
{
    public class DavisStationInterfaceIp : DavisStationInterface
    {
        private readonly IPAddress _ipAddress;
        private readonly int _port;
        private readonly int _disconnectInterval;
        private TcpClient _socket;
        private DateTime _lastReceptionStatsTime;
        private readonly int _responseTime;
        private readonly int _initWaitTime;

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
            _log.Info("IP address = " + _ipAddress + " Port = " + _port);
            _log.Info("periodic disconnect = " + _disconnectInterval);
            _socket = OpenTcpPort();

            Connected = _socket != null;

            if (Connected)
                Init(_socket);

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
                _log.Info("TCP Logger Connect attempt " + attempt);
                try
                {
                    client = new TcpClient(_ipAddress.ToString(), _port);

                    if (!client.Connected) client = null;

                    Thread.Sleep(1000);
                }
                catch(Exception ex)
                {
                    _log.Error(ex.Message);
                }
            }

            // Set the timeout of the underlying stream
            if (client != null)
            {
                client.GetStream().ReadTimeout = 2500;
                _log.Info("TCP Logger reconnected");
            }
            else
            {
                _log.Error("TCP Logger connect failed");
            }

            return client;
        }

        private void Init(TcpClient thePort)
        {
            try
            {
                _log.Info("Flushing input stream");
                var stream = thePort.GetStream();

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

        private bool Wake(TcpClient thePort)
        {
            byte newLineASCII = 10;
            byte LF = 13;
            int passCount = 1;
            const int maxPasses = 4;

            try
            {
                NetworkStream theStream;
                try
                {
                    theStream = thePort.GetStream();
                }
                catch (Exception exStream)
                {
                    // There is a problem with the connection, try to disconnect/connect
                    // refer back to the socket field of this class
                    try
                    {
                        _log.Info("WakeVP: Problem with TCP connection - " + exStream.Message);
                        _socket.Client.Close(0);
                    }
                    finally
                    {
                        // Wait a second
                        Thread.Sleep(1000);
                        _socket = null;

                        _log.Info("WakeVP: Attempting reconnect to logger");
                        // open a new connection
                        _socket = OpenTcpPort();
                        thePort = _socket;
                    }

                    if (thePort == null)
                        return false;
                    theStream = thePort.GetStream();
                }

                // First flush the stream
                while (theStream.DataAvailable)
                    // Read the current character
                    theStream.ReadByte();

                while (passCount < maxPasses)
                {
                    _dataLog.Info("Sending newline");
                    theStream.WriteByte(newLineASCII);

                    Thread.Sleep(_responseTime);
                    var ch1 = theStream.ReadByte();
                    var ch2 = theStream.ReadByte();
                    _dataLog.Info("ch1 = 0x" + ch1.ToString("X2"));
                    _dataLog.Info("ch2 = 0x" + ch2.ToString("X2"));
                    if (ch1 == newLineASCII && ch2 == LF) break;
                    passCount++;
                }

                if (passCount < maxPasses)
                {
                    return true;
                }

                _log.Warn("*** Not woken");
                return false;
            }
            catch (Exception ex)
            {
                _log.Info("WakeVP Error: " + ex.Message);
                return false;
            }
        }


        internal override string GetFirmwareVersion()
        {
            _log.Info("Reading firmware version");
            var response = "";

                var commandString = "NVER\n";
                if (Wake(_socket))
                    try
                    {
                        var stream = _socket.GetStream();

                        stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                        Thread.Sleep(_responseTime);

                        var bytesRead = 0;
                        var buffer = new byte[20];

                        while (stream.DataAvailable)
                        {
                            // Read the current character
                            var ch = stream.ReadByte();
                            response += Convert.ToChar(ch);
                            buffer[bytesRead] = (byte)ch;
                            bytesRead++;
                            //_log.Info("Received " + ch.ToString("X2"));
                        }

                        _dataLog.Info("Received 0x" + BitConverter.ToString(buffer));
                    }
                    catch (Exception ex)
                    {
                        _log.Info("GetFirmwareVersion: Error - " + ex.Message);
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

                var commandString = "RXCHECK\n";
                if (Wake(_socket))
                {
                    var bytesRead = 0;
                    var buffer = new byte[40];

                    try
                    {
                        var stream = _socket.GetStream();

                        stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                        Thread.Sleep(_responseTime);

                        while (stream.DataAvailable)
                        {
                            // Read the current character
                            var ch = stream.ReadByte();
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

                var commandString = "GETTIME\n";
                if (Wake(_socket))
                    try
                    {
                        var stream = _socket.GetStream();
                        stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                        Thread.Sleep(_responseTime);

                        var foundAck = false;
                        int ch;

                        while (stream.DataAvailable && !foundAck)
                        {
                            // Read the current character
                            ch = stream.ReadByte();
                            //_log.Info("Received 0x" + ch.ToString("X2"));
                            if (ch == ACK)
                                foundAck = true;
                        }

                        if (!foundAck)
                        {
                            _log.Info("No ACK - wait a little longer");
                            // wait a little longer
                            Thread.Sleep(500);
                            while (stream.DataAvailable && !foundAck)
                            {
                                // Read the current character
                                ch = stream.ReadByte();
                                _log.Info("Received 0x" + ch.ToString("X2"));
                                if (ch == ACK)
                                    foundAck = true;
                            }

                            if (!foundAck)
                            {
                                _log.Info("No ACK");
                                return DateTime.MinValue;
                            }
                        }

                        _log.Info("ACK received");

                        // Read the time
                        var bytesRead = 0;
                        var buffer = new byte[8];
                        while (stream.DataAvailable && bytesRead < 8)
                        {
                            // Read the current character
                            ch = stream.ReadByte();
                            buffer[bytesRead] = (byte)ch;

                            bytesRead++;
                            //_log.Info("Received " + ch.ToString("X2"));
                        }

                        _dataLog.Info("Received 0x" + BitConverter.ToString(buffer));

                        if (bytesRead != 8)
                            _log.Info("Expected 8 bytes, got " + bytesRead);
                        /*else if (!crcOK(buffer))
						{
							_log.Info("Invalid CRC");
						}*/
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
                    catch (Exception ex)
                    {
                        _log.Info("Get date time: Error - " + ex.Message);
                    }

            return DateTime.MinValue;
        }

        internal override void SetTime()
        {
            _log.Info("Setting console time");

                var commandString = "SETTIME\n";
                if (Wake(_socket))
                    try
                    {
                        var stream = _socket.GetStream();
                        stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                        Thread.Sleep(_responseTime);

                        var foundAck = false;
                        int ch;

                        while (stream.DataAvailable && !foundAck)
                        {
                            // Read the current character
                            ch = stream.ReadByte();
                            _log.Info("Received 0x" + ch.ToString("X2"));
                            if (ch == ACK)
                                foundAck = true;
                        }

                        if (!foundAck)
                        {
                            _log.Info("No ACK");
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

                        stream.Write(buffer, 0, buffer.Length);

                        Thread.Sleep(_responseTime);

                        foundAck = false;

                        while (stream.DataAvailable && !foundAck)
                        {
                            // Read the current character
                            ch = stream.ReadByte();
                            _log.Info("Received 0x" + ch.ToString("X2"));
                            if (ch == ACK)
                                foundAck = true;
                        }

                        if (!foundAck)
                        {
                            _log.Info("No ACK");
                            return;
                        }

                        _log.Info("ACK received");
                    }
                    catch (Exception ex)
                    {
                        _log.Info("setTime Error - " + ex.Message);
                    }
        }


    }
}
