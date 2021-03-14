using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Net.Http;

namespace ConsoleApp1
{


    static class Program
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(int destIp, int srcIP, byte[] macAddr, ref uint physicalAddrLen);

        readonly struct IPRange
        {
            public uint Local { get; }
            public uint First { get; }
            public uint Last { get; }

            public IPRange(uint local, uint first, uint last)
            {
                Local = local;
                First = first;
                Last = last;
            }
        }

        static void Main(string[] args)
        {
            ScanLocalNetwork();
            Console.WriteLine("\nProgram completed.");
            Console.Read();
        }

        private static void ScanLocalNetwork()
        {
            IPGlobalProperties computerProperties = IPGlobalProperties.GetIPGlobalProperties();
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            Console.WriteLine("Interface information for {0}.{1}    ",
                computerProperties.HostName, computerProperties.DomainName);
            Console.WriteLine("************************************************");

            Console.WriteLine("Number of interfaces: {0}", nics.Length);
            foreach (NetworkInterface adapter in nics)
            {
                if (adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    adapter.OperationalStatus == OperationalStatus.Up)
                    ShowPropertiesNodes_InAdapter(adapter);
                else
                {
                    Console.WriteLine("\nScanning from " + adapter.Description);
                    Console.WriteLine("Name: " + adapter.Name);
                    Console.WriteLine("=========================================================");
                    Console.WriteLine("Adapter is not available!");
                }
            }
        }

        private static void ShowPropertiesNodes_InAdapter(NetworkInterface adapter)
        {
            Console.WriteLine("\nScanning from " + adapter.Description);
            Console.WriteLine("Name: " + adapter.Name);
            Console.WriteLine("=========================================================");
            //Console.WriteLine(adapter.Id);
            Console.WriteLine("-------------------------Nodes---------------------------");

            IPRange ipRange = GetIPRange(adapter);
            List<uint> allIP = new List<uint>();
            for (uint ip = ipRange.First; ip < ipRange.Last; IncrementIP(ref ip))
            {
                allIP.Add(ip);
            }

            Console.WriteLine($"      IP:             MAC address:                  Vendor:");
            Parallel.ForEach(allIP, ip =>
            {
                IPAddress ipCurrent = new IPAddress(ip);
                string macLine = GetMacByIP((int)ip, (int)ipRange.Local);
                try
                {
                    if (macLine != null)
                        Console.WriteLine($"{ipCurrent,15}   {macLine,20}    {LookupMac(macLine).Result,20}");
                }
                catch (System.AggregateException exAgg)
                {
                    Console.WriteLine(exAgg);
                }
            });
            
        }

        static async Task<string> LookupMac(string MacAddress)
        {
            try
            {
                var uri = new Uri("http://api.macvendors.com/" + WebUtility.UrlEncode(MacAddress));
                using (var wc = new HttpClient())
                    return await wc.GetStringAsync(uri);
            }
            catch
            {
                return $" Please check internet connection!";
            }
        }

        private static void IncrementIP(ref uint address)
        {
            if (BitConverter.IsLittleEndian)
                address += 1 << 24;
            else
                address++;
        }

        private static IPRange GetIPRange(NetworkInterface netInterface)
        {
            foreach (UnicastIPAddressInformation ip in netInterface.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    uint localIp = ip.Address.GetAddressBytes().ToUInt();
                    uint subnet = ip.IPv4Mask.GetAddressBytes().ToUInt();

                    uint first = localIp & subnet;
                    uint last = first | (0xffffffff & ~subnet);

                    if (BitConverter.IsLittleEndian)
                    {
                        first += 1 << 24;
                    }
                    else
                    {
                        first++;
                        last--;
                    }

                    return new IPRange(localIp, first, last);
                }
            }

            throw new ArgumentException("Interface not scannable");
        }

        private static uint ToUInt(this byte[] bytes)
        {
            if (bytes.Length != 4)
            {
                throw new ArgumentException("Length of array is not 4");
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            uint result = (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
            return result;
        }

        public static string GetMacByIP(int destIP, int srcIP)
        { 
            byte[] macAddr = new byte[6];
            uint macAddrLen = (uint)macAddr.Length;


            if (SendARP(destIP, srcIP, macAddr, ref macAddrLen) != 0)
                return null;
                    //throw new InvalidOperationException("SendARP failed.");

            string[] str = new string[(int)macAddrLen];
            for (int i = 0; i < macAddrLen; i++)
                str[i] = macAddr[i].ToString("x2");

            return string.Join(":", str);
        }


    }
}
