// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        AlarmNotificationForm.cs                                     ║
// ║  PURPOSE:     ALARM / REMINDER POPUP NOTIFICATION FORM                     ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Small popup that appears when an organizer alarm fires.                   ║
// ║  Shows the entry title, time, category. User can dismiss or snooze.        ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    /// <summary>
    /// Alarm notification popup — appears in the bottom-right corner.
    /// User can Dismiss, Snooze, or Open the day organizer.
    /// Auto-closes after 30 seconds if no action taken.
    /// </summary>
    public class AlarmNotificationForm : Form
    {
        // ─── PRIVATE FIELDS ───
        private OrganizerEntry _entry;          // The alarm entry being notified
        private OrganizerSettings _settings;    // Organizer settings (snooze duration, etc.)
        private Timer _autoCloseTimer;          // Timer for auto-close after 30 seconds

        /// <summary>Raised when user clicks "Open" to view the day organizer.</summary>
        public event Action<DateTime> OpenDayRequested;

        /// <summary>
        /// Initializes the alarm notification form with entry data and theme.
        /// Positions in bottom-right corner and starts auto-close timer.
        /// </summary>
        public AlarmNotificationForm(OrganizerEntry entry, bool isDarkMode, CustomTheme customTheme = null)
        {
//             DebugLogger.Log("[AlarmNotification] Constructor called — entry=" + (entry?.Title ?? "null"));

            _entry = entry;
            _settings = OrganizerStorage.LoadSettings();

            // ─── FORM STYLING ───
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(340, 160);
            this.Opacity = 0.96;

            // ─── POSITION: BOTTOM-RIGHT ABOVE TASKBAR ───
            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Right - this.Width - 16, screen.Bottom - this.Height - 16);
//             DebugLogger.Log("[AlarmNotification] Positioned at " + this.Location.X + "," + this.Location.Y);

            BuildUI(isDarkMode, customTheme);

            // ─── AUTO-CLOSE AFTER 30 SECONDS ───
            _autoCloseTimer = new Timer { Interval = 30000 };
            _autoCloseTimer.Tick += (s, e) =>
            {
//                 DebugLogger.Log("[AlarmNotification] Auto-close timer fired");
                _autoCloseTimer.Stop();
                this.Close();
            };
            _autoCloseTimer.Start();
//             DebugLogger.Log("[AlarmNotification] Constructor complete — auto-close timer started");
        }

        /// <summary>
        /// Builds the UI controls for the notification:
        /// - Alarm icon + entry title
        /// - Category and time range
        /// - Description (truncated)
        /// - Three action buttons: Dismiss, Snooze, Open
        /// </summary>
        private void BuildUI(bool isDarkMode, CustomTheme customTheme)
        {
//             DebugLogger.Log("[AlarmNotification] BuildUI called — isDarkMode=" + isDarkMode +
//                            ", customTheme=" + (customTheme?.Enabled ?? false));

            // ─── DETERMINE COLORS ───
            Color bgColor = isDarkMode ? Color.FromArgb(30, 36, 46) : Color.White;
            Color textColor = isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59);
            Color secondaryColor = isDarkMode ? Color.FromArgb(120, 130, 145) : Color.FromArgb(100, 116, 139);
            Color accentColor = Color.FromArgb(255, 127, 80);

            // Use custom theme if enabled
            if (customTheme?.Enabled == true)
            {
                bgColor = customTheme.GetCard();
                textColor = customTheme.GetText();
                secondaryColor = customTheme.GetSecondaryText();
                accentColor = customTheme.GetAccent();
//                 DebugLogger.Log("[AlarmNotification] Applied custom theme colors");
            }

            this.BackColor = bgColor;

            // ─── ALARM ICON (⏰) ───
            var lblIcon = new Label
            {
                Text = "⏰",
                Font = new Font("Segoe UI", 20),
                Location = new Point(12, 12),
                AutoSize = true
            };
            this.Controls.Add(lblIcon);

            // ─── ENTRY TITLE ───
            var lblTitle = new Label
            {
                Text = _entry.Title,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = textColor,
                Location = new Point(52, 14),
                AutoSize = true,
                MaximumSize = new Size(270, 0)
            };
            this.Controls.Add(lblTitle);
//             DebugLogger.Log("[AlarmNotification] Added title label: " + _entry.Title);

            // ─── TIME RANGE + CATEGORY ───
            string timeStr = !string.IsNullOrEmpty(_entry.TimeFrom) ? _entry.TimeFrom : "";
            if (!string.IsNullOrEmpty(_entry.TimeTo)) timeStr += $" – {_entry.TimeTo}";
            var lblDetails = new Label
            {
                Text = $"{_entry.Category}  •  {timeStr}",
                Font = new Font("Segoe UI", 9),
                ForeColor = secondaryColor,
                Location = new Point(52, 40),
                AutoSize = true
            };
            this.Controls.Add(lblDetails);

            // ─── DESCRIPTION (TRUNCATED IF TOO LONG) ───
            if (!string.IsNullOrEmpty(_entry.Description))
            {
                string desc = _entry.Description.Length > 80
                    ? _entry.Description.Substring(0, 80) + "..."
                    : _entry.Description;
                var lblDesc = new Label
                {
                    Text = desc,
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = secondaryColor,
                    Location = new Point(52, 60),
                    AutoSize = true,
                    MaximumSize = new Size(270, 40)
                };
                this.Controls.Add(lblDesc);
            }

            // ─── BUTTONS: DISMISS | SNOOZE | OPEN ───
            int btnY = 118;

            // DISMISS BUTTON
            var btnDismiss = new Button
            {
                Text = "Dismiss",
                Location = new Point(12, btnY),
                Width = 80,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = textColor,
                BackColor = bgColor,
                Cursor = Cursors.Hand
            };
            btnDismiss.FlatAppearance.BorderColor = secondaryColor;
            btnDismiss.Click += (s, e) =>
            {
//                 DebugLogger.Log("[AlarmNotification] Dismiss button clicked");
                OrganizerStorage.MarkAlarmFired(_entry.Id);
                this.Close();
            };
            this.Controls.Add(btnDismiss);

            // SNOOZE BUTTON
            var btnSnooze = new Button
            {
                Text = $"Snooze {_settings.DefaultSnoozeMins}m",
                Location = new Point(100, btnY),
                Width = 100,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(234, 179, 8),
                Cursor = Cursors.Hand
            };
            btnSnooze.FlatAppearance.BorderSize = 0;
            btnSnooze.Click += (s, e) =>
            {
//                 DebugLogger.Log("[AlarmNotification] Snooze button clicked — mins=" + _settings.DefaultSnoozeMins);
                OrganizerStorage.SnoozeAlarm(_entry.Id, _settings.DefaultSnoozeMins);
                this.Close();
            };
            this.Controls.Add(btnSnooze);

            // OPEN BUTTON
            var btnOpen = new Button
            {
                Text = "📆 Open",
                Location = new Point(208, btnY),
                Width = 80,
                Height = 30,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = accentColor,
                Cursor = Cursors.Hand
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += (s, e) =>
            {
//                 DebugLogger.Log("[AlarmNotification] Open button clicked — date=" + _entry.Date);
                OrganizerStorage.MarkAlarmFired(_entry.Id);
                if (DateTime.TryParse(_entry.Date, out var dt))
                {
                    OpenDayRequested?.Invoke(dt);
//                     DebugLogger.Log("[AlarmNotification] OpenDayRequested event raised for " + dt.ToShortDateString());
                }
                this.Close();
            };
            this.Controls.Add(btnOpen);

            // ─── BORDER PAINT (ACCENT COLOR FRAME) ───
            this.Paint += (s, e) =>
            {
                using (var pen = new Pen(accentColor, 2))
                    e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
            };

//             DebugLogger.Log("[AlarmNotification] BuildUI complete");
        }

        /// <summary>
        /// Cleanup: Stops and disposes the auto-close timer when form is closed.
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
//             DebugLogger.Log("[AlarmNotification] OnFormClosed — cleaning up timer");
            _autoCloseTimer?.Stop();
            _autoCloseTimer?.Dispose();
            base.OnFormClosed(e);
//             DebugLogger.Log("[AlarmNotification] Form closed");
        }
    }
}
