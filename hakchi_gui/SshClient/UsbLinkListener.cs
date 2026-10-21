using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace com.clusterrr.ssh
{
    /// <summary>
    /// Fallback for the console's USB network link, which needs no name resolution:
    /// hakchi's boot scripts always give the console the link-local address
    /// 169.254.13.37/16. When this host has a 169.254.x.x address on an interface that
    /// is up (Windows APIPA, or the NetworkManager profile shipped for Linux) and that
    /// address answers on the SSH port, it is offered as a device. Covers hosts where
    /// mDNS is blocked by a firewall or missed on interface hot-plug.
    /// </summary>
    public class UsbLinkListener : IListener
    {
        public const int CHECK_INTERVAL_SECONDS = 3;
        public const int CONNECT_TIMEOUT_MS = 500;

        public IList<Device> Available
        {
            get
            {
                if (DateTime.Now.Subtract(lastChecked) > TimeSpan.FromSeconds(CHECK_INTERVAL_SECONDS))
                {
                    lastChecked = DateTime.Now;
                    check();
                }
                return devices;
            }
        }

        private readonly IPAddress address;
        private readonly ushort port;
        private List<Device> devices;
        private DateTime lastChecked;
        private bool wasReachable;

        public UsbLinkListener(string address, ushort port)
        {
            this.address = IPAddress.Parse(address);
            this.port = port;
            devices = new List<Device>();
            lastChecked = DateTime.MinValue;
        }

        private static bool hasLinkLocalInterface()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces()
                    // USB gadgets have no carrier detection and report Unknown on Linux.
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up || ni.OperationalStatus == OperationalStatus.Unknown)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Any(u => u.Address.AddressFamily == AddressFamily.InterNetwork
                        && u.Address.GetAddressBytes()[0] == 169 && u.Address.GetAddressBytes()[1] == 254);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("USB link: cannot enumerate interfaces: " + ex.Message);
                return false;
            }
        }

        private bool reachable()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    var result = socket.BeginConnect(address, port, null, null);
                    if (!result.AsyncWaitHandle.WaitOne(CONNECT_TIMEOUT_MS))
                    {
                        return false;
                    }
                    socket.EndConnect(result);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private void check()
        {
            bool found = hasLinkLocalInterface() && reachable();
            if (found != wasReachable)
            {
                Trace.WriteLine(found
                    ? $"USB link: console answering at {address}:{port}"
                    : $"USB link: {address} no longer reachable");
                wasReachable = found;
            }
            devices.Clear();
            if (found)
            {
                devices.Add(new Device() { Addresses = new List<IPAddress>() { address }, Port = port });
            }
        }

        public void Cycle()
        {
            devices.Clear();
            lastChecked = DateTime.MinValue;
        }

        public void Dispose()
        {
            devices.Clear();
        }
    }
}
