﻿using System;
using RawInput_dll;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Forms;


namespace Keyboard
{
    public partial class Keyboard : Form
    {
        private readonly RawInput _rawinput;

        const bool CaptureOnlyInForeground = false;
        // Todo: add checkbox to form when checked/uncheck create method to call that does the same as Keyboard ctor

        public Keyboard()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            _rawinput = new RawInput(Handle, CaptureOnlyInForeground);

            Win32.DeviceAudit();            // Writes a file DeviceAudit.txt to the current directory


            _rawinput.TouchActivated += OnKeyPressed;
        }

        private void OnKeyPressed(object sender, RawInputEventArg e)
        {
            lblCoords.Text = "X:" + e.X + " Y:" + e.Y;
            //switch (e.KeyPressEvent.Message)
            //{
            //    case Win32.WM_KEYDOWN:
            //        Debug.WriteLine(e.KeyPressEvent.KeyPressState);
            //        break;
            //     case Win32.WM_KEYUP:
            //        Debug.WriteLine(e.KeyPressEvent.KeyPressState);
            //        break;
            //}
        }

        private void Keyboard_FormClosing(object sender, FormClosingEventArgs e)
        {
            _rawinput.TouchActivated -= OnKeyPressed;
        }

        private static void CurrentDomain_UnhandledException(Object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;

            if (null == ex) return;

            // Log this error. Logging the exception doesn't correct the problem but at least now
            // you may have more insight as to why the exception is being thrown.
            Debug.WriteLine("Unhandled Exception: " + ex.Message);
            Debug.WriteLine("Unhandled Exception: " + ex);
            MessageBox.Show(ex.Message);
        }


    }
}
