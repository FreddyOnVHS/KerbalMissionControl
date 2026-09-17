from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REQUIRED = [
    'KMC.shared/OrbitMapPacket.cs',
    'KMC.Plugin/OrbitMapTelemetrySender.cs',
    'KMC.MissionControl/Pages/MapPage.cs',
    'KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs',
]

def main():
    missing = [name for name in REQUIRED if not (ROOT / name).exists()]
    if missing:
        raise SystemExit('14.22.1 payload incomplete; missing: ' + ', '.join(missing))

    main_form = (ROOT / 'KMC.MissionControl/MainForm.cs').read_text(encoding='utf-8-sig')
    plugin_project = (ROOT / 'KMC.Plugin/KMC.Plugin.csproj').read_text(encoding='utf-8-sig')
    if 'new AscentPage(),\n                enabled: false' in main_form:
        raise SystemExit('MAP placeholder is still present; copy the 14.22.1 modified MainForm.cs first.')
    if 'OrbitMapTelemetrySender.cs' not in plugin_project:
        raise SystemExit('Plugin project is not wired for OrbitMapTelemetrySender.cs.')

    print('KMC 14.22.1 payload is present and project wiring anchors are valid.')
    print('Build KMC.Plugin and KMC.MissionControl in Visual Studio before runtime testing.')
    print('KSP Plugin DLL Required? YES')

if __name__ == '__main__':
    main()
