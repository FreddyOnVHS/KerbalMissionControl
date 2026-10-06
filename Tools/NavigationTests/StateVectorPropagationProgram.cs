using System;
using KMC.Engine.CelestialMechanics;

internal static class StateVectorPropagationProgram
{
    private static int passed;
    private static int failed;

    private static void Main()
    {
        Run("state propagation matches circular quarter orbit", CircularQuarter);
        Run("state propagation is time reversible", Reversible);
        Run("state propagation carries hyperbolic escape outward", Hyperbolic);
        Run("state propagation rejects invalid states", Invalid);

        Console.WriteLine(
            "State propagation: {0} passed, {1} failed",
            passed,
            failed);

        Environment.ExitCode =
            failed == 0 ? 0 : 1;
    }

    private static void Run(string name, Action test)
    {
        try
        {
            test();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception ex)
        {
            failed++;
            Console.WriteLine("FAIL " + name + ": " + ex.Message);
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static void Near(double actual, double expected, double tolerance)
    {
        Check(
            Math.Abs(actual - expected) <= tolerance,
            "expected " + expected.ToString("R") +
            ", got " + actual.ToString("R"));
    }

    private static void CircularQuarter()
    {
        StateVector initial =
            new StateVector(
                new Vector3d(1.0, 0.0, 0.0),
                new Vector3d(0.0, 1.0, 0.0),
                0.0,
                1.0,
                "Primary");

        StateVector state;
        Check(
            StateVectorPropagator.TryPropagate(
                initial,
                Math.PI / 2.0,
                out state),
            "quarter orbit propagation failed");

        Near(state.Position.X, 0.0, 2e-8);
        Near(state.Position.Y, 1.0, 2e-8);
        Near(state.Velocity.X, -1.0, 2e-8);
        Near(state.Velocity.Y, 0.0, 2e-8);
    }

    private static void Reversible()
    {
        StateVector initial =
            new StateVector(
                new Vector3d(1.2, -0.4, 0.2),
                new Vector3d(0.3, 0.8, -0.1),
                10.0,
                1.0,
                "Primary");

        StateVector forward;
        Check(
            StateVectorPropagator.TryPropagate(
                initial,
                14.0,
                out forward),
            "forward propagation failed");

        StateVector back;
        Check(
            StateVectorPropagator.TryPropagate(
                forward,
                10.0,
                out back),
            "reverse propagation failed");

        Near(back.Position.X, initial.Position.X, 2e-7);
        Near(back.Position.Y, initial.Position.Y, 2e-7);
        Near(back.Position.Z, initial.Position.Z, 2e-7);
        Near(back.Velocity.X, initial.Velocity.X, 2e-7);
        Near(back.Velocity.Y, initial.Velocity.Y, 2e-7);
        Near(back.Velocity.Z, initial.Velocity.Z, 2e-7);
    }

    private static void Hyperbolic()
    {
        StateVector initial =
            new StateVector(
                new Vector3d(1.0, 0.0, 0.0),
                new Vector3d(0.0, 2.0, 0.0),
                0.0,
                1.0,
                "Body");

        StateVector state;
        Check(
            StateVectorPropagator.TryPropagate(
                initial,
                5.0,
                out state),
            "hyperbolic propagation failed");

        Check(
            state.Position.Magnitude > 1.0,
            "escape did not move outward");
    }

    private static void Invalid()
    {
        StateVector state;
        Check(
            !StateVectorPropagator.TryPropagate(
                null,
                1.0,
                out state),
            "null accepted");

        StateVector bad =
            new StateVector(
                new Vector3d(),
                new Vector3d(1.0, 0.0, 0.0),
                0.0,
                1.0,
                "Body");

        Check(
            !StateVectorPropagator.TryPropagate(
                bad,
                1.0,
                out state),
            "zero radius accepted");
    }
}
