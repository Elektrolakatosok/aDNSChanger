using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Timers;
using System.IO;
namespace selfDNSservice
{
    public partial class Service1 : ServiceBase
    {
        private static string selfDNS = "192.168.1.100";
        private static int counter = 0;
        private static Timer timer = new Timer();
        private static EventLog eventLog1;
        private static bool isDebug=false;
        public Service1()
        {
            InitializeComponent();
            eventLog1 = new System.Diagnostics.EventLog();
            this.AutoLog = false;
            if (!System.Diagnostics.EventLog.SourceExists("DNSchanger"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "DNSchanger", "Log");
            }
            // configure the event log instance to use this source name
            eventLog1.Source = "DNSchanger";
            eventLog1.Log = "Log";
            if (File.Exists(@"C:\\DNS\debug.txt"))
            {
                isDebug = true;
            }
            timer.Interval = 10000; // 6 seconds
            timer.Elapsed += new ElapsedEventHandler(OnTimer);
            timer.Start();
        }
        private static void OnTimer(object sender, ElapsedEventArgs args)
        {
            NetworkInterface currentInt = GetActiveEthernetOrWifiNetworkInterface();
            bool workdns = false;
            if (currentInt != null)
            {
                if (GetDnsAdress(currentInt).ToString() == selfDNS)
                {
                    try
                    {
                        IPHostEntry hostInfo = Dns.GetHostByName("www.google.com");
                        workdns = true;

                    }
                    catch (Exception)
                    {


                    }
                    if ((!workdns) || (!PingHost(selfDNS)))
                    {
                        if (IsthereNet())
                        {
                            Write(counter.ToString() + ". try - Cant ping self dns server!");
                            counter++;
                        }
                        else
                        {
                            Write("Cant resolve or ping, no internet");
                        }

                    }
                    if (counter == 3)
                    {
                        counter = 0;
                        Write("Reset DNS to 8.8.8.8, set recheck to 60 sec", true);
                        eventLog1.WriteEntry("Reset to google DNS");
                        SetDNS("8.8.8.8");
                        timer.Interval = 60000;
                    }
                }
                else
                {
                    if (PingHost(selfDNS))
                    {
                        Write("Set Self DNS", true);
                        SetDNS(selfDNS);
                        eventLog1.WriteEntry("Set Self DNS server");

                       timer.Interval = 20000;
                    }
                    else
                    {
                        Write("Cant ping self dns, cant set self back");
                    }
                }
            }
            else
            {
                Write("No active interface, no actions");
            }




        }
        protected override void OnStart(string[] args)
        {
            Write("DNS Changer started", true);
            eventLog1.WriteEntry("DNS Changer started");
        }

        protected override void OnStop()
        {
            Write("DNS Changer stopped", true);
            eventLog1.WriteEntry("DNS Changer stopped");
        }


        private static void Write(string inp, bool defdeb = false)
        {
            if (isDebug)
            {
                //Console.WriteLine(DateTime.Now + " | " + inp);
                
                using (StreamWriter w = File.AppendText(@"C:\\DNS\log.txt"))
                {
                    w.WriteLine(DateTime.Now + " | " + inp);
                }
            }
            else
            {
                if (defdeb)
                {
                    using (StreamWriter w = File.AppendText(@"C:\\DNS\log.txt"))
                    {
                        w.WriteLine(DateTime.Now + " | " + inp);
                    }
                }
            }
           
        }

      
        private static bool IsthereNet()
        {
            if (!PingHost("8.8.8.8") && !PingHost("4.2.2.1") && !PingHost("1.1.1.1"))
            {
                Write("Theres is no internet");
                return false;
            }
            else
            {
                Write("Theres is internet");
                return true;
            }
        }

        public static NetworkInterface GetActiveEthernetOrWifiNetworkInterface()
        {
            var Nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(
                a => a.OperationalStatus == OperationalStatus.Up &&
                (a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || a.NetworkInterfaceType == NetworkInterfaceType.Ethernet) &&
                a.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily.ToString() == "InterNetwork"));

            return Nic;
        }
        public static void SetDNS(string DnsString)
        {

            string[] Dns = { DnsString };
            var CurrentInterface = GetActiveEthernetOrWifiNetworkInterface();
            if (CurrentInterface == null) return;

            ManagementClass objMC = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection objMOC = objMC.GetInstances();
            foreach (ManagementObject objMO in objMOC)
            {
                if ((bool)objMO["IPEnabled"])
                {
                    if (objMO["Description"].ToString().Equals(CurrentInterface.Description))
                    {
                        ManagementBaseObject objdns = objMO.GetMethodParameters("SetDNSServerSearchOrder");
                        if (objdns != null)
                        {

                            objdns["DNSServerSearchOrder"] = Dns;
                            objMO.InvokeMethod("SetDNSServerSearchOrder", objdns, null);
                        }
                    }
                }
            }
        }
        public static void UnsetDNS()
        {
            var CurrentInterface = GetActiveEthernetOrWifiNetworkInterface();
            if (CurrentInterface == null) return;
            ManagementClass mClass = new ManagementClass("Win32_NetworkAdapterConfiguration");
            ManagementObjectCollection mObjCol = mClass.GetInstances();
            foreach (ManagementObject mObj in mObjCol)
            {
                if ((bool)mObj["IPEnabled"])
                {
                    ManagementBaseObject mboDNS = mObj.GetMethodParameters("SetDNSServerSearchOrder");
                    if (mboDNS != null)
                    {
                        mboDNS["DNSServerSearchOrder"] = null;
                        mObj.InvokeMethod("SetDNSServerSearchOrder", mboDNS, null);

                    }
                }
            }

        }
        private static IPAddress GetDnsAdress(NetworkInterface networkInterface)
        {
            if (networkInterface.OperationalStatus == OperationalStatus.Up)
            {
                IPInterfaceProperties ipProperties = networkInterface.GetIPProperties();
                IPAddressCollection dnsAddresses = ipProperties.DnsAddresses;

                foreach (IPAddress dnsAdress in dnsAddresses)
                {
                    return dnsAdress;
                }
            }
            throw new InvalidOperationException("Unable to find DNS Address");
        }



        private static bool PingHost(string nameOrAddress)
        {
            bool pingable = false;
            Ping pinger = null;

            try
            {
                pinger = new Ping();
                PingReply reply = pinger.Send(nameOrAddress, 500);
                pingable = reply.Status == IPStatus.Success;
                Write("Pingtry: " + nameOrAddress);
            }
            catch (PingException)
            {
                // Discard PingExceptions and return false;
            }
            finally
            {
                if (pinger != null)
                {
                    pinger.Dispose();
                }
            }

            return pingable;
        }






    }
}
