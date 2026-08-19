using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KMC.Engine.Analysis;
using KMC.MissionControl.Engineering;
using KMC.Shared;

namespace KMC.MissionControl.Training
{
    /// <summary>
    /// Build 14.18.2 explicit instructor IVA-test sender.
    /// This path does not inject failure truth and does not alter telemetry.
    /// </summary>
    public static class InstructorIvaAnnunciatorTestBridge
    {
        public static bool Send(
            IvaAnnunciatorTestId testId,
            IvaAnnunciatorTestOperation operation,
            out string resultText)
        {
            resultText = string.Empty;

            AnalysisPipelineResult latest;
            if (!EngineeringSnapshotStore.TryGetLatest(out latest) ||
                latest == null ||
                latest.Snapshot == null ||
                latest.Snapshot.Vessel == null ||
                string.IsNullOrWhiteSpace(latest.Snapshot.Vessel.VesselId))
            {
                resultText = "NO ACTIVE ENGINEERING VESSEL";
                return false;
            }

            IvaAnnunciatorTestPacket packet =
                new IvaAnnunciatorTestPacket
                {
                    VesselId = latest.Snapshot.Vessel.VesselId,
                    CommandId = Guid.NewGuid().ToString("N"),
                    TestId = testId,
                    Operation = operation
                };

            byte[] bytes = Encoding.UTF8.GetBytes(packet.Serialize());

            try
            {
                using (UdpClient udp = new UdpClient())
                {
                    IPEndPoint target =
                        new IPEndPoint(
                            IPAddress.Loopback,
                            IvaAnnunciatorTestPacket.CommandPort);

                    // Explicit ON/OFF/CLEAR commands are idempotent.
                    for (int index = 0; index < 3; index++)
                        udp.Send(bytes, bytes.Length, target);
                }
            }
            catch (Exception ex)
            {
                resultText =
                    "IVA TEST SEND FAILED / " +
                    ex.GetType().Name +
                    " / " +
                    ex.Message;
                return false;
            }

            resultText =
                "COMMAND SENT / " +
                operation.ToString().ToUpperInvariant() +
                " / " +
                (operation == IvaAnnunciatorTestOperation.ClearAll
                    ? "ALL"
                    : testId.ToString().ToUpperInvariant()) +
                " / VESSEL " +
                packet.VesselId;

            return true;
        }
    }
}
