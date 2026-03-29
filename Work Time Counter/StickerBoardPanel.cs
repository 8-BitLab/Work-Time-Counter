// â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—
// â•‘                        8 BIT LAB ENGINEERING                               â•‘
// â•‘                     WORKFLOW - TEAM TIME TRACKER                            â•‘
// â•‘                                                                            â•‘
// â•‘  FILE:        StickerBoardPanel.cs                                         â•‘
// â•‘  PURPOSE:     KANBAN-STYLE TASK BOARD WITH STICKER CARDS                   â•‘
// â•‘  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         â•‘
// â•‘  LICENSE:     OPEN SOURCE                                                  â•‘
// â•‘                                                                            â•‘
// â•‘  DESCRIPTION:                                                              â•‘
// â•‘  A visual task board (left sidebar) that displays sticker cards in a        â•‘
// â•‘  two-column grid layout. Each sticker represents a task, bug, reminder,     â•‘
// â•‘  or idea. Cards can be expanded/collapsed, marked done, edited, deleted,   â•‘
// â•‘  and assigned to team members.                                              â•‘
// â•‘                                                                            â•‘
// â•‘  CARD TYPES AND COLORS:                                                    â•‘
// â•‘    ToDo     = Amber/Yellow  (#FFAB00)                                      â•‘
// â•‘    Reminder = Teal/Green    (#00875A)                                       â•‘
// â•‘    Bug      = Red           (#DE350B)                                       â•‘
// â•‘    Idea     = Purple        (#6554C0)                                       â•‘
// â•‘                                                                            â•‘
// â•‘  PRIORITY LEVELS:                                                          â•‘
// â•‘    High   = Red    (#DE350B) â€” sorted first                                â•‘
// â•‘    Medium = Amber  (#FFAB00) â€” sorted second                               â•‘
// â•‘    Low    = Blue   (#4C9AFF) â€” sorted last                                 â•‘
// â•‘                                                                            â•‘
// â•‘  FEATURES:                                                                 â•‘
// â•‘  - TWO-COLUMN GRID: Cards arranged in masonry-style layout                 â•‘
// â•‘  - EXPAND/COLLAPSE: Click to toggle between mini and full view             â•‘
// â•‘  - CONTEXT MENU: Right-click for Edit, Delete, Mark Done, Priority         â•‘
// â•‘  - ADMIN CONTROLS: Admin/creator can edit and delete stickers              â•‘
// â•‘  - MUTE CHECK: Muted users cannot create new stickers                      â•‘
// â•‘  - DOUBLE-CLICK: Start working on a sticker (fills timer description)      â•‘
// â•‘  - FILTER: Dropdown to show All, ToDo, Reminder, Bug, or Idea only        â•‘
// â•‘                                                                            â•‘
// â•‘  FIREBASE STRUCTURE:                                                       â•‘
// â•‘    /stickers/{key}/title       --> "Fix login bug"                          â•‘
// â•‘    /stickers/{key}/type        --> "Bug"                                    â•‘
// â•‘    /stickers/{key}/priority    --> "High"                                   â•‘
// â•‘    /stickers/{key}/createdBy   --> "Alice"                                  â•‘
// â•‘    /stickers/{key}/assignedTo  --> "Bob"                                    â•‘
// â•‘    /stickers/{key}/done        --> false                                    â•‘
// â•‘                                                                            â•‘
// â•‘  GitHub: https://github.com/8BitLabEngineering                             â•‘
// â•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Work_Time_Counter
{
    public class StickerBoardPanel : UserControl
    {
        // â•â•â• JIRA-STYLE COLOR PALETTE â€” TYPE AND PRIORITY COLORS â•â•â•
        // These colors match Atlassian Jira's standard palette for accessibility and consistency.
        // High-contrast colors ensure readability in both dark and light modes.

        private static readonly Color clrTodo = Color.FromArgb(255, 171, 0);       // Jira amber
        private static readonly Color clrReminder = Color.FromArgb(0, 135, 90);    // Jira teal-green
        private static readonly Color clrBug = Color.FromArgb(222, 53, 11);        // Jira red
        private static readonly Color clrIdea = Color.FromArgb(101, 84, 192);      // Jira purple
        private static readonly Color clrPersonalNote = Color.FromArgb(0, 184, 217);      // cyan
        private static readonly Color clrPersonalLink = Color.FromArgb(121, 134, 203);     // indigo
        private static readonly Color clrPersonalReminder = Color.FromArgb(0, 200, 83);     // green
        private static readonly Color clrPersonalIdea = Color.FromArgb(255, 112, 67);       // orange

        // â”€â”€â”€ Priority colors (used in priority badge) â”€â”€â”€
        // These also follow Jira's priority color scheme: High=Red, Medium=Amber, Low=Blue
        private static readonly Color clrPrioHigh = Color.FromArgb(222, 53, 11);   // Jira Highest red
        private static readonly Color clrPrioMed = Color.FromArgb(255, 171, 0);    // Jira Medium amber
        private static readonly Color clrPrioLow = Color.FromArgb(76, 154, 255);   // Jira Low blue

        // â•â•â• CARD BACKGROUND COLORS â€” DARK AND LIGHT MODE â•â•â•
        // Dark-mode backgrounds are muted/pastel versions of the type colors (sticky-note style).
        // Light-mode backgrounds are very pale tints, almost white, to maintain readability.

        // â”€â”€â”€ Dark mode: softened card backgrounds (lower saturation, refined) â”€â”€â”€
        private static readonly Color darkBgTodo = Color.FromArgb(55, 48, 30);        // soft warm amber
        private static readonly Color darkBgReminder = Color.FromArgb(30, 55, 48);    // soft teal-green
        private static readonly Color darkBgBug = Color.FromArgb(60, 35, 32);         // soft warm red
        private static readonly Color darkBgIdea = Color.FromArgb(42, 38, 60);        // soft purple
        private static readonly Color darkBgPersonalNote = Color.FromArgb(24, 52, 62);
        private static readonly Color darkBgPersonalLink = Color.FromArgb(35, 42, 68);
        private static readonly Color darkBgPersonalReminder = Color.FromArgb(24, 60, 40);
        private static readonly Color darkBgPersonalIdea = Color.FromArgb(62, 42, 28);
        private static readonly Color darkBgDone = Color.FromArgb(32, 35, 40);        // subtle muted gray (for completed stickers)

        // â”€â”€â”€ Light mode: very pale tints (almost white, slight tint) â”€â”€â”€
        private static readonly Color lightBgTodo = Color.FromArgb(255, 248, 225);    // pale yellow
        private static readonly Color lightBgReminder = Color.FromArgb(225, 248, 240); // pale teal
        private static readonly Color lightBgBug = Color.FromArgb(255, 235, 230);     // pale red
        private static readonly Color lightBgIdea = Color.FromArgb(238, 235, 255);    // pale purple
        private static readonly Color lightBgPersonalNote = Color.FromArgb(232, 247, 252);
        private static readonly Color lightBgPersonalLink = Color.FromArgb(236, 238, 252);
        private static readonly Color lightBgPersonalReminder = Color.FromArgb(232, 252, 238);
        private static readonly Color lightBgPersonalIdea = Color.FromArgb(252, 241, 232);
        private static readonly Color lightBgDone = Color.FromArgb(240, 240, 245);    // very pale gray (for completed stickers)

        // â•â•â• INSTANCE FIELDS â€” FIREBASE URL, USERNAME, STATE â•â•â•
        // Static HTTP client is reused across all instances for efficiency.
        private static readonly HttpClient _http = new HttpClient();

        // Configuration passed to constructor
        private readonly string _firebaseBaseUrl;   // Base URL of Firebase Realtime Database
        private readonly string _currentUserName;   // Username of the person using this panel
        private readonly bool _isAdmin;             // Whether current user is an admin (can edit/delete others' stickers)
        private readonly Func<string, bool> _isMutedCheck; // Function to check if a user is muted (prevents sticker creation)
        private readonly bool _localOnlyMode;
        private readonly bool _usePersonalPalette;
        private readonly string _boardTitle;
        private readonly string _syncOffMessage;

        // UI Components
        private Panel flowStickers;      // Main scrollable container for all sticker cards
        private Button btnAdd;           // "New" button to create a sticker
        private ComboBox cmbFilter;      // Dropdown to filter by type (All, ToDo, Reminder, Bug, Idea)
        private CheckBox _chkShowFinished; // Checkbox to toggle visibility of done/history stickers
        private Label lblTitle;          // "ðŸ“‹ BOARD" title label
        private Panel _syncNoticePanel;
        private Label _syncNoticeLabel;
        private Timer _btnAddAnimTimer;
        private bool _btnAddHover;
        private int _btnAddAnimTick;

        // In-memory cache of stickers loaded from Firebase
        private List<StickerEntry> _stickers = new List<StickerEntry>();
        private const string StickersLocalFileName = "stickers_local.json";
        private string _stickersLocalFileName = StickersLocalFileName;
        private bool _isDarkMode = true;

        // TRACK WHICH STICKERS ARE MINIMIZED (by key).
        // When a sticker's key is in this set, it's rendered in mini view (34px tall);
        // otherwise it's rendered in full expanded view (110px tall).
        // Users click the minimize (â–²) button to add to set, or click anywhere to remove.
        private HashSet<string> _minimizedKeys = new HashSet<string>();

        // â”€â”€â”€ ANTI-REENTRANCE GUARD: Resize â†” RenderStickers oscillation â”€â”€â”€
        // When the scrollbar appears/disappears, it changes ClientSize.Width, which triggers
        // Resize, which calls RenderStickers, which might change scrollbar visibility, etc.
        // This guard prevents that infinite loop.
        private bool _isRendering = false;
        private int _lastRenderWidth = -1;

        // â”€â”€â”€ ANTI-REENTRANCE GUARD: Overlapping RefreshAsync calls â”€â”€â”€
        // If the user clicks "New" while a refresh is in progress, we don't want two
        // async fetches from Firebase happening at the same time.
        private bool _isRefreshing = false;

        /// <summary>
        /// Fired when user double-clicks an expanded sticker card and confirms
        /// they want to start working on it. Passes the StickerEntry.
        /// </summary>
        public event Action<StickerEntry> OnStartWorkRequested;

        /// <summary>
        /// The sticker the user is currently working on (set after double-click start).
        /// Null when no sticker-based work is active.
        /// </summary>
        public StickerEntry ActiveWorkingSticker { get; set; }
        public string ActiveWorkingStickerKey { get; private set; }

        // â•â•â• CONSTRUCTOR â€” ACCEPTS FIREBASE URL, USERNAME, ADMIN FLAG, MUTE CHECK â•â•â•
        /// <summary>
        /// Initializes a new StickerBoardPanel.
        /// </summary>
        /// <param name="firebaseBaseUrl">Base URL of your Firebase Realtime Database (e.g., "https://myapp.firebaseio.com")</param>
        /// <param name="currentUserName">Username of the person using this panel (displayed as creator/assignee)</param>
        /// <param name="isAdmin">If true, user can edit/delete anyone's stickers; otherwise only own stickers</param>
        /// <param name="isMutedCheck">Optional function to check if a user is muted. Muted users cannot create stickers. Pass null for no muting.</param>
        public StickerBoardPanel(
            string firebaseBaseUrl,
            string currentUserName,
            bool isAdmin = false,
            Func<string, bool> isMutedCheck = null,
            bool localOnlyMode = false,
            string localFileName = null,
            string boardTitle = null,
            string syncOffMessage = null)
        {
//             DebugLogger.Log($"[StickerBoard] Constructor: user={currentUserName}, isAdmin={isAdmin}, firebaseUrl={firebaseBaseUrl}");

            _firebaseBaseUrl = (firebaseBaseUrl ?? "").TrimEnd('/');
            _currentUserName = currentUserName;
            _isAdmin = isAdmin;
            _isMutedCheck = isMutedCheck ?? (username => false);
            _localOnlyMode = localOnlyMode;
            _usePersonalPalette = localOnlyMode;
            _boardTitle = string.IsNullOrWhiteSpace(boardTitle) ? "\U0001f4cb BOARD" : boardTitle;
            _syncOffMessage = string.IsNullOrWhiteSpace(syncOffMessage)
                ? "SYNC OFF: Admin must set Firebase URL in Team Settings > Database (local/P2P mode)."
                : syncOffMessage;
            if (!string.IsNullOrWhiteSpace(localFileName))
                _stickersLocalFileName = localFileName.Trim();

            // Configure panel: fixed width, docks to left, dark background
            this.Width = 300;
            this.Dock = DockStyle.Left;
            this.BackColor = Color.FromArgb(30, 36, 46);
            this.Padding = new Padding(6, 10, 6, 6);
            this.BorderStyle = BorderStyle.FixedSingle;

//             DebugLogger.Log("[StickerBoard] Panel UI configured: 300px wide, docked left");
            BuildUI();
//             DebugLogger.Log("[StickerBoard] Constructor complete");
        }

        // â•â•â• BUILD UI â€” CREATES HEADER, FILTER, AND SCROLLABLE CARD CONTAINER â•â•â•
        /// <summary>
        /// Constructs all UI elements: header (title + add button), filter dropdown,
        /// and the scrollable sticker container. Called once in constructor.
        /// </summary>
        private void BuildUI()
        {
//             DebugLogger.Log("[StickerBoard] BuildUI: Starting UI construction");

            // â”€â”€ Header (top area with title, add button, and filter) â”€â”€
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 84,
                BackColor = Color.Transparent,
                Padding = new Padding(6, 8, 6, 4)
            };
//             DebugLogger.Log("[StickerBoard] BuildUI: Header panel created (70px tall)");

            // Title label: "ðŸ“‹ BOARD" in orange/coral color
            lblTitle = new Label
            {
                Text = _boardTitle,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 127, 80),
                Location = new Point(8, 12),
                AutoSize = true
            };
            panelHeader.Controls.Add(lblTitle);
//             DebugLogger.Log("[StickerBoard] BuildUI: Title label added");

            // "New" button to create a sticker (opens StickerDialog)
            btnAdd = new Button
            {
                Text = "+ New",
                Size = new Size(92, 32),
                Location = new Point(214, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 127, 80),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnAdd.FlatAppearance.BorderSize = 0;
            btnAdd.Click += BtnAdd_Click;
            btnAdd.MouseEnter += (s, e) => { _btnAddHover = true; _btnAddAnimTimer?.Start(); };
            btnAdd.MouseLeave += (s, e) =>
            {
                _btnAddHover = false;
                _btnAddAnimTick = 0;
                btnAdd.BackColor = Color.FromArgb(255, 127, 80);
                btnAdd.ForeColor = Color.White;
            };
            panelHeader.Controls.Add(btnAdd);
//             DebugLogger.Log("[StickerBoard] BuildUI: '+ New' button added");

            // Filter dropdown: All / ToDo / Reminder / Bug / Idea
            // When selection changes, RefreshAsync is called to re-render visible stickers
            cmbFilter = new ComboBox
            {
                Location = new Point(8, 44),
                Size = new Size(196, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = Color.FromArgb(38, 44, 56),
                ForeColor = Color.FromArgb(220, 224, 230)
            };
            cmbFilter.Items.AddRange(_usePersonalPalette
                ? new object[] { "All", "Note", "Link", "Reminder", "Idea" }
                : new object[] { "All", "ToDo", "Reminder", "Bug", "Idea" });
            cmbFilter.SelectedIndex = 0;
            cmbFilter.SelectedIndexChanged += async (s, e) =>
            {
//                 DebugLogger.Log($"[StickerBoard] Filter changed to: {cmbFilter.SelectedItem}");
                await RefreshAsync();
            };
            panelHeader.Controls.Add(cmbFilter);
//             DebugLogger.Log("[StickerBoard] BuildUI: Filter dropdown created (All/ToDo/Reminder/Bug/Idea)");

            // â”€â”€ "Show finished tasks" checkbox â€” toggles visibility of done/history stickers â”€â”€
            _chkShowFinished = new CheckBox
            {
                Text = "\u2611 Show finished",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(130, 140, 160),
                Location = new Point(314, 48),
                AutoSize = true,
                Checked = false,
                FlatStyle = FlatStyle.Flat
            };
            _chkShowFinished.CheckedChanged += (s, e) =>
            {
//                 DebugLogger.Log($"[StickerBoard] 'Show finished' toggled to: {_chkShowFinished.Checked}");
                RenderStickers();
            };
            panelHeader.Controls.Add(_chkShowFinished);
//             DebugLogger.Log("[StickerBoard] BuildUI: 'Show finished' checkbox added");

            _btnAddAnimTimer = new Timer { Interval = 45 };
            _btnAddAnimTimer.Tick += (s, e) =>
            {
                if (btnAdd == null)
                    return;

                _btnAddAnimTick++;

                if (!_btnAddHover)
                {
                    _btnAddAnimTimer.Stop();
                    btnAdd.BackColor = Color.FromArgb(255, 127, 80);
                    btnAdd.ForeColor = Color.White;
                    return;
                }

                double wave = (Math.Sin(_btnAddAnimTick / 2.5) + 1.0) / 2.0;
                int g = 127 + (int)(28 * wave);
                int b = 80 + (int)(18 * wave);
                btnAdd.BackColor = Color.FromArgb(255, g, b);
                btnAdd.ForeColor = wave > 0.55 ? Color.FromArgb(255, 252, 245) : Color.White;
            };

            // â”€â”€ Scrollable sticker container (manual 2-column grid) â”€â”€
            // NOTE: We use AutoScroll=true to handle overflow automatically.
            // Cards are positioned manually in RenderStickers() using a two-column masonry algorithm.
            flowStickers = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.Transparent,
                Padding = new Padding(4)
            };

            _syncNoticePanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 22,
                BackColor = Color.FromArgb(56, 42, 24),
                Visible = false
            };
            _syncNoticeLabel = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 8, 0),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 214, 120),
                Text = _syncOffMessage
            };
            _syncNoticePanel.Controls.Add(_syncNoticeLabel);
//             DebugLogger.Log("[StickerBoard] BuildUI: Scrollable sticker container created");

            // IMPORTANT: In WinForms, the LAST control added docks FIRST.
            // Add flowStickers first, then panelHeader last, so the header
            // docks to Top first and flowStickers fills the remaining space below it.
            this.Controls.Add(flowStickers);
            this.Controls.Add(_syncNoticePanel);
            this.Controls.Add(panelHeader);
//             DebugLogger.Log("[StickerBoard] BuildUI: Controls added to panel (flowStickers + header)");

            // â”€â”€â”€ RESIZE HANDLER: Re-layout cards when panel width changes â”€â”€â”€
            // When the scrollbar appears/disappears, or the panel is resized, we need to
            // re-render the two-column grid to fit the new width. However, we guard against
            // re-entrant calls and redundant renders:
            //   1. _isRendering: Skip if already mid-render (prevents oscillation)
            //   2. _lastRenderWidth: Skip if width unchanged (width didn't change)
            flowStickers.Resize += (s, e) =>
            {
                if (_isRendering)
                {
//                     DebugLogger.Log("[StickerBoard] Resize event ignored: already rendering");
                    return; // prevent re-entrant calls
                }
                int currentWidth = flowStickers.ClientSize.Width;
                if (currentWidth == _lastRenderWidth)
                {
//                     DebugLogger.Log($"[StickerBoard] Resize event ignored: width unchanged ({currentWidth}px)");
                    return; // width didn't change
                }
                if (_stickers.Count > 0)
                {
//                     DebugLogger.Log($"[StickerBoard] Resize event triggered re-render: width changed to {currentWidth}px");
                    RenderStickers();
                }
            };
//             DebugLogger.Log("[StickerBoard] BuildUI: Resize handler attached to flowStickers");
            UpdateSyncNotice();
        }

        private void UpdateSyncNotice()
        {
            try
            {
                if (_localOnlyMode)
                {
                    _syncNoticeLabel.Text = _syncOffMessage;
                    _syncNoticePanel.Visible = true;
                    return;
                }
                var team = UserStorage.LoadTeam();
                bool syncOff = team != null && string.IsNullOrWhiteSpace(team.CustomFirebaseUrl);
                _syncNoticePanel.Visible = syncOff;
            }
            catch
            {
                if (_syncNoticePanel != null)
                    _syncNoticePanel.Visible = false;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // â•â•â• REFRESH â€” LOADS STICKERS FROM FIREBASE AND RENDERS THEM â•â•â•
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        private static bool IsLocalStickerKey(string key)
        {
            return !string.IsNullOrWhiteSpace(key) &&
                   key.StartsWith("LOCAL_", StringComparison.OrdinalIgnoreCase);
        }

        private void SaveLocalStickerCache()
        {
            TeamLocalCacheStore.SaveList(_stickersLocalFileName, _stickers);
        }

        private List<StickerEntry> LoadLocalStickerCache()
        {
            return TeamLocalCacheStore.LoadList<StickerEntry>(_stickersLocalFileName);
        }

        private bool IsRemoteSyncEnabled =>
            !_localOnlyMode && !string.IsNullOrWhiteSpace(_firebaseBaseUrl);

        public void SetLocalCacheFileName(string localFileName)
        {
            if (string.IsNullOrWhiteSpace(localFileName))
                return;

            _stickersLocalFileName = localFileName.Trim();
            _stickers = LoadLocalStickerCache();
            RenderStickers();
        }

        private async Task TrySyncLocalUnsyncedStickersAsync(List<StickerEntry> localStickers)
        {
            if (!IsRemoteSyncEnabled)
                return;
            if (localStickers == null || localStickers.Count == 0)
                return;

            bool changed = false;
            foreach (var sticker in localStickers.Where(s => s != null && IsLocalStickerKey(s.Key)).ToList())
            {
                try
                {
                    string url = _firebaseBaseUrl + "/stickers.json";
                    string json = JsonConvert.SerializeObject(sticker);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await _http.PostAsync(url, content);
                    if (!response.IsSuccessStatusCode)
                        continue;

                    string body = await response.Content.ReadAsStringAsync();
                    var created = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);
                    if (created != null && created.ContainsKey("name") && !string.IsNullOrWhiteSpace(created["name"]))
                    {
                        sticker.Key = created["name"];
                        changed = true;
                    }
                }
                catch
                {
                }
            }

            if (changed)
                TeamLocalCacheStore.SaveList(_stickersLocalFileName, localStickers);
        }

        /// <summary>
        /// Fetches all stickers from Firebase at /stickers.json and re-renders the board.
        /// Protected by _isRefreshing to prevent overlapping calls.
        /// </summary>
        public async Task RefreshAsync()
        {
            UpdateSyncNotice();
            var localStickers = LoadLocalStickerCache();
            await TrySyncLocalUnsyncedStickersAsync(localStickers);
            localStickers = LoadLocalStickerCache();
            if (_stickers.Count == 0 && localStickers.Count > 0)
            {
                _stickers = localStickers;
                RenderStickers();
            }
            if (!IsRemoteSyncEnabled)
            {
                _stickers = localStickers ?? new List<StickerEntry>();
                RenderStickers();
                return;
            }
            // Prevent overlapping refreshes â€” if one is already running, skip this call.
            // This avoids multiple simultaneous HTTP requests and keeps state consistent.
            if (_isRefreshing)
            {
//                 DebugLogger.Log("[StickerBoard] RefreshAsync: Skipped (refresh already in progress)");
                return;
            }
            _isRefreshing = true;
//             DebugLogger.Log($"[StickerBoard] RefreshAsync: Starting fetch from {_firebaseBaseUrl}/stickers.json");

            try
            {
                // Fetch the entire stickers tree from Firebase
                string url = _firebaseBaseUrl + "/stickers.json";
                var response = await _http.GetAsync(url);
//                 DebugLogger.Log($"[StickerBoard] RefreshAsync: HTTP response status = {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    DebugLogger.Log($"[StickerBoard] RefreshAsync: Failed with status {response.StatusCode}");
                    if (localStickers.Count > 0)
                    {
                        _stickers = localStickers;
                        RenderStickers();
                    }
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                // Firebase returns "null" if the path is empty
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    if (localStickers.Count > 0)
                    {
                        _stickers = localStickers;
                        RenderStickers();
                    }
                    else
                    {
                        _stickers.Clear();
                        RenderStickers();
                    }
                    return;
                }

                // Deserialize: Firebase returns a flat dictionary with auto-generated keys
                var dict = JsonConvert.DeserializeObject<Dictionary<string, StickerEntry>>(json);
                var rawDict = JsonConvert.DeserializeObject<Dictionary<string, JObject>>(json);
                if (dict != null)
                {
//                     DebugLogger.Log($"[StickerBoard] RefreshAsync: Deserialized {dict.Count} stickers from Firebase");
                    // Track stickers that need alias fields added to Firebase
                    var needsPatch = new List<StickerEntry>();

                    // Convert dict to list, assigning the key to each entry
                    _stickers = dict.Select(kv =>
                    {
                        var s = kv.Value;
                        if (s == null)
                            return null;
                        s.Key = kv.Key;
                        bool wasMissingAlias = false;

                        if (rawDict != null && rawDict.TryGetValue(kv.Key, out var raw) && raw != null)
                        {
                            bool isDoneCompat = false;
                            var isDoneToken = raw["isDone"];
                            if (isDoneToken != null && isDoneToken.Type == JTokenType.Boolean)
                                isDoneCompat = isDoneToken.Value<bool>();
                            else
                            {
                                var statusToken = raw["status"];
                                if (statusToken != null && statusToken.Type == JTokenType.String)
                                {
                                    string status = statusToken.Value<string>() ?? "";
                                    if (status.Equals("completed", StringComparison.OrdinalIgnoreCase) ||
                                        status.Equals("done", StringComparison.OrdinalIgnoreCase) ||
                                        status.Equals("finished", StringComparison.OrdinalIgnoreCase))
                                        isDoneCompat = true;
                                }
                            }

                            if (!s.done && isDoneCompat)
                            {
                                s.done = true;
                                wasMissingAlias = true;
                            }
                        }

                        // MOBILE COMPATIBILITY: if mobile saved "text" but not "title", use it
                        if (string.IsNullOrEmpty(s.title) && !string.IsNullOrEmpty(s.text))
                        {
                            s.title = s.text;
//                             DebugLogger.Log($"[StickerBoard] RefreshAsync: Mobile compat - recovered title from 'text' field for key {s.Key}");
                        }
                        if (string.IsNullOrEmpty(s.description) && !string.IsNullOrEmpty(s.content))
                        {
                            s.description = s.content;
//                             DebugLogger.Log($"[StickerBoard] RefreshAsync: Mobile compat - recovered description from 'content' field for key {s.Key}");
                        }

                        // Sync alias fields so next save includes both
                        if (string.IsNullOrEmpty(s.text) && !string.IsNullOrEmpty(s.title))
                        { s.text = s.title; wasMissingAlias = true; }
                        if (string.IsNullOrEmpty(s.content) && !string.IsNullOrEmpty(s.description))
                        { s.content = s.description; wasMissingAlias = true; }

                        if (wasMissingAlias) needsPatch.Add(s);
                        return s;
                    }).Where(s => s != null).ToList();
                    foreach (var localUnsynced in localStickers.Where(s => s != null && IsLocalStickerKey(s.Key)))
                    {
                        if (_stickers.All(s => !string.Equals(s.Key, localUnsynced.Key, StringComparison.OrdinalIgnoreCase)))
                            _stickers.Add(localUnsynced);
                    }

                    if (!string.IsNullOrWhiteSpace(ActiveWorkingStickerKey))
                        ActiveWorkingSticker = _stickers.FirstOrDefault(s => s.Key == ActiveWorkingStickerKey);
                    SaveLocalStickerCache();
//                     DebugLogger.Log($"[StickerBoard] RefreshAsync: {needsPatch.Count} stickers need alias field patching");

                    // AUTO-PATCH: write alias fields to Firebase for existing stickers
                    // that were created before the mobile compatibility update.
                    // This runs once per sticker â€” after patch, wasMissingAlias won't trigger again.
                    foreach (var s in needsPatch)
                    {
                        try
                        {
                            string patchUrl = _firebaseBaseUrl + "/stickers/" + s.Key + ".json";
                            var patch = new Dictionary<string, object>();
                            if (!string.IsNullOrEmpty(s.text)) patch["text"] = s.text;
                            if (!string.IsNullOrEmpty(s.content)) patch["content"] = s.content;
                            if (patch.Count > 0)
                            {
//                                 DebugLogger.Log($"[StickerBoard] RefreshAsync: Patching sticker {s.Key} with alias fields");
                                var req = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl)
                                {
                                    Content = new StringContent(
                                        JsonConvert.SerializeObject(patch), Encoding.UTF8, "application/json")
                                };
                                _ = _http.SendAsync(req); // fire and forget â€” don't block rendering
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"[StickerBoard] RefreshAsync: Error patching sticker {s.Key}: {ex.Message}");
                        }
                    }
                }

                // Re-render the board with the new stickers
//                 DebugLogger.Log($"[StickerBoard] RefreshAsync: Calling RenderStickers() with {_stickers.Count} stickers");
                RenderStickers();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[StickerBoard] RefreshAsync: Exception during fetch: {ex.Message}");
                if (localStickers.Count > 0)
                {
                    _stickers = localStickers;
                    RenderStickers();
                }
            }
            finally
            {
                _isRefreshing = false;
//                 DebugLogger.Log("[StickerBoard] RefreshAsync: Complete, _isRefreshing = false");
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // â•â•â• RENDER â€” TWO-COLUMN MASONRY GRID LAYOUT â•â•â•
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        /// <summary>
        /// Renders all stickers to the flowStickers panel in a two-column masonry grid.
        ///
        /// HOW THE TWO-COLUMN LAYOUT WORKS:
        /// - calculates card width based on available space (minus scrollbar, minus padding/gap)
        /// - Maintains two independent Y positions: col0Y and col1Y (one for each column)
        /// - For each sticker, places it in whichever column is shorter (greedy packing)
        /// - This keeps cards densely packed and balanced between columns
        ///
        /// FILTERING:
        /// - If filter is "All", shows all stickers; otherwise shows only matching type
        /// - Sorted by: done status (false first), then priority (High > Medium > Low), then timestamp
        ///
        /// STICKER STATES:
        /// - Minimized stickers (key in _minimizedKeys) render at 34px tall with just title and minimize button
        /// - Expanded stickers render at 110px tall with full info: type, priority, description, meta, buttons
        /// </summary>
        private void RenderStickers()
        {
            if (_isRendering)
            {
//                 DebugLogger.Log("[StickerBoard] RenderStickers: Skipped (already rendering)");
                return; // prevent re-entrant rendering
            }
            _isRendering = true;
//             DebugLogger.Log("[StickerBoard] RenderStickers: Starting render of stickers");

            try
            {
                flowStickers.SuspendLayout();
                flowStickers.Controls.Clear();
//                 DebugLogger.Log("[StickerBoard] RenderStickers: Cleared previous controls from panel");

                // â”€â”€â”€ APPLY FILTER â”€â”€â”€
                // Get the selected filter value (All, ToDo, Reminder, Bug, or Idea)
                string filter = cmbFilter?.SelectedItem?.ToString() ?? "All";
//                 DebugLogger.Log($"[StickerBoard] RenderStickers: Applying filter: {filter}");
                // Filter stickers: if "All", keep all; otherwise match the type
                var filtered = filter == "All"
                    ? _stickers
                    : _stickers.Where(s => s.type == filter).ToList();
//                 DebugLogger.Log($"[StickerBoard] RenderStickers: Filtered to {filtered.Count} stickers (from {_stickers.Count} total)");

                // â”€â”€â”€ SPLIT INTO ACTIVE AND HISTORY (DONE) â”€â”€â”€
                var activeStickers = filtered.Where(s => !s.done)
                    .OrderByDescending(s => s.priority == "High" ? 3 : s.priority == "Medium" ? 2 : 1)
                    .ThenByDescending(s => s.createdAt)
                    .ToList();

                var historyStickers = filtered.Where(s => s.done)
                    .OrderByDescending(s => s.createdAt)  // Most recently completed first
                    .ToList();

//                 DebugLogger.Log($"[StickerBoard] RenderStickers: Active stickers = {activeStickers.Count}, History stickers = {historyStickers.Count}");

                // â”€â”€â”€ CALCULATE TWO-COLUMN GRID LAYOUT â”€â”€â”€
                // The grid algorithm accounts for padding, gap, and the scrollbar width.
                int padding = 4;  // space on left/right of the entire grid
                int gap = 6;      // space between columns and between rows

                // Available width = panel width - scrollbar - left padding - right padding
                // We subtract SystemInformation.VerticalScrollBarWidth to avoid the scrollbar
                // appearing/disappearing and causing the width to oscillate.
                int availableWidth = flowStickers.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - padding * 2;

                // Each column gets (availableWidth - gap) / 2
                int cardWidth = Math.Max(180, (availableWidth - gap) / 2);
                int col0X = padding;
                int col1X = padding + cardWidth + gap;

                // Remember width to avoid redundant re-renders on Resize
                _lastRenderWidth = flowStickers.ClientSize.Width;

                // â”€â”€â”€ MASONRY PACKING: Track Y position for each column independently â”€â”€â”€
                int col0Y = padding;
                int col1Y = padding;

                // â”€â”€â”€ RENDER ACTIVE (UNDONE) STICKERS FIRST â”€â”€â”€
//                 DebugLogger.Log($"[StickerBoard] RenderStickers: Rendering {activeStickers.Count} active stickers");
                for (int i = 0; i < activeStickers.Count; i++)
                {
                    var card = CreateStickerCard(activeStickers[i], cardWidth);

                    if (col0Y <= col1Y)
                    {
                        card.Location = new Point(col0X, col0Y);
                        col0Y += card.Height + gap;
                    }
                    else
                    {
                        card.Location = new Point(col1X, col1Y);
                        col1Y += card.Height + gap;
                    }

                    flowStickers.Controls.Add(card);
                }
//                 DebugLogger.Log($"[StickerBoard] RenderStickers: Active stickers rendered, col0Y={col0Y}, col1Y={col1Y}");

                // â”€â”€â”€ HISTORY SECTION: DONE STICKERS (hidden by default, toggle with checkbox) â”€â”€â”€
                bool showFinished = _chkShowFinished != null && _chkShowFinished.Checked;
//                 DebugLogger.Log($"[StickerBoard] RenderStickers: showFinished = {showFinished}");

                if (historyStickers.Count > 0)
                {
                    // Align both columns to same Y for the history header
                    int historyY = Math.Max(col0Y, col1Y) + 8;

                    // â”€â”€ History count label (always visible so user knows there ARE finished tasks) â”€â”€
                    var historyHeader = new Label
                    {
                        Text = showFinished
                            ? $"\U0001f4dc  History ({historyStickers.Count} completed)"
                            : $"\U0001f4dc  {historyStickers.Count} finished task{(historyStickers.Count != 1 ? "s" : "")} hidden",
                        Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Italic),
                        ForeColor = _isDarkMode ? Color.FromArgb(100, 110, 130) : Color.FromArgb(130, 140, 160),
                        Location = new Point(padding, historyY),
                        Size = new Size(availableWidth + padding * 2, 20),
                        TextAlign = System.Drawing.ContentAlignment.MiddleLeft
                    };
                    flowStickers.Controls.Add(historyHeader);
                    historyY += 24;

                    // Only render done sticker cards when "Show finished" is checked
                    if (showFinished)
                    {
//                         DebugLogger.Log($"[StickerBoard] RenderStickers: Rendering {historyStickers.Count} finished stickers");
                        // Separator line
                        var separator = new Panel
                        {
                            Location = new Point(padding, historyY),
                            Size = new Size(availableWidth, 1),
                            BackColor = _isDarkMode ? Color.FromArgb(55, 62, 74) : Color.FromArgb(210, 215, 225)
                        };
                        flowStickers.Controls.Add(separator);
                        historyY += 6;

                        // Reset both columns for history section
                        col0Y = historyY;
                        col1Y = historyY;

                        for (int i = 0; i < historyStickers.Count; i++)
                        {
                            var card = CreateStickerCard(historyStickers[i], cardWidth);

                            if (col0Y <= col1Y)
                            {
                                card.Location = new Point(col0X, col0Y);
                                col0Y += card.Height + gap;
                            }
                            else
                            {
                                card.Location = new Point(col1X, col1Y);
                                col1Y += card.Height + gap;
                            }

                            flowStickers.Controls.Add(card);
                        }
//                         DebugLogger.Log($"[StickerBoard] RenderStickers: History stickers rendered");
                    }
                }

                // â”€â”€â”€ SET SCROLL HEIGHT â”€â”€â”€
                // AutoScroll requires us to add a dummy control to establish the scrollable area height
                int totalHeight = Math.Max(col0Y, col1Y) + padding;
                var spacer = new Panel
                {
                    Location = new Point(0, totalHeight - 1),
                    Size = new Size(1, 1),
                    BackColor = Color.Transparent
                };
                flowStickers.Controls.Add(spacer);
//                 DebugLogger.Log($"[StickerBoard] RenderStickers: Layout complete, totalHeight = {totalHeight}px");

                flowStickers.ResumeLayout(true);
//                 DebugLogger.Log("[StickerBoard] RenderStickers: Layout resumed");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[StickerBoard] RenderStickers: Exception during rendering: {ex.Message}");
            }
            finally
            {
                _isRendering = false;
//                 DebugLogger.Log("[StickerBoard] RenderStickers: Complete");
            }
        }

        // â•â•â• COLOR HELPERS â€” MAPS TYPE/PRIORITY TO COLORS â•â•â•
        /// <summary>
        /// Returns the accent color for a sticker type (Todo, Reminder, Bug, Idea).
        /// Used for type badges and type stripes.
        /// </summary>
        private Color GetTypeColor(string type)
        {
            if (_usePersonalPalette)
            {
                switch (type)
                {
                    case "Link": return clrPersonalLink;
                    case "Reminder": return clrPersonalReminder;
                    case "Idea": return clrPersonalIdea;
                    case "ToDo": return clrPersonalNote;
                    default: return clrPersonalNote;
                }
            }

            switch (type)
            {
                case "Reminder": return clrReminder;
                case "Bug": return clrBug;
                case "Idea": return clrIdea;
                default: return clrTodo;
            }
        }

        /// <summary>
        /// Returns the accent color for a priority level (High, Medium, Low).
        /// Used for priority badges.
        /// </summary>
        private Color GetPriorityColor(string priority)
        {
            switch (priority)
            {
                case "High": return clrPrioHigh;
                case "Medium": return clrPrioMed;
                default: return clrPrioLow;
            }
        }

        /// <summary>
        /// Returns the background color for a sticker card based on its type and done status.
        /// Done stickers use a muted gray; otherwise, type determines the background.
        /// </summary>
        private Color GetCardBackground(StickerEntry sticker)
        {
            // Done stickers always show a muted gray background
            if (sticker.done)
                return _isDarkMode ? darkBgDone : lightBgDone;

            // Undone stickers: color by type
            if (_usePersonalPalette)
            {
                if (_isDarkMode)
                {
                    switch (sticker.type)
                    {
                        case "Link": return darkBgPersonalLink;
                        case "Reminder": return darkBgPersonalReminder;
                        case "Idea": return darkBgPersonalIdea;
                        case "ToDo": return darkBgPersonalNote;
                        default: return darkBgPersonalNote;
                    }
                }
                else
                {
                    switch (sticker.type)
                    {
                        case "Link": return lightBgPersonalLink;
                        case "Reminder": return lightBgPersonalReminder;
                        case "Idea": return lightBgPersonalIdea;
                        case "ToDo": return lightBgPersonalNote;
                        default: return lightBgPersonalNote;
                    }
                }
            }

            if (_isDarkMode)
            {
                switch (sticker.type)
                {
                    case "Reminder": return darkBgReminder;
                    case "Bug": return darkBgBug;
                    case "Idea": return darkBgIdea;
                    default: return darkBgTodo;
                }
            }
            else
            {
                switch (sticker.type)
                {
                    case "Reminder": return lightBgReminder;
                    case "Bug": return lightBgBug;
                    case "Idea": return lightBgIdea;
                    default: return lightBgTodo;
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // â•â•â• CREATE CARD â€” BUILDS A SINGLE STICKER CARD PANEL â•â•â•
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        /// <summary>
        /// Creates a Panel representing a single sticker in either minimized or expanded view.
        ///
        /// MINIMIZED VIEW (34px tall):
        /// - Shows type badge, title, expand button (â–¼), and delete button (X)
        /// - Click anywhere to expand (removes key from _minimizedKeys)
        ///
        /// EXPANDED VIEW (110px tall):
        /// - Row 1: Type badge + Priority badge + Done checkbox
        /// - Row 2: Bold title (with strikeout if done)
        /// - Row 3: Description (readable contrast)
        /// - Row 4: Meta (creator/assignee + date) + Minimize button (â–²) + Delete button (X)
        /// - Double-click on title/description to start working on this sticker
        /// - Right-click for context menu: Edit, Mark Done, Change Priority, Change Type, Delete
        /// </summary>
        private Panel CreateStickerCard(StickerEntry sticker, int cardWidth = 200)
        {
//             DebugLogger.Log($"[StickerBoard] CreateStickerCard: Creating card for sticker '{sticker.title}' (key={sticker.Key}, done={sticker.done})");

            Color typeColor = GetTypeColor(sticker.type);
            Color prioColor = GetPriorityColor(sticker.priority);
            string prioText = sticker.priority == "High" ? "HIGH" : sticker.priority == "Medium" ? "MED" : "LOW";
            bool isDone = sticker.done;
            Color cardBg = GetCardBackground(sticker);
            string key = sticker.Key ?? "";
            bool isMinimized = _minimizedKeys.Contains(key);

            // Minimized stickers are 34px tall; expanded stickers are 110px tall
            int cardHeight = isMinimized ? 34 : 110;
//             DebugLogger.Log($"[StickerBoard] CreateStickerCard: {(isMinimized ? "MINIMIZED" : "EXPANDED")} view ({cardHeight}px), type={sticker.type}, priority={sticker.priority}");

            var card = new Panel
            {
                Width = cardWidth,
                Height = cardHeight,
                BackColor = cardBg,
                Margin = new Padding(3, 3, 3, 5),
                Cursor = isMinimized ? Cursors.Hand : Cursors.Default
            };

            // â”€â”€ Left color stripe (type color â€” always visible, even when done) â”€â”€
            // This thin vertical bar on the left identifies the sticker type at a glance.
            var stripe = new Panel
            {
                Dock = DockStyle.Left,
                Width = 5,
                BackColor = typeColor  // always show type color, even when done
            };
            card.Controls.Add(stripe);

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            //  MINIMIZED VIEW (34px tall) â€” click anywhere to expand
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            if (isMinimized)
            {
                // Type badge (small, left side)
                var lblMiniType = new Label
                {
                    Text = (sticker.type ?? "ToDo").ToUpper(),
                    Font = new Font("Segoe UI", 7, FontStyle.Bold),
                    ForeColor = Color.White,
                    BackColor = typeColor,
                    AutoSize = false,
                    Size = new Size(58, 15),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Location = new Point(10, 9)
                };
                card.Controls.Add(lblMiniType);

                // Mini title (middle, truncated with ellipsis)
                var lblMiniTitle = new Label
                {
                    Text = (sticker.title ?? "(no title)"),
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = _isDarkMode ? Color.FromArgb(255, 255, 255) : Color.FromArgb(40, 50, 65),
                    Location = new Point(74, 9),
                    Size = new Size(cardWidth - 120, 16),
                    AutoEllipsis = true
                };
                card.Controls.Add(lblMiniTitle);

                // Expand icon (â–¼) â€” click to expand
                var btnExpand = new Label
                {
                    Text = "\u25bc",
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.FromArgb(100, 110, 130),
                    Location = new Point(cardWidth - 38, 9),
                    AutoSize = true,
                    Cursor = Cursors.Hand
                };
                card.Controls.Add(btnExpand);

                // Delete X (only if admin or creator)
                // PERMISSION CHECK: Only allow deletion if current user is admin OR created this sticker
                bool canDeleteMini = _isAdmin || sticker.createdBy == _currentUserName;
                var btnDelMini = new Label
                {
                    Text = "\u2715",
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = canDeleteMini ? (_isDarkMode ? Color.FromArgb(200, 210, 225) : Color.FromArgb(140, 145, 155)) : Color.FromArgb(120, 120, 120),
                    Location = new Point(cardWidth - 20, 9),
                    AutoSize = true,
                    Cursor = canDeleteMini ? Cursors.Hand : Cursors.Default,
                    Enabled = canDeleteMini
                };
                if (canDeleteMini)
                {
                    btnDelMini.Click += async (s, ev) =>
                    {
//                         DebugLogger.Log($"[StickerBoard] Delete button clicked for minimized sticker '{sticker.title}' (key={sticker.Key})");
                        if (MessageBox.Show("Delete this sticker?", "Confirm",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            try
                            {
                                await DeleteStickerAsync(sticker);
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.Log($"[StickerBoard] Error deleting sticker: {ex.Message}");
                            }
                        }
                    };
                    btnDelMini.MouseEnter += (s, ev) => btnDelMini.ForeColor = Color.FromArgb(222, 53, 11);
                    btnDelMini.MouseLeave += (s, ev) => btnDelMini.ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.FromArgb(140, 145, 155);
                }
                card.Controls.Add(btnDelMini);

                // Click anywhere on the card to expand (removes key from _minimizedKeys)
                EventHandler expandClick = (s, ev) =>
                {
//                     DebugLogger.Log($"[StickerBoard] Expand clicked for minimized sticker '{sticker.title}'");
                    _minimizedKeys.Remove(key);
                    RenderStickers();
                };
                card.Click += expandClick;
                lblMiniTitle.Click += expandClick;
                lblMiniType.Click += expandClick;
                btnExpand.Click += expandClick;
                lblMiniTitle.Cursor = Cursors.Hand;
                lblMiniType.Cursor = Cursors.Hand;

                AddCardBorder(card, typeColor, isDone);

                // Keep quick "Mark Finished/Undone" action available even in minimized view.
                var miniMenu = new ContextMenuStrip();
                var miniDone = new ToolStripMenuItem(isDone ? "\u2610  Mark Undone" : "\u2611  Mark Finished");
                miniDone.Click += async (s, ev) =>
                {
                    await ToggleStickerDoneAsync(sticker.Key, !isDone, sticker);
                };
                miniMenu.Items.Add(miniDone);
                card.ContextMenuStrip = miniMenu;
                foreach (Control c in card.Controls)
                {
                    if (c.ContextMenuStrip == null)
                        c.ContextMenuStrip = miniMenu;
                }

                return card;
            }

            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
            //  FULL (EXPANDED) VIEW (110px tall)
            // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

            // â”€â”€â”€ ROW 1: Type badge + Priority badge + Done checkbox (y=5, height=18) â”€â”€â”€

            // Type badge (small, left)
            var lblType = new Label
            {
                Text = (sticker.type ?? "ToDo").ToUpper(),
                Font = new Font("Segoe UI", 7, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = isDone ? Color.FromArgb(100, 105, 115) : typeColor,
                AutoSize = false,
                Size = new Size(68, 17),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(10, 5)
            };
            card.Controls.Add(lblType);

            // Priority badge (middle-left)
            var lblPrio = new Label
            {
                Text = prioText,
                Font = new Font("Segoe UI", 7, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = isDone ? Color.FromArgb(100, 105, 115) : prioColor,
                AutoSize = false,
                Size = new Size(40, 17),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(82, 5)
            };
            card.Controls.Add(lblPrio);

            // Done checkbox (right side, toggles sticker.done on Firebase)
            var chkDone = new CheckBox
            {
                Checked = isDone,
                Location = new Point(cardWidth - 28, 4),
                Size = new Size(18, 18),
                Cursor = Cursors.Hand
            };
            chkDone.CheckedChanged += async (s, e) =>
            {
                try
                {
//                     DebugLogger.Log($"[StickerBoard] Done checkbox toggled for '{sticker.title}': {chkDone.Checked}");
                    SoundManager.PlayCheckbox();
                    await ToggleStickerDoneAsync(sticker.Key, chkDone.Checked, sticker);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[StickerBoard] Error toggling done status: {ex.Message}");
                }
            };
            card.Controls.Add(chkDone);

            // â”€â”€â”€ ROW 2: Title (bold, large, high-contrast, y=26, height=20) â”€â”€â”€
            Color titleColor;
            if (isDone)
                titleColor = _isDarkMode ? Color.FromArgb(170, 175, 185) : Color.FromArgb(130, 135, 145);
            else if (_isDarkMode)
                titleColor = Color.FromArgb(255, 255, 255);
            else
                titleColor = Color.FromArgb(25, 35, 55);

            var lblTitleText = new Label
            {
                Text = sticker.title ?? "(no title)",
                Font = new Font("Segoe UI", 10, isDone ? FontStyle.Strikeout : FontStyle.Bold),
                ForeColor = titleColor,
                Location = new Point(10, 26),
                Size = new Size(cardWidth - 24, 20),
                AutoEllipsis = true
            };
            card.Controls.Add(lblTitleText);

            // â”€â”€â”€ ROW 3: Description (readable contrast, y=48, height=32) â”€â”€â”€
            Color descColor;
            if (isDone)
                descColor = _isDarkMode ? Color.FromArgb(150, 155, 165) : Color.FromArgb(120, 125, 135);
            else if (_isDarkMode)
                descColor = Color.FromArgb(235, 235, 240);
            else
                descColor = Color.FromArgb(80, 90, 110);

            var lblDesc = new Label
            {
                Text = sticker.description ?? "",
                Font = new Font("Segoe UI", 8),
                ForeColor = descColor,
                Location = new Point(10, 48),
                Size = new Size(cardWidth - 20, 32),
                AutoEllipsis = true
            };
            card.Controls.Add(lblDesc);

            // â”€â”€â”€ ROW 4: Meta (creator/assignee + date, y=88) + Minimize (â–²) + Delete (X) â”€â”€â”€
            string meta = "";
            if (!string.IsNullOrEmpty(sticker.assignedTo))
                meta += sticker.assignedTo;
            else if (!string.IsNullOrEmpty(sticker.createdBy))
                meta += sticker.createdBy;
            if (DateTime.TryParse(sticker.createdAt, out var dt))
                meta += (meta.Length > 0 ? " \u00b7 " : "") + dt.ToLocalTime().ToString("dd MMM HH:mm");

            Color metaColor = _isDarkMode ? Color.FromArgb(210, 215, 225) : Color.FromArgb(120, 130, 145);

            var lblMeta = new Label
            {
                Text = meta,
                Font = new Font("Segoe UI", 7),
                ForeColor = metaColor,
                Location = new Point(10, 88),
                Size = new Size(cardWidth - 60, 16),
                AutoEllipsis = true
            };
            card.Controls.Add(lblMeta);

            // Minimize button (â–²) â€” click to minimize this card to 34px
            var btnMinimize = new Label
            {
                Text = "\u25b2",
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = _isDarkMode ? Color.FromArgb(130, 140, 155) : Color.FromArgb(130, 140, 155),
                Location = new Point(cardWidth - 38, 86),
                AutoSize = true,
                Cursor = Cursors.Hand
            };
            btnMinimize.Click += (s, ev) =>
            {
//                 DebugLogger.Log($"[StickerBoard] Minimize button clicked for '{sticker.title}'");
                if (!string.IsNullOrEmpty(key))
                {
                    _minimizedKeys.Add(key);
//                     DebugLogger.Log($"[StickerBoard] Added {key} to _minimizedKeys, re-rendering");
                    RenderStickers();
                }
            };
            btnMinimize.MouseEnter += (s, ev) => btnMinimize.ForeColor = Color.FromArgb(255, 127, 80);
            btnMinimize.MouseLeave += (s, ev) => btnMinimize.ForeColor = _isDarkMode ? Color.FromArgb(130, 140, 155) : Color.FromArgb(130, 140, 155);
            card.Controls.Add(btnMinimize);

            // Delete X (only if admin or creator)
            // PERMISSION CHECK: Only allow deletion if current user is admin OR created this sticker
            bool canDeleteExp = _isAdmin || sticker.createdBy == _currentUserName;
            var btnDel = new Label
            {
                Text = "\u2715",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = canDeleteExp ? (_isDarkMode ? Color.FromArgb(130, 140, 155) : Color.FromArgb(130, 140, 155)) : Color.FromArgb(120, 120, 120),
                Location = new Point(cardWidth - 20, 85),
                AutoSize = true,
                Cursor = canDeleteExp ? Cursors.Hand : Cursors.Default,
                Enabled = canDeleteExp
            };
            if (canDeleteExp)
            {
                btnDel.Click += async (s, ev) =>
                {
//                     DebugLogger.Log($"[StickerBoard] Delete button clicked for expanded sticker '{sticker.title}' (key={sticker.Key})");
                    if (MessageBox.Show("Delete this sticker?", "Confirm",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        try
                        {
                            await DeleteStickerAsync(sticker);
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"[StickerBoard] Error deleting expanded sticker: {ex.Message}");
                        }
                    }
                };
                btnDel.MouseEnter += (s, ev) => btnDel.ForeColor = Color.FromArgb(222, 53, 11);
                btnDel.MouseLeave += (s, ev) => btnDel.ForeColor = _isDarkMode ? Color.FromArgb(130, 140, 155) : Color.FromArgb(130, 140, 155);
            }
            card.Controls.Add(btnDel);

            // â”€â”€ Card border with type-color accent â”€â”€
            AddCardBorder(card, typeColor, isDone);

            // â•â•â• CONTEXT MENU â€” RIGHT-CLICK OPTIONS (EDIT, DELETE, DONE, PRIORITY) â•â•â•
            // All users can see this menu, but Edit/Delete are disabled if they don't have permission
            var ctxMenu = new ContextMenuStrip();

            // PERMISSION CHECK: Only allow editing if current user is admin OR created this sticker
            bool canEdit = _isAdmin || sticker.createdBy == _currentUserName;

            // â”€â”€â”€ EDIT STICKER â”€â”€â”€
            var mnuEdit = new ToolStripMenuItem("\u270f  Edit Sticker");
            mnuEdit.Enabled = canEdit;
            mnuEdit.Click += async (s, ev) =>
            {
//                 DebugLogger.Log($"[StickerBoard] Edit context menu clicked for '{sticker.title}'");
                await EditStickerAsync(sticker);
            };
            ctxMenu.Items.Add(mnuEdit);

            ctxMenu.Items.Add(new ToolStripSeparator());

            // â”€â”€â”€ MARK DONE / UNDONE â”€â”€â”€
            // Toggles the done field without opening the edit dialog
            var mnuDone = new ToolStripMenuItem(isDone ? "\u2610  Mark Undone" : "\u2611  Mark Finished");
            mnuDone.Click += async (s, ev) =>
            {
//                 DebugLogger.Log($"[StickerBoard] Toggle done context menu: '{sticker.title}' -> {!isDone}");
                await ToggleStickerDoneAsync(sticker.Key, !isDone, sticker);
            };
            ctxMenu.Items.Add(mnuDone);

            // â”€â”€â”€ CHANGE PRIORITY SUBMENU â”€â”€â”€
            // Submenu with High / Medium / Low; current priority is marked with âœ“
            var mnuPriority = new ToolStripMenuItem("\u26a1  Change Priority");
            foreach (string prio in new[] { "High", "Medium", "Low" })
            {
                string p = prio;
                var mnuP = new ToolStripMenuItem(p + (sticker.priority == p ? "  \u2713" : ""));
                mnuP.Click += async (s, ev) =>
                {
//                     DebugLogger.Log($"[StickerBoard] Priority changed for '{sticker.title}': {sticker.priority} -> {p}");
                    await PatchStickerFieldAsync(sticker.Key, "priority", p);
                };
                mnuPriority.DropDownItems.Add(mnuP);
            }
            ctxMenu.Items.Add(mnuPriority);

            // â”€â”€â”€ CHANGE TYPE SUBMENU â”€â”€â”€
            // Submenu with ToDo / Reminder / Bug / Idea; current type is marked with âœ“
            var mnuType = new ToolStripMenuItem("\U0001f3f7  Change Type");
            foreach (string tp in new[] { "ToDo", "Reminder", "Bug", "Idea" })
            {
                string t = tp;
                var mnuT = new ToolStripMenuItem(t + (sticker.type == t ? "  \u2713" : ""));
                mnuT.Click += async (s, ev) =>
                {
//                     DebugLogger.Log($"[StickerBoard] Type changed for '{sticker.title}': {sticker.type} -> {t}");
                    await PatchStickerFieldAsync(sticker.Key, "type", t);
                };
                mnuType.DropDownItems.Add(mnuT);
            }
            ctxMenu.Items.Add(mnuType);

            ctxMenu.Items.Add(new ToolStripSeparator());

            // PERMISSION CHECK: Only allow deletion if current user is admin OR created this sticker
            bool canDelete = _isAdmin || sticker.createdBy == _currentUserName;

            // â”€â”€â”€ DELETE STICKER â”€â”€â”€
            // Done stickers are kept as permanent history â€” only undone stickers can be deleted
            if (!isDone)
            {
                var mnuDelete = new ToolStripMenuItem("\U0001f5d1  Delete Sticker");
                mnuDelete.Enabled = canDelete;
                mnuDelete.Click += async (s, ev) =>
                {
//                     DebugLogger.Log($"[StickerBoard] Delete context menu clicked for '{sticker.title}' (key={sticker.Key})");
                    if (MessageBox.Show($"Delete sticker \"{sticker.title}\"?", "Confirm Delete",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        try
                        {
                            await DeleteStickerAsync(sticker);
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"[StickerBoard] Error deleting via context menu: {ex.Message}");
                        }
                    }
                };
                ctxMenu.Items.Add(mnuDelete);
            }

            // Attach context menu to card and all child controls
            card.ContextMenuStrip = ctxMenu;
            foreach (Control c in card.Controls)
            {
                if (c.ContextMenuStrip == null)
                    c.ContextMenuStrip = ctxMenu;
            }

            // â•â•â• DOUBLE-CLICK TO START WORKING â•â•â•
            // Double-clicking title, description, or badges on an undone sticker
            // asks the user if they want to start working on it. If yes, fires
            // OnStartWorkRequested and sets ActiveWorkingSticker.
            if (!isDone)
            {
                EventHandler doubleClickHandler = (s, ev) =>
                {
                    string stickerTitle = sticker.title ?? "(no title)";
//                     DebugLogger.Log($"[StickerBoard] Double-click on sticker '{stickerTitle}' - prompting to start work");
                    var result = MessageBox.Show(
                        $"Do you want to start working on \"{stickerTitle}\"?",
                        "Start Working",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                    {
//                         DebugLogger.Log($"[StickerBoard] User confirmed starting work on '{stickerTitle}'");
                        ActiveWorkingSticker = sticker;
                        ActiveWorkingStickerKey = sticker?.Key;
                        OnStartWorkRequested?.Invoke(sticker);
                    }
                };

                card.DoubleClick += doubleClickHandler;
                lblTitleText.DoubleClick += doubleClickHandler;
                lblDesc.DoubleClick += doubleClickHandler;
                lblType.DoubleClick += doubleClickHandler;
                lblPrio.DoubleClick += doubleClickHandler;
                lblMeta.DoubleClick += doubleClickHandler;
            }

            return card;
        }

        /// <summary>
        /// Draws a border around the card. If done, uses a muted color; otherwise uses type color.
        /// Also adds a top accent line in the type color.
        /// Attaches a Paint event handler to render the border graphics.
        /// </summary>
        private void AddCardBorder(Panel card, Color typeColor, bool isDone)
        {
//             DebugLogger.Log($"[StickerBoard] AddCardBorder: Adding border to card (done={isDone})");
            card.Paint += (s, ev) =>
            {
                // Main border color: muted gray if done, darker gray if undone
                Color borderColor = isDone
                    ? (_isDarkMode ? Color.FromArgb(80, 85, 95) : Color.FromArgb(210, 212, 218))
                    : (_isDarkMode ? Color.FromArgb(90, 95, 108) : Color.FromArgb(195, 200, 210));

                using (var pen = new Pen(borderColor))
                    ev.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);

                // Top accent line in type color (only for undone stickers)
                if (!isDone)
                {
                    using (var pen = new Pen(typeColor, 2))
                        ev.Graphics.DrawLine(pen, 5, 0, card.Width - 1, 0);
                }
            };
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // â•â•â• ADD NEW STICKER â€” OPENS DIALOG TO CREATE A NEW TASK â•â•â•
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        /// <summary>
        /// Handles the "New" button click. Opens StickerDialog to get sticker details from the user,
        /// then POSTs the new sticker to Firebase.
        ///
        /// MUTE CHECK: If the user is muted, they cannot create new stickers. The _isMutedCheck
        /// function is called to verify this before opening the dialog.
        /// </summary>
        private async void BtnAdd_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log($"[StickerBoard] BtnAdd_Click: 'New' button clicked by {_currentUserName}");

            // Check if user is muted (muted users cannot add stickers)
            if (_isMutedCheck(_currentUserName))
            {
//                 DebugLogger.Log($"[StickerBoard] BtnAdd_Click: User {_currentUserName} is muted, cannot create sticker");
                MessageBox.Show("You are muted and cannot add stickers.", "Muted",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

//             DebugLogger.Log("[StickerBoard] BtnAdd_Click: Opening StickerDialog");
            using (var dlg = new StickerDialog(
                _isDarkMode,
                null,
                showAssignTo: !_localOnlyMode,
                typeOptions: _usePersonalPalette
                    ? new[] { "Note", "Link", "Reminder", "Idea" }
                    : null))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var sticker = dlg.Result;
                    sticker.createdBy = _currentUserName;
                    sticker.createdAt = DateTime.UtcNow.ToString("o");
                    sticker.done = false;

//                     DebugLogger.Log($"[StickerBoard] BtnAdd_Click: Dialog OK - creating sticker '{sticker.title}' (type={sticker.type})");

                    try
                    {
                        // Create unique local key immediately to prevent accidental overwrite
                        // and keep sticker visible even if Firebase is temporarily unavailable.
                        sticker.Key = "LOCAL_" + Guid.NewGuid().ToString("N");
                        _stickers.Insert(0, sticker);
                        SaveLocalStickerCache();
                        RenderStickers();

                        if (!IsRemoteSyncEnabled)
                        {
                            SoundManager.PlayStickerAdded();
                            return;
                        }

                        // Firebase POST operation: creates a new sticker with an auto-generated key
                        string url = _firebaseBaseUrl + "/stickers.json";
                        string json = JsonConvert.SerializeObject(sticker);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var response = await _http.PostAsync(url, content);
                        if (!response.IsSuccessStatusCode)
                            throw new Exception("Sticker save failed: " + response.StatusCode);

                        string body = await response.Content.ReadAsStringAsync();
                        var created = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);
                        if (created != null && created.ContainsKey("name") && !string.IsNullOrWhiteSpace(created["name"]))
                            sticker.Key = created["name"];

                        SaveLocalStickerCache();
                        SoundManager.PlayStickerAdded();

                        // Force refresh even if another is in progress â€” user just added a sticker
                        _isRefreshing = false;
                        await RefreshAsync();
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[StickerBoard] BtnAdd_Click: Exception creating sticker: {ex.Message}");
                        MessageBox.Show(
                            "Sticker saved locally. It will sync to team board when Firebase is reachable.",
                            "Offline Save",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                }
                else
                {
//                     DebugLogger.Log("[StickerBoard] BtnAdd_Click: Dialog cancelled");
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // â•â•â• EDIT/DELETE STICKER â€” ADMIN AND CREATOR PERMISSIONS â•â•â•
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Opens the edit dialog for a sticker and saves changes to Firebase.
        ///
        /// PERMISSION CHECK: Only admin or the creator can edit a sticker.
        /// If neither, shows a warning and returns without opening the dialog.
        /// </summary>
        private async Task EditStickerAsync(StickerEntry sticker)
        {
//             DebugLogger.Log($"[StickerBoard] EditStickerAsync: Edit requested for '{sticker.title}' (key={sticker.Key})");

            // PERMISSION CHECK: Only allow editing if current user is admin OR created this sticker
            if (!_isAdmin && sticker.createdBy != _currentUserName)
            {
//                 DebugLogger.Log($"[StickerBoard] EditStickerAsync: Permission denied for {_currentUserName} (creator={sticker.createdBy}, isAdmin={_isAdmin})");
                MessageBox.Show("You can only edit your own stickers, or you must be an admin.",
                    "Permission Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

//             DebugLogger.Log($"[StickerBoard] EditStickerAsync: Permission granted, opening StickerDialog");
            using (var dlg = new StickerDialog(
                _isDarkMode,
                sticker,
                showAssignTo: !_localOnlyMode,
                typeOptions: _usePersonalPalette
                    ? new[] { "Note", "Link", "Reminder", "Idea" }
                    : null))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    var updated = dlg.Result;
//                     DebugLogger.Log($"[StickerBoard] EditStickerAsync: Dialog OK - saving changes to '{updated.title}'");
                    updated.Key = sticker.Key;
                    updated.createdBy = sticker.createdBy;
                    updated.createdAt = sticker.createdAt;
                    try
                    {
                        if (!IsRemoteSyncEnabled)
                        {
                            int idx = _stickers.FindIndex(s => s != null && string.Equals(s.Key, sticker.Key, StringComparison.OrdinalIgnoreCase));
                            if (idx >= 0)
                                _stickers[idx] = updated;
                            else
                                _stickers.Insert(0, updated);
                            SaveLocalStickerCache();
                            RenderStickers();
                            return;
                        }

                        // Firebase PUT operation: replaces the entire sticker node with updated data
                        string url = _firebaseBaseUrl + "/stickers/" + sticker.Key + ".json";
                        string json = JsonConvert.SerializeObject(updated);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        var response = await _http.PutAsync(url, content);
//                         DebugLogger.Log($"[StickerBoard] EditStickerAsync: PUT to Firebase completed with status {response.StatusCode}");

                        _isRefreshing = false;
                        await RefreshAsync();
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[StickerBoard] EditStickerAsync: Exception saving changes: {ex.Message}");
                    }
                }
                else
                {
//                     DebugLogger.Log("[StickerBoard] EditStickerAsync: Edit dialog cancelled");
                }
            }
        }

        /// <summary>
        /// Toggles the done/undone status of a sticker.
        /// </summary>
        private async Task<bool> ToggleStickerDoneAsync(string key, bool done, StickerEntry localFallbackSticker = null)
        {
//             DebugLogger.Log($"[StickerBoard] ToggleStickerDoneAsync: key={key}, done={done}");
            var localSticker = !string.IsNullOrWhiteSpace(key)
                ? _stickers.FirstOrDefault(s => s != null && s.Key == key)
                : localFallbackSticker;
            bool hadLocalSticker = localSticker != null;
            bool oldDone = hadLocalSticker && localSticker.done;

            // Optimistic UI/local cache update so user sees the state change immediately.
            if (hadLocalSticker)
            {
                localSticker.done = done;
                SaveLocalStickerCache();
                RenderStickers();
            }

            // If key is missing (local/offline item), keep local state and skip remote patch.
            if (string.IsNullOrWhiteSpace(key))
                return hadLocalSticker;

            bool patched = await PatchStickerFieldAsync(key, "done", done, refreshAfterPatch: false);
            if (patched)
            {
                // Compatibility with clients that use isDone/status.
                await PatchStickerFieldAsync(key, "isDone", done, refreshAfterPatch: false);
                await PatchStickerFieldAsync(key, "status", done ? "Completed" : "In Progress", refreshAfterPatch: false);
            }
            if (!patched)
            {
                // Revert local state if server update failed.
                if (hadLocalSticker)
                {
                    localSticker.done = oldDone;
                    SaveLocalStickerCache();
                    RenderStickers();
                }
                return false;
            }

            _isRefreshing = false;
            await RefreshAsync();
            return true;
        }

        /// <summary>
        /// Updates a single field on a sticker in Firebase using a PATCH operation.
        ///
        /// PATCH OPERATION: Only updates the specified field; other fields are unchanged.
        /// This is safer than PUT, which would replace the entire node.
        /// </summary>
        private async Task<bool> PatchStickerFieldAsync(string key, string field, object value, bool refreshAfterPatch = true)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
//                 DebugLogger.Log("[StickerBoard] PatchStickerFieldAsync: Skipped (key is empty)");
                return false;
            }
            if (!IsRemoteSyncEnabled)
            {
                var local = _stickers.FirstOrDefault(s => s != null && string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
                if (local == null)
                    return false;

                switch ((field ?? "").Trim().ToLowerInvariant())
                {
                    case "done":
                    case "isdone":
                        local.done = Convert.ToBoolean(value);
                        break;
                    case "status":
                        local.done = string.Equals(Convert.ToString(value), "Completed", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "priority":
                        local.priority = Convert.ToString(value);
                        break;
                    case "type":
                        local.type = Convert.ToString(value);
                        break;
                }

                SaveLocalStickerCache();
                if (refreshAfterPatch)
                    RenderStickers();
                return true;
            }

//             DebugLogger.Log($"[StickerBoard] PatchStickerFieldAsync: Patching field '{field}' = {value} for key {key}");
            try
            {
                // Firebase PATCH operation: merges with existing data instead of replacing
                string url = _firebaseBaseUrl + "/stickers/" + key + ".json";
                var patch = new Dictionary<string, object> { { field, value } };
                var req = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = new StringContent(
                        JsonConvert.SerializeObject(patch), Encoding.UTF8, "application/json")
                };
                var response = await _http.SendAsync(req);
//                 DebugLogger.Log($"[StickerBoard] PatchStickerFieldAsync: PATCH completed with status {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    DebugLogger.Log($"[StickerBoard] PatchStickerFieldAsync: PATCH failed ({response.StatusCode}) for {field} on {key}. Body={responseBody}");
                    MessageBox.Show(
                        "Sticker update failed on server. Please try again.",
                        "Sticker Update Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }

                if (refreshAfterPatch)
                {
                    _isRefreshing = false;
                    await RefreshAsync();
                }
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[StickerBoard] PatchStickerFieldAsync: Exception patching field: {ex.Message}");
                MessageBox.Show(
                    "Sticker update failed because of a connection error.",
                    "Sticker Update Failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return false;
            }
        }

        private async Task DeleteStickerAsync(StickerEntry sticker)
        {
            if (sticker == null)
                return;

            _stickers.RemoveAll(s => s != null && string.Equals(s.Key, sticker.Key, StringComparison.OrdinalIgnoreCase));
            SaveLocalStickerCache();
            RenderStickers();

            if (!IsRemoteSyncEnabled || IsLocalStickerKey(sticker.Key))
                return;

            try
            {
                string url = _firebaseBaseUrl + "/stickers/" + sticker.Key + ".json";
                await _http.DeleteAsync(url);
            }
            catch
            {
            }

            _isRefreshing = false;
            await RefreshAsync();
        }

        /// <summary>
        /// Public method: marks the ActiveWorkingSticker as done and clears it.
        /// Called by Form1 when user confirms task is finished on stop.
        /// </summary>
        public async Task MarkActiveWorkingStickerDoneAsync()
        {
            string key = ActiveWorkingSticker?.Key;
            if (string.IsNullOrWhiteSpace(key))
                key = ActiveWorkingStickerKey;

            if (string.IsNullOrWhiteSpace(key))
            {
//                 DebugLogger.Log("[StickerBoard] MarkActiveWorkingStickerDoneAsync: No active working sticker");
                return;
            }

//             DebugLogger.Log($"[StickerBoard] MarkActiveWorkingStickerDoneAsync: Marking '{ActiveWorkingSticker.title}' as done");
            bool marked = await ToggleStickerDoneAsync(key, true, ActiveWorkingSticker);
            if (marked)
            {
                ActiveWorkingSticker = null;
                ActiveWorkingStickerKey = null;
//             DebugLogger.Log("[StickerBoard] MarkActiveWorkingStickerDoneAsync: Active sticker cleared");
            }
        }

        /// <summary>Clears the active working sticker without marking done.</summary>
        public void ClearActiveWorkingSticker()
        {
            if (ActiveWorkingSticker != null)
//                 DebugLogger.Log($"[StickerBoard] ClearActiveWorkingSticker: Clearing '{ActiveWorkingSticker.title}'");
            ActiveWorkingSticker = null;
            ActiveWorkingStickerKey = null;
        }

        /// <summary>Returns a safe sticker snapshot for external consumers (e.g. Ask AI context).</summary>
        public List<StickerEntry> GetStickerSnapshot(int maxCount = 120, bool includeDone = true)
        {
            IEnumerable<StickerEntry> query = _stickers ?? new List<StickerEntry>();
            query = includeDone
                ? query.Where(s => s != null)
                : query.Where(s => s != null && !s.done);

            return query
                .Take(Math.Max(1, maxCount))
                .Select(s => new StickerEntry
                {
                    Key = s.Key,
                    title = s.title,
                    description = s.description,
                    text = s.text,
                    content = s.content,
                    type = s.type,
                    priority = s.priority,
                    createdBy = s.createdBy,
                    assignedTo = s.assignedTo,
                    createdAt = s.createdAt,
                    done = s.done
                })
                .ToList();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // â•â•â• THEME â€” APPLIES DARK/LIGHT MODE TO ALL COMPONENTS â•â•â•
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        /// <summary>
        /// Applies dark or light theme to the panel and all UI components.
        /// Updates all colors and triggers a re-render if stickers are loaded.
        /// </summary>
        public void ApplyTheme(bool darkMode, CustomTheme customTheme = null)
        {
//             DebugLogger.Log($"[StickerBoard] ApplyTheme: darkMode={darkMode}, customTheme={customTheme != null && customTheme.Enabled}");
            _isDarkMode = darkMode;
            UpdateSyncNotice();
            if (customTheme != null && customTheme.Enabled)
            {
//                 DebugLogger.Log("[StickerBoard] ApplyTheme: Using custom theme");
                this.BackColor = customTheme.GetCard();
                lblTitle.ForeColor = customTheme.GetAccent();
                btnAdd.BackColor = customTheme.GetAccent();
                btnAdd.ForeColor = Color.White;
                cmbFilter.BackColor = customTheme.GetInput();
                cmbFilter.ForeColor = customTheme.GetText();
                if (_chkShowFinished != null)
                    _chkShowFinished.ForeColor = customTheme.GetSecondaryText();
            }
            else if (darkMode)
            {
//                 DebugLogger.Log("[StickerBoard] ApplyTheme: Using dark mode theme");
                this.BackColor = ThemeConstants.Dark.BgElevated;
                lblTitle.ForeColor = Color.FromArgb(255, 127, 80);
                btnAdd.BackColor = Color.FromArgb(255, 127, 80);
                btnAdd.ForeColor = Color.White;
                cmbFilter.BackColor = ThemeConstants.Dark.BgInput;
                cmbFilter.ForeColor = ThemeConstants.Dark.TextPrimary;
                if (_chkShowFinished != null)
                    _chkShowFinished.ForeColor = ThemeConstants.Dark.TextSecondary;
            }
            else
            {
//                 DebugLogger.Log("[StickerBoard] ApplyTheme: Using light mode theme");
                this.BackColor = ThemeConstants.Light.BgBase;
                lblTitle.ForeColor = Color.FromArgb(255, 127, 80);
                btnAdd.BackColor = Color.FromArgb(255, 127, 80);
                btnAdd.ForeColor = Color.White;
                cmbFilter.BackColor = ThemeConstants.Light.BgInput;
                cmbFilter.ForeColor = ThemeConstants.Light.TextPrimary;
                if (_chkShowFinished != null)
                    _chkShowFinished.ForeColor = ThemeConstants.Light.TextSecondary;
            }
            // Only re-render if we have stickers loaded â€” prevents wiping the board
            // when ApplyTheme is called before the first RefreshAsync completes
            if (_stickers.Count > 0)
            {
//                 DebugLogger.Log($"[StickerBoard] ApplyTheme: Re-rendering {_stickers.Count} stickers with new theme");
                RenderStickers();
            }
            else
            {
//                 DebugLogger.Log("[StickerBoard] ApplyTheme: No stickers loaded yet, skipping render");
            }
        }
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // STICKER CREATE/EDIT DIALOG
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    /// <summary>
    /// Modal dialog for creating a new sticker or editing an existing one.
    /// Collects: title (required), description (optional), type, priority, assignedTo (optional).
    /// </summary>
    public class StickerDialog : Form
    {
        public StickerEntry Result { get; private set; }

        private TextBox txtTitle;
        private TextBox txtDescription;
        private ComboBox cmbType;
        private ComboBox cmbPriority;
        private TextBox txtAssignedTo;

        public StickerDialog(
            bool darkMode = true,
            StickerEntry existing = null,
            bool showAssignTo = true,
            string[] typeOptions = null)
        {
            bool isEdit = existing != null;
//             DebugLogger.Log($"[StickerDialog] Constructor: {(isEdit ? "EDIT" : "NEW")} mode, darkMode={darkMode}");

            this.Text = isEdit ? "Edit Sticker" : "New Sticker";
            this.Size = new Size(400, showAssignTo ? 370 : 332);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            Color bg = darkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250);
            Color inputBg = darkMode ? Color.FromArgb(38, 44, 56) : Color.White;
            Color fg = darkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59);
            Color labelFg = darkMode ? Color.FromArgb(120, 130, 145) : Color.FromArgb(100, 116, 139);
            Color accent = Color.FromArgb(255, 127, 80);

            this.BackColor = bg;
            this.ForeColor = fg;

            int y = 15, x = 20, w = 340;

            AddLabel("TITLE", x, y, labelFg); y += 20;
            txtTitle = AddInput(x, y, w, inputBg, fg);
            if (isEdit) txtTitle.Text = existing.title ?? "";
            y += 35;

            AddLabel("DESCRIPTION", x, y, labelFg); y += 20;
            txtDescription = AddInput(x, y, w, inputBg, fg);
            txtDescription.Height = 50;
            txtDescription.Multiline = true;
            if (isEdit) txtDescription.Text = existing.description ?? "";
            y += 58;

            AddLabel("TYPE", x, y, labelFg);
            AddLabel("PRIORITY", x + 180, y, labelFg);
            y += 20;

            cmbType = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(160, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10),
                BackColor = inputBg,
                ForeColor = fg
            };
            cmbType.Items.AddRange((typeOptions != null && typeOptions.Length > 0)
                ? typeOptions.Cast<object>().ToArray()
                : new object[] { "ToDo", "Reminder", "Bug", "Idea" });
            cmbType.SelectedIndex = 0;
            if (isEdit && existing.type != null)
            {
                int idx = cmbType.Items.IndexOf(existing.type);
                if (idx >= 0) cmbType.SelectedIndex = idx;
            }
            this.Controls.Add(cmbType);

            cmbPriority = new ComboBox
            {
                Location = new Point(x + 180, y),
                Size = new Size(160, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10),
                BackColor = inputBg,
                ForeColor = fg
            };
            cmbPriority.Items.AddRange(new object[] { "High", "Medium", "Low" });
            cmbPriority.SelectedIndex = 1;
            if (isEdit && existing.priority != null)
            {
                int idx = cmbPriority.Items.IndexOf(existing.priority);
                if (idx >= 0) cmbPriority.SelectedIndex = idx;
            }
            this.Controls.Add(cmbPriority);
            y += 35;

            if (showAssignTo)
            {
                AddLabel("ASSIGN TO (optional)", x, y, labelFg); y += 20;
                txtAssignedTo = AddInput(x, y, w, inputBg, fg);
                if (isEdit) txtAssignedTo.Text = existing.assignedTo ?? "";
                y += 38;
            }

            // Create / Update button
            var btnOK = new Button
            {
                Text = isEdit ? "\U0001f4be  Update" : "\u271a  Create",
                Location = new Point(x, y),
                Size = new Size(160, 40),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += (s, e) =>
            {
//                 DebugLogger.Log("[StickerDialog] Create/Update button clicked");
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    DebugLogger.Log("[StickerDialog] Validation failed: title is empty");
                    MessageBox.Show("Title is required.", "Required",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Result = new StickerEntry
                {
                    title = txtTitle.Text.Trim(),
                    description = txtDescription.Text.Trim(),
                    // MOBILE COMPATIBILITY: also save as "text" and "content"
                    text = txtTitle.Text.Trim(),
                    content = txtDescription.Text.Trim(),
                    type = cmbType.SelectedItem.ToString(),
                    priority = cmbPriority.SelectedItem.ToString(),
                    assignedTo = showAssignTo ? (txtAssignedTo?.Text.Trim() ?? "") : "",
                    // Preserve existing fields when editing
                    Key = isEdit ? existing.Key : null,
                    createdBy = isEdit ? existing.createdBy : null,
                    createdAt = isEdit ? existing.createdAt : null,
                    done = isEdit ? existing.done : false
                };
//                 DebugLogger.Log($"[StickerDialog] Result set: title='{Result.title}', type={Result.type}, priority={Result.priority}");
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(btnOK);

            // Cancel button
            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(x + 180, y),
                Size = new Size(160, 40),
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = darkMode ? Color.FromArgb(38, 44, 56) : Color.FromArgb(200, 200, 210),
                ForeColor = darkMode ? Color.FromArgb(160, 170, 180) : Color.FromArgb(80, 80, 90),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) =>
            {
//                 DebugLogger.Log("[StickerDialog] Cancel button clicked");
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(btnCancel);
//             DebugLogger.Log("[StickerDialog] Dialog UI construction complete");

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private Label AddLabel(string text, int x, int y, Color fg)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8, FontStyle.Bold),
                ForeColor = fg,
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(lbl);
            return lbl;
        }

        private TextBox AddInput(int x, int y, int w, Color bg, Color fg)
        {
            var tb = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 26),
                Font = new Font("Segoe UI", 10),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = bg,
                ForeColor = fg
            };
            this.Controls.Add(tb);
            return tb;
        }
    }
}

