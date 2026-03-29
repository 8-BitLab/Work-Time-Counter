using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    /// <summary>
    /// Premium design system tokens and helpers.
    /// All UI elements must use these values for visual consistency.
    /// </summary>
    public static class ThemeConstants
    {
        // â”€â”€â”€ SPACING (8px base grid) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        private sealed class ActionButtonAnimState
        {
            public Timer HoverTimer;
            public float HoverProgress;
            public bool Hovering;
            public int LastIconSize = -1;
            public float PulsePhase;
            public bool IsStart;
            public Color Bg;
            public Color BgHover;
            public int IconSize;
            public int HoverIconSize;
        }

        private static readonly Dictionary<Button, ActionButtonAnimState> _actionButtonAnimStates = new Dictionary<Button, ActionButtonAnimState>();

        public const int SpaceXS  = 4;
        public const int SpaceS   = 8;
        public const int SpaceM   = 12;
        public const int SpaceL   = 16;
        public const int SpaceXL  = 24;
        public const int SpaceXXL = 32;

        // â”€â”€â”€ CORNER RADIUS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const int RadiusSmall  = 4;
        public const int RadiusMedium = 6;
        public const int RadiusLarge  = 8;

        // â”€â”€â”€ CONTROL HEIGHTS (consistent across all controls) â”€â”€â”€â”€
        public const int InputHeight      = 32;
        public const int ButtonHeight     = 34;
        public const int ButtonHeightSm   = 28;
        public const int ToolbarHeight    = 42;
        public const int GridRowHeight    = 34;
        public const int GridHeaderHeight = 38;

        // â”€â”€â”€ DARK PALETTE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public static class Dark
        {
            // Backgrounds â€” 5 elevation layers (subtle blue undertone)
            public static readonly Color BgBase     = Color.FromArgb(13, 17, 23);
            public static readonly Color BgSurface  = Color.FromArgb(21, 26, 35);
            public static readonly Color BgElevated = Color.FromArgb(27, 33, 44);
            public static readonly Color BgInput    = Color.FromArgb(22, 27, 38);
            public static readonly Color BgHover    = Color.FromArgb(35, 42, 56);

            // Text â€” 3 emphasis levels
            public static readonly Color TextPrimary   = Color.FromArgb(230, 237, 243);
            public static readonly Color TextSecondary  = Color.FromArgb(136, 146, 166);
            public static readonly Color TextMuted      = Color.FromArgb(72, 82, 99);

            // Accent â€” warm amber, used sparingly for brand identity
            public static readonly Color AccentPrimary = Color.FromArgb(232, 133, 74);
            public static readonly Color AccentHover   = Color.FromArgb(240, 154, 96);
            public static readonly Color AccentMuted   = Color.FromArgb(120, 80, 50);
            public static readonly Color AccentSurface = Color.FromArgb(40, 30, 24);

            // Semantic â€” professional, muted tones (not neon)
            public static readonly Color Green      = Color.FromArgb(46, 160, 67);
            public static readonly Color GreenHover = Color.FromArgb(35, 134, 54);
            public static readonly Color Red        = Color.FromArgb(218, 54, 51);
            public static readonly Color RedHover   = Color.FromArgb(182, 35, 36);
            public static readonly Color Blue       = Color.FromArgb(56, 132, 244);
            public static readonly Color Yellow     = Color.FromArgb(210, 170, 50);

            // Borders & dividers
            public static readonly Color Border  = Color.FromArgb(40, 48, 62);
            public static readonly Color Divider = Color.FromArgb(32, 39, 52);

            // Selection & focus
            public static readonly Color Selection = Color.FromArgb(38, 68, 120);
            public static readonly Color FocusRing = Color.FromArgb(56, 132, 244);

            // Sticker card backgrounds
            public static readonly Color StickerTodo     = Color.FromArgb(50, 44, 28);
            public static readonly Color StickerReminder = Color.FromArgb(28, 50, 44);
            public static readonly Color StickerBug      = Color.FromArgb(55, 30, 28);
            public static readonly Color StickerIdea     = Color.FromArgb(38, 34, 56);
            public static readonly Color StickerDone     = Color.FromArgb(28, 32, 38);
        }

        // â”€â”€â”€ LIGHT PALETTE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public static class Light
        {
            public static readonly Color BgBase     = Color.FromArgb(246, 248, 250);
            public static readonly Color BgSurface  = Color.FromArgb(255, 255, 255);
            public static readonly Color BgElevated = Color.FromArgb(241, 243, 248);
            public static readonly Color BgInput    = Color.FromArgb(248, 249, 252);
            public static readonly Color BgHover    = Color.FromArgb(232, 236, 242);

            public static readonly Color TextPrimary   = Color.FromArgb(28, 38, 56);
            public static readonly Color TextSecondary  = Color.FromArgb(96, 112, 136);
            public static readonly Color TextMuted      = Color.FromArgb(148, 163, 184);

            public static readonly Color AccentPrimary = Color.FromArgb(215, 105, 50);
            public static readonly Color AccentHover   = Color.FromArgb(235, 120, 65);
            public static readonly Color AccentMuted   = Color.FromArgb(180, 130, 100);
            public static readonly Color AccentSurface = Color.FromArgb(255, 246, 240);

            public static readonly Color Green      = Color.FromArgb(34, 154, 72);
            public static readonly Color GreenHover = Color.FromArgb(22, 128, 58);
            public static readonly Color Red        = Color.FromArgb(208, 48, 48);
            public static readonly Color RedHover   = Color.FromArgb(178, 34, 34);
            public static readonly Color Blue       = Color.FromArgb(50, 120, 230);
            public static readonly Color Yellow     = Color.FromArgb(200, 160, 20);

            public static readonly Color Border  = Color.FromArgb(218, 226, 238);
            public static readonly Color Divider = Color.FromArgb(230, 234, 242);

            public static readonly Color Selection = Color.FromArgb(210, 228, 255);
            public static readonly Color FocusRing = Color.FromArgb(50, 120, 230);
        }

        // â”€â”€â”€ TYPOGRAPHY â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public const string FontFamily = "Segoe UI";
        public const string FontMono   = "Consolas";

        public static Font FontCaption   => new Font(FontFamily, 7.5f, FontStyle.Regular);
        public static Font FontSmall     => new Font(FontFamily, 8f, FontStyle.Regular);
        public static Font FontSmallBold => new Font(FontFamily, 8f, FontStyle.Bold);
        public static Font FontBody      => new Font(FontFamily, 9f, FontStyle.Regular);
        public static Font FontBodyBold  => new Font(FontFamily, 9f, FontStyle.Bold);
        public static Font FontSubtitle  => new Font(FontFamily, 9.5f, FontStyle.Bold);
        public static Font FontTitle     => new Font(FontFamily, 11f, FontStyle.Bold);
        public static Font FontHeading   => new Font(FontFamily, 12f, FontStyle.Bold);
        public static Font FontHero      => new Font(FontMono, 28f, FontStyle.Bold);

        // â”€â”€â”€ ROUNDED RECTANGLE â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            if (d <= 0)
            {
                path.AddRectangle(bounds);
                return path;
            }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // â”€â”€â”€ BUTTON: Primary (Start, Stop) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public static void StyleButtonPrimary(Button btn, Color bgColor)
        {
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = bgColor;
            btn.ForeColor = Color.White;
            btn.Font = FontBodyBold;
            btn.Cursor = Cursors.Hand;
            btn.Padding = new Padding(0);
            btn.TextAlign = ContentAlignment.MiddleCenter;

            var normalColor = bgColor;
            var hoverColor = ControlPaint.Dark(bgColor, 0.08f);
            btn.MouseEnter += (s, e) => btn.BackColor = hoverColor;
            btn.MouseLeave += (s, e) => btn.BackColor = normalColor;
        }

        // â”€â”€â”€ BUTTON: Secondary (Refresh, Print, Save, etc.) â”€â”€â”€â”€â”€
        public static void StyleButtonSecondary(Button btn, bool isDark = true)
        {
            btn.UseVisualStyleBackColor = false;
            var bg     = isDark ? Dark.BgElevated : Light.BgElevated;
            var fg     = isDark ? Dark.TextPrimary : Light.TextPrimary;
            var border = isDark ? Dark.Border : Light.Border;
            var hover  = isDark ? Dark.BgHover : Light.BgHover;

            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = border;
            btn.BackColor = bg;
            btn.ForeColor = fg;
            btn.Font = FontBody;
            btn.Cursor = Cursors.Hand;
            btn.Padding = new Padding(0);

            btn.MouseEnter += (s, e) => btn.BackColor = hover;
            btn.MouseLeave += (s, e) => btn.BackColor = bg;
        }

        // â”€â”€â”€ BUTTON: Toolbar Tab (icon pill with animated color inversion) â”€â”€
        // Default:  dark bg, light icon â€” clean minimal look
        // Hover:    smooth animated to light bg, dark icon (inverted)
        // Active:   accent-tinted surface with accent icon
        // IMPORTANT: MouseOverBackColor is kept in sync with Timer to prevent flicker.
        public static void StyleToolbarTab(Button btn, bool active, bool isDark = true)
        {
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = FontSmallBold;
            btn.Cursor = Cursors.Hand;
            btn.Padding = new Padding(10, 0, 10, 0);
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.ImageAlign = ContentAlignment.MiddleLeft;
            btn.TextImageRelation = TextImageRelation.ImageBeforeText;

            Color normalBg, normalBorder, normalIconColor;
            Color hoverBg, hoverBorder, hoverIconColor;

            normalBg = GetToolbarVariantBackground(btn.Name, isDark);

            if (isDark)
            {
                normalBorder = Color.FromArgb(38, 46, 60);
                normalIconColor = Color.FromArgb(170, 180, 195);
                hoverBg      = Color.CadetBlue;
                hoverBorder  = Color.CadetBlue;
                hoverIconColor = Color.FromArgb(235, 245, 247);
            }
            else
            {
                normalBorder = Color.FromArgb(218, 224, 234);
                normalIconColor = Color.FromArgb(80, 94, 116);
                hoverBg      = Light.AccentSurface;
                hoverBorder  = Light.AccentPrimary;
                hoverIconColor = Light.AccentPrimary;
            }

            Color activeBg = isDark ? Dark.AccentSurface : Light.AccentSurface;
            Color activeBorder = isDark ? Color.FromArgb(90, 232, 133, 74) : Color.FromArgb(90, 215, 105, 50);

            Color startBg = active ? activeBg : normalBg;
            Color startBorder = active ? activeBorder : normalBorder;

            btn.BackColor = startBg;
            btn.ForeColor = active ? (isDark ? Dark.AccentPrimary : Light.AccentPrimary) : normalIconColor;
            btn.FlatAppearance.BorderColor = startBorder;
            // KEY FIX: set MouseOver same as BackColor so WinForms doesn't override our Timer
            btn.FlatAppearance.MouseOverBackColor = startBg;
            btn.FlatAppearance.MouseDownBackColor = startBg;
            btn.Image = RenderToolbarIcon(btn.Name, 20, active ? (isDark ? Dark.AccentPrimary : Light.AccentPrimary) : normalIconColor);
            btn.Invalidate();

            Action applyInitialVisuals = () =>
            {
                if (btn.IsDisposed) return;
                btn.UseVisualStyleBackColor = false;
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = startBg;
                btn.ForeColor = active ? (isDark ? Dark.AccentPrimary : Light.AccentPrimary) : normalIconColor;
                btn.FlatAppearance.BorderColor = startBorder;
                btn.FlatAppearance.MouseOverBackColor = startBg;
                btn.FlatAppearance.MouseDownBackColor = startBg;
                btn.Image = RenderToolbarIcon(btn.Name, 20, active ? (isDark ? Dark.AccentPrimary : Light.AccentPrimary) : normalIconColor);
                btn.Invalidate();
                btn.Update();
            };

            btn.HandleCreated += (s, e) => btn.BeginInvoke((Action)(() => applyInitialVisuals()));
            btn.VisibleChanged += (s, e) =>
            {
                if (btn.Visible && btn.IsHandleCreated)
                    btn.BeginInvoke((Action)(() => applyInitialVisuals()));
            };

            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                var borderColor = btn.FlatAppearance.BorderColor;
                using (var path = RoundedRect(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), Math.Max(6, btn.Height / 2)))
                using (var pen = new Pen(borderColor, 1.2f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };

            if (active) return; // no animation for active tabs

            Timer hoverTimer = new Timer { Interval = 18 };
            float progress = 0f;
            bool hovering = false;
            int lastIconFrame = -1;

            hoverTimer.Tick += (s2, e2) =>
            {
                progress = hovering
                    ? Math.Min(1f, progress + 0.12f)
                    : Math.Max(0f, progress - 0.12f);

                var curBg = LerpColor(normalBg, hoverBg, progress);
                btn.BackColor = curBg;
                btn.FlatAppearance.MouseOverBackColor = curBg;  // keep in sync!
                btn.FlatAppearance.MouseDownBackColor = curBg;
                btn.FlatAppearance.BorderColor = LerpColor(normalBorder, hoverBorder, progress);

                int iconFrame = (int)(progress * 6);
                if (iconFrame != lastIconFrame)
                {
                    lastIconFrame = iconFrame;
                    var iconClr = LerpColor(normalIconColor, hoverIconColor, progress);
                    int iconSz = btn.Height > 10 ? Math.Min(btn.Height - 8, 20) : 18;
                    btn.Image = RenderToolbarIcon(btn.Name, iconSz, iconClr);
                }

                btn.ForeColor = LerpColor(normalIconColor, hoverIconColor, progress);

                if ((!hovering && progress <= 0f) || (hovering && progress >= 1f))
                    hoverTimer.Stop();
            };

            btn.MouseEnter += (s2, e2) => { hovering = true; hoverTimer.Start(); };
            btn.MouseLeave += (s2, e2) => { hovering = false; hoverTimer.Start(); };
            btn.Disposed += (s2, e2) => { hoverTimer.Stop(); hoverTimer.Dispose(); };
        }

        private static Color GetToolbarVariantBackground(string buttonName, bool isDark)
        {
            string n = (buttonName ?? "").ToLowerInvariant();
            if (isDark)
            {
                if (n.Contains("wiki") || n.Contains("helper")) return Color.FromArgb(33, 52, 55);
                if (n.Contains("ping")) return Color.FromArgb(30, 48, 52);
                if (n.Contains("note") || n.Contains("standup")) return Color.FromArgb(35, 54, 58);
                if (n.Contains("quick")) return Color.FromArgb(32, 50, 54);
                if (n.Contains("timer") || n.Contains("pomodoro")) return Color.FromArgb(28, 46, 50);
                if (n.Contains("team") || n.Contains("switch")) return Color.FromArgb(31, 49, 53);
                return Color.FromArgb(31, 49, 53);
            }

            if (n.Contains("wiki") || n.Contains("helper")) return Color.FromArgb(241, 244, 249);
            if (n.Contains("ping")) return Color.FromArgb(243, 245, 249);
            if (n.Contains("note") || n.Contains("standup")) return Color.FromArgb(239, 243, 248);
            if (n.Contains("quick")) return Color.FromArgb(244, 242, 247);
            if (n.Contains("timer") || n.Contains("pomodoro")) return Color.FromArgb(240, 245, 247);
            if (n.Contains("team") || n.Contains("switch")) return Color.FromArgb(239, 242, 248);
            return Color.FromArgb(235, 238, 244);
        }

        public static void StyleToolbarIconButton(Button btn, bool isDark = true)
        {
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
            btn.Padding = new Padding(0);
            btn.Text = "";
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.ImageAlign = ContentAlignment.MiddleCenter;

            Color normalBg = isDark ? Color.FromArgb(24, 30, 40) : Color.FromArgb(235, 238, 244);
            Color normalBorder = isDark ? Color.FromArgb(38, 46, 60) : Color.FromArgb(218, 224, 234);
            Color normalIcon = isDark ? Color.FromArgb(170, 180, 195) : Color.FromArgb(80, 94, 116);
            Color hoverBg = isDark ? Color.CadetBlue : Light.AccentSurface;
            Color hoverBorder = isDark ? Color.CadetBlue : Light.AccentPrimary;
            Color hoverIcon = isDark ? Color.FromArgb(235, 245, 247) : Light.AccentPrimary;
            int radius = RadiusMedium + 2;

            btn.BackColor = normalBg;
            btn.ForeColor = normalIcon;
            btn.FlatAppearance.BorderColor = normalBorder;
            btn.FlatAppearance.MouseOverBackColor = normalBg;
            btn.FlatAppearance.MouseDownBackColor = normalBg;
            btn.Invalidate();

            Action applyInitialVisuals = () =>
            {
                if (btn.IsDisposed) return;
                btn.UseVisualStyleBackColor = false;
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = normalBg;
                btn.ForeColor = normalIcon;
                btn.FlatAppearance.BorderColor = normalBorder;
                btn.FlatAppearance.MouseOverBackColor = normalBg;
                btn.FlatAppearance.MouseDownBackColor = normalBg;
                btn.Invalidate();
                btn.Update();
            };

            btn.HandleCreated += (s, e) => btn.BeginInvoke((Action)(() => applyInitialVisuals()));
            btn.VisibleChanged += (s, e) =>
            {
                if (btn.Visible && btn.IsHandleCreated)
                    btn.BeginInvoke((Action)(() => applyInitialVisuals()));
            };

            btn.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var path = RoundedRect(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), radius))
                using (var pen = new Pen(btn.FlatAppearance.BorderColor, 1.15f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            };

            Timer hoverTimer = new Timer { Interval = 18 };
            float progress = 0f;
            bool hovering = false;

            hoverTimer.Tick += (s, e) =>
            {
                progress = hovering
                    ? Math.Min(1f, progress + 0.12f)
                    : Math.Max(0f, progress - 0.12f);

                var curBg = LerpColor(normalBg, hoverBg, progress);
                btn.BackColor = curBg;
                btn.FlatAppearance.MouseOverBackColor = curBg;
                btn.FlatAppearance.MouseDownBackColor = curBg;
                btn.FlatAppearance.BorderColor = LerpColor(normalBorder, hoverBorder, progress);
                btn.ForeColor = LerpColor(normalIcon, hoverIcon, progress);

                if ((!hovering && progress <= 0f) || (hovering && progress >= 1f))
                    hoverTimer.Stop();
            };

            btn.MouseEnter += (s, e) => { hovering = true; hoverTimer.Start(); };
            btn.MouseLeave += (s, e) => { hovering = false; hoverTimer.Start(); };
            btn.Disposed += (s, e) => { hoverTimer.Stop(); hoverTimer.Dispose(); };
        }

        /// <summary>
        /// Renders the correct toolbar icon by button Name.
        /// Button names must match: "btnWiki", "btnPing", "btnNotes", "btnQuick", "btnTimer", "btnTeams"
        /// </summary>
        public static Bitmap RenderToolbarIcon(string btnName, int size, Color color)
        {
            if (string.IsNullOrEmpty(btnName)) return null;
            string n = btnName.ToLowerInvariant();
            if (n.Contains("wiki") || n.Contains("helper"))   return CreateWikiIcon(size, color);
            if (n.Contains("ping"))                             return CreateBellIcon(size, color);
            if (n.Contains("note") || n.Contains("standup"))   return CreateNotesIcon(size, color);
            if (n.Contains("quick"))                            return CreateLightningIcon(size, color);
            if (n.Contains("timer") || n.Contains("pomodoro")) return CreateClockIcon(size, color);
            if (n.Contains("team") || n.Contains("switch"))    return CreatePersonIcon(size, color);
            // Fallback: gear icon
            return CreateGearIcon(size, color);
        }

        // Keep old name as alias so existing code still compiles
        public static void StyleToggleButton(Button btn, bool active, bool isDark = true)
        {
            StyleToolbarTab(btn, active, isDark);
        }

        // â”€â”€â”€ COLOR LERP HELPER â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Smoothly blends between two colors by factor t (0.0 = from, 1.0 = to)
        private static Color LerpColor(Color from, Color to, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
        }

        // â”€â”€â”€ BUTTON: Action (START / STOP) â€” Glossy 3D icons + smooth hover â”€â”€
        // Uses glossy gradient circle icons. Smooth Timer hover with MouseOverBackColor synced.
        public static void StyleActionButton(Button btn, bool isDark, bool isStart)
        {
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = FontBodyBold;
            btn.Cursor = Cursors.Hand;
            btn.ForeColor = Color.White;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.TextImageRelation = TextImageRelation.ImageBeforeText;

            Color bg, bgHover;
            int iconSize = Math.Max(22, btn.Height > 0 ? btn.Height - 10 : 22);
            int hoverIconSize = Math.Max(12, iconSize / 2);

            if (isStart)
            {
                bg = isDark ? Color.FromArgb(34, 92, 96) : Color.FromArgb(54, 124, 128);
                bgHover = isDark ? Color.FromArgb(96, 190, 124) : Color.FromArgb(104, 198, 132);
                btn.Text = "  START";
                btn.Image = CreatePlayIcon(iconSize, Color.White);
            }
            else
            {
                bg = isDark ? Color.FromArgb(122, 28, 30) : Color.FromArgb(132, 30, 32);
                bgHover = isDark ? Color.FromArgb(242, 92, 88) : Color.FromArgb(232, 78, 74);
                btn.Text = "  STOP";
                btn.Image = CreateStopIcon(iconSize, Color.White);
            }

            if (!_actionButtonAnimStates.TryGetValue(btn, out var state))
            {
                state = new ActionButtonAnimState
                {
                    HoverTimer = new Timer { Interval = 18 }
                };
                _actionButtonAnimStates[btn] = state;

                state.HoverTimer.Tick += (s, e) =>
                {
                    if (btn.IsDisposed)
                    {
                        state.HoverTimer.Stop();
                        return;
                    }

                    state.HoverProgress = state.Hovering
                        ? Math.Min(1f, state.HoverProgress + 0.20f)
                        : Math.Max(0f, state.HoverProgress - 0.18f);

                    Color curBg;
                    if (state.Hovering)
                    {
                        state.PulsePhase += 0.22f;
                        if (state.PulsePhase > 2f * (float)Math.PI)
                            state.PulsePhase -= 2f * (float)Math.PI;

                        float pulse = (float)(Math.Sin(state.PulsePhase) + 1f) / 2f;
                        Color pulseHover = LerpColor(state.BgHover, ControlPaint.Light(state.BgHover, 0.22f), pulse);
                        curBg = LerpColor(state.Bg, pulseHover, state.HoverProgress);
                    }
                    else
                    {
                        curBg = LerpColor(state.Bg, state.BgHover, state.HoverProgress);
                    }

                    btn.BackColor = curBg;
                    btn.FlatAppearance.MouseOverBackColor = curBg;
                    btn.ForeColor = LerpColor(Color.White, Color.FromArgb(245, 252, 245), state.HoverProgress);

                    int currentIconSize = Math.Max(state.HoverIconSize,
                        (int)Math.Round(state.IconSize - ((state.IconSize - state.HoverIconSize) * state.HoverProgress)));
                    if (currentIconSize != state.LastIconSize)
                    {
                        state.LastIconSize = currentIconSize;
                        btn.Image = state.IsStart
                            ? CreatePlayIcon(currentIconSize, Color.White)
                            : CreateStopIcon(currentIconSize, Color.White);
                    }

                    if (!state.Hovering && state.HoverProgress <= 0f)
                        state.HoverTimer.Stop();
                };

                btn.MouseEnter += (s, e) =>
                {
                    state.Hovering = true;
                    state.HoverTimer.Start();
                };

                btn.MouseLeave += (s, e) =>
                {
                    state.Hovering = false;
                    state.HoverTimer.Start();
                };

                btn.HandleCreated += (s, e) =>
                {
                    if (!btn.IsDisposed && btn.IsHandleCreated)
                    {
                        btn.BeginInvoke((Action)(() =>
                        {
                            if (btn.IsDisposed) return;
                            btn.BackColor = state.Bg;
                            btn.FlatAppearance.MouseOverBackColor = state.Bg;
                            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(state.Bg, 0.15f);
                            btn.Invalidate();
                        }));
                    }
                };

                btn.VisibleChanged += (s, e) =>
                {
                    if (btn.Visible && btn.IsHandleCreated && !btn.IsDisposed)
                    {
                        btn.BeginInvoke((Action)(() =>
                        {
                            if (btn.IsDisposed) return;
                            btn.BackColor = state.Bg;
                            btn.FlatAppearance.MouseOverBackColor = state.Bg;
                            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(state.Bg, 0.15f);
                            btn.Invalidate();
                        }));
                    }
                };

                btn.Disposed += (s, e) =>
                {
                    state.HoverTimer.Stop();
                    state.HoverTimer.Dispose();
                    _actionButtonAnimStates.Remove(btn);
                };
            }

            state.IsStart = isStart;
            state.Bg = bg;
            state.BgHover = bgHover;
            state.IconSize = iconSize;
            state.HoverIconSize = hoverIconSize;
            state.HoverProgress = 0f;
            state.Hovering = false;
            state.LastIconSize = -1;
            state.PulsePhase = 0f;
            state.HoverTimer.Stop();

            btn.BackColor = bg;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = bg;
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bg, 0.15f);
            btn.Invalidate();
        }

        // --- BUTTON: Refresh - smooth animated color-swap hover -----
        public static void StyleRefreshButton(Button btn, bool isDark)
        {
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = FontSmallBold;
            btn.Cursor = Cursors.Hand;
            btn.ForeColor = Color.White;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.ImageAlign = ContentAlignment.MiddleCenter;
            btn.TextImageRelation = TextImageRelation.Overlay;
            btn.Padding = Padding.Empty;
            btn.Text = string.Empty;

            var normalBg     = isDark ? Color.FromArgb(16, 22, 32) : Color.FromArgb(232, 238, 245);
            var normalBorder = isDark ? Color.FromArgb(50, 63, 82) : Light.Border;
            var hoverBg      = isDark ? Color.FromArgb(28, 62, 74) : Color.FromArgb(220, 232, 242);
            var hoverBorder  = isDark ? Color.FromArgb(94, 212, 184) : Light.TextSecondary;
            int normalIconInset = 7;
            int hoverIconInset = 4;

            btn.BackColor = normalBg;
            btn.FlatAppearance.BorderColor = normalBorder;
            btn.FlatAppearance.MouseOverBackColor = normalBg;
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(normalBg, 0.1f);
            btn.Image = CreateRefreshIcon(Math.Max(24, btn.Width - normalIconInset), Color.FromArgb(245, 252, 255));
            btn.Invalidate();

            Action applyInitialVisuals = () =>
            {
                if (btn.IsDisposed) return;
                btn.UseVisualStyleBackColor = false;
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = normalBg;
                btn.FlatAppearance.BorderColor = normalBorder;
                btn.FlatAppearance.MouseOverBackColor = normalBg;
                btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(normalBg, 0.1f);
                btn.Image = CreateRefreshIcon(Math.Max(24, btn.Width - normalIconInset), Color.FromArgb(245, 252, 255));
                btn.Invalidate();
                btn.Update();
            };

            btn.HandleCreated += (s, e) => btn.BeginInvoke((Action)(() => applyInitialVisuals()));
            btn.VisibleChanged += (s, e) =>
            {
                if (btn.Visible && btn.IsHandleCreated)
                    btn.BeginInvoke((Action)(() => applyInitialVisuals()));
            };

            Timer hoverTimer = new Timer { Interval = 15 };
            float progress = 0f;
            bool hovering = false;
            int lastIconFrame = -1;
            float hoverPulsePhase = 0f;

            hoverTimer.Tick += (s, e) =>
            {
                progress = hovering
                    ? Math.Min(1f, progress + 0.22f)
                    : Math.Max(0f, progress - 0.18f);
                var curBg = LerpColor(normalBg, hoverBg, progress);
                if (hovering && progress > 0.90f)
                {
                    hoverPulsePhase += 0.26f;
                    float pulse = (float)(Math.Sin(hoverPulsePhase) * 0.5 + 0.5); // 0..1
                    var pulseBg = LerpColor(curBg, ControlPaint.Light(curBg, 0.18f), pulse);
                    curBg = pulseBg;
                }
                btn.BackColor = curBg;
                btn.FlatAppearance.MouseOverBackColor = curBg;
                var border = LerpColor(normalBorder, hoverBorder, progress);
                if (hovering && progress > 0.90f)
                    border = ControlPaint.Light(border, 0.12f);
                btn.FlatAppearance.BorderColor = border;
                btn.ForeColor = LerpColor(Color.White, Color.FromArgb(245, 252, 255), progress);

                int iconFrame = progress >= 0.55f ? 1 : 0;
                if (iconFrame != lastIconFrame)
                {
                    lastIconFrame = iconFrame;
                    int iconSize = Math.Max(24, btn.Width - (iconFrame == 1 ? hoverIconInset : normalIconInset));
                    btn.Image = CreateRefreshIcon(iconSize, Color.FromArgb(248, 253, 255));
                }
                if (!hovering && progress <= 0f)
                    hoverTimer.Stop();
            };

            btn.MouseEnter += (s, e) => { hovering = true; hoverTimer.Start(); };
            btn.MouseLeave += (s, e) => { hovering = false; hoverTimer.Start(); };
            btn.Disposed += (s, e) => { hoverTimer.Stop(); hoverTimer.Dispose(); };
        }

        // â”€â”€â”€ BUTTON: Print â€” smooth animated color-swap hover â”€â”€â”€â”€â”€â”€â”€
        public static void StylePrintButton(Button btn, bool isDark)
        {
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = FontSmallBold;
            btn.Cursor = Cursors.Hand;
            btn.ForeColor = Color.White;
            btn.TextAlign = ContentAlignment.MiddleCenter;
            btn.ImageAlign = ContentAlignment.MiddleCenter;
            btn.TextImageRelation = TextImageRelation.Overlay;
            btn.Padding = Padding.Empty;
            btn.Text = string.Empty;

            var tealBg       = isDark ? Color.FromArgb(16, 22, 32) : Color.FromArgb(232, 238, 245);
            var normalBorder = isDark ? Color.FromArgb(50, 63, 82) : Light.Border;
            var hoverBg      = isDark ? Color.FromArgb(34, 58, 94) : Color.FromArgb(220, 232, 242);
            var maroonBorder = isDark ? Color.FromArgb(118, 176, 255) : Light.TextSecondary;
            int normalIconInset = 7;
            int hoverIconInset = 4;

            btn.BackColor = tealBg;
            btn.FlatAppearance.BorderColor = normalBorder;
            btn.FlatAppearance.MouseOverBackColor = tealBg;
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(tealBg, 0.1f);
            btn.Image = CreatePrintIcon(Math.Max(24, btn.Width - normalIconInset), Color.FromArgb(245, 252, 255));
            btn.Invalidate();

            Action applyInitialVisuals = () =>
            {
                if (btn.IsDisposed) return;
                btn.UseVisualStyleBackColor = false;
                btn.FlatStyle = FlatStyle.Flat;
                btn.BackColor = tealBg;
                btn.FlatAppearance.BorderColor = normalBorder;
                btn.FlatAppearance.MouseOverBackColor = tealBg;
                btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(tealBg, 0.1f);
                btn.Image = CreatePrintIcon(Math.Max(24, btn.Width - normalIconInset), Color.FromArgb(245, 252, 255));
                btn.Invalidate();
                btn.Update();
            };

            btn.HandleCreated += (s, e) => btn.BeginInvoke((Action)(() => applyInitialVisuals()));
            btn.VisibleChanged += (s, e) =>
            {
                if (btn.Visible && btn.IsHandleCreated)
                    btn.BeginInvoke((Action)(() => applyInitialVisuals()));
            };

            Timer hoverTimer = new Timer { Interval = 15 };
            float progress = 0f;
            bool hovering = false;
            int lastIconFrame = -1;
            float hoverPulsePhase = 0f;

            hoverTimer.Tick += (s, e) =>
            {
                progress = hovering
                    ? Math.Min(1f, progress + 0.22f)
                    : Math.Max(0f, progress - 0.18f);
                var curBg = LerpColor(tealBg, hoverBg, progress);
                if (hovering && progress > 0.90f)
                {
                    hoverPulsePhase += 0.26f;
                    float pulse = (float)(Math.Sin(hoverPulsePhase) * 0.5 + 0.5); // 0..1
                    var pulseBg = LerpColor(curBg, ControlPaint.Light(curBg, 0.18f), pulse);
                    curBg = pulseBg;
                }
                btn.BackColor = curBg;
                btn.FlatAppearance.MouseOverBackColor = curBg;
                var border = LerpColor(normalBorder, maroonBorder, progress);
                if (hovering && progress > 0.90f)
                    border = ControlPaint.Light(border, 0.12f);
                btn.FlatAppearance.BorderColor = border;
                btn.ForeColor = LerpColor(Color.White, Color.FromArgb(248, 252, 255), progress);

                int iconFrame = progress >= 0.55f ? 1 : 0;
                if (iconFrame != lastIconFrame)
                {
                    lastIconFrame = iconFrame;
                    int iconSize = Math.Max(24, btn.Width - (iconFrame == 1 ? hoverIconInset : normalIconInset));
                    btn.Image = CreatePrintIcon(iconSize, Color.FromArgb(248, 253, 255));
                }
                if (!hovering && progress <= 0f)
                    hoverTimer.Stop();
            };

            btn.MouseEnter += (s, e) => { hovering = true; hoverTimer.Start(); };
            btn.MouseLeave += (s, e) => { hovering = false; hoverTimer.Start(); };
            btn.Disposed += (s, e) => { hoverTimer.Stop(); hoverTimer.Dispose(); };
        }

        // â”€â”€â”€ DATA GRID â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public static void StyleDataGrid(DataGridView dgv, bool isDark = true)
        {
            dgv.EnableHeadersVisualStyles = false;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgv.RowTemplate.Height = GridRowHeight;
            dgv.ColumnHeadersHeight = GridHeaderHeight;

            if (isDark)
            {
                dgv.GridColor = Dark.Divider;
                dgv.BackgroundColor = Dark.BgBase;
                dgv.DefaultCellStyle.BackColor = Dark.BgBase;
                dgv.DefaultCellStyle.ForeColor = Dark.TextPrimary;
                dgv.DefaultCellStyle.SelectionBackColor = Dark.Selection;
                dgv.DefaultCellStyle.SelectionForeColor = Color.White;
                dgv.DefaultCellStyle.Font = FontBody;
                dgv.DefaultCellStyle.Padding = new Padding(10, 5, 10, 5);

                dgv.AlternatingRowsDefaultCellStyle.BackColor = Dark.BgSurface;
                dgv.AlternatingRowsDefaultCellStyle.ForeColor = Dark.TextPrimary;

                dgv.ColumnHeadersDefaultCellStyle.BackColor = Dark.BgElevated;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Dark.TextSecondary;
                dgv.ColumnHeadersDefaultCellStyle.Font = FontSmallBold;
                dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 6, 10, 6);
            }
            else
            {
                dgv.GridColor = Light.Divider;
                dgv.BackgroundColor = Light.BgBase;
                dgv.DefaultCellStyle.BackColor = Light.BgSurface;
                dgv.DefaultCellStyle.ForeColor = Light.TextPrimary;
                dgv.DefaultCellStyle.SelectionBackColor = Light.Selection;
                dgv.DefaultCellStyle.SelectionForeColor = Light.TextPrimary;
                dgv.DefaultCellStyle.Font = FontBody;
                dgv.DefaultCellStyle.Padding = new Padding(10, 5, 10, 5);

                dgv.AlternatingRowsDefaultCellStyle.BackColor = Light.BgBase;
                dgv.AlternatingRowsDefaultCellStyle.ForeColor = Light.TextPrimary;

                dgv.ColumnHeadersDefaultCellStyle.BackColor = Light.BgElevated;
                dgv.ColumnHeadersDefaultCellStyle.ForeColor = Light.TextSecondary;
                dgv.ColumnHeadersDefaultCellStyle.Font = FontSmallBold;
                dgv.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 6, 10, 6);
            }
        }

        // â”€â”€â”€ SECTION HEADER LABEL â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public static Label CreateSectionHeader(string text, bool isDark = true)
        {
            return new Label
            {
                Text = text,
                Font = FontTitle,
                ForeColor = isDark ? Dark.TextSecondary : Light.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        // â”€â”€â”€ BORDER HELPERS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        public static void DrawTopBorder(PaintEventArgs e, int width, bool isDark = true)
        {
            var color = isDark ? Dark.Border : Light.Border;
            using (var pen = new Pen(color, 1))
            {
                e.Graphics.DrawLine(pen, 0, 0, width, 0);
            }
        }

        public static void DrawBottomBorder(PaintEventArgs e, int width, int height, bool isDark = true)
        {
            var color = isDark ? Dark.Border : Light.Border;
            using (var pen = new Pen(color, 1))
            {
                e.Graphics.DrawLine(pen, 0, height - 1, width, height - 1);
            }
        }

        // â”€â”€â”€ VECTOR ICON FACTORY â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Clean, anti-aliased vector icons rendered via GDI+.
        // Every icon is a white-on-transparent bitmap suitable for
        // use as Button.Image on dark backgrounds.

        /// <summary>Glossy 3D play button â€” green gradient circle with white triangle + shine</summary>
        public static Bitmap CreatePlayIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float pad = 1f;
                float d = size - pad * 2;
                var circleRect = new RectangleF(pad, pad, d, d);

                // Main gradient: bright green top to dark green bottom
                using (var grad = new LinearGradientBrush(circleRect,
                    Color.FromArgb(130, 220, 80),   // bright lime-green top
                    Color.FromArgb(30, 120, 20),     // dark green bottom
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(grad, circleRect);
                }

                // Subtle dark ring border
                using (var pen = new Pen(Color.FromArgb(100, 0, 60, 0), Math.Max(1f, size / 20f)))
                    g.DrawEllipse(pen, circleRect);

                // Glossy shine: white semi-transparent ellipse in top half
                var shineRect = new RectangleF(pad + d * 0.15f, pad + d * 0.04f, d * 0.7f, d * 0.42f);
                using (var shineBrush = new LinearGradientBrush(shineRect,
                    Color.FromArgb(160, 255, 255, 255),
                    Color.FromArgb(10, 255, 255, 255),
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(shineBrush, shineRect);
                }

                // White play triangle centered
                float cx = size / 2f, cy = size / 2f;
                float triH = d * 0.38f;
                float triW = triH * 0.9f;
                var tri = new PointF[]
                {
                    new PointF(cx - triW * 0.35f, cy - triH),
                    new PointF(cx + triW * 0.8f, cy),
                    new PointF(cx - triW * 0.35f, cy + triH)
                };
                using (var brush = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
                    g.FillPolygon(brush, tri);
            }
            return bmp;
        }

        /// <summary>Glossy 3D stop button â€” red gradient circle with white square + shine</summary>
        public static Bitmap CreateStopIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float pad = 1f;
                float d = size - pad * 2;
                var circleRect = new RectangleF(pad, pad, d, d);

                // Main gradient: bright red top to dark red bottom
                using (var grad = new LinearGradientBrush(circleRect,
                    Color.FromArgb(240, 90, 80),     // bright red-orange top
                    Color.FromArgb(160, 20, 15),     // deep red bottom
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(grad, circleRect);
                }

                // Subtle dark ring border
                using (var pen = new Pen(Color.FromArgb(100, 80, 0, 0), Math.Max(1f, size / 20f)))
                    g.DrawEllipse(pen, circleRect);

                // Glossy shine
                var shineRect = new RectangleF(pad + d * 0.15f, pad + d * 0.04f, d * 0.7f, d * 0.42f);
                using (var shineBrush = new LinearGradientBrush(shineRect,
                    Color.FromArgb(150, 255, 255, 255),
                    Color.FromArgb(10, 255, 255, 255),
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(shineBrush, shineRect);
                }

                // White stop square centered with rounded corners
                float sqSize = d * 0.32f;
                float cx = size / 2f, cy = size / 2f;
                var sqRect = new RectangleF(cx - sqSize, cy - sqSize, sqSize * 2, sqSize * 2);
                int sqR = Math.Max(2, (int)(sqSize * 0.2f));
                using (var sqPath = RoundedRect(Rectangle.Round(sqRect), sqR))
                using (var brush = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
                    g.FillPath(brush, sqPath);
            }
            return bmp;
        }

        public static Bitmap CreateRefreshIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float pad = 1f;
                float d = size - pad * 2f;
                var circleRect = new RectangleF(pad, pad, d, d);
                using (var grad = new LinearGradientBrush(circleRect,
                    Color.FromArgb(86, 236, 192),
                    Color.FromArgb(14, 118, 83),
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(grad, circleRect);
                }

                using (var ringPen = new Pen(Color.FromArgb(120, 8, 84, 62), Math.Max(1f, size / 18f)))
                    g.DrawEllipse(ringPen, circleRect);

                var shineRect = new RectangleF(pad + d * 0.15f, pad + d * 0.06f, d * 0.68f, d * 0.38f);
                using (var shineBrush = new LinearGradientBrush(shineRect,
                    Color.FromArgb(165, 255, 255, 255),
                    Color.FromArgb(8, 255, 255, 255),
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(shineBrush, shineRect);
                }

                float cx = size / 2f;
                float cy = size / 2f;
                float r = d * 0.28f;
                float stroke = Math.Max(1.7f, size / 7.5f);

                using (var pen = new Pen(color, stroke))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, cx - r, cy - r, r * 2f, r * 2f, 28, 212);
                }

                float arrowSize = Math.Max(3.2f, size / 4.4f);
                using (var brush = new SolidBrush(color))
                {
                    double a1 = 244 * Math.PI / 180.0;
                    float tipX1 = cx + r * (float)Math.Cos(a1);
                    float tipY1 = cy + r * (float)Math.Sin(a1);
                    var arrow1 = new[]
                    {
                        new PointF(tipX1, tipY1),
                        new PointF(tipX1 + arrowSize * 0.92f, tipY1 - arrowSize * 0.12f),
                        new PointF(tipX1 + arrowSize * 0.08f, tipY1 + arrowSize * 0.88f)
                    };
                    g.FillPolygon(brush, arrow1);

                    double a2 = 30 * Math.PI / 180.0;
                    float tipX2 = cx + r * (float)Math.Cos(a2);
                    float tipY2 = cy + r * (float)Math.Sin(a2);
                    var arrow2 = new[]
                    {
                        new PointF(tipX2, tipY2),
                        new PointF(tipX2 - arrowSize * 0.96f, tipY2 + arrowSize * 0.06f),
                        new PointF(tipX2 - arrowSize * 0.06f, tipY2 - arrowSize * 0.9f)
                    };
                    g.FillPolygon(brush, arrow2);
                }
            }
            return bmp;
        }

        public static Bitmap CreatePrintIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                float pad = 1f;
                float d = size - pad * 2f;
                var circleRect = new RectangleF(pad, pad, d, d);
                using (var grad = new LinearGradientBrush(circleRect,
                    Color.FromArgb(86, 170, 255),
                    Color.FromArgb(28, 82, 205),
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(grad, circleRect);
                }

                using (var ringPen = new Pen(Color.FromArgb(120, 14, 42, 122), Math.Max(1f, size / 18f)))
                    g.DrawEllipse(ringPen, circleRect);

                var shineRect = new RectangleF(pad + d * 0.14f, pad + d * 0.05f, d * 0.7f, d * 0.36f);
                using (var shineBrush = new LinearGradientBrush(shineRect,
                    Color.FromArgb(160, 255, 255, 255),
                    Color.FromArgb(8, 255, 255, 255),
                    LinearGradientMode.Vertical))
                {
                    g.FillEllipse(shineBrush, shineRect);
                }

                float u = size / 12f;
                float stroke = Math.Max(1.1f, u * 0.9f);
                var paperColor = Color.FromArgb(235, 250, 250, 250);

                using (var pen = new Pen(color, stroke))
                using (var paperBrush = new SolidBrush(paperColor))
                {
                    pen.LineJoin = LineJoin.Round;

                    var paperTop = new RectangleF(3.8f * u, 1.6f * u, 4.4f * u, 2.5f * u);
                    g.FillRectangle(paperBrush, paperTop);
                    g.DrawRectangle(pen, paperTop.X, paperTop.Y, paperTop.Width, paperTop.Height);

                    var bodyRect = new RectangleF(2.2f * u, 4.0f * u, 7.6f * u, 3.8f * u);
                    using (var bodyBrush = new SolidBrush(Color.FromArgb(55, color)))
                    using (var bodyPath = RoundedRect(Rectangle.Round(bodyRect), Math.Max(2, (int)(u * 0.9f))))
                    {
                        g.FillPath(bodyBrush, bodyPath);
                        g.DrawPath(pen, bodyPath);
                    }

                    g.DrawLine(pen, bodyRect.X + 0.9f * u, bodyRect.Y + 1.45f * u, bodyRect.Right - 0.9f * u, bodyRect.Y + 1.45f * u);
                    g.FillEllipse(new SolidBrush(Color.FromArgb(210, 255, 255, 255)), bodyRect.Right - 1.7f * u, bodyRect.Y + 0.8f * u, 0.8f * u, 0.8f * u);

                    var paperBottom = new RectangleF(3.2f * u, 7.1f * u, 5.6f * u, 2.9f * u);
                    g.FillRectangle(paperBrush, paperBottom);
                    g.DrawRectangle(pen, paperBottom.X, paperBottom.Y, paperBottom.Width, paperBottom.Height);

                    using (var thinPen = new Pen(Color.FromArgb(130, color), Math.Max(0.8f, stroke * 0.55f)))
                    {
                        g.DrawLine(thinPen, paperBottom.X + 0.65f * u, paperBottom.Y + 0.85f * u, paperBottom.Right - 0.65f * u, paperBottom.Y + 0.85f * u);
                        g.DrawLine(thinPen, paperBottom.X + 0.65f * u, paperBottom.Y + 1.7f * u, paperBottom.Right - 1.45f * u, paperBottom.Y + 1.7f * u);
                    }
                }
            }
            return bmp;
        }

        public static Bitmap CreateGearIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float cx = size / 2f, cy = size / 2f;
                float outerR = size / 2f - 1;
                float innerR = outerR * 0.62f;
                int teeth = 8;

                var path = new GraphicsPath();
                for (int i = 0; i < teeth; i++)
                {
                    float a1 = (float)(i * 360.0 / teeth - 12) * (float)(Math.PI / 180);
                    float a2 = (float)(i * 360.0 / teeth + 12) * (float)(Math.PI / 180);
                    float a3 = (float)(i * 360.0 / teeth + 360.0 / teeth / 2 - 10) * (float)(Math.PI / 180);
                    float a4 = (float)(i * 360.0 / teeth + 360.0 / teeth / 2 + 10) * (float)(Math.PI / 180);

                    if (i == 0)
                        path.AddLine(cx + outerR * (float)Math.Cos(a1), cy + outerR * (float)Math.Sin(a1),
                                     cx + outerR * (float)Math.Cos(a2), cy + outerR * (float)Math.Sin(a2));
                    else
                        path.AddLine(path.GetLastPoint(),
                                     new PointF(cx + outerR * (float)Math.Cos(a2), cy + outerR * (float)Math.Sin(a2)));

                    path.AddLine(path.GetLastPoint(),
                                 new PointF(cx + innerR * (float)Math.Cos(a3), cy + innerR * (float)Math.Sin(a3)));
                    path.AddLine(path.GetLastPoint(),
                                 new PointF(cx + innerR * (float)Math.Cos(a4), cy + innerR * (float)Math.Sin(a4)));

                    float a5 = (float)((i + 1) * 360.0 / teeth - 12) * (float)(Math.PI / 180);
                    path.AddLine(path.GetLastPoint(),
                                 new PointF(cx + outerR * (float)Math.Cos(a5), cy + outerR * (float)Math.Sin(a5)));
                }
                path.CloseFigure();

                using (var brush = new SolidBrush(color))
                    g.FillPath(brush, path);

                // Center hole
                float holeR = innerR * 0.52f;
                using (var brush = new SolidBrush(Color.Transparent))
                {
                    var region = new Region(new RectangleF(cx - holeR, cy - holeR, holeR * 2, holeR * 2));
                    g.SetClip(region, CombineMode.Exclude);
                }
                // Simple approach: draw a circle cutout
                using (var brush = new SolidBrush(Color.Transparent))
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                using (var clearBrush = new SolidBrush(Color.Transparent))
                    g.FillEllipse(clearBrush, cx - holeR, cy - holeR, holeR * 2, holeR * 2);
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            }
            return bmp;
        }
        // â”€â”€â”€ TOOLBAR LINE-ART ICONS â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Minimal outlined icons inspired by Feather/Lucide icon sets.
        // Each draws clean stroked paths â€” no fills, just lines.

        /// <summary>Wiki: open book icon</summary>
        public static Bitmap CreateWikiIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                float s = size / 16f;
                using (var pen = new Pen(color, Math.Max(1.35f, s * 1.2f)))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    // Left page
                    g.DrawLine(pen, 8 * s, 2.5f * s, 8 * s, 13.5f * s);      // spine
                    g.DrawBezier(pen, 8 * s, 2.5f * s, 6 * s, 2 * s, 3 * s, 1.5f * s, 1.5f * s, 2.5f * s);
                    g.DrawLine(pen, 1.5f * s, 2.5f * s, 1.5f * s, 13.5f * s);
                    g.DrawBezier(pen, 1.5f * s, 13.5f * s, 3.5f * s, 12.5f * s, 6 * s, 12.5f * s, 8 * s, 13.5f * s);
                    // Right page
                    g.DrawBezier(pen, 8 * s, 2.5f * s, 10 * s, 2 * s, 13 * s, 1.5f * s, 14.5f * s, 2.5f * s);
                    g.DrawLine(pen, 14.5f * s, 2.5f * s, 14.5f * s, 13.5f * s);
                    g.DrawBezier(pen, 14.5f * s, 13.5f * s, 12.5f * s, 12.5f * s, 10 * s, 12.5f * s, 8 * s, 13.5f * s);
                }
            }
            return bmp;
        }

        /// <summary>Ping: bell icon</summary>
        public static Bitmap CreateBellIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                float s = size / 16f;
                using (var pen = new Pen(color, Math.Max(1.35f, s * 1.2f)))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    // Bell body path
                    var path = new GraphicsPath();
                    path.AddBezier(3f * s, 10f * s, 3f * s, 5.5f * s, 5f * s, 2f * s, 8f * s, 2f * s);
                    path.AddBezier(8f * s, 2f * s, 11f * s, 2f * s, 13f * s, 5.5f * s, 13f * s, 10f * s);
                    g.DrawPath(pen, path);
                    // Base line
                    g.DrawLine(pen, 1.5f * s, 10f * s, 14.5f * s, 10f * s);
                    // Clapper â€” small arc at bottom
                    g.DrawArc(pen, 6f * s, 11f * s, 4f * s, 3f * s, 0, 180);
                    // Top knob
                    g.DrawLine(pen, 8f * s, 0.5f * s, 8f * s, 2f * s);
                }
            }
            return bmp;
        }

        /// <summary>Notes: lined notepad icon</summary>
        public static Bitmap CreateNotesIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                float s = size / 16f;
                float stroke = Math.Max(1.35f, s * 1.2f);
                using (var pen = new Pen(color, stroke))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    // Outer rectangle (notepad)
                    using (var rect = RoundedRect(new Rectangle(
                        (int)(2f * s), (int)(1f * s), (int)(12f * s), (int)(14f * s)), Math.Max(1, (int)(s))))
                        g.DrawPath(pen, rect);
                    // Text lines
                    var thinPen = new Pen(color, Math.Max(0.9f, stroke * 0.7f));
                    thinPen.StartCap = LineCap.Round;
                    thinPen.EndCap = LineCap.Round;
                    g.DrawLine(thinPen, 4.5f * s, 5f * s, 11.5f * s, 5f * s);
                    g.DrawLine(thinPen, 4.5f * s, 7.5f * s, 11.5f * s, 7.5f * s);
                    g.DrawLine(thinPen, 4.5f * s, 10f * s, 9f * s, 10f * s);
                    thinPen.Dispose();
                }
            }
            return bmp;
        }

        /// <summary>Quick: lightning bolt icon</summary>
        public static Bitmap CreateLightningIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                float s = size / 16f;
                using (var pen = new Pen(color, Math.Max(1.35f, s * 1.2f)))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    var bolt = new PointF[]
                    {
                        new PointF(10f * s, 0.5f * s),
                        new PointF(4f * s, 8.5f * s),
                        new PointF(8f * s, 8.5f * s),
                        new PointF(6f * s, 15.5f * s),
                        new PointF(12f * s, 7f * s),
                        new PointF(8f * s, 7f * s),
                        new PointF(10f * s, 0.5f * s)
                    };
                    g.DrawLines(pen, bolt);
                }
            }
            return bmp;
        }

        /// <summary>Timer: clock icon</summary>
        public static Bitmap CreateClockIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                float s = size / 16f;
                using (var pen = new Pen(color, Math.Max(1.35f, s * 1.2f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    // Circle
                    g.DrawEllipse(pen, 1.5f * s, 1.5f * s, 13f * s, 13f * s);
                    // Hour hand (pointing to 12)
                    g.DrawLine(pen, 8f * s, 8f * s, 8f * s, 4f * s);
                    // Minute hand (pointing to 3)
                    g.DrawLine(pen, 8f * s, 8f * s, 11.5f * s, 8f * s);
                }
            }
            return bmp;
        }

        /// <summary>Teams: person/user icon</summary>
        public static Bitmap CreatePersonIcon(int size, Color color)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);
                float s = size / 16f;
                using (var pen = new Pen(color, Math.Max(1.35f, s * 1.2f)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    // Head circle
                    g.DrawEllipse(pen, 5f * s, 1f * s, 6f * s, 6f * s);
                    // Body/shoulders arc
                    g.DrawArc(pen, 1.5f * s, 9.5f * s, 13f * s, 10f * s, 180, 180);
                }
            }
            return bmp;
        }
    }
}

