using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.Shared;

namespace KMC.MissionControl.Engineering
{
    /// <summary>
    /// Build 14.13.4 sender for the KMC-owned electrical-load lease.
    ///
    /// A short lease is refreshed while Mission Control has a valid Engineering
    /// snapshot. The KSP plugin stops applying the extra EC load automatically
    /// if these heartbeats disappear.
    ///
    /// Build 14.15.1 also reuses this already-proven 500 ms heartbeat to publish
    /// a separate read-only KMC-MFD1 electrical status packet. The MFD transport
    /// has its own UDP port and does not alter the load lease protocol.
    /// </summary>
    internal sealed class ElectricalLoadLeaseSender :
        IDisposable
    {
        private const int HeartbeatMilliseconds = 500;

        private static readonly object StaticSync =
            new object();

        private static ElectricalLoadLeaseSender _instance;

        private readonly UdpClient _client;
        private readonly IPEndPoint _endpoint;
        private readonly Timer _timer;

        private int _sendInProgress;
        private bool _disposed;

        private ElectricalLoadLeaseSender()
        {
            _client =
                new UdpClient();

            _endpoint =
                new IPEndPoint(
                    IPAddress.Loopback,
                    ElectricalLoadLeasePacket.CommandPort);

            _timer =
                new Timer(
                    OnTimer,
                    null,
                    250,
                    HeartbeatMilliseconds);
        }

        public static void EnsureStarted()
        {
            lock (StaticSync)
            {
                if (_instance == null)
                {
                    _instance =
                        new ElectricalLoadLeaseSender();
                }
            }
        }

        private void OnTimer(
            object state)
        {
            if (_disposed ||
                Interlocked.Exchange(
                    ref _sendInProgress,
                    1) != 0)
            {
                return;
            }

            try
            {
                AnalysisPipelineResult result;

                if (!EngineeringSnapshotStore.TryGetLatest(
                        out result) ||
                    result == null ||
                    result.Snapshot == null ||
                    result.Snapshot.SpacecraftSystems == null)
                {
                    return;
                }

                SpacecraftSystemsModel systems =
                    result.Snapshot.SpacecraftSystems;

                SyntheticElectricalDistributionModel distribution =
                    systems.ElectricalDistribution;

                if (distribution == null ||
                    string.IsNullOrWhiteSpace(
                        systems.VesselId))
                {
                    return;
                }

                /*
                 * Build 14.15.1:
                 * Publish only final, observable A/B/ESS/source evidence to the
                 * IVA bridge. The helper is best-effort and self-contained, so
                 * an RPM/MFD transport problem cannot interrupt the real EC
                 * load lease below.
                 */
                KmcMfdStatusSender.TrySend(
                    result);

                double rate =
                    Math.Max(
                        0.0,
                        distribution.KmcOwnedActiveLoadEcPerSecond);

                ElectricalLoadLeasePacket packet =
                    new ElectricalLoadLeasePacket
                    {
                        VesselId =
                            systems.VesselId,
                        EcPerSecond =
                            rate
                    };

                byte[] data =
                    Encoding.UTF8.GetBytes(
                        packet.Serialize());

                _client.Send(
                    data,
                    data.Length,
                    _endpoint);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
                /*
                 * UDP heartbeat is best-effort. Missing heartbeats are safe:
                 * the KSP-side lease expires and the KMC-owned load stops.
                 */
            }
            catch
            {
                /*
                 * Never let a load-bridge transport problem destabilize the
                 * Mission Control UI thread or Engineering pipeline.
                 */
            }
            finally
            {
                Interlocked.Exchange(
                    ref _sendInProgress,
                    0);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_timer != null)
            {
                _timer.Dispose();
            }

            if (_client != null)
            {
                _client.Close();
            }
        }
    }

    /// <summary>
    /// Build 14.15.1 read-only Mission Control -> KSP RPM status publisher.
    ///
    /// No controller command path exists here. Mission Control publishes only
    /// the final electrical evidence already shown on POWER.
    /// </summary>
    internal static class KmcMfdStatusSender
    {
        private static readonly UdpClient Client =
            new UdpClient();

        private static readonly IPEndPoint Endpoint =
            new IPEndPoint(
                IPAddress.Loopback,
                KmcMfdStatusPacket.StatusPort);

        public static void TrySend(
            AnalysisPipelineResult result)
        {
            try
            {
                if (result == null ||
                    result.Snapshot == null ||
                    result.Snapshot.SpacecraftSystems == null)
                {
                    return;
                }

                SpacecraftSystemsModel systems =
                    result.Snapshot.SpacecraftSystems;

                SyntheticElectricalDistributionModel distribution =
                    systems.ElectricalDistribution;

                if (distribution == null ||
                    string.IsNullOrWhiteSpace(
                        systems.VesselId))
                {
                    return;
                }

                SyntheticElectricalBus mainA =
                    distribution.FindBus(
                        "BUS_MAIN_A");

                SyntheticElectricalBus mainB =
                    distribution.FindBus(
                        "BUS_MAIN_B");

                SyntheticElectricalBus essential =
                    distribution.FindBus(
                        "BUS_ESS");

                SyntheticElectricalSource batteryA =
                    distribution.FindSource(
                        "SRC_BAT_A");

                SyntheticElectricalSource batteryB =
                    distribution.FindSource(
                        "SRC_BAT_B");

                /*
                 * Do not manufacture partial MFD truth. If the final Engine
                 * snapshot does not contain every required channel, withhold
                 * the heartbeat and let the KSP-side lease show NO KMC LINK.
                 */
                if (mainA == null ||
                    mainB == null ||
                    essential == null ||
                    batteryA == null ||
                    batteryB == null)
                {
                    return;
                }

                KmcMfdStatusPacket packet =
                    new KmcMfdStatusPacket
                    {
                        VesselId =
                            systems.VesselId,

                        MainAVoltage =
                            mainA.Voltage,
                        MainAState =
                            StateText(
                                mainA.State),
                        MainASource =
                            SourceText(
                                mainA.ActiveSourceId),

                        MainBVoltage =
                            mainB.Voltage,
                        MainBState =
                            StateText(
                                mainB.State),
                        MainBSource =
                            SourceText(
                                mainB.ActiveSourceId),

                        EssentialVoltage =
                            essential.Voltage,
                        EssentialState =
                            StateText(
                                essential.State),
                        EssentialSource =
                            SourceText(
                                essential.ActiveSourceId),

                        BatteryAState =
                            SourceStateText(
                                batteryA.State),
                        BatteryBState =
                            SourceStateText(
                                batteryB.State)
                    };

                byte[] data =
                    Encoding.UTF8.GetBytes(
                        packet.Serialize());

                Client.Send(
                    data,
                    data.Length,
                    Endpoint);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException)
            {
            }
            catch
            {
                /*
                 * Read-only IVA status must never destabilize Mission Control.
                 */
            }
        }

        private static string StateText(
            SyntheticElectricalBusState state)
        {
            return
                state.ToString()
                    .ToUpperInvariant();
        }

        private static string SourceStateText(
            SyntheticElectricalSourceState state)
        {
            return
                state.ToString()
                    .ToUpperInvariant();
        }

        private static string SourceText(
            string sourceId)
        {
            if (string.IsNullOrWhiteSpace(
                    sourceId))
            {
                return "NONE";
            }

            return
                sourceId
                    .Replace(
                        "SRC_",
                        string.Empty)
                    .Trim()
                    .ToUpperInvariant();
        }
    }
}
