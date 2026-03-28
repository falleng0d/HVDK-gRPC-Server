using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Threading;
using System.Runtime.CompilerServices;

//this is the minimal amount of code to allow reading from and writing to the device driver.
//you should consider using a thread for your reading (and maybe writing) code.

namespace HIDCtrl
{
    public class LogArgs : EventArgs
    {
        public string Msg;
    }

    class HIDController
    {
        public event EventHandler<LogArgs> OnLog;

        public Guid HIDGuid;
        protected Boolean FConnected = false;
        protected ushort FProductID;
        protected ushort FVendorID;
        protected SafeFileHandle FDevHandle;
        protected String FDevicePathName;

        public HIDController()
        {
        }

        public Boolean Connected
        {
            get { return FConnected; }
            set { FConnected = value; }
        }

        public ushort ProductID
        {
            get { return FProductID; }
            set { FProductID = value; }
        }

        public ushort VendorID
        {
            get { return FVendorID; }
            set { FVendorID = value; }
        }

        public enum DiGetClassFlags : uint
        {
            DIGCF_PRESENT = 0x00000002,
            DIGCF_DEVICEINTERFACE = 0x00000010,
        }

        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int ERROR_NO_MORE_ITEMS = 259;

        IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid interfaceClassGuid;
            public Int32 flags;
            private IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 1)]
        public struct SP_DEVICE_INTERFACE_DETAIL_DATA
        {
            public uint cbSize;
            public char devicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        public struct HIDD_ATTRIBUTES
        {
            public Int32 Size;
            public UInt16 VendorID;
            public UInt16 ProductID;
            public UInt16 VersionNumber;
        }

        //------------------------------------------------------------------------------------------------------

        [DllImport("SetupApi.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SetupDiGetClassDevs(ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, int Flags);

        [DllImport("Setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo,
            ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiGetDeviceInterfaceDetail(
            IntPtr hDevInfo,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("Setupapi.dll", SetLastError = true)]
        static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);

        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        static extern void HidD_GetHidGuid(out Guid ClassGuid);

        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        static extern bool HidD_GetAttributes(SafeFileHandle HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        private static extern bool HidD_GetNumInputBuffers(SafeFileHandle HidDeviceObject, ref uint NumberBuffers);

        [DllImport("hid.dll", CharSet = CharSet.Auto)]
        private static extern bool HidD_SetNumInputBuffers(SafeFileHandle HidDeviceObject, uint BufferLength);

        [DllImport("hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool HidD_SetFeature(SafeFileHandle HidDeviceObject, byte[] Buffer, uint BufferLength);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint fileShare,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr template);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool ReadFileEx(
            SafeFileHandle hFile,
            [Out] byte[] lpbuffer,
            [In] uint nNumberOfBytesToRead,
            [In, Out] ref NativeOverlapped lpOverlapped,
            IntPtr lpCompletionRoutine);

        //------------------------------------------------------------------------------------------------------
        //------------------------------------------------------------------------------------------------------

        //make a log entry
        private void DoLog(string Msg)
        {
            if (OnLog != null)
            {
                LogArgs args = new LogArgs();
                args.Msg = Msg;
                OnLog(this, args);
            }
        }

        private static bool TryGetDevicePath(IntPtr pnpHandle, ref SP_DEVICE_INTERFACE_DATA devInterfaceData,
            out string devicePath)
        {
            devicePath = null;

            uint needed;
            bool result = SetupDiGetDeviceInterfaceDetail(pnpHandle, ref devInterfaceData, IntPtr.Zero, 0, out needed,
                IntPtr.Zero);
            int error = Marshal.GetLastWin32Error();
            if (!result && error != ERROR_INSUFFICIENT_BUFFER)
            {
                return false;
            }

            IntPtr detailData = Marshal.AllocHGlobal((int)needed);
            try
            {
                Marshal.WriteInt32(detailData, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(pnpHandle, ref devInterfaceData, detailData, needed, out needed,
                        IntPtr.Zero))
                {
                    return false;
                }

                IntPtr pathPtr = IntPtr.Add(detailData, 4);
                devicePath = Marshal.PtrToStringAuto(pathPtr);
                return !string.IsNullOrEmpty(devicePath);
            }
            finally
            {
                Marshal.FreeHGlobal(detailData);
            }
        }

        private static SafeFileHandle OpenDeviceHandle(string devicePath)
        {
            SafeFileHandle handle = CreateFile(devicePath, GENERIC_READ | GENERIC_WRITE,
                FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (!handle.IsInvalid)
            {
                return handle;
            }

            return CreateFile(devicePath, 0, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0,
                IntPtr.Zero);
        }

        //connect to the driver.  We'll iterate through all present HID devices and find the one that matches our target VENDORID and PRODUCTID
        public void Connect()
        {
            DoLog("Connecting...");
            if (FConnected)
            {
                DoLog("Already connected.");
                return;
            }

            HidD_GetHidGuid(out HIDGuid);

            IntPtr PnPHandle = SetupDiGetClassDevs(ref HIDGuid, IntPtr.Zero, IntPtr.Zero,
                (int)(DiGetClassFlags.DIGCF_PRESENT | DiGetClassFlags.DIGCF_DEVICEINTERFACE));
            if (PnPHandle == (IntPtr)INVALID_HANDLE_VALUE)
            {
                DoLog("Connect: SetupDiGetClassDevs failed.");
                return;
            }

            try
            {
                for (uint i = 0;; i++)
                {
                    SP_DEVICE_INTERFACE_DATA DevInterfaceData = new SP_DEVICE_INTERFACE_DATA();
                    DevInterfaceData.cbSize = (uint)Marshal.SizeOf(DevInterfaceData);
                    if (!SetupDiEnumDeviceInterfaces(PnPHandle, IntPtr.Zero, ref HIDGuid, i, ref DevInterfaceData))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != ERROR_NO_MORE_ITEMS)
                        {
                            DoLog("Connect: SetupDiEnumDeviceInterfaces failed.");
                        }

                        break;
                    }

                    string devicePath;
                    if (!TryGetDevicePath(PnPHandle, ref DevInterfaceData, out devicePath))
                    {
                        continue;
                    }

                    SafeFileHandle devHandle = OpenDeviceHandle(devicePath);
                    if (devHandle.IsInvalid)
                    {
                        continue;
                    }

                    HIDD_ATTRIBUTES HIDAttributes = new HIDD_ATTRIBUTES();
                    HIDAttributes.Size = Marshal.SizeOf(HIDAttributes);
                    Boolean success = HidD_GetAttributes(devHandle, ref HIDAttributes);
                    if (success && HIDAttributes.VendorID == FVendorID && HIDAttributes.ProductID == FProductID)
                    {
                        FDevicePathName = devicePath;
                        FDevHandle = devHandle;
                        FConnected = true;
                        DoLog("Connected.");
                        return;
                    }

                    devHandle.Close();
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(PnPHandle);
            }
        }

        //disconnect
        public void Disconnect()
        {
            //uninitialize some of our connect code
            //getting occasional error on shutdown due to something being left open...
            //not sure what effect garbage collector has on this.
            FConnected = false;
            if (FDevHandle != null && !FDevHandle.IsClosed)
            {
                FDevHandle.Close();
            }
        }

        //send data to the driver
        public Boolean SendData(byte[] Buffer, uint BufferLength)
        {
            bool res = false;
            if (FConnected)
            {
                res = HidD_SetFeature(FDevHandle, Buffer, BufferLength + 1);
            }

            return res;
        }

        //read data from the driver.  This ideally should be in a thread
        //in this example, we oversimplified the possible effects of overlapped data reads since readfilex is asynchronous
        public Boolean ReadData(byte[] Buffer, uint BufferLength)
        {
            bool res = false;
            if (FConnected)
            {
                if (!FDevHandle.IsInvalid)

                {
                    var overlapped = new NativeOverlapped();
                    overlapped.EventHandle = IntPtr.Zero;
                    bool res1 = ReadFileEx(FDevHandle, Buffer, BufferLength, ref overlapped, IntPtr.Zero);
                    res = true;
                }
            }

            return res;
        }
    }
}
