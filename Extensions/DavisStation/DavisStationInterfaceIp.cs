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

        internal override bool SendLoopCommand(string commandString)
        {
            var Found_ACK = false;
            const int ACK = 6; // ASCII 6
            var passCount = 1;
            const int maxPasses = 4;

            commandString += "\n";

            try
            {
                var stream = _socket.GetStream();

                // Try the command until we get a clean ACKnowledge from the VP.  We count the number of passes since
                // a timeout will never occur reading from the sockets buffer.  If we try a few times (maxPasses) and
                // we get nothing back, we assume that the connection is broken
                while (!Found_ACK && passCount < maxPasses)
                {
                    // send the LOOP n command
                    _log.Debug("Sending command: " + commandString.Replace("\n", "") + ", attempt " +
                                            passCount);
                    stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);
                    Thread.Sleep(_responseTime);
                    _log.Debug("Wait for ACK");
                    // Wait for the VP to acknowledge the the receipt of the command - sometimes we get a '\n\r'
                    // in the buffer first or no response is given.  If all else fails, try again.
                    while (stream.DataAvailable && !Found_ACK)
                    {
                        // Read the current character
                        var data = stream.ReadByte();
                        _dataLog.Info("Received 0x" + data.ToString("X2"));
                        if (data == ACK)
                        {
                            _log.Debug("Received ACK");
                            Found_ACK = true;
                        }
                    }

                    passCount++;
                }
            }
            catch (Exception ex)
            {
                _dataLog.Info("Error sending LOOP command: " + ex.Message);
            }

            // return result to indicate success or otherwise
            return Found_ACK;
        }

        internal override void SendBarRead()
        {
            _log.Debug("Sending BARREAD");

            var response = "";

                var commandString = "BARREAD\n";
                if (Wake(_socket))
                    try
                    {
                        var stream = _socket.GetStream();
                        stream.Write(Encoding.ASCII.GetBytes(commandString), 0, commandString.Length);

                        Thread.Sleep(_responseTime);

                        var bytesRead = 0;
                        var buffer = new byte[64];

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
                        _log.Debug("SendBarRead: Error - " + ex.Message);
                    }
            
        }

        internal override byte[] GetLoop2Data()
        {
            var loopString = new byte[LOOP_2_DATA_LENGTH];

            try
            {
                    // Wait until the buffer is full
                    var loopCount = 1;
                    while (loopCount < 100 && _socket.Available < LOOP_2_DATA_LENGTH)
                    {
                        // Wait a short period to let more data load into the buffer
                        Thread.Sleep(200);
                        loopCount++;
                    }

                    if (loopCount == 100)
                    {
                        // all data not received
                        _log.Info("!!! loop2 data not received");
                        return new byte[]{};
                    }

                    // Read the first 99 bytes of the buffer into the array
                    _socket.GetStream().Read(loopString, 0, LOOP_2_DATA_LENGTH);
                }
                catch (Exception ex)
                {
                    _log.Debug("Loop2 data: Error - " + ex.Message);
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
                _log.Error("Error during periodic disconnect. Error: "+ex);
                return new byte[] { };
            }

            // Wait until the buffer is full - we've received returnLength characters from the command response
                var loopcount = 1;
                while (loopcount < 100 && _socket.Available < LOOP_DATA_LENGTH)
                {
                    // Wait a short period to let more data load into the buffer
                    Thread.Sleep(200);
                    loopcount++;
                }

                if (loopcount == 100)
                {
                    // all data not received
                    _log.Info("!!! loop data not received");
                    return new byte[]{};
                }

                // Read the first 99 bytes of the buffer into the array
                _socket.GetStream().Read(loopString, 0, LOOP_DATA_LENGTH);

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
            _socket.GetStream().WriteByte(10);
            _socket.Close();
        }
    }
}
