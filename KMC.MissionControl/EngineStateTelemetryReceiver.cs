using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using KMC.MissionControl.Telemetry;
namespace KMC.MissionControl
{
    public sealed class EngineStateTelemetryReceiver : IDisposable
    {
        private const int Port = 5058; private const string ProtocolId = "KMC-ENGINE1";
        private UdpClient _client; private Thread _thread; private volatile bool _running;
        public void Start()
        {
            if (_running) return; _client = new UdpClient(new IPEndPoint(IPAddress.Any, Port)); _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "KMC Engine State Receiver" }; _thread.Start();
        }
        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _client.Receive(ref sender);
                    Dictionary<uint, EngineStateTelemetry> states;
                    if (TryParse(Encoding.UTF8.GetString(data), out states)) EngineStateTelemetryStore.Publish(states);
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { if (!_running) return; }
            }
        }
        private static bool TryParse(string message, out Dictionary<uint, EngineStateTelemetry> states)
        {
            states = new Dictionary<uint, EngineStateTelemetry>();
            string[] records = (message ?? string.Empty).Split('|');
            if (records.Length < 2 || records[0] != ProtocolId) return false;
            long ticks; if (!long.TryParse(records[1], out ticks)) return false;
            for (int i=2;i<records.Length;i++)
            {
                string[] f=records[i].Split(','); if (f.Length!=8) continue;
                uint id; int ign,prod,flame,shutdown,solid; double cur,max;
                if (!uint.TryParse(f[0],out id)||!int.TryParse(f[1],out ign)||!int.TryParse(f[2],out prod)||
                    !int.TryParse(f[3],out flame)||!int.TryParse(f[4],out shutdown)||
                    !double.TryParse(f[5],NumberStyles.Float,CultureInfo.InvariantCulture,out cur)||
                    !double.TryParse(f[6],NumberStyles.Float,CultureInfo.InvariantCulture,out max)||!int.TryParse(f[7],out solid)) continue;
                EngineOperatingState s = flame!=0 ? EngineOperatingState.Flameout : prod!=0 ? EngineOperatingState.Producing :
                    ign!=0 ? EngineOperatingState.Ignited : shutdown!=0 ? EngineOperatingState.Shutdown : EngineOperatingState.Armed;
                states[id]=new EngineStateTelemetry { PartId=id, OperatingState=s, CurrentThrust=cur, MaximumThrust=max, IsSolidBooster=solid!=0 };
            }
            return true;
        }
        public void Stop()
        {
            _running=false; EngineStateTelemetryStore.Clear(); if(_client!=null){_client.Close();_client=null;}
            if(_thread!=null&&_thread.IsAlive&&Thread.CurrentThread!=_thread)_thread.Join(1000); _thread=null;
        }
        public void Dispose(){Stop();}
    }
}
