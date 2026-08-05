using System;

namespace KMC.MissionControl.Telemetry
{
    public static class SolidFuelTelemetryStore
    {
        private static readonly object SyncRoot =
            new object();

        private static SolidFuelTelemetrySnapshot _latest =
            new SolidFuelTelemetrySnapshot();

        public static void Publish(
            SolidFuelTelemetrySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _latest =
                    snapshot;
            }
        }

        public static SolidFuelTelemetrySnapshot GetSnapshot()
        {
            lock (SyncRoot)
            {
                return new SolidFuelTelemetrySnapshot
                {
                    TimestampUtc =
                        _latest.TimestampUtc,

                    TotalAmount =
                        _latest.TotalAmount,

                    TotalCapacity =
                        _latest.TotalCapacity,

                    ActiveAmount =
                        _latest.ActiveAmount,

                    ActiveCapacity =
                        _latest.ActiveCapacity,

                    BoosterCount =
                        _latest.BoosterCount,

                    BurningBoosterCount =
                        _latest.BurningBoosterCount
                };
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest =
                    new SolidFuelTelemetrySnapshot();
            }
        }
    }
}
