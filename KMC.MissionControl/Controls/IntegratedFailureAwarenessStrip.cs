using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Engineering;

namespace KMC.MissionControl.Controls
{
    /// <summary>
    /// Build 14.10 persistent cross-console systems caution/warning strip.
    ///
    /// The strip reads Engine-owned integrated warning truth. ACK only changes
    /// the local annunciation latch; it never clears a failure or changes
    /// spacecraft-system state.
    /// </summary>
    public sealed class IntegratedFailureAwarenessStrip : Control
    {
        private readonly Timer _refreshTimer;
        private readonly Font _titleFont;
        private readonly Font _statusFont;
        private readonly Font _smallFont;
        private readonly HashSet<string> _previousAlertIds;

        private IntegratedCautionWarningSnapshot _snapshot;
        private IntegratedAlertSeverity _latchedSeverity;
        private bool _acknowledged;
        private bool _flashOn;
        private Rectangle _ackBounds;

        public IntegratedFailureAwarenessStrip()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            TabStop = true;
            Cursor = Cursors.Hand;

            BackColor =
                Color.FromArgb(
                    24,
                    29,
                    27);

            ForeColor =
                Color.FromArgb(
                    190,
                    255,
                    190);

            _titleFont =
                new Font(
                    "Consolas",
                    8.0f,
                    FontStyle.Bold);

            _statusFont =
                new Font(
                    "Consolas",
                    9.0f,
                    FontStyle.Bold);

            _smallFont =
                new Font(
                    "Consolas",
                    7.5f,
                    FontStyle.Bold);

            _previousAlertIds =
                new HashSet<string>(
                    StringComparer.Ordinal);

            _snapshot =
                new IntegratedCautionWarningSnapshot();

            _latchedSeverity =
                IntegratedAlertSeverity.Normal;

            _acknowledged = false;
            _flashOn = true;

            _refreshTimer =
                new Timer
                {
                    Interval = 500
                };

            _refreshTimer.Tick +=
                OnRefreshTimerTick;

            _refreshTimer.Start();

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                _titleFont.Dispose();
                _statusFont.Dispose();
                _smallFont.Dispose();
            }

            base.Dispose(
                disposing);
        }

        protected override void OnMouseDown(
            MouseEventArgs e)
        {
            base.OnMouseDown(e);

            Focus();

            if (_ackBounds.Contains(
                    e.Location))
            {
                Acknowledge();
            }
        }

        protected override void OnKeyDown(
            KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.A)
            {
                Acknowledge();
                e.Handled = true;
            }
        }

        private void OnRefreshTimerTick(
            object sender,
            EventArgs e)
        {
            _flashOn =
                !_flashOn;

            AnalysisPipelineResult result;
            SpacecraftSystemsModel systems = null;

            if (EngineeringSnapshotStore.TryGetLatest(
                    out result) &&
                result != null &&
                result.Snapshot != null)
            {
                systems =
                    result.Snapshot.SpacecraftSystems;
            }

            IntegratedCautionWarningSnapshot next =
                IntegratedCautionWarningAnalyzer.Build(
                    systems);

            bool newAlarm =
                HasNewAlarm(
                    next);

            IntegratedAlertSeverity current =
                next != null
                    ? next.HighestSeverity
                    : IntegratedAlertSeverity.Advisory;

            if (current >=
                IntegratedAlertSeverity.Caution)
            {
                if (_latchedSeverity <
                    IntegratedAlertSeverity.Caution)
                {
                    _latchedSeverity =
                        current;

                    _acknowledged =
                        false;
                }
                else if (current >
                         _latchedSeverity)
                {
                    _latchedSeverity =
                        current;

                    _acknowledged =
                        false;
                }
                else if (_acknowledged &&
                         current <
                         _latchedSeverity)
                {
                    /*
                     * A previously acknowledged higher-severity condition has
                     * recovered while a lower-severity condition remains.
                     * Follow current truth without creating a false new alarm.
                     */
                    _latchedSeverity =
                        current;
                }

                if (newAlarm)
                {
                    _latchedSeverity =
                        MaxSeverity(
                            _latchedSeverity,
                            current);

                    _acknowledged =
                        false;

                    _flashOn =
                        true;
                }
            }
            else
            {
                /*
                 * No current caution/warning remains. Clear the local latch so
                 * RESET NOMINAL or an actual recovery returns the strip to
                 * current Engine truth without requiring an unrelated ACK.
                 */
                _latchedSeverity =
                    IntegratedAlertSeverity.Normal;

                _acknowledged =
                    false;
            }

            _snapshot =
                next ??
                new IntegratedCautionWarningSnapshot();

            CaptureAlertIds(
                _snapshot);

            Invalidate();
        }

        private bool HasNewAlarm(
            IntegratedCautionWarningSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            for (int index = 0;
                 index < snapshot.Alerts.Count;
                 index++)
            {
                IntegratedAlertItem item =
                    snapshot.Alerts[index];

                if (item == null ||
                    item.Severity <
                        IntegratedAlertSeverity.Caution)
                {
                    continue;
                }

                if (!_previousAlertIds.Contains(
                        item.AlertId))
                {
                    return true;
                }
            }

            return false;
        }

        private void CaptureAlertIds(
            IntegratedCautionWarningSnapshot snapshot)
        {
            _previousAlertIds.Clear();

            if (snapshot == null)
            {
                return;
            }

            for (int index = 0;
                 index < snapshot.Alerts.Count;
                 index++)
            {
                IntegratedAlertItem item =
                    snapshot.Alerts[index];

                if (item != null &&
                    !string.IsNullOrWhiteSpace(
                        item.AlertId))
                {
                    _previousAlertIds.Add(
                        item.AlertId);
                }
            }
        }

        private void Acknowledge()
        {
            if (_latchedSeverity >=
                IntegratedAlertSeverity.Caution)
            {
                _acknowledged =
                    true;

                if (_snapshot == null ||
                    _snapshot.HighestSeverity <
                        IntegratedAlertSeverity.Caution)
                {
                    _latchedSeverity =
                        IntegratedAlertSeverity.Normal;

                    _acknowledged =
                        false;
                }
            }

            Invalidate();
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics g =
                e.Graphics;

            Rectangle bounds =
                new Rectangle(
                    0,
                    0,
                    Math.Max(
                        1,
                        Width - 1),
                    Math.Max(
                        1,
                        Height - 1));

            IntegratedAlertSeverity current =
                _snapshot != null
                    ? _snapshot.HighestSeverity
                    : IntegratedAlertSeverity.Advisory;

            IntegratedAlertSeverity display =
                MaxSeverity(
                    current,
                    _latchedSeverity);

            Color accent =
                GetSeverityColor(
                    display);

            bool alarmVisible =
                _latchedSeverity <
                    IntegratedAlertSeverity.Caution ||
                _acknowledged ||
                _flashOn;

            using (SolidBrush background =
                new SolidBrush(
                    Color.FromArgb(
                        24,
                        29,
                        27)))
            using (Pen border =
                new Pen(
                    alarmVisible
                        ? accent
                        : Color.FromArgb(
                            70,
                            74,
                            70),
                    display >=
                        IntegratedAlertSeverity.Caution
                        ? 2.0f
                        : 1.0f))
            {
                g.FillRectangle(
                    background,
                    bounds);

                g.DrawRectangle(
                    border,
                    bounds);
            }

            int ackWidth =
                Math.Max(
                    74,
                    Math.Min(
                        102,
                        Width / 12));

            _ackBounds =
                new Rectangle(
                    Width - ackWidth - 8,
                    7,
                    ackWidth,
                    Math.Max(
                        24,
                        Height - 14));

            int subsystemWidth =
                Math.Max(
                    300,
                    Math.Min(
                        470,
                        Width / 3));

            Rectangle titleBounds =
                new Rectangle(
                    10,
                    4,
                    152,
                    16);

            Rectangle statusBounds =
                new Rectangle(
                    10,
                    20,
                    180,
                    Math.Max(
                        20,
                        Height - 24));

            Rectangle subsystemBounds =
                new Rectangle(
                    200,
                    8,
                    subsystemWidth,
                    Math.Max(
                        26,
                        Height - 16));

            Rectangle messageBounds =
                new Rectangle(
                    subsystemBounds.Right + 10,
                    6,
                    Math.Max(
                        40,
                        _ackBounds.Left -
                        subsystemBounds.Right -
                        18),
                    Math.Max(
                        30,
                        Height - 12));

            TextRenderer.DrawText(
                g,
                "INTEGRATED SYSTEMS C/W",
                _titleFont,
                titleBounds,
                Color.FromArgb(
                    170,
                    210,
                    200),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            string status =
                display.ToString().ToUpperInvariant();

            if (_latchedSeverity >=
                    IntegratedAlertSeverity.Caution &&
                _acknowledged)
            {
                status += " / ACK";
            }

            TextRenderer.DrawText(
                g,
                status,
                _statusFont,
                statusBounds,
                alarmVisible
                    ? accent
                    : Color.FromArgb(
                        80,
                        75,
                        60),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            DrawSubsystems(
                g,
                subsystemBounds);

            string message =
                _snapshot != null
                    ? _snapshot.Summary
                    : "ADVISORY / SYSTEMS SNAPSHOT UNAVAILABLE";

            TextRenderer.DrawText(
                g,
                message,
                _smallFont,
                messageBounds,
                Color.FromArgb(
                    190,
                    225,
                    205),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPadding);

            DrawAckButton(
                g,
                _ackBounds,
                _latchedSeverity >=
                    IntegratedAlertSeverity.Caution &&
                !_acknowledged);
        }

        private void DrawSubsystems(
            Graphics g,
            Rectangle bounds)
        {
            string[] subsystems =
                new[]
                {
                    "POWER",
                    "PROP",
                    "GNC",
                    "COMM",
                    "SYS",
                    "DATA"
                };

            int gap = 4;

            int width =
                Math.Max(
                    36,
                    (bounds.Width -
                     gap *
                     (subsystems.Length - 1)) /
                    subsystems.Length);

            for (int index = 0;
                 index < subsystems.Length;
                 index++)
            {
                string subsystem =
                    subsystems[index];

                Rectangle box =
                    new Rectangle(
                        bounds.Left +
                        index *
                        (width + gap),
                        bounds.Top,
                        width,
                        bounds.Height);

                IntegratedAlertSeverity subsystemSeverity =
                    _snapshot != null
                        ? _snapshot.GetSubsystemSeverity(
                            subsystem)
                        : IntegratedAlertSeverity.Normal;

                bool active =
                    subsystemSeverity >
                    IntegratedAlertSeverity.Normal;

                Color subsystemAccent =
                    GetSeverityColor(
                        subsystemSeverity);

                using (SolidBrush fill =
                    new SolidBrush(
                        active
                            ? Color.FromArgb(
                                56,
                                50,
                                38)
                            : Color.FromArgb(
                                34,
                                39,
                                36)))
                using (Pen outline =
                    new Pen(
                        active
                            ? subsystemAccent
                            : Color.FromArgb(
                                76,
                                84,
                                78),
                        active
                            ? 1.5f
                            : 1.0f))
                {
                    g.FillRectangle(
                        fill,
                        box);

                    g.DrawRectangle(
                        outline,
                        box);
                }

                TextRenderer.DrawText(
                    g,
                    subsystem,
                    _smallFont,
                    box,
                    active
                        ? subsystemAccent
                        : Color.FromArgb(
                            95,
                            110,
                            102),
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }
        }

        private void DrawAckButton(
            Graphics g,
            Rectangle bounds,
            bool active)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    active
                        ? Color.FromArgb(
                            88,
                            73,
                            28)
                        : Color.FromArgb(
                            42,
                            47,
                            43)))
            using (Pen border =
                new Pen(
                    active
                        ? Color.FromArgb(
                            238,
                            187,
                            64)
                        : Color.FromArgb(
                            100,
                            115,
                            106),
                    1.0f))
            {
                g.FillRectangle(
                    fill,
                    bounds);

                g.DrawRectangle(
                    border,
                    bounds);
            }

            TextRenderer.DrawText(
                g,
                "ACK",
                _smallFont,
                bounds,
                active
                    ? Color.FromArgb(
                        255,
                        220,
                        100)
                    : Color.FromArgb(
                        130,
                        150,
                        140),
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static IntegratedAlertSeverity MaxSeverity(
            IntegratedAlertSeverity first,
            IntegratedAlertSeverity second)
        {
            return
                first >= second
                    ? first
                    : second;
        }

        private static Color GetSeverityColor(
            IntegratedAlertSeverity severity)
        {
            switch (severity)
            {
                case IntegratedAlertSeverity.Warning:
                    return
                        Color.FromArgb(
                            220,
                            72,
                            50);

                case IntegratedAlertSeverity.Caution:
                    return
                        Color.FromArgb(
                            235,
                            170,
                            45);

                case IntegratedAlertSeverity.Advisory:
                    return
                        Color.FromArgb(
                            80,
                            150,
                            200);

                default:
                    return
                        Color.FromArgb(
                            105,
                            185,
                            105);
            }
        }
    }
}
