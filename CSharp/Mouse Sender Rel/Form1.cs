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
            _hid.ProductId = (ushort)DriversConst.TtcProductidMouserel;     //the Tetherscript Virtual Mouse Absolute Driver productid
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
            tbLog.AppendText("Sending mouse rel movement and button states...\r\n");
            Send_Data_To_MouseRel(false);
            tmrRelease.Enabled = true; //this will release the mouse buttons if they are pressed
        }

        //here we send data to the mouse
        private void Send_Data_To_MouseRel(bool ignoreMove)
        {
            var mouseRelData = new SetFeatureMouseRel();
            mouseRelData.ReportID = 1;
            mouseRelData.CommandCode = 2;
            byte btns = 0;
            if (cbLeft.Checked) { btns = 1; };
            if (cbRight.Checked) { btns = (byte)(btns | (1 << 1)); }
            if (cbLeft.Checked) { btns = (byte)(btns | (1 << 2)); }
            mouseRelData.Buttons = btns;  //button states are represented by the 3 least significant bits
            if (!ignoreMove)
            {
                mouseRelData.X = (sbyte)spnX.Value;
                mouseRelData.Y = (sbyte)spnY.Value;
            }
            //convert struct to buffer
            var buf = GetBytesSfj(mouseRelData, Marshal.SizeOf(mouseRelData));
            //send filled buffer to driver
            _hid.SendData(buf, (uint)Marshal.SizeOf(mouseRelData));
        }

        //for converting a struct to byte array
        public byte[] GetBytesSfj(SetFeatureMouseRel sfj, int size)
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
            Send_Data_To_MouseRel(true); //in this example, when we release the buttons we don't to move
        }
    }
}
