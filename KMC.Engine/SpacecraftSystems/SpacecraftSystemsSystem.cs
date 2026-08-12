using System;
using KMC.Engine.Models;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// Owns the complete Engine-side synthetic spacecraft model.
    ///
    /// 14.0 supplies the generic component/dependency foundation.
    /// 14.1 overlays electrical distribution and reapplies dependency state.
    /// </summary>
    public sealed class SpacecraftSystemsSystem
    {
        private readonly object _syncRoot;
        private readonly SpacecraftSystemsFoundationSystem _foundation;
        private readonly SyntheticElectricalDistributionSystem _electrical;
        private SpacecraftSystemsModel _latest;

        public SpacecraftSystemsSystem()
        {
            _syncRoot =
                new object();

            _foundation =
                new SpacecraftSystemsFoundationSystem();

            _electrical =
                new SyntheticElectricalDistributionSystem();

            _latest =
                new SpacecraftSystemsModel();
        }

        public void Update(
            VesselModel vessel,
            DateTime generatedUtc)
        {
            _foundation.Update(
                vessel,
                generatedUtc);

            SpacecraftSystemsModel model =
                _foundation.GetLatest();

            if (vessel != null)
            {
                model.ElectricalDistribution =
                    _electrical.BuildAndApply(
                        model,
                        generatedUtc);
            }

            lock (_syncRoot)
            {
                _latest =
                    model;
            }
        }

        public SpacecraftSystemsModel GetLatest()
        {
            lock (_syncRoot)
            {
                return
                    _latest != null
                        ? _latest.Clone()
                        : new SpacecraftSystemsModel();
            }
        }
    }
}
