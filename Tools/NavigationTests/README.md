# Navigation behavioral tests

Run `./Tools/NavigationTests/Run-Tests.ps1` from PowerShell on Windows with Visual Studio MSBuild and the .NET Framework 4.8 targeting pack. The runner builds a console executable against the real Engine and MissionControl assemblies and returns a nonzero exit code on failure. It uses no NuGet test-framework packages. It renders both MAP tabs offscreen without opening a window or transmitting an uplink.

`Fixtures/legacy-14.22.32.csv` was captured from the unmodified MAP methods at commit `38f30056eb989001859bdacdf135d78f385eda97`, before extraction. The eight deterministic inputs are defined by `Fixture` in Program.cs and by the capture script. Values must not be regenerated against the new engine to make a failing test pass. The capture script requires Windows PowerShell 5.1 and an unmodified 14.22.32 Release build because the old private methods no longer exist after this refactor.

Baseline Python regressions (`python -m pytest -q` at repository root): 359 passed, 10 skipped, 31 failures. The recorded failure identities in `Fixtures/baseline-python-failures.txt` let the verification report distinguish existing failures from new ones. They are not skip/xfail configuration; the Python suite still reports them as failures. Two existing math source-inspection tests now read Engine/Navigation or the adapter where the tested expressions moved. New numerical tests exercise production behavior instead of looking for source strings.

The element-to-state API is independent of KSP, Unity, telemetry and graphics. Numerical tests use synthetic units/bodies and analytical values. Renderer agreement checks frame compatibility, not independent physical correctness. Live KSP acceptance is documented in the build README.


14.22.34 adds zero-revolution Lambert behavioral checks: canonical short/long circular arcs, the standard Vallado 3D reference case, two-body invariant agreement, and rejection of invalid/degenerate boundary geometry. MAP does not consume the Lambert solver in this build.
