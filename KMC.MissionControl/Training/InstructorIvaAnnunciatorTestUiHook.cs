using System;
using System.Drawing;
using System.Windows.Forms;

namespace KMC.MissionControl.Training
{
    /// <summary>
    /// Build 14.18.3 Rev B — deterministic F10 IVA-test launcher attachment.
    ///
    /// The original 14.18.2 implementation relied only on Application.Idle.
    /// That proved timing-sensitive: the instructor form could open without
    /// receiving the IVA TESTS button.
    ///
    /// Rev B retains the existing non-invasive hook architecture but adds a
    /// short-lived WinForms startup timer.  The timer exists only while waiting
    /// for InstructorConsoleForm to become an open form and stops immediately
    /// after the button is attached.  There is no permanent periodic F10 timer.
    /// </summary>
    public static class InstructorIvaAnnunciatorTestUiHook
    {
        private static bool _installed;
        private static Timer _startupTimer;
        private static int _startupAttempts;

        private const int StartupIntervalMs = 100;
        private const int MaxStartupAttempts = 50;

        public static void EnsureInstalled()
        {
            if (_installed)
                return;

            _installed = true;

            // Keep the original idle path as a harmless secondary opportunity.
            Application.Idle += OnApplicationIdle;

            // Deterministic startup path: the hook is armed while the
            // InstructorConsoleForm constructor is populating its selectors.
            // The form may not yet be in Application.OpenForms at that instant.
            _startupAttempts = 0;
            _startupTimer = new Timer();
            _startupTimer.Interval = StartupIntervalMs;
            _startupTimer.Tick += OnStartupTimerTick;
            _startupTimer.Start();
        }

        private static void OnApplicationIdle(
            object sender,
            EventArgs e)
        {
            TryAttachToOpenInstructorForms();
        }

        private static void OnStartupTimerTick(
            object sender,
            EventArgs e)
        {
            _startupAttempts++;

            bool attached =
                TryAttachToOpenInstructorForms();

            if (attached ||
                _startupAttempts >= MaxStartupAttempts)
            {
                StopStartupTimer();
            }
        }

        private static bool TryAttachToOpenInstructorForms()
        {
            bool foundInstructor =
                false;

            // Copy through the collection by index on the UI thread.
            for (int index = 0;
                 index < Application.OpenForms.Count;
                 index++)
            {
                InstructorConsoleForm instructor =
                    Application.OpenForms[index]
                    as InstructorConsoleForm;

                if (instructor == null ||
                    instructor.IsDisposed)
                {
                    continue;
                }

                foundInstructor =
                    true;

                EnsureButton(
                    instructor);
            }

            return foundInstructor;
        }

        private static void EnsureButton(
            InstructorConsoleForm form)
        {
            if (form == null ||
                form.IsDisposed ||
                FindButton(
                    form,
                    "IVA TESTS") != null)
            {
                return;
            }

            Button refresh =
                FindButton(
                    form,
                    "REFRESH");

            if (refresh == null)
                return;

            TableLayoutPanel actions =
                refresh.Parent
                as TableLayoutPanel;

            if (actions == null)
                return;

            // Frozen F10 action row has four controls.  Expand it to five
            // equal columns and append IVA TESTS. Existing button handlers
            // and controls are not replaced.
            actions.SuspendLayout();

            try
            {
                actions.ColumnCount =
                    5;

                actions.ColumnStyles.Clear();

                for (int index = 0;
                     index < 5;
                     index++)
                {
                    actions.ColumnStyles.Add(
                        new ColumnStyle(
                            SizeType.Percent,
                            20.0f));
                }

                Button button =
                    new Button
                    {
                        Text =
                            "IVA TESTS",
                        Dock =
                            DockStyle.Fill,
                        Margin =
                            new Padding(5),
                        FlatStyle =
                            FlatStyle.Flat,
                        BackColor =
                            refresh.BackColor,
                        ForeColor =
                            refresh.ForeColor,
                        Font =
                            refresh.Font,
                        TabStop =
                            false
                    };

                button.FlatAppearance.BorderColor =
                    refresh.FlatAppearance.BorderColor;

                button.FlatAppearance.MouseOverBackColor =
                    refresh.FlatAppearance.MouseOverBackColor;

                button.Click +=
                    delegate
                    {
                        using (
                            InstructorIvaAnnunciatorTestForm
                                testForm =
                                    new InstructorIvaAnnunciatorTestForm())
                        {
                            testForm.ShowDialog(
                                form);
                        }
                    };

                actions.Controls.Add(
                    button,
                    4,
                    0);
            }
            finally
            {
                actions.ResumeLayout(
                    true);
            }
        }

        private static void StopStartupTimer()
        {
            if (_startupTimer == null)
                return;

            _startupTimer.Stop();
            _startupTimer.Tick -=
                OnStartupTimerTick;
            _startupTimer.Dispose();
            _startupTimer =
                null;
        }

        private static Button FindButton(
            Control parent,
            string text)
        {
            if (parent == null)
                return null;

            Button button =
                parent as Button;

            if (button != null &&
                string.Equals(
                    button.Text,
                    text,
                    StringComparison.Ordinal))
            {
                return button;
            }

            foreach (Control child
                     in parent.Controls)
            {
                Button found =
                    FindButton(
                        child,
                        text);

                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
