namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Result of the target-SOI shooting correction.
    /// </summary>
    public sealed class TargetSoiShootingResult
    {
        internal TargetSoiShootingResult(
            LambertParkingOrbitEjectionSolution initialEjection,
            TargetSoiShootingAssessment initialAssessment,
            LambertParkingOrbitEjectionSolution correctedEjection,
            TargetSoiShootingAssessment correctedAssessment,
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

        public TargetSoiShootingAssessment InitialAssessment
        {
            get;
            private set;
        }

        public LambertParkingOrbitEjectionSolution CorrectedEjection
        {
            get;
            private set;
        }

        public TargetSoiShootingAssessment CorrectedAssessment
        {
            get;
            private set;
        }

        public int Iterations { get; private set; }

        public int Evaluations { get; private set; }

        public bool Applied
        {
            get
            {
                return
                    InitialAssessment != null &&
                    CorrectedAssessment != null &&
                    CorrectedAssessment.MissDistanceMeters <
                        InitialAssessment.MissDistanceMeters -
                        1e-6;
            }
        }
    }
}
