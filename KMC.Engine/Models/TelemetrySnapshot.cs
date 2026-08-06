using System;

namespace KMC.Engine.Models
{
    public sealed class TelemetrySnapshot
    {
        public TelemetrySnapshot(long sequence, DateTime receivedUtc, object packet)
        {
            Sequence = sequence;
            ReceivedUtc = receivedUtc.Kind == DateTimeKind.Utc
                ? receivedUtc
                : receivedUtc.ToUniversalTime();
            Packet = packet;
        }

        public long Sequence { get; }
        public DateTime ReceivedUtc { get; }
        public object Packet { get; }
    }
}
