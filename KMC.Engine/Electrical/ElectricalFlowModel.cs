using System;

namespace KMC.Engine.Electrical
{
    public enum ElectricalStorageFlowState
    {
        Unavailable = 0,
        InsufficientData,
        Stable,
        Charging,
        Discharging,
        Depleted
    }

    /// <summary>
    /// Measured vessel-wide ElectricCharge storage behavior.
    ///
    /// NetStorageRateEcPerSecond is the measured rate of change of stored EC.
    /// It is not claimed to be total generation minus total consumption while
    /// storage is clamped at empty/full capacity.
    ///
    /// When storage is depleted, delta-EC can no longer observe vessel demand.
    /// Depleted is therefore an explicit engineering state rather than Stable.
    /// </summary>
    public sealed class ElectricalFlowModel
    {
        public ElectricalFlowModel()
        {
            State =
                ElectricalStorageFlowState.Unavailable;
        }

        public bool TelemetryAvailable { get; internal set; }
        public DateTime LastSampleUtc { get; internal set; }
        public double StoredEc { get; internal set; }
        public double CapacityEc { get; internal set; }

        public double ChargePercent
        {
            get
            {
                if (CapacityEc <= 0.0)
                {
                    return 0.0;
                }

                double value =
                    StoredEc /
                    CapacityEc *
                    100.0;

                if (value < 0.0)
                {
                    return 0.0;
                }

                if (value > 100.0)
                {
                    return 100.0;
                }

                return value;
            }
        }

        public bool HasMeasuredNetStorageRate { get; internal set; }
        public double NetStorageRateEcPerSecond { get; internal set; }
        public ElectricalStorageFlowState State { get; internal set; }
        public int SampleCount { get; internal set; }
        public double WindowSeconds { get; internal set; }

        public bool IsAtCapacity
        {
            get
            {
                return
                    CapacityEc > 0.000001 &&
                    StoredEc >= CapacityEc - 0.001;
            }
        }

        public bool IsDepleted
        {
            get
            {
                return
                    CapacityEc > 0.000001 &&
                    StoredEc <= 0.001;
            }
        }

        public bool HasEstimatedSecondsToEmpty { get; internal set; }
        public double EstimatedSecondsToEmpty { get; internal set; }

        public bool HasEstimatedSecondsToFull { get; internal set; }
        public double EstimatedSecondsToFull { get; internal set; }
    }
}
