using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// General single-impulse transfer from an elliptic parking orbit to an
    /// arbitrary three-dimensional hyperbolic excess-velocity target.
    ///
    /// The formulation follows the Ocampo/Saudemont class of solutions used by
    /// mature KSP planners: solve the actual hyperbolic departure plane rather
    /// than projecting the desired v-infinity into the parking-orbit plane.
    ///
    /// For the near-circular parking-orbit domain used by KMC, a bounded
    /// one-dimensional rotation search minimizes local impulse magnitude.
    /// </summary>
    internal static class General3dHyperbolicDeparturePlanner
    {
        private const int RotationSamples = 41;
        private const int RotationRefineIterations = 48;
        private const double MaximumParkingEccentricity = 0.05;
        private const double VectorTolerance = 1e-12;

        public static bool TryCalculate(
            OrbitalElements parkingOrbit,
            double referenceBodyRadiusMeters,
            CelestialBodyState originBody,
            Vector3d targetVInfinity,
            double referenceUniversalTimeSeconds,
            out LambertParkingOrbitEjectionSolution solution)
        {
            solution = null;

            if (parkingOrbit == null ||
                originBody == null ||
                !parkingOrbit.HasFiniteGeometry ||
                !targetVInfinity.IsFinite ||
                !Finite(referenceUniversalTimeSeconds) ||
                !Positive(originBody.GravParameter) ||
                !Positive(referenceBodyRadiusMeters) ||
                !Positive(parkingOrbit.SemiMajorAxisMeters) ||
                parkingOrbit.Eccentricity < 0.0 ||
                parkingOrbit.Eccentricity >= 1.0 ||
                parkingOrbit.Eccentricity >
                    MaximumParkingEccentricity)
                return false;

            if (!string.IsNullOrWhiteSpace(parkingOrbit.ReferenceBodyName) &&
                !string.Equals(
                    parkingOrbit.ReferenceBodyName,
                    originBody.Name,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            double vinf =
                targetVInfinity.Magnitude;

            if (!Positive(vinf))
                return false;

            StateVector referenceState;

            if (!KeplerPropagator.TryPropagate(
                    parkingOrbit,
                    originBody.GravParameter,
                    referenceUniversalTimeSeconds,
                    out referenceState))
                return false;

            BurnCandidate best;

            if (!TryOptimizeRotation(
                    originBody.GravParameter,
                    referenceState.Position,
                    referenceState.Velocity,
                    targetVInfinity,
                    out best))
                return false;

            double burnUt =
                referenceUniversalTimeSeconds +
                best.CoastSeconds;

            StateVector actualParkingState;

            if (!KeplerPropagator.TryPropagate(
                    parkingOrbit,
                    originBody.GravParameter,
                    burnUt,
                    out actualParkingState))
                return false;

            double parkingRadius =
                actualParkingState.Position.Magnitude;

            if (!Positive(parkingRadius) ||
                parkingRadius <= referenceBodyRadiusMeters)
                return false;

            Vector3d prograde;
            Vector3d normal;
            Vector3d radial;

            if (!Normalize(
                    actualParkingState.Velocity,
                    out prograde) ||
                !Normalize(
                    Vector3d.Cross(
                        actualParkingState.Position,
                        actualParkingState.Velocity),
                    out normal) ||
                !Normalize(
                    Vector3d.Cross(
                        prograde,
                        normal),
                    out radial))
                return false;

            /*
             * The analytic parking-orbit velocity and the propagated velocity
             * describe the same burn point. Use the propagated KMC state when
             * constructing the actual maneuver impulse so the output P/N/R
             * basis exactly matches the rest of the navigation engine.
             */
            Vector3d deltaV =
                best.PostBurnVelocity -
                actualParkingState.Velocity;

            double totalDv =
                deltaV.Magnitude;

            if (!Positive(totalDv))
                return false;

            double mu =
                originBody.GravParameter;

            double postSpeed2 =
                Vector3d.Dot(
                    best.PostBurnVelocity,
                    best.PostBurnVelocity);

            double specificEnergy =
                0.5 * postSpeed2 -
                mu / parkingRadius;

            if (!Positive(specificEnergy))
                return false;

            double achievedVinf =
                Math.Sqrt(
                    2.0 * specificEnergy);

            Vector3d hVector =
                Vector3d.Cross(
                    actualParkingState.Position,
                    best.PostBurnVelocity);

            double h =
                hVector.Magnitude;

            if (!Positive(h))
                return false;

            Vector3d eccentricityVector =
                Vector3d.Cross(
                    best.PostBurnVelocity,
                    hVector) *
                    (1.0 / mu) -
                actualParkingState.Position *
                    (1.0 / parkingRadius);

            double eccentricity =
                eccentricityVector.Magnitude;

            if (!Finite(eccentricity) ||
                eccentricity <= 1.0)
                return false;

            double periapsisRadius =
                h * h /
                (mu * (1.0 + eccentricity));

            if (!Positive(periapsisRadius))
                return false;

            double periapsisSpeed =
                Math.Sqrt(
                    achievedVinf * achievedVinf +
                    2.0 * mu / periapsisRadius);

            double asymptoteAngle =
                Math.Acos(
                    Clamp(
                        -1.0 / eccentricity,
                        -1.0,
                        1.0));

            solution =
                new LambertParkingOrbitEjectionSolution
                {
                    BurnUniversalTimeSeconds =
                        burnUt,
                    WindowOffsetSeconds =
                        best.CoastSeconds,
                    ParkingRadiusMeters =
                        parkingRadius,
                    ParkingAltitudeMeters =
                        parkingRadius -
                        referenceBodyRadiusMeters,
                    ParkingSpeedMetersPerSecond =
                        actualParkingState.Velocity.Magnitude,
                    HyperbolicExcessSpeedMetersPerSecond =
                        achievedVinf,
                    HyperbolicPeriapsisSpeedMetersPerSecond =
                        periapsisSpeed,
                    HyperbolicEccentricity =
                        eccentricity,
                    AsymptoteAngleDegrees =
                        asymptoteAngle *
                        180.0 / Math.PI,
                    ProgradeDeltaVMetersPerSecond =
                        Vector3d.Dot(
                            deltaV,
                            prograde),
                    NormalDeltaVMetersPerSecond =
                        Vector3d.Dot(
                            deltaV,
                            normal),
                    RadialDeltaVMetersPerSecond =
                        Vector3d.Dot(
                            deltaV,
                            radial),
                    TotalDeltaVMetersPerSecond =
                        totalDv,
                    GeometryResidualDegrees =
                        best.GeometryResidualRadians *
                        180.0 / Math.PI
                };

            return
                Positive(
                    solution.TotalDeltaVMetersPerSecond) &&
                Finite(
                    solution.ProgradeDeltaVMetersPerSecond) &&
                Finite(
                    solution.NormalDeltaVMetersPerSecond) &&
                Finite(
                    solution.RadialDeltaVMetersPerSecond);
        }

        private static bool TryOptimizeRotation(
            double mu,
            Vector3d referencePosition,
            Vector3d referenceVelocity,
            Vector3d targetVInfinity,
            out BurnCandidate best)
        {
            best = null;

            double minimum =
                -0.5 * Math.PI;

            double maximum =
                0.5 * Math.PI;

            int bestIndex =
                -1;

            double bestScore =
                double.PositiveInfinity;

            for (int i = 0;
                i < RotationSamples;
                i++)
            {
                double rotation =
                    minimum +
                    (maximum - minimum) *
                    i /
                    (RotationSamples - 1.0);

                BurnCandidate candidate;

                if (!TryAnalytic(
                        mu,
                        referencePosition,
                        referenceVelocity,
                        targetVInfinity,
                        rotation,
                        out candidate))
                    continue;

                if (candidate.DeltaVMagnitude <
                    bestScore)
                {
                    bestScore =
                        candidate.DeltaVMagnitude;

                    best =
                        candidate;

                    bestIndex =
                        i;
                }
            }

            if (best == null ||
                bestIndex < 0)
                return false;

            double sampleSpacing =
                (maximum - minimum) /
                (RotationSamples - 1.0);

            double left =
                Math.Max(
                    minimum,
                    minimum +
                    (bestIndex - 1) *
                    sampleSpacing);

            double right =
                Math.Min(
                    maximum,
                    minimum +
                    (bestIndex + 1) *
                    sampleSpacing);

            if (right <= left)
                return true;

            const double golden =
                0.6180339887498948482;

            double x1 =
                right -
                golden *
                (right - left);

            double x2 =
                left +
                golden *
                (right - left);

            BurnCandidate c1;
            BurnCandidate c2;

            bool has1 =
                TryAnalytic(
                    mu,
                    referencePosition,
                    referenceVelocity,
                    targetVInfinity,
                    x1,
                    out c1);

            bool has2 =
                TryAnalytic(
                    mu,
                    referencePosition,
                    referenceVelocity,
                    targetVInfinity,
                    x2,
                    out c2);

            for (int iteration = 0;
                iteration < RotationRefineIterations;
                iteration++)
            {
                double score1 =
                    has1
                        ? c1.DeltaVMagnitude
                        : double.PositiveInfinity;

                double score2 =
                    has2
                        ? c2.DeltaVMagnitude
                        : double.PositiveInfinity;

                if (score1 < bestScore)
                {
                    bestScore =
                        score1;
                    best =
                        c1;
                }

                if (score2 < bestScore)
                {
                    bestScore =
                        score2;
                    best =
                        c2;
                }

                if (score1 <= score2)
                {
                    right =
                        x2;

                    x2 =
                        x1;

                    c2 =
                        c1;

                    has2 =
                        has1;

                    x1 =
                        right -
                        golden *
                        (right - left);

                    has1 =
                        TryAnalytic(
                            mu,
                            referencePosition,
                            referenceVelocity,
                            targetVInfinity,
                            x1,
                            out c1);
                }
                else
                {
                    left =
                        x1;

                    x1 =
                        x2;

                    c1 =
                        c2;

                    has1 =
                        has2;

                    x2 =
                        left +
                        golden *
                        (right - left);

                    has2 =
                        TryAnalytic(
                            mu,
                            referencePosition,
                            referenceVelocity,
                            targetVInfinity,
                            x2,
                            out c2);
                }
            }

            return best != null;
        }

        private static bool TryAnalytic(
            double mu,
            Vector3d r0,
            Vector3d v0,
            Vector3d targetVInfinity,
            double rotation,
            out BurnCandidate candidate)
        {
            candidate = null;

            double r0Magnitude =
                r0.Magnitude;

            double v0Squared =
                Vector3d.Dot(
                    v0,
                    v0);

            double vinfSquared =
                Vector3d.Dot(
                    targetVInfinity,
                    targetVInfinity);

            if (!Positive(r0Magnitude) ||
                !Positive(vinfSquared))
                return false;

            Vector3d h0 =
                Vector3d.Cross(
                    r0,
                    v0);

            double h0Magnitude =
                h0.Magnitude;

            if (!Positive(h0Magnitude))
                return false;

            Vector3d h0Hat =
                h0 *
                (1.0 / h0Magnitude);

            double inverseA0 =
                2.0 / r0Magnitude -
                v0Squared / mu;

            if (!Positive(inverseA0))
                return false;

            double a0 =
                1.0 / inverseA0;

            Vector3d eccentricityVector =
                Vector3d.Cross(
                    v0,
                    h0) *
                    (1.0 / mu) -
                r0 *
                    (1.0 / r0Magnitude);

            double eccentricity0 =
                eccentricityVector.Magnitude;

            if (!Finite(eccentricity0) ||
                eccentricity0 >= 1.0)
                return false;

            double p0 =
                a0 *
                (1.0 -
                 eccentricity0 *
                 eccentricity0);

            if (!Positive(p0))
                return false;

            Vector3d rp0Hat;

            if (eccentricity0 >
                1e-10)
            {
                rp0Hat =
                    eccentricityVector *
                    (1.0 / eccentricity0);
            }
            else if (!Normalize(
                         r0,
                         out rp0Hat))
            {
                return false;
            }

            Vector3d vp0Hat;

            if (!Normalize(
                    Vector3d.Cross(
                        h0,
                        rp0Hat),
                    out vp0Hat))
                return false;

            Vector3d vinfHat;

            if (!Normalize(
                    targetVInfinity,
                    out vinfHat))
                return false;

            double planeDot =
                Vector3d.Dot(
                    h0Hat,
                    vinfHat);

            Vector3d hfHat;

            if (1.0 -
                Math.Abs(planeDot) <
                1e-10)
            {
                if (!Normalize(
                        Vector3d.Cross(
                            rp0Hat,
                            vinfHat),
                        out hfHat))
                    return false;
            }
            else
            {
                if (!Normalize(
                        Vector3d.Cross(
                            vinfHat,
                            Vector3d.Cross(
                                h0Hat,
                                vinfHat)),
                        out hfHat))
                    return false;
            }

            Vector3d r1Hat;

            if (Math.Abs(planeDot) >
                1e-10)
            {
                hfHat =
                    RotateAroundAxis(
                        hfHat,
                        vinfHat,
                        -rotation);

                double side =
                    planeDot >= 0.0
                        ? 1.0
                        : -1.0;

                if (!Normalize(
                        Vector3d.Cross(
                            h0Hat,
                            hfHat) *
                            side,
                        out r1Hat))
                    return false;
            }
            else
            {
                if (!Normalize(
                        Vector3d.Cross(
                            vinfHat,
                            hfHat),
                        out r1Hat))
                    return false;

                r1Hat =
                    RotateAroundAxis(
                        r1Hat,
                        h0Hat,
                        -rotation);

                if (!Normalize(
                        r1Hat,
                        out r1Hat))
                    return false;
            }

            double nu10 =
                Math.Atan2(
                    Vector3d.Dot(
                        h0Hat,
                        Vector3d.Cross(
                            rp0Hat,
                            r1Hat)),
                    Clamp(
                        Vector3d.Dot(
                            rp0Hat,
                            r1Hat),
                        -1.0,
                        1.0));

            double denominator =
                1.0 +
                eccentricity0 *
                Math.Cos(
                    nu10);

            if (denominator <= 0.0)
                return false;

            double r1 =
                p0 /
                denominator;

            if (!Positive(r1))
                return false;

            Vector3d burnPosition =
                r1Hat *
                r1;

            double af =
                -mu /
                vinfSquared;

            double k =
                -af /
                r1;

            if (!Positive(k))
                return false;

            double deltaNu =
                SafeAcos(
                    Vector3d.Dot(
                        r1Hat,
                        vinfHat));

            double sinDelta =
                Math.Sin(
                    deltaNu);

            double sin2 =
                sinDelta *
                sinDelta;

            double cosDelta =
                Math.Cos(
                    deltaNu);

            double radical =
                sin2 +
                4.0 *
                k *
                (1.0 - cosDelta);

            if (radical < 0.0)
                return false;

            double numerator =
                sin2 +
                2.0 * k * k +
                2.0 * k *
                    (1.0 - cosDelta) +
                sinDelta *
                    Math.Sqrt(
                        radical);

            if (numerator < 0.0)
                return false;

            double ef =
                Math.Sqrt(
                    numerator) /
                (Math.Sqrt(2.0) * k);

            ef =
                Math.Max(
                    ef,
                    1.0 + 1e-12);

            double pf =
                af *
                (1.0 -
                 ef * ef);

            if (!Positive(pf))
                return false;

            double nu1fArgument =
                (-1.0 / ef) *
                    cosDelta +
                Math.Sqrt(
                    ef * ef - 1.0) /
                    ef *
                    sinDelta;

            double nu1f =
                SafeAcos(
                    nu1fArgument);

            double turningAngle =
                2.0 *
                Math.Asin(
                    Clamp(
                        1.0 / ef,
                        -1.0,
                        1.0));

            Vector3d vinfIncomingHat =
                vinfHat *
                    Math.Cos(
                        turningAngle) +
                Vector3d.Cross(
                    vinfHat,
                    hfHat) *
                    Math.Sin(
                        turningAngle);

            Vector3d rpfHat;
            Vector3d vpfHat;

            if (!Normalize(
                    vinfIncomingHat -
                    vinfHat,
                    out rpfHat) ||
                !Normalize(
                    vinfIncomingHat +
                    vinfHat,
                    out vpfHat))
                return false;

            double hyperbolicScale =
                Math.Sqrt(
                    mu /
                    pf);

            Vector3d vPos =
                rpfHat *
                    (-Math.Sin(
                        nu1f) *
                     hyperbolicScale) +
                vpfHat *
                    ((ef +
                      Math.Cos(
                          nu1f)) *
                     hyperbolicScale);

            double parkingScale =
                Math.Sqrt(
                    mu /
                    p0);

            Vector3d vNeg =
                rp0Hat *
                    (-Math.Sin(
                        nu10) *
                     parkingScale) +
                vp0Hat *
                    ((eccentricity0 +
                      Math.Cos(
                          nu10)) *
                     parkingScale);

            double deltaV =
                (vPos -
                 vNeg).Magnitude;

            if (!Positive(deltaV))
                return false;

            Vector3d r0Hat;

            if (!Normalize(
                    r0,
                    out r0Hat))
                return false;

            double nu0 =
                Math.Atan2(
                    Vector3d.Dot(
                        h0Hat,
                        Vector3d.Cross(
                            rp0Hat,
                            r0Hat)),
                    Clamp(
                        Vector3d.Dot(
                            rp0Hat,
                            r0Hat),
                        -1.0,
                        1.0));

            double root =
                Math.Sqrt(
                    Math.Max(
                        0.0,
                        1.0 -
                        eccentricity0 *
                        eccentricity0));

            double e0 =
                Math.Atan2(
                    root *
                    Math.Sin(
                        nu0),
                    eccentricity0 +
                    Math.Cos(
                        nu0));

            double e1 =
                Math.Atan2(
                    root *
                    Math.Sin(
                        nu10),
                    eccentricity0 +
                    Math.Cos(
                        nu10));

            double m0 =
                e0 -
                eccentricity0 *
                Math.Sin(
                    e0);

            double m1 =
                e1 -
                eccentricity0 *
                Math.Sin(
                    e1);

            double meanMotion =
                Math.Sqrt(
                    mu /
                    (a0 * a0 * a0));

            if (!Positive(meanMotion))
                return false;

            double coast =
                (m1 - m0) /
                meanMotion;

            double period =
                2.0 *
                Math.PI /
                meanMotion;

            while (coast < 0.0)
                coast += period;

            while (coast >= period)
                coast -= period;

            /*
             * By construction the outgoing asymptote direction is vinfHat.
             * Keep a residual based on the achieved hyperbolic energy only;
             * angular residual is numerically zero in the analytic model.
             */
            double energy =
                0.5 *
                Vector3d.Dot(
                    vPos,
                    vPos) -
                mu /
                burnPosition.Magnitude;

            if (!Positive(energy))
                return false;

            double achievedVinf =
                Math.Sqrt(
                    2.0 *
                    energy);

            double targetVinf =
                Math.Sqrt(
                    vinfSquared);

            double relativeEnergyResidual =
                Math.Abs(
                    achievedVinf -
                    targetVinf) /
                Math.Max(
                    1.0,
                    targetVinf);

            candidate =
                new BurnCandidate
                {
                    BurnPosition =
                        burnPosition,
                    ParkingVelocity =
                        vNeg,
                    PostBurnVelocity =
                        vPos,
                    CoastSeconds =
                        coast,
                    DeltaVMagnitude =
                        deltaV,
                    GeometryResidualRadians =
                        relativeEnergyResidual
                };

            return
                burnPosition.IsFinite &&
                vNeg.IsFinite &&
                vPos.IsFinite &&
                Finite(coast) &&
                coast >= 0.0;
        }

        private static Vector3d RotateAroundAxis(
            Vector3d vector,
            Vector3d unitAxis,
            double angle)
        {
            double cosine =
                Math.Cos(
                    angle);

            double sine =
                Math.Sin(
                    angle);

            return
                vector *
                    cosine +
                Vector3d.Cross(
                    unitAxis,
                    vector) *
                    sine +
                unitAxis *
                    (Vector3d.Dot(
                        unitAxis,
                        vector) *
                     (1.0 - cosine));
        }

        private static bool Normalize(
            Vector3d value,
            out Vector3d normalized)
        {
            normalized =
                new Vector3d();

            double magnitude =
                value.Magnitude;

            if (!Positive(magnitude))
                return false;

            normalized =
                value *
                (1.0 / magnitude);

            return
                normalized.IsFinite;
        }

        private static double SafeAcos(
            double value)
        {
            return
                Math.Acos(
                    Clamp(
                        value,
                        -1.0,
                        1.0));
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return
                Math.Max(
                    minimum,
                    Math.Min(
                        maximum,
                        value));
        }

        private static bool Positive(
            double value)
        {
            return
                Finite(value) &&
                value > 0.0;
        }

        private static bool Finite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private sealed class BurnCandidate
        {
            public Vector3d BurnPosition;
            public Vector3d ParkingVelocity;
            public Vector3d PostBurnVelocity;
            public double CoastSeconds;
            public double DeltaVMagnitude;
            public double GeometryResidualRadians;
        }
    }
}
