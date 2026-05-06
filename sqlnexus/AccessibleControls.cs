using System;
using System.Windows.Forms;
using System.Windows.Forms.Automation;

namespace sqlnexus
{
    /// <summary>
    /// Accessible control subclasses for WCAG compliance in WinForms.
    /// These controls address limitations in standard WinForms controls
    /// that prevent screen readers and accessibility tools from fully
    /// interacting with the UI.
    /// </summary>

    /// <summary>
    /// A TextBox subclass that supports the UIA Text pattern for accessibility compliance.
    /// Standard single-line WinForms TextBox controls expose ControlType.Edit but only support
    /// the Value pattern. The UIA spec (Section 508 502.3.10, WCAG 4.1.2) requires Edit controls
    /// to also support the Text pattern. Setting Multiline=true enables the Text pattern in the
    /// underlying Win32 edit control while the visual appearance and behavior remain single-line.
    /// </summary>
    public class AccessibleTextBox : TextBox
    {
        public AccessibleTextBox()
        {
            Multiline = true;
            WordWrap = false;
            AcceptsReturn = false;
            ScrollBars = ScrollBars.None;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Prevent Enter key from inserting a newline
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            // Strip any newlines that may have been pasted
            if (Text.Contains("\n") || Text.Contains("\r"))
            {
                int selStart = SelectionStart;
                Text = Text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
                SelectionStart = Math.Min(selStart, Text.Length);
            }
        }
    }

    /// <summary>
    /// A RadioButton subclass that announces positional information (e.g., "1 of 3")
    /// to screen readers. Standard WinForms RadioButton does not expose UIA
    /// PositionInSet/SizeOfSet properties, so Narrator omits positional announcements.
    /// This subclass updates AccessibleDescription with position info when the control
    /// receives focus or is selected via arrow keys.
    /// </summary>
    public class AccessibleRadioButton : RadioButton
    {
        protected override void OnGotFocus(EventArgs e)
        {
            UpdateAccessibleDescription();
            base.OnGotFocus(e);
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            if (Checked)
            {
                UpdateAccessibleDescription();
                // Narrator doesn't re-read properties on arrow-key radio button
                // changes since no UIA focus event fires. Explicitly raise a
                // notification so the screen reader announces the newly selected item.
                string announcement = string.Format("{0}, {1}", Text, AccessibleDescription);
                AccessibilityObject.RaiseAutomationNotification(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    announcement);
            }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            UpdateAccessibleDescription();
        }

        private void UpdateAccessibleDescription()
        {
            if (Parent == null)
                return;

            // Use TabIndex to determine position since Controls collection
            // order may not match the visual left-to-right layout
            int total = 0;
            int position = 0;

            foreach (Control c in Parent.Controls)
            {
                if (c is RadioButton rb)
                {
                    total++;
                    if (rb == this)
                        position = rb.TabIndex + 1;
                }
            }

            if (position > 0 && total > 0)
            {
                AccessibleDescription = string.Format("{0} of {1}", position, total);
            }
        }
    }
}
