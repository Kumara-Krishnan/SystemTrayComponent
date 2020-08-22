using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SystemTrayComponent.Util;

namespace SystemTrayComponent
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Mutex mutex = null;
            if (!Mutex.TryOpenExisting(Constants.SystemTrayMutex, out mutex))
            {
                mutex = new Mutex(false, Constants.SystemTrayMutex);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SystemTrayApplicationContext());
                mutex.Close();
            }
        }
    }
}
