using System;
using System;
using System.Windows.Forms;

namespace sqlnexus
{
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
}
