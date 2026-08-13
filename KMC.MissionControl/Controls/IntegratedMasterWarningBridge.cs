using System;
using System.Reflection;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Engineering;

namespace KMC.MissionControl.Controls
{
    /// <summary>
    /// Build 14.10.1 compatibility bridge from the integrated spacecraft
    /// caution/warning model into the existing lower EVENT / CAUTION panel.
    ///
    /// Failure truth remains owned by KMC.Engine. This class only requests the
    /// established MASTER WARNING lamp/latch when integrated severity reaches
    /// WARNING.
    /// </summary>
    public sealed class IntegratedMasterWarningBridge :
        IDisposable
    {
        private readonly MissionSummary _summary;
        private readonly Timer _timer;

        private readonly FieldInfo _masterWarningLatchedField;
        private readonly FieldInfo _masterWarningAcknowledgedField;
        private readonly FieldInfo _alarmFlashOnField;
        private readonly FieldInfo _ackBoundsField;
        private readonly FieldInfo _linkStateTimerField;
        private readonly MethodInfo _setLampActiveMethod;
        private readonly MethodInfo _hasCurrentWarningConditionMethod;

        private bool _disposed;
        private bool _integratedWarningActive;
        private bool _integratedWarningAcknowledged;
        private bool _preexistingMasterWarningLatched;
        private string _warningSignature;
        private Timer _summaryLinkStateTimer;

        public IntegratedMasterWarningBridge(
            MissionSummary summary)
        {
            if (summary == null)
            {
                throw new ArgumentNullException(
                    nameof(summary));
            }

            _summary = summary;

            Type type =
                typeof(MissionSummary);

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.NonPublic;

            _masterWarningLatchedField =
                RequireField(
                    type,
                    "_masterWarningLatched",
                    flags);

            _masterWarningAcknowledgedField =
                RequireField(
                    type,
                    "_masterWarningAcknowledged",
                    flags);

            _alarmFlashOnField =
                RequireField(
                    type,
                    "_alarmFlashOn",
                    flags);

            _ackBoundsField =
                RequireField(
                    type,
                    "_ackBounds",
                    flags);

            _linkStateTimerField =
                RequireField(
                    type,
                    "_linkStateTimer",
                    flags);

            _setLampActiveMethod =
                RequireMethod(
                    type,
                    "SetLampActive",
                    flags,
                    new[]
                    {
                        typeof(string),
                        typeof(bool)
                    });

            _hasCurrentWarningConditionMethod =
                RequireMethod(
                    type,
                    "HasCurrentWarningCondition",
                    flags,
                    Type.EmptyTypes);

            _warningSignature =
                string.Empty;

            _summary.MouseDown +=
                OnSummaryMouseDown;

            _summary.KeyDown +=
                OnSummaryKeyDown;

            _summaryLinkStateTimer =
                _linkStateTimerField.GetValue(
                    _summary) as Timer;

            if (_summaryLinkStateTimer == null)
            {
                throw new InvalidOperationException(
                    "MissionSummary link-state timer unavailable.");
            }

            /*
             * MissionSummary's own Tick handler was registered during its
             * constructor. Register this handler afterward so, on the same
             * 500 ms Tick, native warning evaluation runs first and this
             * bridge then reapplies integrated MASTER WARNING state before
             * the UI is painted. This prevents the ACK flicker caused by two
             * independent timers alternately clearing and restoring the latch.
             */
            _summaryLinkStateTimer.Tick +=
                OnSummaryLinkStateTimerTick;

            _timer =
                new Timer
                {
                    Interval = 500
                };

            _timer.Tick +=
                OnTimerTick;

            _timer.Start();
        }

        private static FieldInfo RequireField(
            Type type,
            string name,
            BindingFlags flags)
        {
            FieldInfo field =
                type.GetField(
                    name,
                    flags);

            if (field == null)
            {
                throw new InvalidOperationException(
                    "MissionSummary field unavailable: " +
                    name);
            }

            return field;
        }

        private static MethodInfo RequireMethod(
            Type type,
            string name,
            BindingFlags flags,
            Type[] parameterTypes)
        {
            MethodInfo method =
                type.GetMethod(
                    name,
                    flags,
                    null,
                    parameterTypes,
                    null);

            if (method == null)
            {
                throw new InvalidOperationException(
                    "MissionSummary method unavailable: " +
                    name);
            }

            return method;
        }

        private void OnSummaryLinkStateTimerTick(
            object sender,
            EventArgs e)
        {
            if (_disposed ||
                !_integratedWarningActive ||
                _summary.IsDisposed)
            {
                return;
            }

            /*
             * Native MissionSummary warning evaluation does not know about
             * Build 14 integrated failure truth. After it finishes, maintain
             * the integrated master state in the same Tick. In particular, an
             * acknowledged integrated warning stays steadily illuminated
             * instead of being cleared by MissionSummary and restored 250 ms
             * later by the bridge timer.
             */
            SetBoolean(
                _masterWarningLatchedField,
                true);

            SetBoolean(
                _masterWarningAcknowledgedField,
                _integratedWarningAcknowledged);

            SetMasterWarningLamp(
                true);

            _summary.Invalidate();
        }

        private void OnTimerTick(
            object sender,
            EventArgs e)
        {
            ApplyIntegratedState();
        }

        private void ApplyIntegratedState()
        {
            if (_disposed ||
                _summary.IsDisposed)
            {
                return;
            }

            IntegratedCautionWarningSnapshot snapshot =
                GetIntegratedSnapshot();

            bool warning =
                snapshot != null &&
                snapshot.HighestSeverity ==
                    IntegratedAlertSeverity.Warning;

            string signature =
                warning
                    ? BuildWarningSignature(
                        snapshot)
                    : string.Empty;

            if (warning)
            {
                bool newOccurrence =
                    !_integratedWarningActive ||
                    !string.Equals(
                        signature,
                        _warningSignature,
                        StringComparison.Ordinal);

                if (newOccurrence)
                {
                    _preexistingMasterWarningLatched =
                        GetBoolean(
                            _masterWarningLatchedField);

                    /*
                     * Re-arm only for a genuinely different, stable warning
                     * set. The signature is order-independent in 14.10.4.
                     */
                    _integratedWarningAcknowledged =
                        false;

                    SetBoolean(
                        _alarmFlashOnField,
                        true);
                }

                _integratedWarningActive =
                    true;

                _warningSignature =
                    signature;

                SetBoolean(
                    _masterWarningLatchedField,
                    true);

                SetBoolean(
                    _masterWarningAcknowledgedField,
                    _integratedWarningAcknowledged);

                SetMasterWarningLamp(
                    true);

                _summary.Invalidate();

                return;
            }

            if (!_integratedWarningActive)
            {
                return;
            }

            _integratedWarningActive =
                false;

            _integratedWarningAcknowledged =
                false;

            _warningSignature =
                string.Empty;

            bool directWarning =
                GetDirectWarningCondition();

            if (!_preexistingMasterWarningLatched &&
                !directWarning)
            {
                SetBoolean(
                    _masterWarningLatchedField,
                    false);

                SetBoolean(
                    _masterWarningAcknowledgedField,
                    false);

                SetMasterWarningLamp(
                    false);
            }

            _preexistingMasterWarningLatched =
                false;

            _summary.Invalidate();
        }

        private static IntegratedCautionWarningSnapshot
            GetIntegratedSnapshot()
        {
            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(
                    out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.SpacecraftSystems == null)
            {
                return
                    new IntegratedCautionWarningSnapshot();
            }

            return
                IntegratedCautionWarningAnalyzer.Build(
                    result.Snapshot.SpacecraftSystems);
        }

        private static string BuildWarningSignature(
            IntegratedCautionWarningSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            System.Collections.Generic.List<string> ids =
                new System.Collections.Generic.List<string>();

            for (int index = 0;
                 index < snapshot.Alerts.Count;
                 index++)
            {
                IntegratedAlertItem item =
                    snapshot.Alerts[index];

                if (item == null ||
                    item.Severity !=
                        IntegratedAlertSeverity.Warning)
                {
                    continue;
                }

                string id =
                    item.AlertId ??
                    item.Subsystem ??
                    item.Summary ??
                    "WARNING";

                ids.Add(
                    id);
            }

            /*
             * Alert presentation order is not identity. Sort before building
             * the signature so the same warning set cannot re-arm MASTER
             * WARNING merely because analyzer/list ordering changed.
             */
            ids.Sort(
                StringComparer.Ordinal);

            return
                string.Join(
                    "|",
                    ids.ToArray());
        }

        private void OnSummaryMouseDown(
            object sender,
            MouseEventArgs e)
        {
            if (!_integratedWarningActive)
            {
                return;
            }

            object value =
                _ackBoundsField.GetValue(
                    _summary);

            if (!(value is System.Drawing.Rectangle))
            {
                return;
            }

            System.Drawing.Rectangle bounds =
                (System.Drawing.Rectangle)value;

            if (!bounds.Contains(
                    e.Location))
            {
                return;
            }

            AcknowledgeIntegratedWarningAfterSummary();
        }

        private void OnSummaryKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (!_integratedWarningActive ||
                e.KeyCode != Keys.A)
            {
                return;
            }

            AcknowledgeIntegratedWarningAfterSummary();
        }

        private void AcknowledgeIntegratedWarningAfterSummary()
        {
            _integratedWarningAcknowledged =
                true;

            if (_summary.IsHandleCreated)
            {
                _summary.BeginInvoke(
                    new MethodInvoker(
                        delegate
                        {
                            if (_disposed ||
                                !_integratedWarningActive)
                            {
                                return;
                            }

                            SetBoolean(
                                _masterWarningLatchedField,
                                true);

                            SetBoolean(
                                _masterWarningAcknowledgedField,
                                true);

                            SetMasterWarningLamp(
                                true);

                            _summary.Invalidate();
                        }));
            }
        }

        private bool GetDirectWarningCondition()
        {
            object value =
                _hasCurrentWarningConditionMethod.Invoke(
                    _summary,
                    null);

            return
                value is bool &&
                (bool)value;
        }

        private bool GetBoolean(
            FieldInfo field)
        {
            object value =
                field.GetValue(
                    _summary);

            return
                value is bool &&
                (bool)value;
        }

        private void SetBoolean(
            FieldInfo field,
            bool value)
        {
            field.SetValue(
                _summary,
                value);
        }

        private void SetMasterWarningLamp(
            bool active)
        {
            _setLampActiveMethod.Invoke(
                _summary,
                new object[]
                {
                    "master.warning",
                    active
                });
        }

        /// <summary>
        /// Build 14.10.5 synchronous repair path after MissionSummary receives
        /// a normal telemetry update. MissionSummary.UpdateTelemetry performs
        /// its legacy master-warning evaluation immediately; integrated
        /// warning truth must therefore be reapplied before the WinForms
        /// message loop is allowed to paint.
        /// </summary>
        public void RefreshAfterTelemetry()
        {
            if (_disposed ||
                _summary.IsDisposed)
            {
                return;
            }

            ApplyIntegratedState();

            if (!_integratedWarningActive)
            {
                return;
            }

            SetBoolean(
                _masterWarningLatchedField,
                true);

            SetBoolean(
                _masterWarningAcknowledgedField,
                _integratedWarningAcknowledged);

            SetMasterWarningLamp(
                true);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed =
                true;

            _timer.Stop();
            _timer.Tick -=
                OnTimerTick;
            _timer.Dispose();

            _summary.MouseDown -=
                OnSummaryMouseDown;

            _summary.KeyDown -=
                OnSummaryKeyDown;

            if (_summaryLinkStateTimer != null)
            {
                _summaryLinkStateTimer.Tick -=
                    OnSummaryLinkStateTimerTick;

                _summaryLinkStateTimer =
                    null;
            }
        }
    }
}
