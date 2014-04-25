// a background thread class enabled to do direct calls to GUI thread
// this variant uses WM_COPY DATA to exchange data
using System;
using System.Text;
using System.Windows.Forms;
using Microsoft.WindowsCE.Forms;
using System.ComponentModel;
using System.Threading;
using System.Runtime.InteropServices;

    public class bgThread2 : Component
    {
        /// <summary>
        /// the bgThread2EventHandler is the protoype of the eventhandler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="bte = the EventArgs supplied later with the event"></param>
        public delegate void bgThread2EventHandler(object sender, BgThreadEventArgs bte);
        /// <summary>
        /// this event function is the 'link' back to the subscriber of the event handler
        /// and will be used to fire the event in the other thread
        /// </summary>
        public event bgThread2EventHandler bgThread2Event;
        private Container components;
        /// <summary>
        /// we will run a background thread. this is the thread object we use
        /// </summary>
        private Thread myThread;
        /// <summary>
        /// a var to let the thread stop
        /// in reality the thread will be mostly aborted by using thread.Abort()
        /// </summary>
        private bool bRunThread;
        /// <summary>
        /// we will store the derived window handle for communication
        /// </summary>
        internal bgThread2WndProc bgWnd;
        private string _sCOM = "COM4";
        /// <summary>
        /// we use a user window message ID, not any of the predefined WM_ constants
        /// <summary>
        internal const int msgID = 1025;
        internal const int WM_COPYDATA = 0x004A;
        #region WM_COPYDATA_code
        internal struct COPYDATASTRUCT
        {
            public int dwData;
            public int cbData;      //size of following string
            public IntPtr lpData;   //pointer to string
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        internal struct UserData
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
            public string msg;
        }
        #endregion
        #region Nested Class

        /// This is the message window we use to communicate back to the thread
        /// where the bgThread2 object is created
        /// so we avoid cross-threading updates problems especially with the GUI thread
        /// </summary>
        internal class bgThread2WndProc : MessageWindow
        {
            #region Fields
            private bgThread2 _bgThread2;
            public IntPtr hwndControl;
            #endregion
            #region Constructors
            public bgThread2WndProc(bgThread2 Parent)
            {
                System.Diagnostics.Debug.WriteLine("Creating ThreadWnd...");
                this._bgThread2 = Parent;
                hwndControl = Hwnd;
                System.Diagnostics.Debug.WriteLine("bgThread2WndProc hWnd is 0x: "+hwndControl.ToInt32().ToString("x"));
            }
            #endregion
            #region Methods
            protected override void WndProc(ref Message m)
            {
                int iMsg = m.Msg;
                System.Diagnostics.Debug.WriteLine("WndProc called...");
                switch (iMsg)
                {
                    case WM_COPYDATA:
                        System.Diagnostics.Debug.WriteLine("WndProc sending notification ...");
                        // extract the own data from the Windows Message m
                        COPYDATASTRUCT data = (COPYDATASTRUCT)Marshal.PtrToStructure
                            (m.LParam, typeof(COPYDATASTRUCT));
                        UserData userData =
                            (UserData)Marshal.PtrToStructure(data.lpData, typeof(UserData));
                        this._bgThread2.NotifyData(data.dwData, userData.msg);
                        m.Result = IntPtr.Zero;
                        break;
                    default:
                        base.WndProc(ref m);
                        break;
                }
            }
            #endregion //Methods

        }//MsgWnd
        #endregion
        #region theClass
        public bgThread2()
        {
            bgWnd = new bgThread2WndProc(this);
            myThread = new Thread(myThreadStart);
            bRunThread = true;
            myThread.Start();
        }
        //for the ping here is a second constuctor to enable you to specify the IP
        public bgThread2(string sCOM)
        {
            bgWnd = new bgThread2WndProc(this);
            myThread = new Thread(myThreadStart);
            bRunThread = true;
            _sCOM = sCOM;
            myThread.Start();
        }
        #region TheTHREAD
        private void myThreadStart()
        {
            System.Diagnostics.Debug.WriteLine("Entering thread proc");
            int _i=0;
            try
            {
                do
                {
                    //The blocking function...
                    char[] cSep = new char[] { '\r', '\n' };
                    int iRes = 0;
                    do
                    {
                        string sRes = "";
                        iRes = GPS_Sample8.ReadFileClass.readFile(_sCOM, ref sRes);
                        string[] sAll = sRes.Split(cSep);
                        foreach (string s1 in sAll)
                        {
                            SendData(bgWnd.Hwnd, iRes, s1); 
                        }
                        //Application.DoEvents();
                        Thread.Sleep(100);
                    } while (iRes == 0);

                    System.Diagnostics.Debug.WriteLine("Thread sleeps...");
                    _i++;
                    Thread.Sleep(100);
                } while (bRunThread);
            }
            catch (ThreadAbortException)
            {
                System.Diagnostics.Debug.WriteLine("Thread will abort");
                bRunThread = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception in ThreadStart: " + ex.Message);
            }
            System.Diagnostics.Debug.WriteLine("ThreadProc ended");
        }
        #endregion
        private void SendData(IntPtr hWnd, int id, string sMsg)
        {
            COPYDATASTRUCT data = new COPYDATASTRUCT();

            data.dwData = id;// 3; // whatever value (have to match receiver end)
            data.cbData = 512;// size of UserData struct (I have two 200 bytes long ANSI array)

            UserData userData = new UserData();
            if (sMsg.Length > 500)
                userData.msg = sMsg.Substring(0, 500);
            else
                userData.msg = sMsg;

            //copy data to unmanaged pointers
            data.lpData = Marshal.AllocCoTaskMem(data.cbData);
            Marshal.StructureToPtr( userData, data.lpData, false);

            IntPtr lpData = Marshal.AllocCoTaskMem(Marshal.SizeOf(data));
            Marshal.StructureToPtr(data, lpData, false);

            Message msg1 = Message.Create(hWnd, WM_COPYDATA, lpData, lpData);
            MessageWindow.SendMessage(ref msg1);

            Marshal.FreeCoTaskMem(data.lpData);
            Marshal.FreeCoTaskMem(lpData);
        }
	    protected override void Dispose (bool disposing)
		{
			if (disposing)
			{
				if (this.components != null)
				{
					this.components.Dispose ();
				}
			}
            this.myThread.Abort();
			base.Dispose (disposing);
		}
        ~bgThread2()
		{
            myThread.Abort();
			this.Dispose (false);
        }
        #region theNotifyHandlers

        private void NotifyData(int i1, string s)
        {
            BgThreadEventArgs _bgThread2EventArgs;
            //is there any subscriber
            if (this.bgThread2Event == null)
            {
                return;
            }
            try
            {
                _bgThread2EventArgs = new BgThreadEventArgs(i1, s);
                this.bgThread2Event(this, _bgThread2EventArgs);
            }
            catch (MissingMethodException)
            {
            }
        }

        #endregion
        //==============================================================================
        #region EventArgs
        /// <summary>
        /// the GUI eventhandler will get an instance of this EventArgs class
        /// the instance is generated inside the bgThread2 class
        /// </summary>
        public class BgThreadEventArgs : EventArgs
        {
            #region Fields
                public int iStatus;
                public string sString;
            #endregion
            #region Constructors
            public BgThreadEventArgs(int i1, string s)
            {
                this.iStatus = i1;
                this.sString = s;
            }
            #endregion
        }
        #endregion
    }
    #endregion
