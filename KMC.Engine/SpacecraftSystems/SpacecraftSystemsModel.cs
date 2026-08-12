using System;
using System.Collections.Generic;

namespace KMC.Engine.SpacecraftSystems
{
    public sealed class SpacecraftSystemDependency
    {
        public SpacecraftSystemDependency()
        {
            SourceId = string.Empty;
            TargetId = string.Empty;
            Kind = SpacecraftDependencyKind.Functional;
            Required = true;
        }

        public string SourceId { get; set; }
        public string TargetId { get; set; }
        public SpacecraftDependencyKind Kind { get; set; }
        public bool Required { get; set; }

        internal SpacecraftSystemDependency Clone()
        {
            return
                new SpacecraftSystemDependency
                {
                    SourceId = SourceId ?? string.Empty,
                    TargetId = TargetId ?? string.Empty,
                    Kind = Kind,
                    Required = Required
                };
        }
    }

    public sealed class SpacecraftSystemComponent
    {
        public SpacecraftSystemComponent()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            Category = SpacecraftSystemCategory.General;
            CommandedOn = true;
            Health = SpacecraftSystemHealth.Nominal;
            State = SpacecraftSystemState.Online;
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public SpacecraftSystemCategory Category { get; set; }
        public bool CommandedOn { get; set; }
        public SpacecraftSystemHealth Health { get; set; }
        public SpacecraftSystemState State { get; internal set; }

        /// <summary>
        /// Optional state supplied by a lower-level provider such as the
        /// synthetic electrical distribution. Intrinsic OFF/FAILED/DEGRADED
        /// health still takes precedence.
        /// </summary>
        internal SpacecraftSystemState? ProviderStateOverride { get; set; }

        internal SpacecraftSystemComponent Clone()
        {
            return
                new SpacecraftSystemComponent
                {
                    Id = Id ?? string.Empty,
                    DisplayName = DisplayName ?? string.Empty,
                    Category = Category,
                    CommandedOn = CommandedOn,
                    Health = Health,
                    State = State,
                    ProviderStateOverride =
                        ProviderStateOverride
                };
        }
    }

    /// <summary>
    /// Engine-owned synthetic spacecraft model.
    ///
    /// Build 14.0 deliberately models operational topology only:
    /// components, intrinsic health, commanded state, and dependencies.
    /// It does not yet model voltage, current, breakers, batteries, failures,
    /// or any mutation of the real KSP vessel.
    /// </summary>
    public sealed class SpacecraftSystemsModel
    {
        private readonly List<SpacecraftSystemComponent> _components;
        private readonly List<SpacecraftSystemDependency> _dependencies;

        public SpacecraftSystemsModel()
        {
            VesselId = string.Empty;
            VesselName = string.Empty;
            TemplateId = string.Empty;
            GeneratedUtc = DateTime.MinValue;
            ElectricalDistribution =
                new SyntheticElectricalDistributionModel();

            _components =
                new List<SpacecraftSystemComponent>();

            _dependencies =
                new List<SpacecraftSystemDependency>();
        }

        public string VesselId { get; internal set; }
        public string VesselName { get; internal set; }
        public long TopologyRevision { get; internal set; }
        public string TemplateId { get; internal set; }
        public DateTime GeneratedUtc { get; internal set; }

        public SyntheticElectricalDistributionModel ElectricalDistribution
        {
            get;
            internal set;
        }

        public IList<SpacecraftSystemComponent> Components
        {
            get { return _components; }
        }

        public IList<SpacecraftSystemDependency> Dependencies
        {
            get { return _dependencies; }
        }

        public SpacecraftSystemComponent FindComponent(
            string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int index = 0;
                 index < _components.Count;
                 index++)
            {
                SpacecraftSystemComponent component =
                    _components[index];

                if (component != null &&
                    string.Equals(
                        component.Id,
                        id,
                        StringComparison.Ordinal))
                {
                    return component;
                }
            }

            return null;
        }

        public void Recalculate()
        {
            /*
             * Start from intrinsic component state.
             * Later passes resolve required dependencies.
             */
            for (int index = 0;
                 index < _components.Count;
                 index++)
            {
                SpacecraftSystemComponent component =
                    _components[index];

                if (component == null)
                {
                    continue;
                }

                component.State =
                    DetermineIntrinsicState(
                        component);
            }

            /*
             * Re-run until stable so a dependency chain can propagate through
             * more than one level. The bounded pass count prevents malformed
             * future templates from creating an infinite loop.
             */
            int maximumPasses =
                Math.Max(
                    1,
                    _components.Count);

            for (int pass = 0;
                 pass < maximumPasses;
                 pass++)
            {
                bool changed = false;

                for (int index = 0;
                     index < _dependencies.Count;
                     index++)
                {
                    SpacecraftSystemDependency dependency =
                        _dependencies[index];

                    if (dependency == null ||
                        !dependency.Required)
                    {
                        continue;
                    }

                    SpacecraftSystemComponent source =
                        FindComponent(
                            dependency.SourceId);

                    SpacecraftSystemComponent target =
                        FindComponent(
                            dependency.TargetId);

                    if (source == null ||
                        target == null ||
                        target.State ==
                            SpacecraftSystemState.Failed ||
                        target.State ==
                            SpacecraftSystemState.Offline)
                    {
                        continue;
                    }

                    SpacecraftSystemState? dependencyState =
                        ResolveDependencyState(
                            dependency.Kind,
                            source.State);

                    if (!dependencyState.HasValue)
                    {
                        continue;
                    }

                    SpacecraftSystemState next =
                        dependencyState.Value;

                    /*
                     * Never let a dependency overwrite a more severe intrinsic
                     * target state. In Build 14.2 the important case is a
                     * degraded electrical provider: the powered equipment must
                     * become DEGRADED, while a dead provider must make it
                     * UNPOWERED.
                     */
                    if (target.State != next)
                    {
                        target.State = next;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }
            }
        }

        public SpacecraftSystemsModel Clone()
        {
            SpacecraftSystemsModel clone =
                new SpacecraftSystemsModel
                {
                    VesselId = VesselId ?? string.Empty,
                    VesselName = VesselName ?? string.Empty,
                    TopologyRevision = TopologyRevision,
                    TemplateId = TemplateId ?? string.Empty,
                    GeneratedUtc = GeneratedUtc,
                    ElectricalDistribution =
                        ElectricalDistribution != null
                            ? ElectricalDistribution.Clone()
                            : new SyntheticElectricalDistributionModel()
                };

            for (int index = 0;
                 index < _components.Count;
                 index++)
            {
                SpacecraftSystemComponent component =
                    _components[index];

                if (component != null)
                {
                    clone.Components.Add(
                        component.Clone());
                }
            }

            for (int index = 0;
                 index < _dependencies.Count;
                 index++)
            {
                SpacecraftSystemDependency dependency =
                    _dependencies[index];

                if (dependency != null)
                {
                    clone.Dependencies.Add(
                        dependency.Clone());
                }
            }

            return clone;
        }

        private static SpacecraftSystemState
            DetermineIntrinsicState(
                SpacecraftSystemComponent component)
        {
            if (component == null)
            {
                return
                    SpacecraftSystemState.Offline;
            }

            if (!component.CommandedOn)
            {
                return
                    SpacecraftSystemState.Offline;
            }

            switch (component.Health)
            {
                case SpacecraftSystemHealth.Failed:
                    return
                        SpacecraftSystemState.Failed;

                case SpacecraftSystemHealth.Degraded:
                    return
                        SpacecraftSystemState.Degraded;
            }

            if (component.ProviderStateOverride.HasValue)
            {
                return
                    component.ProviderStateOverride.Value;
            }

            return
                SpacecraftSystemState.Online;
        }

        private static SpacecraftSystemState?
            ResolveDependencyState(
                SpacecraftDependencyKind kind,
                SpacecraftSystemState sourceState)
        {
            if (kind ==
                    SpacecraftDependencyKind.Power)
            {
                switch (sourceState)
                {
                    case SpacecraftSystemState.Online:
                        return null;

                    case SpacecraftSystemState.Degraded:
                        return
                            SpacecraftSystemState.Degraded;

                    default:
                        return
                            SpacecraftSystemState.Unpowered;
                }
            }

            /*
             * Functional/data dependencies preserve the original 14.0
             * behavior: ONLINE and DEGRADED providers remain usable, while
             * an unavailable provider degrades the dependent system.
             */
            if (sourceState ==
                    SpacecraftSystemState.Online ||
                sourceState ==
                    SpacecraftSystemState.Degraded)
            {
                return null;
            }

            return
                SpacecraftSystemState.Degraded;
        }
    }
}
