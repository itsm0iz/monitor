using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StudyMonitor
{
    // The last 7 days as bars, with a dashed line at the daily goal.
    class BarChart : Control
    {
        static readonly Color Hit = Color.FromArgb(46, 158, 91);
        static readonly Color Half = Color.FromArgb(232, 163, 23);
        static readonly Color Low = Color.FromArgb(214, 84, 72);
        static readonly Color GoalLine = Color.FromArgb(150, 150, 150);

        double[] values = new double[0];
        string[] labels = new string[0];
        double goal;
        string key = "";

        public BarChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void SetData(double[] newValues, string[] newLabels, double newGoal)
        {
            // Only repaint when something visible changed (values are shown to the minute).
            string k = newGoal.ToString("0");
            foreach (double v in newValues) k += "|" + Math.Floor(v).ToString("0");
            foreach (string l in newLabels) k += "|" + l;
            if (k == key) return;
            key = k;
            values = newValues;
            labels = newLabels;
            goal = newGoal;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            int n = values.Length;
            if (n == 0) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int labelH = Font.Height + 2;
            float top = Font.Height / 2f;
            float bottom = Height - labelH;
            float h = bottom - top;
            double max = goal;
            foreach (double v in values) if (v > max) max = v;
            if (max <= 0) max = 60;

            float slot = Width / (float)n;
            float barW = slot * 0.56f;
            for (int i = 0; i < n; i++)
            {
                float bh = (float)(values[i] / max * h);
                if (values[i] > 0 && bh < 2) bh = 2;
                float x = i * slot + (slot - barW) / 2f;
                Color c = goal <= 0 || values[i] >= goal ? Hit : values[i] >= goal / 2 ? Half : Low;
                using (SolidBrush b = new SolidBrush(c)) g.FillRectangle(b, x, bottom - bh, barW, bh);
                Rectangle r = new Rectangle((int)(i * slot), (int)bottom + 1, (int)slot, labelH);
                TextRenderer.DrawText(g, labels[i], Font, r, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
            }

            if (goal > 0)
            {
                float y = bottom - (float)(goal / max * h);
                using (Pen p = new Pen(GoalLine, 1f))
                {
                    p.DashStyle = DashStyle.Dash;
                    g.DrawLine(p, 0, y, Width, y);
                }
            }
            using (Pen p = new Pen(GoalLine, 1f)) g.DrawLine(p, 0, bottom, Width, bottom);
        }
    }
}
