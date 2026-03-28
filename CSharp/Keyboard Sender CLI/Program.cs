using System;
using System.Management;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using HIDCtrl;
using Drivers;
using KeyboardUtils;

namespace KeyboardSenderCLI
{
    class Program
    {
        static readonly ushort VendorID = (ushort)DriversConst.TTC_VENDORID;
        static readonly ushort ProductID = (ushort)DriversConst.TTC_PRODUCTID_KEYBOARD;
        const uint DIGCF_PRESENT = 0x00000002;
        const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_SHARE_READ = 0x00000001;
        const uint FILE_SHARE_WRITE = 0x00000002;
        const uint OPEN_EXISTING = 3;
        const int ERROR_INSUFFICIENT_BUFFER = 122;
        const int ERROR_NO_MORE_ITEMS = 259;

        // P/Invoke for HID enumeration
        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        static extern void HidD_GetHidGuid(out Guid ClassGuid);

        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        static extern bool HidD_GetAttributes(SafeHandle HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("SetupApi.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, int Flags);

        [DllImport("Setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr hDevInfo, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern SafeFileHandle CreateFile(string fileName, uint fileAccess, uint fileShare, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr template);

        [DllImport("Setupapi.dll")]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);

        [StructLayout(LayoutKind.Sequential)]
        struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid interfaceClassGuid;
            public int flags;
            private IntPtr reserved;
        }

        public struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        static bool TryGetDevicePath(IntPtr devInfo, ref SP_DEVICE_INTERFACE_DATA ifData, out string path)
        {
            path = null;

            uint needed;
            bool result = SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out needed, IntPtr.Zero);
            int error = Marshal.GetLastWin32Error();
            if (!result && error != ERROR_INSUFFICIENT_BUFFER)
                return false;

            IntPtr detailPtr = Marshal.AllocHGlobal((int)needed);
            try
            {
                Marshal.WriteInt32(detailPtr, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, detailPtr, needed, out needed, IntPtr.Zero))
                    return false;

                IntPtr pathPtr = IntPtr.Add(detailPtr, 4);
                path = Marshal.PtrToStringAuto(pathPtr);
                return !string.IsNullOrEmpty(path);
            }
            finally
            {
                Marshal.FreeHGlobal(detailPtr);
            }
        }

        static SafeFileHandle OpenHandle(string path)
        {
            SafeFileHandle handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (!handle.IsInvalid)
                return handle;

            return CreateFile(path, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        }

        static void Main(string[] args)
        {
            Console.WriteLine("=== Keyboard Sender CLI ===");
            Console.WriteLine();

            CheckDriverInstalled();
            Console.WriteLine();
            ScanHidDevices();

            Console.WriteLine();
            Console.WriteLine("Attempting HID connection (VendorID=0x" + VendorID.ToString("X4") + " ProductID=0x" + ProductID.ToString("X4") + ")...");

            var hid = new HIDController();
            hid.OnLog += (s, e) => Console.WriteLine("[HID] " + e.Msg);
            hid.VendorID = VendorID;
            hid.ProductID = ProductID;

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

        static void ScanHidDevices()
        {
            Console.WriteLine("--- HID Device Scan ---");
            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);
            Console.WriteLine("HID GUID: " + hidGuid);
            Console.WriteLine("Process bitness: " + (Environment.Is64BitProcess ? "64-bit" : "32-bit"));

            IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero, (int)(DIGCF_PRESENT | DIGCF_DEVICEINTERFACE));
            if (devInfo == new IntPtr(-1))
            {
                Console.WriteLine("[ERROR] SetupDiGetClassDevs failed.");
                return;
            }

            int found = 0;
            try
            {
                for (uint i = 0; ; i++)
                {
                    SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
                    ifData.cbSize = (uint)Marshal.SizeOf(ifData);
                    if (!SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, i, ref ifData))
                    {
                        if (Marshal.GetLastWin32Error() != ERROR_NO_MORE_ITEMS)
                        {
                            Console.WriteLine("[WARN] SetupDiEnumDeviceInterfaces failed before enumeration completed.");
                        }

                        Console.WriteLine("  Scanned " + i + " interfaces, " + found + " accessible.");
                        break;
                    }

                    string path;
                    if (!TryGetDevicePath(devInfo, ref ifData, out path))
                        continue;

                    SafeFileHandle handle = OpenHandle(path);
                    if (handle.IsInvalid)
                        continue;

                    using (handle)
                    {
                        HIDD_ATTRIBUTES attr = new HIDD_ATTRIBUTES();
                        attr.Size = Marshal.SizeOf(attr);
                        if (HidD_GetAttributes(handle, ref attr))
                        {
                            string match = (attr.VendorID == VendorID && attr.ProductID == ProductID) ? " <-- TARGET" : "";
                            Console.WriteLine("  [" + i + "] VID=0x" + attr.VendorID.ToString("X4") + " PID=0x" + attr.ProductID.ToString("X4") + match);
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

        static void CheckDriverInstalled()
        {
            Console.WriteLine("--- WMI Driver Check ---");
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Name LIKE '%Tetherscript%'"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        Console.WriteLine("[OK] " + obj["Name"] + " | " + obj["PNPDeviceID"] + " | " + obj["Status"]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WARN] WMI query failed: " + ex.Message);
            }
        }

        static void SendText(HIDController hid, string text, int keyDownMs, int interkeyMs)
        {
            var kbUtils = new KbUtils();
            foreach (char ch in text)
            {
                bool upper = ch == char.ToUpper(ch) && char.IsLetter(ch);
                byte modifier = upper ? (byte)2 : (byte)0;
                byte keyCode = kbUtils.GetKeyKeyCode(char.ToLower(ch).ToString());
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

        static void Send(HIDController hid, byte modifier, byte padding,
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
            int size = Marshal.SizeOf(data);
            byte[] buf = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(data, ptr, false);
            Marshal.Copy(ptr, buf, 0, size);
            Marshal.FreeHGlobal(ptr);
            hid.SendData(buf, (uint)size);
        }
    }
}
