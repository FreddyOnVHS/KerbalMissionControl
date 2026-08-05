using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace KMC.Plugin
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class EngineStateTelemetrySender : MonoBehaviour
    {
        private const int Port = 5058;
        private const float SendIntervalSeconds = 0.1f;
        private const string ProtocolId = "KMC-ENGINE1";
        private UdpClient _client;
        private IPEndPoint _endpoint;
        private float _nextSendTime;

        public void Start()
        {
            _client = new UdpClient();
            _endpoint = new IPEndPoint(IPAddress.Loopback, Port);
        }

        public void Update()
        {
            if (Time.realtimeSinceStartup < _nextSendTime) return;
            _nextSendTime = Time.realtimeSinceStartup + SendIntervalSeconds;
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null || vessel.parts == null || _client == null) return;

            StringBuilder message = new StringBuilder();
            message.Append(ProtocolId).Append('|')
                .Append(DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));

            foreach (Part part in vessel.parts)
            {
                if (part == null || part.Modules == null) continue;
                State state = ReadPart(part);
                if (state == null) continue;
                message.Append('|').Append(state.PartId.ToString(CultureInfo.InvariantCulture))
                    .Append(',').Append(state.Ignited ? '1' : '0')
                    .Append(',').Append(state.Producing ? '1' : '0')
                    .Append(',').Append(state.Flameout ? '1' : '0')
                    .Append(',').Append(state.Shutdown ? '1' : '0')
                    .Append(',').Append(state.CurrentThrust.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',').Append(state.MaximumThrust.ToString("R", CultureInfo.InvariantCulture))
                    .Append(',').Append(state.IsSolid ? '1' : '0');
            }

            byte[] data = Encoding.UTF8.GetBytes(message.ToString());
            _client.Send(data, data.Length, _endpoint);
        }

        private static State ReadPart(Part part)
        {
            State result = null;
            foreach (PartModule module in part.Modules)
            {
                ModuleEngines engine = module as ModuleEngines;
                if (engine == null) continue;
                if (result == null) result = new State { PartId = part.flightID, Shutdown = true };
                bool ignited = engine.EngineIgnited && !engine.engineShutdown;
                result.Ignited |= ignited;
                result.Producing |= engine.finalThrust > 0.01f;
                result.Flameout |= engine.flameout;
                result.Shutdown &= engine.engineShutdown;
                result.CurrentThrust += Math.Max(0.0, engine.finalThrust);
                double limit = Math.Max(0.0, Math.Min(1.0, engine.thrustPercentage / 100.0));
                result.MaximumThrust += Math.Max(0.0, engine.maxThrust * limit);
                result.IsSolid |= UsesSolidFuel(engine);
            }
            return result;
        }

        private static bool UsesSolidFuel(ModuleEngines engine)
        {
            if (engine == null || engine.propellants == null) return false;
            foreach (Propellant p in engine.propellants)
                if (p != null && string.Equals(p.name, "SolidFuel", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public void OnDestroy()
        {
            if (_client != null) { _client.Close(); _client = null; }
        }

        private sealed class State
        {
            public uint PartId; public bool Ignited; public bool Producing;
            public bool Flameout; public bool Shutdown; public bool IsSolid;
            public double CurrentThrust; public double MaximumThrust;
        }
    }
}
