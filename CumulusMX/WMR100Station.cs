using System;
using System.IO.Ports;
using System.Threading;
using HidSharp;

namespace CumulusMX
{
    internal class WMR100Station : WeatherStation
    {
        private readonly DeviceList devicelist;
        private readonly HidDevice station;
        private readonly HidStream stream;
        private const int Vendorid = 0x0FDE;
        private const int Productid = 0xCA01;

        private const int BARO_PACKET_TYPE = 0x46;
        private const int TEMP_PACKET_TYPE = 0x42;
        private const int WIND_PACKET_TYPE = 0x48;
        private const int RAIN_PACKET_TYPE = 0x41;
        private const int POND_PACKET_TYPE = 0x44;
        private const int UV_PACKET_TYPE = 0x47;
        private const int DATE_PACKET_TYPE = 0x60;

        private const int BARO_PACKET_LENGTH = 8;
        private const int TEMP_PACKET_LENGTH = 12;
        private const int WIND_PACKET_LENGTH = 11;
        private const int RAIN_PACKET_LENGTH = 17;
        private const int UV_PACKET_LENGTH = 6;
        private const int DATE_PACKET_LENGTH = 12;
        private const int POND_PACKET_LENGTH = 7;

        private readonly byte[] PacketBuffer;
        private int CurrentPacketLength;
        private int CurrentPacketType = 255;
        private const int PacketBufferBound = 255;
        private readonly byte[] usbbuffer = new byte[9];

        public WMR100Station(Cumulus cumulus) : base(cumulus)
        {
            cumulus.Manufacturer = cumulus.OREGONUSB;
            devicelist = DeviceList.Local;
            station = devicelist.GetHidDeviceOrNull(Vendorid, Productid);

            if (station != null)
            {
                cumulus.LogMessage(DateTime.Now.ToLongTimeString() + " WMR100 station found");

                if (station.TryOpen(out stream))
                {
                    cumulus.LogMessage(DateTime.Now.ToLongTimeString() + " Stream opened");
                }

                PacketBuffer = new byte[PacketBufferBound];

                WMR200ExtraTempValues = new double[11];
                WMR200ExtraHumValues = new double[11];
                WMR200ChannelPresent = new bool[11];
                WMR200ExtraDPValues = new double[11];
            }
            else
            {
                cumulus.LogMessage(DateTime.Now.ToLongTimeString() + " WMR100 station not found!");
                Console.WriteLine("WMR100 station not found!");
            }
        }

        public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        public override void Start()
        {
            DoDayResetIfNeeded();
            LoadLastHoursFromDataLogs(DateTime.Now);
            DoTrendValues(DateTime.Now);
            cumulus.StartTimers();

            cumulus.LogMessage("Sending reset");
            SendReset();
            cumulus.LogMessage("Start loop");
            int numBytes;
            int responseLength;
            int startByte;
            int offset;

            // Returns 9-byte usb packet, with report ID in first byte
            responseLength = 9;
            startByte = 1;
            offset = 0;

            try
            {
                while (true)
                {
                    cumulus.LogDebugMessage("Calling Read, current packet length = "+CurrentPacketLength);

                    try
                    {
                        numBytes = stream.Read(usbbuffer, offset, responseLength);

                        String Str = "";

                        for (int I = startByte; I < responseLength; I++)
                        {
                            Str = Str + " " + usbbuffer[I].ToString("X2");
                        }

                        cumulus.LogDataMessage(Str);

                        // Number of valid bytes is in first byte
                        int dataLength = usbbuffer[1];
                        cumulus.LogDebugMessage("data length = " + dataLength);

                        for (int i = 1; i <= dataLength; i++)
                        {
                            byte C = usbbuffer[i + 1];
                            switch (CurrentPacketLength)
                            {
                                case 0: // We're looking for the start of a packet
                                    if (C == 0xFF)
                                    {
                                        // Possible start of packet
                                        CurrentPacketLength = 1;
                                    }
                                    break;
                                case 1: // We're looking for the second start-of-packet character
                                    if (C == 0xFF)
                                    {
                                        // Possible continuation
                                        CurrentPacketLength = 2;
                                    }
                                    else
                                    {
                                        // Incorrect sequence, start again
                                        CurrentPacketLength = 0;
                                    }
                                    break;
                                case 2: // This is typically a flags byte, and will be the first byte of our actual data packet
                                    PacketBuffer[0] = C;
                                    CurrentPacketLength = 3;
                                    break;
                                default: // We've had the packet header and the flags byte, continue collecting the packet
                                    PacketBuffer[CurrentPacketLength - 2] = C;
                                    CurrentPacketLength++;
                                    if (CurrentPacketLength == 4)
                                    {
                                        CurrentPacketType = C;
                                        cumulus.LogDebugMessage("Current packet type: "+CurrentPacketType.ToString("X2"));
                                    }
                                    if (CurrentPacketLength - 2 == WMR100PacketLength(CurrentPacketType))
                                    {
                                        // We've collected a complete packet, process it
                                        ProcessWMR100Packet();
                                        // Get ready for the next packet
                                        CurrentPacketLength = 0;
                                        CurrentPacketType = 255;
                                    }
                                    break;
                            } // end of case for current packet length
                        }
                    }
                    catch (Exception ex)
                    {
                        // Might just be a timeout, which is normal, so debug log only
                        cumulus.LogDebugMessage("Data read loop: " + ex.Message);
                    }
                }
                //Thread.Sleep(100);
            }

            // Catch the ThreadAbortException
            catch (ThreadAbortException)
            {
            }
        }

        private int WMR100PacketLength(int packettype)
        {
            switch (packettype)
            {
                case BARO_PACKET_TYPE:
                    return BARO_PACKET_LENGTH;
                case TEMP_PACKET_TYPE:
                    return TEMP_PACKET_LENGTH;
                case WIND_PACKET_TYPE:
                    return WIND_PACKET_LENGTH;
                case RAIN_PACKET_TYPE:
                    return RAIN_PACKET_LENGTH;
                case UV_PACKET_TYPE:
                    return UV_PACKET_LENGTH;
                case DATE_PACKET_TYPE:
                    return DATE_PACKET_LENGTH;
                case POND_PACKET_TYPE:
                    return POND_PACKET_LENGTH;
                default:
                    return 255;
            }
        }

        /*
        private void ClearPacketBuffer()
        {
            for (int I = 0; I < PacketBufferBound; I++)
            {
                PacketBuffer[I] = 0;
            }
            CurrentPacketLength = 0;
        }
        */

        /*
        private void RemovePacketFromBuffer()
        {
            // removes packet from start of buffer
            // there might be a partial packet behind it
            int actualpacketlength = PacketBuffer[1];
            int overflow = CurrentPacketLength - actualpacketlength;
            if (overflow == 0)
            {
                // only one packet in buffer, clear it
                ClearPacketBuffer();
            }
            else
            {
                // need to move surplus data to start of packet
                for (int I = 0; I < overflow; I++)
                {
                    PacketBuffer[I] = PacketBuffer[actualpacketlength + I];
                }
                CurrentPacketLength = overflow;
                string Str = " ";
                for (int I = 0; I < CurrentPacketLength; I++)
                {
                    Str += PacketBuffer[I].ToString("X2");
                }
                cumulus.LogDebugMessage(Str);
            }
        }
        */

        private void ProcessWMR100Packet()
        {
            string Str = String.Empty;

            for (int i = 0; i <= CurrentPacketLength - 3; i++)
            {
                Str = Str + " " + PacketBuffer[i].ToString("X2");
            }

            cumulus.LogDataMessage("Packet:" + Str);

            if (CRCOK())
            {
                switch (CurrentPacketType)
                {
                    case BARO_PACKET_TYPE:
                        ProcessBaroPacket();
                        break;
                    case TEMP_PACKET_TYPE:
                        ProcessTempPacket();
                        break;
                    case WIND_PACKET_TYPE:
                        ProcessWindPacket();
                        break;
                    case RAIN_PACKET_TYPE:
                        ProcessRainPacket();
                        break;
                    case UV_PACKET_TYPE:
                        ProcessUVPacket();
                        break;
                    case DATE_PACKET_TYPE:
                        ProcessDatePacket();
                        break;
                    case POND_PACKET_TYPE:
                        ProcessPondPacket();
                        break;
                    default:
                        cumulus.LogMessage("Unknown packet type: " + CurrentPacketType.ToString("X2"));
                        return;
                }

                UpdateStatusPanel(DateTime.Now);
                UpdateMQTT();
            }
            else
            {
                cumulus.LogDebugMessage("Invalid CRC");
            }
        }

        private void ProcessPondPacket()
        {
            cumulus.LogDebugMessage("Pond packet");
            int sensor = PacketBuffer[2] & 0xF;
            int sign;

            //MainForm.SystemLog.WriteLogString('Pond packet received, ch = ' + IntToStr(sensor));

            if ((sensor > 1) && (sensor < 11))
            {
                WMR200ChannelPresent[sensor] = true;
                // Humidity n/a
                WMR200ExtraHumValues[sensor] = 0;

                // temp
                if ((PacketBuffer[4] & 0x80) == 0x80)
                    sign = -1;
                else
                    sign = 1;

                double num = (sign*((PacketBuffer[4] & 0xF)*256 + PacketBuffer[3]))/10.0;

                WMR200ExtraTempValues[sensor] = ConvertTempCToUser(num);
                DoExtraTemp(WMR200ExtraTempValues[sensor], sensor);

                // outdoor dewpoint - n/a

                WMR200ExtraDPValues[sensor] = 0;
                ExtraSensorsDetected = true;
            }
        }

        private void ProcessUVPacket()
        {
            cumulus.LogDebugMessage("UV packet");
            var num = PacketBuffer[3] & 0xF;

            if (num < 0)
                num = 0;

            if (num > 16)
                num = 16;

            DoUV(num, DateTime.Now);

            // UV value is stored as channel 1 of the extra sensors
            WMR200ExtraHumValues[1] = num;

            ExtraSensorsDetected = true;

            WMR200ChannelPresent[1] = true;
        }

        private void ProcessRainPacket()
        {
            cumulus.LogDebugMessage("Rain packet");
            double counter = ((PacketBuffer[9]*256) + PacketBuffer[8])/100.0;

            double rate = ((PacketBuffer[3]*256) + PacketBuffer[2])/100.0;

            // check for overflow  (9999 mm = approx 393 in) and set to 999 mm/hr
            if (rate > 393)
            {
                rate = 39.33;
            }

            DoRain(ConvertRainINToUser(counter), ConvertRainINToUser(rate), DateTime.Now);

            // battery status
            //if PacketBuffer[0] and $40 = $40 then
            //MainForm.RainBatt.Position := 0
            //else
            //MainForm.RainBatt.Position := 100;
        }

        private void ProcessWindPacket()
        {
            cumulus.LogDebugMessage("Wind packet");
            DateTime Now = DateTime.Now;

            double wc;

            // bearing
            double b = (PacketBuffer[2] & 0xF)*22.5;
            // gust
            double g = ((PacketBuffer[5] & 0xF)*256 + PacketBuffer[4])/10.0;
            // average
            double a = ((PacketBuffer[6]*16) + (PacketBuffer[5]/16))/10.0;

            DoWind(ConvertWindMSToUser(g), (int) (b), ConvertWindMSToUser(a), Now);

            if ((PacketBuffer[8] & 0x20) == 0x20)
            {
                // no wind chill, use current temp if (available
                // note that even if (Cumulus is set to calculate wind chill
                // it can't/won't do it if (temp isn't available, so don't
                // bother calling anyway

                if (TempReadyToPlot)
                {
                    wc = OutdoorTemperature;
                    DoWindChill(wc, Now);
                }
            }
            else
            {
                // wind chill is in Fahrenheit!
                wc = (PacketBuffer[7] + (PacketBuffer[8] & 0xF)*256)/10.0;

                if ((PacketBuffer[8] & 0x80) == 0x80)
                    // wind chill negative
                    wc = -wc;

                if ((cumulus.TempUnit == 0))
                    // convert to C
                    wc = (wc - 32)/1.8;

                DoWindChill(wc, Now);
            }

            // battery status
            //if ((PacketBuffer[0] & 0x40) == 0x40)
            //    MainForm.WindBatt.Position = 0;
            //else
            //    MainForm.WindBatt.Position = 100;
        }

        private void ProcessTempPacket()
        {
            // which sensor is this for? 0 = indoor, 1 = outdoor, n = extra
            int sensor = PacketBuffer[2] & 0xF;
            DateTime Now = DateTime.Now;

            int sign;
            double num;

            cumulus.LogDebugMessage("Temp/hum packet, ch = " + sensor);

            if (sensor == cumulus.WMR200TempChannel)
            {
                //MainForm.SystemLog.WriteLogString('Main Outdoor sensor');
                // outdoor hum
                DoOutdoorHumidity(PacketBuffer[5], DateTime.Now);

                // outdoor temp
                if ((PacketBuffer[4] & 0x80) == 0x80)
                    sign = -1;
                else
                    sign = 1;

                num = (sign*((PacketBuffer[4] & 0xF)*256 + PacketBuffer[3]))/10.0;
                DoOutdoorTemp(ConvertTempCToUser(num), Now);

                // outdoor dewpoint
                if ((PacketBuffer[7] & 0x80) == 0x80)
                    sign = -1;
                else
                    sign = 1;

                num = (sign*((PacketBuffer[7] & 0xF)*256 + PacketBuffer[6]))/10.0;
                DoOutdoorDewpoint(ConvertTempCToUser(num), Now);

                DoApparentTemp(Now);
                DoFeelsLike();


                // battery status
                //if (PacketBuffer[0] & 0x40 == 0x40 )
                //  MainForm.TempBatt.Position = 0
                //else
                //  MainForm.TempBatt.Position = 100;
            }
            else if (sensor == 0)
            {
                //MainForm.SystemLog.WriteLogString('Indoor sensor');
                // indoor hum
                DoIndoorHumidity(PacketBuffer[5]);

                // outdoor temp
                if ((PacketBuffer[4] & 0x80) == 0x80)
                    sign = -1;
                else
                    sign = 1;

                num = (sign*((PacketBuffer[4] & 0xF)*256 + PacketBuffer[3]))/10.0;
                DoIndoorTemp(ConvertTempCToUser(num));
            }

            if ((sensor > 1) && (sensor < 11))
            {
                WMR200ChannelPresent[sensor] = true;
                // outdoor hum
                WMR200ExtraHumValues[sensor] = PacketBuffer[5];

                DoExtraHum(WMR200ExtraHumValues[sensor], sensor);

                // outdoor temp
                if ((PacketBuffer[4] & 0x80) == 0x80)
                    sign = -1;
                else
                    sign = 1;

                num = (sign*((PacketBuffer[4] & 0xF)*256 + PacketBuffer[3]))/10.0;

                WMR200ExtraTempValues[sensor] = ConvertTempCToUser(num);
                DoExtraTemp(WMR200ExtraTempValues[sensor], sensor);

                // outdoor dewpoint
                if ((PacketBuffer[7] & 0x80) == 0x80)
                    sign = -1;
                else
                    sign = 1;

                num = (sign*((PacketBuffer[7] & 0xF)*256 + PacketBuffer[6]))/10.0;
                WMR200ExtraDPValues[sensor] = ConvertTempCToUser(num);
                DoExtraDP(WMR200ExtraDPValues[sensor], sensor);
                ExtraSensorsDetected = true;
            }
        }

        private void ProcessBaroPacket()
        {
            cumulus.LogDebugMessage("Barometer packet");
            double num = ((PacketBuffer[5] & 0xF)*256) + PacketBuffer[4];

            double slp = ConvertPressMBToUser(num);

            num = ((PacketBuffer[3] & 0xF)*256) + PacketBuffer[2];

            StationPressure = ConvertPressMBToUser(num);

            DoPressure(slp, DateTime.Now);

            UpdatePressureTrendString();

            int forecast = PacketBuffer[3]/16;
            string fcstr;

            switch (forecast)
            {
                case 0:
                    fcstr = "Partly Cloudy";
                    break;
                case 1:
                    fcstr = "Rainy";
                    break;
                case 2:
                    fcstr = "Cloudy";
                    break;
                case 3:
                    fcstr = "Sunny";
                    break;
                case 4:
                    fcstr = "Clear";
                    break;
                case 5:
                    fcstr = "Snowy";
                    break;
                case 6:
                    fcstr = "Partly Cloudy";
                    break;
                default:
                    fcstr = "Unknown";
                    break;
            }

            DoForecast(fcstr, false);
        }

        private void ProcessDatePacket()
        {
        }

        private Boolean CRCOK()
        {
            var packetLen = CurrentPacketLength - 2;

            if (packetLen < 3)
            {
                return true;
            }
            else
            {
                // packet CRC is in last two bytes, low byte then high byte
                var packetCRC = (PacketBuffer[packetLen - 1]*256) + PacketBuffer[packetLen - 2];

                var calculatedCRC = 0;

                // CRC is calulated by summing all but the last two bytes
                for (int i = 0; i <= packetLen - 3; i++)
                {
                    calculatedCRC += PacketBuffer[i];
                }

                cumulus.LogDebugMessage("Packet CRC = " + packetCRC);
                cumulus.LogDebugMessage("Calculated CRC = "+calculatedCRC);

                return (packetCRC == calculatedCRC);
            }
        }

        private void SendReset()
        {
            if (cumulus.logging)
            {
                cumulus.LogMessage("Sending reset");
            }

            byte[] reset;

            if (cumulus.IsOSX)
            {
                reset = new byte[] { 0x20, 0x00, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00 };
            }
            else
            {
                reset = new byte[] { 0x00, 0x20, 0x00, 0x08, 0x01, 0x00, 0x00, 0x00, 0x00 };
            }

            stream.Write(reset);
        }

        public override void Stop()
        {
        }
    }
}
