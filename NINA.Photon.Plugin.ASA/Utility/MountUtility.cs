#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using NINA.Photon.Plugin.ASA.Model;
using NINA.Photon.Plugin.ASA.Equipment;

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace NINA.Photon.Plugin.ASA.Utility
{
    public static class MountUtility
    {
        private static ISet<string> SupportedProducts = ImmutableHashSet.CreateRange(
            new[] {
                "10micron GM1000HPS",
                "10micron GM2000QCI",
                "10micron GM2000HPS",
                "10micron GM3000HPS",
                "10micron GM4000QCI",
                "10micron GM4000QCI 48V",
                "10micron GM4000HPS",
                "10micron AZ2000",
                "10micron AZ2000HPS",
                "10micron AZ4000HPS"
            });

        public static bool IsSupportedProduct(ProductFirmware productFirmware)
        {
            return SupportedProducts.Contains(productFirmware.ProductName);
        }

        private static double GetASCOMProfileDouble(string driverId, string name, string subkey, double defaultvalue)
        {
            if (double.TryParse(ASCOM.Com.Profile.GetValue(ASCOM.Common.DeviceTypes.Telescope, progId: driverId, valueName: name, subKey: subkey, defaultValue: ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return defaultvalue;
        }

        private static bool GetASCOMProfileBool(string driverId, string name, string subkey, bool defaultvalue)
        {
            if (bool.TryParse(ASCOM.Com.Profile.GetValue(ASCOM.Common.DeviceTypes.Telescope, progId: driverId, valueName: name, subKey: subkey, defaultValue: ""), out var result))
            {
                return result;
            }
            return defaultvalue;
        }

        private static string GetASCOMProfileString(string driverId, string name, string subkey, string defaultvalue)
        {
            return ASCOM.Com.Profile.GetValue(ASCOM.Common.DeviceTypes.Telescope, progId: driverId, valueName: name, subKey: subkey, defaultValue: defaultvalue);
        }

        public static MountAscomConfig GetMountAscomConfig(string driverId)
        {
            var registered = ASCOM.Com.Profile.IsRegistered(ASCOM.Common.DeviceTypes.Telescope, driverId);
            if (registered)
            {
                var profileJson = JsonConvert.SerializeObject(ASCOM.Com.Profile.GetValues(ASCOM.Common.DeviceTypes.Telescope, driverId));
                Logger.Info($"10u ASCOM driver configuration: {profileJson}");

                if (driverId == "ASCOM.tenmicron_mount.Telescope")
                {
                    return new MountAscomConfig()
                    {
                        EnableUncheckedRawCommands = GetASCOMProfileBool(driverId, "enable_unchecked_raw_commands", "mount_settings", true),
                        UseJ2000Coordinates = GetASCOMProfileBool(driverId, "use_J2000_coords", "mount_settings", false),
                        EnableSync = GetASCOMProfileBool(driverId, "enable_sync", "mount_settings", false),
                        UseSyncAsAlignment = GetASCOMProfileBool(driverId, "use_sync_as_alignment", "mount_settings", false),
                        RefractionUpdateFile = GetASCOMProfileString(driverId, "refraction_update_file", "mount_settings", "")
                    };
                }
            }
            return null;
        }

        public static bool ValidateMountAscomConfig(MountAscomConfig config)
        {
            if (config.EnableUncheckedRawCommands)
            {
                Notification.ShowError("Enable Unchecked Raw Commands cannnot be enabled. Open the ASCOM driver configuration and disable it, then reconnect");
                return false;
            }
            if (config.EnableSync && config.UseSyncAsAlignment)
            {
                Notification.ShowWarning("Use Sync as Alignment is enabled. It is recommended you disable this setting and build models explicitly");
            }
            if (config.UseJ2000Coordinates)
            {
                Notification.ShowWarning("ASCOM driver is configured to use J2000 coordinates. It is recommended you use JNow instead and reconnect");
            }
            return true;
        }

        // See: https://stackoverflow.com/questions/861873/wake-on-lan-using-c-sharp
        public static async Task WakeOnLan(string macAddress, string broadcastAddress, CancellationToken ct)
        {
            byte[] magicPacket = BuildMagicPacket(macAddress);

            if (IPAddress.TryParse(broadcastAddress, out var broadcastIpAddress))
            {
                await SendWakeOnLan(IPAddress.Any, broadcastIpAddress, magicPacket, ct);
            }

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces().Where((n) =>
                n.NetworkInterfaceType != NetworkInterfaceType.Loopback && n.OperationalStatus == OperationalStatus.Up))
            {
                ct.ThrowIfCancellationRequested();

                var iPInterfaceProperties = networkInterface.GetIPProperties();
                foreach (var multicastIPAddressInformation in iPInterfaceProperties.MulticastAddresses)
                {
                    ct.ThrowIfCancellationRequested();

                    var multicastIpAddress = multicastIPAddressInformation.Address;
                    // Ipv6: All hosts on LAN (with zone index)
                    if (multicastIpAddress.ToString().StartsWith("ff02::1%", StringComparison.OrdinalIgnoreCase))
                    {
                        var unicastIPAddressInformation = iPInterfaceProperties.UnicastAddresses.Where((u) =>
                            u.Address.AddressFamily == AddressFamily.InterNetworkV6 && !u.Address.IsIPv6LinkLocal).FirstOrDefault();
                        if (unicastIPAddressInformation != null)
                        {
                            await SendWakeOnLan(unicastIPAddressInformation.Address, multicastIpAddress, magicPacket, ct);
                            break;
                        }
                    }
                    else if (multicastIpAddress.ToString().Equals("224.0.0.1"))
                    {
                        // Ipv4: All hosts on LAN
                        var unicastIPAddressInformation = iPInterfaceProperties.UnicastAddresses.Where((u) =>
                            u.Address.AddressFamily == AddressFamily.InterNetwork && !iPInterfaceProperties.GetIPv4Properties().IsAutomaticPrivateAddressingActive).FirstOrDefault();
                        if (unicastIPAddressInformation != null)
                        {
                            await SendWakeOnLan(unicastIPAddressInformation.Address, multicastIpAddress, magicPacket, ct);
                            break;
                        }
                    }
                }
            }
        }

        private static byte[] BuildMagicPacket(string macAddress)
        {
            macAddress = Regex.Replace(macAddress, "[: -]", "");
            byte[] macBytes = new byte[6];
            for (int i = 0; i < 6; i++)
            {
                macBytes[i] = Convert.ToByte(macAddress.Substring(i * 2, 2), 16);
            }

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    for (int i = 0; i < 6; i++)
                    {
                        bw.Write((byte)0xff);
                    }
                    for (int i = 0; i < 16; i++)
                    {
                        bw.Write(macBytes);
                    }
                }
                return ms.ToArray();
            }
        }

        private static async Task SendWakeOnLan(IPAddress localIpAddress, IPAddress multicastIpAddress, byte[] magicPacket, CancellationToken ct)
        {
            using (var client = new UdpClient(new IPEndPoint(localIpAddress, 0)))
            {
                using (ct.Register(() => client.Close()))
                {
                    try
                    {
                        await client.SendAsync(magicPacket, magicPacket.Length, multicastIpAddress.ToString(), 9);
                    }
                    catch (Exception)
                    {
                        ct.ThrowIfCancellationRequested();
                        throw;
                    }
                }
            }
        }

        public static async Task<bool> IsResponding(IPAddress ipAddress, int port, CancellationToken ct)
        {
            try
            {
                using (var client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
                {
                    using (ct.Register(() => client.Close()))
                    {
                        client.SendTimeout = 2000;
                        client.ReceiveTimeout = 1000;

                        await client.ConnectAsync(ipAddress, port);
                        ct.ThrowIfCancellationRequested();

                        var command = ":GJD#";
                        var commandData = Encoding.ASCII.GetBytes(command);
                        var sentBytes = await client.SendAsync(new ArraySegment<byte>(commandData), SocketFlags.None);
                        ct.ThrowIfCancellationRequested();

                        // 14 bytes expected: JJJJJJJ.JJJJJ#
                        var receivedData = new byte[14];
                        var receivedBytes = await client.ReceiveAsync(new ArraySegment<byte>(receivedData), SocketFlags.None);
                        if (receivedBytes > 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        public static async Task<bool> WaitUntilResponding(IPAddress ipAddress, int port, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                if (await IsResponding(ipAddress, port, ct))
                {
                    return true;
                }
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
    }
}