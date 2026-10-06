namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Deterministic local correction result for a parking-orbit Lambert ejection.
    /// The optimizer changes only burn UT and the local P/N/R impulse components.
    /// </summary>
    public sealed class FiniteSoiDepartureCorrectionResult
    {
        internal FiniteSoiDepartureCorrectionResult(
            LambertParkingOrbitEjectionSolution initialEjection,
            FiniteSoiDepartureAssessment initialAssessment,
            LambertParkingOrbitEjectionSolution correctedEjection,
            FiniteSoiDepartureAssessment correctedAssessment,
            int iterations,
            int evaluations)
        {
            InitialEjection = initialEjection;
            InitialAssessment = initialAssessment;
            CorrectedEjection = correctedEjection;
            CorrectedAssessment = correctedAssessment;
            Iterations = iterations;
            Evaluations = evaluations;
        }

        public LambertParkingOrbitEjectionSolution InitialEjection
        {
            get;
            private set;
        }

        public FiniteSoiDepartureAssessment InitialAssessment
        {
            get;
            private set;
        }

        public LambertParkingOrbitEjectionSolution CorrectedEjection
        {
            get;
            private set;
        }

        public FiniteSoiDepartureAssessment CorrectedAssessment
        {
            get;
            private set;
        }

        public int Iterations
        {
            get;
            private set;
        }

        public int Evaluations
        {
            get;
            private set;
        }

        public bool Applied
        {
            get
            {
                return
                    InitialAssessment != null &&
                    CorrectedAssessment != null &&
                    CorrectedAssessment.NormalizedStateError <
                        InitialAssessment.NormalizedStateError -
                        1e-12;
            }
        }

        public double ScoreImprovement
        {
            get
            {
                if (InitialAssessment == null ||
                    CorrectedAssessment == null)
                    return 0.0;

                return
                    InitialAssessment.NormalizedStateError -
                    CorrectedAssessment.NormalizedStateError;
            }
        }
    }
}
