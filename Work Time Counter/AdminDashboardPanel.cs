// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        AdminDashboardPanel.cs                                       ║
// ║  PURPOSE:     ADMIN DASHBOARD WITH HOURS-PER-USER BAR CHART                ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;

namespace Work_Time_Counter
{
    /// <summary>
    /// A Panel subclass that displays a visual hours summary dashboard for admins.
    /// Shows a horizontal bar chart with total hours per user for the current week.
    /// </summary>
    public class AdminDashboardPanel : Panel
    {
        // ─── PRIVATE FIELDS ───
        private List<LogEntryWithIndex> _logs;           // Work time log entries
        private List<string> _userNames;                 // List of team members
        private bool _isDarkMode;                         // Current theme mode
        private Dictionary<string, System.Drawing.Color> _userColors; // User → Color mapping

        // ─── THEME COLORS ───
        private System.Drawing.Color _backgroundColor;   // Panel background
        private System.Drawing.Color _textColor;         // Text labels
        private System.Drawing.Color _gridColor;         // Bar background color

        /// <summary>
        /// Initializes the admin dashboard panel with theme settings.
        /// Sets up double buffering and Paint handler for rendering the bar chart.
        /// </summary>
        public AdminDashboardPanel(bool isDarkMode)
        {
//             DebugLogger.Log("[AdminDashboard] Constructor called — isDarkMode=" + isDarkMode);

            _isDarkMode = isDarkMode;
            _logs = new List<LogEntryWithIndex>();
            _userNames = new List<string>();
            _userColors = new Dictionary<string, System.Drawing.Color>();

            this.DoubleBuffered = true;
            this.Resize += (s, e) => this.Invalidate();
            this.Paint += AdminDashboardPanel_Paint;

            ApplyTheme(isDarkMode);
//             DebugLogger.Log("[AdminDashboard] Constructor complete");
        }

        /// <summary>
        /// Loads work time logs and user names, then assigns distinct colors to each user.
        /// Triggers a repaint to display the updated bar chart.
        /// </summary>
        public void LoadData(List<LogEntryWithIndex> logs, List<string> userNames)
        {
//             DebugLogger.Log("[AdminDashboard] LoadData called — logs=" + (logs?.Count ?? 0) +
//                            ", users=" + (userNames?.Count ?? 0));

            _logs = logs ?? new List<LogEntryWithIndex>();
            _userNames = userNames ?? new List<string>();

            // ─── ASSIGN DISTINCT COLORS TO EACH USER ───
            // Use a predefined palette cycling through if more than 8 users
            _userColors.Clear();
            var colors = new System.Drawing.Color[]
            {
                System.Drawing.Color.FromArgb(52, 152, 219),    // Blue
                System.Drawing.Color.FromArgb(46, 204, 113),    // Green
                System.Drawing.Color.FromArgb(231, 76, 60),     // Red
                System.Drawing.Color.FromArgb(155, 89, 182),    // Purple
                System.Drawing.Color.FromArgb(241, 196, 15),    // Orange
                System.Drawing.Color.FromArgb(26, 188, 156),    // Turquoise
                System.Drawing.Color.FromArgb(230, 126, 34),    // Dark Orange
                System.Drawing.Color.FromArgb(149, 165, 166),   // Gray
            };

            for (int i = 0; i < _userNames.Count; i++)
            {
                _userColors[_userNames[i]] = colors[i % colors.Length];
//                 DebugLogger.Log("[AdminDashboard] Assigned color to user: " + _userNames[i]);
            }

            this.Invalidate();
//             DebugLogger.Log("[AdminDashboard] LoadData complete — invalidated for repaint");
        }

        /// <summary>
        /// Updates the theme colors based on dark/light mode and triggers a repaint.
        /// </summary>
        public void ApplyTheme(bool isDarkMode)
        {
//             DebugLogger.Log("[AdminDashboard] ApplyTheme called — isDarkMode=" + isDarkMode);

            _isDarkMode = isDarkMode;

            if (isDarkMode)
            {
                // Dark mode: light text on dark background
                _backgroundColor = System.Drawing.Color.FromArgb(30, 30, 30);
                _textColor = System.Drawing.Color.FromArgb(220, 220, 220);
                _gridColor = System.Drawing.Color.FromArgb(60, 60, 60);
                this.BackColor = _backgroundColor;
//                 DebugLogger.Log("[AdminDashboard] Applied dark theme colors");
            }
            else
            {
                // Light mode: dark text on white background
                _backgroundColor = System.Drawing.Color.White;
                _textColor = System.Drawing.Color.FromArgb(40, 40, 40);
                _gridColor = System.Drawing.Color.FromArgb(200, 200, 200);
                this.BackColor = _backgroundColor;
//                 DebugLogger.Log("[AdminDashboard] Applied light theme colors");
            }

            this.Invalidate();
        }

        /// <summary>
        /// Paint event handler: Renders the bar chart showing weekly hours per user.
        /// Called whenever the control needs repainting (resize, Invalidate, etc.).
        /// </summary>
        private void AdminDashboardPanel_Paint(object sender, PaintEventArgs e)
        {
//             DebugLogger.Log("[AdminDashboard] Paint event — redrawing dashboard");
            e.Graphics.Clear(_backgroundColor);

            // Handle no data case
            if (_logs.Count == 0 || _userNames.Count == 0)
            {
//                 DebugLogger.Log("[AdminDashboard] No data available — displaying empty message");
                DrawNoData(e);
                return;
            }

            // ─── CALCULATE WEEK BOUNDARIES (MONDAY TO SUNDAY) ───
            var now = DateTime.Now;
            var currentWeekStart = now.AddDays(-(int)now.DayOfWeek + 1);
            var currentWeekEnd = currentWeekStart.AddDays(6);
//             DebugLogger.Log("[AdminDashboard] Week range: " + currentWeekStart.Date.ToShortDateString() +
//                            " to " + currentWeekEnd.Date.ToShortDateString());

            // ─── AGGREGATE HOURS PER USER FOR CURRENT WEEK ───
            var userHours = new Dictionary<string, double>();
            foreach (var userName in _userNames)
            {
                userHours[userName] = 0;
            }

            foreach (var log in _logs)
            {
                if (!DateTime.TryParse(log.timestamp, out var logDate))
                    continue;

                // Filter by current week
                if (logDate.Date >= currentWeekStart.Date && logDate.Date <= currentWeekEnd.Date)
                {
                    if (userHours.ContainsKey(log.userName))
                    {
                        var hours = ParseHours(log.workingTime);
                        userHours[log.userName] += hours;
                    }
                }
            }

//             DebugLogger.Log("[AdminDashboard] Aggregated hours — users with data: " + userHours.Count);
            DrawChart(e, userHours);
        }

        /// <summary>
        /// Draws centered "No data available" message when dashboard is empty.
        /// </summary>
        private void DrawNoData(PaintEventArgs e)
        {
//             DebugLogger.Log("[AdminDashboard] DrawNoData called");
            using (var font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Regular))
            using (var brush = new SolidBrush(_textColor))
            {
                var message = "No data available";
                var size = e.Graphics.MeasureString(message, font);
                var x = (this.Width - size.Width) / 2;
                var y = (this.Height - size.Height) / 2;
                e.Graphics.DrawString(message, font, brush, x, y);
            }
        }

        /// <summary>
        /// Renders horizontal bar chart with user hours.
        /// Shows title, sorted users (high to low), colored bars normalized to max value.
        /// </summary>
        private void DrawChart(PaintEventArgs e, Dictionary<string, double> userHours)
        {
//             DebugLogger.Log("[AdminDashboard] DrawChart called — userHours count=" + userHours.Count);

            var padding = 20;
            var titleHeight = 40;
            var barHeight = 30;
            var barSpacing = 10;
            var leftLabelWidth = 100;
            var rightValueWidth = 60;

            var chartTop = padding + titleHeight;
            var availableHeight = this.Height - chartTop - padding;

            // ─── DRAW TITLE ───
            using (var titleFont = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold))
            using (var titleBrush = new SolidBrush(_textColor))
            {
                e.Graphics.DrawString("📊 Weekly Hours Summary", titleFont, titleBrush, padding, padding);
            }

            // ─── FILTER AND SORT USERS BY HOURS (DESCENDING) ───
            var usersWithData = userHours
                .Where(kvp => kvp.Value > 0)
                .OrderByDescending(kvp => kvp.Value)
                .ToList();

            if (usersWithData.Count == 0)
            {
//                 DebugLogger.Log("[AdminDashboard] No users with hours data");
                DrawNoData(e);
                return;
            }

            // ─── CALCULATE NORMALIZATION FACTOR (MAX HOURS) ───
            var maxHours = usersWithData.Max(kvp => kvp.Value);
            if (maxHours <= 0)
                maxHours = 1;

//             DebugLogger.Log("[AdminDashboard] MaxHours=" + maxHours + ", UsersCount=" + usersWithData.Count);

            var barWidth = this.Width - leftLabelWidth - rightValueWidth - padding * 2;
            var currentY = chartTop;

            // ─── DRAW EACH USER'S BAR ───
            foreach (var userHour in usersWithData)
            {
                // Check if bar fits on screen
                if (currentY + barHeight > this.Height - padding)
                {
//                     DebugLogger.Log("[AdminDashboard] Out of vertical space, stopping bar render");
                    break;
                }

                var userName = userHour.Key;
                var hours = userHour.Value;
                var barColor = _userColors.ContainsKey(userName) ? _userColors[userName] : System.Drawing.Color.LightGray;

//                 DebugLogger.Log("[AdminDashboard] Drawing bar for " + userName + " — hours=" + hours);

                // ─── DRAW USER NAME LABEL (RIGHT-ALIGNED) ───
                using (var labelFont = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Regular))
                using (var labelBrush = new SolidBrush(_textColor))
                {
                    var labelRect = new System.Drawing.Rectangle(padding, currentY, leftLabelWidth - 10, barHeight);
                    var stringFormat = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(userName, labelFont, labelBrush, labelRect, stringFormat);
                }

                // ─── CALCULATE BAR LENGTH PROPORTIONAL TO MAX ───
                var normalizedHours = hours / maxHours;
                var filledBarWidth = (int)(barWidth * normalizedHours);

                // ─── DRAW BACKGROUND BAR (CONTAINER) ───
                var bgBarRect = new System.Drawing.Rectangle(
                    padding + leftLabelWidth,
                    currentY + 5,
                    barWidth,
                    barHeight - 10);

                using (var bgBrush = new SolidBrush(_gridColor))
                {
                    e.Graphics.FillRectangle(bgBrush, bgBarRect);
                }

                // ─── DRAW FILLED BAR (COLORED) ───
                if (filledBarWidth > 0)
                {
                    var filledBarRect = new System.Drawing.Rectangle(
                        padding + leftLabelWidth,
                        currentY + 5,
                        filledBarWidth,
                        barHeight - 10);

                    using (var barBrush = new SolidBrush(barColor))
                    {
                        e.Graphics.FillRectangle(barBrush, filledBarRect);
                    }
                }

                // ─── DRAW BORDER AROUND BAR ───
                using (var borderPen = new Pen(_textColor, 1))
                {
                    e.Graphics.DrawRectangle(borderPen, bgBarRect);
                }

                // ─── DRAW HOURS VALUE ON THE RIGHT ───
                using (var valueFont = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Regular))
                using (var valueBrush = new SolidBrush(_textColor))
                {
                    var valueText = $"{hours:F1}h";
                    var valueRect = new System.Drawing.Rectangle(
                        padding + leftLabelWidth + barWidth + 5,
                        currentY,
                        rightValueWidth - 10,
                        barHeight);
                    var stringFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(valueText, valueFont, valueBrush, valueRect, stringFormat);
                }

                currentY += barHeight + barSpacing;
            }
        }

        /// <summary>
        /// Parses a working time string to hours as a double.
        /// Expected format: "2h 30m", "1h", "45m", etc.
        /// Returns 0 if parsing fails or string is null/empty.
        /// </summary>
        private double ParseHours(string workingTime)
        {
            if (string.IsNullOrWhiteSpace(workingTime))
            {
//                 DebugLogger.Log("[AdminDashboard] ParseHours — empty/null input");
                return 0;
            }

            var hours = 0;
            var minutes = 0;

            // Split by spaces to extract "Xh" and "Ym" components
            var parts = workingTime.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                if (part.EndsWith("h"))
                {
                    // Parse hours component (e.g., "2h")
                    if (int.TryParse(part.TrimEnd('h'), out var h))
                        hours = h;
                }
                else if (part.EndsWith("m"))
                {
                    // Parse minutes component (e.g., "30m")
                    if (int.TryParse(part.TrimEnd('m'), out var m))
                        minutes = m;
                }
            }

            double result = hours + (minutes / 60.0);
//             DebugLogger.Log("[AdminDashboard] ParseHours — input='" + workingTime + "' result=" + result);
            return result;
        }
    }
}
