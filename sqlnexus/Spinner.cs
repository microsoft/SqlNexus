using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace sqlnexus
{
    public partial class Spinner : Control
    {
        // Constants =========================================================
        private const int NUMBER_OF_DEGREES_CIRCLE = 360;
        private const int NUMBER_OF_DEGREES_HALF_CIRCLE = NUMBER_OF_DEGREES_CIRCLE / 2;
        private const int PERCENTAGE_OF_DARKEN = 10;
        private const int DEFAULT_INNER_CIRCLE_RADIUS = 8;
        private const int DEFAULT_OUTER_CIRCLE_RADIUS = 10;
        private const int DEFAULT_NUMBER_OF_SPOKE = 10;
        private const int DEFAULT_SPOKE_THICKNESS = 4;
        private Color DEFAULT_COLOR = Color.DarkGray;

        // Attributes ========================================================
        private Timer aTimer;
        private bool aTimerActive;
        private int aNumberOfSpoke;
        private int aSpokeThickness;
        private int aProgressValue;
        private int aOuterCircleRadius;
        private int aInnerCircleRadius;
        private PointF aCenterPoint;
        private Color aColor;
        private Color[] aColors;
        private double[] aAngles;

        // Properties ========================================================
        /// <summary>
        /// Gets or sets the lightest color of the circle.
        /// </summary>
        /// <value>The lightest color of the circle.</value>
        [TypeConverter("System.Drawing.ColorConverter"),
         Category("Spinner"),
         Description("Sets the color of spoke.")]
        public Color Color
        {
            get
            {
                return aColor;
            }
            set
            {
                aColor = value;

                GenerateColorsPallet();
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the outer circle radius.
        /// </summary>
        /// <value>The outer circle radius.</value>
        [System.ComponentModel.Description("Gets or sets the radius of outer circle."),
         System.ComponentModel.Category("Spinner")]
        public int OuterCircleRadius
        {
            get
            {
                if (aOuterCircleRadius == 0)
                    aOuterCircleRadius = DEFAULT_OUTER_CIRCLE_RADIUS;

                return aOuterCircleRadius;
            }
            set
            {
                aOuterCircleRadius = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the inner circle radius.
        /// </summary>
        /// <value>The inner circle radius.</value>
        [System.ComponentModel.Description("Gets or sets the radius of inner circle."),
         System.ComponentModel.Category("Spinner")]
        public int InnerCircleRadius
        {
            get
            {
                if (aInnerCircleRadius == 0)
                    aInnerCircleRadius = DEFAULT_INNER_CIRCLE_RADIUS;

                return aInnerCircleRadius;
            }
            set
            {
                aInnerCircleRadius = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the number of spoke.
        /// </summary>
        /// <value>The number of spoke.</value>
        [System.ComponentModel.Description("Gets or sets the number of spoke."),
        System.ComponentModel.Category("Spinner")]
        public int NumberSpoke
        {
            get
            {
                if (aNumberOfSpoke == 0)
                    aNumberOfSpoke = DEFAULT_NUMBER_OF_SPOKE;

                return aNumberOfSpoke;
            }
            set
            {
                if (aNumberOfSpoke != value && aNumberOfSpoke > 0)
                {
                    aNumberOfSpoke = value;
                    GenerateColorsPallet();
                    GetSpokesAngles();

                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="T:Spinner"/> is active.
        /// </summary>
        /// <value><c>true</c> if active; otherwise, <c>false</c>.</value>
        [System.ComponentModel.Description("Gets or sets the number of spoke."),
        System.ComponentModel.Category("Spinner")]
        public bool Active
        {
            get
            {
                return aTimerActive;
            }
            set
            {
                aTimerActive = value;
                ActiveTimer();
            }
        }

        /// <summary>
        /// Gets or sets the spoke thickness.
        /// </summary>
        /// <value>The spoke thickness.</value>
        [System.ComponentModel.Description("Gets or sets the thickness of a spoke."),
        System.ComponentModel.Category("Spinner")]
        public int SpokeThickness
        {
            get
            {
                if (aSpokeThickness <= 0)
                    aSpokeThickness = DEFAULT_SPOKE_THICKNESS;

                return aSpokeThickness;
            }
            set
            {
                aSpokeThickness = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the rotation speed.
        /// </summary>
        /// <value>The rotation speed.</value>
        [System.ComponentModel.Description("Gets or sets the rotation speed. Higher the slower."),
        System.ComponentModel.Category("Spinner")]
        public int RotationSpeed
        {
            get
            {
                return aTimer.Interval;
            }
            set
            {
                if (value > 0)
                    aTimer.Interval = value;
            }
        }

        // Construtor ========================================================
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Spinner"/> class.
        /// </summary>
        public Spinner()
        {
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.ResizeRedraw, true);

            aColor = DEFAULT_COLOR;

            GenerateColorsPallet();
            GetSpokesAngles();
            GetControlCenterPoint();

            aTimer = new Timer();
            aTimer.Tick += new EventHandler(aTimer_Tick);
            ActiveTimer();

            this.Resize += new EventHandler(Spinner_Resize);
        }

        // Events ============================================================
        /// <summary>
        /// Handles the Resize event of the Spinner control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event data.</param>
        void Spinner_Resize(object sender, EventArgs e)
        {
            GetControlCenterPoint();
        }

        /// <summary>
        /// Handles the Tick event of the aTimer control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="T:System.EventArgs"/> instance containing the event data.</param>
        void aTimer_Tick(object sender, EventArgs e)
        {
            aProgressValue = ++aProgressValue % aNumberOfSpoke;
            Invalidate();
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.Control.Paint"></see> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.Forms.PaintEventArgs"></see> that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (aNumberOfSpoke > 0)
            {
                e.Graphics.SmoothingMode = SmoothingMode.HighQuality;

                int intPosition = aProgressValue;
                for (int intCounter = 0; intCounter < aNumberOfSpoke; intCounter++)
                {
                    intPosition = intPosition % aNumberOfSpoke;
                    DrawLine(e.Graphics,
                             GetCoordinate(aCenterPoint, aInnerCircleRadius, aAngles[intPosition]),
                             GetCoordinate(aCenterPoint, aOuterCircleRadius, aAngles[intPosition]),
                             aColors[intCounter], aSpokeThickness);
                    intPosition++;
                }
            }

            base.OnPaint(e);
        }

        // Methods ===========================================================
        /// <summary>
        /// Darkens a specified color.
        /// </summary>
        /// <param name="_objColor">Color to darken.</param>
        /// <param name="_intPercent">The percent of darken.</param>
        /// <returns>The new color generated.</returns>
        private Color Darken(Color _objColor, int _intPercent)
        {
            int intRed = _objColor.R - (_intPercent * (_objColor.R / 100));
            int intGreen = _objColor.G - (_intPercent * (_objColor.G / 100));
            int intBlue = _objColor.B - (_intPercent * (_objColor.B / 100));
            return Color.FromArgb(Math.Min(intRed, byte.MaxValue), Math.Min(intGreen, byte.MaxValue), Math.Min(intBlue, byte.MaxValue));
        }

        /// <summary>
        /// Generates the colors pallet.
        /// </summary>
        private void GenerateColorsPallet()
        {
            aColors = GenerateColorsPallet(aColor, Active, (int)Math.Floor((double)aNumberOfSpoke / 3));
        }

        /// <summary>
        /// Generates the colors pallet.
        /// </summary>
        /// <param name="_objColor">Color of the lightest spoke.</param>
        /// <param name="_blnShadeColor">if set to <c>true</c> the color will be shaded on X spoke.</param>
        /// <returns>An array of color used to draw the circle.</returns>
        private Color[] GenerateColorsPallet(Color _objColor, bool _blnShadeColor, int _intNbSpoke)
        {
            Color[] objColors = new Color[NumberSpoke];

            for (int intCursor = 0; intCursor < NumberSpoke; intCursor++)
            {
                if (_blnShadeColor)
                    if (intCursor == 0 || intCursor < NumberSpoke - _intNbSpoke)
                        objColors[intCursor] = _objColor;
                    else
                        objColors[intCursor] = Darken(objColors[intCursor - 1], PERCENTAGE_OF_DARKEN);
                else
                    objColors[intCursor] = _objColor;
            }

            return objColors;
        }

        /// <summary>
        /// Gets the control center point.
        /// </summary>
        private void GetControlCenterPoint()
        {
            aCenterPoint = GetControlCenterPoint(this);
        }

        /// <summary>
        /// Gets the control center point.
        /// </summary>
        /// <returns>PointF object</returns>
        private PointF GetControlCenterPoint(Control _objControl)
        {
            return new PointF(_objControl.Width / 2f, _objControl.Height / 2f);
        }

        /// <summary>
        /// Draws the line with GDI+.
        /// </summary>
        /// <param name="_objGraphics">The Graphics object.</param>
        /// <param name="_objPointOne">The point one.</param>
        /// <param name="_objPointTwo">The point two.</param>
        /// <param name="_objColor">Color of the spoke.</param>
        /// <param name="_intLineThickness">The thickness of spoke.</param>
        private void DrawLine(Graphics _objGraphics, PointF _objPointOne, PointF _objPointTwo, Color _objColor, int _intLineThickness)
        {
            Pen objPen = new Pen(new SolidBrush(_objColor), _intLineThickness);
            objPen.StartCap = LineCap.Round;
            objPen.EndCap = LineCap.Round;

            _objGraphics.DrawLine(objPen, _objPointOne, _objPointTwo);
        }

        /// <summary>
        /// Gets the coordinate.
        /// </summary>
        /// <param name="_objCircleCenter">The Circle center.</param>
        /// <param name="_intRadius">The radius.</param>
        /// <param name="_dblAngle">The angle.</param>
        /// <returns></returns>
        private PointF GetCoordinate(PointF _objCircleCenter, int _intRadius, double _dblAngle)
        {
            PointF objPoint = new PointF();
            double dblAngle = Math.PI * _dblAngle / NUMBER_OF_DEGREES_HALF_CIRCLE;

            objPoint.X = _objCircleCenter.X + _intRadius * (float)Math.Cos(dblAngle);
            objPoint.Y = _objCircleCenter.Y + _intRadius * (float)Math.Sin(dblAngle);

            return objPoint;
        }


        /// <summary>
        /// Gets the spokes angles.
        /// </summary>
        private void GetSpokesAngles()
        {
            aAngles = GetSpokesAngles(NumberSpoke);
        }

        /// <summary>
        /// Gets the spoke angles.
        /// </summary>
        /// <param name="_shtNumberSpoke">The number spoke.</param>
        /// <returns>An array of angle.</returns>
        private double[] GetSpokesAngles(int _shtNumberSpoke)
        {
            double[] Angles = new double[_shtNumberSpoke];
            double dblAngle = (double)NUMBER_OF_DEGREES_CIRCLE / _shtNumberSpoke;

            for (int shtCounter = 0; shtCounter < _shtNumberSpoke; shtCounter++)
                Angles[shtCounter] = (shtCounter == 0 ? dblAngle : Angles[shtCounter - 1] + dblAngle);

            return Angles;
        }

        /// <summary>
        /// Actives the timer.
        /// </summary>
        private void ActiveTimer()
        {
            if (aTimerActive)
                aTimer.Start();
            else
            {
                aTimer.Stop();
                aProgressValue = 0;
            }

            GenerateColorsPallet();
            Invalidate();
        }
    }
}
