using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Tmds.MDns;

namespace com.clusterrr.ssh
{
    public class MdnsListener : IListener
    {
        public IList<Device> Available
        {
            get; private set;
        }

        ServiceBrowser serviceBrowser;
        string serviceName;
        string serviceType;
        Timer refreshTimer;

        // How often to re-scan network interfaces off Windows. Mono raises NetworkChange events
        // when the console's USB network interface appears, but typically before NetworkManager
        // has assigned it an address, so the browser records no local subnet for it and drops the
        // console's answers. Re-scanning picks the address up a moment later.
        const int REFRESH_INTERVAL_MS = 2000;

        public MdnsListener(string name, string type)
        {
            Available = new List<Device>();
            serviceName = name;
            serviceType = type;

            // enable service browser
            serviceBrowser = new ServiceBrowser();
            serviceBrowser.ServiceAdded += onServiceAdded;
            serviceBrowser.ServiceChanged += onServiceChanged;
            serviceBrowser.ServiceRemoved += onServiceRemoved;
            serviceBrowser.NetworkInterfaceAdded += (s, e) => Trace.WriteLine($"mDNS: watching interface {e.NetworkInterface.Name} ({describe(e.NetworkInterface)})");
            serviceBrowser.NetworkInterfaceRemoved += (s, e) => Trace.WriteLine($"mDNS: interface {e.NetworkInterface.Name} gone");
            serviceBrowser.StartBrowse(type);
            if (!com.clusterrr.hakchi_gui.Shared.isWindows)
            {
                refreshTimer = new Timer(_ =>
                {
                    try { serviceBrowser.RefreshNetworkInterfaces(); }
                    catch (Exception ex) { Trace.WriteLine("mDNS: interface refresh failed: " + ex.Message); }
                }, null, REFRESH_INTERVAL_MS, REFRESH_INTERVAL_MS);
            }
        }

        private static string describe(System.Net.NetworkInformation.NetworkInterface ni)
        {
            try
            {
                var addrs = ni.GetIPProperties().UnicastAddresses.Select(u => u.Address.ToString());
                return $"{ni.OperationalStatus}, {(addrs.Any() ? string.Join(", ", addrs) : "no address")}";
            }
            catch (Exception ex)
            {
                return ex.GetType().Name;
            }
        }

        public void Cycle()
        {
            // no-op
        }

        public void Dispose()
        {
            refreshTimer?.Dispose();
            refreshTimer = null;
            serviceBrowser.StopBrowse();
            Available.Clear();
        }

        private void debugAnnouncement(string header, ServiceAnnouncement a)
        {
            Trace.WriteLine(header);
            Trace.Indent();
            Trace.WriteLine("Instance: " + a.Instance);
            Trace.WriteLine("Type: " + a.Type);
            Trace.WriteLine("IP: " + string.Join(", ", a.Addresses));
            Trace.WriteLine("Port: " + a.Port);
            Trace.WriteLine("Txt: " + string.Join(", ", a.Txt));
            Trace.Unindent();
        }

        private void onServiceAdded(object sender, ServiceAnnouncementEventArgs e)
        {
            // ignore other services
            if (e.Announcement.Instance != this.serviceName)
            {
                return;
            }

            // debug
            debugAnnouncement("Service added:", e.Announcement);

            // create entry
            var dev = new Device()
            {
                Addresses = e.Announcement.Addresses,
                Port = e.Announcement.Port,
            };

            // build device info
            foreach (var txt in e.Announcement.Txt)
            {
                var tokens = txt.Split('=');
                if (tokens.Length == 2)
                {
                    switch (tokens[0])
                    {
                        case "hwid":
                            dev.UniqueID = tokens[1].Replace(" ", "").ToUpper();
                            break;

                        case "type":
                            dev.ConsoleType = tokens[1];
                            break;

                        case "region":
                            dev.ConsoleRegion = tokens[1];
                            break;
                    }
                }
            }

            // check to avoid adding duplicate devices
            foreach (var a in Available)
            {
                if (dev.Addresses.SequenceEqual(e.Announcement.Addresses))
                {
                    Trace.WriteLine("Duplicate announce for addresses: " + string.Join(", ", e.Announcement.Addresses));
                    return;
                }
                if (dev.UniqueID == a.UniqueID)
                {
                    Trace.WriteLine("Duplicate announce for same device: " + a.UniqueID);
                    return;
                }
            }

            Available.Add(dev);
        }

        private void onServiceChanged(object sender, ServiceAnnouncementEventArgs e)
        {
            // silently ignore other services
            if (e.Announcement.Instance != this.serviceName)
            {
                return;
            }
            debugAnnouncement("A service changed:", e.Announcement);
        }

        private void onServiceRemoved(object sender, ServiceAnnouncementEventArgs e)
        {
            // silently ignore other services
            if (e.Announcement.Instance != this.serviceName)
            {
                return;
            }
            debugAnnouncement("A service was removed:", e.Announcement);

            foreach (var a in Available)
            {
                if (a.Addresses.SequenceEqual(e.Announcement.Addresses))
                {
                    Available.Remove(a);
                    return;
                }
            }
            Trace.WriteLine("Service had not been detected before. Hmmm...");
        }

    }
}
