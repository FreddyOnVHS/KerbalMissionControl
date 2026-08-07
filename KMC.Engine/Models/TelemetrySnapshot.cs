using System;
using KMC.Engine.Electrical;

namespace KMC.Engine.Models
{
    public sealed class TelemetrySnapshot
    {
        public TelemetrySnapshot(
            long sequence,
            DateTime receivedUtc,
            object packet,
            ElectricalFlowModel electricalFlow)
        {
            Sequence =
                sequence;

            ReceivedUtc =
                receivedUtc.Kind == DateTimeKind.Utc
                    ? receivedUtc
                    : receivedUtc.ToUniversalTime();

            Packet =
                packet;

            ElectricalFlow =
                electricalFlow ??
                new ElectricalFlowModel();
        }

        public long Sequence { get; private set; }
        public DateTime ReceivedUtc { get; private set; }
        public object Packet { get; private set; }
        public ElectricalFlowModel ElectricalFlow { get; private set; }
    }
}
