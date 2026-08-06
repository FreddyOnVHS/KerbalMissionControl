using System;
using System.Windows.Forms;

namespace KMC.MissionControl.Debugging.Electrical
{
    public sealed class ElectricalTopologyDebuggerHost :
        IDisposable
    {
        private readonly Form _owner;
        private ElectricalTopologyDebuggerForm _window;

        private ElectricalTopologyDebuggerHost(
            Form owner)
        {
            _owner =
                owner;

            _owner.KeyDown +=
                OnOwnerKeyDown;

            _owner.FormClosed +=
                OnOwnerFormClosed;
        }

        public static ElectricalTopologyDebuggerHost
            Attach(
                Form owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(
                    nameof(owner));
            }

            return
                new ElectricalTopologyDebuggerHost(
                    owner);
        }

        private void OnOwnerKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Control &&
                e.Shift &&
                e.KeyCode ==
                    Keys.F11)
            {
                ShowDebugger();

                e.Handled =
                    true;

                e.SuppressKeyPress =
                    true;
            }
        }

        private void ShowDebugger()
        {
            if (_window == null ||
                _window.IsDisposed)
            {
                _window =
                    new ElectricalTopologyDebuggerForm();

                _window.FormClosed +=
                    delegate
                    {
                        _window =
                            null;
                    };
            }

            if (!_window.Visible)
            {
                _window.Show(
                    _owner);
            }

            _window.WindowState =
                FormWindowState.Normal;

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
            _owner.KeyDown -=
                OnOwnerKeyDown;

            _owner.FormClosed -=
                OnOwnerFormClosed;

            if (_window != null &&
                !_window.IsDisposed)
            {
                _window.Close();
            }

            _window =
                null;
        }
    }
}
