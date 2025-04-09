using System;
using System.Windows.Forms;

namespace SystemTrayApp
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // Prevent application from closing when no form is shown
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            
            // Run the application with our custom context
            Application.Run(new TrayApplicationContext());
        }
    }
}