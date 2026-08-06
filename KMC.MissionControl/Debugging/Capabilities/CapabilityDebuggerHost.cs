using System;
using System.Windows.Forms;

namespace KMC.MissionControl.Debugging.Capabilities
{
    public sealed class CapabilityDebuggerHost :
        IDisposable
    {
        private readonly Form _owner;
        private CapabilityDebuggerForm _window;

        private CapabilityDebuggerHost(Form owner)
        {
            _owner = owner;
            _owner.KeyDown += OnOwnerKeyDown;
            _owner.FormClosed += OnOwnerFormClosed;
        }

        public static CapabilityDebuggerHost Attach(Form owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            return new CapabilityDebuggerHost(owner);
        }

        private void OnOwnerKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Control &&
                e.Shift &&
                e.KeyCode == Keys.F10)
            {
                ShowDebugger();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void ShowDebugger()
        {
            if (_window == null ||
                _window.IsDisposed)
            {
                _window = new CapabilityDebuggerForm();

                _window.FormClosed +=
                    delegate
                    {
                        _window = null;
                    };
            }

            if (!_window.Visible)
            {
                _window.Show(_owner);
            }

            _window.WindowState = FormWindowState.Normal;
            _window.BringToFront();
            _window.Activate();
            _window.RefreshSnapshot();
        }

        private void OnOwnerFormClosed(
            object sender,
            FormClosedEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            _owner.KeyDown -= OnOwnerKeyDown;
            _owner.FormClosed -= OnOwnerFormClosed;

            if (_window != null &&
                !_window.IsDisposed)
            {
                _window.Close();
            }

            _window = null;
        }
    }
}
