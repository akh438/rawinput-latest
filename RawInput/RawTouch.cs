﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RawInput_dll
{
    public sealed class RawTouch
    {
        private readonly Dictionary<IntPtr, KeyPressEvent> _deviceList = new Dictionary<IntPtr, KeyPressEvent>();
        public delegate void DeviceEventHandler(object sender, RawInputEventArg e);
        public event DeviceEventHandler TouchActivated;

        readonly object _padLock = new object();

        private TouchDevice touchDevice;

        public int PrevX { get; set; }

        static InputData _rawBuffer;

        // size of GESTURECONFIG structure
        private int _gestureConfigSize;
        // size of GESTUREINFO structure
        private int _gestureInfoSize;


        public RawTouch(IntPtr hwnd, bool captureOnlyInForeground)
        {

            var rid = new RawInputDevice[1];

            rid[0].UsagePage = HidUsagePage.Digitizer; //this.touchDevice.DeviceInfo.UsagePage;
            rid[0].Usage = HidUsage.Joystick;
            rid[0].Flags = (captureOnlyInForeground ? RawInputDeviceFlags.NONE : RawInputDeviceFlags.INPUTSINK) | RawInputDeviceFlags.DEVNOTIFY;
            rid[0].Target = hwnd;

            SetupStructSizes();

            if (!Win32.RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0])))
            {
                throw new ApplicationException("Failed to register raw input device(s).");
            }
        }

        public RawTouch(IntPtr hwnd, bool captureOnlyInForeground, TouchDevice touchDevice) : this(hwnd, captureOnlyInForeground)
        {
            this.touchDevice = touchDevice;
        }

        public int GuestureConfigSize { get { return _gestureConfigSize; } }

        public string Guesture { get; private set; }

        [SecurityPermission(SecurityAction.Demand)]
        private void SetupStructSizes()
        {
            // Both GetGestureCommandInfo and GetTouchInputInfo need to be
            // passed the size of the structure they will be filling
            // we get the sizes upfront so they can be used later.
            _gestureConfigSize = Marshal.SizeOf(new GESTURECONFIG());
            _gestureInfoSize = Marshal.SizeOf(new GESTUREINFO());
        }


        public void ProcessRawInput(IntPtr hdevice)
        {
            //Debug.WriteLine(_rawBuffer.data.keyboard.ToString());
            //Debug.WriteLine(_rawBuffer.data.hid.ToString());
            //Debug.WriteLine(_rawBuffer.header.ToString());


            dynamic Size = 0;

            // Determine Size to be allocated

            dynamic ret = Win32.GetRawInputData(hdevice, DataCommand.RID_INPUT, IntPtr.Zero, Size, Marshal.SizeOf(typeof(Rawinputheader)));
            if (ret == -1)
            {
                Console.WriteLine("error");
                return;
            }

            dynamic SizeToAllocate = Math.Max(Size, Marshal.SizeOf(typeof(RawInput.RAWINPUT_Marshalling)));
            IntPtr pData = Marshal.AllocHGlobal(SizeToAllocate);
            try
            {
                //Populate alocated memory
                ret = GetRawInputData(hRawInput, GetRawInputDataCommand.RID_INPUT, pData, SizeToAllocate, Marshal.SizeOf(typeof(Rawinputheader)));
                if (ret == -1)
                    throw new System.ComponentModel.Win32Exception();
                RAWINPUTHEADER Header = Marshal.PtrToStructure(pData, typeof(API.RawInput.RAWINPUTHEADER));
                //RAWINPUT starts with RAWINPUTHEADER, so we can do this
                switch (Header.dwType)
                {
                    case DeviceTypes.RIM_TYPEHID:
                        //As described on page of RAWHID, RAWHID needs special treatement
                        RAWINPUT_Marshalling raw = Marshal.PtrToStructure(pData, typeof(RAWINPUT_Marshalling));
                        //Get marshalling version, it contains information about block size and count
                        API.RAWINPUT_NonMarshalling raw2 = default(API.RAWINPUT_NonMarshalling);
                        //Do some copying
                        raw2.header = raw.header;
                        raw2.hid.dwCount = raw.hid.dwCount;
                        raw2.hid.dwSizeHid = raw.hid.dwSizeHid;
                        // ERROR: Not supported in C#: ReDimStatement

                        //Allocate array
                        //Populate the array
                        Marshal.Copy(pData.ToInt64 + Marshal.SizeOf(typeof(RAWINPUTHEADER)) + Marshal.SizeOf(typeof(RAWHID_Marshalling)), raw2.hid.bRawData, 0, raw.hid.dwCount * raw.hid.dwSizeHid);
                        return raw2;
                    default:
                        //No additional processing is needed
                        return (RAWINPUT_Marshalling)Marshal.PtrToStructure(pData, typeof(RAWINPUT_Marshalling));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pData);
            }


            var dwSize = 0;
            Win32.GetRawInputData(hdevice, DataCommand.RID_INPUT, IntPtr.Zero, ref dwSize, Marshal.SizeOf(typeof(Rawinputheader)));


            if (dwSize != Win32.GetRawInputData(hdevice, DataCommand.RID_INPUT, out _rawBuffer, ref dwSize, Marshal.SizeOf(typeof(Rawinputheader))))
            {
                Debug.WriteLine("Error getting the rawinput buffer");
                return;
            }

            if (_rawBuffer.header.dwType == (uint) RawInputDeviceType.RIM_TYPEHID)
            {
                List<byte> data = new List<byte>();
                do
                {
                    Rawhid hid = _rawBuffer.data.hid;
                    data.Add(hid.bRawData);

                    dwSize = (int)hid.dwSizHid;

                    Console.WriteLine($"DWSize:{dwSize} dwSizHid:{hid.dwSizHid} dwCount:{hid.dwCount} dwRawData:{hid.bRawData}");

                    if (dwSize != Win32.GetRawInputData(hdevice, DataCommand.RID_INPUT, out _rawBuffer, ref dwSize, Marshal.SizeOf(typeof(Rawinputheader))))
                    {

                    }

                }
                while (dwSize > 0);

                int x =  _rawBuffer.data.mouse.lLastX;
                //Console.WriteLine($"data: dwcount- {_rawBuffer.data.hid.dwCount}, dwsize- {_rawBuffer.data.hid.dwSizHid}, byte {_rawBuffer.data.hid.bRawData.ToString()} ");
                //Console.WriteLine($"{x}");
                if (x < this.PrevX)
                {
                    this.Guesture = "swipe left";
                }
                else
                {
                    this.Guesture = "unknown";
                }

                this.PrevX = x;

                Console.WriteLine($"{this.Guesture} : {x} < {Win32.TouchDevice.Width}");
            }
        }

        public bool DecodeGesture(ref Message m)
        {
            GESTUREINFO gi;

            try
            {
                gi = new GESTUREINFO();
            }
            catch (Exception excep)
            {
                Debug.Print("Could not allocate resources to decode gesture");
                Debug.Print(excep.ToString());

                return false;
            }

            gi.cbSize = _gestureInfoSize;

            // Load the gesture information.
            // We must p/invoke into user32 [winuser.h]
            if (!Win32.GetGestureInfo(m.LParam, ref gi))
            {
                return false;
            }

            switch (gi.dwID)
            {
                case Win32.GID_BEGIN:
                    {
                        Console.WriteLine("touch begin");
                        break;
                    }
                case Win32.GID_END:
                    {
                        Console.WriteLine("touch end");
                        break;
                    }

                case Win32.GID_PAN:

                    switch (gi.dwFlags)
                    {
                        case Win32.GF_BEGIN:

                            Console.WriteLine("PAN BEGIN: " + gi.ToString() + System.Environment.NewLine);
                            break;
                        case Win32.GF_INERTIA:
                            //In this case the ullArguments encodes direction and velocity
                            Console.WriteLine("PAN INERTIA: " + gi.ToString() + System.Environment.NewLine);
                            break;
                        case Win32.GF_END:
                            Console.WriteLine("PAN END: " + gi.ToString() + System.Environment.NewLine);
                            break;
                        case Win32.GF_END | Win32.GF_INERTIA:
                            Console.WriteLine("PAN END: " + gi.ToString() + System.Environment.NewLine);
                            break;
                        default:
                            Console.WriteLine("PAN: " + gi.ToString() + System.Environment.NewLine);
                            break;
                    }
                    break;


            }

            return true;
        }
    }

}


/*

          case Win32.GID_ZOOM:
                    switch (gi.dwFlags)
                    {
                        //case Win32.GF_BEGIN:
                        //    _iArguments = (int)(gi.ullArguments & ULL_ARGUMENTS_BIT_MASK);
                        //    _ptFirst.X = gi.ptsLocation.x;
                        //    _ptFirst.Y = gi.ptsLocation.y;
                        //    _ptFirst = PointToClient(_ptFirst);
                        //    this.textBoxStatus.Clear();
                        //    this.textBoxStatus.AppendText("ZOOM BEGIN: (" + gi.ptsLocation.x + "," + gi.ptsLocation.y + ")" + System.Environment.NewLine);
                        //    break;

                        //default:
                        //    // We read here the second point of the gesture. This
                        //    // is middle point between fingers in this new
                        //    // position.
                        //    _ptSecond.X = gi.ptsLocation.x;
                        //    _ptSecond.Y = gi.ptsLocation.y;
                        //    _ptSecond = PointToClient(_ptSecond);
                        //    this.textBoxStatus.AppendText("ZOOM: (" + gi.ptsLocation.x + "," + gi.ptsLocation.y + ")" + System.Environment.NewLine);

                        //    // We have to calculate zoom center point
                        //    Point ptZoomCenter = new Point((_ptFirst.X + _ptSecond.X) / 2,
                        //                                (_ptFirst.Y + _ptSecond.Y) / 2);

                        //    // The zoom factor is the ratio of the new
                        //    // and the old distance. The new distance
                        //    // between two fingers is stored in
                        //    // gi.ullArguments (lower 4 bytes) and the old
                        //    // distance is stored in _iArguments.
                        //    double k = (double)(gi.ullArguments & ULL_ARGUMENTS_BIT_MASK) /
                        //                (double)(_iArguments);


                        //    // Now we have to store new information as a starting
                        //    // information for the next step in this gesture.
                        //    _ptFirst = _ptSecond;
                        //    _iArguments = (int)(gi.ullArguments & ULL_ARGUMENTS_BIT_MASK);
                        //    break;
                    }
                    break;

                case Win32.GID_ROTATE:
                    //switch (gi.dwFlags)
                    //{
                    //    case GF_BEGIN:
                    //        _iArguments = 0;
                    //        this.textBoxStatus.Clear();
                    //        this.textBoxStatus.AppendText("ROTATE BEGIN: (" + gi.ptsLocation.x + "," + gi.ptsLocation.y + ")" + System.Environment.NewLine);

                    //        break;

                    //    default:
                    //        _ptFirst.X = gi.ptsLocation.x;
                    //        _ptFirst.Y = gi.ptsLocation.y;
                    //        _ptFirst = PointToClient(_ptFirst);
                    //        this.textBoxStatus.AppendText("ROTATE: (" + gi.ptsLocation.x + "," + gi.ptsLocation.y + ")" + System.Environment.NewLine);

                    //        // Gesture handler returns cumulative rotation angle.

                    //        _iArguments = (int)(gi.ullArguments & ULL_ARGUMENTS_BIT_MASK);
                    //        break;
                    //}
                    break;

                case Win32.GID_TWOFINGERTAP:
                    //this.textBoxStatus.Clear();
                    //this.textBoxStatus.AppendText("TWOFINGERTAP: (" + gi.ptsLocation.x + "," + gi.ptsLocation.y + ")" + System.Environment.NewLine);

                    break;

                case Win32.GID_PRESSANDTAP:
                    //if (gi.dwFlags == GF_BEGIN)
                    //{
                    //    this.textBoxStatus.Clear();
                    //}
                    //this.textBoxStatus.AppendText("PRESSANDTAP: (" + gi.ptsLocation.x + "," + gi.ptsLocation.y + ")" + System.Environment.NewLine);

                    break;


    */