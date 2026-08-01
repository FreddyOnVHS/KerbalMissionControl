using System.Text;

namespace KMC.Plugin.Simulation
{
    internal static class CraftSimulationReporter
    {
        public static string CreateReport(
            CraftSimulationModel model)
        {
            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                "[KMC] Simulation model:");

            if (model == null)
            {
                builder.AppendLine(
                    "[KMC] Simulation unavailable.");

                return builder.ToString();
            }

            builder.AppendFormat(
                "[KMC] Sim vessel: parts={0}, engines={1}, roots={2}, crossfeed traversals={3}",
                model.Parts.Count,
                model.Engines.Count,
                model.RootPartCount,
                model.CrossFeedLinkCount);

            builder.AppendLine();

            for (int engineIndex = 0;
                 engineIndex < model.Engines.Count;
                 engineIndex++)
            {
                SimulatedEngine engine =
                    model.Engines[engineIndex];

                builder.AppendFormat(
                    "[KMC] Sim engine {0:00}: part={1}, stage={2:00}, SL thrust={3:0.0} kN, VAC thrust={4:0.0} kN, propellants={5}",
                    engineIndex + 1,
                    string.IsNullOrEmpty(
                        engine.PartName)
                        ? "---"
                        : engine.PartName,
                    engine.ActivationStage,
                    engine.SeaLevelThrustKilonewtons,
                    engine.VacuumThrustKilonewtons,
                    engine.Propellants.Count);

                builder.AppendLine();

                foreach (SimulatedPropellant propellant in
                    engine.Propellants)
                {
                    builder.AppendFormat(
                        "[KMC]   Propellant {0}: ratio={1:0.###}, flow={2}, raw={3}",
                        propellant.Name,
                        propellant.Ratio,
                        propellant.FlowCategory,
                        propellant.RawFlowMode);

                    builder.AppendLine();
                }

                SimulatedPart enginePart =
                    FindPart(
                        model,
                        engine.PartPersistentId);

                if (enginePart != null)
                {
                    builder.AppendFormat(
                        "[KMC]   Fuel graph: physical links={0}, crossfeed parts={1}, local resources={2}",
                        enginePart.LinkedPartIds.Count,
                        enginePart.CrossFeedPartIds.Count,
                        enginePart.Resources.Count);

                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private static SimulatedPart FindPart(
            CraftSimulationModel model,
            uint persistentId)
        {
            foreach (SimulatedPart part in
                model.Parts)
            {
                if (part.PersistentId ==
                    persistentId)
                {
                    return part;
                }
            }

            return null;
        }
    }
}
