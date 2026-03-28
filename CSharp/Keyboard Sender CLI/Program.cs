using System;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using Drivers;
using HIDCtrl;
using KeyboardUtils;
using Microsoft.Win32.SafeHandles;

namespace KeyboardSenderCLI
{
    internal abstract class Program
    {
        private const ushort VendorId = (ushort)DriversConst.TtcVendorid;
        private const ushort ProductId = (ushort)DriversConst.TtcProductidKeyboard;
        private const uint DigcfPresent = 0x00000002;
        private const uint DigcfDeviceinterface = 0x00000010;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const int ErrorInsufficientBuffer = 122;
        private const int ErrorNoMoreItems = 259;

        // P/Invoke for HID enumeration
        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        private static extern void HidD_GetHidGuid(out Guid classGuid);

        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        private static extern bool HidD_GetAttributes(SafeHandle hidDeviceObject, ref HidController.HiddAttributes attributes);

        [DllImport("SetupApi.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent,
            int flags);

        [DllImport("Setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo,
            ref Guid interfaceClassGuid, uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo,
            ref SpDeviceInterfaceData deviceInterfaceData, IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(string fileName, uint fileAccess, uint fileShare,
            IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr template);

        [DllImport("Setupapi.dll")]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDeviceInterfaceData
        {
            public uint cbSize;
            public Guid interfaceClassGuid;
            public int flags;
            private IntPtr reserved;
        }

        private static void LogToConsole(object sender, LogArgs e)
        {
            Console.WriteLine("[HID] " + e.Msg);
        }

        private static bool TryGetDevicePath(IntPtr devInfo, ref SpDeviceInterfaceData ifData, out string path)
        {
            path = null;

            uint needed;
            var result = SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out needed, IntPtr.Zero);
            var error = Marshal.GetLastWin32Error();
            if (!result && error != ErrorInsufficientBuffer)
                return false;

            var detailPtr = Marshal.AllocHGlobal((int)needed);
            try
            {
                Marshal.WriteInt32(detailPtr, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, detailPtr, needed, out needed, IntPtr.Zero))
                    return false;

                var pathPtr = IntPtr.Add(detailPtr, 4);
                path = Marshal.PtrToStringAuto(pathPtr);
                return !string.IsNullOrEmpty(path);
            }
            finally
            {
                Marshal.FreeHGlobal(detailPtr);
            }
        }

        private static SafeFileHandle OpenHandle(string path)
        {
            var handle = CreateFile(path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, IntPtr.Zero,
                OpenExisting, 0, IntPtr.Zero);
            return !handle.IsInvalid
                ? handle
                : CreateFile(path, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        }

        private static void Main()
        {
            Console.WriteLine("=== Keyboard Sender CLI ===");
            Console.WriteLine();

            CheckDriverInstalled();
            Console.WriteLine();
            ScanHidDevices();

            Console.WriteLine();
            Console.WriteLine("Attempting HID connection (VendorID=0x" + VendorId.ToString("X4") + " ProductID=0x" +
                              ProductId.ToString("X4") + ")...");

            var hid = new HidController();
            hid.OnLog += new EventHandler<LogArgs>(LogToConsole);
            hid.VendorId = VendorId;
            hid.ProductId = ProductId;

            hid.Connect();

            if (!hid.Connected)
            {
                Console.WriteLine("[ERROR] Failed to connect.");
                Environment.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("Sending test keystrokes: 'Hello'");
            SendText(hid, "Hello", 50, 100);

            Console.WriteLine("Releasing all keys...");
            Send(hid, 0, 0, 0, 0, 0, 0, 0, 0);

            Console.WriteLine("Disconnecting...");
            hid.Disconnect();

            Console.WriteLine("Done.");
        }

        private static void ScanHidDevices()
        {
            Console.WriteLine("--- HID Device Scan ---");
            HidD_GetHidGuid(out var hidGuid);
            Console.WriteLine("HID GUID: " + hidGuid);
            Console.WriteLine("Process bitness: " + (Environment.Is64BitProcess ? "64-bit" : "32-bit"));

            var devInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                (int)(DigcfPresent | DigcfDeviceinterface));
            if (devInfo == new IntPtr(-1))
            {
                Console.WriteLine("[ERROR] SetupDiGetClassDevs failed.");
                return;
            }

            var found = 0;
            try
            {
                for (uint i = 0; ; i++)
                {
                    var ifData = new SpDeviceInterfaceData();
                    ifData.cbSize = (uint)Marshal.SizeOf(ifData);
                    if (!SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, i, ref ifData))
                    {
                        if (Marshal.GetLastWin32Error() != ErrorNoMoreItems)
                        {
                            Console.WriteLine(
                                "[WARN] SetupDiEnumDeviceInterfaces failed before enumeration completed.");
                        }

                        Console.WriteLine("  Scanned " + i + " interfaces, " + found + " accessible.");
                        break;
                    }

                    string path;
                    if (!TryGetDevicePath(devInfo, ref ifData, out path))
                        continue;

                    var handle = OpenHandle(path);
                    if (handle.IsInvalid)
                        continue;

                    using (handle)
                    {
                        var attr = new HidController.HiddAttributes();
                        attr.Size = Marshal.SizeOf(attr);

                        if (HidD_GetAttributes(handle, ref attr))
                        {
                            var match = (attr.VendorId == VendorId && attr.ProductId == ProductId) ? " <-- TARGET" : "";
                            Console.WriteLine("  [" + i + "] VID=0x" + attr.VendorId.ToString("X4") + " PID=0x" +
                                              attr.ProductId.ToString("X4") + " VER=0x" + attr.VersionNumber.ToString("X4") + match);
                            Console.WriteLine("       " + path);
                            found++;
                        }
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }

            if (found == 0)
                Console.WriteLine("  No accessible HID devices found.");
        }

        private static void CheckDriverInstalled()
        {
            Console.WriteLine("--- WMI Driver Check ---");
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                           "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%Tetherscript%'"))
                {
                    foreach (var o in searcher.Get())
                    {
                        var obj = (ManagementObject)o;
                        Console.WriteLine("[OK] " + obj["Name"] + " | " + obj["PNPDeviceID"] + " | " + obj["Status"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] WMI query failed: " + ex.Message);
            }
        }

        private static void SendText(HidController hid, string text, int keyDownMs, int interkeyMs)
        {
            var kbUtils = new KbUtils();
            foreach (var ch in text)
            {
                var upper = ch == char.ToUpper(ch) && char.IsLetter(ch);
                var modifier = upper ? (byte)2 : (byte)0;
                var keyCode = kbUtils.GetKeyKeyCode(char.ToLower(ch).ToString());
                if (keyCode == 0)
                {
                    Console.WriteLine("  [SKIP] No keycode for '" + ch + "'");
                    continue;
                }

                Console.WriteLine("  Pressing '" + ch + "' (keycode=" + keyCode + ", modifier=" + modifier + ")");
                Send(hid, modifier, 0, keyCode, 0, 0, 0, 0, 0);
                Thread.Sleep(keyDownMs);
                Send(hid, 0, 0, 0, 0, 0, 0, 0, 0);
                Thread.Sleep(interkeyMs);
            }
        }

        private static void Send(HidController hid, byte modifier, byte padding,
            byte k0, byte k1, byte k2, byte k3, byte k4, byte k5)
        {
            var data = new SetFeatureKeyboard
            {
                ReportID = 1,
                CommandCode = 2,
                Timeout = 5000 / 5,
                Modifier = modifier,
                Padding = padding,
                Key0 = k0,
                Key1 = k1,
                Key2 = k2,
                Key3 = k3,
                Key4 = k4,
                Key5 = k5
            };
            var size = Marshal.SizeOf(data);
            var buf = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(data, ptr, false);
            Marshal.Copy(ptr, buf, 0, size);
            Marshal.FreeHGlobal(ptr);
            hid.SendData(buf, (uint)size);
        }
    }
}
