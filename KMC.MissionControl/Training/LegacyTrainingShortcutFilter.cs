using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace KMC.MissionControl.Training
{
    /// <summary>
    /// Build 14.11.1A retires the scattered Ctrl+Shift failure-injection
    /// shortcuts without requiring a risky rewrite of MainForm.
    ///
    /// F10 is now the authoritative instructor/test surface. The performance
    /// overlay shortcut Ctrl+Shift+D is intentionally not blocked because it
    /// does not mutate spacecraft or failure state.
    /// </summary>
    internal sealed class LegacyTrainingShortcutFilter :
        IMessageFilter
    {
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;

        public bool PreFilterMessage(
            ref Message m)
        {
            if (m.Msg != WmKeyDown &&
                m.Msg != WmSysKeyDown)
            {
                return false;
            }

            Keys modifiers =
                Control.ModifierKeys;

            if ((modifiers & Keys.Control) != Keys.Control ||
                (modifiers & Keys.Shift) != Keys.Shift)
            {
                return false;
            }

            Keys key =
                (Keys)m.WParam.ToInt32();

            switch (key)
            {
                case Keys.E:
                case Keys.P:
                case Keys.K:
                case Keys.G:
                case Keys.C:
                    Debug.WriteLine(
                        "KMC.MissionControl LEGACY TEST SHORTCUT BLOCKED" +
                        " | Ctrl+Shift+" +
                        key.ToString() +
                        " | Use F10 Instructor / Scenario Control.");

                    return true;

                default:
                    return false;
            }
        }
    }
}
