using System.Collections.Generic;
using KMC.Engine.Electrical;

namespace KMC.Engine.Models
{
    public sealed class PowerModel
    {
        public PowerModel()
        {
            ElectricalNetwork =
                new ElectricalNetwork();

            Flow =
                new ElectricalFlowModel();

            Attribution =
                new ElectricalAttributionModel();

            Load =
                new ElectricalLoadModel();

            Diagnostic =
                new ElectricalPowerDiagnosticModel();

            LoadShedding =
                new ElectricalLoadSheddingModel();

            Diagnostics =
                new List<string>();
        }

        public ElectricalNetwork ElectricalNetwork
        {
            get;
            internal set;
        }

        public ElectricalFlowModel Flow
        {
            get;
            internal set;
        }

        public ElectricalAttributionModel Attribution
        {
            get;
            internal set;
        }

        public ElectricalLoadModel Load
        {
            get;
            internal set;
        }

        public ElectricalPowerDiagnosticModel Diagnostic
        {
            get;
            internal set;
        }

        public ElectricalLoadSheddingModel LoadShedding
        {
            get;
            internal set;
        }

        public List<string> Diagnostics
        {
            get;
            private set;
        }
    }
}
