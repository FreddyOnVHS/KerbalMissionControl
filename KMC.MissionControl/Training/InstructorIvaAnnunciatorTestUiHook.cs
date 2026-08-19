using System;
using System.Drawing;
using System.Windows.Forms;

namespace KMC.MissionControl.Training
{
    public static class InstructorIvaAnnunciatorTestUiHook
    {
        private static bool _installed;

        public static void EnsureInstalled()
        {
            if (_installed) return;
            _installed = true;
            Application.Idle += OnApplicationIdle;
        }

        private static void OnApplicationIdle(object sender, EventArgs e)
        {
            foreach (Form form in Application.OpenForms)
            {
                InstructorConsoleForm instructor = form as InstructorConsoleForm;
                if (instructor != null) EnsureButton(instructor);
            }
        }

        private static void EnsureButton(InstructorConsoleForm form)
        {
            if (FindButton(form, "IVA TESTS") != null) return;
            Button refresh = FindButton(form, "REFRESH");
            if (refresh == null) return;

            TableLayoutPanel actions = refresh.Parent as TableLayoutPanel;
            if (actions == null) return;

            actions.ColumnCount = 5;
            actions.ColumnStyles.Clear();
            for (int i = 0; i < 5; i++)
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20.0f));

            Button button = new Button
            {
                Text = "IVA TESTS",
                Dock = DockStyle.Fill,
                Margin = new Padding(5),
                FlatStyle = FlatStyle.Flat,
                BackColor = refresh.BackColor,
                ForeColor = refresh.ForeColor,
                Font = refresh.Font,
                TabStop = false
            };
            button.FlatAppearance.BorderColor = refresh.FlatAppearance.BorderColor;
            button.FlatAppearance.MouseOverBackColor = refresh.FlatAppearance.MouseOverBackColor;
            button.Click += delegate
            {
                using (InstructorIvaAnnunciatorTestForm testForm = new InstructorIvaAnnunciatorTestForm())
                    testForm.ShowDialog(form);
            };
            actions.Controls.Add(button, 4, 0);
        }

        private static Button FindButton(Control parent, string text)
        {
            if (parent == null) return null;
            Button button = parent as Button;
            if (button != null && string.Equals(button.Text, text, StringComparison.Ordinal)) return button;
            foreach (Control child in parent.Controls)
            {
                Button found = FindButton(child, text);
                if (found != null) return found;
            }
            return null;
        }
    }
}
