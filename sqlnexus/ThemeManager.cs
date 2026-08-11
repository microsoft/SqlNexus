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

    /// <summary>
    /// A flat, Fluent-inspired color table used to render menus and toolbars without
    /// the legacy glossy gradients. Provides a subtle light-gray surface with a modern
    /// blue accent for hover/selection.
    /// </summary>
    public class ModernColorTable : ProfessionalColorTable
    {
        private static readonly Color Surface = ColorTranslator.FromHtml("#F3F3F3");
        private static readonly Color Accent = ColorTranslator.FromHtml("#0078D4");
        private static readonly Color AccentLight = ColorTranslator.FromHtml("#CCE4F7");
        private static readonly Color AccentBorder = ColorTranslator.FromHtml("#99CAEF");
        private static readonly Color Divider = ColorTranslator.FromHtml("#E0E0E0");

        public override Color ToolStripGradientBegin => Surface;
        public override Color ToolStripGradientMiddle => Surface;
        public override Color ToolStripGradientEnd => Surface;
        public override Color ToolStripBorder => Divider;

        public override Color MenuStripGradientBegin => Surface;
        public override Color MenuStripGradientEnd => Surface;
        public override Color MenuBorder => Divider;
        public override Color MenuItemBorder => AccentBorder;

        public override Color MenuItemSelected => AccentLight;
        public override Color MenuItemSelectedGradientBegin => AccentLight;
        public override Color MenuItemSelectedGradientEnd => AccentLight;
        public override Color MenuItemPressedGradientBegin => Surface;
        public override Color MenuItemPressedGradientEnd => Surface;

        public override Color ButtonSelectedHighlight => AccentLight;
        public override Color ButtonSelectedGradientBegin => AccentLight;
        public override Color ButtonSelectedGradientMiddle => AccentLight;
        public override Color ButtonSelectedGradientEnd => AccentLight;
        public override Color ButtonSelectedBorder => AccentBorder;

        public override Color ButtonPressedGradientBegin => AccentBorder;
        public override Color ButtonPressedGradientMiddle => AccentBorder;
        public override Color ButtonPressedGradientEnd => AccentBorder;
        public override Color ButtonPressedBorder => Accent;

        public override Color ButtonCheckedGradientBegin => AccentLight;
        public override Color ButtonCheckedGradientMiddle => AccentLight;
        public override Color ButtonCheckedGradientEnd => AccentLight;

        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;

        public override Color SeparatorDark => Divider;
        public override Color SeparatorLight => Color.White;

        public override Color ToolStripContentPanelGradientBegin => Surface;
        public override Color ToolStripContentPanelGradientEnd => Surface;
        public override Color ToolStripPanelGradientBegin => Surface;
        public override Color ToolStripPanelGradientEnd => Surface;
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

        #region Modern Fluent palette (shared by the refreshed Default nav)
        // Central place for the modern accent colors so the sidebar matches the flat
        // toolbars/menus rendered by ModernColorTable.
        internal static readonly Color ModernSurface = ColorTranslator.FromHtml("#F3F3F3"); // nav column
        internal static readonly Color ModernBody = ColorTranslator.FromHtml("#FFFFFF");    // body panels
        internal static readonly Color ModernAccent = ColorTranslator.FromHtml("#0078D4");  // headers / links
        internal static readonly Color ModernDivider = ColorTranslator.FromHtml("#E0E0E0"); // panel borders
        internal static readonly Color ModernText = ColorTranslator.FromHtml("#1B1B1B");    // primary text
        internal static readonly Color ModernButtonBorder = ColorTranslator.FromHtml("#8A8886"); // flat button outline
        internal static readonly Color SidebarBorder = ColorTranslator.FromHtml("#3B3B3B");   // dark gray box outline
        #endregion

        /// <summary>
        /// Border color for the sidebar container boxes and header strips. Uses a dark
        /// gray for strong, accessible contrast against the light surroundings.
        /// </summary>
        public static Color SidebarBorderColor =>
            IsHighContrastEnabled ? SystemColors.WindowText : SidebarBorder;


        /// <summary>
        /// The modern Windows UI font (Segoe UI). Replacing the legacy default of
        /// Microsoft Sans Serif 8.25pt instantly gives the whole app a current look
        /// because child controls inherit the container font.
        /// </summary>
        public static Font GetModernFont()
        {
            try
            {
                return new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch
            {
                // Fall back to the system default if Segoe UI is unavailable.
                return Control.DefaultFont;
            }
        }

        /// <summary>
        /// Applies modern, low-risk cosmetic polish to a form: the Segoe UI system font
        /// and a flat (non-glossy) renderer for its menus and toolbars. No layout changes.
        /// </summary>
        public static void ApplyModernAppearance(Form form)
        {
            if (form == null)
                return;

            // 1. Modern font. High Contrast users keep the system-provided font.
            if (!IsHighContrastEnabled)
            {
                form.Font = GetModernFont();
            }

            // 3. Flat, Fluent-style menus and toolbars instead of the dated 2007 gradients.
            ToolStripManager.Renderer = new ToolStripProfessionalRenderer(new ModernColorTable())
            {
                RoundedEdges = false
            };
        }


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
                       },
            new Theme {
                        Name = "Fluent Light",
                        BackColor = System.Drawing.ColorTranslator.FromHtml("#FFFFFF"),
                        ForeColor = System.Drawing.ColorTranslator.FromHtml("#1B1B1B"),
                        OtherColor = System.Drawing.ColorTranslator.FromHtml("#0078D4")
                       },
            new Theme {
                        Name = "Fluent Dark",
                        BackColor = System.Drawing.ColorTranslator.FromHtml("#1F1F1F"),
                        ForeColor = System.Drawing.ColorTranslator.FromHtml("#F3F3F3"),
                        OtherColor = System.Drawing.ColorTranslator.FromHtml("#4CC2FF")
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
                    control.BackColor = ModernSurface;
                    control.ForeColor = ModernText;
                    leftMenu = true; // setting this for the iterations as we are in the hierarchy for the left hand menu
                }
                else
                {
                    if (control is LinkLabel)
                    {
                        if (control.Name == "llTasks" || control.Name == "llData" || control.Name == "llReports")
                        {
                            var linkLabel = (LinkLabel)control;
                            linkLabel.BackColor = ModernAccent;
                            linkLabel.ForeColor = Color.White;
                            linkLabel.ActiveLinkColor = Color.White;
                            linkLabel.LinkColor = Color.White;
                            linkLabel.DisabledLinkColor = Color.Gainsboro;
                            linkLabel.LinkBehavior = LinkBehavior.AlwaysUnderline;
                        }
                        else
                        {
                            var linkLabel = (LinkLabel)control;
                            linkLabel.BackColor = ModernBody;
                            linkLabel.ForeColor = ModernText;
                            linkLabel.ActiveLinkColor = ModernAccent;
                            linkLabel.LinkColor = ModernAccent;
                            linkLabel.DisabledLinkColor = ModernAccent;
                            linkLabel.LinkBehavior = LinkBehavior.AlwaysUnderline;
                        }
                    }
                    else
                    {
                        if (control.Name == "paReportsHeader" || control.Name == "paTasksHeader" || control.Name == "paDataHeader")
                        {
                            control.BackColor = ModernAccent;
                            control.ForeColor = Color.White;
                        }
                        else
                        {
                            control.BackColor = ModernBody;
                            control.ForeColor = ModernText;
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

            // 5. Cross-theme polish: give buttons a flat, modern look instead of the
            // raised 3D chrome, but keep a subtle border so they remain clearly visible.
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = IsHighContrastEnabled
                    ? SystemColors.WindowText
                    : ModernButtonBorder;
                button.UseVisualStyleBackColor = false;
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
