using System.Collections.Generic;

namespace KMC.MissionControl.Rendering.Propulsion
{
    public sealed class PropulsionLayout
    {
        public PropulsionLayout()
        {
            Nodes =
                new Dictionary<uint, PropulsionLayoutNode>();
        }

        public Dictionary<uint, PropulsionLayoutNode>
            Nodes { get; private set; }
    }
}
