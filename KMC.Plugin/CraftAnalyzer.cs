using System;
using System.Collections.Generic;
using System.Text;

namespace KMC.Plugin
{
    internal static class CraftAnalyzer
    {
        public static CraftAnalysis Analyze(Vessel vessel)
        {
            CraftAnalysis analysis = new CraftAnalysis();

            if (vessel == null)
            {
                return analysis;
            }

            analysis.VesselName = vessel.vesselName ?? string.Empty;
            analysis.VesselId = vessel.id.ToString();
            analysis.PartCount = vessel.parts != null ? vessel.parts.Count : 0;

            Dictionary<int, StageAnalysis> stages =
                new Dictionary<int, StageAnalysis>();

            if (vessel.parts == null)
            {
                return analysis;
            }

            foreach (Part part in vessel.parts)
            {
                if (part == null)
                {
                    continue;
                }

                int stageNumber = Math.Max(0, part.inverseStage);
                StageAnalysis stage = GetOrCreateStage(stages, stageNumber);

                stage.PartCount++;

                double dryMass = GetPartDryMassTonnes(part);
                double resourceMass = GetPartResourceMassTonnes(part);

                stage.DryMassTonnes += dryMass;
                stage.ResourceMassTonnes += resourceMass;
                stage.WetMassTonnes += dryMass + resourceMass;

                ReadPartResources(part, stage);
                ReadPartModules(part, stage);
            }

            List<int> stageNumbers = new List<int>(stages.Keys);
            stageNumbers.Sort(delegate (int left, int right)
            {
                return right.CompareTo(left);
            });

            foreach (int stageNumber in stageNumbers)
            {
                StageAnalysis stage = stages[stageNumber];

                FinalizeStage(vessel, stage);

                analysis.Stages.Add(stage);
                analysis.TotalDryMassTonnes += stage.DryMassTonnes;
                analysis.TotalResourceMassTonnes += stage.ResourceMassTonnes;
                analysis.EngineCount += stage.EngineCount;

                if (stage.EngineCount > 0)
                {
                    analysis.PropulsiveStageCount++;

                    if (stage.StageNumber >
                        analysis.InitialPropulsiveStageNumber)
                    {
                        analysis.InitialPropulsiveStageNumber =
                            stage.StageNumber;

                        analysis.InitialSeaLevelThrustKilonewtons =
                            stage.SeaLevelThrustKilonewtons;

                        analysis.InitialVacuumThrustKilonewtons =
                            stage.VacuumThrustKilonewtons;
                    }
                }
            }

            analysis.TotalMassTonnes =
                analysis.TotalDryMassTonnes +
                analysis.TotalResourceMassTonnes;

            analysis.MassClass =
                ClassifyMass(
                    analysis.TotalMassTonnes);

            analysis.InitialSeaLevelThrustToWeightRatio =
                CalculateInitialThrustToWeightRatio(
                    vessel,
                    analysis.TotalMassTonnes,
                    analysis.InitialSeaLevelThrustKilonewtons);

            analysis.ThrustClass =
                ClassifyThrust(
                    analysis.InitialSeaLevelThrustToWeightRatio);

            return analysis;
        }

        public static string CreateDiagnosticReport(CraftAnalysis analysis)
        {
            if (analysis == null)
            {
                return "[KMC] Craft analysis unavailable.";
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendLine("[KMC] Craft Analyzer Phase 1");
            builder.Append("[KMC] Vessel: ");
            builder.AppendLine(
                string.IsNullOrEmpty(analysis.VesselName)
                    ? "---"
                    : analysis.VesselName);

            builder.AppendFormat(
                "[KMC] Parts: {0}  Engines: {1}  Propulsive stages: {2}",
                analysis.PartCount,
                analysis.EngineCount,
                analysis.PropulsiveStageCount);

            builder.AppendLine();

            builder.AppendFormat(
                "[KMC] Launch mass: {0:0.000} t  Dry: {1:0.000} t  Resources: {2:0.000} t  Mass class: {3}",
                analysis.TotalMassTonnes,
                analysis.TotalDryMassTonnes,
                analysis.TotalResourceMassTonnes,
                FormatMassClass(
                    analysis.MassClass));

            builder.AppendLine();

            builder.AppendFormat(
                "[KMC] Initial propulsive stage: {0:00}  SL thrust: {1:0.0} kN  VAC thrust: {2:0.0} kN  Initial SL TWR: {3:0.00}  Thrust class: {4}",
                analysis.InitialPropulsiveStageNumber,
                analysis.InitialSeaLevelThrustKilonewtons,
                analysis.InitialVacuumThrustKilonewtons,
                analysis.InitialSeaLevelThrustToWeightRatio,
                FormatThrustClass(
                    analysis.ThrustClass));

            builder.AppendLine();

            foreach (StageAnalysis stage in analysis.Stages)
            {
                builder.AppendFormat(
                    "[KMC] Stage {0:00}: parts={1}, engines={2}, wet={3:0.000} t, dry={4:0.000} t, prop={5:0.000} t, SL thrust={6:0.0} kN, VAC thrust={7:0.0} kN, SL Isp={8:0.0} s, VAC Isp={9:0.0} s, SL TWR={10:0.00}",
                    stage.StageNumber,
                    stage.PartCount,
                    stage.EngineCount,
                    stage.WetMassTonnes,
                    stage.DryMassTonnes,
                    stage.ResourceMassTonnes,
                    stage.SeaLevelThrustKilonewtons,
                    stage.VacuumThrustKilonewtons,
                    stage.SeaLevelSpecificImpulse,
                    stage.VacuumSpecificImpulse,
                    stage.SeaLevelThrustToWeightRatio);

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static StageAnalysis GetOrCreateStage(
            IDictionary<int, StageAnalysis> stages,
            int stageNumber)
        {
            StageAnalysis stage;

            if (!stages.TryGetValue(stageNumber, out stage))
            {
                stage = new StageAnalysis
                {
                    StageNumber = stageNumber
                };

                stages.Add(stageNumber, stage);
            }

            return stage;
        }

        private static double GetPartDryMassTonnes(Part part)
        {
            if (part == null)
            {
                return 0.0;
            }

            double dryMass = part.mass;

            if (part.Modules != null)
            {
                foreach (PartModule module in part.Modules)
                {
                    IPartMassModifier modifier =
                        module as IPartMassModifier;

                    if (modifier == null)
                    {
                        continue;
                    }

                    try
                    {
                        dryMass += modifier.GetModuleMass(
                            part.mass,
                            ModifierStagingSituation.CURRENT);
                    }
                    catch
                    {
                    }
                }
            }

            return SanitizeNonNegative(dryMass);
        }

        private static double GetPartResourceMassTonnes(Part part)
        {
            if (part == null || part.Resources == null)
            {
                return 0.0;
            }

            double mass = 0.0;

            foreach (PartResource resource in part.Resources)
            {
                if (resource == null || resource.info == null)
                {
                    continue;
                }

                double resourceMass =
                    resource.amount * resource.info.density;

                if (IsFinite(resourceMass) && resourceMass > 0.0)
                {
                    mass += resourceMass;
                }
            }

            return mass;
        }

        private static void ReadPartResources(
            Part part,
            StageAnalysis stage)
        {
            if (part == null ||
                stage == null ||
                part.Resources == null)
            {
                return;
            }

            foreach (PartResource resource in part.Resources)
            {
                if (resource == null || resource.info == null)
                {
                    continue;
                }

                string name = resource.info.name ?? string.Empty;

                if (string.Equals(name, "LiquidFuel", StringComparison.Ordinal))
                {
                    stage.LiquidFuelAmount += SanitizeNonNegative(resource.amount);
                    stage.LiquidFuelCapacity += SanitizeNonNegative(resource.maxAmount);
                }
                else if (string.Equals(name, "Oxidizer", StringComparison.Ordinal))
                {
                    stage.OxidizerAmount += SanitizeNonNegative(resource.amount);
                    stage.OxidizerCapacity += SanitizeNonNegative(resource.maxAmount);
                }
                else if (string.Equals(name, "SolidFuel", StringComparison.Ordinal))
                {
                    stage.SolidFuelAmount += SanitizeNonNegative(resource.amount);
                    stage.SolidFuelCapacity += SanitizeNonNegative(resource.maxAmount);
                }
                else if (string.Equals(name, "MonoPropellant", StringComparison.Ordinal))
                {
                    stage.MonopropellantAmount += SanitizeNonNegative(resource.amount);
                    stage.MonopropellantCapacity += SanitizeNonNegative(resource.maxAmount);
                }
            }
        }

        private static void ReadPartModules(
            Part part,
            StageAnalysis stage)
        {
            if (part == null ||
                stage == null ||
                part.Modules == null)
            {
                return;
            }

            foreach (PartModule module in part.Modules)
            {
                if (module == null)
                {
                    continue;
                }

                ModuleEngines engine = module as ModuleEngines;

                if (engine != null)
                {
                    ReadEngine(engine, stage);
                    continue;
                }

                string moduleName = module.moduleName ?? string.Empty;

                if (moduleName.IndexOf(
                        "Decouple",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    stage.DecouplerCount++;
                }
            }
        }

        private static void ReadEngine(
            ModuleEngines engine,
            StageAnalysis stage)
        {
            stage.EngineCount++;

            double thrustLimit =
                Clamp(engine.thrustPercentage / 100.0, 0.0, 1.0);

            double vacuumThrust =
                SanitizeNonNegative(engine.maxThrust) * thrustLimit;

            double seaLevelIsp =
                EvaluateSpecificImpulse(engine, 1.0f);

            double vacuumIsp =
                EvaluateSpecificImpulse(engine, 0.0f);

            stage.SeaLevelThrustKilonewtons +=
                ConvertVacuumThrustToAmbientThrust(
                    vacuumThrust,
                    vacuumIsp,
                    seaLevelIsp);

            stage.VacuumThrustKilonewtons += vacuumThrust;

            if (seaLevelIsp > 0.0)
            {
                stage.SeaLevelSpecificImpulseTotal += seaLevelIsp;
                stage.SeaLevelSpecificImpulseSamples++;
            }

            if (vacuumIsp > 0.0)
            {
                stage.VacuumSpecificImpulseTotal += vacuumIsp;
                stage.VacuumSpecificImpulseSamples++;
            }
        }

        private static double ConvertVacuumThrustToAmbientThrust(
            double vacuumThrust,
            double vacuumIsp,
            double ambientIsp)
        {
            if (vacuumThrust <= 0.0 ||
                vacuumIsp <= 0.0 ||
                ambientIsp <= 0.0)
            {
                return 0.0;
            }

            return vacuumThrust * ambientIsp / vacuumIsp;
        }

        private static double EvaluateSpecificImpulse(
            ModuleEngines engine,
            float pressureAtmospheres)
        {
            if (engine == null || engine.atmosphereCurve == null)
            {
                return 0.0;
            }

            return SanitizeNonNegative(
                engine.atmosphereCurve.Evaluate(pressureAtmospheres));
        }

        private static void FinalizeStage(
            Vessel vessel,
            StageAnalysis stage)
        {
            if (stage.SeaLevelSpecificImpulseSamples > 0)
            {
                stage.SeaLevelSpecificImpulse =
                    stage.SeaLevelSpecificImpulseTotal /
                    stage.SeaLevelSpecificImpulseSamples;
            }

            if (stage.VacuumSpecificImpulseSamples > 0)
            {
                stage.VacuumSpecificImpulse =
                    stage.VacuumSpecificImpulseTotal /
                    stage.VacuumSpecificImpulseSamples;
            }

            double gravity = GetSurfaceGravity(vessel);

            if (stage.WetMassTonnes > 0.0 && gravity > 0.0)
            {
                stage.SeaLevelThrustToWeightRatio =
                    stage.SeaLevelThrustKilonewtons /
                    (stage.WetMassTonnes * gravity);
            }
        }

        private static double GetSurfaceGravity(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null)
            {
                return 9.80665;
            }

            double gravity = vessel.mainBody.GeeASL * 9.80665;

            if (!IsFinite(gravity) || gravity <= 0.0)
            {
                return 9.80665;
            }

            return gravity;
        }


        private static LaunchMassClass ClassifyMass(
            double launchMassTonnes)
        {
            if (launchMassTonnes < 25.0)
            {
                return LaunchMassClass.Light;
            }

            if (launchMassTonnes < 100.0)
            {
                return LaunchMassClass.Medium;
            }

            if (launchMassTonnes < 400.0)
            {
                return LaunchMassClass.Heavy;
            }

            return LaunchMassClass.SuperHeavy;
        }

        private static ThrustClass ClassifyThrust(
            double initialSeaLevelTwr)
        {
            if (initialSeaLevelTwr < 1.25)
            {
                return ThrustClass.Low;
            }

            if (initialSeaLevelTwr <= 1.75)
            {
                return ThrustClass.Standard;
            }

            return ThrustClass.High;
        }

        private static double CalculateInitialThrustToWeightRatio(
            Vessel vessel,
            double launchMassTonnes,
            double seaLevelThrustKilonewtons)
        {
            if (launchMassTonnes <= 0.0 ||
                seaLevelThrustKilonewtons <= 0.0)
            {
                return 0.0;
            }

            double gravity =
                GetSurfaceGravity(
                    vessel);

            if (gravity <= 0.0)
            {
                return 0.0;
            }

            return
                seaLevelThrustKilonewtons /
                (launchMassTonnes *
                 gravity);
        }

        private static string FormatMassClass(
            LaunchMassClass value)
        {
            switch (value)
            {
                case LaunchMassClass.Light:
                    return "LIGHT";

                case LaunchMassClass.Medium:
                    return "MEDIUM";

                case LaunchMassClass.Heavy:
                    return "HEAVY";

                case LaunchMassClass.SuperHeavy:
                    return "SUPER HEAVY";

                default:
                    return "UNKNOWN";
            }
        }

        private static string FormatThrustClass(
            ThrustClass value)
        {
            switch (value)
            {
                case ThrustClass.Low:
                    return "LOW";

                case ThrustClass.Standard:
                    return "STANDARD";

                case ThrustClass.High:
                    return "HIGH";

                default:
                    return "UNKNOWN";
            }
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double SanitizeNonNegative(double value)
        {
            if (!IsFinite(value) || value < 0.0)
            {
                return 0.0;
            }

            return value;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }


    internal enum LaunchMassClass
    {
        Light,
        Medium,
        Heavy,
        SuperHeavy
    }

    internal enum ThrustClass
    {
        Low,
        Standard,
        High
    }

    internal sealed class CraftAnalysis
    {
        public CraftAnalysis()
        {
            Stages = new List<StageAnalysis>();
        }

        public string VesselId { get; set; }
        public string VesselName { get; set; }
        public int PartCount { get; set; }
        public int EngineCount { get; set; }
        public int PropulsiveStageCount { get; set; }
        public int InitialPropulsiveStageNumber { get; set; } = -1;
        public double TotalMassTonnes { get; set; }
        public double TotalDryMassTonnes { get; set; }
        public double TotalResourceMassTonnes { get; set; }
        public double InitialSeaLevelThrustKilonewtons { get; set; }
        public double InitialVacuumThrustKilonewtons { get; set; }
        public double InitialSeaLevelThrustToWeightRatio { get; set; }
        public LaunchMassClass MassClass { get; set; }
        public ThrustClass ThrustClass { get; set; }
        public IList<StageAnalysis> Stages { get; private set; }
    }

    internal sealed class StageAnalysis
    {
        public int StageNumber { get; set; }
        public int PartCount { get; set; }
        public int EngineCount { get; set; }
        public int DecouplerCount { get; set; }
        public double WetMassTonnes { get; set; }
        public double DryMassTonnes { get; set; }
        public double ResourceMassTonnes { get; set; }
        public double LiquidFuelAmount { get; set; }
        public double LiquidFuelCapacity { get; set; }
        public double OxidizerAmount { get; set; }
        public double OxidizerCapacity { get; set; }
        public double SolidFuelAmount { get; set; }
        public double SolidFuelCapacity { get; set; }
        public double MonopropellantAmount { get; set; }
        public double MonopropellantCapacity { get; set; }
        public double SeaLevelThrustKilonewtons { get; set; }
        public double VacuumThrustKilonewtons { get; set; }
        public double SeaLevelSpecificImpulse { get; set; }
        public double VacuumSpecificImpulse { get; set; }
        public double SeaLevelThrustToWeightRatio { get; set; }

        internal double SeaLevelSpecificImpulseTotal { get; set; }
        internal int SeaLevelSpecificImpulseSamples { get; set; }
        internal double VacuumSpecificImpulseTotal { get; set; }
        internal int VacuumSpecificImpulseSamples { get; set; }
    }
}