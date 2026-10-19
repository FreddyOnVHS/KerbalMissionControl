param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$targets = @(
 'KMC.shared/Topology/VesselTopologyPacketCodec.cs',
 'KMC.Plugin/Topology/VesselTopologyMonitor.cs',
 'KMC.MissionControl/Transport/TelemetryTransport.cs',
 'KMC.MissionControl/Pages/SchematicPage.cs'
)
$files = @{}
foreach ($relative in $targets) {
 $path = Join-Path $root $relative
 if (!(Test-Path -LiteralPath $path)) { throw "Missing required file: $relative" }
 $files[$relative] = [IO.File]::ReadAllText($path)
}
$marker = 'KMC 14.23.11 CHUNKED TOPOLOGY'
$backupRoot = Join-Path $root '.kmc_14_23_11_backup'
if ($Restore) {
 foreach($relative in $targets) {
  $backup = Join-Path $backupRoot $relative
  if (!(Test-Path -LiteralPath $backup)) { throw "Missing restore backup: $relative" }
 }
 foreach($relative in $targets) {
  $path = Join-Path $root $relative
  [IO.File]::WriteAllText($path,[IO.File]::ReadAllText((Join-Path $backupRoot $relative)),(New-Object System.Text.UTF8Encoding($false)))
 }
 Write-Host '14.23.11 source restored.'; exit 0
}
foreach($relative in $targets) {
 if($files[$relative].Contains($marker)) { throw "Already patched: $relative" }
}
function Replace-Once([string]$relative,[string]$old,[string]$new) {
 $source=$files[$relative]
 $pos=$source.IndexOf($old,[StringComparison]::Ordinal)
 if($pos -lt 0) { throw "Preflight anchor missing: $relative / $($old.Substring(0,[Math]::Min(65,$old.Length)))" }
 if($source.IndexOf($old,$pos+$old.Length,[StringComparison]::Ordinal) -ge 0) { throw "Ambiguous anchor: $relative" }
 $files[$relative] = $source.Substring(0,$pos) + $new + $source.Substring($pos+$old.Length)
}
$codec = 'KMC.shared/Topology/VesselTopologyPacketCodec.cs'
$plugin = 'KMC.Plugin/Topology/VesselTopologyMonitor.cs'
$transport = 'KMC.MissionControl/Transport/TelemetryTransport.cs'
$page = 'KMC.MissionControl/Pages/SchematicPage.cs'
$fragmentCode = @'
    // KMC 14.23.11 CHUNKED TOPOLOGY: bounded UDP framing around the unchanged v5 codec.
    // Supports interleaved vessels, stale-fragment eviction, size limits, and FNV-1a integrity.
    public sealed class VesselTopologyFragmentAssembler
    {
        private const int FragmentMagic = unchecked((int)0x4B4D4346); // KMCF
        private const int HeaderSize = 44;
        private const int FragmentPayload = 1156;
        private const int MaxTotalBytes = 1048576;
        private const int MaxFragments = 1024;
        private const int LifetimeSeconds = 8;
        private readonly object _sync = new object();
        private readonly System.Collections.Generic.Dictionary<Guid, Pending> _pending =
            new System.Collections.Generic.Dictionary<Guid, Pending>();

        private sealed class Pending
        {
            public DateTime LastUtc;
            public int Total;
            public int Count;
            public uint Checksum;
            public byte[][] Pieces;
            public int Received;
        }

        private static uint Hash(byte[] bytes)
        {
            uint hash = 2166136261u;
            unchecked { foreach (byte b in bytes) hash = (hash ^ b) * 16777619u; }
            return hash;
        }

        public static byte[][] Split(byte[] encoded)
        {
            if (encoded == null || encoded.Length == 0 || encoded.Length > MaxTotalBytes)
                throw new ArgumentOutOfRangeException("encoded", "Topology packet size exceeds 1 MiB limit.");
            int count = (encoded.Length + FragmentPayload - 1) / FragmentPayload;
            if (count > MaxFragments) throw new InvalidDataException("Too many topology fragments.");
            Guid id = Guid.NewGuid();
            byte[] idBytes = id.ToByteArray();
            uint checksum = Hash(encoded);
            byte[][] chunks = new byte[count][];
            for (int i = 0; i < count; ++i)
            {
                int start = i * FragmentPayload;
                int size = Math.Min(FragmentPayload, encoded.Length - start);
                using (MemoryStream ms = new MemoryStream(HeaderSize + size))
                using (BinaryWriter bw = new BinaryWriter(ms))
                {
                    bw.Write(FragmentMagic);
                    bw.Write(1); // envelope version
                    bw.Write(idBytes);
                    bw.Write(encoded.Length);
                    bw.Write(count);
                    bw.Write(i);
                    bw.Write(checksum);
                    bw.Write(size);
                    bw.Write(encoded, start, size);
                    bw.Flush();
                    chunks[i] = ms.ToArray();
                }
            }
            return chunks;
        }

        // Returns false only if the datagram is unrelated to the fragment protocol.
        // When true, a null completed result means incomplete or invalid input.
        public bool Accept(byte[] datagram, out byte[] completed)
        {
            completed = null;
            if (datagram == null || datagram.Length < 4 || BitConverter.ToInt32(datagram, 0) != FragmentMagic)
                return false;
            if (datagram.Length < HeaderSize || datagram.Length > HeaderSize + FragmentPayload) return true;
            try
            {
                using (BinaryReader r = new BinaryReader(new MemoryStream(datagram, false)))
                {
                    r.ReadInt32();
                    if (r.ReadInt32() != 1) return true;
                    Guid id = new Guid(r.ReadBytes(16));
                    int total = r.ReadInt32();
                    int count = r.ReadInt32();
                    int index = r.ReadInt32();
                    uint checksum = r.ReadUInt32();
                    int length = r.ReadInt32();
                    if (total < 1 || total > MaxTotalBytes || count < 1 || count > MaxFragments ||
                        count != (total + FragmentPayload - 1) / FragmentPayload ||
                        index < 0 || index >= count || length < 1 || length > FragmentPayload ||
                        length != Math.Min(FragmentPayload, total - index * FragmentPayload) ||
                        datagram.Length != HeaderSize + length) return true;
                    byte[] payload = r.ReadBytes(length);
                    lock (_sync)
                    {
                        DateTime now = DateTime.UtcNow;
                        var expired = new System.Collections.Generic.List<Guid>();
                        foreach (var item in _pending)
                            if ((now - item.Value.LastUtc).TotalSeconds > LifetimeSeconds) expired.Add(item.Key);
                        foreach (Guid key in expired) _pending.Remove(key);
                        Pending pending;
                        if (!_pending.TryGetValue(id, out pending))
                        {
                            if (_pending.Count >= 8) _pending.Clear();
                            pending = new Pending { LastUtc = now, Total = total, Count = count,
                                Checksum = checksum, Pieces = new byte[count][] };
                            _pending.Add(id, pending);
                        }
                        if (pending.Total != total || pending.Count != count || pending.Checksum != checksum)
                        { _pending.Remove(id); return true; }
                        pending.LastUtc = now;
                        if (pending.Pieces[index] == null) { pending.Pieces[index] = payload; pending.Received++; }
                        else if (!Equal(pending.Pieces[index], payload)) { _pending.Remove(id); return true; }
                        if (pending.Received != count) return true;
                        byte[] result = new byte[total];
                        int position = 0;
                        foreach (byte[] piece in pending.Pieces)
                        {
                            if (piece == null || position + piece.Length > total) { _pending.Remove(id); return true; }
                            Buffer.BlockCopy(piece, 0, result, position, piece.Length);
                            position += piece.Length;
                        }
                        _pending.Remove(id);
                        if (position == total && Hash(result) == checksum) completed = result;
                        return true;
                    }
                }
            }
            catch (Exception) { return true; }
        }

        private static bool Equal(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
    }
'@
# Insert before the namespace closing brace, preserving the existing codec class.
$codecSource = $files[$codec]
$lastBrace = $codecSource.LastIndexOf("`n}",[StringComparison]::Ordinal)
if ($lastBrace -lt 0 -or $lastBrace -lt $codecSource.Length - 12) { throw 'Codec namespace end anchor missing' }
$files[$codec] = $codecSource.Substring(0,$lastBrace) + "`n" + $fragmentCode + $codecSource.Substring($lastBrace)
$oldSend = @'
            _udpClient.Send(
                payload,
                payload.Length,
                _missionControlEndpoint);
'@
$newSend = @'
            // KMC 14.23.11 CHUNKED TOPOLOGY: avoid oversized UDP datagrams.
            byte[][] chunks = VesselTopologyFragmentAssembler.Split(payload);
            foreach (byte[] chunk in chunks)
            {
                _udpClient.Send(chunk, chunk.Length, _missionControlEndpoint);
            }
'@
Replace-Once $plugin $oldSend $newSend
$oldField = '        private readonly Dictionary<int, Channel> _channels ='
$newField = @'
        // KMC 14.23.11 CHUNKED TOPOLOGY: one assembler shared by all UDP channels.
        private readonly VesselTopologyFragmentAssembler _topologyAssembler =
            new VesselTopologyFragmentAssembler();

        private readonly Dictionary<int, Channel> _channels =
'@
Replace-Once $transport $oldField $newField.TrimEnd("`r","`n")
$oldDispatch = @'
            VesselTopology topology;

            if (VesselTopologyPacketCodec.TryDecode(
                    data,
                    out topology))
'@
$newDispatch = @'
            // KMC 14.23.11 CHUNKED TOPOLOGY: never decode incomplete fragments.
            byte[] reconstructed;
            if (_topologyAssembler.Accept(data, out reconstructed))
            {
                if (reconstructed == null) return;
                data = reconstructed;
            }
            VesselTopology topology;

            if (VesselTopologyPacketCodec.TryDecode(
                    data,
                    out topology))
'@
Replace-Once $transport $oldDispatch $newDispatch
$oldGet = @'
            VesselSchematicSnapshot snapshot =
                VesselSchematicTelemetryStore.GetLatest();

            if (snapshot == null ||
'@
$newGet = @'
            // KMC 14.23.11 CHUNKED TOPOLOGY: avoid displaying old vessels.
            VesselSchematicSnapshot snapshot =
                VesselSchematicTelemetryStore.GetLatest();
            bool hasActiveName = telemetry != null &&
                !string.IsNullOrWhiteSpace(telemetry.VesselName);
            bool wrongVessel = snapshot != null && snapshot.Topology != null &&
                hasActiveName && !string.Equals(snapshot.Topology.VesselName,
                    telemetry.VesselName, StringComparison.OrdinalIgnoreCase);
            bool stale = snapshot != null &&
                (DateTime.UtcNow - snapshot.ReceivedUtc).TotalSeconds > 8.0;
            if (wrongVessel || stale)
            {
                DrawWaiting(context, viewBounds);
                DrawStatus(context, statusBounds, null, DateTime.MinValue, 0, 0);
                return;
            }

            if (snapshot == null ||
'@
Replace-Once $page $oldGet $newGet
# All anchors confirmed; only now back up and atomically write the four files.
foreach($relative in $targets) {
 $b = Join-Path $backupRoot $relative
 $folder = Split-Path -Parent $b
 [IO.Directory]::CreateDirectory($folder) | Out-Null
 [IO.File]::WriteAllText($b,[IO.File]::ReadAllText((Join-Path $root $relative)),(New-Object System.Text.UTF8Encoding($false)))
}
foreach($relative in $targets) {
 $path = Join-Path $root $relative
 $temp = $path + '.kmc2311.tmp'
 [IO.File]::WriteAllText($temp,$files[$relative],(New-Object System.Text.UTF8Encoding($false)))
 Move-Item -LiteralPath $temp -Destination $path -Force
}
Write-Host 'KMC 14.23.11 applied. Rebuild BOTH plugin and Mission Control; run tests.'
Write-Warning 'Unverified experimental patch. Keep backup files out of Git.'
