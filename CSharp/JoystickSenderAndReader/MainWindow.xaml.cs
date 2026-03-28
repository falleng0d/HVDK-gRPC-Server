using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Drivers;
using HIDCtrl;

//this app uses the minimal code to read and write the the virtual HID driver.

namespace JoystickSenderAndReader
{
    public partial class MainWindow : Window
    {
        //create the HIDController object
        private HidController _hid = new HidController();
        private DispatcherTimer _timer = new DispatcherTimer();
        private Boolean _closing = false;

        public MainWindow()
        {
            InitializeComponent();
            tbLog.AppendText("\r\n");
            //create the HIDController 
            _hid.OnLog += new EventHandler<LogArgs>(Log);
            _hid.VendorId = (ushort)DriversConst.TtcVendorid;                //the Tetherscript vendorid
            _hid.ProductId = (ushort)DriversConst.TtcProductidJoystick;     //the Tetherscript Virtual Joystick Driver productid
            _hid.Connect();
            //create a timer with a 15ms interval.
            //the timer will be used to send data to and read data from the driver.
            _timer.Interval = TimeSpan.FromMilliseconds(15);
            _timer.Tick += timer_Tick;
            _timer.Start();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _closing = true;
            _timer.Stop();
            _hid.Disconnect();
        }

        //this log event will be called by HIDController
        public void Log(object s, LogArgs e)
        {
            tbLog.AppendText(e.Msg + "\r\n");
        }

        //the timer which runs every 50ms.  It sends data to the joystick drive, then reads it back
        //normally you would put the reading code in a thread instead of the way we are doing it here
        private void timer_Tick(object sender, EventArgs e)
        {
            if (!_closing)
            {
                Send_Data_To_Joystick();
                Get_Data_From_Joystick();
            }
        }

        //here we send data to the joystick
        private void Send_Data_To_Joystick()
        {
            var joyData = new SetFeatureJoy();
            joyData.ReportID = 1;
            joyData.CommandCode = 2;
            joyData.X = (UInt16)slX.Value;
            joyData.Y = (UInt16)slY.Value;
            joyData.Z = (UInt16)slZ.Value;
            joyData.rX = (UInt16)slrX.Value;
            joyData.rY = (UInt16)slrY.Value;
            joyData.rZ = (UInt16)slrZ.Value;
            joyData.slider = (UInt16)slSlider.Value;
            joyData.wheel = (UInt16)slWheel.Value;
            joyData.dial = (UInt16)slDial.Value;
            joyData.hat = (Byte)slHat.Value;
            //non-hat buttons are represented as a bit array of 16 bytes, 1 bit per button.
            //we could have used a byte array here, but for simplicity we instead just declared 16 byte variables.
            //each button is represented by 1 bit.  The bit is 1 if pressed, 0 if not pressed.
            //in this example, we'll only show the first 8 buttons and also button 128.
            joyData.btn0 = 0;
            if (cb0.IsChecked == true) { joyData.btn0 = (byte)(joyData.btn0 | (1 << 0)); }
            if (cb1.IsChecked == true) { joyData.btn0 = (byte)(joyData.btn0 | (1 << 1)); }
            if (cb2.IsChecked == true) { joyData.btn0 = (byte)(joyData.btn0 | (1 << 2)); }
            if (cb3.IsChecked == true) { joyData.btn0 = (byte)(joyData.btn0 | (1 << 3)); }
            if (cb4.IsChecked == true) { joyData.btn0 = (byte)(joyData.btn0 | (1 << 4)); }
            if (cb5.IsChecked == true) { joyData.btn0 = (byte)(joyData.btn0 | (1 << 5)); }
            if (cb6.IsChecked == true) { joyData.btn0 = (byte)(joyData.btn0 | (1 << 6)); }
            if (cb7.IsChecked == true) { joyData.btn0 = (byte)(joyData.btn0 | (1 << 7)); }
            joyData.btn1 = 0;
            if (cb8.IsChecked == true) { joyData.btn1 = (byte)(joyData.btn1 | (1 << 0)); }
            joyData.btn15 = 0;
            if (cb128.IsChecked == true) { joyData.btn15 = (byte)(joyData.btn15 | (1 << 0)); }
            //convert struct to buffer
            var buf = GetBytesSfj(joyData, Marshal.SizeOf(joyData));
            //send filled buffer to driver
            _hid.SendData(buf, (uint)Marshal.SizeOf(joyData));
        }

        //here we get data from the joystick
        private void Get_Data_From_Joystick()
        {
            var joyData = new GetDataJoy();
            //convert struct to buffer
            var buf = GetBytesGdj(joyData, Marshal.SizeOf(joyData));
            //send empty buffer to driver
            _hid.ReadData(buf, (uint)Marshal.SizeOf(joyData));
            var jd= FromBytes(buf);
            pbX.Value = jd.X;
            pbY.Value = jd.Y;
            pbZ.Value = jd.Z;
            pbrX.Value = jd.rX;
            pbrY.Value = jd.rY;
            pbrZ.Value = jd.rZ;
            pbSlider.Value = jd.slider;
            pbWheel.Value = jd.wheel;
            pbDial.Value = jd.dial;
            lbHatData.Content = jd.hat.ToString();
            //let's convert the button bit array to two strings for display
            string s;
            s = Convert.ToString(jd.btn0, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn1, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn2, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn3, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn4, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn5, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn6, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn7, 2).PadLeft(8, '0');
            lbButtonsData1.Content = s;
            s = Convert.ToString(jd.btn8, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn9, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn10, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn11, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn12, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn13, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn14, 2).PadLeft(8, '0');
            s = s + " " + Convert.ToString(jd.btn15, 2).PadLeft(8, '0');
            lbButtonsData2.Content = s;
        }

        //for converting a struct to byte array
        public byte[] GetBytesSfj(SetFeatureJoy sfj, int size)
        {
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(sfj, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        //for converting a struct to byte array
        public byte[] GetBytesGdj(GetDataJoy gdj, int size)
        {
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(gdj, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        //for converting a byte array to struct
        private GetDataJoy FromBytes(byte[] arr)
        {
            var str = new GetDataJoy();
            var size = Marshal.SizeOf(str);
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(arr, 0, ptr, size);
            str = (GetDataJoy)Marshal.PtrToStructure(ptr, str.GetType());
            Marshal.FreeHGlobal(ptr);
            return str;
        }

    }
}


