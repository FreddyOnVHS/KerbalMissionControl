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

        public List<string> Diagnostics
        {
            get;
            private set;
        }
    }
}
