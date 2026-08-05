using System;
using System.Drawing;
using KMC.MissionControl.Cards;
using KMC.MissionControl.Cards.Propulsion;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;

namespace KMC.MissionControl.Pages
{
    public sealed class PropulsionPage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        private readonly EngineClusterCard
            _engineClusterCard =
                new EngineClusterCard();

        private readonly PropulsionPerformanceCard
            _performanceCard =
                new PropulsionPerformanceCard();

        private readonly PropellantFlowCard
            _propellantFlowCard =
                new PropellantFlowCard();

        private readonly PropulsionFooterCard
            _footerCard =
                new PropulsionFooterCard();

        private readonly PropulsionCardChangeTracker
            _changeTracker =
                new PropulsionCardChangeTracker();

        private long _lastTopologyRevision =
            long.MinValue;

        private int _lastStage =
            int.MinValue;

        public string Name
        {
            get { return "PROPULSION"; }
        }

        public Size PreferredVirtualCanvasSize
        {
            get
            {
                return new Size(
                    2400,
                    1350);
            }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return
                    MissionPageContentProfile.DenseEngineering;
            }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (telemetry == null)
            {
                return;
            }

            MissionPageLayout pageLayout =
                new MissionPageLayout(
                    context);

            pageLayout.DrawHeader(
                Name,
                "CH 04");

            Rectangle working =
                new Rectangle(
                    context.ContentBounds.Left + 18,
                    context.ContentBounds.Top + 78,
                    context.ContentBounds.Width - 36,
                    context.ContentBounds.Height - 98);

            MissionCardLayout layout =
                MissionCardLayoutEngine
                    .CalculatePropulsion(
                        working);

            PropulsionRenderGraph graph =
                PropulsionGraphStore.GetCurrent();

            PropulsionAnalysis analysis =
                graph != null
                    ? PropulsionAnalysisCache
                        .GetOrBuild(graph)
                    : null;

            bool topologyChanged =
                graph == null
                    ? _lastTopologyRevision !=
                        long.MinValue
                    : graph.TopologyRevision !=
                        _lastTopologyRevision ||
                      graph.CurrentStage !=
                        _lastStage;

            if (topologyChanged)
            {
                _changeTracker.Reset();

                MarkAllCardsDirty(
                    CardDirtyState.Static |
                    CardDirtyState.Telemetry);
            }

            PropulsionCardChangeSet changes =
                _changeTracker.Evaluate(
                    telemetry,
                    graph);

            if (changes.EngineClusterChanged)
            {
                _engineClusterCard.MarkDirty(
                    CardDirtyState.Telemetry);
            }

            if (changes.PerformanceChanged)
            {
                _performanceCard.MarkDirty(
                    CardDirtyState.Telemetry);
            }

            if (changes.FlowChanged)
            {
                _propellantFlowCard.MarkDirty(
                    CardDirtyState.Telemetry);
            }

            if (changes.FooterChanged)
            {
                _footerCard.MarkDirty(
                    CardDirtyState.Telemetry);
            }

            _lastTopologyRevision =
                graph != null
                    ? graph.TopologyRevision
                    : long.MinValue;

            _lastStage =
                graph != null
                    ? graph.CurrentStage
                    : int.MinValue;

            PropulsionPageRenderModel model =
                new PropulsionPageRenderModel
                {
                    Graph =
                        graph,

                    Analysis =
                        analysis,

                    Telemetry =
                        telemetry
                };

            _engineClusterCard.Bounds =
                layout.EngineCluster;

            _performanceCard.Bounds =
                layout.Performance;

            _propellantFlowCard.Bounds =
                layout.PropellantFlow;

            _footerCard.Bounds =
                layout.Footer;

            _engineClusterCard.Draw(
                context,
                model);

            _performanceCard.Draw(
                context,
                model);

            _propellantFlowCard.Draw(
                context,
                model);

            _footerCard.Draw(
                context,
                model);
        }

        private void MarkAllCardsDirty(
            CardDirtyState dirtyState)
        {
            _engineClusterCard.MarkDirty(
                dirtyState);

            _performanceCard.MarkDirty(
                dirtyState);

            _propellantFlowCard.MarkDirty(
                dirtyState);

            _footerCard.MarkDirty(
                dirtyState);
        }
    }
}
