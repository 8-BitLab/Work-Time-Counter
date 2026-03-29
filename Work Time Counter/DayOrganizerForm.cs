// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        DayOrganizerForm.cs                                          ║
// ║  PURPOSE:     POPUP FORM FOR DAILY ORGANIZER — FULL DAY VIEW               ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Large popup that opens when user clicks a day on the calendar.            ║
// ║  Shows all entries for that day: meetings, tasks, reminders, etc.          ║
// ║  Includes daily notes, entry management (add/edit/delete), alarm setup,    ║
// ║  search/filter, and status tracking.                                       ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    /// <summary>
    /// Full-day organizer popup form — manages entries for a selected date.
    /// </summary>
    public class DayOrganizerForm : Form
    {
        // ═══ STATE ═══
        private DateTime _selectedDate;
        private string _dateKey;
        private List<OrganizerEntry> _entries;
        private bool _isDarkMode;
        private CustomTheme _customTheme;
        private string _currentUser;

        // ═══ UI CONTROLS ═══
        private Label _lblDate;
        private Panel _headerPanel;
        private Panel _entryListPanel;
        private Panel _notesPanel;
        private Panel _detailPanel;
        private SplitContainer _splitMain;
        private SplitContainer _rightSplit;
        private RichTextBox _txtDailyNotes;
        private TextBox _txtSearch;
        private ComboBox _cmbCategoryFilter;
        private FlowLayoutPanel _entryCardsContainer;
        private Button _btnAdd;
        private Button _btnClose;
        private Label _lblSummary;

        // ── Entry detail/edit controls ──
        private TextBox _txtTitle;
        private TextBox _txtDescription;
        private ComboBox _cmbCategory;
        private ComboBox _cmbStatus;
        private ComboBox _cmbPriority;
        private DateTimePicker _dtpTimeFrom;
        private DateTimePicker _dtpTimeTo;
        private TextBox _txtLink;
        private CheckBox _chkAlarm;
        private DateTimePicker _dtpAlarmDate;
        private DateTimePicker _dtpAlarmTime;
        private ComboBox _cmbRecurrence;
        private Button _btnSave;
        private Button _btnCancel;
        private Button _btnDelete;
        private Button _btnSetAlarm;
        private OrganizerEntry _editingEntry = null; // null = adding new
        private bool _compactWindowLayout;

        /// <summary>
        /// Date currently shown by this organizer window.
        /// </summary>
        public DateTime SelectedDate => _selectedDate.Date;

        // ═══ CATEGORY COLORS ═══
        private static readonly Dictionary<OrganizerCategory, Color> CategoryColors = new Dictionary<OrganizerCategory, Color>
        {
            { OrganizerCategory.Meeting, Color.FromArgb(59, 130, 246) },    // Blue
            { OrganizerCategory.Interview, Color.FromArgb(168, 85, 247) },  // Purple
            { OrganizerCategory.Call, Color.FromArgb(34, 197, 94) },        // Green
            { OrganizerCategory.Task, Color.FromArgb(255, 127, 80) },       // Coral
            { OrganizerCategory.Reminder, Color.FromArgb(234, 179, 8) },    // Yellow
            { OrganizerCategory.Personal, Color.FromArgb(236, 72, 153) }    // Pink
        };

        private static readonly Dictionary<OrganizerCategory, string> CategoryIcons = new Dictionary<OrganizerCategory, string>
        {
            { OrganizerCategory.Meeting, "📅" },
            { OrganizerCategory.Interview, "🎤" },
            { OrganizerCategory.Call, "📞" },
            { OrganizerCategory.Task, "✅" },
            { OrganizerCategory.Reminder, "🔔" },
            { OrganizerCategory.Personal, "👤" }
        };

        // ═══════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════════
        /// <summary>Constructor: Initialize form for a specific date with theme and user info.</summary>
        public DayOrganizerForm(DateTime date, bool isDarkMode, CustomTheme customTheme = null, string currentUser = "")
        {
//             DebugLogger.Log($"[DayOrganizer] Opening form for {date:yyyy-MM-dd}");

            _selectedDate = date.Date;
            _dateKey = _selectedDate.ToString("yyyy-MM-dd");
            _isDarkMode = isDarkMode;
            _customTheme = customTheme;
            _currentUser = currentUser;

            // Load saved window size from settings. Older wide defaults are converted
            // to a taller phone-style popup that still remains freely resizable.
            var settings = OrganizerStorage.LoadSettings();
            int popupWidth = settings.PopupWidth;
            int popupHeight = settings.PopupHeight;
            if (popupWidth == 900 && popupHeight == 700)
            {
                popupWidth = 460;
                popupHeight = 820;
            }

            this.Size = new Size(popupWidth, popupHeight);
            this.MinimumSize = new Size(400, 620);

            // Configure window
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = $"Day Organizer — {_selectedDate:dddd, MMMM d, yyyy}";
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.KeyPreview = true;
            this.Padding = new Padding(1);

            // Wire up keyboard: ESC to close
            this.KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) { DebugLogger.Log("[DayOrganizer] ESC pressed, closing form"); this.Close(); } };

            // Initialize UI and data
            BuildUI();
            ApplyTheme();
            LoadData();
            UpdateResponsiveLayout();

            this.Resize += (s, e) => UpdateResponsiveLayout();
            this.ResizeEnd += (s, e) => PersistPopupSize();
            this.FormClosing += (s, e) => PersistPopupSize();

//             DebugLogger.Log("[DayOrganizer] Form initialization complete");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  BUILD UI
        // ═══════════════════════════════════════════════════════════════════════

        private void BuildUI()
        {
            this.SuspendLayout();

            // ── HEADER PANEL ──
            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 70,
                Padding = new Padding(20, 10, 20, 6)
            };

            // Use a TableLayoutPanel for header so badge flows after date text
            var headerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 2,
                AutoSize = false
            };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

            _lblDate = new Label
            {
                Text = $"📆  {_selectedDate:dddd, MMMM d, yyyy}",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 2, 8, 0)
            };
            headerLayout.Controls.Add(_lblDate, 0, 0);

            bool isToday = _selectedDate == DateTime.Today;
            if (isToday)
            {
                var todayBadge = new Label
                {
                    Text = " TODAY ",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = Color.FromArgb(34, 197, 94),
                    AutoSize = true,
                    Padding = new Padding(6, 3, 6, 3),
                    Margin = new Padding(0, 7, 0, 0)
                };
                headerLayout.Controls.Add(todayBadge, 1, 0);
            }

            _lblSummary = new Label
            {
                Font = new Font("Segoe UI", 9),
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            headerLayout.SetColumnSpan(_lblSummary, 3);
            headerLayout.Controls.Add(_lblSummary, 0, 1);

            _headerPanel.Controls.Add(headerLayout);
            this.Controls.Add(_headerPanel);

            // ── MAIN SPLIT: Left (entries) | Right (notes + detail) ──
            _splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 8,
                BorderStyle = BorderStyle.None,
                FixedPanel = FixedPanel.None
            };
            // Set splitter distance after adding to form (in Load event)
            _splitMain.SplitterDistance = 480;

            // ──── LEFT PANEL: Entry list ────
            var leftPanel = new Panel { Dock = DockStyle.Fill };

            // Toolbar: Search + Filter + Add — use TableLayoutPanel for responsive layout
            var toolbar = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(10, 8, 10, 6),
                ColumnCount = 3,
                RowCount = 1
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

            _txtSearch = new TextBox
            {
                Text = "Search entries...",
                ForeColor = SystemColors.GrayText,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                Margin = new Padding(0, 0, 6, 0)
            };
            _txtSearch.GotFocus += (s2, e2) => { if (_txtSearch.ForeColor == SystemColors.GrayText) { _txtSearch.Text = ""; _txtSearch.ForeColor = SystemColors.WindowText; } };
            _txtSearch.LostFocus += (s2, e2) => { if (string.IsNullOrWhiteSpace(_txtSearch.Text)) { _txtSearch.ForeColor = SystemColors.GrayText; _txtSearch.Text = "Search entries..."; } };
            _txtSearch.TextChanged += (s, e) => { if (_txtSearch.ForeColor != SystemColors.GrayText) FilterAndDisplayEntries(); };
            toolbar.Controls.Add(_txtSearch, 0, 0);

            _cmbCategoryFilter = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9),
                Margin = new Padding(0, 0, 6, 0)
            };
            _cmbCategoryFilter.Items.Add("All Categories");
            foreach (var cat in Enum.GetValues(typeof(OrganizerCategory)))
                _cmbCategoryFilter.Items.Add(cat);
            _cmbCategoryFilter.SelectedIndex = 0;
            _cmbCategoryFilter.SelectedIndexChanged += (s, e) => FilterAndDisplayEntries();
            toolbar.Controls.Add(_cmbCategoryFilter, 1, 0);

            _btnAdd = new Button
            {
                Text = "＋ Add Entry",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(34, 197, 94),
                Cursor = Cursors.Hand,
                Margin = new Padding(0)
            };
            _btnAdd.FlatAppearance.BorderSize = 0;
            _btnAdd.Click += BtnAdd_Click;
            toolbar.Controls.Add(_btnAdd, 2, 0);

            leftPanel.Controls.Add(toolbar);

            // Scrollable entry cards
            _entryCardsContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(10, 6, 10, 6)
            };
            leftPanel.Controls.Add(_entryCardsContainer);
            _entryCardsContainer.BringToFront();

            _splitMain.Panel1.Controls.Add(leftPanel);

            // ──── RIGHT PANEL: Split into Notes (top) + Detail/Edit (bottom) ────
            _rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 8,
                BorderStyle = BorderStyle.None,
                FixedPanel = FixedPanel.Panel1
            };
            _rightSplit.SplitterDistance = 200;

            // Notes panel (top-right)
            _notesPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8) };
            var notesLabel = new Label
            {
                Text = "📝 Daily Notes",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(0, 0, 0, 4)
            };
            _notesPanel.Controls.Add(notesLabel);

            _txtDailyNotes = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                AcceptsTab = true
            };
            _txtDailyNotes.TextChanged += (s, e) =>
            {
                if (_txtDailyNotes.Tag is Timer t) t.Stop();
                var saveTimer = new Timer { Interval = 1500 };
                saveTimer.Tick += (s2, e2) =>
                {
                    saveTimer.Stop();
                    saveTimer.Dispose();
                    OrganizerStorage.SaveDailyNotes(_dateKey, _txtDailyNotes.Text);
                };
                _txtDailyNotes.Tag = saveTimer;
                saveTimer.Start();
            };
            _notesPanel.Controls.Add(_txtDailyNotes);
            _txtDailyNotes.BringToFront();

            _rightSplit.Panel1.Controls.Add(_notesPanel);

            // Detail/Edit panel (bottom-right)
            _detailPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8), AutoScroll = true };
            BuildDetailPanel();
            _rightSplit.Panel2.Controls.Add(_detailPanel);

            _splitMain.Panel2.Controls.Add(_rightSplit);

            this.Controls.Add(_splitMain);
            _splitMain.BringToFront();

            this.ResumeLayout(true);
        }

        private void UpdateResponsiveLayout()
        {
            if (_splitMain == null || _rightSplit == null || IsDisposed)
                return;

            bool compact = ClientSize.Width < 760;
            _compactWindowLayout = compact;

            SuspendLayout();

            if (compact)
            {
                _splitMain.Orientation = Orientation.Horizontal;
                _splitMain.FixedPanel = FixedPanel.None;
                _splitMain.SplitterDistance = Math.Max(220, Math.Min(ClientSize.Height / 2, ClientSize.Height - 320));

                _rightSplit.Orientation = Orientation.Horizontal;
                _rightSplit.FixedPanel = FixedPanel.Panel1;
                _rightSplit.SplitterDistance = Math.Max(150, Math.Min(220, _splitMain.Panel2.Height / 3));

                _headerPanel.Height = 86;
            }
            else
            {
                _splitMain.Orientation = Orientation.Vertical;
                _splitMain.FixedPanel = FixedPanel.None;
                _splitMain.SplitterDistance = Math.Max(360, Math.Min(540, (int)(ClientSize.Width * 0.52)));

                _rightSplit.Orientation = Orientation.Horizontal;
                _rightSplit.FixedPanel = FixedPanel.Panel1;
                _rightSplit.SplitterDistance = 200;

                _headerPanel.Height = 70;
            }

            ResumeLayout(true);
        }

        private void PersistPopupSize()
        {
            if (WindowState != FormWindowState.Normal)
                return;

            var settings = OrganizerStorage.LoadSettings();
            if (settings.PopupWidth == Width && settings.PopupHeight == Height)
                return;

            settings.PopupWidth = Width;
            settings.PopupHeight = Height;
            OrganizerStorage.SaveSettings(settings);
        }

        /// <summary>Build the entry add/edit form in the detail panel using responsive TableLayoutPanel.</summary>
        private void BuildDetailPanel()
        {
            var formFont = new Font("Segoe UI", 9);
            var formFontInput = new Font("Segoe UI", 9.5f);
            var labelMargin = new Padding(0, 6, 6, 0);
            var inputMargin = new Padding(0, 2, 0, 4);

            // Main layout: 4 columns (Label, Input, Label, Input) — auto-rows
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 4,
                Padding = new Padding(0),
                Margin = new Padding(0)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));           // Label 1
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));        // Input 1
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));           // Label 2
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));        // Input 2

            int row = 0;

            // ── Title (spans full width) ──
            var titleLabel = new Label { Text = "Title:", Font = formFont, AutoSize = true, Margin = labelMargin };
            layout.Controls.Add(titleLabel, 0, row);
            _txtTitle = new TextBox { Dock = DockStyle.Fill, Font = formFontInput, Margin = inputMargin };
            layout.SetColumnSpan(_txtTitle, 3);
            layout.Controls.Add(_txtTitle, 1, row);
            row++;

            // ── Details (spans full width, multiline) ──
            var descLabel = new Label { Text = "Details:", Font = formFont, AutoSize = true, Margin = labelMargin };
            layout.Controls.Add(descLabel, 0, row);
            _txtDescription = new TextBox { Dock = DockStyle.Fill, Height = 48, Multiline = true, Font = formFont, Margin = inputMargin };
            layout.SetColumnSpan(_txtDescription, 3);
            layout.Controls.Add(_txtDescription, 1, row);
            row++;

            // ── Category + Priority (side by side) ──
            var catLabel = new Label { Text = "Category:", Font = formFont, AutoSize = true, Margin = labelMargin };
            layout.Controls.Add(catLabel, 0, row);
            _cmbCategory = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = formFont, Margin = inputMargin };
            foreach (var cat in Enum.GetValues(typeof(OrganizerCategory)))
                _cmbCategory.Items.Add(cat);
            _cmbCategory.SelectedIndex = 3;
            layout.Controls.Add(_cmbCategory, 1, row);

            var prioLabel = new Label { Text = "Priority:", Font = formFont, AutoSize = true, Margin = new Padding(12, 6, 6, 0) };
            layout.Controls.Add(prioLabel, 2, row);
            _cmbPriority = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = formFont, Margin = inputMargin };
            foreach (var p in Enum.GetValues(typeof(OrganizerPriority)))
                _cmbPriority.Items.Add(p);
            _cmbPriority.SelectedIndex = 1;
            layout.Controls.Add(_cmbPriority, 3, row);
            row++;

            // ── Status + (empty) ──
            var statusLabel = new Label { Text = "Status:", Font = formFont, AutoSize = true, Margin = labelMargin };
            layout.Controls.Add(statusLabel, 0, row);
            _cmbStatus = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = formFont, Margin = inputMargin };
            foreach (var s in Enum.GetValues(typeof(OrganizerStatus)))
                _cmbStatus.Items.Add(s);
            _cmbStatus.SelectedIndex = 0;
            layout.Controls.Add(_cmbStatus, 1, row);
            row++;

            // ── From / To (side by side) ──
            var timeFromLabel = new Label { Text = "From:", Font = formFont, AutoSize = true, Margin = labelMargin };
            layout.Controls.Add(timeFromLabel, 0, row);
            _dtpTimeFrom = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = formFont, Margin = inputMargin };
            layout.Controls.Add(_dtpTimeFrom, 1, row);

            var timeToLabel = new Label { Text = "To:", Font = formFont, AutoSize = true, Margin = new Padding(12, 6, 6, 0) };
            layout.Controls.Add(timeToLabel, 2, row);
            _dtpTimeTo = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = formFont, Margin = inputMargin };
            layout.Controls.Add(_dtpTimeTo, 3, row);
            row++;

            // ── Link (spans full width) ──
            var linkLabel = new Label { Text = "Link:", Font = formFont, AutoSize = true, Margin = labelMargin };
            layout.Controls.Add(linkLabel, 0, row);
            _txtLink = new TextBox { Dock = DockStyle.Fill, Font = formFont, Margin = inputMargin };
            layout.SetColumnSpan(_txtLink, 3);
            layout.Controls.Add(_txtLink, 1, row);
            row++;

            // ── ALARM SECTION ──
            _chkAlarm = new CheckBox { Text = "⏰ Set Alarm", AutoSize = true, Font = new Font("Segoe UI", 9, FontStyle.Bold), Margin = new Padding(0, 8, 0, 4) };
            _chkAlarm.CheckedChanged += (s, e) =>
            {
                _dtpAlarmDate.Enabled = _chkAlarm.Checked;
                _dtpAlarmTime.Enabled = _chkAlarm.Checked;
                _cmbRecurrence.Enabled = _chkAlarm.Checked;
            };
            layout.SetColumnSpan(_chkAlarm, 4);
            layout.Controls.Add(_chkAlarm, 0, row);
            row++;

            // Alarm Date + Time (side by side)
            var alarmDateLabel = new Label { Text = "Date:", Font = formFont, AutoSize = true, Margin = new Padding(16, 6, 6, 0) };
            layout.Controls.Add(alarmDateLabel, 0, row);
            _dtpAlarmDate = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Short, Font = formFont, Enabled = false, Margin = inputMargin };
            _dtpAlarmDate.Value = _selectedDate;
            layout.Controls.Add(_dtpAlarmDate, 1, row);

            var alarmTimeLabel = new Label { Text = "Time:", Font = formFont, AutoSize = true, Margin = new Padding(12, 6, 6, 0) };
            layout.Controls.Add(alarmTimeLabel, 2, row);
            _dtpAlarmTime = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Time, ShowUpDown = true, Font = formFont, Enabled = false, Margin = inputMargin };
            layout.Controls.Add(_dtpAlarmTime, 3, row);
            row++;

            // Repeat
            var recurLabel = new Label { Text = "Repeat:", Font = formFont, AutoSize = true, Margin = new Padding(16, 6, 6, 0) };
            layout.Controls.Add(recurLabel, 0, row);
            _cmbRecurrence = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = formFont, Enabled = false, Margin = inputMargin };
            foreach (var r in Enum.GetValues(typeof(RecurrenceType)))
                _cmbRecurrence.Items.Add(r);
            _cmbRecurrence.SelectedIndex = 0;
            layout.Controls.Add(_cmbRecurrence, 1, row);
            row++;

            // ── ACTION BUTTONS ──
            var buttonFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 10, 0, 6),
                Padding = new Padding(0),
                WrapContents = true
            };

            _btnSave = new Button
            {
                Text = "💾 Save",
                Width = 96,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(34, 197, 94),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.Click += BtnSave_Click;
            buttonFlow.Controls.Add(_btnSave);

            _btnCancel = new Button
            {
                Text = "Cancel",
                Width = 80,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnCancel.FlatAppearance.BorderSize = 1;
            _btnCancel.Click += BtnCancel_Click;
            buttonFlow.Controls.Add(_btnCancel);

            _btnDelete = new Button
            {
                Text = "🗑 Delete",
                Width = 86,
                Height = 34,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(239, 68, 68),
                Cursor = Cursors.Hand,
                Visible = false,
                Margin = new Padding(0)
            };
            _btnDelete.FlatAppearance.BorderSize = 0;
            _btnDelete.Click += BtnDelete_Click;
            buttonFlow.Controls.Add(_btnDelete);

            layout.SetColumnSpan(buttonFlow, 4);
            layout.Controls.Add(buttonFlow, 0, row);
            row++;

            // ── Quick-add templates ──
            var templatesLabel = new Label
            {
                Text = "Quick Templates:",
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Margin = new Padding(0, 10, 0, 2)
            };
            layout.SetColumnSpan(templatesLabel, 4);
            layout.Controls.Add(templatesLabel, 0, row);
            row++;

            var templateFlow = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                Margin = new Padding(0, 2, 0, 4),
                Padding = new Padding(0)
            };

            var templates = new[] {
                ("📅 Meeting", OrganizerCategory.Meeting),
                ("🎤 Interview", OrganizerCategory.Interview),
                ("📞 Call", OrganizerCategory.Call),
                ("🔄 Follow-up", OrganizerCategory.Task)
            };

            foreach (var (name, cat) in templates)
            {
                var btn = new Button
                {
                    Text = name,
                    AutoSize = true,
                    Height = 28,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 8),
                    Cursor = Cursors.Hand,
                    Tag = cat,
                    Margin = new Padding(0, 0, 6, 4)
                };
                btn.FlatAppearance.BorderSize = 1;
                btn.Click += TemplateBtn_Click;
                templateFlow.Controls.Add(btn);
            }

            layout.SetColumnSpan(templateFlow, 4);
            layout.Controls.Add(templateFlow, 0, row);

            _detailPanel.Controls.Add(layout);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  DATA LOADING
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Load entries and notes for the selected date from storage.</summary>
        private void LoadData()
        {
//             DebugLogger.Log($"[DayOrganizer] Loading data for {_dateKey}");

            // Fetch all entries for this date
            _entries = OrganizerStorage.GetEntriesForDate(_dateKey);
//             DebugLogger.Log($"[DayOrganizer] Loaded {_entries.Count} entries");

            // Load daily notes if any
            string notes = OrganizerStorage.GetDailyNotes(_dateKey);
            _txtDailyNotes.Text = notes;
//             DebugLogger.Log($"[DayOrganizer] Loaded daily notes ({notes.Length} chars)");

            // Display filtered entries and update UI
            FilterAndDisplayEntries();
            UpdateSummary();
            ClearDetailForm();

//             DebugLogger.Log("[DayOrganizer] Data loading complete");
        }

        /// <summary>Update summary label with entry counts and status info.</summary>
        private void UpdateSummary()
        {
            int total = _entries.Count;
            int done = _entries.Count(e => e.IsCompleted || e.Status == OrganizerStatus.Done);
            int alarms = _entries.Count(e => e.AlarmEnabled && !e.AlarmFired);

            _lblSummary.Text = $"{total} entries  •  {done} completed  •  {alarms} pending alarms";
//             DebugLogger.Log($"[DayOrganizer] Summary: {total} total, {done} done, {alarms} alarms");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  ENTRY CARD RENDERING
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Filter entries by search text and category, then render them.</summary>
        private void FilterAndDisplayEntries()
        {
//             DebugLogger.Log("[DayOrganizer] Filtering and displaying entries");

            _entryCardsContainer.SuspendLayout();
            _entryCardsContainer.Controls.Clear();

            var filtered = _entries.AsEnumerable();

            // Apply text search filter (ignore placeholder text)
            string search = _txtSearch?.Text?.Trim().ToLowerInvariant() ?? "";
            if (_txtSearch?.ForeColor == SystemColors.GrayText) search = "";

            if (!string.IsNullOrEmpty(search))
            {
//                 DebugLogger.Log($"[DayOrganizer] Searching for: '{search}'");
                filtered = filtered.Where(e =>
                    (e.Title ?? "").ToLowerInvariant().Contains(search) ||
                    (e.Description ?? "").ToLowerInvariant().Contains(search));
//                 DebugLogger.Log($"[DayOrganizer] Search matched {filtered.Count()} entries");
            }

            // Apply category filter
            if (_cmbCategoryFilter?.SelectedIndex > 0)
            {
                var selCat = (OrganizerCategory)_cmbCategoryFilter.SelectedItem;
//                 DebugLogger.Log($"[DayOrganizer] Filtering by category: {selCat}");
                filtered = filtered.Where(e => e.Category == selCat);
//                 DebugLogger.Log($"[DayOrganizer] Category filter matched {filtered.Count()} entries");
            }

            // Render filtered entries sorted by time
            int renderedCount = 0;
            foreach (var entry in filtered.OrderBy(e => e.TimeFrom).ThenBy(e => e.Title))
            {
                var card = CreateEntryCard(entry);
                _entryCardsContainer.Controls.Add(card);
                renderedCount++;
            }

            // Show empty state if no entries
            if (renderedCount == 0)
            {
//                 DebugLogger.Log("[DayOrganizer] No entries to display, showing empty message");
                var emptyLabel = new Label
                {
                    Text = "No entries for this day.\nClick '＋ Add Entry' to create one.",
                    Font = new Font("Segoe UI", 10),
                    ForeColor = _isDarkMode ? Color.FromArgb(100, 110, 130) : Color.FromArgb(140, 150, 170),
                    TextAlign = ContentAlignment.MiddleCenter,
                    AutoSize = false,
                    Width = _entryCardsContainer.Width - 30,
                    Height = 80
                };
                _entryCardsContainer.Controls.Add(emptyLabel);
            }
            else
            {
//                 DebugLogger.Log($"[DayOrganizer] Rendered {renderedCount} entry cards");
            }

            _entryCardsContainer.ResumeLayout(true);
        }

        /// <summary>Create a visual card panel for one organizer entry.</summary>
        private Panel CreateEntryCard(OrganizerEntry entry)
        {
            var catColor = CategoryColors.ContainsKey(entry.Category) ? CategoryColors[entry.Category] : Color.Gray;
            var catIcon = CategoryIcons.ContainsKey(entry.Category) ? CategoryIcons[entry.Category] : "•";

            Color cardBg = _isDarkMode ? Color.FromArgb(38, 44, 56) : Color.FromArgb(248, 249, 252);
            Color textColor = _isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59);
            Color secondaryColor = _isDarkMode ? Color.FromArgb(120, 130, 145) : Color.FromArgb(100, 116, 139);

            if (_customTheme?.Enabled == true)
            {
                cardBg = _customTheme.GetInput();
                textColor = _customTheme.GetText();
                secondaryColor = _customTheme.GetSecondaryText();
            }

            // Dim completed/cancelled entries
            if (entry.IsCompleted || entry.Status == OrganizerStatus.Done || entry.Status == OrganizerStatus.Cancelled)
            {
                cardBg = _isDarkMode ? Color.FromArgb(32, 38, 48) : Color.FromArgb(242, 243, 246);
                textColor = secondaryColor;
            }

            int cardWidth = _entryCardsContainer.Width - 30;
            var card = new Panel
            {
                Width = cardWidth,
                Height = 72,
                Margin = new Padding(0, 2, 0, 2),
                BackColor = cardBg,
                Cursor = Cursors.Hand,
                Tag = entry
            };

            // Left color bar
            var colorBar = new Panel
            {
                Width = 4,
                Height = card.Height,
                Location = new Point(0, 0),
                BackColor = catColor
            };
            card.Controls.Add(colorBar);

            // Checkbox
            var chk = new CheckBox
            {
                Checked = entry.IsCompleted,
                Location = new Point(10, 26),
                AutoSize = true,
                Tag = entry
            };
            chk.CheckedChanged += (s, e) =>
            {
                var ent = (OrganizerEntry)chk.Tag;
                ent.IsCompleted = chk.Checked;
                if (chk.Checked) ent.Status = OrganizerStatus.Done;
                OrganizerStorage.SaveEntry(ent);
                _entries = OrganizerStorage.GetEntriesForDate(_dateKey);
                UpdateSummary();
                FilterAndDisplayEntries();
            };
            card.Controls.Add(chk);

            // Category icon + Title
            string timeStr = !string.IsNullOrEmpty(entry.TimeFrom) ? $"{entry.TimeFrom}" : "";
            if (!string.IsNullOrEmpty(entry.TimeTo)) timeStr += $"–{entry.TimeTo}";
            string titleText = $"{catIcon}  {entry.Title}";
            if (entry.IsCompleted) titleText = $"✓  {entry.Title}";

            var lblTitle = new Label
            {
                Text = titleText,
                Font = new Font("Segoe UI", 9.5f, entry.IsCompleted ? FontStyle.Strikeout : FontStyle.Bold),
                ForeColor = textColor,
                Location = new Point(34, 6),
                AutoSize = true
            };
            card.Controls.Add(lblTitle);

            // Time + Category
            string metaText = $"{entry.Category}";
            if (!string.IsNullOrEmpty(timeStr)) metaText = $"{timeStr}  •  {metaText}";
            if (entry.AlarmEnabled && !entry.AlarmFired) metaText += "  ⏰";
            if (entry.Priority == OrganizerPriority.High) metaText += "  🔴";
            if (entry.Priority == OrganizerPriority.Urgent) metaText += "  🔴🔴";

            var lblMeta = new Label
            {
                Text = metaText,
                Font = new Font("Segoe UI", 8),
                ForeColor = secondaryColor,
                Location = new Point(34, 28),
                AutoSize = true
            };
            card.Controls.Add(lblMeta);

            // Status badge
            var lblStatus = new Label
            {
                Text = entry.Status.ToString(),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(34, 50),
                Padding = new Padding(4, 1, 4, 1)
            };
            switch (entry.Status)
            {
                case OrganizerStatus.Planned: lblStatus.ForeColor = Color.FromArgb(59, 130, 246); break;
                case OrganizerStatus.Done: lblStatus.ForeColor = Color.FromArgb(34, 197, 94); break;
                case OrganizerStatus.Postponed: lblStatus.ForeColor = Color.FromArgb(234, 179, 8); break;
                case OrganizerStatus.Cancelled: lblStatus.ForeColor = Color.FromArgb(239, 68, 68); break;
            }
            card.Controls.Add(lblStatus);

            // Link icon
            if (!string.IsNullOrEmpty(entry.Link))
            {
                var lblLink = new Label
                {
                    Text = "🔗",
                    Font = new Font("Segoe UI", 10),
                    AutoSize = true,
                    Location = new Point(cardWidth - 40, 6),
                    Cursor = Cursors.Hand
                };
                lblLink.Click += (s, e) =>
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(entry.Link) { UseShellExecute = true }); }
                    catch { }
                };
                card.Controls.Add(lblLink);
            }

            // Click card to edit
            Action selectCard = () =>
            {
                _editingEntry = entry;
                PopulateDetailForm(entry);
            };
            card.Click += (s, e) => selectCard();
            lblTitle.Click += (s, e) => selectCard();
            lblMeta.Click += (s, e) => selectCard();
            lblStatus.Click += (s, e) => selectCard();

            return card;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  DETAIL FORM OPERATIONS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Clear detail form and prepare for adding a new entry.</summary>
        private void ClearDetailForm()
        {
//             DebugLogger.Log("[DayOrganizer] Clearing detail form for new entry");

            _editingEntry = null;
            _txtTitle.Text = "";
            _txtDescription.Text = "";
            _cmbCategory.SelectedIndex = 3; // Default: Task
            _cmbPriority.SelectedIndex = 1; // Default: Normal
            _cmbStatus.SelectedIndex = 0;   // Default: Planned
            _dtpTimeFrom.Value = DateTime.Today.AddHours(9);
            _dtpTimeTo.Value = DateTime.Today.AddHours(10);
            _txtLink.Text = "";
            _chkAlarm.Checked = false;
            _dtpAlarmDate.Value = _selectedDate;
            _dtpAlarmTime.Value = DateTime.Today.AddHours(9);
            _cmbRecurrence.SelectedIndex = 0; // Default: None
            _btnDelete.Visible = false;
        }

        /// <summary>Populate detail form with existing entry data for editing.</summary>
        private void PopulateDetailForm(OrganizerEntry entry)
        {
//             DebugLogger.Log($"[DayOrganizer] Loading entry for edit: {entry.Title}");

            _editingEntry = entry;
            _txtTitle.Text = entry.Title;
            _txtDescription.Text = entry.Description;
            _cmbCategory.SelectedItem = entry.Category;
            _cmbPriority.SelectedItem = entry.Priority;
            _cmbStatus.SelectedItem = entry.Status;

            // Parse and set time fields from stored TimeSpan strings
            if (TimeSpan.TryParse(entry.TimeFrom, out var from))
            {
                _dtpTimeFrom.Value = DateTime.Today.Add(from);
//                 DebugLogger.Log($"[DayOrganizer] TimeFrom: {entry.TimeFrom}");
            }

            if (TimeSpan.TryParse(entry.TimeTo, out var to))
            {
                _dtpTimeTo.Value = DateTime.Today.Add(to);
//                 DebugLogger.Log($"[DayOrganizer] TimeTo: {entry.TimeTo}");
            }

            _txtLink.Text = entry.Link ?? "";
            _chkAlarm.Checked = entry.AlarmEnabled;

            // Parse alarm datetime if set
            if (!string.IsNullOrEmpty(entry.AlarmDateTime) && DateTime.TryParse(entry.AlarmDateTime, out var alarmDt))
            {
                _dtpAlarmDate.Value = alarmDt.Date;
                _dtpAlarmTime.Value = alarmDt;
//                 DebugLogger.Log($"[DayOrganizer] Alarm set for: {alarmDt:o}");
            }

            _cmbRecurrence.SelectedItem = entry.Recurrence;
            _btnDelete.Visible = true;

//             DebugLogger.Log("[DayOrganizer] Entry loaded in detail form");
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  BUTTON HANDLERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Add button: clear form for new entry.</summary>
        private void BtnAdd_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[DayOrganizer] Add button clicked");
            ClearDetailForm();
            _txtTitle.Focus();
        }

        /// <summary>Save button: validate and save entry (new or updated).</summary>
        private void BtnSave_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[DayOrganizer] Save button clicked");

            // Validate required title field
            if (string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                DebugLogger.Log("[DayOrganizer] Validation failed: title is required");
                MessageBox.Show("Please enter a title.", "Organizer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Create or update entry
            var entry = _editingEntry ?? new OrganizerEntry();
            entry.Date = _dateKey;
            entry.Title = _txtTitle.Text.Trim();
            entry.Description = _txtDescription.Text.Trim();
            entry.Category = (OrganizerCategory)_cmbCategory.SelectedItem;
            entry.Priority = (OrganizerPriority)_cmbPriority.SelectedItem;
            entry.Status = (OrganizerStatus)_cmbStatus.SelectedItem;
            entry.TimeFrom = _dtpTimeFrom.Value.ToString("HH:mm");
            entry.TimeTo = _dtpTimeTo.Value.ToString("HH:mm");
            entry.Link = _txtLink.Text.Trim();
            entry.IsCompleted = entry.Status == OrganizerStatus.Done;
            entry.Owner = _currentUser;

//             DebugLogger.Log($"[DayOrganizer] Saving entry: {entry.Title} ({entry.Category})");

            // Handle alarm if enabled
            entry.AlarmEnabled = _chkAlarm.Checked;
            if (_chkAlarm.Checked)
            {
                var alarmDt = _dtpAlarmDate.Value.Date.Add(_dtpAlarmTime.Value.TimeOfDay);
                entry.AlarmDateTime = alarmDt.ToString("o");
                entry.AlarmFired = false;
                entry.SnoozedUntil = "";
//                 DebugLogger.Log($"[DayOrganizer] Alarm configured for: {alarmDt:o}");
            }
            entry.Recurrence = (RecurrenceType)_cmbRecurrence.SelectedItem;

            // Persist to storage
            OrganizerStorage.SaveEntry(entry);
//             DebugLogger.Log("[DayOrganizer] Entry saved to storage");

            // Refresh UI
            _entries = OrganizerStorage.GetEntriesForDate(_dateKey);
            FilterAndDisplayEntries();
            UpdateSummary();
            ClearDetailForm();

//             DebugLogger.Log("[DayOrganizer] UI refreshed after save");
        }

        /// <summary>Cancel button: clear form without saving.</summary>
        private void BtnCancel_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[DayOrganizer] Cancel button clicked");
            ClearDetailForm();
        }

        /// <summary>Delete button: confirm and remove entry from storage.</summary>
        private void BtnDelete_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[DayOrganizer] Delete button clicked");

            if (_editingEntry == null)
            {
//                 DebugLogger.Log("[DayOrganizer] No entry selected for deletion");
                return;
            }

            var result = MessageBox.Show($"Delete \"{_editingEntry.Title}\"?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
//                 DebugLogger.Log($"[DayOrganizer] Deleting entry: {_editingEntry.Title}");
                OrganizerStorage.DeleteEntry(_editingEntry.Id);
//                 DebugLogger.Log("[DayOrganizer] Entry deleted from storage");

                // Refresh UI
                _entries = OrganizerStorage.GetEntriesForDate(_dateKey);
                FilterAndDisplayEntries();
                UpdateSummary();
                ClearDetailForm();

//                 DebugLogger.Log("[DayOrganizer] UI refreshed after delete");
            }
            else
            {
//                 DebugLogger.Log("[DayOrganizer] Delete cancelled by user");
            }
        }

        private void TemplateBtn_Click(object sender, EventArgs e)
        {
            var btn = (Button)sender;
            var cat = (OrganizerCategory)btn.Tag;
            ClearDetailForm();
            _cmbCategory.SelectedItem = cat;

            switch (cat)
            {
                case OrganizerCategory.Meeting:
                    _txtTitle.Text = "Meeting: ";
                    _dtpTimeFrom.Value = DateTime.Today.AddHours(10);
                    _dtpTimeTo.Value = DateTime.Today.AddHours(11);
                    break;
                case OrganizerCategory.Interview:
                    _txtTitle.Text = "Interview: ";
                    _txtLink.Text = "https://";
                    _dtpTimeFrom.Value = DateTime.Today.AddHours(14);
                    _dtpTimeTo.Value = DateTime.Today.AddHours(15);
                    break;
                case OrganizerCategory.Call:
                    _txtTitle.Text = "Call: ";
                    break;
                case OrganizerCategory.Task:
                    _txtTitle.Text = "Follow-up: ";
                    _cmbPriority.SelectedItem = OrganizerPriority.High;
                    break;
            }
            _txtTitle.Focus();
            _txtTitle.SelectionStart = _txtTitle.Text.Length;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  THEME
        // ═══════════════════════════════════════════════════════════════════════

        private void ApplyTheme()
        {
            Color bgMain, bgPanel, bgInput, fgMain, fgSecondary, accent;

            if (_customTheme?.Enabled == true)
            {
                bgMain = _customTheme.GetBackground();
                bgPanel = _customTheme.GetCard();
                bgInput = _customTheme.GetInput();
                fgMain = _customTheme.GetText();
                fgSecondary = _customTheme.GetSecondaryText();
                accent = _customTheme.GetAccent();
            }
            else if (_isDarkMode)
            {
                bgMain = Color.FromArgb(22, 26, 34);
                bgPanel = Color.FromArgb(28, 34, 44);
                bgInput = Color.FromArgb(36, 42, 54);
                fgMain = Color.FromArgb(225, 228, 235);
                fgSecondary = Color.FromArgb(120, 130, 150);
                accent = Color.FromArgb(99, 144, 255);
            }
            else
            {
                bgMain = Color.FromArgb(245, 247, 250);
                bgPanel = Color.White;
                bgInput = Color.FromArgb(248, 249, 252);
                fgMain = Color.FromArgb(30, 41, 59);
                fgSecondary = Color.FromArgb(100, 116, 139);
                accent = Color.FromArgb(99, 144, 255);
            }

            this.BackColor = bgMain;
            this.ForeColor = fgMain;
            _headerPanel.BackColor = bgPanel;
            _lblDate.ForeColor = fgMain;
            _lblSummary.ForeColor = fgSecondary;

            // Notes panel subtle card background
            _notesPanel.BackColor = bgPanel;
            _detailPanel.BackColor = bgPanel;

            ApplyThemeToControls(this.Controls, bgMain, bgPanel, bgInput, fgMain, fgSecondary, accent);
        }

        private void ApplyThemeToControls(Control.ControlCollection controls,
            Color bgMain, Color bgPanel, Color bgInput, Color fgMain, Color fgSecondary, Color accent)
        {
            foreach (Control ctrl in controls)
            {
                if (ctrl is TextBox tb)
                {
                    tb.BackColor = bgInput;
                    tb.ForeColor = fgMain;
                    tb.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (ctrl is RichTextBox rtb)
                {
                    rtb.BackColor = bgInput;
                    rtb.ForeColor = fgMain;
                }
                else if (ctrl is ComboBox cb)
                {
                    cb.BackColor = bgInput;
                    cb.ForeColor = fgMain;
                }
                else if (ctrl is DateTimePicker dtp)
                {
                    dtp.CalendarMonthBackground = bgInput;
                    dtp.CalendarForeColor = fgMain;
                }
                else if (ctrl is Label lbl)
                {
                    // Don't override colored badge labels
                    if (lbl.BackColor != Color.FromArgb(34, 197, 94))
                        lbl.ForeColor = fgMain;
                }
                else if (ctrl is Panel pnl && pnl != _headerPanel && pnl != _notesPanel && pnl != _detailPanel)
                {
                    // Keep color bars (Width == 4) and special colored panels
                    if (pnl.Width == 4) { }
                    else if (pnl.BackColor != Color.FromArgb(34, 197, 94) &&
                             pnl.BackColor != Color.FromArgb(239, 68, 68))
                    {
                        pnl.BackColor = bgMain;
                    }
                }
                else if (ctrl is FlowLayoutPanel flp)
                {
                    flp.BackColor = Color.Transparent;
                }
                else if (ctrl is TableLayoutPanel tlp)
                {
                    tlp.BackColor = Color.Transparent;
                }
                else if (ctrl is SplitContainer sc)
                {
                    sc.BackColor = bgMain;
                    sc.Panel1.BackColor = bgMain;
                    sc.Panel2.BackColor = bgMain;
                }
                else if (ctrl is CheckBox chk)
                {
                    chk.ForeColor = fgMain;
                }

                if (ctrl.HasChildren)
                    ApplyThemeToControls(ctrl.Controls, bgMain, bgPanel, bgInput, fgMain, fgSecondary, accent);
            }
        }
    }
}
