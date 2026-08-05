using System;
using KMC.MissionControl.Debugging;

namespace KMC.MissionControl
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            System.Windows.Forms.Application
                .EnableVisualStyles();

            System.Windows.Forms.Application
                .SetCompatibleTextRenderingDefault(
                    false);

            MainForm mainForm =
                new MainForm();

            using (PropulsionDebuggerHost debugger =
                PropulsionDebuggerHost.Attach(
                    mainForm))
            {
                System.Windows.Forms.Application.Run(
                    mainForm);
            }
        }
    }
}
