using System;

namespace KMC.Engine.CelestialMechanics
{
    /// <summary>
    /// Dependency-free, zero-revolution Lambert solver using universal variables
    /// and Stumpff functions. This is a pure two-body boundary-value calculation:
    /// no SOI transitions, body hierarchy, atmospheric effects, thrust model,
    /// maneuver-node creation or KSP/Unity dependency is present here.
    /// </summary>
    public static class LambertSolver
    {
        private const int MaxBracketIterations = 96;
        private const int MaxRootIterations = 192;
        private const double GeometryTolerance = 1e-12;
        private const double TimeToleranceScale = 1e-11;
        private const double StumpffSeriesThreshold = 1e-8;
        private static readonly double MaxPositiveZ =
            4.0 * Math.PI * Math.PI * (1.0 - 1e-12);

        public static bool TrySolve(
            Vector3d departurePosition,
            Vector3d arrivalPosition,
            double timeOfFlightSeconds,
            double gravParameter,
            LambertTransferPath path,
            out LambertSolution solution)
        {
            solution = null;

            if (!departurePosition.IsFinite ||
                !arrivalPosition.IsFinite ||
                !Vector3d.Finite(timeOfFlightSeconds) ||
                !Vector3d.Finite(gravParameter) ||
                timeOfFlightSeconds <= 0.0 ||
                gravParameter <= 0.0)
                return false;

            if (path != LambertTransferPath.ShortWay &&
                path != LambertTransferPath.LongWay)
                return false;

            double r1 = departurePosition.Magnitude;
            double r2 = arrivalPosition.Magnitude;
            if (!Vector3d.Finite(r1) || !Vector3d.Finite(r2) ||
                r1 <= 0.0 || r2 <= 0.0)
                return false;

            double cosine =
                Vector3d.Dot(departurePosition, arrivalPosition) / (r1 * r2);
            if (!Vector3d.Finite(cosine)) return false;
            cosine = Math.Max(-1.0, Math.Min(1.0, cosine));

            Vector3d cross = Vector3d.Cross(departurePosition, arrivalPosition);
            double sineMagnitude = cross.Magnitude / (r1 * r2);
            if (!Vector3d.Finite(sineMagnitude) ||
                sineMagnitude <= GeometryTolerance ||
                1.0 - cosine <= GeometryTolerance)
            {
                // Parallel/antiparallel endpoints do not uniquely identify the
                // transfer plane. Add an explicit plane normal in a later API
                // rather than guessing a physically significant orientation.
                return false;
            }

            double sine =
                path == LambertTransferPath.LongWay
                    ? -sineMagnitude
                    : sineMagnitude;

            double denominator = 1.0 - cosine;
            double a = sine * Math.Sqrt((r1 * r2) / denominator);
            if (!Vector3d.Finite(a) || Math.Abs(a) <= GeometryTolerance)
                return false;

            double z;
            double y;
            if (!TrySolveUniversalVariable(
                    r1,
                    r2,
                    a,
                    timeOfFlightSeconds,
                    gravParameter,
                    out z,
                    out y))
                return false;

            double f = 1.0 - (y / r1);
            double g = a * Math.Sqrt(y / gravParameter);
            double gDot = 1.0 - (y / r2);
            if (!Vector3d.Finite(f) || !Vector3d.Finite(g) ||
                !Vector3d.Finite(gDot) || Math.Abs(g) <= GeometryTolerance)
                return false;

            Vector3d departureVelocity =
                (arrivalPosition - (departurePosition * f)) * (1.0 / g);
            Vector3d arrivalVelocity =
                ((arrivalPosition * gDot) - departurePosition) * (1.0 / g);

            if (!departureVelocity.IsFinite || !arrivalVelocity.IsFinite)
                return false;

            solution = new LambertSolution(
                departureVelocity,
                arrivalVelocity,
                timeOfFlightSeconds,
                gravParameter,
                path);
            return true;
        }

        private static bool TrySolveUniversalVariable(
            double r1,
            double r2,
            double a,
            double timeOfFlightSeconds,
            double gravParameter,
            out double z,
            out double y)
        {
            z = double.NaN;
            y = double.NaN;

            double baseZ = 0.0;
            double baseFunction;
            double baseY;

            if (!TryEvaluateTimeEquation(
                    baseZ,
                    r1,
                    r2,
                    a,
                    timeOfFlightSeconds,
                    gravParameter,
                    out baseFunction,
                    out baseY))
            {
                // For very shallow short-way geometry z=0 can yield y <= 0.
                // Find the first valid positive z, then bisect back toward the
                // y=0 validity boundary so the root cannot be skipped.
                double invalidZ = 0.0;
                double validZ = 0.25;
                bool foundValid = false;

                for (int i = 0; i < MaxBracketIterations; i++)
                {
                    if (validZ > MaxPositiveZ) validZ = MaxPositiveZ;
                    if (TryEvaluateTimeEquation(
                            validZ,
                            r1,
                            r2,
                            a,
                            timeOfFlightSeconds,
                            gravParameter,
                            out baseFunction,
                            out baseY))
                    {
                        foundValid = true;
                        break;
                    }

                    if (validZ >= MaxPositiveZ) break;
                    invalidZ = validZ;
                    validZ = Math.Min(MaxPositiveZ, validZ * 2.0);
                }

                if (!foundValid) return false;

                for (int i = 0; i < 96; i++)
                {
                    double mid = invalidZ + 0.5 * (validZ - invalidZ);
                    double function;
                    double candidateY;
                    if (TryEvaluateTimeEquation(
                            mid,
                            r1,
                            r2,
                            a,
                            timeOfFlightSeconds,
                            gravParameter,
                            out function,
                            out candidateY))
                    {
                        validZ = mid;
                        baseFunction = function;
                        baseY = candidateY;
                    }
                    else
                    {
                        invalidZ = mid;
                    }
                }

                baseZ = validZ;
            }

            double tolerance =
                TimeToleranceScale * Math.Max(1.0, timeOfFlightSeconds);
            if (Math.Abs(baseFunction) <= tolerance)
            {
                z = baseZ;
                y = baseY;
                return true;
            }

            double lowZ;
            double highZ;
            double lowFunction = double.NaN;
            double highFunction = double.NaN;

            if (baseFunction < 0.0)
            {
                lowZ = baseZ;
                lowFunction = baseFunction;
                double step = Math.Max(0.25, Math.Abs(baseZ) * 0.25 + 0.25);
                highZ = Math.Min(MaxPositiveZ, baseZ + step);
                bool bracketed = false;

                for (int i = 0; i < MaxBracketIterations; i++)
                {
                    double candidateFunction;
                    double candidateY;
                    if (TryEvaluateTimeEquation(
                            highZ,
                            r1,
                            r2,
                            a,
                            timeOfFlightSeconds,
                            gravParameter,
                            out candidateFunction,
                            out candidateY) &&
                        candidateFunction >= 0.0)
                    {
                        highFunction = candidateFunction;
                        bracketed = true;
                        break;
                    }

                    if (highZ >= MaxPositiveZ) return false;
                    step *= 2.0;
                    highZ = Math.Min(MaxPositiveZ, baseZ + step);
                }

                if (!bracketed) return false;
            }
            else
            {
                highZ = baseZ;
                highFunction = baseFunction;
                double step = Math.Max(0.25, Math.Abs(baseZ) * 0.25 + 0.25);
                lowZ = baseZ - step;
                bool bracketed = false;
                lowFunction = double.NaN;

                for (int i = 0; i < MaxBracketIterations; i++)
                {
                    double candidateFunction;
                    double candidateY;
                    if (TryEvaluateTimeEquation(
                            lowZ,
                            r1,
                            r2,
                            a,
                            timeOfFlightSeconds,
                            gravParameter,
                            out candidateFunction,
                            out candidateY))
                    {
                        if (candidateFunction <= 0.0)
                        {
                            lowFunction = candidateFunction;
                            bracketed = true;
                            break;
                        }
                    }
                    else
                    {
                        // We crossed below the y>0 domain. Locate the valid
                        // boundary from the known-valid high side. As y tends
                        // to zero the time of flight tends to zero, providing
                        // the negative side of the residual for any dt > 0.
                        double invalidZ = lowZ;
                        double validZ = highZ;
                        double boundaryFunction = highFunction;

                        for (int j = 0; j < 96; j++)
                        {
                            double mid = invalidZ + 0.5 * (validZ - invalidZ);
                            double function;
                            double boundaryY;
                            if (TryEvaluateTimeEquation(
                                    mid,
                                    r1,
                                    r2,
                                    a,
                                    timeOfFlightSeconds,
                                    gravParameter,
                                    out function,
                                    out boundaryY))
                            {
                                validZ = mid;
                                boundaryFunction = function;
                            }
                            else
                            {
                                invalidZ = mid;
                            }
                        }

                        if (boundaryFunction <= 0.0)
                        {
                            lowZ = validZ;
                            lowFunction = boundaryFunction;
                            bracketed = true;
                            break;
                        }

                        return false;
                    }

                    step *= 2.0;
                    lowZ = baseZ - step;
                }

                if (!bracketed) return false;
            }

            double rootY = double.NaN;
            for (int i = 0; i < MaxRootIterations; i++)
            {
                double midZ = lowZ + 0.5 * (highZ - lowZ);
                double midFunction;
                double midY;

                if (!TryEvaluateTimeEquation(
                        midZ,
                        r1,
                        r2,
                        a,
                        timeOfFlightSeconds,
                        gravParameter,
                        out midFunction,
                        out midY))
                {
                    lowZ = midZ;
                    continue;
                }

                rootY = midY;
                if (Math.Abs(midFunction) <= tolerance)
                {
                    z = midZ;
                    y = midY;
                    return true;
                }

                if (midFunction < 0.0)
                {
                    lowZ = midZ;
                    lowFunction = midFunction;
                }
                else
                {
                    highZ = midZ;
                    highFunction = midFunction;
                }

                if (Math.Abs(highZ - lowZ) <=
                    2e-14 * Math.Max(1.0, Math.Abs(midZ)))
                {
                    z = lowZ + 0.5 * (highZ - lowZ);
                    double finalFunction;
                    if (!TryEvaluateTimeEquation(
                            z,
                            r1,
                            r2,
                            a,
                            timeOfFlightSeconds,
                            gravParameter,
                            out finalFunction,
                            out y))
                        return false;

                    return Math.Abs(finalFunction) <= 10.0 * tolerance;
                }
            }

            return false;
        }

        private static bool TryEvaluateTimeEquation(
            double z,
            double r1,
            double r2,
            double a,
            double requestedTime,
            double gravParameter,
            out double function,
            out double y)
        {
            function = double.NaN;
            y = double.NaN;

            double c;
            double s;
            if (!TryStumpff(z, out c, out s) || c <= 0.0)
                return false;

            double sqrtC = Math.Sqrt(c);
            y = r1 + r2 + a * (((z * s) - 1.0) / sqrtC);
            if (!Vector3d.Finite(y) || y <= 0.0)
                return false;

            double x = Math.Sqrt(y / c);
            double calculatedTime =
                ((x * x * x * s) + (a * Math.Sqrt(y))) /
                Math.Sqrt(gravParameter);

            if (!Vector3d.Finite(calculatedTime) || calculatedTime < 0.0)
                return false;

            function = calculatedTime - requestedTime;
            return Vector3d.Finite(function);
        }

        private static bool TryStumpff(double z, out double c, out double s)
        {
            c = double.NaN;
            s = double.NaN;
            if (!Vector3d.Finite(z)) return false;

            double magnitude = Math.Abs(z);
            if (magnitude < StumpffSeriesThreshold)
            {
                double z2 = z * z;
                double z3 = z2 * z;
                c =
                    0.5 -
                    (z / 24.0) +
                    (z2 / 720.0) -
                    (z3 / 40320.0);
                s =
                    (1.0 / 6.0) -
                    (z / 120.0) +
                    (z2 / 5040.0) -
                    (z3 / 362880.0);
                return Vector3d.Finite(c) && Vector3d.Finite(s);
            }

            if (z > 0.0)
            {
                double root = Math.Sqrt(z);
                c = (1.0 - Math.Cos(root)) / z;
                s = (root - Math.Sin(root)) / (root * root * root);
            }
            else
            {
                double root = Math.Sqrt(-z);
                if (!Vector3d.Finite(root) || root > 700.0)
                    return false;
                c = (Math.Cosh(root) - 1.0) / (-z);
                s = (Math.Sinh(root) - root) / (root * root * root);
            }

            return
                Vector3d.Finite(c) &&
                Vector3d.Finite(s) &&
                c > 0.0;
        }
    }
}
