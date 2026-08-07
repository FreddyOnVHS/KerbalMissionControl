using System;
using System.Collections.Generic;

namespace KMC.Engine.Electrical
{
    /// <summary>
    /// Smooths live vessel ElectricCharge samples with a rolling linear
    /// regression. Capacity changes reset the window so staging cannot create
    /// a false charge/discharge spike.
    /// </summary>
    internal sealed class ElectricalFlowTracker
    {
        private const double WindowLengthSeconds = 2.0;
        private const double MinimumWindowSeconds = 0.5;
        private const int MinimumSamples = 4;
        private const double StableRateThreshold = 0.005;
        private const double CapacityResetTolerance = 0.01;
        private const double MaximumGapSeconds = 1.0;
        private const double DepletedStoredEcThreshold = 0.001;

        private readonly object _syncRoot =
            new object();

        private readonly List<Sample> _samples =
            new List<Sample>();

        private ElectricalFlowModel _latest =
            new ElectricalFlowModel();

        public void AddSample(
            double storedEc,
            double capacityEc,
            DateTime receivedUtc)
        {
            DateTime utc =
                receivedUtc.Kind == DateTimeKind.Utc
                    ? receivedUtc
                    : receivedUtc.ToUniversalTime();

            lock (_syncRoot)
            {
                if (_samples.Count > 0)
                {
                    Sample previous =
                        _samples[_samples.Count - 1];

                    double gap =
                        (utc - previous.ReceivedUtc)
                            .TotalSeconds;

                    if (gap < 0.0 ||
                        gap > MaximumGapSeconds ||
                        Math.Abs(
                            previous.CapacityEc -
                            capacityEc) >
                        CapacityResetTolerance)
                    {
                        _samples.Clear();
                    }
                }

                _samples.Add(
                    new Sample
                    {
                        ReceivedUtc = utc,
                        StoredEc = Math.Max(0.0, storedEc),
                        CapacityEc = Math.Max(0.0, capacityEc)
                    });

                Prune(utc);

                _latest =
                    BuildModel();
            }
        }

        public ElectricalFlowModel GetLatest()
        {
            lock (_syncRoot)
            {
                return Clone(_latest);
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _samples.Clear();
                _latest = new ElectricalFlowModel();
            }
        }

        private void Prune(
            DateTime newestUtc)
        {
            DateTime cutoff =
                newestUtc.AddSeconds(
                    -WindowLengthSeconds);

            while (_samples.Count > 1 &&
                   _samples[0].ReceivedUtc < cutoff)
            {
                _samples.RemoveAt(0);
            }
        }

        private ElectricalFlowModel BuildModel()
        {
            ElectricalFlowModel model =
                new ElectricalFlowModel();

            if (_samples.Count == 0)
            {
                return model;
            }

            Sample newest =
                _samples[_samples.Count - 1];

            model.TelemetryAvailable = true;
            model.LastSampleUtc = newest.ReceivedUtc;
            model.StoredEc = newest.StoredEc;
            model.CapacityEc = newest.CapacityEc;
            model.SampleCount = _samples.Count;

            if (_samples.Count > 1)
            {
                model.WindowSeconds =
                    (newest.ReceivedUtc -
                     _samples[0].ReceivedUtc)
                        .TotalSeconds;
            }

            /*
             * Depletion is a physical boundary condition, not a zero-flow
             * equilibrium. Once storage reaches zero, delta-EC can no longer
             * reveal continuing vessel demand because EC cannot fall below
             * zero. Mark the state immediately and invalidate storage-rate
             * demand inference.
             */
            if (model.CapacityEc > 0.000001 &&
                model.StoredEc <= DepletedStoredEcThreshold)
            {
                model.State =
                    ElectricalStorageFlowState.Depleted;

                model.HasMeasuredNetStorageRate =
                    false;

                model.NetStorageRateEcPerSecond =
                    0.0;

                model.HasEstimatedSecondsToEmpty =
                    true;

                model.EstimatedSecondsToEmpty =
                    0.0;

                return model;
            }

            if (_samples.Count < MinimumSamples ||
                model.WindowSeconds < MinimumWindowSeconds)
            {
                model.State =
                    ElectricalStorageFlowState.InsufficientData;

                return model;
            }

            double slope =
                CalculateSlope();

            if (Math.Abs(slope) <
                StableRateThreshold)
            {
                slope = 0.0;
            }

            model.HasMeasuredNetStorageRate = true;
            model.NetStorageRateEcPerSecond = slope;

            if (slope > 0.0)
            {
                model.State =
                    ElectricalStorageFlowState.Charging;

                double remaining =
                    Math.Max(
                        0.0,
                        model.CapacityEc -
                        model.StoredEc);

                if (remaining > 0.001)
                {
                    model.HasEstimatedSecondsToFull = true;
                    model.EstimatedSecondsToFull =
                        remaining /
                        slope;
                }
            }
            else if (slope < 0.0)
            {
                model.State =
                    ElectricalStorageFlowState.Discharging;

                if (model.StoredEc > 0.001)
                {
                    model.HasEstimatedSecondsToEmpty = true;
                    model.EstimatedSecondsToEmpty =
                        model.StoredEc /
                        -slope;
                }
            }
            else
            {
                model.State =
                    ElectricalStorageFlowState.Stable;
            }

            return model;
        }

        private double CalculateSlope()
        {
            DateTime origin =
                _samples[0].ReceivedUtc;

            double meanTime = 0.0;
            double meanEc = 0.0;

            for (int i = 0;
                 i < _samples.Count;
                 i++)
            {
                meanTime +=
                    (_samples[i].ReceivedUtc -
                     origin).TotalSeconds;

                meanEc +=
                    _samples[i].StoredEc;
            }

            meanTime /= _samples.Count;
            meanEc /= _samples.Count;

            double numerator = 0.0;
            double denominator = 0.0;

            for (int i = 0;
                 i < _samples.Count;
                 i++)
            {
                double time =
                    (_samples[i].ReceivedUtc -
                     origin).TotalSeconds;

                double dt =
                    time -
                    meanTime;

                numerator +=
                    dt *
                    (_samples[i].StoredEc -
                     meanEc);

                denominator +=
                    dt *
                    dt;
            }

            if (denominator <= 0.0000001)
            {
                return 0.0;
            }

            return
                numerator /
                denominator;
        }

        private static ElectricalFlowModel Clone(
            ElectricalFlowModel source)
        {
            return
                new ElectricalFlowModel
                {
                    TelemetryAvailable = source.TelemetryAvailable,
                    LastSampleUtc = source.LastSampleUtc,
                    StoredEc = source.StoredEc,
                    CapacityEc = source.CapacityEc,
                    HasMeasuredNetStorageRate = source.HasMeasuredNetStorageRate,
                    NetStorageRateEcPerSecond = source.NetStorageRateEcPerSecond,
                    State = source.State,
                    SampleCount = source.SampleCount,
                    WindowSeconds = source.WindowSeconds,
                    HasEstimatedSecondsToEmpty = source.HasEstimatedSecondsToEmpty,
                    EstimatedSecondsToEmpty = source.EstimatedSecondsToEmpty,
                    HasEstimatedSecondsToFull = source.HasEstimatedSecondsToFull,
                    EstimatedSecondsToFull = source.EstimatedSecondsToFull
                };
        }

        private sealed class Sample
        {
            public DateTime ReceivedUtc { get; set; }
            public double StoredEc { get; set; }
            public double CapacityEc { get; set; }
        }
    }
}
