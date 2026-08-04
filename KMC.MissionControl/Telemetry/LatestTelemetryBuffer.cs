using KMC.Shared;
using System;

namespace KMC.MissionControl.Telemetry
{
    /// <summary>
    /// Thread-safe, single-slot telemetry buffer.
    ///
    /// The KSP receiver may publish packets faster than the WinForms UI
    /// should redraw. This buffer always retains the newest packet and lets
    /// the display timer skip obsolete intermediate packets without building
    /// an unbounded UI callback queue.
    /// </summary>
    public sealed class LatestTelemetryBuffer
    {
        private readonly object _syncRoot =
            new object();

        private TelemetryPacket _latestPacket;
        private long _latestSequence;
        private long _lastReadSequence;
        private long _receivedPacketCount;
        private long _supersededPacketCount;
        private DateTime _lastReceivedUtc;

        /// <summary>
        /// Stores the newest telemetry packet.
        /// This method is safe to call from the receiver thread.
        /// </summary>
        public void Publish(
            TelemetryPacket packet)
        {
            if (packet == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                /*
                 * If the previously published sequence has not yet been
                 * consumed, replacing it means one display update has been
                 * intentionally superseded by newer telemetry.
                 */
                if (_latestPacket != null &&
                    _latestSequence >
                    _lastReadSequence)
                {
                    _supersededPacketCount++;
                }

                _latestPacket =
                    packet;

                _latestSequence++;
                _receivedPacketCount++;

                _lastReceivedUtc =
                    DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Returns the newest packet only when it is newer than the sequence
        /// already consumed by the caller.
        /// </summary>
        public bool TryReadLatest(
            ref long lastConsumedSequence,
            out TelemetryPacket packet)
        {
            lock (_syncRoot)
            {
                if (_latestPacket == null ||
                    _latestSequence <=
                    lastConsumedSequence)
                {
                    packet = null;
                    return false;
                }

                packet =
                    _latestPacket;

                lastConsumedSequence =
                    _latestSequence;

                _lastReadSequence =
                    Math.Max(
                        _lastReadSequence,
                        _latestSequence);

                return true;
            }
        }

        public long ReceivedPacketCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _receivedPacketCount;
                }
            }
        }

        public long SupersededPacketCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _supersededPacketCount;
                }
            }
        }

        public DateTime LastReceivedUtc
        {
            get
            {
                lock (_syncRoot)
                {
                    return _lastReceivedUtc;
                }
            }
        }
    }
}
