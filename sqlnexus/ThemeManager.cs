using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace sqlnexus
{

    public class Theme
    {
        public string Name { get; set; }
        public Color BackColor { get; set; }
        public Color ForeColor { get; set; }
        public Color OtherColor { get; set; }

        public override string ToString()
        {
            return Name; //this is to show name in combobox
        }
    }

    public static class ThemeManager
    {
        public static string CurrentThemeName;
        public static System.Drawing.Color CurrentForeColor;
        public static System.Drawing.Color CurrentBackColor;
        public static System.Drawing.Color CurrentOtherColor;

        /// <summary>
        /// Returns true if Windows High Contrast mode is enabled
        /// </summary>
        public static bool IsHighContrastEnabled => SystemInformation.HighContrast;

        #region Theme Definitions
        public static List<Theme> Themes = new List<Theme>
        {
            //only place for theme colors, if we ever need to change colors ,change here
            new Theme {
                        Name = "Default",
                        BackColor = Form.DefaultBackColor,
                        ForeColor = System.Drawing.Color.Black,
                        OtherColor = System.Drawing.ColorTranslator.FromHtml("#75E9FC"),
                       },
            new Theme {
                        Name = "Aquatic",
                        BackColor = System.Drawing.ColorTranslator.FromHtml("#202020"),
                        ForeColor = System.Drawing.ColorTranslator.FromHtml("#FFFFFF"),
                        OtherColor = System.Drawing.ColorTranslator.FromHtml("#75E9FC")
                       },
            new Theme {
                        Name = "Desert",
                        BackColor = System.Drawing.ColorTranslator.FromHtml("#FFFAEF"),
                        ForeColor = System.Drawing.ColorTranslator.FromHtml("#3D3D3D"),
                        OtherColor = System.Drawing.ColorTranslator.FromHtml("#1C5E75")
                       }
            //if we want to add more themes, add here with the preffered colors, this will automatically populate in the theme selection combobox
        };
        #endregion

        //recursive function to apply theme to all controls, call this function from main control/form
        static bool leftMenu = false;
        public static void ApplyTheme(Control control)
        {
            // When Windows High Contrast mode is enabled, use system colors for accessibility
            if (IsHighContrastEnabled)
            {
                ApplyHighContrastTheme(control);
                return;
            }
            #region special handling for default theme on left hand menu to keep original colors.
            if (CurrentThemeName == "Default" || leftMenu)
            {               
                if (control.Name == "tableLayoutPanel1")
                {
                    control.BackColor = Color.LightSkyBlue;
                    control.ForeColor = Color.Black;
                    leftMenu = true; // setting this for the iterations as we are in the hierarchy for the left hand menu
                }
                else
                {
                    if (control is LinkLabel)
                    {
                        if (control.Name == "llTasks" || control.Name == "llData" || control.Name == "llReports")
                        {
                            var linkLabel = (LinkLabel)control;
                            linkLabel.BackColor = Color.DarkBlue;
                            linkLabel.ForeColor = Color.White;
                            linkLabel.ActiveLinkColor = Color.White;
                            linkLabel.LinkColor = Color.White;
                            linkLabel.DisabledLinkColor = Color.Gray;
                            linkLabel.LinkBehavior = LinkBehavior.AlwaysUnderline;
                        }
                        else
                        {
                            var linkLabel = (LinkLabel)control;
                            linkLabel.BackColor = Color.AliceBlue;
                            linkLabel.ForeColor = Color.Black;
                            linkLabel.ActiveLinkColor = Color.DarkBlue;
                            linkLabel.LinkColor = Color.DarkBlue;
                            linkLabel.DisabledLinkColor = Color.DarkBlue;
                            linkLabel.LinkBehavior = LinkBehavior.AlwaysUnderline;
                        }
                    }
                    else
                    {
                        if (control.Name == "paReportsHeader" || control.Name == "paTasksHeader" || control.Name == "paDataHeader")
                        {
                            control.BackColor = Color.DarkBlue;
                            control.ForeColor = Color.White;
                        }
                        else
                        {
                            control.BackColor = Color.AliceBlue;
                            control.ForeColor = Color.Black;
                        }
                    }
                }
                
            }
            #endregion  
            else
            {
                control.ForeColor = ThemeManager.CurrentForeColor;
                control.BackColor = ThemeManager.CurrentBackColor;

                //adding special checks for control types as some properties are control specific
                if (control.GetType() == typeof(System.Windows.Forms.LinkLabel))
                {
                    ((LinkLabel)control).LinkColor = CurrentForeColor;
                    ((LinkLabel)control).ActiveLinkColor = CurrentForeColor;
                    ((LinkLabel)control).DisabledLinkColor = CurrentForeColor;
                    ((LinkLabel)control).LinkBehavior = LinkBehavior.AlwaysUnderline;
                }
                //this was not there on the original design but the differentiation was background colors , using this as border line to separate different panels
                if (control.GetType() == typeof(System.Windows.Forms.Panel))
                {
                    ((Panel)control).BorderStyle = BorderStyle.FixedSingle;
                }
            }

            if (control.HasChildren)
            {
                foreach (Control childControl in control.Controls)
                {
                    ApplyTheme(childControl);
                }
            }
            else
            {
                leftMenu = false; // reset the flag when we are done with the current branch of the control hierarchy
            }
        }

        /// <summary>
        /// Applies Windows High Contrast system colors to controls for accessibility compliance
        /// </summary>
        private static void ApplyHighContrastTheme(Control control)
        {
            control.ForeColor = SystemColors.WindowText;
            control.BackColor = SystemColors.Window;

            if (control.GetType() == typeof(System.Windows.Forms.LinkLabel))
            {
                var linkLabel = (LinkLabel)control;
                // Use ButtonHighlight for links on dark header panels, otherwise use HotTrack
                bool isOnDarkHeader = control.Parent != null &&
                    (control.Parent.Name.Contains("Header") ||
                     control.Parent.BackColor == Color.DarkBlue ||
                     control.Parent.BackColor == SystemColors.Highlight);

                if (isOnDarkHeader)
                {
                    linkLabel.LinkColor = SystemColors.ButtonHighlight;
                    linkLabel.ActiveLinkColor = SystemColors.ButtonHighlight;
                    linkLabel.DisabledLinkColor = SystemColors.GrayText;
                    linkLabel.LinkBehavior = LinkBehavior.AlwaysUnderline;
                }
                else
                {
                    linkLabel.LinkColor = SystemColors.HotTrack;
                    linkLabel.ActiveLinkColor = SystemColors.HotTrack;
                    linkLabel.DisabledLinkColor = SystemColors.GrayText;
                    linkLabel.LinkBehavior = LinkBehavior.AlwaysUnderline;
                }
            }

            if (control.GetType() == typeof(System.Windows.Forms.Panel))
            {
                var panel = (Panel)control;
                panel.BorderStyle = BorderStyle.FixedSingle;

                // Header panels should use system highlight colors
                if (control.Name.Contains("Header"))
                {
                    panel.BackColor = SystemColors.Highlight;
                }
            }

            if (control.GetType() == typeof(System.Windows.Forms.Button))
            {
                control.ForeColor = SystemColors.ControlText;
                control.BackColor = SystemColors.Control;
            }

            if (control.GetType() == typeof(System.Windows.Forms.TextBox) ||
                control.GetType() == typeof(System.Windows.Forms.ComboBox) ||
                control.GetType() == typeof(System.Windows.Forms.ListBox))
            {
                control.ForeColor = SystemColors.WindowText;
                control.BackColor = SystemColors.Window;
            }

            if (control.HasChildren)
            {
                foreach (Control childControl in control.Controls)
                {
                    ApplyHighContrastTheme(childControl);
                }
            }
        }

        //sets the current theme based on the theme name passed
        public static void ChangeCurrentTheme(string theme)
        {
            // If High Contrast is enabled, we'll use system colors regardless of selected theme
            if (IsHighContrastEnabled)
            {
                CurrentForeColor = SystemColors.WindowText;
                CurrentBackColor = SystemColors.Window;
                CurrentOtherColor = SystemColors.HotTrack;
                CurrentThemeName = "HighContrast";
                return;
            }

            var selectedTheme = Themes.FirstOrDefault(t => t.Name.Equals(theme));
            if (selectedTheme != null)
            {
                CurrentForeColor = selectedTheme.ForeColor;
                CurrentBackColor = selectedTheme.BackColor;
                CurrentOtherColor = selectedTheme.OtherColor;
                CurrentThemeName = selectedTheme.Name;
            }
            else
            {
                // Fallback to default theme if the theme name is wrong while calling this function // this should not happen normally as theme names are populated from the same list
                var defaultTheme = Themes.First(t => t.Name == "Default");
                CurrentForeColor = defaultTheme.ForeColor;
                CurrentBackColor = defaultTheme.BackColor;
                CurrentOtherColor = defaultTheme.OtherColor;
                CurrentThemeName = defaultTheme.Name;
            }
        }
    }
}
