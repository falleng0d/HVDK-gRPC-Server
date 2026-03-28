using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// ReSharper disable UnusedMember.Global

//this is the minimal amount of code to allow reading from and writing to the device driver.
//you should consider using a thread for your reading (and maybe writing) code.

namespace HIDCtrl
{
    public class LogArgs : EventArgs
    {
        public string Msg;
    }

    internal class HidController
    {
        public event EventHandler<LogArgs> OnLog;

        private Guid _hidGuid;
        private SafeFileHandle _fDevHandle;

        public Boolean Connected { get; set; }

        public ushort ProductId { get; set; }

        public ushort VendorId { get; set; }

        public enum DiGetClassFlags : uint
        {
            DigcfPresent = 0x00000002,
            DigcfDeviceinterface = 0x00000010,
        }

        public const uint FileFlagOverlapped = 0x40000000;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const int ErrorInsufficientBuffer = 122;
        private const int ErrorNoMoreItems = 259;

        private readonly IntPtr _invalidHandleValue = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        public struct SpDeviceInterfaceData
        {
            public uint cbSize;
            public Guid interfaceClassGuid;
            public Int32 flags;
            private IntPtr reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 1)]
        public struct SpDeviceInterfaceDetailData
        {
            public uint cbSize;
            public char devicePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SpDevinfoData
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        public struct HiddAttributes
        {
            public Int32 Size;
            public UInt16 VendorId;
            public UInt16 ProductId;
            public UInt16 VersionNumber;
        }

        //------------------------------------------------------------------------------------------------------

        [DllImport("SetupApi.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, int flags);

        [DllImport("Setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiEnumDeviceInterfaces(IntPtr hDevInfo, IntPtr devInfo,
            ref Guid interfaceClassGuid, uint memberIndex, ref SpDeviceInterfaceData deviceInterfaceData);

        [DllImport(@"setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern Boolean SetupDiGetDeviceInterfaceDetail(
            IntPtr hDevInfo,
            ref SpDeviceInterfaceData deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("Setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);

        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        private static extern void HidD_GetHidGuid(out Guid classGuid);

        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        private static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HiddAttributes attributes);

        [DllImport("HID.dll", CharSet = CharSet.Auto)]
        private static extern bool HidD_GetNumInputBuffers(SafeFileHandle hidDeviceObject, ref uint numberBuffers);

        [DllImport("hid.dll", CharSet = CharSet.Auto)]
        private static extern bool HidD_SetNumInputBuffers(SafeFileHandle hidDeviceObject, uint bufferLength);

        [DllImport("hid.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] buffer, uint bufferLength);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
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
        private void DoLog(string msg)
        {
            if (OnLog != null)
            {
                var args = new LogArgs();
                args.Msg = msg;
                OnLog(this, args);
            }
        }

        private static bool TryGetDevicePath(IntPtr pnpHandle, ref SpDeviceInterfaceData devInterfaceData,
            out string devicePath)
        {
            devicePath = null;

            uint needed;
            var result = SetupDiGetDeviceInterfaceDetail(pnpHandle, ref devInterfaceData, IntPtr.Zero, 0, out needed,
                IntPtr.Zero);
            var error = Marshal.GetLastWin32Error();
            if (!result && error != ErrorInsufficientBuffer)
            {
                return false;
            }

            var detailData = Marshal.AllocHGlobal((int)needed);
            try
            {
                Marshal.WriteInt32(detailData, IntPtr.Size == 8 ? 8 : 6);
                if (!SetupDiGetDeviceInterfaceDetail(pnpHandle, ref devInterfaceData, detailData, needed, out needed,
                        IntPtr.Zero))
                {
                    return false;
                }

                var pathPtr = IntPtr.Add(detailData, 4);
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
            var handle = CreateFile(devicePath, GenericRead | GenericWrite,
                FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (!handle.IsInvalid)
            {
                return handle;
            }

            return CreateFile(devicePath, 0, FileShareRead | FileShareWrite, IntPtr.Zero, OpenExisting, 0,
                IntPtr.Zero);
        }

        //connect to the driver.  We'll iterate through all present HID devices and find the one that matches our target VENDORID and PRODUCTID
        public void Connect()
        {
            DoLog("Connecting...");
            if (Connected)
            {
                DoLog("Already connected.");
                return;
            }

            HidD_GetHidGuid(out _hidGuid);

            var pnPHandle = SetupDiGetClassDevs(ref _hidGuid, IntPtr.Zero, IntPtr.Zero,
                (int)(DiGetClassFlags.DigcfPresent | DiGetClassFlags.DigcfDeviceinterface));
            if (pnPHandle == _invalidHandleValue)
            {
                DoLog("Connect: SetupDiGetClassDevs failed.");
                return;
            }

            try
            {
                for (uint i = 0;; i++)
                {
                    var devInterfaceData = new SpDeviceInterfaceData();
                    devInterfaceData.cbSize = (uint)Marshal.SizeOf(devInterfaceData);
                    if (!SetupDiEnumDeviceInterfaces(pnPHandle, IntPtr.Zero, ref _hidGuid, i, ref devInterfaceData))
                    {
                        var error = Marshal.GetLastWin32Error();
                        if (error != ErrorNoMoreItems)
                        {
                            DoLog("Connect: SetupDiEnumDeviceInterfaces failed.");
                        }

                        break;
                    }

                    string devicePath;
                    if (!TryGetDevicePath(pnPHandle, ref devInterfaceData, out devicePath))
                    {
                        continue;
                    }

                    var devHandle = OpenDeviceHandle(devicePath);
                    if (devHandle.IsInvalid)
                    {
                        continue;
                    }

                    var hidAttributes = new HiddAttributes();
                    hidAttributes.Size = Marshal.SizeOf(hidAttributes);
                    var success = HidD_GetAttributes(devHandle, ref hidAttributes);
                    if (success && hidAttributes.VendorId == VendorId && hidAttributes.ProductId == ProductId)
                    {
                        _fDevHandle = devHandle;
                        Connected = true;
                        DoLog("Connected.");
                        return;
                    }

                    devHandle.Close();
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(pnPHandle);
            }
        }

        //disconnect
        public void Disconnect()
        {
            //uninitialize some of our connect code
            //getting occasional error on shutdown due to something being left open...
            //not sure what effect garbage collector has on this.
            Connected = false;
            if (_fDevHandle != null && !_fDevHandle.IsClosed)
            {
                _fDevHandle.Close();
            }
        }

        //send data to the driver
        public Boolean SendData(byte[] buffer, uint bufferLength)
        {
            var res = false;
            if (Connected)
            {
                res = HidD_SetFeature(_fDevHandle, buffer, bufferLength + 1);
            }

            return res;
        }

        //read data from the driver.  This ideally should be in a thread
        //in this example, we oversimplified the possible effects of overlapped data reads since readfilex is asynchronous
        public Boolean ReadData(byte[] buffer, uint bufferLength)
        {
            var res = false;
            if (Connected)
            {
                if (!_fDevHandle.IsInvalid)

                {
                    var overlapped = new NativeOverlapped();
                    overlapped.EventHandle = IntPtr.Zero;
                    var res1 = ReadFileEx(_fDevHandle, buffer, bufferLength, ref overlapped, IntPtr.Zero);
                    res = true;
                }
            }

            return res;
        }
    }
}
