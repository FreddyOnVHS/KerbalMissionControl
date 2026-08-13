﻿using System;
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
            {
                solidFuel.Start();

                /*
                 * Build 8.13.1:
                 *
                 * Do NOT start EngineStateTelemetryReceiver here.
                 *
                 * UDP 5058 / KMC-ENGINE1 is now owned exclusively by
                 * TelemetryTransport through MissionControlReceiver.
                 *
                 * Starting the legacy receiver here would bind 5058 first
                 * and cause TelemetryTransport to fail with:
                 *
                 * SocketError=AddressAlreadyInUse (10048)
                 */
                System.Windows.Forms.Application.Run(
                    mainForm);
            }
        }
    }
}
