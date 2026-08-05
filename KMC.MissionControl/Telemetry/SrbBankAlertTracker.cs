using System;

namespace KMC.MissionControl.Telemetry
{
    public enum SrbBankAlertState
    {
        Offline = 0,
        Active = 1,
        Separate = 2,
        Separated = 3
    }

    public sealed class SrbBankAlertBankSnapshot
    {
        public SrbBankAlertState State { get; set; }

        public int BoosterCount { get; set; }

        public bool Burning { get; set; }

        public double Amount { get; set; }

        public double Capacity { get; set; }

        public bool FlashOn { get; set; }
    }

    public sealed class SrbBankAlertSnapshot
    {
        public SrbBankAlertBankSnapshot BankA { get; set; }

        public SrbBankAlertBankSnapshot BankB { get; set; }

        public long AnimationRevision { get; set; }

        public bool ShouldDisplay
        {
            get
            {
                return
                    BankA.State !=
                        SrbBankAlertState.Offline ||
                    BankB.State !=
                        SrbBankAlertState.Offline;
            }
        }
    }

    /// <summary>
    /// Persistent SRB-bank alert state machine.
    ///
    /// ACTIVE -> SEPARATE when attached solid fuel is depleted.
    /// SEPARATE -> SEPARATED when the bank disappears.
    /// SEPARATED -> OFFLINE after a two-second confirmation flash.
    /// </summary>
    public static class SrbBankAlertTracker
    {
        private static readonly object SyncRoot =
            new object();

        private static readonly BankState BankA =
            new BankState();

        private static readonly BankState BankB =
            new BankState();

        private const double EmptyFractionThreshold =
            0.005;

        private static readonly TimeSpan SeparatedDuration =
            TimeSpan.FromSeconds(
                4.0);

        private static readonly TimeSpan FlashInterval =
            TimeSpan.FromMilliseconds(
                250.0);

        public static SrbBankAlertSnapshot Update(
            SolidFuelTelemetrySnapshot telemetry,
            DateTime nowUtc)
        {
            if (telemetry == null)
            {
                telemetry =
                    new SolidFuelTelemetrySnapshot();
            }

            lock (SyncRoot)
            {
                int totalCount =
                    Math.Max(
                        0,
                        telemetry.BoosterCount);

                int bankACount;
                int bankBCount;

                ResolveBankCounts(
                    telemetry,
                    totalCount,
                    out bankACount,
                    out bankBCount);

                UpdateBank(
                    BankA,
                    bankACount,
                    telemetry.LeftBurning,
                    telemetry.LeftAmount,
                    telemetry.LeftCapacity,
                    nowUtc);

                UpdateBank(
                    BankB,
                    bankBCount,
                    telemetry.RightBurning,
                    telemetry.RightAmount,
                    telemetry.RightCapacity,
                    nowUtc);

                bool animationActive =
                    IsFlashing(
                        BankA.State) ||
                    IsFlashing(
                        BankB.State);

                long flashTick =
                    animationActive
                        ? nowUtc.Ticks /
                          FlashInterval.Ticks
                        : 0L;

                bool flashOn =
                    !animationActive ||
                    flashTick % 2L == 0L;

                return new SrbBankAlertSnapshot
                {
                    BankA =
                        CreateSnapshot(
                            BankA,
                            flashOn),

                    BankB =
                        CreateSnapshot(
                            BankB,
                            flashOn),

                    AnimationRevision =
                        flashTick
                };
            }
        }

        private static void ResolveBankCounts(
            SolidFuelTelemetrySnapshot telemetry,
            int totalCount,
            out int bankACount,
            out int bankBCount)
        {
            bool bankAExists =
                telemetry.LeftCapacity >
                0.0001;

            bool bankBExists =
                telemetry.RightCapacity >
                0.0001;

            if (totalCount <= 0)
            {
                bankACount =
                    0;

                bankBCount =
                    0;

                return;
            }

            if (!bankAExists &&
                bankBExists)
            {
                bankACount =
                    0;

                bankBCount =
                    totalCount;

                return;
            }

            if (bankAExists &&
                !bankBExists)
            {
                bankACount =
                    totalCount;

                bankBCount =
                    0;

                return;
            }

            bankACount =
                (totalCount + 1) /
                2;

            bankBCount =
                totalCount -
                bankACount;
        }

        private static void UpdateBank(
            BankState bank,
            int boosterCount,
            bool burning,
            double amount,
            double capacity,
            DateTime nowUtc)
        {
            bool separationDetected =
                bank.PreviousBoosterCount >
                    0 &&
                boosterCount <
                    bank.PreviousBoosterCount;

            if (separationDetected)
            {
                bank.SeparatedUntilUtc =
                    nowUtc +
                    SeparatedDuration;
            }

            if (boosterCount > 0)
            {
                bank.EverObserved =
                    true;

                bool depleted =
                    capacity > 0.0001 &&
                    amount <=
                    Math.Max(
                        0.01,
                        capacity *
                        EmptyFractionThreshold);

                bank.State =
                    depleted &&
                    !burning
                        ? SrbBankAlertState.Separate
                        : SrbBankAlertState.Active;
            }
            else if (bank.EverObserved &&
                     nowUtc <
                     bank.SeparatedUntilUtc)
            {
                bank.State =
                    SrbBankAlertState.Separated;
            }
            else
            {
                bank.State =
                    SrbBankAlertState.Offline;
            }

            bank.BoosterCount =
                boosterCount;

            bank.Burning =
                burning;

            bank.Amount =
                Math.Max(
                    0.0,
                    amount);

            bank.Capacity =
                Math.Max(
                    0.0,
                    capacity);

            bank.PreviousBoosterCount =
                boosterCount;
        }

        private static bool IsFlashing(
            SrbBankAlertState state)
        {
            /*
             * Only the action-required SEPARATE warning flashes.
             * The successful SEPARATED confirmation is steady green.
             */
            return
                state ==
                    SrbBankAlertState.Separate;
        }

        private static SrbBankAlertBankSnapshot CreateSnapshot(
            BankState bank,
            bool flashOn)
        {
            return new SrbBankAlertBankSnapshot
            {
                State =
                    bank.State,

                BoosterCount =
                    bank.BoosterCount,

                Burning =
                    bank.Burning,

                Amount =
                    bank.Amount,

                Capacity =
                    bank.Capacity,

                FlashOn =
                    flashOn
            };
        }

        private sealed class BankState
        {
            public bool EverObserved;
            public int PreviousBoosterCount;
            public int BoosterCount;
            public bool Burning;
            public double Amount;
            public double Capacity;
            public DateTime SeparatedUntilUtc;
            public SrbBankAlertState State;
        }
    }
}
