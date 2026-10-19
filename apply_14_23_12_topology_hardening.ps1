# KMC 14.23.12 — Topology transport hardening
# Applies to frozen GitHub 14.23.11 (5f0fab5360a8edfb89e4eeb40381739435d6bca9)
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$files = @(
  'KMC.Plugin/Topology/VesselTopologyMonitor.cs',
  'KMC.shared/Topology/VesselTopologyPacketCodec.cs'
)
$original = @{}
$updated = @{}
function Replace-Once([string]$content, [string]$old, [string]$new, [string]$description) {
  $pos = $content.IndexOf($old, [System.StringComparison]::Ordinal)
  if ($pos -lt 0) { throw "Preflight failed: $description (expected anchor missing or already patched). No files changed." }
  if ($content.IndexOf($old, $pos + $old.Length, [System.StringComparison]::Ordinal) -ge 0) {
    throw "Preflight failed: $description (ambiguous anchor). No files changed."
  }
  return $content.Substring(0, $pos) + $new + $content.Substring($pos + $old.Length)
}
foreach ($file in $files) {
  $full = Join-Path $root $file
  if (-not (Test-Path -LiteralPath $full)) { throw "Missing source: $file. No files changed." }
  $original[$file] = [System.IO.File]::ReadAllText($full)
  $updated[$file] = $original[$file].Replace("`r`n", "`n")
}
$monitor = $files[0]
$codec = $files[1]
# A too-large payload should not throw on every update or starve other KMC telemetry.
$updated[$monitor] = Replace-Once $updated[$monitor] @'
        private float _nextResyncTime;
'@ @'
        private float _nextResyncTime;
        // Log an unsupported topology once per vessel revision, not each resync.
        private string _lastOversizeKey = string.Empty;
'@ 'monitor oversize marker field'
$updated[$monitor] = Replace-Once $updated[$monitor] @'
            _nextResyncTime = 0.0f;
'@ @'
            _nextResyncTime = 0.0f;
            _lastOversizeKey = string.Empty;
'@ 'monitor startup reset'
$updated[$monitor] = Replace-Once $updated[$monitor] @'
            // KMC 14.23.11 CHUNKED TOPOLOGY: avoid oversized UDP datagrams.
            byte[][] chunks = VesselTopologyFragmentAssembler.Split(payload);
            foreach (byte[] chunk in chunks)
            {
                _udpClient.Send(chunk, chunk.Length, _missionControlEndpoint);
            }
'@ @'
            // KMC 14.23.12: reject unsupported sizes without retry-error storms.
            // A later smaller vessel / changed revision can still be transmitted.
            if (payload.Length > VesselTopologyFragmentAssembler.MaximumTopologyBytes)
            {
                string oversizeKey = (topology.VesselId ?? string.Empty) + "/" + topology.Revision;
                if (!string.Equals(_lastOversizeKey, oversizeKey, StringComparison.Ordinal))
                {
                    _lastOversizeKey = oversizeKey;
                    Debug.LogWarning("[KMC] TOPOLOGY OVERSIZE: vessel=" +
                        topology.VesselName + " revision=" + topology.Revision +
                        " encodedBytes=" + payload.Length + " maxBytes=" +
                        VesselTopologyFragmentAssembler.MaximumTopologyBytes +
                        ". SCHEM will become stale; normal flight telemetry continues.");
                }
                return;
            }
            _lastOversizeKey = string.Empty;

            // KMC 14.23.11 CHUNKED TOPOLOGY: avoid oversized UDP datagrams.
            byte[][] chunks = VesselTopologyFragmentAssembler.Split(payload);
            foreach (byte[] chunk in chunks)
            {
                _udpClient.Send(chunk, chunk.Length, _missionControlEndpoint);
            }
'@ 'monitor chunk send'
# Avoid discarding all in-flight messages when a single new message exceeds the cap.
$updated[$codec] = Replace-Once $updated[$codec] @'
        private const int MaxTotalBytes = 1048576;
'@ @'
        private const int MaxTotalBytes = 1048576;
        public const int MaximumTopologyBytes = MaxTotalBytes;
'@ 'codec size constant'
$updated[$codec] = Replace-Once $updated[$codec] @'
        private readonly object _sync = new object();
'@ @'
        private readonly object _sync = new object();
        private long _completedMessages;
        private long _evictedMessages;
        private long _checksumFailures;
        public long CompletedMessages { get { return System.Threading.Interlocked.Read(ref _completedMessages); } }
        public long EvictedMessages { get { return System.Threading.Interlocked.Read(ref _evictedMessages); } }
        public long ChecksumFailures { get { return System.Threading.Interlocked.Read(ref _checksumFailures); } }
'@ 'codec counters'
$updated[$codec] = Replace-Once $updated[$codec] @'
                        foreach (Guid key in expired) _pending.Remove(key);
'@ @'
                        foreach (Guid key in expired)
                        {
                            if (_pending.Remove(key)) System.Threading.Interlocked.Increment(ref _evictedMessages);
                        }
'@ 'codec expired cleanup'
$updated[$codec] = Replace-Once $updated[$codec] @'
                            if (_pending.Count >= 8) _pending.Clear();
'@ @'
                            if (_pending.Count >= 8)
                            {
                                // Evict one oldest incomplete message; protect other vessels.
                                Guid oldestKey = Guid.Empty;
                                DateTime oldestUtc = DateTime.MaxValue;
                                foreach (var item in _pending)
                                {
                                    if (item.Value.LastUtc < oldestUtc)
                                    {
                                        oldestUtc = item.Value.LastUtc;
                                        oldestKey = item.Key;
                                    }
                                }
                                if (_pending.Remove(oldestKey))
                                    System.Threading.Interlocked.Increment(ref _evictedMessages);
                            }
'@ 'codec pending capacity'
$updated[$codec] = Replace-Once $updated[$codec] @'
                        if (position == total && Hash(result) == checksum) completed = result;
'@ @'
                        if (position == total && Hash(result) == checksum)
                        {
                            completed = result;
                            System.Threading.Interlocked.Increment(ref _completedMessages);
                        }
                        else
                        {
                            System.Threading.Interlocked.Increment(ref _checksumFailures);
                        }
'@ 'codec checksum telemetry'
# Preflight passed: write backups outside the repo (not automatically committed).
$backupRoot = Join-Path $env:TEMP ('KMC_14_23_12_backup_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss_fff'))
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
foreach ($file in $files) {
  $dest = Join-Path $backupRoot ($file -replace '[\\/]','__')
  [System.IO.File]::WriteAllText($dest, $original[$file], (New-Object System.Text.UTF8Encoding($false)))
}
$written = @()
try {
  foreach ($file in $files) {
    $target = Join-Path $root $file
    $writeText = $updated[$file]
    if ($original[$file].Contains("`r`n")) { $writeText = $writeText.Replace("`n", "`r`n") }
    [System.IO.File]::WriteAllText($target, $writeText, (New-Object System.Text.UTF8Encoding($false)))
    $written += $file
  }
} catch {
  foreach ($file in $written) {
    [System.IO.File]::WriteAllText((Join-Path $root $file), $original[$file], (New-Object System.Text.UTF8Encoding($false)))
  }
  throw
}
Write-Host 'KMC 14.23.12 topology transport hardening applied.' -ForegroundColor Green
Write-Host "Backups (outside repository): $backupRoot"
Write-Host 'Build/test before deployment. KMC.Plugin.dll and Mission Control must be rebuilt.'
