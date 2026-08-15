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
}
