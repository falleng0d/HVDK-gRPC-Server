using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Drivers;
using HIDCtrl;
using KeyboardUtils;

namespace App
{

    //create the HIDController object
    public partial class Form1 : Form
    {

        private HidController _hid = new HidController();
        private KbUtils _kUtils = new KbUtils();

        private uint _fTimeout = 5000;  //approx five seconds

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
            _hid.ProductId = (ushort)DriversConst.TtcProductidKeyboard;     //the Tetherscript Virtual Keyboard Driver productid
            _hid.Connect();
            tmrPing.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _hid.Disconnect();
        }

        public void Log(object s, LogArgs e)
        {
            tbLog.AppendText(e.Msg + "\r\n");
        }

        //here we send data to the keyboard driver
        private void Send(Byte modifier, Byte padding, Byte key0, Byte key1, Byte key2, Byte key3, Byte key4, Byte key5)
        {
            var keyboardData = new SetFeatureKeyboard();
            keyboardData.ReportID = 1;
            keyboardData.CommandCode = 2;
            keyboardData.Timeout = _fTimeout / 5; //5 because we count in blocks of 5 in the driver
            keyboardData.Modifier = modifier;
            //padding should always be zero.
            keyboardData.Padding = padding;
            keyboardData.Key0 = key0;
            keyboardData.Key1 = key1;
            keyboardData.Key2 = key2;
            keyboardData.Key3 = key3;
            keyboardData.Key4 = key4;
            keyboardData.Key5 = key5;
            //convert struct to buffer
            var buf = GetBytesSfj(keyboardData, Marshal.SizeOf(keyboardData));
            //send filled buffer to driver
            _hid.SendData(buf, (uint)Marshal.SizeOf(keyboardData));
        }

        //for converting a struct to byte array
        public byte[] GetBytesSfj(SetFeatureKeyboard sfj, int size)
        {
            var arr = new byte[size];
            var ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(sfj, ptr, false);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            return arr;
        }

        private void tmrPing_Tick(object sender, EventArgs e)
        {
            tmrPing.Stop();
            Ping();
            tmrPing.Start();
        }

        //here we send a ping to the keyboard driver
        private void Ping()
        {
            var keyboardData = new SetFeatureKeyboard();
            keyboardData.ReportID = 1;
            keyboardData.CommandCode = 3;
            //the timeout is how long the driver will wait (milliseconds) without receiving a ping before resetting itself
            //we'll be pinging every 200ms, and loss of ping will cause driver reset in FTimeout.  
            //No more stuck keys requiring reboot to clear.
            //the following fields are not used by the driver for a ping, but we'll zero them anyways
            keyboardData.Timeout = _fTimeout / 5; //50 because we count in blocks of 50 in the driver;
            keyboardData.Modifier = 0;
            keyboardData.Padding = 0;
            keyboardData.Key0 = 0;
            keyboardData.Key1 = 0;
            keyboardData.Key2 = 0;
            keyboardData.Key3 = 0;
            keyboardData.Key4 = 0;
            keyboardData.Key5 = 0;
            //convert struct to buffer
            var buf = GetBytesSfj(keyboardData, Marshal.SizeOf(keyboardData));
            //send filled buffer to driver
            _hid.SendData(buf, (uint)Marshal.SizeOf(keyboardData));
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SetTarget();
            //here we resolve the 'a' to be a keycode of 4 by using the keycode list
            var k = _kUtils.GetKeyKeyCode("a");
            if (k > 0)
            {
                Send(0, 0, k, 0, 0, 0, 0, 0);
                //keep the key pressed for this many ms.  If you hold it down for a long time, it will activate the OS key repeat function
                Thread.Sleep(50);
                //we'll release the 'a' key by no longer including it in the pressed key list
                Send(0, 0, 0, 0, 0, 0, 0, 0);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SetTarget();
            //here we resolve the 'a' to be a keycode of 4 by using the keycode list
            var k = _kUtils.GetKeyKeyCode("a");
            if (k > 0)
            {
                Send(0, 0, k, 0, 0, 0, 0, 0);
                //keep the key pressed for this many ms.  If you hold it down for a long time, it will activate the OS key repeat function
                //we'll use a timer here to release the key as the repeat function won't activate when using System.Threading.Thread.Sleep().
                tmrRelease.Start();
            }
        }

        private void tmrRelease_Tick(object sender, EventArgs e)
        {
            tmrRelease.Stop();
            //release all keys
            Send(0, 0, 0, 0, 0, 0, 0, 0);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SetTarget();
            //we'll need to send a CTRL-A simultaneously, then press the Delete key to clear the selected text.
            //for simplicity, we have pre-computed the values needed
            //Send LCTRL
            Send(1, 0, 0, 0, 0, 0, 0, 0);
            Thread.Sleep(50);
            //wait a bit
            Thread.Sleep(50);
            //Send A
            Send(1, 0, 4, 0, 0, 0, 0, 0);
            Thread.Sleep(50);
            Send(0, 0, 0, 0, 0, 0, 0, 0);
            //wait a bit
            Thread.Sleep(50);
            //Send Delete
            Send(0, 0, 76, 0, 0, 0, 0, 0);
            Thread.Sleep(50);
            Send(0, 0, 0, 0, 0, 0, 0, 0);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            SetTarget();
            var k1 = _kUtils.GetKeyKeyCode("a");
            var k2 = _kUtils.GetKeyKeyCode("b");
            if ((k1 > 0) && (k2 > 0))
            {
                //you can press up to six keys (not including modifiers) simultaneously.
                Send(0, 0, k1, k2, 0, 0, 0, 0);
                //keep the key pressed for this many ms.  If you hold it down for a long time, it will activate the OS key repeat function
                Thread.Sleep(50);
                //we'll release the 'a' and 'b' key by no longer including it in the pressed key list
                Send(0, 0, 0, 0, 0, 0, 0, 0);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SendText("Hello", 50, 100);
        }

        private void SendText(string text, int down, int interkey)
        {
            SetTarget();
            //this example sends a simple 'Hello'.  If you want to use other modifers or keys like 'SPACEBAR' or 'TAB' you'll need to tweak this code. 
            //we will put each character in the key0 slot, wait a bit, clear the slot, wait a bit and then do the next letter
            int m;
            byte m1;
            byte k1;
            //iterate through the text.  We'll send one character at a time
            for (var i = 0; i < text.Length; i++)
            {
                var t = text[i];
                //check for uppercase letters
                if (t == char.ToUpper(t))
                {
                    m = _kUtils.GetModifierKeyCode("[LSHIFT]");
                }
                else
                {
                    //no modifier needed
                    m = -1;
                }
                //the modifier is represented by bit positions in a single byte
                //in this example we are allowing only one modifier to be pressed
                //but you could have something like a LALT-LSHIFT-A sequence which means
                //that you would need to set the bit for LALT, then OR that with LSHIFT to give a value of 6
                //in this example, we are only handling [LSHIFT]. 
                //calc the modifier
                switch (m)
                {
                    case 0: m1 = 1; break;
                    case 1: m1 = 2; break;
                    case 2: m1 = 4; break;
                    case 3: m1 = 8; break;
                    case 4: m1 = 16; break;
                    case 5: m1 = 32; break;
                    case 6: m1 = 64; break;
                    case 7: m1 = 128; break;
                    default: m1 = 0; break;
                }
                //the keycode is the same whether it is capitalized or not
                k1 = _kUtils.GetKeyKeyCode(char.ToLower(t).ToString());
                if (k1 > 0)
                {
                    //pressing the key down
                    Send(m1, 0, k1, 0, 0, 0, 0, 0);
                    //keep the key pressed for this many ms.
                    Thread.Sleep(down);
                    //release the key
                    Send(0, 0, 0, 0, 0, 0, 0, 0);
                    //wait before sending the next key
                    Thread.Sleep(interkey);
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SetTarget();
            //press the 'a' key
            Send(0, 0, 4, 0, 0, 0, 0, 0);
            Thread.Sleep(50);
            //whoops we forget to release it, so it will remain pressed.  Let's wait 5000ms and then shut down the app.
            //you'll see the key still repeating even though this app is shut down.
            //shutting down this app will cause the driver to reset in FTimeout ms since the driver is no longer being pinged
            //the driver timeout will save you many headaches if your app doesn't properly close (crashes) while a key is pressed.
            Application.Exit();
        }

        private void SetTarget()
        {
            //we could just dump the text to the editbox on the main form, but they sleep delays will give odd effects and cause key repeats to not look correct since
            //the main thread is sleeping, the ui isn't updated with the keystrokes until the sleep is finished.
            //tbLog.Focus();  
            _kUtils.AppActivate("Tetherscript Virtual Keyboard Driver Reader", 300);
        }

    }

}
