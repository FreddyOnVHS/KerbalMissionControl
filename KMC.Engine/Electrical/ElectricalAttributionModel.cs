using System.Collections.Generic;

namespace KMC.Engine.Electrical
{
    public enum ElectricalAttributionKind
    {
        Producer = 0,
        Consumer
    }

    public enum ElectricalRateEvidence
    {
        Unknown = 0,
        MeasuredCurrent,
        DeclaredActive,
        DeclaredMaximum
    }

    public sealed class ElectricalAttributionEntry
    {
        public ElectricalAttributionEntry()
        {
            PartTitle =
                string.Empty;

            Category =
                string.Empty;

            Evidence =
                ElectricalRateEvidence.Unknown;
        }

        public ElectricalAttributionKind Kind { get; set; }
        public uint PartId { get; set; }
        public string PartTitle { get; set; }
        public string Category { get; set; }
        public ElectricalRateEvidence Evidence { get; set; }

        public bool CurrentRateKnown { get; set; }
        public double CurrentRateEcPerSecond { get; set; }

        public bool MaximumRateKnown { get; set; }
        public double MaximumRateEcPerSecond { get; set; }

        public bool Enabled { get; set; }
        public bool ActiveStateKnown { get; set; }
        public bool Active { get; set; }
    }

    public sealed class ElectricalAttributionModel
    {
        public ElectricalAttributionModel()
        {
            Entries =
                new List<ElectricalAttributionEntry>();
        }

        public bool TelemetryAvailable { get; set; }

        public List<ElectricalAttributionEntry> Entries
        {
            get;
            private set;
        }

        public int ProducerCount { get; internal set; }
        public int ConsumerCount { get; internal set; }

        public int KnownCurrentProducerCount { get; internal set; }
        public int KnownCurrentConsumerCount { get; internal set; }

        public int UnknownCurrentProducerCount
        {
            get
            {
                return
                    ProducerCount -
                    KnownCurrentProducerCount;
            }
        }

        public int UnknownCurrentConsumerCount
        {
            get
            {
                return
                    ConsumerCount -
                    KnownCurrentConsumerCount;
            }
        }

        public double KnownCurrentGenerationEcPerSecond { get; internal set; }
        public double KnownCurrentConsumptionEcPerSecond { get; internal set; }

        public double KnownCurrentBalanceEcPerSecond
        {
            get
            {
                return
                    KnownCurrentGenerationEcPerSecond -
                    KnownCurrentConsumptionEcPerSecond;
            }
        }

        public double DeclaredMaximumGenerationEcPerSecond { get; internal set; }
        public double DeclaredMaximumConsumptionEcPerSecond { get; internal set; }

        public void Recalculate()
        {
            ProducerCount =
                0;

            ConsumerCount =
                0;

            KnownCurrentProducerCount =
                0;

            KnownCurrentConsumerCount =
                0;

            KnownCurrentGenerationEcPerSecond =
                0.0;

            KnownCurrentConsumptionEcPerSecond =
                0.0;

            DeclaredMaximumGenerationEcPerSecond =
                0.0;

            DeclaredMaximumConsumptionEcPerSecond =
                0.0;

            for (int index = 0;
                 index < Entries.Count;
                 index++)
            {
                ElectricalAttributionEntry entry =
                    Entries[index];

                if (entry == null)
                {
                    continue;
                }

                if (entry.Kind ==
                    ElectricalAttributionKind.Producer)
                {
                    ProducerCount++;

                    if (entry.CurrentRateKnown)
                    {
                        KnownCurrentProducerCount++;

                        KnownCurrentGenerationEcPerSecond +=
                            entry.CurrentRateEcPerSecond;
                    }

                    if (entry.MaximumRateKnown)
                    {
                        DeclaredMaximumGenerationEcPerSecond +=
                            entry.MaximumRateEcPerSecond;
                    }
                }
                else
                {
                    ConsumerCount++;

                    if (entry.CurrentRateKnown)
                    {
                        KnownCurrentConsumerCount++;

                        KnownCurrentConsumptionEcPerSecond +=
                            entry.CurrentRateEcPerSecond;
                    }

                    if (entry.MaximumRateKnown)
                    {
                        DeclaredMaximumConsumptionEcPerSecond +=
                            entry.MaximumRateEcPerSecond;
                    }
                }
            }
        }
    }
}
