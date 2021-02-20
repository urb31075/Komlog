using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Windows.Controls.Primitives;

namespace Komlog
{
    internal struct DeviceInfo
    {
        public int DeviceIndex;
        public string Name;
        public string Port;
    }

    class FindDevice
    {
        private static byte CalCheckSum(IReadOnlyList<byte> packetData, int packetLength)
        {
            byte messCrc = 0;
            const byte b80 = 0x80;
            const byte b18 = 0x18;

            for (var i = 0; i < packetLength; i++)
            {
                byte inch = packetData[i];
                for (var b = 0; b < 8; b++)
                {
                    if (((inch ^ messCrc) & 0x01) > 0)
                    {
                        messCrc = (byte)(b80 | ((messCrc ^ b18) >> 1));
                    }
                    else
                    {
                        messCrc = (byte)(messCrc >> 1);
                    }

                    inch >>= 1;
                }

            }
            return messCrc;
        }

        private static string ToHexStr(IReadOnlyList<byte> packetData, int packetLength)
        {
            var line = new byte[1];
            var outHex = "";
            for (var i = 0; i < packetLength; i++)
            {
                line[0] = packetData[i];
                outHex += BitConverter.ToString(line) + " ";
            }

            return outHex.Trim();
        }

        public List<DeviceInfo> Run(MainWindow mainWindow, StatusBarItem statusBarItem)
        {

            var deviceList = new List<DeviceInfo>();
            var portnames = SerialPort.GetPortNames();
            for (var deviceIndex = 0; deviceIndex < portnames.Length; deviceIndex++)
            {
                //DebugStatusBarItem.Content = "Cancelled"
                statusBarItem.Content = $"Поиск на {portnames[deviceIndex]} ...";
                mainWindow.Refresh(statusBarItem);
                if (portnames[deviceIndex] == "COM1") continue;

                var port1 = new SerialPort(portnames[deviceIndex], 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 1000 };
                try
                {
                    port1.Open();
                    port1.DiscardInBuffer();
                    port1.DiscardOutBuffer();
                    port1.WriteLine("T");
                    var signatura = port1.ReadLine().Replace("\r\n", "").Replace("\r", "").Replace("\n", "");
                    if (signatura == "PointR_AD7606")
                    {
                        var deviceInfo = new DeviceInfo
                        {
                            DeviceIndex = deviceIndex, Port = portnames[deviceIndex], Name = "PointR"
                        };
                        deviceList.Add(deviceInfo);
                        continue;
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
                finally
                {
                    port1.Close();
                }

                var port2 = new SerialPort(portnames[deviceIndex], 115200, Parity.None, 8, StopBits.One) { ReadTimeout = 1000 };
                try
                {
                    port2.Open();
                    port2.DiscardInBuffer();
                    port2.DiscardOutBuffer();
                    var data = new byte[64];
                    byte commandLength = 5;
                    data[0] = 0x1D;
                    data[1] = commandLength;
                    data[2] = 0x80;
                    data[3] = 0x00;
                    data[4] = 0x00;  // MESSAGE_00
                    data[5] = CalCheckSum(data, commandLength);
                    port2.Write(data, 0, commandLength + 1);
                    var offset = 0;
                    while (true)
                    {
                        var readResult = port2.Read(data, offset, 1);
                        offset += readResult;
                        if (offset == commandLength + 1)
                            break;
                    }

                    if (offset == commandLength + 1)
                    {
                        var signatura = ToHexStr(data, offset);
                        if (signatura == "1D 05 80 00 00 73")
                        {
                            var deviceInfo = new DeviceInfo
                            {
                                DeviceIndex = deviceIndex,
                                Port = portnames[deviceIndex],
                                Name = "SpbUnit"
                            };
                            deviceList.Add(deviceInfo);
                        }
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
                finally
                {
                    port2.Close();
                }
            }

            return deviceList;
        }
    }
}
