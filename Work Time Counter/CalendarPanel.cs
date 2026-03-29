// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        CalendarPanel.cs                                             ║
// ║  PURPOSE:     CUSTOM MONTHLY CALENDAR WIDGET FOR THE SIDEBAR               ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Owner-drawn monthly calendar that fits inside the right sidebar panel.    ║
// ║  Highlights today, marks dates with entries, supports click to open day,   ║
// ║  and double-click for quick-add. Respects dark/light theme.                ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    /// <summary>
    /// Custom-drawn monthly calendar control.
    /// Fires DateClicked when user clicks a day, DateDoubleClicked for quick-add.
    /// </summary>
    public class CalendarPanel : Panel
    {
        // ═══ EVENTS ═══
        /// <summary>Fired when user single-clicks a date cell.</summary>
        public event Action<DateTime> DateClicked;

        /// <summary>Fired when user double-clicks a date cell (quick-add).</summary>
        public event Action<DateTime> DateDoubleClicked;

        // ═══ STATE ═══
        private DateTime _displayMonth;              // Currently displayed month
        private HashSet<string> _datesWithEntries;   // "yyyy-MM-dd" keys that have entries
        private Dictionary<string, int> _dateEntryCounts; // Entry counts for tooltips
        private bool _isDarkMode = true;
        private CustomTheme _customTheme = null;
        private int _firstDayOfWeek = 1;             // 0=Sunday, 1=Monday
        private bool _showWeekNumbers = false;
        private DateTime? _hoveredDate = null;
        private DateTime? _selectedDate = null;

        // ═══ LAYOUT CONSTANTS ═══
        private const int HEADER_HEIGHT = 36;        // Month/year header row
        private const int DAY_HEADER_HEIGHT = 22;    // Day-of-week labels
        private const int NAV_BTN_SIZE = 28;         // < > navigation buttons
        private const int CELL_PADDING = 2;

        // ═══ TOOLTIP ═══
        private ToolTip _tooltip;
        private DateTime? _lastTooltipDate = null;

        /// <summary>Constructor: initialize calendar control with double-buffering, state, and event handlers.</summary>
        public CalendarPanel()
        {
//             DebugLogger.Log("[Calendar] Initializing CalendarPanel");

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            // Initialize state to current month
            _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
//             DebugLogger.Log($"[Calendar] Display month set to: {_displayMonth:yyyy-MM}");

            _datesWithEntries = new HashSet<string>();
            _dateEntryCounts = new Dictionary<string, int>();

            // Configure tooltip behavior
            _tooltip = new ToolTip
            {
                InitialDelay = 400,
                ReshowDelay = 200,
                AutoPopDelay = 3000,
                BackColor = Color.FromArgb(50, 55, 65),
                ForeColor = Color.White,
                OwnerDraw = false
            };

            // Wire up event handlers for mouse interaction and rendering
            this.MouseClick += CalendarPanel_MouseClick;
            this.MouseDoubleClick += CalendarPanel_MouseDoubleClick;
            this.MouseMove += CalendarPanel_MouseMove;
            this.MouseLeave += (s, e) => { _hoveredDate = null; Invalidate(); };
            this.Paint += CalendarPanel_Paint;

//             DebugLogger.Log("[Calendar] Initialization complete");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  PUBLIC METHODS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Refresh which dates are marked as having entries.</summary>
        public void RefreshEntryMarkers()
        {
//             DebugLogger.Log("[Calendar] Refreshing entry markers");

            try
            {
                // Fetch all dates that have at least one entry
                _datesWithEntries = OrganizerStorage.GetDatesWithEntries();
//                 DebugLogger.Log($"[Calendar] Found {_datesWithEntries.Count} dates with entries");

                // Build counts for each date to display in tooltips
                _dateEntryCounts.Clear();
                foreach (var dateKey in _datesWithEntries)
                {
                    int count = OrganizerStorage.GetEntryCount(dateKey);
                    _dateEntryCounts[dateKey] = count;
//                     DebugLogger.Log($"[Calendar] Date {dateKey}: {count} entries");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Calendar] ERROR refreshing entry markers: {ex.Message}");
                _datesWithEntries = new HashSet<string>();
                _dateEntryCounts.Clear();
            }

            // Trigger repaint to show updated markers
            Invalidate();
//             DebugLogger.Log("[Calendar] Entry markers refresh complete");
        }

        /// <summary>Apply theme colors.</summary>
        public void ApplyTheme(bool isDarkMode, CustomTheme customTheme = null)
        {
//             DebugLogger.Log($"[Calendar] Applying theme: isDarkMode={isDarkMode}, customTheme={customTheme?.PresetName ?? "none"}");
            _isDarkMode = isDarkMode;
            _customTheme = customTheme;
            Invalidate();
        }

        /// <summary>Set first day of week (0=Sunday, 1=Monday).</summary>
        public void SetFirstDayOfWeek(int day)
        {
//             DebugLogger.Log($"[Calendar] Set first day of week to: {day} (0=Sunday, 1=Monday)");
            _firstDayOfWeek = day;
            Invalidate();
        }

        /// <summary>Set whether to show week numbers.</summary>
        public void SetShowWeekNumbers(bool show)
        {
//             DebugLogger.Log($"[Calendar] Set show week numbers: {show}");
            _showWeekNumbers = show;
            Invalidate();
        }

        /// <summary>Navigate to today's month.</summary>
        public void GoToToday()
        {
            _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
//             DebugLogger.Log($"[Calendar] Navigated to today: {_displayMonth:yyyy-MM}");
            Invalidate();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  THEME COLORS
        // ═══════════════════════════════════════════════════════════════════════

        private Color BgColor => (_customTheme?.Enabled == true)
            ? _customTheme.GetCard()
            : _isDarkMode ? ThemeConstants.Dark.BgElevated : ThemeConstants.Light.BgSurface;

        private Color HeaderBg => (_customTheme?.Enabled == true)
            ? _customTheme.GetBackground()
            : _isDarkMode ? ThemeConstants.Dark.BgBase : ThemeConstants.Light.BgElevated;

        private Color TextColor => (_customTheme?.Enabled == true)
            ? _customTheme.GetText()
            : _isDarkMode ? ThemeConstants.Dark.TextPrimary : ThemeConstants.Light.TextPrimary;

        private Color SecondaryText => (_customTheme?.Enabled == true)
            ? _customTheme.GetSecondaryText()
            : _isDarkMode ? ThemeConstants.Dark.TextSecondary : ThemeConstants.Light.TextSecondary;

        private Color AccentColor => (_customTheme?.Enabled == true)
            ? _customTheme.GetAccent()
            : _isDarkMode ? ThemeConstants.Dark.AccentPrimary : ThemeConstants.Light.AccentPrimary;

        // Today uses coral accent for consistent brand identity (was green)
        private Color TodayBg => (_customTheme?.Enabled == true)
            ? _customTheme.GetAccent()
            : _isDarkMode ? ThemeConstants.Dark.AccentPrimary : ThemeConstants.Light.AccentPrimary;
        private Color TodayText => Color.White;

        private Color EntryMarkerColor => AccentColor;

        private Color HoverBg => _isDarkMode
            ? ThemeConstants.Dark.BgHover
            : ThemeConstants.Light.BgHover;

        private Color SelectedBg => _isDarkMode
            ? ThemeConstants.Dark.Selection
            : ThemeConstants.Light.Selection;

        private Color GridLineColor => _isDarkMode
            ? ThemeConstants.Dark.Divider
            : ThemeConstants.Light.Divider;

        private Color WeekNumColor => _isDarkMode
            ? ThemeConstants.Dark.TextMuted
            : ThemeConstants.Light.TextMuted;

        // ═══════════════════════════════════════════════════════════════════════
        //  PAINTING
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Paint handler: render the entire calendar (header, day labels, date grid).</summary>
        private void CalendarPanel_Paint(object sender, PaintEventArgs e)
        {
//             DebugLogger.Log($"[Calendar] Paint event: {this.Width}x{this.Height}");

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;

            // Draw background
            using (var bgBrush = new SolidBrush(BgColor))
                g.FillRectangle(bgBrush, 0, 0, w, h);

            // ── HEADER: < Month Year > ──
            DrawHeader(g, w);

            // ── DAY-OF-WEEK LABELS ──
            int dayHeaderY = HEADER_HEIGHT;
            DrawDayHeaders(g, w, dayHeaderY);

            // ── DATE GRID ──
            int gridY = dayHeaderY + DAY_HEADER_HEIGHT;
            int gridH = h - gridY;
            DrawDateGrid(g, w, gridY, gridH);
        }

        /// <summary>Draw calendar header with navigation buttons and month/year label.</summary>
        private void DrawHeader(Graphics g, int width)
        {
//             DebugLogger.Log($"[Calendar] Drawing header for {_displayMonth:MMMM yyyy}");

            // Header background
            using (var brush = new SolidBrush(HeaderBg))
                g.FillRectangle(brush, 0, 0, width, HEADER_HEIGHT);

            // Navigation: < and >
            var navFont = new Font("Segoe UI", 12f, FontStyle.Bold);
            var navBrush = new SolidBrush(AccentColor);

            // Left arrow (previous month)
            g.DrawString("◀", navFont, navBrush, 6, (HEADER_HEIGHT - navFont.Height) / 2);

            // Right arrow (next month)
            var rightSize = g.MeasureString("▶", navFont);
            g.DrawString("▶", navFont, navBrush, width - rightSize.Width - 8, (HEADER_HEIGHT - navFont.Height) / 2);

            // Month Year text centered
            string monthText = _displayMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            var titleFont = new Font("Segoe UI", 11f, FontStyle.Bold);
            var titleBrush = new SolidBrush(TextColor);
            var titleSize = g.MeasureString(monthText, titleFont);
            g.DrawString(monthText, titleFont, titleBrush, (width - titleSize.Width) / 2, (HEADER_HEIGHT - titleSize.Height) / 2);

            // Bottom separator line
            using (var pen = new Pen(GridLineColor, 1))
                g.DrawLine(pen, 0, HEADER_HEIGHT - 1, width, HEADER_HEIGHT - 1);

            navFont.Dispose();
            navBrush.Dispose();
            titleFont.Dispose();
            titleBrush.Dispose();
        }

        private void DrawDayHeaders(Graphics g, int width, int y)
        {
            int cols = _showWeekNumbers ? 8 : 7;
            float cellW = (float)width / cols;
            var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            var brush = new SolidBrush(SecondaryText);

            int startCol = 0;
            if (_showWeekNumbers)
            {
                // Week number column header
                g.DrawString("Wk", font, brush, 4, y + 3);
                startCol = 1;
            }

            string[] dayNames = GetDayNames();
            for (int i = 0; i < 7; i++)
            {
                float x = (startCol + i) * cellW;
                var size = g.MeasureString(dayNames[i], font);
                g.DrawString(dayNames[i], font, brush, x + (cellW - size.Width) / 2, y + 3);
            }

            // Bottom line
            using (var pen = new Pen(GridLineColor, 1))
                g.DrawLine(pen, 0, y + DAY_HEADER_HEIGHT - 1, width, y + DAY_HEADER_HEIGHT - 1);

            font.Dispose();
            brush.Dispose();
        }

        private void DrawDateGrid(Graphics g, int width, int startY, int gridHeight)
        {
            int cols = _showWeekNumbers ? 8 : 7;
            float cellW = (float)width / cols;

            int daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
            DateTime firstDay = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
            int firstDayCol = GetDayColumn(firstDay.DayOfWeek);

            // Number of rows needed
            int totalCells = firstDayCol + daysInMonth;
            int rows = (int)Math.Ceiling(totalCells / 7.0);
            float cellH = (float)gridHeight / rows;

            var dateFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            var dateBoldFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            var markerFont = new Font("Segoe UI", 6f, FontStyle.Bold);
            var today = DateTime.Today;

            for (int row = 0; row < rows; row++)
            {
                int weekNumCol = 0;

                // Draw week number if enabled
                if (_showWeekNumbers)
                {
                    int dayOfRow = row * 7 - firstDayCol + 1;
                    if (dayOfRow < 1) dayOfRow = 1;
                    if (dayOfRow > daysInMonth) dayOfRow = daysInMonth;
                    var dateInRow = new DateTime(_displayMonth.Year, _displayMonth.Month, dayOfRow);
                    int weekNum = CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                        dateInRow, CalendarWeekRule.FirstFourDayWeek, (DayOfWeek)_firstDayOfWeek);

                    float wnX = 0;
                    float wnY = startY + row * cellH;
                    using (var wnBrush = new SolidBrush(WeekNumColor))
                    using (var wnFont = new Font("Segoe UI", 7f, FontStyle.Regular))
                    {
                        var wnSize = g.MeasureString(weekNum.ToString(), wnFont);
                        g.DrawString(weekNum.ToString(), wnFont, wnBrush,
                            wnX + (cellW - wnSize.Width) / 2, wnY + (cellH - wnSize.Height) / 2);
                    }
                    weekNumCol = 1;
                }

                for (int col = 0; col < 7; col++)
                {
                    int cellIndex = row * 7 + col;
                    int dayNum = cellIndex - firstDayCol + 1;

                    float x = (weekNumCol + col) * cellW;
                    float y = startY + row * cellH;
                    var cellRect = new RectangleF(x, y, cellW, cellH);

                    if (dayNum < 1 || dayNum > daysInMonth)
                        continue; // Empty cell

                    var cellDate = new DateTime(_displayMonth.Year, _displayMonth.Month, dayNum);
                    string dateKey = cellDate.ToString("yyyy-MM-dd");
                    bool isToday = cellDate == today;
                    bool hasEntries = _datesWithEntries.Contains(dateKey);
                    bool isHovered = _hoveredDate.HasValue && _hoveredDate.Value == cellDate;
                    bool isSelected = _selectedDate.HasValue && _selectedDate.Value == cellDate;

                    // ── Cell background ──
                    if (isToday)
                    {
                        // Today: rounded green pill
                        using (var todayBrush = new SolidBrush(TodayBg))
                        {
                            var pill = new RectangleF(x + 2, y + 1, cellW - 4, cellH - 2);
                            FillRoundedRect(g, todayBrush, pill, 4);
                        }
                    }
                    else if (isSelected)
                    {
                        using (var selBrush = new SolidBrush(SelectedBg))
                        {
                            var pill = new RectangleF(x + 2, y + 1, cellW - 4, cellH - 2);
                            FillRoundedRect(g, selBrush, pill, 4);
                        }
                    }
                    else if (isHovered)
                    {
                        using (var hoverBrush = new SolidBrush(HoverBg))
                        {
                            var pill = new RectangleF(x + 2, y + 1, cellW - 4, cellH - 2);
                            FillRoundedRect(g, hoverBrush, pill, 4);
                        }
                    }

                    // ── Day number ──
                    string dayStr = dayNum.ToString();
                    Color dayColor = isToday ? TodayText : TextColor;
                    var font = isToday ? dateBoldFont : dateFont;
                    using (var dayBrush = new SolidBrush(dayColor))
                    {
                        var sz = g.MeasureString(dayStr, font);
                        g.DrawString(dayStr, font, dayBrush,
                            x + (cellW - sz.Width) / 2, y + (cellH - sz.Height) / 2 - (hasEntries ? 4 : 0));
                    }

                    // ── Entry marker dot ──
                    if (hasEntries)
                    {
                        float dotSize = 5;
                        float dotX = x + (cellW - dotSize) / 2;
                        float dotY = y + cellH - dotSize - 3;
                        using (var dotBrush = new SolidBrush(isToday ? Color.White : EntryMarkerColor))
                            g.FillEllipse(dotBrush, dotX, dotY, dotSize, dotSize);
                    }
                }
            }

            dateFont.Dispose();
            dateBoldFont.Dispose();
            markerFont.Dispose();
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  MOUSE HANDLING
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Handle single-click: navigation buttons, date selection, or month jump.</summary>
        private void CalendarPanel_MouseClick(object sender, MouseEventArgs e)
        {
//             DebugLogger.Log($"[Calendar] Mouse click at ({e.X}, {e.Y})");

            // Check if click is in header area (navigation or month label)
            if (e.Y < HEADER_HEIGHT)
            {
                // Left arrow: previous month
                if (e.X < NAV_BTN_SIZE + 10)
                {
                    _displayMonth = _displayMonth.AddMonths(-1);
//                     DebugLogger.Log($"[Calendar] Navigated to previous month: {_displayMonth:yyyy-MM}");
                    RefreshEntryMarkers();
                    return;
                }
                // Right arrow: next month
                if (e.X > this.Width - NAV_BTN_SIZE - 10)
                {
                    _displayMonth = _displayMonth.AddMonths(1);
//                     DebugLogger.Log($"[Calendar] Navigated to next month: {_displayMonth:yyyy-MM}");
                    RefreshEntryMarkers();
                    return;
                }
                // Click on month name → go to today
//                 DebugLogger.Log("[Calendar] Month label clicked, jumping to today");
                GoToToday();
                RefreshEntryMarkers();
                return;
            }

            // Click on date cell
            var date = HitTestDate(e.X, e.Y);
            if (date.HasValue)
            {
                _selectedDate = date.Value;
//                 DebugLogger.Log($"[Calendar] Date selected: {date.Value:yyyy-MM-dd}");
                Invalidate();
                DateClicked?.Invoke(date.Value);
            }
            else
            {
//                 DebugLogger.Log("[Calendar] Click missed date cells");
            }
        }

        /// <summary>Handle double-click on date cell: trigger quick-add event.</summary>
        private void CalendarPanel_MouseDoubleClick(object sender, MouseEventArgs e)
        {
//             DebugLogger.Log($"[Calendar] Double-click at ({e.X}, {e.Y})");

            // Ignore if click is in header or day-of-week row
            if (e.Y < HEADER_HEIGHT + DAY_HEADER_HEIGHT)
            {
//                 DebugLogger.Log("[Calendar] Double-click in header area, ignored");
                return;
            }

            var date = HitTestDate(e.X, e.Y);
            if (date.HasValue)
            {
//                 DebugLogger.Log($"[Calendar] Quick-add triggered for {date.Value:yyyy-MM-dd}");
                DateDoubleClicked?.Invoke(date.Value);
            }
        }

        /// <summary>Handle mouse move: update hover state and tooltip.</summary>
        private void CalendarPanel_MouseMove(object sender, MouseEventArgs e)
        {
            var date = HitTestDate(e.X, e.Y);

            // Only redraw if hover state changed
            if (date != _hoveredDate)
            {
                _hoveredDate = date;
//                 DebugLogger.Log($"[Calendar] Hover changed to: {date?.ToString("yyyy-MM-dd") ?? "none"}");
                Invalidate();

                // Update tooltip based on hovered date
                if (date.HasValue)
                {
                    string dateKey = date.Value.ToString("yyyy-MM-dd");
                    if (_dateEntryCounts.ContainsKey(dateKey) && _dateEntryCounts[dateKey] > 0)
                    {
                        int count = _dateEntryCounts[dateKey];
                        string tooltipText = $"{date.Value:ddd, MMM d} — {count} item{(count > 1 ? "s" : "")}";
                        _tooltip.SetToolTip(this, tooltipText);
//                         DebugLogger.Log($"[Calendar] Tooltip: {tooltipText}");
                    }
                    else
                    {
                        string tooltipText = $"{date.Value:ddd, MMM d}";
                        _tooltip.SetToolTip(this, tooltipText);
                    }
                }
                else
                {
                    _tooltip.SetToolTip(this, "");
                }
            }
        }

        /// <summary>Hit-test: given pixel coords, return which date (if any) was hit.</summary>
        private DateTime? HitTestDate(int px, int py)
        {
            int headerTotal = HEADER_HEIGHT + DAY_HEADER_HEIGHT;

            // Click above grid area
            if (py < headerTotal)
            {
//                 DebugLogger.Log($"[Calendar] HitTest: click in header area (y={py})");
                return null;
            }

            int cols = _showWeekNumbers ? 8 : 7;
            float cellW = (float)this.Width / cols;
            int gridH = this.Height - headerTotal;

            // Calculate grid layout
            int daysInMonth = DateTime.DaysInMonth(_displayMonth.Year, _displayMonth.Month);
            DateTime firstDay = new DateTime(_displayMonth.Year, _displayMonth.Month, 1);
            int firstDayCol = GetDayColumn(firstDay.DayOfWeek);
            int totalCells = firstDayCol + daysInMonth;
            int rows = (int)Math.Ceiling(totalCells / 7.0);
            float cellH = (float)gridH / rows;

            // Calculate row/col from pixel position
            int weekNumCol = _showWeekNumbers ? 1 : 0;
            int col = (int)((px) / cellW) - weekNumCol;
            int row = (int)((py - headerTotal) / cellH);

            // Bounds check
            if (col < 0 || col >= 7 || row < 0 || row >= rows)
            {
//                 DebugLogger.Log($"[Calendar] HitTest: out of bounds (col={col}, row={row})");
                return null;
            }

            // Calculate day number
            int cellIndex = row * 7 + col;
            int dayNum = cellIndex - firstDayCol + 1;

            if (dayNum < 1 || dayNum > daysInMonth)
            {
//                 DebugLogger.Log($"[Calendar] HitTest: empty cell (dayNum={dayNum})");
                return null;
            }

            DateTime result = new DateTime(_displayMonth.Year, _displayMonth.Month, dayNum);
//             DebugLogger.Log($"[Calendar] HitTest: hit date {result:yyyy-MM-dd} at grid ({col},{row})");
            return result;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Get the column index (0-6) for a given day of week, respecting first-day setting.</summary>
        private int GetDayColumn(DayOfWeek dow)
        {
            int col = ((int)dow - _firstDayOfWeek + 7) % 7;
            return col;
        }

        /// <summary>Get abbreviated day names ordered by first-day-of-week setting.</summary>
        private string[] GetDayNames()
        {
            var names = new string[7];
            for (int i = 0; i < 7; i++)
            {
                var dow = (DayOfWeek)((_firstDayOfWeek + i) % 7);
                names[i] = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(int)dow]
                    .Substring(0, Math.Min(2, CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames[(int)dow].Length));
            }
            return names;
        }

        /// <summary>Fill a rounded rectangle with smooth corners.</summary>
        private void FillRoundedRect(Graphics g, Brush brush, RectangleF rect, int radius)
        {
            // Create rounded rectangle path
            using (var path = new GraphicsPath())
            {
                float d = radius * 2f;
                // Draw four corner arcs
                path.AddArc(rect.X, rect.Y, d, d, 180, 90);
                path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
                path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
                path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }
    }
}
