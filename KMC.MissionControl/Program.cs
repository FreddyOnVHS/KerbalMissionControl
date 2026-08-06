using System;
using KMC.MissionControl.Debugging;
using KMC.MissionControl.Debugging.Capabilities;
using KMC.MissionControl.Debugging.Electrical;

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
                .SetCompatibleTextRenderingDefault(false);

            MainForm mainForm = new MainForm();

            using (
                PropulsionDebuggerHost propulsionDebugger =
                    PropulsionDebuggerHost.Attach(mainForm))
            using (
                ElectricalTopologyDebuggerHost electricalDebugger =
                    ElectricalTopologyDebuggerHost.Attach(mainForm))
            using (
                CapabilityDebuggerHost capabilityDebugger =
                    CapabilityDebuggerHost.Attach(mainForm))
            using (
                SolidFuelTelemetryReceiver solidFuel =
                    new SolidFuelTelemetryReceiver())
            using (
                EngineStateTelemetryReceiver engineStates =
                    new EngineStateTelemetryReceiver())
            {
                solidFuel.Start();
                engineStates.Start();

                System.Windows.Forms.Application.Run(mainForm);
            }
        }
    }
}
