using System.Collections.Generic;

namespace KMC.Engine.Models
{
    public sealed class CapabilityModel
    {
        private readonly HashSet<string> _capabilities = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        public IReadOnlyCollection<string> Items => _capabilities;

        public void Add(string capability)
        {
            if (!string.IsNullOrWhiteSpace(capability))
            {
                _capabilities.Add(capability.Trim());
            }
        }

        public bool Contains(string capability)
        {
            return !string.IsNullOrWhiteSpace(capability) && _capabilities.Contains(capability.Trim());
        }
    }
}
