using System;
using System.Threading;
using System.Windows.Forms;

namespace SystemTrayApp
{
    internal static class Program
    {
        private static Mutex? _singleInstanceMutex;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Ensure only a single instance runs at a time. Two instances would
            // fight over the gamma state and spawn duplicate dispwin.exe processes.
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, @"Local\HDRGammaFix", out createdNew);
            if (!createdNew)
            {
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                MessageBox.Show("HDR Gamma Fix is already running.",
                                "HDR Gamma Fix",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Prevent application from closing when no form is shown
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            
            try
            {
                // Run the application with our custom context
                Application.Run(new TrayApplicationContext());
            }
            finally
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
                _singleInstanceMutex = null;
            }
        }
    }
}
