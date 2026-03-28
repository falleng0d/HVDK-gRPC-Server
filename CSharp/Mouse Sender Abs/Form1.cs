using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Drivers;
using HIDCtrl;

namespace App
{

    //create the HIDController object
    public partial class Form1 : Form
    {

        private HidController _hid = new HidController();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            tbLog.AppendText("\r\n");
            //create the HIDController 
            _hid.OnLog += new EventHandler<LogArgs>(Log);
            _hid.VendorId = (ushort)DriversConst.TtcVendorid;                //the Tetherscript vendorid
            _hid.ProductId = (ushort)DriversConst.TtcProductidMouseabs;     //the Tetherscript Virtual Mouse Absolute Driver productid
            _hid.Connect();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _hid.Disconnect();
        }

        public void Log(object s, LogArgs e)
        {
            tbLog.AppendText(e.Msg + "\r\n");
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            tbLog.AppendText("Sending mouse abs coords and button states...\r\n");
            Send_Data_To_MouseAbs();
            tmrRelease.Enabled = true; //this will release the mouse buttons if they are pressed
        }

        //here we send data to the mouse
        private void Send_Data_To_MouseAbs()
        {
            var mouseAbsData = new SetFeatureMouseAbs();
            mouseAbsData.ReportID = 1;
            mouseAbsData.CommandCode = 2;
            byte btns = 0;
            if (cbLeft.Checked) { btns = 1; }
            ;
            if (cbRight.Checked) { btns = (byte)(btns | (1 << 1)); }
            if (cbLeft.Checked) { btns = (byte)(btns | (1 << 2)); }
            mouseAbsData.Buttons = btns;  //button states are represented by the 3 least significant bits
            mouseAbsData.X = (UInt16)spnX.Value;
            mouseAbsData.Y = (UInt16)spnY.Value;
            //convert struct to buffer
            var buf = GetBytesSfj(mouseAbsData, Marshal.SizeOf(mouseAbsData));
            //send filled buffer to driver
            _hid.SendData(buf, (uint)Marshal.SizeOf(mouseAbsData));
        }

        //for converting a struct to byte array
        public byte[] GetBytesSfj(SetFeatureMouseAbs sfj, int size)
        {
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(sfj, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        private void tmrRelease_Tick(object sender, EventArgs e)
        {
            tmrRelease.Enabled = false;
            tbLog.AppendText("Releasing buttons...\r\n");
            cbLeft.Checked = false;
            cbRight.Checked = false;
            cbMiddle.Checked = false;
            Send_Data_To_MouseAbs();
        }
    }
}
