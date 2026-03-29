using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    public class StartupSplashForm : Form
    {
        private readonly Image _splashImage;
        private readonly AnimatedPillProgressBar _progressBar;
        private readonly Label _versionLabel;
        private readonly Font _versionFontNormal;
        private readonly Font _versionFontPulse;

        public StartupSplashForm(Rectangle targetBounds, string versionText)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Black;
            DoubleBuffered = true;

            string splashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Start.png");
            if (File.Exists(splashPath))
            {
                using (var img = Image.FromFile(splashPath))
                {
                    _splashImage = new Bitmap(img);
                }
            }

            var splashImageBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Image = _splashImage
            };
            Controls.Add(splashImageBox);

            _progressBar = new AnimatedPillProgressBar
            {
                BackColor = Color.Transparent
            };
            Controls.Add(_progressBar);
            _progressBar.BringToFront();

            _versionFontNormal = new Font("Segoe UI", 12f, FontStyle.Bold);
            _versionFontPulse = new Font("Segoe UI", 13f, FontStyle.Bold);
            _versionLabel = new Label
            {
                AutoSize = true,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(210, 225, 245),
                Font = _versionFontNormal,
                Text = string.IsNullOrWhiteSpace(versionText) ? "v1.0.3" : versionText
            };
            splashImageBox.Controls.Add(_versionLabel);
            _versionLabel.BringToFront();

            if (targetBounds.Width > 0 && targetBounds.Height > 0)
            {
                Bounds = targetBounds;
            }
            else
            {
                StartPosition = FormStartPosition.CenterScreen;
                if (_splashImage != null)
                {
                    ClientSize = _splashImage.Size;
                }
                else
                {
                    ClientSize = new Size(1200, 700);
                }
            }

            Resize += (s, e) => LayoutProgressBar();
            LayoutProgressBar();
            SetProgress(0);
        }

        public void SetProgress(double value)
        {
            SetProgress(value, Environment.TickCount);
        }

        public void SetProgress(double value, long elapsedMilliseconds)
        {
            _progressBar.SetAnimationState(value, elapsedMilliseconds);
            AnimateVersionLabel(elapsedMilliseconds);
        }

        private void LayoutProgressBar()
        {
            int barWidth = Math.Max(280, (int)(ClientSize.Width * 0.52));
            int barHeight = Math.Max(19, (int)(ClientSize.Height * 0.028) + 1);
            int x = (ClientSize.Width - barWidth) / 2;
            int y = (int)(ClientSize.Height * 0.505);

            _progressBar.Bounds = new Rectangle(x, y, barWidth, barHeight);
            _versionLabel.Location = new Point(20, Math.Max(10, ClientSize.Height - _versionLabel.Height - 16));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _splashImage?.Dispose();
                _versionFontNormal?.Dispose();
                _versionFontPulse?.Dispose();
            }

            base.Dispose(disposing);
        }

        private void AnimateVersionLabel(long elapsedMilliseconds)
        {
            // Soft pulse to blend with splash artwork
            double pulse = (Math.Sin(elapsedMilliseconds * 0.018) + 1.0) * 0.5;
            int alpha = 180 + (int)(75 * pulse);
            int r = 205 + (int)(30 * pulse);
            int g = 220 + (int)(22 * pulse);
            int b = 240 + (int)(15 * pulse);

            _versionLabel.ForeColor = Color.FromArgb(alpha, r, g, b);
            _versionLabel.Font = pulse > 0.57 ? _versionFontPulse : _versionFontNormal;
        }

        private sealed class AnimatedPillProgressBar : Control
        {
            private double _progress;
            private double _pulse;
            private double _finishBlink;

            public AnimatedPillProgressBar()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.UserPaint |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.SupportsTransparentBackColor,
                    true);
            }

            public void SetAnimationState(double progress, long elapsedMilliseconds)
            {
                _progress = Math.Max(0, Math.Min(1, progress));

                // Whole bar breathing pulse (vibrant but smooth)
                _pulse = (Math.Sin(elapsedMilliseconds * 0.015) + 1.0) * 0.5;

                // Orange blink near the end of loading
                const double blinkStart = 0.86;
                if (_progress >= blinkStart)
                {
                    double ramp = (_progress - blinkStart) / (1.0 - blinkStart);
                    double blinkWave = (Math.Sin(elapsedMilliseconds * 0.08) + 1.0) * 0.5;
                    _finishBlink = blinkWave * ramp;
                }
                else
                {
                    _finishBlink = 0;
                }

                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);

                if (Width <= 2 || Height <= 2)
                {
                    return;
                }

                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle trackRect = new Rectangle(0, 0, Width - 1, Height - 1);
                int cornerRadius = trackRect.Height;

                // Pulse glow around the full pill
                Rectangle glowRect = Rectangle.Inflate(trackRect, 4, 4);
                using (GraphicsPath glowPath = CreateRoundedPath(glowRect, glowRect.Height))
                using (SolidBrush glowBrush = new SolidBrush(Color.FromArgb(40 + (int)(60 * _pulse), 36, 178, 255)))
                {
                    e.Graphics.FillPath(glowBrush, glowPath);
                }

                // Base track (dark pill)
                using (GraphicsPath trackPath = CreateRoundedPath(trackRect, cornerRadius))
                using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(22, 34, 58)))
                {
                    e.Graphics.FillPath(trackBrush, trackPath);
                }

                int fillWidth = (int)Math.Round(trackRect.Width * _progress);
                if (fillWidth > 0)
                {
                    Rectangle fillRect = new Rectangle(trackRect.X, trackRect.Y, fillWidth, trackRect.Height);
                    int fillRadius = Math.Min(cornerRadius, fillRect.Height);

                    using (GraphicsPath fillPath = CreateRoundedPath(fillRect, fillRadius))
                    using (LinearGradientBrush fillBrush = new LinearGradientBrush(fillRect, Color.Empty, Color.Empty, LinearGradientMode.Horizontal))
                    {
                        ColorBlend blend = new ColorBlend
                        {
                            Positions = new[] { 0f, 0.55f, 1f },
                            Colors = new[]
                            {
                                Color.FromArgb(112, 225, 255),
                                Color.FromArgb(56, 191, 255),
                                Color.FromArgb(255, 143, 0)
                            }
                        };
                        fillBrush.InterpolationColors = blend;
                        e.Graphics.FillPath(fillBrush, fillPath);
                    }

                    // Extra lively shine pulse
                    int shineAlpha = 35 + (int)(95 * _pulse);
                    Rectangle shineRect = new Rectangle(fillRect.X, fillRect.Y, fillRect.Width, Math.Max(1, fillRect.Height / 2));
                    using (GraphicsPath shinePath = CreateRoundedPath(shineRect, shineRect.Height))
                    using (SolidBrush shineBrush = new SolidBrush(Color.FromArgb(shineAlpha, 255, 255, 255)))
                    {
                        e.Graphics.FillPath(shineBrush, shinePath);
                    }

                    // End blink flash in orange when opening is near ready
                    if (_finishBlink > 0.01)
                    {
                        int flashWidth = Math.Max(24, (int)(fillRect.Width * 0.24));
                        Rectangle flashRect = new Rectangle(fillRect.Right - flashWidth, fillRect.Y, flashWidth, fillRect.Height);
                        int flashAlpha = 50 + (int)(170 * _finishBlink);
                        using (GraphicsPath flashPath = CreateRoundedPath(flashRect, flashRect.Height))
                        using (SolidBrush flashBrush = new SolidBrush(Color.FromArgb(flashAlpha, 255, 165, 20)))
                        {
                            e.Graphics.FillPath(flashBrush, flashPath);
                        }
                    }
                }

                using (GraphicsPath borderPath = CreateRoundedPath(trackRect, cornerRadius))
                using (Pen borderPen = new Pen(Color.FromArgb(105, 130, 185)))
                {
                    e.Graphics.DrawPath(borderPen, borderPath);
                }
            }

            private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
            {
                int safeRadius = Math.Max(2, Math.Min(radius, Math.Min(rect.Width, rect.Height)));
                int diameter = safeRadius;
                int arc = diameter;

                GraphicsPath path = new GraphicsPath();
                path.StartFigure();
                path.AddArc(rect.X, rect.Y, arc, arc, 180, 90);
                path.AddArc(rect.Right - arc, rect.Y, arc, arc, 270, 90);
                path.AddArc(rect.Right - arc, rect.Bottom - arc, arc, arc, 0, 90);
                path.AddArc(rect.X, rect.Bottom - arc, arc, arc, 90, 90);
                path.CloseFigure();
                return path;
            }
        }
    }
}
