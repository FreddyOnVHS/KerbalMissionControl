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
            ElectricalFlowModel electricalFlow,
            ElectricalAttributionModel electricalAttribution)
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

            ElectricalAttribution =
                electricalAttribution ??
                new ElectricalAttributionModel();
        }

        public long Sequence { get; private set; }
        public DateTime ReceivedUtc { get; private set; }
        public object Packet { get; private set; }
        public ElectricalFlowModel ElectricalFlow { get; private set; }
        public ElectricalAttributionModel ElectricalAttribution { get; private set; }
    }
}
