// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        PomodoroTimer.cs                                             ║
// ║  PURPOSE:     POMODORO TECHNIQUE TIMER (25/5/15 CYCLES)                    ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Work_Time_Counter
{
    public class PomodoroPhaseEventArgs : EventArgs
    {
        public string PhaseName { get; set; }
        public int SessionNumber { get; set; }
        public int TotalSessions { get; set; }
    }

    public class PomodoroPanel : Panel
    {
        private const int WORK_MIN = 25;
        private const int SHORT_BREAK = 5;
        private const int LONG_BREAK = 15;
        private const int SESSIONS = 4;

        private Timer _timer;
        private int _totalSeconds;
        private int _remainingSeconds;
        private bool _isRunning;
        private int _workMin = WORK_MIN, _shortBreak = SHORT_BREAK, _longBreak = LONG_BREAK, _sessions = SESSIONS;
        private int _currentSession = 1;
        private bool _isWorkSession = true;
        private bool _isDarkMode;

        // Colors
        private Color _bgColor, _fgColor, _workColor, _breakColor, _ringBg, _btnBg, _btnHover;

        public event EventHandler<PomodoroPhaseEventArgs> PhaseChanged;

        /// <summary>
        /// Initialize the Pomodoro timer panel with work/break durations and UI styling.
        /// </summary>
        public PomodoroPanel(bool isDarkMode)
        {
//             DebugLogger.Log("[Pomodoro] PomodoroPanel constructor called");

            _isDarkMode = isDarkMode;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw, true);
            ApplyColors();
            this.Size = new Size(280, 340);
            this.Padding = new Padding(10);
            this.Cursor = Cursors.Default;

            // Initialize 1-second timer for countdown
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += (s, e) =>
            {
                if (_isRunning && _remainingSeconds > 0)
                {
                    _remainingSeconds--;
                    this.Invalidate();

                    if (_remainingSeconds == 0)
                    {
//                         DebugLogger.Log("[Pomodoro] Timer expired, advancing phase");
                        AdvancePhase();
                    }
                }
            };

            _totalSeconds = _workMin * 60;
            _remainingSeconds = _totalSeconds;
//             DebugLogger.Log($"[Pomodoro] Initialized: work={_workMin}min, shortBreak={_shortBreak}min, longBreak={_longBreak}min, sessions={_sessions}");

            this.MouseDown += PomodoroPanel_MouseDown;
        }

        private void ApplyColors()
        {
            if (_isDarkMode)
            {
                _bgColor = Color.FromArgb(22, 27, 38);
                _fgColor = Color.FromArgb(230, 235, 245);
                _ringBg = Color.FromArgb(42, 48, 62);
                _btnBg = Color.FromArgb(38, 44, 58);
                _btnHover = Color.FromArgb(52, 58, 72);
            }
            else
            {
                _bgColor = Color.FromArgb(255, 255, 255);
                _fgColor = Color.FromArgb(30, 35, 50);
                _ringBg = Color.FromArgb(230, 233, 240);
                _btnBg = Color.FromArgb(240, 242, 248);
                _btnHover = Color.FromArgb(220, 225, 235);
            }
            _workColor = Color.FromArgb(239, 68, 68);   // red
            _breakColor = Color.FromArgb(34, 197, 94);   // green
            this.BackColor = _bgColor;
        }

        /// <summary>
        /// Apply a theme (light or dark) to the timer display.
        /// </summary>
        public void ApplyTheme(bool isDarkMode)
        {
//             DebugLogger.Log($"[Pomodoro] ApplyTheme({(isDarkMode ? "dark" : "light")})");
            _isDarkMode = isDarkMode;
            ApplyColors();
            this.Invalidate();
        }

        // ── Hit-test buttons via mouse click on painted regions ──
        private RectangleF _btnStartRect, _btnPauseRect, _btnResetRect, _btnSkipRect, _btnCloseRect;

        /// <summary>
        /// Handle mouse clicks on painted button regions.
        /// Start, Pause, Reset, Skip, and Close buttons.
        /// </summary>
        private void PomodoroPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (_btnCloseRect.Contains(e.Location))
            {
//                 DebugLogger.Log("[Pomodoro] Close button clicked");
                this.Visible = false;
                return;
            }
            if (_btnStartRect.Contains(e.Location) && !_isRunning)
            {
//                 DebugLogger.Log("[Pomodoro] Start button clicked");
                Start();
            }
            else if (_btnPauseRect.Contains(e.Location) && _isRunning)
            {
//                 DebugLogger.Log("[Pomodoro] Pause button clicked");
                Pause();
            }
            else if (_btnResetRect.Contains(e.Location))
            {
//                 DebugLogger.Log("[Pomodoro] Reset button clicked");
                Reset();
            }
            else if (_btnSkipRect.Contains(e.Location))
            {
//                 DebugLogger.Log("[Pomodoro] Skip button clicked");
                Skip();
            }
        }

        /// <summary>
        /// Start the timer countdown.
        /// </summary>
        public void Start()
        {
//             DebugLogger.Log($"[Pomodoro] Start() - session {_currentSession}/{_sessions}, phase={(_isWorkSession ? "work" : "break")}");
            _isRunning = true;
            _timer.Start();
            Invalidate();
        }

        /// <summary>
        /// Pause the timer countdown.
        /// </summary>
        public void Pause()
        {
//             DebugLogger.Log($"[Pomodoro] Pause() - {_remainingSeconds}s remaining");
            _isRunning = false;
            _timer.Stop();
            Invalidate();
        }

        /// <summary>
        /// Reset to first session and work phase.
        /// </summary>
        public void Reset()
        {
//             DebugLogger.Log("[Pomodoro] Reset() - returning to session 1, work phase");
            _isRunning = false;
            _timer.Stop();
            _currentSession = 1;
            _isWorkSession = true;
            _totalSeconds = _workMin * 60;
            _remainingSeconds = _totalSeconds;
            Invalidate();
        }

        /// <summary>
        /// Skip current phase and advance to next.
        /// </summary>
        public void Skip()
        {
//             DebugLogger.Log($"[Pomodoro] Skip() - advancing from {(_isWorkSession ? "work" : "break")} phase");
            _isRunning = false;
            _timer.Stop();
            AdvancePhase();
        }

        /// <summary>
        /// Advance to the next phase (work -> break or break -> work).
        /// Handles session counter and determines break length (short vs long).
        /// Triggers sound alert and PhaseChanged event.
        /// </summary>
        private void AdvancePhase()
        {
            _isRunning = false;

            if (_isWorkSession)
            {
                // Transitioning from work to break
                bool isLongBreak = (_currentSession % _sessions == 0);
                _totalSeconds = isLongBreak ? (_longBreak * 60) : (_shortBreak * 60);
                _isWorkSession = false;
//                 DebugLogger.Log($"[Pomodoro] Work phase complete, advancing to {(isLongBreak ? "LONG" : "SHORT")} break ({_totalSeconds}s)");
            }
            else
            {
                // Transitioning from break to work
                _currentSession++;
                _totalSeconds = _workMin * 60;
                _isWorkSession = true;
//                 DebugLogger.Log($"[Pomodoro] Break complete, advancing to work session {_currentSession}/{_sessions} ({_totalSeconds}s)");
            }

            _remainingSeconds = _totalSeconds;

            // Play notification sound
            try
            {
                SoundManager.PlayPingAlert();
//                 DebugLogger.Log("[Pomodoro] Sound alert played");
            }
            catch (Exception ex)
            {
                 DebugLogger.Log($"[Pomodoro] WARNING: Could not play sound - {ex.Message}");
            }

            // Notify subscribers of phase change
            PhaseChanged?.Invoke(this, new PomodoroPhaseEventArgs
            {
                PhaseName = _isWorkSession ? "Work" : "Break",
                SessionNumber = _currentSession,
                TotalSessions = _sessions
            });

            Invalidate();
        }

        // ════════════════════════════════════════════════════
        //  PAINT — everything is drawn, no child controls
        // ════════════════════════════════════════════════════
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = this.Width, h = this.Height;

            // ── Background with subtle rounded border ──
            using (var bgBrush = new SolidBrush(_bgColor))
                g.FillRectangle(bgBrush, 0, 0, w, h);

            Color borderColor = _isDarkMode ? Color.FromArgb(55, 65, 85) : Color.FromArgb(200, 205, 215);
            using (var pen = new Pen(borderColor, 1.5f))
                DrawRoundedRect(g, pen, new Rectangle(0, 0, w - 1, h - 1), 12);

            // ── Close button (top-right X) ──
            _btnCloseRect = new RectangleF(w - 30, 6, 22, 22);
            using (var closeFg = new Pen(_isDarkMode ? Color.FromArgb(160, 170, 190) : Color.FromArgb(120, 130, 150), 2f))
            {
                g.DrawLine(closeFg, w - 26, 10, w - 12, 24);
                g.DrawLine(closeFg, w - 12, 10, w - 26, 24);
            }

            // ── Title / status ──
            string statusText = _isWorkSession
                ? $"🍅  Work ({_currentSession}/{_sessions})"
                : (_currentSession % _sessions == 0 ? "☕  Long Break" : "☕  Short Break");

            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            using (var titleFont = new System.Drawing.Font("Segoe UI", 11f, FontStyle.Bold))
            {
                g.DrawString(statusText, titleFont, new SolidBrush(_fgColor), new RectangleF(0, 12, w, 26), sf);
            }

            // ── Circular progress ring ──
            int ringSize = Math.Min(w, h) - 100;
            if (ringSize < 100) ringSize = 100;
            int ringThickness = 10;
            int cx = w / 2, cy = 48 + ringSize / 2 + 10;
            Rectangle ringRect = new Rectangle(cx - ringSize / 2, cy - ringSize / 2, ringSize, ringSize);

            // Ring background track
            using (var trackPen = new Pen(_ringBg, ringThickness))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                g.DrawEllipse(trackPen, ringRect);
            }

            // Progress arc
            float progress = 1f - (_remainingSeconds / (float)Math.Max(_totalSeconds, 1));
            float sweepAngle = 360f * progress;
            Color arcColor = _isWorkSession ? _workColor : _breakColor;

            if (sweepAngle > 0.5f)
            {
                using (var arcPen = new Pen(arcColor, ringThickness))
                {
                    arcPen.StartCap = LineCap.Round;
                    arcPen.EndCap = LineCap.Round;
                    g.DrawArc(arcPen, ringRect, -90f, sweepAngle);
                }
            }

            // Glow dot at progress tip
            if (sweepAngle > 1f)
            {
                double angle = Math.PI / 180.0 * (-90 + sweepAngle);
                float dotX = cx + (ringSize / 2f) * (float)Math.Cos(angle);
                float dotY = cy + (ringSize / 2f) * (float)Math.Sin(angle);
                using (var glowBrush = new SolidBrush(Color.FromArgb(180, arcColor)))
                    g.FillEllipse(glowBrush, dotX - 7, dotY - 7, 14, 14);
                using (var dotBrush = new SolidBrush(arcColor))
                    g.FillEllipse(dotBrush, dotX - 5, dotY - 5, 10, 10);
            }

            // ── Time text inside the ring ──
            int mins = _remainingSeconds / 60;
            int secs = _remainingSeconds % 60;
            string timeText = $"{mins:D2}:{secs:D2}";

            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                // Large time
                using (var timeFont = new System.Drawing.Font("Segoe UI", 34f, FontStyle.Bold))
                    g.DrawString(timeText, timeFont, new SolidBrush(_fgColor), new RectangleF(ringRect.X, ringRect.Y, ringRect.Width, ringRect.Height), sf);

                // Small sub-label under time
                string subLabel = _isRunning ? "RUNNING" : (_remainingSeconds == _totalSeconds ? "READY" : "PAUSED");
                Color subColor = _isRunning ? arcColor : (_isDarkMode ? Color.FromArgb(120, 130, 150) : Color.FromArgb(140, 150, 170));
                using (var subFont = new System.Drawing.Font("Segoe UI", 8f, FontStyle.Bold))
                    g.DrawString(subLabel, subFont, new SolidBrush(subColor),
                        new RectangleF(ringRect.X, ringRect.Y + 36, ringRect.Width, ringRect.Height), sf);
            }

            // ── Buttons row ──
            int btnY = cy + ringSize / 2 + 18;
            int btnW = 56, btnH = 32, btnGap = 8;
            int totalBtnW = 4 * btnW + 3 * btnGap;
            int btnStartX = (w - totalBtnW) / 2;

            _btnStartRect = DrawPillButton(g, "▶ Start", btnStartX, btnY, btnW, btnH, !_isRunning, _isWorkSession ? _workColor : _breakColor);
            _btnPauseRect = DrawPillButton(g, "⏸ Pause", btnStartX + btnW + btnGap, btnY, btnW, btnH, _isRunning, Color.FromArgb(234, 179, 8));
            _btnResetRect = DrawPillButton(g, "↺ Reset", btnStartX + 2 * (btnW + btnGap), btnY, btnW, btnH, true, _isDarkMode ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184));
            _btnSkipRect = DrawPillButton(g, "⏭ Skip", btnStartX + 3 * (btnW + btnGap), btnY, btnW, btnH, true, Color.FromArgb(99, 102, 241));

            // ── Session dots at bottom ──
            int sessionDotY = btnY + btnH + 14;
            int dotSize = 10, dotGap = 6;
            int totalDotsW = _sessions * dotSize + (_sessions - 1) * dotGap;
            int dotStartX = (w - totalDotsW) / 2;

            for (int i = 0; i < _sessions; i++)
            {
                int dx = dotStartX + i * (dotSize + dotGap);
                Color dotColor;
                if (i < _currentSession - 1)
                    dotColor = _workColor; // completed
                else if (i == _currentSession - 1 && _isWorkSession)
                    dotColor = _isRunning ? arcColor : (_isDarkMode ? Color.FromArgb(80, 90, 110) : Color.FromArgb(180, 185, 200));
                else
                    dotColor = _isDarkMode ? Color.FromArgb(50, 56, 72) : Color.FromArgb(210, 215, 225);

                using (var b = new SolidBrush(dotColor))
                    g.FillEllipse(b, dx, sessionDotY, dotSize, dotSize);
            }
        }

        private RectangleF DrawPillButton(Graphics g, string text, int x, int y, int w, int h, bool enabled, Color accentColor)
        {
            var rect = new RectangleF(x, y, w, h);
            Color bg = enabled ? accentColor : (_isDarkMode ? Color.FromArgb(35, 40, 52) : Color.FromArgb(225, 228, 235));
            Color fg = enabled ? Color.White : (_isDarkMode ? Color.FromArgb(80, 90, 110) : Color.FromArgb(170, 175, 190));

            if (enabled)
            {
                // Subtle shadow
                using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                    FillRoundedRect(g, shadowBrush, new Rectangle(x + 1, y + 2, w, h), 8);
            }

            using (var bgBrush = new SolidBrush(bg))
                FillRoundedRect(g, bgBrush, new Rectangle(x, y, w, h), 8);

            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var font = new System.Drawing.Font("Segoe UI", 7.5f, FontStyle.Bold))
                g.DrawString(text, font, new SolidBrush(fg), rect, sf);

            return rect;
        }

        // ── Rounded rect helpers ──
        private static GraphicsPath GetRoundedRectPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawRoundedRect(Graphics g, Pen pen, Rectangle r, int radius)
        {
            using (var path = GetRoundedRectPath(r, radius))
                g.DrawPath(pen, path);
        }

        private static void FillRoundedRect(Graphics g, Brush brush, Rectangle r, int radius)
        {
            using (var path = GetRoundedRectPath(r, radius))
                g.FillPath(brush, path);
        }

        /// <summary>
        /// Set work session duration in minutes. Only applicable when timer is stopped.
        /// </summary>
        public void SetWorkDuration(int m)
        {
            if (m > 0 && !_isRunning)
            {
                _workMin = m;
                if (_isWorkSession)
                {
                    _totalSeconds = m * 60;
                    _remainingSeconds = _totalSeconds;
                    Invalidate();
                }
//                 DebugLogger.Log($"[Pomodoro] Work duration set to {m} minute(s)");
            }
        }

        /// <summary>
        /// Set short break duration in minutes. Only applicable when timer is stopped.
        /// </summary>
        public void SetShortBreakDuration(int m)
        {
            if (m > 0 && !_isRunning)
            {
                _shortBreak = m;
//                 DebugLogger.Log($"[Pomodoro] Short break duration set to {m} minute(s)");
            }
        }

        /// <summary>
        /// Set long break duration in minutes. Only applicable when timer is stopped.
        /// </summary>
        public void SetLongBreakDuration(int m)
        {
            if (m > 0 && !_isRunning)
            {
                _longBreak = m;
//                 DebugLogger.Log($"[Pomodoro] Long break duration set to {m} minute(s)");
            }
        }

        /// <summary>
        /// Set number of work sessions before long break. Only applicable when timer is stopped.
        /// </summary>
        public void SetSessionsBeforeLongBreak(int n)
        {
            if (n > 0 && !_isRunning)
            {
                _sessions = n;
//                 DebugLogger.Log($"[Pomodoro] Sessions before long break set to {n}");
            }
        }

        /// <summary>
        /// Clean up resources when panel is disposed.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && _timer != null)
            {
//                 DebugLogger.Log("[Pomodoro] Dispose() - cleaning up timer");
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
