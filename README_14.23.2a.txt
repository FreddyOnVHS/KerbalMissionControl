KMC 14.23.2a - ParticleSystemRenderer Compile Fix

CAUSE
14.23.2 referenced UnityEngine.ParticleSystemRenderer directly when filtering
renderers. In this KSP/Unity assembly layout, that type is forwarded to:

  UnityEngine.ParticleSystemModule.dll

KMC.Plugin does not currently reference that optional split module, so the
plugin build failed with CS1069.

FIX
Do not add another Unity assembly dependency just to identify renderer types.
The filter now checks renderer.GetType().Name for:
- ParticleSystemRenderer
- LineRenderer
- TrailRenderer

The same renderers are excluded, but the plugin no longer needs a compile-time
reference to UnityEngine.ParticleSystemModule.

Apply:
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_2a_particle_renderer_compile_fix.ps1

Then rebuild KMC.Plugin.

After the plugin builds, run:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

If tests pass, replace the KSP DLL with the newly built KMC.Plugin.dll.
