// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        OrganizerSettingsForm.cs                                     ║
// ║  PURPOSE:     SETTINGS DIALOG FOR CALENDAR / ORGANIZER MODULE              ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Drawing;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    /// <summary>
    /// Settings dialog for the Calendar/Organizer feature.
    /// Allows toggling calendar visibility, alarm sounds, first day of week, etc.
    /// </summary>
    public class OrganizerSettingsForm : Form
    {
        private OrganizerSettings _settings;
        private bool _isDarkMode;
        private CustomTheme _customTheme;

        // ── UI Controls — Calendar ──
        private CheckBox _chkShowCalendar;
        private CheckBox _chkSoundAlarm;
        private CheckBox _chkPopupOnStartup;
        private CheckBox _chkShowWeekNumbers;
        private CheckBox _chkCompactView;
        private ComboBox _cmbFirstDay;
        private NumericUpDown _nudSnoozeMins;
        private NumericUpDown _nudPopupWidth;
        private NumericUpDown _nudPopupHeight;
        private Button _btnSave;
        private Button _btnCancel;

        // ── UI Controls — Project Folder ──
        private TextBox _txtProjectPath;
        private Button _btnSetProjectPath;
        private CheckBox _chkAutoLoadProject;

        // ── UI Controls — Shared Folder ──
        private CheckBox _chkSharedEnabled;
        private TextBox _txtSharedPath;
        private Button _btnSetSharedPath;
        private CheckBox _chkAutoRefresh;
        private NumericUpDown _nudRefreshInterval;
        private CheckBox _chkAutoDownload;

        // ── Settings Objects ──
        private ProjectFolderSettings _projSettings;
        private SharedFolderSettings _sharedSettings;

        /// <summary>Raised when settings are saved, so the caller can apply changes.</summary>
        public event Action<OrganizerSettings> SettingsSaved;

        /// <summary>Raised when project folder settings change.</summary>
        public event Action<ProjectFolderSettings> ProjectSettingsSaved;

        /// <summary>Raised when shared folder settings change.</summary>
        public event Action<SharedFolderSettings> SharedSettingsSaved;

        /// <summary>
        /// Initializes the Settings form with theme and current settings.
        /// Loads calendar, project folder, and shared folder settings.
        /// </summary>
        public OrganizerSettingsForm(bool isDarkMode, CustomTheme customTheme = null)
        {
//             DebugLogger.Log("[OrganizerSettings] Constructor: Initializing settings form, dark mode: " + isDarkMode);

            _isDarkMode = isDarkMode;
            _customTheme = customTheme;
            _settings = OrganizerStorage.LoadSettings();

            this.Text = "⚙ WorkFlow Settings";
            this.Size = new Size(440, 750);
            this.AutoScroll = true;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.KeyPreview = true;
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DebugLogger.Log("[OrganizerSettings] Escape key pressed, closing"); this.Close(); } };

//             DebugLogger.Log("[OrganizerSettings] Constructor: Building UI");
            BuildUI();
            ApplyTheme();
            LoadValues();

//             DebugLogger.Log("[OrganizerSettings] Constructor: Form initialization complete");
        }

        private void BuildUI()
        {
            int y = 16;
            int x = 20;
            int rowH = 32;

            var titleLabel = new Label
            {
                Text = "📆  Calendar & Organizer Settings",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(titleLabel);
            y += 36;

            _chkShowCalendar = new CheckBox
            {
                Text = "Show Calendar Panel",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(_chkShowCalendar);
            y += rowH;

            _chkSoundAlarm = new CheckBox
            {
                Text = "Enable Sound Alarms",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(_chkSoundAlarm);
            y += rowH;

            _chkPopupOnStartup = new CheckBox
            {
                Text = "Show Today's Reminders on Startup",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(_chkPopupOnStartup);
            y += rowH;

            _chkShowWeekNumbers = new CheckBox
            {
                Text = "Show Week Numbers",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(_chkShowWeekNumbers);
            y += rowH;

            _chkCompactView = new CheckBox
            {
                Text = "Compact Calendar View",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(_chkCompactView);
            y += rowH + 4;

            // First day of week
            var firstDayLabel = new Label
            {
                Text = "First Day of Week:",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y + 3),
                AutoSize = true
            };
            this.Controls.Add(firstDayLabel);
            _cmbFirstDay = new ComboBox
            {
                Location = new Point(180, y),
                Width = 130,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9)
            };
            _cmbFirstDay.Items.AddRange(new[] { "Sunday", "Monday" });
            this.Controls.Add(_cmbFirstDay);
            y += rowH + 2;

            // Snooze duration
            var snoozeLabel = new Label
            {
                Text = "Default Snooze (min):",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y + 3),
                AutoSize = true
            };
            this.Controls.Add(snoozeLabel);
            _nudSnoozeMins = new NumericUpDown
            {
                Location = new Point(180, y),
                Width = 70,
                Minimum = 1,
                Maximum = 120,
                Font = new Font("Segoe UI", 9)
            };
            this.Controls.Add(_nudSnoozeMins);
            y += rowH + 2;

            // Popup size
            var popupSizeLabel = new Label
            {
                Text = "Popup Size (W × H):",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y + 3),
                AutoSize = true
            };
            this.Controls.Add(popupSizeLabel);
            _nudPopupWidth = new NumericUpDown
            {
                Location = new Point(180, y),
                Width = 60,
                Minimum = 600,
                Maximum = 1600,
                Increment = 50,
                Font = new Font("Segoe UI", 9)
            };
            this.Controls.Add(_nudPopupWidth);
            var xLabel = new Label { Text = "×", Location = new Point(245, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9) };
            this.Controls.Add(xLabel);
            _nudPopupHeight = new NumericUpDown
            {
                Location = new Point(260, y),
                Width = 60,
                Minimum = 400,
                Maximum = 1200,
                Increment = 50,
                Font = new Font("Segoe UI", 9)
            };
            this.Controls.Add(_nudPopupHeight);
            y += rowH + 16;

            // ═══════════════════════════════════════════════════════════
            //  SECTION 2: PROJECT FOLDER
            // ═══════════════════════════════════════════════════════════
            y += 10;
            var projTitle = new Label
            {
                Text = "📁  Project Folder",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(projTitle);
            y += 32;

            var projPathLabel = new Label
            {
                Text = "Project Folder Path:",
                Font = new Font("Segoe UI", 9),
                Location = new Point(x, y + 3),
                AutoSize = true
            };
            this.Controls.Add(projPathLabel);
            y += 22;

            _txtProjectPath = new TextBox
            {
                Location = new Point(x, y),
                Width = 280,
                ReadOnly = true,
                Font = new Font("Segoe UI", 9)
            };
            this.Controls.Add(_txtProjectPath);

            _btnSetProjectPath = new Button
            {
                Text = "Browse...",
                Location = new Point(310, y - 1),
                Width = 80,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            _btnSetProjectPath.FlatAppearance.BorderSize = 1;
            _btnSetProjectPath.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Project Folder";
                    if (fbd.ShowDialog() == DialogResult.OK)
                        _txtProjectPath.Text = fbd.SelectedPath;
                }
            };
            this.Controls.Add(_btnSetProjectPath);
            y += rowH;

            _chkAutoLoadProject = new CheckBox
            {
                Text = "Auto-load last project folder on startup",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(_chkAutoLoadProject);
            y += rowH + 4;

            // ═══════════════════════════════════════════════════════════
            //  SECTION 3: SHARED FOLDER
            // ═══════════════════════════════════════════════════════════
            y += 6;
            var sharedTitle = new Label
            {
                Text = "📁  Shared Team Folder",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(sharedTitle);
            y += 32;

            _chkSharedEnabled = new CheckBox
            {
                Text = "Enable Shared Folder Feature",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(_chkSharedEnabled);
            y += rowH;

            var sharedPathLabel = new Label
            {
                Text = "Local Shared Folder Path:",
                Font = new Font("Segoe UI", 9),
                Location = new Point(x, y + 3),
                AutoSize = true
            };
            this.Controls.Add(sharedPathLabel);
            y += 22;

            _txtSharedPath = new TextBox
            {
                Location = new Point(x, y),
                Width = 280,
                ReadOnly = true,
                Font = new Font("Segoe UI", 9)
            };
            this.Controls.Add(_txtSharedPath);

            _btnSetSharedPath = new Button
            {
                Text = "Browse...",
                Location = new Point(310, y - 1),
                Width = 80,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5f),
                Cursor = Cursors.Hand
            };
            _btnSetSharedPath.FlatAppearance.BorderSize = 1;
            _btnSetSharedPath.Click += (s, e) =>
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Shared Folder";
                    if (fbd.ShowDialog() == DialogResult.OK)
                        _txtSharedPath.Text = fbd.SelectedPath;
                }
            };
            this.Controls.Add(_btnSetSharedPath);
            y += rowH;

            _chkAutoRefresh = new CheckBox
            {
                Text = "Auto-refresh metadata",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(_chkAutoRefresh);
            y += rowH;

            var refreshLabel = new Label
            {
                Text = "Refresh interval (sec):",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y + 3),
                AutoSize = true
            };
            this.Controls.Add(refreshLabel);
            _nudRefreshInterval = new NumericUpDown
            {
                Location = new Point(200, y),
                Width = 70,
                Minimum = 10,
                Maximum = 300,
                Increment = 5,
                Font = new Font("Segoe UI", 9)
            };
            this.Controls.Add(_nudRefreshInterval);
            y += rowH;

            _chkAutoDownload = new CheckBox
            {
                Text = "Auto-download new shared files",
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(_chkAutoDownload);
            y += rowH + 16;

            // ═══════════════════════════════════════════════════════════
            //  BUTTONS
            // ═══════════════════════════════════════════════════════════
            _btnSave = new Button
            {
                Text = "💾 Save All",
                Location = new Point(x, y),
                Width = 120,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(34, 197, 94),
                Cursor = Cursors.Hand
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += BtnSave_Click;
            this.Controls.Add(_btnSave);

            _btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(150, y),
                Width = 90,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            _btnCancel.FlatAppearance.BorderSize = 1;
            _btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(_btnCancel);
        }

        /// <summary>
        /// Loads all settings from storage and populates form controls.
        /// Handles calendar, project folder, and shared folder settings.
        /// </summary>
        private void LoadValues()
        {
//             DebugLogger.Log("[OrganizerSettings] LoadValues: Loading all settings");

            // Calendar settings
            _chkShowCalendar.Checked = _settings.ShowCalendar;
            _chkSoundAlarm.Checked = _settings.SoundAlarmEnabled;
            _chkPopupOnStartup.Checked = _settings.PopupOnStartup;
            _chkShowWeekNumbers.Checked = _settings.ShowWeekNumbers;
            _chkCompactView.Checked = _settings.CompactView;
            _cmbFirstDay.SelectedIndex = _settings.FirstDayOfWeek; // 0=Sun, 1=Mon
            _nudSnoozeMins.Value = _settings.DefaultSnoozeMins;
            _nudPopupWidth.Value = _settings.PopupWidth;
            _nudPopupHeight.Value = _settings.PopupHeight;

//             DebugLogger.Log("[OrganizerSettings] LoadValues: Calendar settings loaded");

            // Project folder settings
            _projSettings = ProjectFolderSettings.Load();
            _txtProjectPath.Text = _projSettings.FolderPath;
            _chkAutoLoadProject.Checked = _projSettings.AutoLoadOnStartup;

//             DebugLogger.Log($"[OrganizerSettings] LoadValues: Project folder: {_projSettings.FolderPath}");

            // Shared folder settings
            _sharedSettings = SharedFolderSettings.LoadSettings();
            _chkSharedEnabled.Checked = _sharedSettings.Enabled;
            _txtSharedPath.Text = _sharedSettings.LocalSharedFolderPath;
            _chkAutoRefresh.Checked = _sharedSettings.AutoRefreshEnabled;
            _nudRefreshInterval.Value = Math.Max(10, Math.Min(300, _sharedSettings.RefreshIntervalSeconds));
            _chkAutoDownload.Checked = _sharedSettings.AutoDownload;

//             DebugLogger.Log($"[OrganizerSettings] LoadValues: Shared folder: {_sharedSettings.LocalSharedFolderPath}, enabled: {_sharedSettings.Enabled}");
        }

        /// <summary>
        /// Saves all settings from form controls back to storage.
        /// Raises events for calendar, project, and shared folder settings.
        /// </summary>
        private void BtnSave_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[OrganizerSettings] BtnSave_Click: Saving all settings");

            // ── Save Calendar Settings ──
            _settings.ShowCalendar = _chkShowCalendar.Checked;
            _settings.SoundAlarmEnabled = _chkSoundAlarm.Checked;
            _settings.PopupOnStartup = _chkPopupOnStartup.Checked;
            _settings.ShowWeekNumbers = _chkShowWeekNumbers.Checked;
            _settings.CompactView = _chkCompactView.Checked;
            _settings.FirstDayOfWeek = _cmbFirstDay.SelectedIndex;
            _settings.DefaultSnoozeMins = (int)_nudSnoozeMins.Value;
            _settings.PopupWidth = (int)_nudPopupWidth.Value;
            _settings.PopupHeight = (int)_nudPopupHeight.Value;
            OrganizerStorage.SaveSettings(_settings);
//             DebugLogger.Log("[OrganizerSettings] BtnSave_Click: Calendar settings saved");
            SettingsSaved?.Invoke(_settings);

            // ── Save Project Folder Settings ──
            if (_projSettings == null) _projSettings = new ProjectFolderSettings();
            _projSettings.FolderPath = _txtProjectPath.Text;
            _projSettings.AutoLoadOnStartup = _chkAutoLoadProject.Checked;
            if (!string.IsNullOrEmpty(_projSettings.FolderPath))
                _projSettings.LastOpenedPath = _projSettings.FolderPath;
            _projSettings.Save();
//             DebugLogger.Log($"[OrganizerSettings] BtnSave_Click: Project folder settings saved - {_projSettings.FolderPath}");
            ProjectSettingsSaved?.Invoke(_projSettings);

            // ── Save Shared Folder Settings ──
            if (_sharedSettings == null) _sharedSettings = new SharedFolderSettings();
            _sharedSettings.Enabled = _chkSharedEnabled.Checked;
            _sharedSettings.LocalSharedFolderPath = _txtSharedPath.Text;
            _sharedSettings.AutoRefreshEnabled = _chkAutoRefresh.Checked;
            _sharedSettings.RefreshIntervalSeconds = (int)_nudRefreshInterval.Value;
            _sharedSettings.AutoDownload = _chkAutoDownload.Checked;
            SharedFolderSettings.SaveSettings(_sharedSettings);
//             DebugLogger.Log($"[OrganizerSettings] BtnSave_Click: Shared folder settings saved - enabled: {_sharedSettings.Enabled}");
            SharedSettingsSaved?.Invoke(_sharedSettings);

//             DebugLogger.Log("[OrganizerSettings] BtnSave_Click: All settings saved, closing form");
            this.Close();
        }

        /// <summary>
        /// Applies theme colors to all form controls.
        /// Uses custom theme if enabled, otherwise dark or light mode.
        /// </summary>
        private void ApplyTheme()
        {
//             DebugLogger.Log("[OrganizerSettings] ApplyTheme: Applying theme colors");

            Color bgMain, bgInput, fgMain;

            if (_customTheme?.Enabled == true)
            {
//                 DebugLogger.Log("[OrganizerSettings] ApplyTheme: Using custom theme");
                bgMain = _customTheme.GetBackground();
                bgInput = _customTheme.GetInput();
                fgMain = _customTheme.GetText();
            }
            else if (_isDarkMode)
            {
//                 DebugLogger.Log("[OrganizerSettings] ApplyTheme: Using dark mode");
                bgMain = Color.FromArgb(24, 28, 36);
                bgInput = Color.FromArgb(38, 44, 56);
                fgMain = Color.FromArgb(220, 224, 230);
            }
            else
            {
//                 DebugLogger.Log("[OrganizerSettings] ApplyTheme: Using light mode");
                bgMain = Color.FromArgb(245, 247, 250);
                bgInput = Color.White;
                fgMain = Color.FromArgb(30, 41, 59);
            }

            this.BackColor = bgMain;
            this.ForeColor = fgMain;

            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is TextBox tb) { tb.BackColor = bgInput; tb.ForeColor = fgMain; }
                else if (ctrl is ComboBox cb) { cb.BackColor = bgInput; cb.ForeColor = fgMain; }
                else if (ctrl is NumericUpDown nud) { nud.BackColor = bgInput; nud.ForeColor = fgMain; }
                else if (ctrl is CheckBox chk) { chk.ForeColor = fgMain; }
                else if (ctrl is Label lbl) { lbl.ForeColor = fgMain; }

                if (ctrl is Button btn && btn.BackColor != Color.FromArgb(34, 197, 94))
                {
                    btn.ForeColor = fgMain;
                    btn.FlatAppearance.BorderColor = fgMain;
                }
            }

//             DebugLogger.Log("[OrganizerSettings] ApplyTheme: Theme applied to all controls");
        }
    }
}
