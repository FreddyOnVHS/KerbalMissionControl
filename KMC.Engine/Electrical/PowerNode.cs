using System.Collections.Generic;

namespace KMC.Engine.Electrical
{
    public sealed class PowerNode
    {
        public PowerNode()
        {
            PartName = string.Empty;
            PartTitle = string.Empty;
            Kind = ElectricalNodeKind.Unknown;
            Sources = new List<PowerSource>();
            Storage = new List<PowerStorage>();
            Consumers = new List<PowerConsumer>();
        }

        public uint PartId { get; set; }
        public uint ParentPartId { get; set; }
        public bool HasParent { get; set; }

        public string PartName { get; set; }
        public string PartTitle { get; set; }

        public int ActivationStage { get; set; }
        public int SeparationStage { get; set; }

        public ElectricalNodeKind Kind { get; internal set; }

        public List<PowerSource> Sources { get; private set; }
        public List<PowerStorage> Storage { get; private set; }
        public List<PowerConsumer> Consumers { get; private set; }

        internal void RefreshKind()
        {
            bool source =
                Sources.Count > 0;

            bool storage =
                Storage.Count > 0;

            bool consumer =
                Consumers.Count > 0;

            if (source && storage && consumer)
            {
                Kind =
                    ElectricalNodeKind.Multifunction;
            }
            else if (source && storage)
            {
                Kind =
                    ElectricalNodeKind.SourceAndStorage;
            }
            else if (source && consumer)
            {
                Kind =
                    ElectricalNodeKind.SourceAndConsumer;
            }
            else if (storage && consumer)
            {
                Kind =
                    ElectricalNodeKind.StorageAndConsumer;
            }
            else if (source)
            {
                Kind =
                    ElectricalNodeKind.Source;
            }
            else if (storage)
            {
                Kind =
                    ElectricalNodeKind.Storage;
            }
            else if (consumer)
            {
                Kind =
                    ElectricalNodeKind.Consumer;
            }
            else
            {
                Kind =
                    ElectricalNodeKind.Unknown;
            }
        }
    }
}
