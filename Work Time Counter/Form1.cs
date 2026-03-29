// +------------------------------------------------------------------------------+
// ?                        8 BIT LAB ENGINEERING                               ?
// ?                     WORKFLOW - TEAM TIME TRACKER                            ?
// ?                                                                            ?
// ?  FILE:        Form1.cs (WorlFlow ? Main Application Window)                ?
// ?  PURPOSE:     MAIN FORM ? TIME TRACKING, PANELS, ONLINE STATUS, ADMIN      ?
// ?  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ?
// ?  LICENSE:     OPEN SOURCE                                                  ?
// ?                                                                            ?
// ?  DESCRIPTION:                                                              ?
// ?  This is the main application form ? the "hub" that ties everything        ?
// ?  together. It contains the work timer, data grid, sticker board, chat,     ?
// ?  file sharing, helper wiki, online users panel, and toolbar.               ?
// ?                                                                            ?
// ?  LAYOUT:                                                                   ?
// ?    LEFT:   Sticker Board (task cards)                                      ?
// ?    CENTER: Work Timer + Description + Data Grid (time logs)                ?
// ?    RIGHT:  Team Status Panel (who's online) + Helper Wiki                  ?
// ?    BOTTOM: Team Chat (left) + File Share (right)                           ?
// ?    TOP:    Toolbar (toggle buttons, ping, standup, settings)               ?
// ?                                                                            ?
// ?  KEY SYSTEMS:                                                              ?
// ?  - TIME TRACKING: Start/Stop with Firebase real-time sync                  ?
// ?  - ONLINE STATUS: Heartbeat every 60s, status panel updates                ?
// ?  - PANEL SYSTEM: Resizable panels with drag splitters                      ?
// ?  - ADMIN FEATURES: Delete entries, manage team via Settings                ?
// ?  - AUTO-STOP: Safety timer stops work after 10 hours                       ?
// ?  - JIRA INTEGRATION: Import tasks from Jira Cloud                         ?
// ?  - POMODORO TIMER: 25/5/15 minute work cycles                             ?
// ?                                                                            ?
// ?  GitHub: https://github.com/8BitLabEngineering                             ?
// +------------------------------------------------------------------------------+

using Firebase.Database;
using Firebase.Database.Query;
using iTextSharp.text;
using iTextSharp.text.pdf;
// RESOLVE AMBIGUITY: Both System.Drawing and iTextSharp.text define "Font"
// This alias ensures "Font" always means System.Drawing.Font in this file
using Font = System.Drawing.Font;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Work_Time_Counter
{
    public partial class WorlFlow : Form
    {
        // -- WIN32: FLASH TASKBAR ICON --
        // Used to flash the taskbar when alarms trigger or pings arrive
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        // Structure for taskbar flash parameters
        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }
        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        // --- APPLICATION FIELDS ? TIMERS, PANELS, STATE VARIABLES ---
        // Includes: work timer, UI panels (sticker board, chat, file share, helper wiki),
        // online status tracking, theme system, and Firebase sync mechanisms.
        // ============================================================
        // FIELDS
        // ============================================================

        // Version management for update checking
        private string currentAppVersion = "1.0.3";
        public class AppVersionInfo
        {
            public string latestVersion { get; set; }
            public string downloadUrl { get; set; }
            public string releaseNotes { get; set; }
        }
        private DebugForm _debugForm;

        private DateTime _startTime;
        private Timer _workingTimer;
        private TimeSpan _elapsedTime;

        // START BUTTON ANIMATION ? pulsing "Working..." while timer runs
        private Timer _startBtnAnimTimer;
        private int _startBtnAnimDots = 0;
        private float _startBtnPulsePhase = 0f;  // 0..2PI smooth sine pulse
        private Color _startBtnOriginalColor;
        private string _startBtnOriginalText;
        // Base Firebase URL for storing time logs ? loaded from UserStorage config
        private readonly string firebaseUrl = UserStorage.GetFirebaseLogsUrl();
        private System.Windows.Forms.Timer autoRefreshTimer;
        private System.Windows.Forms.Timer onlineCheckTimer;
        private string currentLiveLogKey;  // Tracks the live timer entry being edited

        // -- USER AND TEAM DATA --
        // Current logged-in user and all team members
        private UserInfo _currentUser;
        private List<UserInfo> _allUsers;

        // -- ONLINE STATUS PANEL --
        // Right sidebar displaying who's online, their status, and DM/admin options
        private Panel panelOnlineUsers;
        private Label labelOnlineTitle;
        private List<OnlineUserControl> onlineUserControls = new List<OnlineUserControl>();
        private double _baseWeeklyHoursAtStart = 0; // Weekly hours snapshot when timer started (for live progress bar)
        private bool isDarkMode = true;
        private CustomTheme _customTheme = null; // Active custom theme (null = use dark/light)
        private Timer _holdTimer;

        // -- NEW: Sticker board + Chat + File Share + Helper panels --
        private StickerBoardPanel _stickerBoard;
        private ChatPanel _chatPanel;
        private FileSharePanel _fileSharePanel;
        private HelperPanel _helperPanel;
        private Form _helperWindow;
        private StickerBoardPanel _personalStickerBoard;
        private Form _personalBoardWindow;
        // Panel toggle buttons (Board, Chat, Team, Files, Calendar) moved to Settings dialog
        private Button btnToggleHelper;
        private Button btnSettings; // ? Settings ? visible to admin only
        private Panel _toolbarPanel; // toolbar at top of middle area
        private Panel _activeTeamBadge;
        private Label _labelActiveTeamTitle;
        private Label _labelActiveTeam;

        // -- Floating Day Organizer window --
        private DayOrganizerForm _dayOrganizerWindow;

        // Splitter bars for resizable panels
        private Panel _splitterHoriz;      // between main area and chat/fileshare (drag up/down)
        private Panel _splitterVertBottom; // between chat and fileshare (drag left/right)
        private Timer _panelRefreshTimer;
        private bool _boardVisible = true;
        private bool _chatVisible = true;
        private bool _teamVisible = true;
        private bool _filesVisible = true;
        private bool _helperVisible = false;  // hidden by default
        private int _bottomPanelHeight = 0;
        private bool _weatherVisible = true;
        private string _weatherMode = "daily";
        private bool _askAiVisible = false;
        private bool _weatherLocationResolved = false;
        private double _weatherLat = 0;
        private double _weatherLon = 0;
        private string _weatherCity = "";
        private Panel _weatherPanel;
        private Label _weatherTitle;
        private Label _weatherSummary;
        private Label _weatherDetails;
        private Timer _weatherTimer;
        private Panel _askAiPanel;
        private Label _askAiStatus;
        private Button _btnAskAi;
        private TableLayoutPanel _personalBoardHostLayout;
        private bool _personalBoardVisible = false;
        private bool _personalBoardOnRight = true;
        private bool _isUpdatingPersonalBoardBounds = false;
        private bool _aiChatVisible = false;
        private bool _aiChatOnRight = true;
        private bool _isUpdatingAiChatBounds = false;
        private bool _aiChatGreeted = false;
        private bool _sleepWarningShown = false;
        private Form _aiChatWindow;
        private RichTextBox _aiChatHistory;
        private TextBox _aiChatInput;
        private Button _btnAiChatSend;
        private Button _btnAiFontSmall;
        private Button _btnAiFontMedium;
        private Button _btnAiFontBig;
        private string _aiChatFontSizeName = "Small";
        private float _aiChatFontSize = 9.5f;

        // -- CALENDAR / ORGANIZER MODULE --
        private CalendarPanel _calendarPanel;
        private OrganizerSettings _orgSettings;
        private Timer _alarmCheckTimer;
        private bool _calendarVisible = true;

        // -- PROJECT FOLDER MODULE --
        private ProjectFolderPanel _projectFolderPanel;
        private bool _projectFolderVisible = false;

        // -- ALARM SERVICE --
        private AlarmService _alarmService;

        // -- CROSS-DEVICE SESSION SYNC --
        private Timer _sessionSyncTimer;
        private string _sessionStartedByPlatform = null;

        // -- PING AND NOTIFICATION TRACKING --
        // Manages incoming pings and prevents duplicate alert notifications
        private Dictionary<string, string> _previousUserStatuses = new Dictionary<string, string>();
        private string _lastPingTimestamp = "";
        // Tracks whether the first ping check has been done (skip alerts on first load)
        private bool _firstPingCheckDone = false;
        // Tracks Firebase keys of pings that user already acknowledged (pressed OK)
        private HashSet<string> _acknowledgedPingKeys = new HashSet<string>();

        // -- NEW: Date range filter + Export + Project category --
        private DateTimePicker _dtpFrom;
        private DateTimePicker _dtpTo;
        private Button _btnExport;
        private ComboBox _cmbProject;
        private Label _lblHoursToday;

        // -- NEW: Feature panels & timers --
        private JiraIntegration _jira = new JiraIntegration();
        private PomodoroPanel _pomodoroPanel;
        private AdminDashboardPanel _adminDashboard;
        private Timer _autoStopTimer;
        private DateTime _workStartedAt;
        private const int AUTO_STOP_HOURS = 10; // auto-stop after 10 hours
        private List<RecurringTask> _recurringTasks = new List<RecurringTask>();
        private string _recurringTasksPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorkTimeCounter", "recurring_tasks.json");

        // Single shared HttpClient to avoid socket exhaustion
        private static readonly HttpClient _httpClient = new HttpClient();
        private const string DefaultAiEndpoint = "https://openrouter.ai/api/v1/chat/completions";
        private const string DefaultAiModel = "openai/gpt-4o-mini";
        private const string PersonalAiSettingsCacheFile = "ai_user_settings.json";

        private class PersonalAiSettings
        {
            public bool Enabled { get; set; }
            public string Endpoint { get; set; } = "";
            public string Model { get; set; } = "";
            public string ApiKey { get; set; } = "";
        }

        // -- OPEN DM WINDOWS ? track to prevent duplicates --
        private Dictionary<string, DirectMessageForm> _openDmForms = new Dictionary<string, DirectMessageForm>(StringComparer.OrdinalIgnoreCase);

        // -- SYSTEM TRAY ? NotifyIcon + DM polling --
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private Timer _dmCheckTimer;
        private Dictionary<string, int> _lastKnownDmCounts = new Dictionary<string, int>();
        private bool _trayInitialized = false;

        // -- WEEKLY HOURS TRACKING ? for progress bars & motivational messages --
        private Dictionary<string, double> _lastNotifiedHours = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, int> _milestoneShowCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _milestoneDismissed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string _lastBalloonMilestoneKey = null; // Track which milestone the current balloon belongs to
        private Timer _weeklyHoursTimer;
        private ToolTip _clockNoticeToolTip;
        private double _sessionTodayHoursAtStart = 0;
        private double _sessionWeekHoursAtStart = 0;
        private bool _sessionLimitSnapshotReady = false;
        private bool _continuous2hWarned = false;
        private bool _continuous4hWarned = false;
        private bool _weeklyLimitWarnedInSession = false;
        private bool _dailyLimitReachedInSession = false;
        private bool _dailyLimitOverrideAccepted = false;
        private bool _restoreMainWindowAsMaximized = false;
        private readonly string _mainWindowStatePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorkFlow",
            "main_window_state.json");
        private const int HelperWindowDefaultWidth = 320;
        private const int PersonalBoardWindowDefaultWidth = 540;
        private const int AiChatWindowDefaultWidth = 430;

        private sealed class AiChatMessage
        {
            public string Role { get; set; } = "user";
            public string Content { get; set; } = "";
        }

        private readonly List<AiChatMessage> _aiChatMessages = new List<AiChatMessage>();

        private sealed class MainWindowState
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public bool IsMaximized { get; set; }
        }

        // --- CONSTRUCTOR ? INITIALIZES USER, TIMERS, AND ALL PANELS ---
        // ============================================================
        // CONSTRUCTOR ? accepts user + users list from Program.cs
        // ============================================================
        public WorlFlow(UserInfo currentUser, List<UserInfo> allUsers)
        {
//             DebugLogger.Log("[Form1] Constructor starting ? initializing all timers, panels, and Firebase sync");
            InitializeComponent();
            EnsureLogoImage();

            // ── BACKUP MANAGER: Initialize + verify/repair from mirror on startup ──
            BackupManager.Initialize();
            int restoredFiles = BackupManager.VerifyAndRepair();
            if (restoredFiles > 0)
                DebugLogger.Log($"[Form1] BackupManager restored {restoredFiles} files from mirror backup on startup");

            _currentUser = currentUser;
            _allUsers = allUsers;
//             DebugLogger.Log($"[Form1] User={_currentUser?.Name ?? "unknown"}, TeamMembers={_allUsers?.Count ?? 0}");
//             DebugLogger.Log("[Form1] Cleaning up mobile duplicate user entries (like '6J82GG_Blagoy')");

            

            // -- CLEAN UP MOBILE DUPLICATES ON STARTUP --
            // Remove entries like "6J82GG_Blagoy" if "Blagoy" already exists
            // These get saved to users.bit from mobile joins and persist across sessions
            var nameSet = new HashSet<string>(_allUsers.Select(u => u.Name), StringComparer.OrdinalIgnoreCase);
//             DebugLogger.Log($"[Form1] Removing users from list");
            _allUsers.RemoveAll(u =>
            {
                string n = u.Name;
                if (n.Contains("_"))
                {
                    string after = n.Substring(n.IndexOf('_') + 1);
                    if (after.Length > 0 && !string.Equals(after, n, StringComparison.OrdinalIgnoreCase)
                        && nameSet.Contains(after))
                        return true; // Remove "6J82GG_Blagoy" ? "Blagoy" exists
                }
                if (n.Contains(" "))
                {
                    string after = n.Substring(n.IndexOf(' ') + 1);
                    if (after.Length > 0 && !string.Equals(after, n, StringComparison.OrdinalIgnoreCase)
                        && nameSet.Contains(after))
                        return true;
                }
                return false;
            });
            // Save cleaned list so duplicates don't come back
            if (_allUsers.Count < nameSet.Count)
                UserStorage.SaveUsers(_allUsers);

            if (_currentUser == null)
            {
                this.Load += (s, e) => this.Close();
                return;
            }

//             DebugLogger.Log("[Form1] Loading team from storage");
            var initTeam = UserStorage.LoadTeam();
            bool isAnyAdmin = _currentUser.IsAdmin || (initTeam != null && initTeam.HasAdminPrivileges(_currentUser.Name));
//             DebugLogger.Log($"[Form1] Title bar ? User={_currentUser.Name}, IsAdmin={_currentUser.IsAdmin}");
            this.Text = _currentUser.IsAdmin
                ? $"Work Time Counter ? {_currentUser.Name} (Admin)"
                : isAnyAdmin
                    ? $"Work Time Counter ? {_currentUser.Name} (Asst. Admin)"
                    : $"Work Time Counter ? {_currentUser.Name}";
            if (!TryRestoreMainWindowState())
                this.Size = new System.Drawing.Size(1200, 600);

            BuildOnlineUsersPanel();
//             DebugLogger.Log("[Form1] Building online users panel ? team status sidebar");

            timer1 = new Timer();
            timer1.Interval = 1000;
            timer1.Tick += Timer_Tick;
            timer1.Start();
//             DebugLogger.Log("[Form1] Main UI timer started (1000ms interval ? updates clock + button animation)");

            buttonDelete.Click += buttonDelete_Click;
            dataGridView1.ReadOnly = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            InitializeTheme();
            StyleButtons();
            SetupUserFilterComboBox();
            InitializeDataGridFeatures();
            InitializeHoldToToggleDebug();

            // -- Build UI panels in constructor (so form appears with layout ready) --
            BuildStickerAndChatPanels();
            BuildCalendarPanel();
            BuildWeatherPanel();
            BuildPersonalBoard();
            BuildAiChatPanel();
            SetupSystemTray();

            // -- Load local settings (fast LiteDB reads) --
            _jira.LoadSettings();
            LoadRecurringTasks();

            // ============================================================
            // DEFERRED STARTUP: All network calls, background timers, and
            // Firebase sync are started AFTER the form is visible.
            // This makes the window appear instantly.
            // ============================================================
            this.Shown += async (s, e) =>
            {
                if (_restoreMainWindowAsMaximized)
                    this.WindowState = FormWindowState.Maximized;
                ApplyMainColumnLayout();
                UpdateHelperWindowBounds();
                if (_personalBoardVisible)
                    ShowPersonalBoardWindow();
                else
                    HidePersonalBoardWindow();
                UpdatePersonalBoardWindowBounds();
                if (_aiChatVisible)
                    ShowAiChatWindow();
                else
                    HideAiChatWindow();
                UpdateAiChatWindowBounds();
                _ = RefreshWeatherAsync();

                // -- Online heartbeat timer (60s) --
                onlineCheckTimer = new System.Windows.Forms.Timer();
                onlineCheckTimer.Interval = 60000;
                onlineCheckTimer.Tick += async (s2, e2) =>
                {
                    await RefreshOnlineStatusAsync();
                    await SendOnlineHeartbeatAsync();
                };
                onlineCheckTimer.Start();

                // -- Firebase: heartbeat + online status --
                _ = SendHeartbeatThenRefreshAsync();
                _ = CheckForUpdateAsync();

                // -- Alarm check timer (30s) --
                SetupAlarmCheckTimer();

                // -- Auto-stop safety timer (60s, 10-hour limit) --
                _autoStopTimer = new Timer { Interval = 60000 };
                _autoStopTimer.Tick += AutoStopTimer_Tick;
                _autoStopTimer.Start();

                // -- Cross-device session sync (10s) --
                _sessionSyncTimer = new Timer { Interval = 10000 };
                _sessionSyncTimer.Tick += async (s2, e2) => await CheckRemoteSessionSync();
                _sessionSyncTimer.Start();
                _ = CheckRemoteSessionSync();

                // -- DM polling --
                SetupDmCheckTimer();

                // -- Weekly hours (5 min refresh, initial load after 2s) --
                _weeklyHoursTimer = new Timer { Interval = 300000 };
                _weeklyHoursTimer.Tick += async (s2, e2) => await RefreshWeeklyHoursAsync();
                _weeklyHoursTimer.Start();
                _ = Task.Delay(2000).ContinueWith(_ => { if (!IsDisposed) BeginInvoke(new Action(async () => await RefreshWeeklyHoursAsync())); });

                // ── BACKUP: Run initial mirror backup after startup is complete ──
                _ = Task.Run(() => BackupManager.RunMirrorBackup());

                // ── BACKUP: GitHub auto-backup timer (checks interval setting, 5 min tick) ──
                var backupTimer = new Timer { Interval = 300000 }; // 5 min
                backupTimer.Tick += async (s2, e2) => await BackupManager.CheckAutoGitHubBackupAsync();
                backupTimer.Start();
            };

            // On form close: cleanup all timers, remove online status, stop file server
            this.FormClosing += async (s, e) =>
            {
                SaveMainWindowState();

                // ── BACKUP: Final mirror backup before exit (saves everything) ──
                try { BackupManager.RunMirrorBackup(); }
                catch (Exception ex) { DebugLogger.Log($"[Form1] Close backup error: {ex.Message}"); }

                try { _helperWindow?.Close(); } catch { }
                try { _personalBoardWindow?.Close(); } catch { }
                try { _aiChatWindow?.Close(); } catch { }

                // Save panel layout before closing
                SaveCurrentPanelLayout();

                // Stop all timers to prevent callbacks after disposal
                timer1?.Stop();
                _workingTimer?.Stop();
                _autoStopTimer?.Stop();
                _panelRefreshTimer?.Stop();
                onlineCheckTimer?.Stop();
                autoRefreshTimer?.Stop();
                _dmCheckTimer?.Stop();
                _weeklyHoursTimer?.Stop();
                _startBtnAnimTimer?.Stop();
                _alarmCheckTimer?.Stop();
                _weatherTimer?.Stop();

                await RemoveOnlineSignalAsync();
//                     DebugLogger.Log("[Form1] Removing online status signal before exit");

                // Dispose timers
                timer1?.Dispose();
                _workingTimer?.Dispose();
                _autoStopTimer?.Dispose();
                _panelRefreshTimer?.Dispose();
                onlineCheckTimer?.Dispose();
                autoRefreshTimer?.Dispose();
                _dmCheckTimer?.Dispose();
                _startBtnAnimTimer?.Dispose();
                _alarmCheckTimer?.Dispose();
                _weatherTimer?.Dispose();

                // Remove tray icon on exit
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
//                     DebugLogger.Log("[Form1] Hiding system tray icon");
                    _trayIcon.Dispose();
                }

                // STOP P2P FILE SERVER ON EXIT
                _fileSharePanel?.StopTcpFileServer();
//                     DebugLogger.Log("[Form1] Stopping P2P file server");
                _projectFolderPanel?.Dispose();
                _alarmService?.Stop();
            };
            this.LocationChanged += (s, e) =>
            {
                ApplyMainColumnLayout();
                UpdateHelperWindowBounds();
                UpdatePersonalBoardWindowBounds();
                UpdateAiChatWindowBounds();
            };
            this.SizeChanged += (s, e) =>
            {
                ApplyMainColumnLayout();
                UpdateHelperWindowBounds();
                UpdatePersonalBoardWindowBounds();
                UpdateAiChatWindowBounds();
            };
            // this.AutoSize = true; // disabled ? breaks docked panels
        }

        private bool TryRestoreMainWindowState()
        {
            try
            {
                if (!File.Exists(_mainWindowStatePath))
                    return false;

                string json = File.ReadAllText(_mainWindowStatePath);
                if (string.IsNullOrWhiteSpace(json))
                    return false;

                var state = JsonConvert.DeserializeObject<MainWindowState>(json);
                if (state == null || state.Width < 500 || state.Height < 400)
                    return false;

                var targetBounds = new System.Drawing.Rectangle(state.X, state.Y, state.Width, state.Height);
                bool intersectsAnyScreen = Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(targetBounds));
                if (!intersectsAnyScreen)
                {
                    var wa = Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);
                    int width = Math.Min(state.Width, wa.Width - 40);
                    int height = Math.Min(state.Height, wa.Height - 40);
                    int x = wa.Left + Math.Max(0, (wa.Width - width) / 2);
                    int y = wa.Top + Math.Max(0, (wa.Height - height) / 2);
                    targetBounds = new System.Drawing.Rectangle(x, y, width, height);
                }

                this.StartPosition = FormStartPosition.Manual;
                this.Bounds = targetBounds;
                _restoreMainWindowAsMaximized = state.IsMaximized;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void EnsureLogoImage()
        {
            if (pictureBoxLogo == null)
                return;

            if (pictureBoxLogo.Image != null)
            {
                pictureBoxLogo.Visible = true;
                return;
            }

            try
            {
                string startup = Application.StartupPath;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] candidates =
                {
                    Path.Combine(startup, "logo.png"),
                    Path.Combine(baseDir, "logo.png"),
                    Path.Combine(startup, "Start.png"),
                    Path.Combine(baseDir, "Start.png"),
                    Path.GetFullPath(Path.Combine(startup, "..", "..", "logo.png")),
                    Path.GetFullPath(Path.Combine(startup, "..", "..", "Start.png"))
                };

                foreach (var path in candidates)
                {
                    if (!File.Exists(path))
                        continue;

                    pictureBoxLogo.Image = new Bitmap(path);
                    break;
                }
            }
            catch { }

            if (pictureBoxLogo.Image == null && this.Icon != null)
            {
                try { pictureBoxLogo.Image = this.Icon.ToBitmap(); } catch { }
            }

            pictureBoxLogo.Visible = true;
            pictureBoxLogo.BringToFront();
        }

        private void SaveMainWindowState()
        {
            try
            {
                var bounds = this.WindowState == FormWindowState.Normal ? this.Bounds : this.RestoreBounds;
                if (bounds.Width < 500 || bounds.Height < 400)
                    return;

                string dir = Path.GetDirectoryName(_mainWindowStatePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var state = new MainWindowState
                {
                    X = bounds.X,
                    Y = bounds.Y,
                    Width = bounds.Width,
                    Height = bounds.Height,
                    IsMaximized = this.WindowState == FormWindowState.Maximized
                };

                File.WriteAllText(_mainWindowStatePath, JsonConvert.SerializeObject(state, Formatting.Indented));
            }
            catch
            {
            }
        }

        // --- DEBUG FORM SETUP ? HOLD LOGO 5 SECONDS TO TOGGLE DEBUG PANEL ---
        // Creates the debug form, initializes the static DebugLogger,
        // and wires up the logo hold-to-toggle gesture.
        /// <summary>Initialize debug form and hold-to-toggle gesture on logo.
        /// Hold logo 5 seconds to show/hide debug output panel.
        /// </summary>
        private void InitializeHoldToToggleDebug()
        {
            _debugForm = null;
            _holdTimer = null;
        }

        // --- ONLINE USERS PANEL ? RIGHT SIDEBAR WITH AVATARS AND STATUS ---
        // ============================================================
        // BUILD THE "WHO'S ONLINE" PANEL (right side)
        // ============================================================
        /// <summary>Create the Team Status panel (right sidebar).
        /// Displays all team members with their online/working/offline status.
        /// Includes user avatars, context menus for DM/mute/admin, and version info.
        /// </summary>
        private void BuildOnlineUsersPanel()
        {
            // Right sidebar panel ? 320px wide, docked to right, scrollable for many users
            panelOnlineUsers = new Panel
            {
                Width = 320,
                Dock = DockStyle.Right,
                BackColor = Color.FromArgb(245, 245, 250),
                BorderStyle = BorderStyle.FixedSingle,
                AutoScroll = true,
                Padding = new Padding(10)
            };

            labelOnlineTitle = new Label
            {
                Text = "Team Status",
                Font = ThemeConstants.FontTitle,
                ForeColor = isDarkMode ? ThemeConstants.Dark.TextSecondary : ThemeConstants.Light.TextSecondary,
                Dock = DockStyle.Top,
                Height = 36,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(ThemeConstants.SpaceS, 0, 0, 0)
            };
            panelOnlineUsers.Controls.Add(labelOnlineTitle);

            int yPos = 45;
//             DebugLogger.Log("[Form1] Loading team from storage");
            var team = UserStorage.LoadTeam();
            bool viewerIsAdmin = _currentUser.IsAdmin || (team != null && team.HasAdminPrivileges(_currentUser.Name));

            foreach (var user in _allUsers)
            {
                // -- SKIP MOBILE DUPLICATES: "6J82GG_Blagoy" when "Blagoy" exists --
                bool skipBuild = false;
                string bn = user.Name;
                if (bn.Contains("_"))
                {
                    string afterUnder = bn.Substring(bn.IndexOf('_') + 1);
                    if (afterUnder.Length > 0 && _allUsers.Any(other =>
                        !string.Equals(other.Name, bn, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(other.Name, afterUnder, StringComparison.OrdinalIgnoreCase)))
                        skipBuild = true;
                }
                if (!skipBuild && bn.Contains(" "))
                {
                    string afterSpace = bn.Substring(bn.IndexOf(' ') + 1);
                    if (afterSpace.Length > 0 && _allUsers.Any(other =>
                        !string.Equals(other.Name, bn, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(other.Name, afterSpace, StringComparison.OrdinalIgnoreCase)))
                        skipBuild = true;
                }
                if (skipBuild) continue;

                bool isCurrentUser = user.Name == _currentUser.Name;
                var ctrl = new OnlineUserControl(user, isCurrentUser, viewerIsAdmin);
                ctrl.ViewerName = _currentUser.Name;
                ctrl.Location = new Point(10, yPos);
                ctrl.Width = 290;

                // Wire up context menu events
                ctrl.OnSendDirectMessage += (targetUser) =>
                {
                    OpenOrFocusDmForm(targetUser);
                };
                ctrl.OnMuteUser += (targetUser) =>
                {
//                     DebugLogger.Log("[Form1] Loading team from storage");
                    var t = UserStorage.LoadTeam();
                    if (t == null) return;
                    if (t.MutedMembers == null) t.MutedMembers = new System.Collections.Generic.List<string>();
                    if (t.MutedMembers.Contains(targetUser))
                        t.MutedMembers.Remove(targetUser);
                    else
                        t.MutedMembers.Add(targetUser);
//                     DebugLogger.Log("[Form1] Saving team data to storage");
            UserStorage.SaveTeam(t);
//                     DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show(t.IsMuted(targetUser) ? $"{targetUser} has been muted." : $"{targetUser} has been unmuted.",
                        "Mute Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };
                ctrl.OnSecurityCheckUser += async (targetUser) =>
                {
                    await ShowUserSecurityCheckDialogAsync(targetUser);
                };
                ctrl.OnKickUser += (targetUser) =>
                {
                    KickAndBanUser(targetUser);
                };
                ctrl.OnMakeAssistantAdmin += (targetUser) =>
                {
//                     DebugLogger.Log("[Form1] Loading team from storage");
                    var t = UserStorage.LoadTeam();
                    if (t == null) return;
                    if (t.AssistantAdmins == null) t.AssistantAdmins = new System.Collections.Generic.List<string>();
                    if (t.AssistantAdmins.Contains(targetUser))
                        t.AssistantAdmins.Remove(targetUser);
                    else
                        t.AssistantAdmins.Add(targetUser);
//                     DebugLogger.Log("[Form1] Saving team data to storage");
            UserStorage.SaveTeam(t);
//                     DebugLogger.Log("[Form1] Saving team data to Firebase");
            _ = UserStorage.SaveTeamToFirebaseAsync(t);
//                     DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show(t.HasAdminPrivileges(targetUser)
                        ? $"{targetUser} is now an Assistant Admin."
                        : $"{targetUser} has been demoted from Assistant Admin.",
                        "Admin Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                panelOnlineUsers.Controls.Add(ctrl);
                onlineUserControls.Add(ctrl);
                yPos += ctrl.Height + 6;
            }

            BuildAskAiPanel();

            // -- Version labels: docked to BOTTOM of team panel --
            // Remove labels from main form and add to a bottom-docked sub-panel
            this.Controls.Remove(labelVersion);
            this.Controls.Remove(labelMessage);

            var versionPanel = new Panel
            {
                Name = "versionPanel",
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.Transparent,
                Padding = new Padding(10, 4, 10, 4)
            };

            // Separator line at top of version panel
            var versionSeparator = new Label
            {
                AutoSize = false,
                Height = 1,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(180, 180, 200)
            };
            versionPanel.Controls.Add(versionSeparator);

            // Style labelVersion: smaller, clean, at bottom
            labelVersion.Font = new System.Drawing.Font("Segoe UI", 9, FontStyle.Bold);
            labelVersion.AutoSize = true;
            labelVersion.Location = new Point(10, 8);
            labelVersion.Cursor = Cursors.Default;
            versionPanel.Controls.Add(labelVersion);

            // Style labelMessage: update notification, clickable
            labelMessage.Font = new System.Drawing.Font("Segoe UI", 8.5f, FontStyle.Bold | FontStyle.Underline);
            labelMessage.AutoSize = true;
            labelMessage.Location = new Point(10, 28);
            labelMessage.Cursor = Cursors.Hand;
            labelMessage.Visible = false;
            versionPanel.Controls.Add(labelMessage);

            panelOnlineUsers.Controls.Add(versionPanel);

            this.Controls.Add(panelOnlineUsers);
        }

        private void BuildAskAiPanel()
        {
            if (_orgSettings == null)
                _orgSettings = OrganizerStorage.LoadSettings();
            _askAiVisible = _orgSettings?.ShowAiWidget ?? false;

            if (_askAiPanel != null && !_askAiPanel.IsDisposed)
            {
                if (_askAiPanel.Parent != null)
                    _askAiPanel.Parent.Controls.Remove(_askAiPanel);
                _askAiPanel.Dispose();
            }

            _askAiPanel = new Panel
            {
                Name = "askAiPanel",
                Dock = DockStyle.Fill,
                Height = 78,
                BackColor = isDarkMode ? Color.FromArgb(20, 24, 34) : Color.White,
                Padding = new Padding(10, 8, 10, 8)
            };

            var sep = new Label
            {
                Dock = DockStyle.Top,
                Height = 1,
                AutoSize = false,
                BackColor = isDarkMode ? Color.FromArgb(70, 76, 90) : Color.FromArgb(210, 214, 224)
            };
            _askAiPanel.Controls.Add(sep);

            var title = new Label
            {
                Text = "Ask AI",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = isDarkMode ? Color.FromArgb(251, 146, 60) : Color.FromArgb(194, 65, 12),
                AutoSize = true,
                Location = new Point(10, 9)
            };
            _askAiPanel.Controls.Add(title);

            _askAiStatus = new Label
            {
                Text = "Not configured",
                Font = new Font("Segoe UI", 8f, FontStyle.Regular),
                ForeColor = isDarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(71, 85, 105),
                AutoSize = false,
                Location = new Point(10, 29),
                Size = new Size(300, 16)
            };
            _askAiPanel.Controls.Add(_askAiStatus);

            _btnAskAi = new Button
            {
                Text = "Ask AI",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(31, 132, 255),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(10, 52),
                Size = new Size(132, 24),
                Cursor = Cursors.Hand
            };
            _btnAskAi.FlatAppearance.BorderSize = 0;
            _btnAskAi.Click += async (s, e) => await OpenAskAiDialogAsync();
            _askAiPanel.Controls.Add(_btnAskAi);
            RefreshAskAiPanelStatus();
            RebuildPersonalBoardWindowContent();
        }

        private string GetCurrentAiSettingsKey(TeamInfo team)
        {
            string joinCode = team?.JoinCode;
            if (string.IsNullOrWhiteSpace(joinCode))
                joinCode = UserStorage.GetActiveTeamCode();
            if (string.IsNullOrWhiteSpace(joinCode))
                joinCode = "DEFAULT";
            string userName = _currentUser?.Name ?? UserStorage.GetLastUser() ?? "unknown";
            return $"{joinCode.ToUpperInvariant()}|{userName}";
        }

        private PersonalAiSettings LoadPersonalAiSettings(TeamInfo team = null)
        {
            try
            {
                var all = TeamLocalCacheStore.LoadDictionary<PersonalAiSettings>(PersonalAiSettingsCacheFile);
                string key = GetCurrentAiSettingsKey(team ?? UserStorage.LoadTeam());
                if (all.TryGetValue(key, out var settings) && settings != null)
                    return settings;
            }
            catch
            {
            }

            return new PersonalAiSettings();
        }

        private void SavePersonalAiSettings(PersonalAiSettings settings, TeamInfo team = null)
        {
            try
            {
                var all = TeamLocalCacheStore.LoadDictionary<PersonalAiSettings>(PersonalAiSettingsCacheFile);
                string key = GetCurrentAiSettingsKey(team ?? UserStorage.LoadTeam());
                all[key] = settings ?? new PersonalAiSettings();
                TeamLocalCacheStore.SaveDictionary(PersonalAiSettingsCacheFile, all);
            }
            catch
            {
            }
        }

        private bool TryResolveAiConfig(out string endpoint, out string model, out string apiKey, out string source)
        {
            endpoint = "";
            model = "";
            apiKey = "";
            source = "Not configured";

            if (!_askAiVisible)
            {
                source = "Ask AI is OFF in Settings";
                return false;
            }

            var team = UserStorage.LoadTeam();
            var personal = LoadPersonalAiSettings(team);
            if (personal != null && personal.Enabled && !string.IsNullOrWhiteSpace(personal.ApiKey))
            {
                endpoint = string.IsNullOrWhiteSpace(personal.Endpoint) ? DefaultAiEndpoint : personal.Endpoint.Trim();
                model = string.IsNullOrWhiteSpace(personal.Model) ? DefaultAiModel : personal.Model.Trim();
                apiKey = personal.ApiKey.Trim();
                source = $"My API override ({model})";
                return true;
            }

            string teamEndpoint = string.IsNullOrWhiteSpace(team?.TeamAiEndpoint) ? DefaultAiEndpoint : team.TeamAiEndpoint.Trim();
            string teamModel = string.IsNullOrWhiteSpace(team?.TeamAiModel) ? DefaultAiModel : team.TeamAiModel.Trim();
            string teamKey = team?.TeamAiApiKey?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(teamKey))
            {
                endpoint = teamEndpoint;
                model = teamModel;
                apiKey = teamKey;
                source = $"Team AI ({model})";
                return true;
            }

            source = "No API key configured";
            return false;
        }

        private void RefreshAskAiPanelStatus()
        {
            if (_askAiStatus == null || _askAiStatus.IsDisposed)
                return;

            if (_askAiPanel != null && !_askAiPanel.IsDisposed && _askAiPanel.Parent != null)
                _askAiPanel.Visible = _askAiVisible && _personalBoardVisible;

            if (!_askAiVisible)
            {
                _askAiStatus.Text = "Disabled in Settings";
                _askAiStatus.ForeColor = isDarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
                if (_btnAskAi != null) _btnAskAi.Enabled = false;
                if (_btnAiChatSend != null) _btnAiChatSend.Enabled = false;
                return;
            }

            if (TryResolveAiConfig(out _, out _, out _, out var source))
            {
                _askAiStatus.Text = source;
                _askAiStatus.ForeColor = isDarkMode ? Color.FromArgb(34, 197, 94) : Color.FromArgb(22, 163, 74);
                if (_btnAskAi != null) _btnAskAi.Enabled = true;
                if (_btnAiChatSend != null) _btnAiChatSend.Enabled = true;
            }
            else
            {
                _askAiStatus.Text = source + " - configure in Team Settings";
                _askAiStatus.ForeColor = isDarkMode ? Color.FromArgb(248, 113, 113) : Color.FromArgb(220, 38, 38);
                if (_btnAskAi != null) _btnAskAi.Enabled = false;
                if (_btnAiChatSend != null) _btnAiChatSend.Enabled = false;
            }

            RebuildPersonalBoardWindowContent();
        }

        private async Task ShowAskAiSetupDialogAsync()
        {
            var team = UserStorage.LoadTeam();
            bool canEditTeam = _currentUser != null && (team?.HasAdminPrivileges(_currentUser.Name) == true || _currentUser.IsAdmin);
            var personal = LoadPersonalAiSettings(team);
            bool personalBoardEnabled = OrganizerStorage.LoadSettings().ShowPersonalBoard;

            using (var dlg = new Form())
            {
                dlg.Text = "Ask AI Setup";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.Size = new Size(640, canEditTeam ? 430 : 290);
                dlg.BackColor = isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250);
                dlg.ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59);

                int y = 14;
                if (canEditTeam)
                {
                    var lblTeam = new Label
                    {
                        Text = "Team AI (admin shared settings)",
                        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                        Location = new Point(16, y),
                        AutoSize = true
                    };
                    dlg.Controls.Add(lblTeam);
                    y += 26;

                    dlg.Controls.Add(new Label { Text = "Endpoint", Location = new Point(16, y), AutoSize = true });
                    var txtTeamEndpoint = new TextBox
                    {
                        Location = new Point(16, y + 16),
                        Size = new Size(592, 24),
                        Text = string.IsNullOrWhiteSpace(team?.TeamAiEndpoint) ? DefaultAiEndpoint : team.TeamAiEndpoint
                    };
                    dlg.Controls.Add(txtTeamEndpoint);
                    y += 48;

                    dlg.Controls.Add(new Label { Text = "Model", Location = new Point(16, y), AutoSize = true });
                    var txtTeamModel = new TextBox
                    {
                        Location = new Point(16, y + 16),
                        Size = new Size(288, 24),
                        Text = string.IsNullOrWhiteSpace(team?.TeamAiModel) ? DefaultAiModel : team.TeamAiModel
                    };
                    dlg.Controls.Add(txtTeamModel);

                    dlg.Controls.Add(new Label { Text = "API key", Location = new Point(320, y), AutoSize = true });
                    var txtTeamApiKey = new TextBox
                    {
                        Location = new Point(320, y + 16),
                        Size = new Size(288, 24),
                        Text = team?.TeamAiApiKey ?? "",
                        UseSystemPasswordChar = true
                    };
                    dlg.Controls.Add(txtTeamApiKey);
                    y += 52;

                    var sep = new Label
                    {
                        Location = new Point(16, y),
                        Size = new Size(592, 1),
                        BackColor = isDarkMode ? Color.FromArgb(70, 76, 90) : Color.FromArgb(210, 214, 224)
                    };
                    dlg.Controls.Add(sep);
                    y += 12;

                    var lblPersonal = new Label
                    {
                        Text = "My personal override (optional)",
                        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                        Location = new Point(16, y),
                        AutoSize = true
                    };
                    dlg.Controls.Add(lblPersonal);
                    y += 28;

                    BuildPersonalAiSetupControls(dlg, y, personal, personalBoardEnabled, out var chkEnabled, out var txtEndpoint, out var txtModel, out var txtApiKey);

                    var btnSave = new Button
                    {
                        Text = "Save",
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Color.FromArgb(31, 132, 255),
                        ForeColor = Color.White,
                        Location = new Point(428, dlg.ClientSize.Height - 42),
                        Size = new Size(86, 28)
                    };
                    btnSave.FlatAppearance.BorderSize = 0;
                    btnSave.Click += async (s, e) =>
                    {
                        team.TeamAiEndpoint = string.IsNullOrWhiteSpace(txtTeamEndpoint.Text) ? DefaultAiEndpoint : txtTeamEndpoint.Text.Trim();
                        team.TeamAiModel = string.IsNullOrWhiteSpace(txtTeamModel.Text) ? DefaultAiModel : txtTeamModel.Text.Trim();
                        team.TeamAiApiKey = txtTeamApiKey.Text.Trim();
                        UserStorage.SaveTeam(team);
                        await UserStorage.SaveTeamToFirebaseAsync(team);

                        SavePersonalAiSettings(new PersonalAiSettings
                        {
                            Enabled = chkEnabled.Checked,
                            Endpoint = txtEndpoint.Text.Trim(),
                            Model = txtModel.Text.Trim(),
                            ApiKey = txtApiKey.Text.Trim()
                        }, team);

                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    };
                    dlg.Controls.Add(btnSave);

                    var btnCancel = new Button
                    {
                        Text = "Cancel",
                        FlatStyle = FlatStyle.Flat,
                        BackColor = isDarkMode ? Color.FromArgb(45, 55, 72) : Color.FromArgb(226, 232, 240),
                        ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59),
                        Location = new Point(522, dlg.ClientSize.Height - 42),
                        Size = new Size(86, 28)
                    };
                    btnCancel.FlatAppearance.BorderSize = 0;
                    btnCancel.Click += (s, e) => dlg.Close();
                    dlg.Controls.Add(btnCancel);
                }
                else
                {
                    var lblPersonal = new Label
                    {
                        Text = "My personal AI setup",
                        Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                        Location = new Point(16, y),
                        AutoSize = true
                    };
                    dlg.Controls.Add(lblPersonal);
                    y += 28;
                    BuildPersonalAiSetupControls(dlg, y, personal, personalBoardEnabled, out var chkEnabled, out var txtEndpoint, out var txtModel, out var txtApiKey);

                    var btnSave = new Button
                    {
                        Text = "Save",
                        FlatStyle = FlatStyle.Flat,
                        BackColor = Color.FromArgb(31, 132, 255),
                        ForeColor = Color.White,
                        Location = new Point(428, dlg.ClientSize.Height - 42),
                        Size = new Size(86, 28)
                    };
                    btnSave.FlatAppearance.BorderSize = 0;
                    btnSave.Click += (s, e) =>
                    {
                        SavePersonalAiSettings(new PersonalAiSettings
                        {
                            Enabled = chkEnabled.Checked,
                            Endpoint = txtEndpoint.Text.Trim(),
                            Model = txtModel.Text.Trim(),
                            ApiKey = txtApiKey.Text.Trim()
                        }, team);
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    };
                    dlg.Controls.Add(btnSave);

                    var btnCancel = new Button
                    {
                        Text = "Cancel",
                        FlatStyle = FlatStyle.Flat,
                        BackColor = isDarkMode ? Color.FromArgb(45, 55, 72) : Color.FromArgb(226, 232, 240),
                        ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59),
                        Location = new Point(522, dlg.ClientSize.Height - 42),
                        Size = new Size(86, 28)
                    };
                    btnCancel.FlatAppearance.BorderSize = 0;
                    btnCancel.Click += (s, e) => dlg.Close();
                    dlg.Controls.Add(btnCancel);
                }

                dlg.ShowDialog(this);
            }

            RefreshAskAiPanelStatus();
            await Task.CompletedTask;
        }

        private static void BuildPersonalAiSetupControls(Form host, int y, PersonalAiSettings personal, bool allowPersonalOverride,
            out CheckBox chkEnabled, out TextBox txtEndpoint, out TextBox txtModel, out TextBox txtApiKey)
        {
            chkEnabled = new CheckBox
            {
                Text = "Use my own API instead of team API",
                Checked = allowPersonalOverride && personal != null && personal.Enabled,
                Location = new Point(16, y),
                Size = new Size(350, 24)
            };
            chkEnabled.Enabled = allowPersonalOverride;
            host.Controls.Add(chkEnabled);
            y += 28;

            if (!allowPersonalOverride)
            {
                host.Controls.Add(new Label
                {
                    Text = "Personal AI override is disabled because Personal Board is OFF.",
                    Location = new Point(36, y - 4),
                    AutoSize = true,
                    ForeColor = Color.FromArgb(148, 163, 184)
                });
                y += 14;
            }

            host.Controls.Add(new Label { Text = "Endpoint", Location = new Point(16, y), AutoSize = true });
            txtEndpoint = new TextBox
            {
                Location = new Point(16, y + 16),
                Size = new Size(592, 24),
                Text = string.IsNullOrWhiteSpace(personal?.Endpoint) ? DefaultAiEndpoint : personal.Endpoint
            };
            txtEndpoint.Enabled = allowPersonalOverride;
            host.Controls.Add(txtEndpoint);
            y += 48;

            host.Controls.Add(new Label { Text = "Model", Location = new Point(16, y), AutoSize = true });
            txtModel = new TextBox
            {
                Location = new Point(16, y + 16),
                Size = new Size(288, 24),
                Text = string.IsNullOrWhiteSpace(personal?.Model) ? DefaultAiModel : personal.Model
            };
            txtModel.Enabled = allowPersonalOverride;
            host.Controls.Add(txtModel);

            host.Controls.Add(new Label { Text = "API key", Location = new Point(320, y), AutoSize = true });
            txtApiKey = new TextBox
            {
                Location = new Point(320, y + 16),
                Size = new Size(288, 24),
                Text = personal?.ApiKey ?? "",
                UseSystemPasswordChar = true
            };
            txtApiKey.Enabled = allowPersonalOverride;
            host.Controls.Add(txtApiKey);
        }

        private async Task OpenAskAiDialogAsync()
        {
            if (!_askAiVisible)
            {
                MessageBox.Show("Ask AI is OFF. Enable it in Team Settings > Panel Visibility > Ask AI.", "Ask AI",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!TryResolveAiConfig(out var endpoint, out var model, out var apiKey, out _))
            {
                MessageBox.Show("Ask AI is not configured yet. Click Setup and add API key.", "Ask AI",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var dlg = new Form())
            {
                dlg.Text = "Ask AI";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.Size = new Size(760, 560);
                dlg.MinimumSize = new Size(680, 480);
                dlg.BackColor = isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250);
                dlg.ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59);

                var lblInfo = new Label
                {
                    Text = $"Model: {model}",
                    Location = new Point(16, 12),
                    AutoSize = true,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                };
                dlg.Controls.Add(lblInfo);

                var txtPrompt = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Location = new Point(16, 34),
                    Size = new Size(dlg.ClientSize.Width - 32, 118),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new Font("Segoe UI", 9f),
                    BackColor = isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White,
                    ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59)
                };
                dlg.Controls.Add(txtPrompt);

                var btnAsk = new Button
                {
                    Text = "Ask",
                    Location = new Point(16, 160),
                    Size = new Size(90, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(31, 132, 255),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                };
                btnAsk.FlatAppearance.BorderSize = 0;
                dlg.Controls.Add(btnAsk);

                var lblBusy = new Label
                {
                    Text = "",
                    Location = new Point(116, 166),
                    Size = new Size(450, 20),
                    ForeColor = isDarkMode ? Color.FromArgb(148, 163, 184) : Color.FromArgb(71, 85, 105)
                };
                dlg.Controls.Add(lblBusy);

                var txtAnswer = new RichTextBox
                {
                    ReadOnly = true,
                    BorderStyle = BorderStyle.FixedSingle,
                    Location = new Point(16, 198),
                    Size = new Size(dlg.ClientSize.Width - 32, dlg.ClientSize.Height - 214),
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new Font("Segoe UI", 9.5f),
                    BackColor = isDarkMode ? Color.FromArgb(20, 24, 34) : Color.White,
                    ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59)
                };
                dlg.Controls.Add(txtAnswer);

                Func<Task> askNow = async () =>
                {
                    string prompt = txtPrompt.Text.Trim();
                    if (string.IsNullOrWhiteSpace(prompt))
                        return;

                    btnAsk.Enabled = false;
                    lblBusy.Text = "Asking AI...";
                    txtAnswer.Text = "";
                    try
                    {
                        string context = await BuildAiProjectContextAsync();
                        string finalPrompt = prompt + "\n\n--- PROJECT CONTEXT (AUTO) ---\n" + context;
                        string answer = await RequestAiCompletionAsync(endpoint, model, apiKey, finalPrompt);
                        txtAnswer.Text = answer;
                    }
                    catch (Exception ex)
                    {
                        txtAnswer.Text = "Failed to get response:\n" + ex.Message;
                    }
                    finally
                    {
                        lblBusy.Text = "";
                        btnAsk.Enabled = true;
                        txtPrompt.Clear();
                        txtPrompt.Focus();
                    }
                };

                btnAsk.Click += async (s, e) => await askNow();
                txtPrompt.KeyDown += async (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter && !e.Shift)
                    {
                        e.SuppressKeyPress = true;
                        await askNow();
                    }
                };

                dlg.ShowDialog(this);
            }

            await Task.CompletedTask;
        }

        private async Task<string> BuildAiProjectContextAsync()
        {
            var sb = new StringBuilder(12000);
            try
            {
                string projectName = _cmbProject?.SelectedItem?.ToString();
                if (string.IsNullOrWhiteSpace(projectName))
                    projectName = _cmbProject?.Text ?? "General";
                sb.AppendLine("Current project: " + projectName);
            }
            catch
            {
                sb.AppendLine("Current project: General");
            }

            try
            {
                sb.AppendLine();
                sb.AppendLine("Team board stickers:");
                foreach (var s in (_stickerBoard?.GetStickerSnapshot(120, includeDone: true) ?? new List<StickerEntry>()))
                {
                    sb.AppendLine($"- [{(s.done ? "Done" : "Open")}] {s.title} | {s.type}/{s.priority} | by {s.createdBy} | to {s.assignedTo}");
                }
            }
            catch { }

            try
            {
                sb.AppendLine();
                sb.AppendLine("My personal board stickers:");
                foreach (var s in (_personalStickerBoard?.GetStickerSnapshot(120, includeDone: true) ?? new List<StickerEntry>()))
                {
                    sb.AppendLine($"- [{(s.done ? "Done" : "Open")}] {s.title} | {s.type}/{s.priority}");
                }
            }
            catch { }

            try
            {
                sb.AppendLine();
                sb.AppendLine("Project folder files:");
                string folderPath = _projectFolderPanel?.FolderPath;
                if (string.IsNullOrWhiteSpace(folderPath))
                    folderPath = ProjectFolderSettings.Load()?.FolderPath;

                if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                {
                    var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories)
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
                    int added = 0;
                    foreach (var file in files)
                    {
                        string rel = file;
                        try
                        {
                            rel = file.StartsWith(folderPath, StringComparison.OrdinalIgnoreCase)
                                ? file.Substring(folderPath.Length).TrimStart('\\')
                                : file;
                        }
                        catch { }
                        sb.AppendLine("- " + rel);
                        added++;
                        if (added >= 5000 || sb.Length > 26000)
                        {
                            sb.AppendLine("... file list truncated ...");
                            break;
                        }
                    }
                }
                else
                {
                    sb.AppendLine("- No project folder configured.");
                }
            }
            catch { }

            await Task.CompletedTask;
            return sb.ToString();
        }

        private async Task<string> RequestAiCompletionAsync(string endpoint, string model, string apiKey, string prompt)
        {
            if (!_askAiVisible)
                throw new InvalidOperationException("Ask AI is OFF in Settings.");

            if (string.IsNullOrWhiteSpace(endpoint))
                endpoint = DefaultAiEndpoint;
            if (string.IsNullOrWhiteSpace(model))
                model = DefaultAiModel;

            var payload = new JObject
            {
                ["model"] = model,
                ["temperature"] = 0.6,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = prompt
                    }
                }
            };

            using (var req = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                req.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                string key = (apiKey ?? "").Trim();
                if (!key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    key = "Bearer " + key;
                req.Headers.TryAddWithoutValidation("Authorization", key);

                if (endpoint.IndexOf("openrouter.ai", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    req.Headers.TryAddWithoutValidation("HTTP-Referer", "https://8bitlab.de");
                    req.Headers.TryAddWithoutValidation("X-Title", "Work Time Counter");
                }

                var res = await _httpClient.SendAsync(req);
                string json = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                    throw new Exception($"HTTP {(int)res.StatusCode}: {json}");

                var root = JObject.Parse(json);
                var contentToken = root.SelectToken("choices[0].message.content");
                if (contentToken == null)
                    return "No response content returned.";

                if (contentToken.Type == JTokenType.Array)
                {
                    var sb = new StringBuilder();
                    foreach (var part in contentToken)
                    {
                        string text = part?["text"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(text))
                            sb.Append(text);
                    }
                    string combined = sb.ToString().Trim();
                    return string.IsNullOrWhiteSpace(combined) ? "No response content returned." : combined;
                }

                return contentToken.ToString().Trim();
            }
        }

        // -----------------------------------------------------------------------
        //  CALENDAR / ORGANIZER ? BUILD, ALARM TIMER, HELPERS
        // -----------------------------------------------------------------------

        /// <summary>
        /// Build the calendar/date organizer in the right column.
        /// Calendar sits below Team Status and to the right of File Share.
        /// </summary>
        private void BuildCalendarPanel()
        {
            _orgSettings = OrganizerStorage.LoadSettings();
            _calendarVisible = _orgSettings.ShowCalendar;
            int calendarHeight = _orgSettings.CompactView ? 170 : 210;

            _calendarPanel = new CalendarPanel
            {
                Dock = DockStyle.None,
                Size = new Size(RIGHT_COLUMN_WIDTH, calendarHeight),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Visible = _orgSettings.ShowCalendar
            };
            int chatTopY = this.ClientSize.Height - _bottomPanelHeight - statusStrip1.Height;
            _calendarPanel.Location = new Point(this.ClientSize.Width - RIGHT_COLUMN_WIDTH, chatTopY);

            _calendarPanel.ApplyTheme(isDarkMode, _customTheme);
            _calendarPanel.SetFirstDayOfWeek(_orgSettings.FirstDayOfWeek);
            _calendarPanel.SetShowWeekNumbers(_orgSettings.ShowWeekNumbers);
            _calendarPanel.RefreshEntryMarkers();

            // Wire events: click ? open day popup, double-click ? quick-add
            _calendarPanel.DateClicked += CalendarPanel_DateClicked;
            _calendarPanel.DateDoubleClicked += CalendarPanel_DateDoubleClicked;

            this.Controls.Add(_calendarPanel);
            _calendarPanel.BringToFront();
            ApplyMainColumnLayout();

            // Show startup reminders if enabled
            if (_orgSettings.PopupOnStartup)
            {
                ShowStartupReminders();
            }
        }

        private void BuildWeatherPanel()
        {
            if (_orgSettings == null)
                _orgSettings = OrganizerStorage.LoadSettings();

            _weatherVisible = _orgSettings.ShowWeatherWidget;
            _weatherMode = NormalizeWeatherMode(_orgSettings.WeatherWidgetMode);

            _weatherPanel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = isDarkMode ? ThemeConstants.Dark.BgElevated : ThemeConstants.Light.BgSurface,
                Visible = _weatherVisible
            };

            _weatherTitle = new Label
            {
                AutoSize = false,
                Height = 22,
                Dock = DockStyle.Top,
                Padding = new Padding(10, 2, 10, 0),
                Font = new Font(ThemeConstants.FontFamily, 9f, FontStyle.Bold),
                ForeColor = isDarkMode ? ThemeConstants.Dark.AccentPrimary : ThemeConstants.Light.AccentPrimary,
                Text = "Weather"
            };

            _weatherSummary = new Label
            {
                AutoSize = false,
                Height = 26,
                Dock = DockStyle.Top,
                Padding = new Padding(10, 0, 10, 0),
                Font = new Font(ThemeConstants.FontFamily, 10.5f, FontStyle.Bold),
                ForeColor = isDarkMode ? ThemeConstants.Dark.TextPrimary : ThemeConstants.Light.TextPrimary,
                AutoEllipsis = true,
                Text = "Loading weather..."
            };

            _weatherDetails = new Label
            {
                AutoSize = false,
                Height = 22,
                Dock = DockStyle.Top,
                Padding = new Padding(10, 0, 10, 2),
                Font = new Font(ThemeConstants.FontFamily, 8.5f, FontStyle.Regular),
                ForeColor = isDarkMode ? ThemeConstants.Dark.TextSecondary : ThemeConstants.Light.TextSecondary,
                AutoEllipsis = true,
                Text = "Click to switch: Daily / Weekly"
            };

            _weatherPanel.Controls.Add(_weatherDetails);
            _weatherPanel.Controls.Add(_weatherSummary);
            _weatherPanel.Controls.Add(_weatherTitle);
            this.Controls.Add(_weatherPanel);

            EventHandler cycleHandler = (s, e) => CycleWeatherMode();
            _weatherPanel.Click += cycleHandler;
            _weatherTitle.Click += cycleHandler;
            _weatherSummary.Click += cycleHandler;
            _weatherDetails.Click += cycleHandler;

            _weatherTimer = new Timer { Interval = 15 * 60 * 1000 };
            _weatherTimer.Tick += async (s, e) => await RefreshWeatherAsync();
            _weatherTimer.Start();

            ApplyMainColumnLayout();
        }

        private string NormalizeWeatherMode(string mode)
        {
            string m = (mode ?? "daily").Trim().ToLowerInvariant();
            if (m == "weekly")
                return m;
            return "daily";
        }

        private async Task<bool> EnsureWeatherLocationAsync()
        {
            if (_weatherLocationResolved)
                return true;

            try
            {
                var resp = await _httpClient.GetAsync("https://ipapi.co/json/");
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    var o = JsonConvert.DeserializeObject<JObject>(json);
                    if (o != null &&
                        double.TryParse(o["latitude"]?.ToString(), out _weatherLat) &&
                        double.TryParse(o["longitude"]?.ToString(), out _weatherLon))
                    {
                        string city = o["city"]?.ToString() ?? "";
                        string country = o["country_name"]?.ToString() ?? "";
                        _weatherCity = string.IsNullOrWhiteSpace(city) ? country : (city + (string.IsNullOrWhiteSpace(country) ? "" : ", " + country));
                        _weatherLocationResolved = true;
                        return true;
                    }
                }
            }
            catch { }

            try
            {
                var resp2 = await _httpClient.GetAsync("https://ipwho.is/");
                if (resp2.IsSuccessStatusCode)
                {
                    string json = await resp2.Content.ReadAsStringAsync();
                    var o = JsonConvert.DeserializeObject<JObject>(json);
                    if (o != null &&
                        bool.TryParse(o["success"]?.ToString(), out bool ok) && ok &&
                        double.TryParse(o["latitude"]?.ToString(), out _weatherLat) &&
                        double.TryParse(o["longitude"]?.ToString(), out _weatherLon))
                    {
                        string city = o["city"]?.ToString() ?? "";
                        string country = o["country"]?.ToString() ?? "";
                        _weatherCity = string.IsNullOrWhiteSpace(city) ? country : (city + (string.IsNullOrWhiteSpace(country) ? "" : ", " + country));
                        _weatherLocationResolved = true;
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private string WeatherCodeToIcon(int code)
        {
            if (code == 0) return "Clear";
            if (code == 1 || code == 2) return "Partly Cloudy";
            if (code == 3) return "Cloudy";
            if (code == 45 || code == 48) return "Fog";
            if (code >= 51 && code <= 67) return "Rain";
            if (code >= 71 && code <= 77) return "Snow";
            if (code >= 80 && code <= 82) return "Showers";
            if (code >= 95) return "Storm";
            return "Weather";
        }

        private string WeatherCodeToSymbol(int code)
        {
            if (code == 0) return "☀";
            if (code == 1 || code == 2) return "⛅";
            if (code == 3) return "☁";
            if (code == 45 || code == 48) return "🌫";
            if (code >= 51 && code <= 67) return "🌧";
            if (code >= 71 && code <= 77) return "❄";
            if (code >= 80 && code <= 82) return "🌦";
            if (code >= 95) return "⛈";
            return "•";
        }

        private async Task RefreshWeatherAsync()
        {
            if (!_weatherVisible || _weatherPanel == null || _weatherPanel.IsDisposed)
                return;

            try
            {
                if (!await EnsureWeatherLocationAsync())
                {
                    _weatherTitle.Text = "Weather • " + char.ToUpper(_weatherMode[0]) + _weatherMode.Substring(1);
                    _weatherSummary.Text = "Location unavailable";
                    _weatherDetails.Text = "Turn on internet to load weather";
                    return;
                }

                int days = _weatherMode == "weekly" ? 7 : 1;
                string url = $"https://api.open-meteo.com/v1/forecast?latitude={_weatherLat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={_weatherLon.ToString(System.Globalization.CultureInfo.InvariantCulture)}&current=temperature_2m,weather_code&daily=temperature_2m_max,temperature_2m_min,precipitation_probability_max&forecast_days={days}&timezone=auto";
                var resp = await _httpClient.GetAsync(url);
                if (!resp.IsSuccessStatusCode)
                {
                    _weatherSummary.Text = "Weather unavailable";
                    _weatherDetails.Text = "Service error: " + (int)resp.StatusCode;
                    return;
                }

                string json = await resp.Content.ReadAsStringAsync();
                var o = JsonConvert.DeserializeObject<JObject>(json);
                if (o == null)
                    return;

                double currentTemp = 0;
                int currentCode = 0;
                _ = double.TryParse(o["current"]?["temperature_2m"]?.ToString(), out currentTemp);
                _ = int.TryParse(o["current"]?["weather_code"]?.ToString(), out currentCode);

                var maxArr = o["daily"]?["temperature_2m_max"] as JArray;
                var minArr = o["daily"]?["temperature_2m_min"] as JArray;
                var rainArr = o["daily"]?["precipitation_probability_max"] as JArray;
                var codeArr = o["daily"]?["weather_code"] as JArray;
                var timeArr = o["daily"]?["time"] as JArray;

                _weatherTitle.Text = "Weather • " + char.ToUpper(_weatherMode[0]) + _weatherMode.Substring(1);
                _weatherSummary.Text = $"{_weatherCity}  {Math.Round(currentTemp)}°C  {WeatherCodeToIcon(currentCode)}";

                if (_weatherMode == "daily")
                {
                    double hi = maxArr != null && maxArr.Count > 0 ? (double)maxArr[0] : currentTemp;
                    double lo = minArr != null && minArr.Count > 0 ? (double)minArr[0] : currentTemp;
                    double rain = rainArr != null && rainArr.Count > 0 ? (double)rainArr[0] : 0;
                    _weatherDetails.Text = $"Today: {Math.Round(lo)}°/{Math.Round(hi)}°  Rain {Math.Round(rain)}%  (click for weekly)";
                }
                else
                {
                    int count = Math.Min(7, Math.Min(timeArr?.Count ?? 0, codeArr?.Count ?? 0));
                    var parts = new List<string>();
                    for (int i = 0; i < count; i++)
                    {
                        string dayLabel = $"D{i + 1}";
                        if (DateTime.TryParse(timeArr[i]?.ToString(), out DateTime d))
                            dayLabel = d.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture);
                        int dCode = 0;
                        _ = int.TryParse(codeArr[i]?.ToString(), out dCode);
                        parts.Add($"{dayLabel} {WeatherCodeToSymbol(dCode)}");
                    }
                    if (parts.Count == 0)
                    {
                        _weatherDetails.Text = "Week forecast unavailable";
                    }
                    else
                    {
                        string line = string.Join("  ", parts);
                        if (line.Length > 80)
                        {
                            line = string.Join("  ", parts.Take(5)) + " ...";
                        }
                        _weatherDetails.Text = line;
                    }
                }
            }
            catch
            {
                _weatherSummary.Text = "Weather unavailable";
                _weatherDetails.Text = "Network error";
            }
        }

        private void SaveWeatherPreferences()
        {
            try
            {
                var s = OrganizerStorage.LoadSettings();
                s.ShowWeatherWidget = _weatherVisible;
                s.WeatherWidgetMode = _weatherMode;
                OrganizerStorage.SaveSettings(s);
            }
            catch { }
        }

        private void CycleWeatherMode()
        {
            if (!_weatherVisible)
                return;

            if (_weatherMode == "daily")
                _weatherMode = "weekly";
            else
                _weatherMode = "daily";

            SaveWeatherPreferences();
            _ = RefreshWeatherAsync();
        }

        /// <summary>Handle single-click on a calendar date: open the DayOrganizerForm popup.</summary>
        private void CalendarPanel_DateClicked(DateTime date)
        {
            OpenDayOrganizer(date);
        }

        /// <summary>Handle double-click on a calendar date: open popup in quick-add mode.</summary>
        private void CalendarPanel_DateDoubleClicked(DateTime date)
        {
            OpenDayOrganizer(date);
        }

        /// <summary>Open the DayOrganizer as a floating owned window near the calendar.</summary>
        private void OpenDayOrganizer(DateTime date)
        {
            // If the same day is already open, just focus existing window to avoid close/reopen flicker.
            if (_dayOrganizerWindow != null && !_dayOrganizerWindow.IsDisposed &&
                _dayOrganizerWindow.SelectedDate.Date == date.Date)
            {
                if (_dayOrganizerWindow.WindowState == FormWindowState.Minimized)
                    _dayOrganizerWindow.WindowState = FormWindowState.Normal;
                _dayOrganizerWindow.BringToFront();
                _dayOrganizerWindow.Activate();
                return;
            }

            CloseDayOrganizer();

            _dayOrganizerWindow = new DayOrganizerForm(date, isDarkMode, _customTheme, _currentUser?.Name ?? "");
            _dayOrganizerWindow.StartPosition = FormStartPosition.Manual;
            _dayOrganizerWindow.Location = GetDayOrganizerWindowLocation(_dayOrganizerWindow.Size);
            _dayOrganizerWindow.FormClosed += (s, e) =>
            {
                if (ReferenceEquals(_dayOrganizerWindow, s))
                    _dayOrganizerWindow = null;

                _calendarPanel?.RefreshEntryMarkers();
            };

            _dayOrganizerWindow.Show(this);
            _dayOrganizerWindow.BringToFront();
            _dayOrganizerWindow.Activate();
        }

        private Point GetDayOrganizerWindowLocation(Size popupSize)
        {
            var workArea = Screen.FromControl(this).WorkingArea;
            var anchorScreen = _calendarPanel != null
                ? _calendarPanel.PointToScreen(Point.Empty)
                : this.PointToScreen(new Point(this.ClientSize.Width - popupSize.Width - 24, 72));

            int x = anchorScreen.X - popupSize.Width - 18;
            if (x < workArea.Left + 12)
                x = Math.Min(workArea.Right - popupSize.Width - 12, this.Left + Math.Max(24, (this.Width - popupSize.Width) / 2));

            int y = Math.Max(workArea.Top + 12, anchorScreen.Y - 24);
            if (y + popupSize.Height > workArea.Bottom - 12)
                y = Math.Max(workArea.Top + 12, workArea.Bottom - popupSize.Height - 12);

            return new Point(x, y);
        }

        /// <summary>Close the floating Day Organizer window.</summary>
        private void CloseDayOrganizer()
        {
            var form = _dayOrganizerWindow;
            _dayOrganizerWindow = null;

            if (form != null)
            {
                if (!form.IsDisposed)
                {
                    form.Close();
                    form.Dispose();
                }
            }

            _calendarPanel?.RefreshEntryMarkers();
        }

        /// <summary>Open the calendar/organizer settings form.</summary>
        private void OpenOrganizerSettings()
        {
            var form = new OrganizerSettingsForm(isDarkMode, _customTheme);
            form.SettingsSaved += (settings) =>
            {
                _orgSettings = settings;
                _calendarVisible = settings.ShowCalendar;
                if (_calendarPanel != null)
                {
                    _calendarPanel.Visible = settings.ShowCalendar;
                    int newH = settings.CompactView ? 170 : 210;
                    _calendarPanel.Height = newH;
                    _calendarPanel.SetFirstDayOfWeek(settings.FirstDayOfWeek);
                    _calendarPanel.SetShowWeekNumbers(settings.ShowWeekNumbers);
                    _calendarPanel.RefreshEntryMarkers();
                    ApplyMainColumnLayout();
                }
            };
            form.ProjectSettingsSaved += (projSettings) =>
            {
                if (_projectFolderPanel != null && !string.IsNullOrEmpty(projSettings.FolderPath))
                {
                    _projectFolderPanel.FolderPath = projSettings.FolderPath;
                }
            };
            form.SharedSettingsSaved += (sharedSettings) =>
            {
                // Shared folder settings will be picked up on next refresh
                // by the FileSharePanel's auto-refresh timer
            };
            form.ShowDialog(this);
        }

        /// <summary>
        /// Set up a timer that checks for pending alarms every 30 seconds.
        /// When an alarm fires, shows a notification popup + plays a sound.
        /// </summary>
        private void SetupAlarmCheckTimer()
        {
            _alarmCheckTimer = new Timer { Interval = 30000 }; // Check every 30 seconds
            _alarmCheckTimer.Tick += AlarmCheckTimer_Tick;
            _alarmCheckTimer.Start();
//             DebugLogger.Log("[Form1] Alarm check timer started (30s ? detects pending reminders)");

            // Also do an initial check after a short delay
            var initialCheck = new Timer { Interval = 5000 };
            initialCheck.Tick += (s, e) =>
            {
                initialCheck.Stop();
                initialCheck.Dispose();
                AlarmCheckTimer_Tick(null, EventArgs.Empty);
            };
            initialCheck.Start();
        }

        private void AlarmCheckTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var pendingAlarms = OrganizerStorage.GetPendingAlarms();
                foreach (var entry in pendingAlarms)
                {
                    // Play alarm sound if enabled
                    if (_orgSettings != null && _orgSettings.SoundAlarmEnabled)
                    {
                        PlayOrganizerAlarm();
                    }

                    // Show visual notification popup
                    ShowAlarmNotification(entry);

                    // Flash the taskbar
                    try
                    {
                        FLASHWINFO fInfo = new FLASHWINFO();
                        fInfo.cbSize = Convert.ToUInt32(System.Runtime.InteropServices.Marshal.SizeOf(fInfo));
                        fInfo.hwnd = this.Handle;
                        fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                        fInfo.uCount = 5;
                        fInfo.dwTimeout = 0;
                        FlashWindowEx(ref fInfo);
                    }
                    catch { }

                    // Handle recurrence: if recurring, create next occurrence
                    if (entry.Recurrence != RecurrenceType.None)
                    {
                        CreateNextRecurrence(entry);
                    }
                }
            }
            catch { }
        }

        private void EnsureHelperWindow()
        {
            if (_helperWindow != null && !_helperWindow.IsDisposed)
                return;

            _helperWindow = new Form
            {
                Text = "Helper Wiki",
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = false,
                BackColor = this.BackColor,
                Size = new Size(HelperWindowDefaultWidth, this.Height)
            };

            _helperWindow.FormClosing += (s, e) =>
            {
                // User closes wiki window directly -> keep state in sync and just hide.
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    _helperVisible = false;
                    _helperWindow.Hide();
                    UpdateToggleButtonStyle(btnToggleHelper, _helperVisible);
                }
            };
        }

        private void ShowHelperWindow()
        {
            EnsureHelperWindow();
            if (_helperPanel.Parent != _helperWindow)
            {
                try { this.Controls.Remove(_helperPanel); } catch { }
                _helperPanel.Dock = DockStyle.Fill;
                _helperWindow.Controls.Clear();
                _helperWindow.Controls.Add(_helperPanel);
                _helperPanel.Visible = true;
            }

            UpdateHelperWindowBounds();
            if (!_helperWindow.Visible)
                _helperWindow.Show(this);
            _helperWindow.BringToFront();
        }

        private void HideHelperWindow()
        {
            if (_helperWindow != null && !_helperWindow.IsDisposed)
                _helperWindow.Hide();
        }

        private void UpdateHelperWindowBounds()
        {
            if (!_helperVisible || _helperWindow == null || _helperWindow.IsDisposed)
                return;

            if (this.WindowState == FormWindowState.Minimized)
            {
                _helperWindow.Hide();
                return;
            }

            int helperWidth = Math.Max(280, _helperWindow.Width <= 0 ? HelperWindowDefaultWidth : _helperWindow.Width);
            int x = this.Left - helperWidth - 2;
            int y = this.Top;
            int h = Math.Max(500, this.Height);

            _helperWindow.SetBounds(x, y, helperWidth, h);
            if (!_helperWindow.Visible)
                _helperWindow.Show(this);
        }

        private static string SanitizeStorageToken(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "general";
            var chars = raw.Trim().ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();
            string token = new string(chars);
            while (token.Contains("__"))
                token = token.Replace("__", "_");
            token = token.Trim('_');
            return string.IsNullOrWhiteSpace(token) ? "general" : token;
        }

        private string BuildPersonalBoardCacheFileName()
        {
            string user = SanitizeStorageToken(_currentUser?.Name ?? "user");
            string project = SanitizeStorageToken(_cmbProject?.SelectedItem?.ToString() ?? "general");
            return $"personal_stickers_{user}_{project}.json";
        }

        private void SavePersonalBoardPreferences()
        {
            try
            {
                var s = OrganizerStorage.LoadSettings();
                s.ShowPersonalBoard = _personalBoardVisible;
                s.PersonalBoardSide = _personalBoardOnRight ? "right" : "left";
                OrganizerStorage.SaveSettings(s);
            }
            catch { }
        }

        private async Task RefreshPersonalBoardForCurrentProjectAsync()
        {
            if (_personalStickerBoard == null)
                return;

            _personalStickerBoard.SetLocalCacheFileName(BuildPersonalBoardCacheFileName());
            await _personalStickerBoard.RefreshAsync();
        }

        private void EnsurePersonalBoardWindow()
        {
            if (_personalBoardWindow != null && !_personalBoardWindow.IsDisposed)
                return;

            _personalBoardWindow = new Form
            {
                Text = "Personal Board",
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = false,
                BackColor = this.BackColor,
                Size = new Size(PersonalBoardWindowDefaultWidth, this.Height)
            };

            _personalBoardWindow.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    _personalBoardVisible = false;
                    _personalBoardWindow.Hide();
                    SavePersonalBoardPreferences();
                }
            };

            _personalBoardWindow.Move += (s, e) =>
            {
                if (_isUpdatingPersonalBoardBounds || _personalBoardWindow == null || _personalBoardWindow.IsDisposed)
                    return;
                int boardCenter = _personalBoardWindow.Left + (_personalBoardWindow.Width / 2);
                int mainCenter = this.Left + (this.Width / 2);
                bool newRight = boardCenter >= mainCenter;
                if (newRight != _personalBoardOnRight)
                {
                    _personalBoardOnRight = newRight;
                    SavePersonalBoardPreferences();
                }
            };
        }

        private void ShowPersonalBoardWindow()
        {
            EnsurePersonalBoardWindow();
            if (_personalStickerBoard == null)
                return;

            RebuildPersonalBoardWindowContent();

            UpdatePersonalBoardWindowBounds();
            if (!_personalBoardWindow.Visible)
                _personalBoardWindow.Show(this);
            _personalBoardWindow.BringToFront();
            _ = RefreshPersonalBoardForCurrentProjectAsync();
        }

        private void RebuildPersonalBoardWindowContent()
        {
            if (_personalBoardWindow == null || _personalBoardWindow.IsDisposed || _personalStickerBoard == null)
                return;

            if (_personalBoardHostLayout == null || _personalBoardHostLayout.IsDisposed || _personalBoardHostLayout.Parent != _personalBoardWindow)
            {
                _personalBoardHostLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    BackColor = _personalBoardWindow.BackColor,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                _personalBoardHostLayout.ColumnStyles.Clear();
                _personalBoardHostLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                _personalBoardHostLayout.RowStyles.Clear();
                _personalBoardHostLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));
                _personalBoardHostLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                _personalBoardWindow.Controls.Clear();
                _personalBoardWindow.Controls.Add(_personalBoardHostLayout);
            }

            if (_askAiPanel != null && !_askAiPanel.IsDisposed)
            {
                if (_askAiPanel.Parent != null)
                    _askAiPanel.Parent.Controls.Remove(_askAiPanel);
                _askAiPanel.Dock = DockStyle.Fill;
                _askAiPanel.Visible = _askAiVisible && _personalBoardVisible;
                _personalBoardHostLayout.Controls.Add(_askAiPanel, 0, 0);
                _personalBoardHostLayout.RowStyles[0].Height = _askAiVisible ? 78f : 0f;
            }
            else
            {
                _personalBoardHostLayout.RowStyles[0].Height = 0f;
            }

            if (_personalStickerBoard.Parent != null)
                _personalStickerBoard.Parent.Controls.Remove(_personalStickerBoard);
            _personalStickerBoard.Dock = DockStyle.Fill;
            _personalStickerBoard.Visible = true;
            _personalBoardHostLayout.Controls.Add(_personalStickerBoard, 0, 1);
        }

        private void HidePersonalBoardWindow()
        {
            if (_personalBoardWindow != null && !_personalBoardWindow.IsDisposed)
                _personalBoardWindow.Hide();
        }

        private void UpdatePersonalBoardWindowBounds()
        {
            if (!_personalBoardVisible || _personalBoardWindow == null || _personalBoardWindow.IsDisposed)
                return;
            if (this.WindowState == FormWindowState.Minimized)
            {
                _personalBoardWindow.Hide();
                return;
            }

            int boardWidth = Math.Max(STICKER_WIDTH, _personalBoardWindow.Width <= 0 ? PersonalBoardWindowDefaultWidth : _personalBoardWindow.Width);
            int x = _personalBoardOnRight
                ? this.Right + 2
                : this.Left - boardWidth - 2;
            int y = this.Top;
            int h = Math.Max(500, this.Height);

            _isUpdatingPersonalBoardBounds = true;
            try
            {
                _personalBoardWindow.SetBounds(x, y, boardWidth, h);
            }
            finally
            {
                _isUpdatingPersonalBoardBounds = false;
            }

            if (!_personalBoardWindow.Visible)
                _personalBoardWindow.Show(this);
        }

        private void BuildPersonalBoard()
        {
            try
            {
                var s = OrganizerStorage.LoadSettings();
                _personalBoardVisible = s.ShowPersonalBoard;
                _personalBoardOnRight = !string.Equals(s.PersonalBoardSide, "left", StringComparison.OrdinalIgnoreCase);
                _askAiVisible = s.ShowAiWidget;
            }
            catch
            {
                _personalBoardVisible = false;
                _personalBoardOnRight = true;
            }

            _personalStickerBoard = new StickerBoardPanel(
                UserStorage.GetFirebaseBaseUrl(),
                _currentUser.Name,
                true,
                username => false,
                localOnlyMode: true,
                localFileName: BuildPersonalBoardCacheFileName(),
                boardTitle: "\U0001f4cc PERSONAL BOARD",
                syncOffMessage: "PRIVATE BOARD: Local-only reminders and links for your own planning.");
            _personalStickerBoard.Dock = DockStyle.Fill;
            _personalStickerBoard.Visible = true;
            _personalStickerBoard.ApplyTheme(isDarkMode, _customTheme);
            BuildAskAiPanel();

            if (_personalBoardVisible)
                ShowPersonalBoardWindow();
            else
                HidePersonalBoardWindow();
        }

        private void SaveAiChatPreferences()
        {
            try
            {
                var s = OrganizerStorage.LoadSettings();
                s.ShowAiChatPanel = _aiChatVisible;
                s.AiChatPanelSide = _aiChatOnRight ? "right" : "left";
                OrganizerStorage.SaveSettings(s);
            }
            catch { }
        }

        private void BuildAiChatPanel()
        {
            try
            {
                var s = OrganizerStorage.LoadSettings();
                _aiChatVisible = s.ShowAiChatPanel;
                _aiChatOnRight = !string.Equals(s.AiChatPanelSide, "left", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                _aiChatVisible = false;
                _aiChatOnRight = true;
            }

            _aiChatFontSizeName = UserStorage.GetAiChatFontSize(_currentUser?.Name ?? UserStorage.GetLastUser() ?? "user");

            EnsureAiChatWindow();
            ApplyAiChatFontSize(_aiChatFontSizeName, false);
            if (_aiChatVisible)
                ShowAiChatWindow();
            else
                HideAiChatWindow();
        }

        private void EnsureAiChatWindow()
        {
            if (_aiChatWindow != null && !_aiChatWindow.IsDisposed)
                return;

            _aiChatWindow = new Form
            {
                Text = "AI Chat",
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                MinimizeBox = false,
                MaximizeBox = false,
                BackColor = this.BackColor,
                Size = new Size(AiChatWindowDefaultWidth, this.Height)
            };

            _aiChatHistory = new RichTextBox
            {
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                Dock = DockStyle.Fill,
                DetectUrls = true,
                BackColor = isDarkMode ? Color.FromArgb(20, 24, 34) : Color.White,
                ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59),
                Font = new Font("Segoe UI", 9.5f)
            };

            _aiChatInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f),
                BackColor = isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White,
                ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59)
            };

            _btnAiChatSend = new Button
            {
                Text = "Ask",
                Width = 70,
                Dock = DockStyle.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(31, 132, 255),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _btnAiChatSend.FlatAppearance.BorderSize = 0;

            var inputHost = new Panel { Dock = DockStyle.Bottom, Height = 38, Padding = new Padding(8, 6, 8, 6) };
            inputHost.Controls.Add(_aiChatInput);
            inputHost.Controls.Add(_btnAiChatSend);

            var title = new Label
            {
                Text = "Chat with AI",
                Dock = DockStyle.Left,
                Width = 150,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = isDarkMode ? Color.FromArgb(251, 146, 60) : Color.FromArgb(194, 65, 12),
                Padding = new Padding(8, 0, 0, 0)
            };

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 30
            };

            _btnAiFontBig = new Button
            {
                Text = "Big",
                Width = 46,
                Height = 22,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(headerPanel.Width - 50, 4),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnAiFontBig.FlatAppearance.BorderSize = 0;

            _btnAiFontMedium = new Button
            {
                Text = "Med",
                Width = 46,
                Height = 22,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(headerPanel.Width - 100, 4),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnAiFontMedium.FlatAppearance.BorderSize = 0;

            _btnAiFontSmall = new Button
            {
                Text = "Small",
                Width = 52,
                Height = 22,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(headerPanel.Width - 156, 4),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnAiFontSmall.FlatAppearance.BorderSize = 0;

            _btnAiFontSmall.Click += (s, e) => ApplyAiChatFontSize("Small", true);
            _btnAiFontMedium.Click += (s, e) => ApplyAiChatFontSize("Medium", true);
            _btnAiFontBig.Click += (s, e) => ApplyAiChatFontSize("Big", true);

            headerPanel.Controls.Add(_btnAiFontBig);
            headerPanel.Controls.Add(_btnAiFontMedium);
            headerPanel.Controls.Add(_btnAiFontSmall);
            headerPanel.Controls.Add(title);

            _aiChatWindow.Controls.Clear();
            _aiChatWindow.Controls.Add(_aiChatHistory);
            _aiChatWindow.Controls.Add(inputHost);
            _aiChatWindow.Controls.Add(headerPanel);

            _btnAiChatSend.Click += async (s, e) => await SendAiChatMessageAsync();
            _aiChatInput.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && !e.Shift)
                {
                    e.SuppressKeyPress = true;
                    await SendAiChatMessageAsync();
                }
            };

            _aiChatWindow.FormClosing += (s, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    _aiChatVisible = false;
                    _aiChatWindow.Hide();
                    SaveAiChatPreferences();
                }
            };

            _aiChatWindow.Move += (s, e) =>
            {
                if (_isUpdatingAiChatBounds || _aiChatWindow == null || _aiChatWindow.IsDisposed)
                    return;
                int chatCenter = _aiChatWindow.Left + (_aiChatWindow.Width / 2);
                int mainCenter = this.Left + (this.Width / 2);
                bool newRight = chatCenter >= mainCenter;
                if (newRight != _aiChatOnRight)
                {
                    _aiChatOnRight = newRight;
                    SaveAiChatPreferences();
                }
            };

            ApplyAiChatFontSize(_aiChatFontSizeName, false);
        }

        private void ShowAiChatWindow()
        {
            EnsureAiChatWindow();
            UpdateAiChatWindowBounds();
            RefreshAskAiPanelStatus();
            if (!_aiChatWindow.Visible)
                _aiChatWindow.Show(this);
            _aiChatWindow.BringToFront();
            EnsureAiGreeting();
        }

        private void HideAiChatWindow()
        {
            if (_aiChatWindow != null && !_aiChatWindow.IsDisposed)
                _aiChatWindow.Hide();
        }

        private void UpdateAiChatWindowBounds()
        {
            if (!_aiChatVisible || _aiChatWindow == null || _aiChatWindow.IsDisposed)
                return;
            if (this.WindowState == FormWindowState.Minimized)
            {
                _aiChatWindow.Hide();
                return;
            }

            int chatWidth = Math.Max(360, _aiChatWindow.Width <= 0 ? AiChatWindowDefaultWidth : _aiChatWindow.Width);
            int rightOffset = 2;
            int leftOffset = 2;
            if (_personalBoardVisible && _personalBoardWindow != null && !_personalBoardWindow.IsDisposed)
            {
                int personalWidth = Math.Max(STICKER_WIDTH, _personalBoardWindow.Width <= 0 ? PersonalBoardWindowDefaultWidth : _personalBoardWindow.Width);
                if (_aiChatOnRight && _personalBoardOnRight)
                    rightOffset += personalWidth + 2;
                if (!_aiChatOnRight && !_personalBoardOnRight)
                    leftOffset += personalWidth + 2;
            }

            int x = _aiChatOnRight
                ? this.Right + rightOffset
                : this.Left - chatWidth - leftOffset;
            int y = this.Top;
            int h = Math.Max(500, this.Height);

            _isUpdatingAiChatBounds = true;
            try
            {
                _aiChatWindow.SetBounds(x, y, chatWidth, h);
            }
            finally
            {
                _isUpdatingAiChatBounds = false;
            }

            if (!_aiChatWindow.Visible)
                _aiChatWindow.Show(this);
        }

        private void EnsureAiGreeting()
        {
            if (_aiChatGreeted)
                return;

            string userName = string.IsNullOrWhiteSpace(_currentUser?.Name) ? "there" : _currentUser.Name;
            AppendAiChat("assistant", $"Hi {userName}, I am your project AI assistant. Ask anything about files, stickers, notes, or your current work.");
            _aiChatGreeted = true;
        }

        private void ApplyAiChatFontSize(string sizeName, bool persist)
        {
            _aiChatFontSizeName = string.IsNullOrWhiteSpace(sizeName) ? "Small" : sizeName;
            switch (_aiChatFontSizeName)
            {
                case "Big":
                    _aiChatFontSize = 12f;
                    break;
                case "Medium":
                    _aiChatFontSize = 10.5f;
                    break;
                default:
                    _aiChatFontSizeName = "Small";
                    _aiChatFontSize = 9.5f;
                    break;
            }

            if (_aiChatHistory != null && !_aiChatHistory.IsDisposed)
                _aiChatHistory.Font = new Font("Segoe UI", _aiChatFontSize);
            if (_aiChatInput != null && !_aiChatInput.IsDisposed)
                _aiChatInput.Font = new Font("Segoe UI", _aiChatFontSize);

            Color activeCol = Color.FromArgb(255, 127, 80);
            Color inactiveCol = isDarkMode ? Color.FromArgb(45, 55, 72) : Color.FromArgb(226, 232, 240);

            if (_btnAiFontSmall != null) _btnAiFontSmall.BackColor = _aiChatFontSizeName == "Small" ? activeCol : inactiveCol;
            if (_btnAiFontMedium != null) _btnAiFontMedium.BackColor = _aiChatFontSizeName == "Medium" ? activeCol : inactiveCol;
            if (_btnAiFontBig != null) _btnAiFontBig.BackColor = _aiChatFontSizeName == "Big" ? activeCol : inactiveCol;

            if (_btnAiFontSmall != null) _btnAiFontSmall.ForeColor = Color.White;
            if (_btnAiFontMedium != null) _btnAiFontMedium.ForeColor = Color.White;
            if (_btnAiFontBig != null) _btnAiFontBig.ForeColor = Color.White;

            if (persist)
                UserStorage.SaveAiChatFontSize(_currentUser?.Name ?? UserStorage.GetLastUser() ?? "user", _aiChatFontSizeName);
        }

        private void AppendAiChat(string role, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            _aiChatMessages.Add(new AiChatMessage { Role = role ?? "assistant", Content = text.Trim() });
            if (_aiChatHistory == null || _aiChatHistory.IsDisposed)
                return;

            string prefix = role == "user" ? $"{_currentUser?.Name ?? "You"}" : "AI";
            _aiChatHistory.AppendText(prefix + ": " + text.Trim() + Environment.NewLine + Environment.NewLine);
            _aiChatHistory.SelectionStart = _aiChatHistory.TextLength;
            _aiChatHistory.ScrollToCaret();
        }

        private string BuildAiConversationTranscript()
        {
            var sb = new StringBuilder(6000);
            int start = Math.Max(0, _aiChatMessages.Count - 16);
            for (int i = start; i < _aiChatMessages.Count; i++)
            {
                var item = _aiChatMessages[i];
                sb.AppendLine($"{item.Role}: {item.Content}");
            }
            return sb.ToString();
        }

        private async Task SendAiChatMessageAsync()
        {
            if (_aiChatInput == null || _btnAiChatSend == null || _aiChatHistory == null)
                return;

            string prompt = _aiChatInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;
            if (!_askAiVisible)
            {
                MessageBox.Show("Ask AI is OFF. Enable it in Team Settings > Panel Visibility > Ask AI.", "AI Chat",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!TryResolveAiConfig(out var endpoint, out var model, out var apiKey, out _))
            {
                MessageBox.Show("AI API is not configured. Add API key in Team Settings.", "AI Chat",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _btnAiChatSend.Enabled = false;
            _aiChatInput.Enabled = false;
            AppendAiChat("user", prompt);

            try
            {
                string context = await BuildAiProjectContextAsync();
                string transcript = BuildAiConversationTranscript();
                string fullPrompt =
                    "Conversation so far:\n" + transcript + "\n\n" +
                    "New user question:\n" + prompt + "\n\n" +
                    "--- PROJECT CONTEXT (AUTO) ---\n" + context;
                string answer = await RequestAiCompletionAsync(endpoint, model, apiKey, fullPrompt);
                AppendAiChat("assistant", answer);
            }
            catch (Exception ex)
            {
                AppendAiChat("assistant", "I could not answer right now: " + ex.Message);
            }
            finally
            {
                _aiChatInput.Clear();
                _aiChatInput.Enabled = true;
                _btnAiChatSend.Enabled = true;
                _aiChatInput.Focus();
            }
        }

        /// <summary>Play a distinctive alarm sound using SoundManager pattern.</summary>
        private void PlayOrganizerAlarm()
        {
            // Use SoundManager's pattern ? urgent triple ascending tone
            try
            {
//                 DebugLogger.Log("[Form1] Playing sound: ping alert");
            SoundManager.PlayPingAlert(); // Reuse the attention-grabbing triple beep
            }
            catch { }
        }

        /// <summary>Show the alarm notification popup.</summary>
        private void ShowAlarmNotification(OrganizerEntry entry)
        {
            try
            {
                // Mark as fired to prevent re-showing
                OrganizerStorage.MarkAlarmFired(entry.Id);

                var notification = new AlarmNotificationForm(entry, isDarkMode, _customTheme);
                notification.OpenDayRequested += (date) =>
                {
                    if (!this.IsDisposed)
                        this.BeginInvoke(new Action(() => OpenDayOrganizer(date)));
                };
                notification.Show();

                // Also show tray balloon if available
                if (_trayIcon != null && _trayIcon.Visible)
                {
                    _trayIcon.ShowBalloonTip(5000, "Reminder", entry.Title, ToolTipIcon.Info);
                }
            }
            catch { }
        }

        /// <summary>Create the next occurrence of a recurring alarm.</summary>
        private void CreateNextRecurrence(OrganizerEntry entry)
        {
            try
            {
                if (!DateTime.TryParse(entry.AlarmDateTime, out var alarmDt)) return;
                DateTime nextAlarm;
                DateTime nextDate;

                switch (entry.Recurrence)
                {
                    case RecurrenceType.Daily:
                        nextAlarm = alarmDt.AddDays(1);
                        nextDate = DateTime.TryParse(entry.Date, out var d) ? d.AddDays(1) : nextAlarm.Date;
                        break;
                    case RecurrenceType.Weekly:
                        nextAlarm = alarmDt.AddDays(7);
                        nextDate = DateTime.TryParse(entry.Date, out var w) ? w.AddDays(7) : nextAlarm.Date;
                        break;
                    case RecurrenceType.Monthly:
                        nextAlarm = alarmDt.AddMonths(1);
                        nextDate = DateTime.TryParse(entry.Date, out var m) ? m.AddMonths(1) : nextAlarm.Date;
                        break;
                    default:
                        return;
                }

                var nextEntry = new OrganizerEntry
                {
                    Date = nextDate.ToString("yyyy-MM-dd"),
                    Title = entry.Title,
                    Description = entry.Description,
                    Category = entry.Category,
                    Status = OrganizerStatus.Planned,
                    Priority = entry.Priority,
                    TimeFrom = entry.TimeFrom,
                    TimeTo = entry.TimeTo,
                    Link = entry.Link,
                    AlarmEnabled = true,
                    AlarmDateTime = nextAlarm.ToString("o"),
                    AlarmFired = false,
                    Recurrence = entry.Recurrence,
                    Owner = entry.Owner
                };

                OrganizerStorage.SaveEntry(nextEntry);
            }
            catch { }
        }

        /// <summary>Show reminders for today on startup.</summary>
        private void ShowStartupReminders()
        {
            try
            {
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                var todayEntries = OrganizerStorage.GetEntriesForDate(today);
                var pending = todayEntries.Where(e => !e.IsCompleted && e.Status == OrganizerStatus.Planned).ToList();
                if (pending.Count > 0)
                {
                    string msg = $"You have {pending.Count} item{(pending.Count > 1 ? "s" : "")} planned for today:\n\n";
                    foreach (var entry in pending.Take(5))
                    {
                        string timeStr = !string.IsNullOrEmpty(entry.TimeFrom) ? $"  {entry.TimeFrom}" : "";
                        msg += $"  - {entry.Title}{timeStr}\n";
                    }
                    if (pending.Count > 5) msg += $"\n  ... and {pending.Count - 5} more";

                    // Use a delayed invocation so it shows after the form is fully loaded
                    var startupTimer = new Timer { Interval = 3000 };
                    startupTimer.Tick += (s, e) =>
                    {
                        startupTimer.Stop();
                        startupTimer.Dispose();
                        if (!this.IsDisposed)
                        {
//                             DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show(this, msg, "Today's Schedule", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    };
                    startupTimer.Start();
                }
            }
            catch { }
        }

        // --- PANEL SYSTEM ? STICKER BOARD, CHAT, FILES, WIKI, SPLITTERS ---
        // ============================================================
        // BUILD STICKER BOARD + CHAT + TOGGLE BUTTONS (NEW)
        // ============================================================
        private const int STICKER_WIDTH = 540;
        private const int CHAT_HEIGHT = 220;
        private const int RIGHT_COLUMN_WIDTH = 320;

        private int GetDefaultBottomPanelHeight()
        {
            int minBottomHeight = 120;
            int available = Math.Max(260, this.ClientSize.Height - statusStrip1.Height - 260);
            int centered = available / 2;
            return Math.Max(minBottomHeight, centered);
        }

        private void ResetSplittersToDefault()
        {
            _bottomPanelHeight = GetDefaultBottomPanelHeight();
            if (_chatPanel != null)
            {
                int leftBottomWidth = Math.Max(420, this.ClientSize.Width - RIGHT_COLUMN_WIDTH);
                int minChatW = 220;
                int minFilesW = 220;
                int chatW = (int)(leftBottomWidth * 0.6);
                chatW = Math.Max(minChatW, Math.Min(leftBottomWidth - minFilesW, chatW));
                _chatPanel.Width = chatW;
            }

            ApplyMainColumnLayout();
            SaveCurrentPanelLayout();
        }

        private void ApplyMainColumnLayout()
        {
            if (this.IsDisposed || panelOnlineUsers == null)
                return;

            if (_bottomPanelHeight <= 0)
                _bottomPanelHeight = GetDefaultBottomPanelHeight();

            int rightColWidth = RIGHT_COLUMN_WIDTH;
            int minBottomHeight = 120;
            int maxBottomHeight = Math.Max(minBottomHeight, this.ClientSize.Height - statusStrip1.Height - 260);
            int bottomHeight = Math.Max(minBottomHeight, Math.Min(maxBottomHeight, _bottomPanelHeight));
            _bottomPanelHeight = bottomHeight;
            int chatTopY = this.ClientSize.Height - statusStrip1.Height - bottomHeight;
            int weatherHeight = (_weatherVisible && _weatherPanel != null) ? 74 : 0;
            int onlineTopHeight = Math.Max(120, chatTopY - weatherHeight);

            int leftBottomWidth = Math.Max(420, this.ClientSize.Width - rightColWidth);

            // Right column top: user panel
            panelOnlineUsers.Dock = DockStyle.None;
            panelOnlineUsers.SetBounds(this.ClientSize.Width - rightColWidth, 0, rightColWidth, onlineTopHeight);
            panelOnlineUsers.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            if (_weatherPanel != null)
            {
                _weatherPanel.Visible = _weatherVisible;
                _weatherPanel.SetBounds(this.ClientSize.Width - rightColWidth, onlineTopHeight, rightColWidth, weatherHeight);
                _weatherPanel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            }

            // Right column bottom: calendar panel
            if (_calendarPanel != null)
            {
                _calendarPanel.Dock = DockStyle.None;
                _calendarPanel.SetBounds(this.ClientSize.Width - rightColWidth, chatTopY, rightColWidth, bottomHeight);
                _calendarPanel.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            }

            // Bottom row left to right: chat + file share
            if (_chatPanel != null && _fileSharePanel != null)
            {
                int minChatW = 220;
                int minFilesW = 220;
                int chatW = _chatPanel.Width > 0 ? _chatPanel.Width : (int)(leftBottomWidth * 0.62);
                chatW = Math.Max(minChatW, Math.Min(leftBottomWidth - minFilesW, chatW));
                int filesW = Math.Max(minFilesW, leftBottomWidth - chatW);

                _chatPanel.SetBounds(0, chatTopY, chatW, bottomHeight);
                _fileSharePanel.SetBounds(chatW, chatTopY, filesW, bottomHeight);

                if (_projectFolderPanel != null)
                    _projectFolderPanel.SetBounds(chatW, chatTopY, filesW, bottomHeight);

                if (_splitterVertBottom != null)
                    _splitterVertBottom.SetBounds(chatW, chatTopY, 6, bottomHeight);
            }

            if (_splitterHoriz != null)
                _splitterHoriz.SetBounds(0, chatTopY - 3, leftBottomWidth, 6);

            if (_stickerBoard != null)
            {
                int boardTop = _stickerBoard.Top;
                _stickerBoard.Location = new Point(0, boardTop);
                _stickerBoard.Width = STICKER_WIDTH;
                _stickerBoard.Height = Math.Max(220, chatTopY - boardTop);
                _stickerBoard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            }

            if (dataGridView1 != null)
            {
                int gridBottom = chatTopY - 3;
                int newGridHeight = Math.Max(120, gridBottom - dataGridView1.Top);
                dataGridView1.Height = newGridHeight;
            }
        }

        /// <summary>
        /// BUILD STICKER BOARD + CHAT + TOGGLE BUTTONS
        /// Creates the main layout: sticker board (left), work counter (center), chat/files (bottom),
        /// team status panel (right), and draggable splitters for resizing.
        /// This is the MAIN panel layout orchestration method.
        /// </summary>
        private void BuildStickerAndChatPanels()
        {
//             DebugLogger.Log("[Form1] BuildStickerAndChatPanels() starting ? setting up main UI layout");

            this.SuspendLayout();

            // ----------------------------------------------------------
            // Step 1: Resize form FIRST so all calculations use the
            //         correct ClientSize dimensions
            // ----------------------------------------------------------
//             DebugLogger.Log("[Form1] Step 1: Resizing form to 1600?950 base size");
            this.Size = new Size(1600, 950);
            this.MinimumSize = new Size(1200, 700);

            // ----------------------------------------------------------
            // Step 2: Force layout so ClientSize reflects new Size(1600,950)
            // ----------------------------------------------------------
            this.ResumeLayout(true);
            this.SuspendLayout();

            // Now ClientSize is correct for the 1600?950 form
            int teamPanelWidth = panelOnlineUsers.Width; // 320
            int clientW = this.ClientSize.Width;         // ~1584
            if (_bottomPanelHeight <= 0)
                _bottomPanelHeight = GetDefaultBottomPanelHeight();
            int chatTopY = this.ClientSize.Height - _bottomPanelHeight - statusStrip1.Height;
            int maxGridBottom = chatTopY - 3;

            // ----------------------------------------------------------
            // Step 2b: Reposition middle-area controls in clean rows
            //          between sticker board and team status panel
            // ----------------------------------------------------------
            int midLeft = STICKER_WIDTH;
            int midRight = clientW - teamPanelWidth - ThemeConstants.SpaceL;
            int midW = midRight - midLeft;
            int topOffset = ThemeConstants.ToolbarHeight + ThemeConstants.SpaceM;
            int innerGap = ThemeConstants.SpaceM;
            int timerBlockWidth = 360;
            int logoSize = 56;
            int actionBtnW = 118;
            int actionBtnH = 56;
            int actionBtnGap = ThemeConstants.SpaceS;
            int actionBlockWidth = (actionBtnW * 2) + actionBtnGap;
            int actionAreaWidth = actionBlockWidth + innerGap + logoSize;
            int workInputWidth = Math.Max(360, midW - actionAreaWidth - 32);

            Color headerAccent = isDarkMode ? ThemeConstants.Dark.AccentPrimary : ThemeConstants.Light.AccentPrimary;

            // -- ROW 0 (y=topOffset): Title left | hero clock right --
            label1.Font = ThemeConstants.FontHeading;
            label1.ForeColor = headerAccent;
            label1.Location = new Point(midLeft + 2, topOffset + 2);
            UpdateActiveTeamBadge(new Point(label1.Right + ThemeConstants.SpaceM + 10, topOffset + 20), midRight - timerBlockWidth - ThemeConstants.SpaceL);

            // -- CLOCK: Premium hero time display --
            int timerBlockLeft = Math.Max(midLeft + 320, midRight - timerBlockWidth - 14) + 8; // ~2mm right
            if (_activeTeamBadge != null)
            {
                // Keep a clean gap from the team card so elements never touch.
                timerBlockLeft = Math.Max(timerBlockLeft, _activeTeamBadge.Right + 8);
            }
            label3.Text = "";
            label3.Visible = false;
            labelTimerNow.Font = new Font(ThemeConstants.FontFamily, 34f, FontStyle.Bold);
            labelTimerNow.ForeColor = isDarkMode ? ThemeConstants.Dark.TextPrimary : ThemeConstants.Light.TextPrimary;
            labelTimerNow.BackColor = Color.Transparent;
            labelTimerNow.AutoSize = true;
            string timerPreview = DateTime.Now.ToString("HH:mm:ss");
            int timerPreviewWidth = TextRenderer.MeasureText(timerPreview, labelTimerNow.Font).Width;
            int timerCenteredX = timerBlockLeft + Math.Max(0, (timerBlockWidth - timerPreviewWidth) / 2);
            labelTimerNow.Location = new Point(timerCenteredX, topOffset - 8);

            // checkBoxTheme hidden but still used internally for state
            checkBoxTheme.Visible = false;
            checkBoxTheme.Checked = isDarkMode;

            int infoBaselineY = _activeTeamBadge != null
                ? _activeTeamBadge.Bottom
                : topOffset + 88;

            // -- ROW 1: section caption left | timing meta right --
            int row1Y = topOffset + 38;
            labelDescription.Font = ThemeConstants.FontBody;
            labelDescription.ForeColor = isDarkMode ? ThemeConstants.Dark.TextSecondary : ThemeConstants.Light.TextSecondary;
            labelDescription.Location = new Point(midLeft + 2, row1Y);

            // Time info ? right-aligned compact row
            label4.Font = new Font(ThemeConstants.FontFamily, 9.5f, FontStyle.Regular);
            label4.ForeColor = isDarkMode ? ThemeConstants.Dark.TextMuted : ThemeConstants.Light.TextMuted;
            label4.Location = new Point(timerBlockLeft + 6, infoBaselineY - label4.PreferredHeight);
            labelStartTime.Font = new Font(ThemeConstants.FontFamily, 12.5f, FontStyle.Bold);
            labelStartTime.Location = new Point(timerBlockLeft + 72, infoBaselineY - labelStartTime.PreferredHeight);

            label5.Font = new Font(ThemeConstants.FontFamily, 9.5f, FontStyle.Regular);
            label5.ForeColor = isDarkMode ? ThemeConstants.Dark.TextMuted : ThemeConstants.Light.TextMuted;
            label5.Location = new Point(timerBlockLeft + 158, infoBaselineY - label5.PreferredHeight);
            labelWorkingTime.Font = new Font(ThemeConstants.FontFamily, 12.5f, FontStyle.Bold);
            labelWorkingTime.Location = new Point(timerBlockLeft + 262, infoBaselineY - labelWorkingTime.PreferredHeight);

            // -- ROW 2: work input left | actions right --
            int row2Y = infoBaselineY - ThemeConstants.InputHeight;

            // Project/Category selector ? improved sizing
            _cmbProject = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = ThemeConstants.FontBody,
                Size = new Size(140, ThemeConstants.InputHeight),
                Location = new Point(midLeft, row2Y),
                BackColor = isDarkMode ? ThemeConstants.Dark.BgInput : ThemeConstants.Light.BgInput,
                ForeColor = isDarkMode ? ThemeConstants.Dark.TextPrimary : ThemeConstants.Light.TextPrimary
            };
            _cmbProject.Items.AddRange(new object[] { "General", "Development", "Testing", "Meeting", "Documentation", "Support", "Research" });
            _cmbProject.SelectedIndex = 0;
            _cmbProject.SelectedIndexChanged += async (s, e) => await RefreshPersonalBoardForCurrentProjectAsync();
            this.Controls.Add(_cmbProject);
            _cmbProject.BringToFront();

            int descriptionRight = _activeTeamBadge != null
                ? _activeTeamBadge.Right
                : (midLeft + workInputWidth);
            richTextBoxDescription.Location = new Point(midLeft, row2Y + ThemeConstants.InputHeight + ThemeConstants.SpaceXS);
            richTextBoxDescription.Size = new Size(Math.Max(220, descriptionRight - midLeft), 56);
            int actionBoxTop = richTextBoxDescription.Top;

            int totalTopActionWidth = actionBtnW + actionBtnW + logoSize;
            int availableTopActionWidth = Math.Max(totalTopActionWidth + (ThemeConstants.SpaceS * 3), midRight - descriptionRight);
            int symmetricGap = Math.Max(ThemeConstants.SpaceS, Math.Min(ThemeConstants.SpaceL + 4, (availableTopActionWidth - totalTopActionWidth) / 3));

            int btnColX = descriptionRight + symmetricGap;
            int stopBtnX = btnColX + actionBtnW + symmetricGap;
            int logoX = stopBtnX + actionBtnW + symmetricGap;

            int overflowRight = (logoX + logoSize) - midRight;
            if (overflowRight > 0)
            {
                btnColX -= overflowRight;
                stopBtnX -= overflowRight;
                logoX -= overflowRight;
            }
            buttonStart.Size = new Size(actionBtnW, actionBtnH);
            ThemeConstants.StyleActionButton(buttonStart, isDarkMode, true);
            ApplyRoundedCorners(buttonStart, ThemeConstants.RadiusMedium);
            buttonStart.Location = new Point(btnColX, actionBoxTop);
            buttonStop.Size = new Size(actionBtnW, actionBtnH);
            ThemeConstants.StyleActionButton(buttonStop, isDarkMode, false);
            ApplyRoundedCorners(buttonStop, ThemeConstants.RadiusMedium);
            buttonStop.Location = new Point(stopBtnX, actionBoxTop);

            // Refresh & Print - glossy circular icon buttons below Start/Stop
            int roundActionBtnSize = 42;
            buttonRefresh.Text = string.Empty;
            buttonRefresh.Size = new Size(roundActionBtnSize, roundActionBtnSize);
            buttonRefresh.Location = new Point(btnColX, row2Y + actionBtnH + actionBtnGap + 4);
            ThemeConstants.StyleRefreshButton(buttonRefresh, isDarkMode);
            buttonRefresh.Image = ThemeConstants.CreateRefreshIcon(roundActionBtnSize - 3, Color.White);
            buttonRefresh.TextImageRelation = TextImageRelation.Overlay;
            ApplyCircularButton(buttonRefresh);
            buttonPrint.Text = string.Empty;
            buttonPrint.Size = new Size(roundActionBtnSize, roundActionBtnSize);
            buttonPrint.Location = new Point(buttonRefresh.Right + actionBtnGap + 8, row2Y + actionBtnH + actionBtnGap + 4);
            ThemeConstants.StylePrintButton(buttonPrint, isDarkMode);
            buttonPrint.Image = ThemeConstants.CreatePrintIcon(roundActionBtnSize - 3, Color.White);
            buttonPrint.TextImageRelation = TextImageRelation.Overlay;
            ApplyCircularButton(buttonPrint);

            // Hide old buttons
            button1.Visible = false;
            buttonReport.Visible = false;

            // Company logo ? aligned using symmetric spacing with Start/Stop
            pictureBoxLogo.Size = new Size(logoSize, logoSize);
            pictureBoxLogo.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxLogo.BackColor = Color.Transparent;
            pictureBoxLogo.Location = new Point(logoX, actionBoxTop);
            EnsureLogoImage();
            pictureBoxLogo.BringToFront();

            // -- ROW 3: filters row --
            int row3Y = richTextBoxDescription.Bottom + ThemeConstants.SpaceS + 4;

            buttonSave.Location = new Point(midLeft, row3Y);
            buttonSave.Size = new Size(80, ThemeConstants.ButtonHeightSm);
            buttonSave.Text = "Save";
            ThemeConstants.StyleButtonSecondary(buttonSave, isDarkMode);

            buttonDelete.Location = new Point(midLeft + 88, row3Y);
            buttonDelete.Size = new Size(80, ThemeConstants.ButtonHeightSm);
            buttonDelete.Text = "Delete";
            ThemeConstants.StyleButtonSecondary(buttonDelete, isDarkMode);

            // Date range filter
            int filterRowY = row3Y + 1;
            int filterGap = 8;
            int datePickerWidth = 102;
            int fromToValueGap = 6;
            int lblFromWidth = TextRenderer.MeasureText("From:", ThemeConstants.FontSmall).Width;
            int lblToWidth = TextRenderer.MeasureText("To:", ThemeConstants.FontSmall).Width;

            int dateRowGroupWidth =
                lblFromWidth + fromToValueGap + datePickerWidth +
                filterGap + lblToWidth + fromToValueGap + datePickerWidth;

            int dateRowSidePadding = 12;
            int dateContentAvailable = Math.Max(0, richTextBoxDescription.Width - (dateRowSidePadding * 2));
            int filterRowStartX = richTextBoxDescription.Left + dateRowSidePadding;
            if (dateRowGroupWidth > dateContentAvailable)
            {
                // Fallback to centered placement if future font/locale makes controls wider.
                filterRowStartX = richTextBoxDescription.Left +
                    Math.Max(0, (richTextBoxDescription.Width - dateRowGroupWidth) / 2);
            }

            var lblFrom = new Label
            {
                Text = "From:",
                Font = ThemeConstants.FontSmall,
                ForeColor = isDarkMode ? ThemeConstants.Dark.TextMuted : ThemeConstants.Light.TextMuted,
                AutoSize = true,
                Location = new Point(filterRowStartX, filterRowY + 4)
            };
            this.Controls.Add(lblFrom);
            lblFrom.BringToFront();

            _dtpFrom = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Font = ThemeConstants.FontSmall,
                Size = new Size(datePickerWidth, ThemeConstants.ButtonHeightSm),
                Location = new Point(filterRowStartX + lblFromWidth + fromToValueGap, filterRowY),
                Value = DateTime.Today.AddDays(-30)
            };
            _dtpFrom.ValueChanged += (s, e) => RefreshLogs();
            this.Controls.Add(_dtpFrom);
            _dtpFrom.BringToFront();

            var lblTo = new Label
            {
                Text = "To:",
                Font = ThemeConstants.FontSmall,
                ForeColor = isDarkMode ? ThemeConstants.Dark.TextMuted : ThemeConstants.Light.TextMuted,
                AutoSize = true,
                Location = new Point(_dtpFrom.Right + filterGap, filterRowY + 4)
            };
            this.Controls.Add(lblTo);
            lblTo.BringToFront();

            _dtpTo = new DateTimePicker
            {
                Format = DateTimePickerFormat.Short,
                Font = ThemeConstants.FontSmall,
                Size = new Size(datePickerWidth, ThemeConstants.ButtonHeightSm),
                Location = new Point(lblTo.Right + fromToValueGap, filterRowY),
                Value = DateTime.Today
            };
            _dtpTo.ValueChanged += (s, e) => RefreshLogs();
            this.Controls.Add(_dtpTo);
            _dtpTo.BringToFront();

            // Keep refresh/print and "My Work" aligned on the same row.
            // Refresh/Print are locked to START button left/right edges (visual symmetry).
            int printToUserGap = 14;
            int actionRowY = filterRowY + ((_dtpFrom.Height - buttonRefresh.Height) / 2);

            buttonRefresh.Location = new Point(buttonStart.Left, actionRowY);
            buttonPrint.Location = new Point(buttonStart.Right - buttonPrint.Width, actionRowY);

            int userAlignedRightX = pictureBoxLogo.Right - comboBoxUserFilter.Width;
            int userMinX = buttonPrint.Right + printToUserGap;
            int userX = Math.Max(userMinX, userAlignedRightX);
            comboBoxUserFilter.Location = new Point(userX, filterRowY + 1);

            // Keep "My Work" inside middle column.
            int filterRowRightLimit = midRight - 4;
            if (comboBoxUserFilter.Right > filterRowRightLimit)
                comboBoxUserFilter.Left = filterRowRightLimit - comboBoxUserFilter.Width;

            // -- ROW 4+: DataGridView fills remaining space ? premium grid styling --
            int gridTop = row3Y + ThemeConstants.ButtonHeightSm + ThemeConstants.SpaceS;
            dataGridView1.Location = new Point(midLeft, gridTop);
            dataGridView1.Size = new Size(midRight - midLeft, maxGridBottom - gridTop);
            dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            ThemeConstants.StyleDataGrid(dataGridView1, isDarkMode);

            // ----------------------------------------------------------
            // ADMIN & MUTE CHECKS ? needed by sticker board, chat, and settings
            // ----------------------------------------------------------
//             DebugLogger.Log("[Form1] Loading team from storage");
            var teamForMute = UserStorage.LoadTeam();
            bool userIsAdmin = _currentUser.IsAdmin || (teamForMute != null && teamForMute.HasAdminPrivileges(_currentUser.Name));
            Func<string, bool> isMutedCheck = (name) =>
            {
//                 DebugLogger.Log("[Form1] Loading team from storage");
                var t = UserStorage.LoadTeam();
                return t != null && t.IsMuted(name);
            };

            // ----------------------------------------------------------
            // Step 4: Create sticker board ? absolute position, left side
            // ----------------------------------------------------------
            _stickerBoard = new StickerBoardPanel(UserStorage.GetFirebaseBaseUrl(), _currentUser.Name, userIsAdmin, isMutedCheck);
            _stickerBoard.Dock = DockStyle.None;
            int boardTop = ThemeConstants.SpaceM + 2;
            _stickerBoard.Location = new Point(0, boardTop);
            _stickerBoard.Size = new Size(STICKER_WIDTH, chatTopY - boardTop);
            _stickerBoard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;

            // Double-click on sticker ? fill description + auto-start work timer
            _stickerBoard.OnStartWorkRequested += (sticker) =>
            {
                if (richTextBoxDescription != null)
                {
                    // Build description from sticker title + description
                    string desc = sticker.title ?? "";
                    if (!string.IsNullOrWhiteSpace(sticker.description))
                        desc += " ? " + sticker.description;
                    richTextBoxDescription.Text = desc;
                }
                // Trigger the Start button click
                buttonStart_Click(buttonStart, EventArgs.Empty);
            };

            // ----------------------------------------------------------
            // Step 5: Create chat panel ? left 60% of bottom area
            // ----------------------------------------------------------
            int bottomWidth = this.ClientSize.Width - panelOnlineUsers.Width;
            int chatWidth = (int)(bottomWidth * 0.6);
            int filesWidth = bottomWidth - chatWidth;

            _chatPanel = new ChatPanel(UserStorage.GetFirebaseBaseUrl(), _currentUser.Name, userIsAdmin, isMutedCheck, _allUsers);
            _chatPanel.Dock = DockStyle.None;
            _chatPanel.Location = new Point(0, chatTopY);
            _chatPanel.Size = new Size(chatWidth, CHAT_HEIGHT);
            _chatPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            // Apply saved chat font size preference
            string savedFontSize = UserStorage.GetChatFontSize(_currentUser.Name);
            _chatPanel.SetChatFontSize(savedFontSize);

            // ----------------------------------------------------------
            // Step 5b: Create file share panel ? right 40% of bottom area
            // ----------------------------------------------------------
            _fileSharePanel = new FileSharePanel(UserStorage.GetFirebaseBaseUrl(), _currentUser.Name);
            _fileSharePanel.Dock = DockStyle.None;
            _fileSharePanel.Location = new Point(chatWidth, chatTopY);
            _fileSharePanel.Size = new Size(filesWidth, CHAT_HEIGHT);
            _fileSharePanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            // Password verification: use the current user's VerifyPassword method
            _fileSharePanel.VerifyPassword = (pw) => _currentUser.VerifyPassword(pw);
            // Admin permission for shared folder delete
//             DebugLogger.Log("[Form1] Loading team from storage");
            var _loadedTeam = UserStorage.LoadTeam();
            _fileSharePanel.IsAdmin = _loadedTeam != null && _loadedTeam.HasAdminPrivileges(_currentUser.Name);
            // Peer online check for availability status
            _fileSharePanel.IsPeerOnline = (userName) =>
            {
                // Check online status from the users panel
                try
                {
                    if (_previousUserStatuses != null && _previousUserStatuses.ContainsKey(userName))
                    {
                        var status = _previousUserStatuses[userName];
                        return status == "online" || status == "working";
                    }
                }
                catch { }
                return false;
            };

            // ----------------------------------------------------------
            // Step 5c: Create Helper Wiki panel ? opens to the RIGHT
            //   by expanding the window. No docking ? manual position.
            // ----------------------------------------------------------
            _helperPanel = new HelperPanel(UserStorage.GetFirebaseBaseUrl(), _currentUser.Name, _currentUser.IsAdmin);
            _helperPanel.Dock = DockStyle.Fill;
            _helperPanel.Visible = true;

            // ----------------------------------------------------------
            // Step 6: Add panels to form and bring to front
            // ----------------------------------------------------------
            // -- PROJECT FOLDER PANEL ? below/beside file share area --
            _projectFolderPanel = new ProjectFolderPanel();
            _projectFolderPanel.Dock = DockStyle.None;
            _projectFolderPanel.Location = new Point(chatWidth, chatTopY);
            _projectFolderPanel.Size = new Size(filesWidth, CHAT_HEIGHT);
            _projectFolderPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _projectFolderPanel.Visible = false; // Hidden by default, toggled via settings
            _projectFolderPanel.ApplyTheme(isDarkMode, _customTheme);

            this.Controls.Add(_stickerBoard);
            this.Controls.Add(_chatPanel);
            this.Controls.Add(_fileSharePanel);
            this.Controls.Add(_projectFolderPanel);

            _stickerBoard.BringToFront();
            _chatPanel.BringToFront();
            _fileSharePanel.BringToFront();
            _projectFolderPanel.BringToFront();

            // ----------------------------------------------------------
            // Step 6b: Splitter bars for resizable panels
            // ----------------------------------------------------------
            Color splitterColor = isDarkMode ? Color.FromArgb(38, 44, 58) : Color.FromArgb(210, 215, 225);
            Color splitterHover = isDarkMode ? Color.FromArgb(255, 127, 80) : Color.FromArgb(255, 127, 80);

            // -- Horizontal splitter: between main area (top) and chat/fileshare (bottom) --
            _splitterHoriz = new Panel
            {
                Location = new Point(0, chatTopY - 3),
                Size = new Size(this.ClientSize.Width - panelOnlineUsers.Width, 6),
                BackColor = splitterColor,
                Cursor = Cursors.HSplit,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            {
                bool dragging = false;
                int dragStartY = 0;
                int origChatTopY = 0;
                _splitterHoriz.MouseEnter += (s, e) => _splitterHoriz.BackColor = splitterHover;
                _splitterHoriz.MouseLeave += (s, e) => { if (!dragging) _splitterHoriz.BackColor = splitterColor; };
                _splitterHoriz.MouseDown += (s, e) => { dragging = true; dragStartY = e.Y; origChatTopY = _chatPanel.Top; };
                _splitterHoriz.MouseUp += (s, e) => { dragging = false; _splitterHoriz.BackColor = splitterColor; SaveCurrentPanelLayout(); };
                _splitterHoriz.MouseMove += (s, e) =>
                {
                    if (!dragging) return;
                    int delta = e.Y - dragStartY;
                    int newChatTop = origChatTopY + delta;
                    int minChatH = 120;  // min chat height
                    int maxChatTop = this.ClientSize.Height - statusStrip1.Height - minChatH;
                    int minChatTop = 300; // don't crush the main area
                    newChatTop = Math.Max(minChatTop, Math.Min(maxChatTop, newChatTop));

                    int newChatHeight = this.ClientSize.Height - statusStrip1.Height - newChatTop;
                    _bottomPanelHeight = newChatHeight;

                    _chatPanel.Location = new Point(_chatPanel.Left, newChatTop);
                    _chatPanel.Height = newChatHeight;
                    _fileSharePanel.Location = new Point(_fileSharePanel.Left, newChatTop);
                    _fileSharePanel.Height = newChatHeight;
                    _splitterHoriz.Top = newChatTop - 3;

                    // Resize sticker board height
                    _stickerBoard.Height = newChatTop;

                    // Keep datagrid bottom aligned with sticker board bottom edge
                    int gridBottom = newChatTop - 3;
                    dataGridView1.Height = gridBottom - dataGridView1.Top;

                    // Keep the bottom vertical splitter in sync
                    _splitterVertBottom.Top = newChatTop;
                    _splitterVertBottom.Height = newChatHeight;

                    ApplyMainColumnLayout();
                };
            }
            this.Controls.Add(_splitterHoriz);
            _splitterHoriz.BringToFront();

            // -- Vertical splitter: between chat and fileshare at the bottom --
            _splitterVertBottom = new Panel
            {
                Location = new Point(_chatPanel.Right, chatTopY),
                Size = new Size(6, _bottomPanelHeight),
                BackColor = splitterColor,
                Cursor = Cursors.VSplit,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            {
                bool dragging = false;
                int dragStartX = 0;
                int origChatW = 0;
                _splitterVertBottom.MouseEnter += (s, e) => _splitterVertBottom.BackColor = splitterHover;
                _splitterVertBottom.MouseLeave += (s, e) => { if (!dragging) _splitterVertBottom.BackColor = splitterColor; };
                _splitterVertBottom.MouseDown += (s, e) => { dragging = true; dragStartX = e.X; origChatW = _chatPanel.Width; };
                _splitterVertBottom.MouseUp += (s, e) => { dragging = false; _splitterVertBottom.BackColor = splitterColor; SaveCurrentPanelLayout(); };
                _splitterVertBottom.MouseMove += (s, e) =>
                {
                    if (!dragging) return;
                    int delta = e.X - dragStartX;
                    int totalW = this.ClientSize.Width - panelOnlineUsers.Width;
                    int newChatW = origChatW + delta;
                    int minChatW = 200;
                    int minFilesW = 200;
                    newChatW = Math.Max(minChatW, Math.Min(totalW - minFilesW, newChatW));
                    int newFilesW = totalW - newChatW;

                    _chatPanel.Width = newChatW;
                    _fileSharePanel.Location = new Point(newChatW, _fileSharePanel.Top);
                    _fileSharePanel.Width = newFilesW;
                    _splitterVertBottom.Left = _chatPanel.Right;
                    _splitterVertBottom.Top = _chatPanel.Top;
                    _splitterVertBottom.Height = _chatPanel.Height;
                };
            }
            this.Controls.Add(_splitterVertBottom);
            _splitterVertBottom.BringToFront();

            // ----------------------------------------------------------
            // Step 7: Toolbar strip ? thin panel across top of middle area
            // ----------------------------------------------------------
            _toolbarPanel = new Panel
            {
                Location = new Point(midLeft, ThemeConstants.SpaceS),
                Size = new Size(midRight - midLeft, ThemeConstants.ToolbarHeight - 2),
                BackColor = isDarkMode ? ThemeConstants.Dark.BgElevated : ThemeConstants.Light.BgElevated,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            // Draw subtle bottom border line for visual separation
            _toolbarPanel.Paint += (s, e) =>
            {
                ThemeConstants.DrawBottomBorder(e, _toolbarPanel.Width, _toolbarPanel.Height, isDarkMode);
            };
            this.Controls.Add(_toolbarPanel);
            _toolbarPanel.BringToFront();
            var toolbarPanel = _toolbarPanel; // local alias for convenience below

            // -- Toolbar buttons: refined sizing with better spacing --
            int tbX = ThemeConstants.SpaceS;
            int tbY = 4;
            int tbBtnW = 116;
            int tbBtnH = 32;
            int tbGap = 2;

            // Panel toggle buttons (Board, Chat, Team, Files, Calendar) moved to Settings dialog.
            // Panels initialize visible/hidden based on their _xxxVisible flags.

            int tbPillW = 102;         // long premium capsule shape without crowding right controls
            int tbPillH = tbBtnH;     // same height as before
            int tbPillR = tbPillH / 2; // fully round ends
            var normalIconClr = isDarkMode ? Color.FromArgb(170, 180, 195) : Color.FromArgb(80, 94, 116);
            var activeIconClr = isDarkMode ? ThemeConstants.Dark.AccentPrimary : ThemeConstants.Light.AccentPrimary;

            btnToggleHelper = CreateToggleButton("Wiki", tbX, tbY);
            btnToggleHelper.Name = "btnWiki";
            btnToggleHelper.Size = new Size(tbPillW, tbPillH);

            ThemeConstants.StyleToolbarTab(btnToggleHelper, _helperVisible, isDarkMode);
            btnToggleHelper.Image = ThemeConstants.RenderToolbarIcon("btnWiki", 20,
                _helperVisible ? activeIconClr : normalIconClr);
            ApplyRoundedCorners(btnToggleHelper, tbPillR);
            new ToolTip().SetToolTip(btnToggleHelper, "Wiki");
            btnToggleHelper.Click += (s, e) =>
            {
                _helperVisible = !_helperVisible;
                if (_helperVisible)
                {
                    ShowHelperWindow();
                }
                else
                {
                    HideHelperWindow();
                }
                UpdateToggleButtonStyle(btnToggleHelper, _helperVisible);
            };
            toolbarPanel.Controls.Add(btnToggleHelper);
            UpdateToggleButtonStyle(btnToggleHelper, _helperVisible);

            // Calendar, Board, Chat, Team, Files toggle buttons moved to Settings dialog.

            // -- Vertical divider between toggle and action groups --
            var tbDivider1 = new Panel
            {
                Location = new Point(tbX + 1 * (tbPillW + tbGap) - tbGap / 2, (ThemeConstants.ToolbarHeight - 20) / 2),
                Size = new Size(1, 20),
                BackColor = isDarkMode ? ThemeConstants.Dark.Border : ThemeConstants.Light.Border
            };
            toolbarPanel.Controls.Add(tbDivider1);

            // Ping button (position 1, after Wiki)
            var btnPing = new Button
            {
                Name = "btnPing",
                Text = "Ping",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(tbPillW, tbPillH),
                Location = new Point(tbX + 1 * (tbPillW + tbGap), tbY),
                Cursor = Cursors.Hand
            };
            ThemeConstants.StyleToolbarTab(btnPing, false, isDarkMode);
            btnPing.Image = ThemeConstants.RenderToolbarIcon("btnPing", 20, normalIconClr);
            ApplyRoundedCorners(btnPing, tbPillR);
            new ToolTip().SetToolTip(btnPing, "Ping");
            btnPing.Click += (s, e) => ShowPingDialog();
            toolbarPanel.Controls.Add(btnPing);

            // Notes/Standup button (position 2)
            var btnStandup = new Button
            {
                Name = "btnNotes",
                Text = "Notes",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(tbPillW, tbPillH),
                Location = new Point(tbX + 2 * (tbPillW + tbGap), tbY),
                Cursor = Cursors.Hand
            };
            ThemeConstants.StyleToolbarTab(btnStandup, false, isDarkMode);
            btnStandup.Image = ThemeConstants.RenderToolbarIcon("btnNotes", 20, normalIconClr);
            ApplyRoundedCorners(btnStandup, tbPillR);
            new ToolTip().SetToolTip(btnStandup, "Notes");
            btnStandup.Click += (s, e) => ShowStandupSummary();
            toolbarPanel.Controls.Add(btnStandup);

            // Quick Task button (position 3)
            var btnQuickTask = new Button
            {
                Name = "btnQuick",
                Text = "Quick",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(tbPillW, tbPillH),
                Location = new Point(tbX + 3 * (tbPillW + tbGap), tbY),
                Cursor = Cursors.Hand
            };
            ThemeConstants.StyleToolbarTab(btnQuickTask, false, isDarkMode);
            btnQuickTask.Image = ThemeConstants.RenderToolbarIcon("btnQuick", 20, normalIconClr);
            ApplyRoundedCorners(btnQuickTask, tbPillR);
            new ToolTip().SetToolTip(btnQuickTask, "Quick Task");
            btnQuickTask.Click += (s, e) => ShowQuickTaskMenu(btnQuickTask);
            toolbarPanel.Controls.Add(btnQuickTask);

            // Timer/Pomodoro button (position 4)
            var btnPomodoro = new Button
            {
                Name = "btnTimer",
                Text = "Timer",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(tbPillW, tbPillH),
                Location = new Point(tbX + 4 * (tbPillW + tbGap), tbY),
                Cursor = Cursors.Hand
            };
            ThemeConstants.StyleToolbarTab(btnPomodoro, false, isDarkMode);
            btnPomodoro.Image = ThemeConstants.RenderToolbarIcon("btnTimer", 20, normalIconClr);
            ApplyRoundedCorners(btnPomodoro, tbPillR);
            new ToolTip().SetToolTip(btnPomodoro, "Timer");
            btnPomodoro.Click += (s, e) => {
                TogglePomodoroPanel();
            };
            toolbarPanel.Controls.Add(btnPomodoro);

            // -- Vertical divider before right-side buttons --
            var tbDivider2 = new Panel
            {
                Size = new Size(1, 20),
                BackColor = isDarkMode ? ThemeConstants.Dark.Border : ThemeConstants.Light.Border,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            // Switch Team button - pill shape, opens team switcher
            var btnSwitchTeam = new Button
            {
                Name = "btnSwitchTeam",
                Text = "Teams",
                FlatStyle = FlatStyle.Flat,
                Size = new Size(tbPillW, tbPillH),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = true
            };
            ThemeConstants.StyleToolbarTab(btnSwitchTeam, false, isDarkMode);
            btnSwitchTeam.Image = ThemeConstants.RenderToolbarIcon("btnSwitchTeam", 20, normalIconClr);
            ApplyRoundedCorners(btnSwitchTeam, tbPillR);
            new ToolTip().SetToolTip(btnSwitchTeam, "Teams");

            btnSwitchTeam.Click += (s, e) => {
                OnSwitchTeamClicked();
            };
            toolbarPanel.Controls.Add(tbDivider2);
            toolbarPanel.Controls.Add(btnSwitchTeam);
//             DebugLogger.Log("[Form1] Team switcher button added to toolbar");

            // ? Settings button ? ALWAYS VISIBLE for all users
            // Non-admin users can still view team info, change password, etc.
            // Admin-only actions are restricted inside ShowTeamOptions()
            btnSettings = new Button
            {
                Text = "",
                FlatStyle = FlatStyle.Flat,
                Font = ThemeConstants.FontBodyBold,
                Size = new Size(40, tbBtnH),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = true
            };
            btnSettings.Image = ThemeConstants.CreateGearIcon(20,
                isDarkMode ? Color.FromArgb(170, 180, 195) : Color.FromArgb(80, 94, 116));
            btnSettings.ImageAlign = ContentAlignment.MiddleCenter;
            ThemeConstants.StyleToolbarIconButton(btnSettings, isDarkMode);
            ApplyRoundedCorners(btnSettings, ThemeConstants.RadiusMedium + 2);
            // Place at right edge of toolbar
            btnSettings.Location = new Point(toolbarPanel.Width - btnSettings.Width - ThemeConstants.SpaceS, tbY);
            btnSwitchTeam.Location = new Point(
                Math.Max(btnPomodoro.Right + ThemeConstants.SpaceM, btnSettings.Left - ThemeConstants.SpaceM - btnSwitchTeam.Width),
                tbY);
            tbDivider2.Location = new Point(btnSwitchTeam.Left - ThemeConstants.SpaceS, (ThemeConstants.ToolbarHeight - 20) / 2);
            // Wire up settings click event
            btnSettings.Click += (s, e) => {
//                 DebugLogger.Log("[Form1] Settings button clicked");
                ShowTeamOptions();
            };
            toolbarPanel.Controls.Add(btnSettings);
//             DebugLogger.Log("[Form1] Settings button added to toolbar");

            this.ResumeLayout(true);
//             DebugLogger.Log("[Form1] Layout resumed");

            // ----------------------------------------------------------
            // Step 7b: RESTORE SAVED PANEL LAYOUT (if exists)
            // Loads user's previous panel positions/sizes from disk
            // ----------------------------------------------------------
//             DebugLogger.Log("[Form1] Restoring saved panel layout");
            ApplySavedPanelLayout();
            ApplyMainColumnLayout();

            // Make panels draggable/resizable (except work counter which stays fixed)
            // Drag top 24px of panel to move, drag bottom-right corner to resize
            PanelLayoutManager.MakeDraggableAndResizable(_stickerBoard, SaveCurrentPanelLayout);
            PanelLayoutManager.MakeDraggableAndResizable(_chatPanel, SaveCurrentPanelLayout);
            PanelLayoutManager.MakeDraggableAndResizable(_fileSharePanel, SaveCurrentPanelLayout);
            // Keep team status fixed on the right; do not allow drag/resize persistence.

            // ----------------------------------------------------------
            // Step 8: Auto-refresh + initial load
            // ----------------------------------------------------------
            _panelRefreshTimer = new Timer();
            _panelRefreshTimer.Interval = 15000;
            // Panel auto-refresh timer - ticks every 15 seconds
            _panelRefreshTimer.Tick += async (s, e) =>
            {
//                 DebugLogger.Log("[Form1] Timer tick - starting panel refresh cycle");
                await _stickerBoard.RefreshAsync();
                await _chatPanel.RefreshAsync();
                await _fileSharePanel.RefreshAsync();
                if (_helperVisible) await _helperPanel.RefreshAsync();
            };
            _panelRefreshTimer.Start();
//             DebugLogger.Log("[Form1] Panel refresh timer started (15s ? updates stickers, chat, files)");

            // Apply theme FIRST (before data loads) so colors are correct
            // ApplyTheme won't wipe stickers ? it only re-renders if data exists
            _stickerBoard.ApplyTheme(isDarkMode, _customTheme);
            _chatPanel.ApplyTheme(isDarkMode, _customTheme);
            _fileSharePanel.ApplyTheme(isDarkMode, _customTheme);
            _helperPanel.ApplyTheme(isDarkMode, _customTheme);

            // THEN start loading data from Firebase
            _ = _stickerBoard.RefreshAsync();
            _ = _chatPanel.RefreshAsync();
            _ = _fileSharePanel.RefreshAsync();
            _ = _helperPanel.RefreshAsync();
        }

        private void UpdateActiveTeamBadge(Point location, int maxRight)
        {
            if (_activeTeamBadge == null)
            {
                _activeTeamBadge = new Panel
                {
                    Height = 68,
                    Visible = true
                };
                _activeTeamBadge.Paint += (s, e) =>
                {
                    var panel = s as Panel;
                    if (panel == null) return;

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, panel.Width - 1, panel.Height - 1);
                    using (var path = ThemeConstants.RoundedRect(rect, ThemeConstants.RadiusLarge))
                    using (var bgBrush = new SolidBrush(isDarkMode ? Color.FromArgb(52, 35, 29) : Color.FromArgb(255, 242, 232)))
                    using (var borderPen = new Pen(isDarkMode ? ThemeConstants.Dark.AccentMuted : ThemeConstants.Light.AccentMuted))
                    {
                        e.Graphics.FillPath(bgBrush, path);
                        e.Graphics.DrawPath(borderPen, path);
                    }
                };
                this.Controls.Add(_activeTeamBadge);
            }

            if (_labelActiveTeamTitle == null)
            {
                _labelActiveTeamTitle = new Label
                {
                    AutoSize = false,
                    TextAlign = ContentAlignment.BottomCenter,
                    Font = ThemeConstants.FontSmallBold,
                    BackColor = Color.Transparent
                };
                _activeTeamBadge.Controls.Add(_labelActiveTeamTitle);
            }

            if (_labelActiveTeam == null)
            {
                _labelActiveTeam = new Label
                {
                    AutoSize = false,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.TopCenter,
                    Font = new Font(ThemeConstants.FontFamily, 14.5f, FontStyle.Bold),
                    BackColor = Color.Transparent
                };
                _activeTeamBadge.Controls.Add(_labelActiveTeam);
            }

            var teamInfo = UserStorage.LoadTeam();
            string teamName = !string.IsNullOrWhiteSpace(teamInfo?.TeamName)
                ? teamInfo.TeamName.Trim()
                : (!string.IsNullOrWhiteSpace(teamInfo?.JoinCode) ? teamInfo.JoinCode.Trim() : "No Team");

            Color accentColor = isDarkMode ? ThemeConstants.Dark.AccentPrimary : ThemeConstants.Light.AccentPrimary;
            _labelActiveTeamTitle.Text = "Team:";
            _labelActiveTeamTitle.ForeColor = ControlPaint.Light(accentColor, 0.15f);
            _labelActiveTeam.Text = teamName;
            _labelActiveTeam.ForeColor = accentColor;

            int titleWidth = TextRenderer.MeasureText(_labelActiveTeamTitle.Text, _labelActiveTeamTitle.Font).Width;
            int nameWidth = TextRenderer.MeasureText(_labelActiveTeam.Text, _labelActiveTeam.Font).Width;
            int desiredWidth = Math.Max(186, Math.Max(titleWidth, nameWidth) + 34);
            int availableWidth = Math.Max(210, maxRight - location.X);
            int badgeWidth = Math.Min(desiredWidth, availableWidth);
            int badgeX = location.X;
            if (badgeX + badgeWidth > maxRight)
                badgeX = Math.Max(location.X, maxRight - badgeWidth);

            _activeTeamBadge.Size = new Size(badgeWidth, 68);
            _activeTeamBadge.Location = new Point(badgeX, location.Y);
            _labelActiveTeamTitle.Bounds = new System.Drawing.Rectangle(12, 10, _activeTeamBadge.Width - 24, 18);
            _labelActiveTeam.Bounds = new System.Drawing.Rectangle(12, 28, _activeTeamBadge.Width - 24, 32);

            _activeTeamBadge.BringToFront();
            _labelActiveTeamTitle.BringToFront();
            _labelActiveTeam.BringToFront();
            _activeTeamBadge.Invalidate();
        }

        // --- PANEL LAYOUT PERSISTENCE ? SAVE/LOAD/APPLY ---

        /// <summary>Save current positions of all movable panels to disk.</summary>
        private void SaveCurrentPanelLayout()
        {
            try
            {
                var config = new LayoutConfig
                {
                    FormWidth = this.Width,
                    FormHeight = this.Height
                };

                if (_stickerBoard != null)
                    config.Panels["StickerBoard"] = PanelLayoutManager.CapturePanel(_stickerBoard);
                if (_chatPanel != null)
                    config.Panels["Chat"] = PanelLayoutManager.CapturePanel(_chatPanel);
                if (_fileSharePanel != null)
                    config.Panels["FileShare"] = PanelLayoutManager.CapturePanel(_fileSharePanel);
                if (panelOnlineUsers != null)
                    config.Panels["OnlineUsers"] = PanelLayoutManager.CapturePanel(panelOnlineUsers);

                // Save splitter positions
                if (_splitterHoriz != null)
                    config.HorizSplitterY = _splitterHoriz.Top;
                if (_splitterVertBottom != null)
                    config.VertSplitterX = _splitterVertBottom.Left;

                PanelLayoutManager.SaveLayout(config);
            }
            catch { }
        }

        /// <summary>Apply saved panel layout from disk. If no saved layout, panels stay at default positions.</summary>
        private void ApplySavedPanelLayout()
        {
            var config = PanelLayoutManager.LoadLayout();
            if (config == null)
            {
                if (_bottomPanelHeight <= 0)
                    _bottomPanelHeight = GetDefaultBottomPanelHeight();
                return;
            }

            try
            {
                // Form size is restored via TryRestoreMainWindowState().
                // Do not restore from panel layout to avoid legacy "extra wiki width" geometry.

                // Restore panel positions (skip visibility for OnlineUsers ? controlled by toggle)
                if (config.Panels.ContainsKey("StickerBoard") && _stickerBoard != null)
                {
                    var d = config.Panels["StickerBoard"];
                    _stickerBoard.Location = new Point(d.X, d.Y);
                    _stickerBoard.Size = new Size(d.Width, d.Height);
                }

                if (config.Panels.ContainsKey("Chat") && _chatPanel != null)
                {
                    var d = config.Panels["Chat"];
                    _chatPanel.Location = new Point(d.X, d.Y);
                    _chatPanel.Size = new Size(d.Width, d.Height);
                    _chatPanel.Anchor = AnchorStyles.None;
                    if (d.Height > 0)
                        _bottomPanelHeight = d.Height;
                }

                if (config.Panels.ContainsKey("FileShare") && _fileSharePanel != null)
                {
                    var d = config.Panels["FileShare"];
                    _fileSharePanel.Location = new Point(d.X, d.Y);
                    _fileSharePanel.Size = new Size(d.Width, d.Height);
                    _fileSharePanel.Anchor = AnchorStyles.None;
                }

                // Never restore online users panel as free-floating;
                // it must stay docked right in the new layout model.

                // Restore splitter positions
                if (config.HorizSplitterY > 0 && _splitterHoriz != null)
                {
                    _splitterHoriz.Top = config.HorizSplitterY;
                    int restoredBottom = this.ClientSize.Height - statusStrip1.Height - config.HorizSplitterY;
                    int minBottomHeight = 120;
                    int maxBottomHeight = Math.Max(minBottomHeight, this.ClientSize.Height - statusStrip1.Height - 260);
                    _bottomPanelHeight = Math.Max(minBottomHeight, Math.Min(maxBottomHeight, restoredBottom));
                }
                if (config.VertSplitterX > 0 && _splitterVertBottom != null)
                {
                    _splitterVertBottom.Left = config.VertSplitterX;
                    if (_chatPanel != null)
                    {
                        int leftBottomWidth = Math.Max(420, this.ClientSize.Width - RIGHT_COLUMN_WIDTH);
                        int minChatW = 220;
                        int minFilesW = 220;
                        int restoredChatW = Math.Max(minChatW, Math.Min(leftBottomWidth - minFilesW, config.VertSplitterX));
                        _chatPanel.Width = restoredChatW;
                    }
                }
            }
            catch { }
            finally
            {
                ApplyMainColumnLayout();
            }
        }

        private Button CreateToggleButton(string text, int x, int y)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                Font = ThemeConstants.FontSmallBold,
                Size = new Size(86, 34),
                Location = new Point(x, y),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            // Hover styling handled by UpdateToggleButtonStyle / ThemeConstants.StyleToggleButton
            return btn;
        }

        private void UpdateToggleButtonStyle(Button btn, bool isActive)
        {
            ThemeConstants.StyleToggleButton(btn, isActive, isDarkMode);
            // Re-render icon with correct color for active/inactive state
            var iconClr = isActive
                ? (isDarkMode ? ThemeConstants.Dark.AccentPrimary : ThemeConstants.Light.AccentPrimary)
                : (isDarkMode ? Color.FromArgb(170, 180, 195) : Color.FromArgb(80, 94, 116));
            btn.Image = ThemeConstants.RenderToolbarIcon(btn.Name, 18, iconClr);
        }

        // ============================================================
        // STARTUP HELPER: SEND HEARTBEAT THEN REFRESH (SEQUENTIAL)
        // Ensures current user's "Online" signal is in Firebase
        // BEFORE the first status refresh reads it
        // ============================================================
        private async Task SendHeartbeatThenRefreshAsync()
        {
            try
            {
                await SendOnlineHeartbeatAsync();
                await RefreshOnlineStatusAsync();
            }
            catch { }
        }

        // ============================================================
        // REFRESH ONLINE STATUS
        // ============================================================
        private async Task SendOnlineHeartbeatAsync()
        {
            try
            {
                var data = new
                {
                    description = "",
                    startTime = "",
                    workingTime = "",
                    timestamp = DateTime.UtcNow.ToString("o"),
                    status = "Online",
                    userId = GetCurrentUserId(),
                    userName = _currentUser.Name,
                    platform = "desktop"
                };

                var response = await _httpClient.GetAsync(firebaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json) && json != "null")
                    {
//                         DebugLogger.Log("[Form1] Deserializing Firebase logs from JSON");
                var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json);
                        var existing = logsDict.FirstOrDefault(l =>
                            l.Value.userId == GetCurrentUserId() &&
                            l.Value.status == "Online");

                        if (existing.Key != null)
                        {
                            string updateUrl = firebaseUrl.Replace(".json", $"/{existing.Key}.json");
                            var patch = new { timestamp = DateTime.UtcNow.ToString("o") };
                            var request = new HttpRequestMessage(new HttpMethod("PATCH"), updateUrl)
                            {
                                Content = new StringContent(
                                    JsonConvert.SerializeObject(patch), Encoding.UTF8, "application/json")
                            };
                            await _httpClient.SendAsync(request);
                            return;
                        }
                    }
                }

                var content = new StringContent(
                    JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(firebaseUrl, content);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox($"[ERROR] Heartbeat failed: {ex.Message}\r\n");
            }
        }

        /// <summary>
        /// Sends online heartbeat to ALL joined teams (not just the active one).
        /// This ensures the user appears online across all projects they've joined.
        /// </summary>
        private async Task SendHeartbeatToAllTeamsAsync()
        {
            try
            {
                var joinedTeams = UserStorage.GetJoinedTeams();
                string activeCode = UserStorage.GetActiveTeamCode();

                foreach (var teamEntry in joinedTeams)
                {
                    // Skip the active team ? it's already handled by SendOnlineHeartbeatAsync
                    if (teamEntry.JoinCode.Equals(activeCode, StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        string teamLogsUrl = UserStorage.GetFirebaseBaseUrlForTeam(teamEntry.JoinCode) + "/logs.json";

                        // Get active team name to show in other teams
                        string activeTeamName = "";
                        var activeEntry = joinedTeams.FirstOrDefault(t =>
                            t.JoinCode.Equals(activeCode, StringComparison.OrdinalIgnoreCase));
                        if (activeEntry != null)
                            activeTeamName = activeEntry.TeamName ?? "";

                        var data = new
                        {
                            description = !string.IsNullOrEmpty(activeTeamName)
                                ? $"  (in {activeTeamName})"
                                : "  (in another project)",
                            startTime = "",
                            workingTime = "",
                            timestamp = DateTime.UtcNow.ToString("o"),
                            status = "Online",
                            userId = _currentUser.Name,
                            userName = _currentUser.Name,
                            platform = "desktop"
                        };

                        // Check for existing heartbeat
                        var response = await _httpClient.GetAsync(teamLogsUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            if (!string.IsNullOrWhiteSpace(json) && json != "null")
                            {
//                                 DebugLogger.Log("[Form1] Deserializing Firebase logs from JSON");
                var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json);
                                if (logsDict != null)
                                {
                                    var existing = logsDict.FirstOrDefault(l =>
                                        string.Equals(l.Value.userId, _currentUser.Name, StringComparison.OrdinalIgnoreCase) &&
                                        l.Value.status == "Online");

                                    if (existing.Key != null)
                                    {
                                        string updateUrl = teamLogsUrl.Replace(".json", $"/{existing.Key}.json");
                                        // Update both timestamp and description (shows which project the user is in)
                                        var patch = new
                                        {
                                            timestamp = DateTime.UtcNow.ToString("o"),
                                            description = data.description
                                        };
                                        var req = new HttpRequestMessage(new HttpMethod("PATCH"), updateUrl)
                                        {
                                            Content = new StringContent(
                                                JsonConvert.SerializeObject(patch), Encoding.UTF8, "application/json")
                                        };
                                        await _httpClient.SendAsync(req);
                                        continue;
                                    }
                                }
                            }
                        }

                        // No existing entry ? create new
                        var content = new StringContent(
                            JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
                        await _httpClient.PostAsync(teamLogsUrl, content);
                    }
                    catch { /* Silently skip teams with errors */ }
                }
            }
            catch { /* Don't break the main timer */ }
        }

        // --- ONLINE STATUS ? HEARTBEAT AND REFRESH FROM FIREBASE ---
        // --------------------------------------------------------------
        // THIS METHOD NOW ALSO SYNCS THE TEAM MEMBER LIST FROM FIREBASE
        // SO NEW MEMBERS JOINING FROM OTHER COMPUTERS APPEAR AUTOMATICALLY
        // --------------------------------------------------------------
        private async Task RefreshOnlineStatusAsync()
        {
            try
            {
                // -- SYNC TEAM MEMBERS FROM FIREBASE ON EVERY REFRESH --
                // Detect new members who joined via invite code immediately
                await SyncTeamMembersFromFirebaseAsync();

                var response = await _httpClient.GetAsync(firebaseUrl);
                if (!response.IsSuccessStatusCode) return;

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    foreach (var ctrl in onlineUserControls)
                        ctrl.SetStatus("Offline", "");
                    return;
                }

//                 DebugLogger.Log("[Form1] Deserializing Firebase logs from JSON");
                var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json);

                // -- CHECK FOR UNKNOWN USERS IN LOGS ? IMMEDIATE SYNC --
                // If a new user just joined and is pinging, trigger rebuild right away
                var knownNames = new HashSet<string>(onlineUserControls.Select(c => c.UserInfo.Name), StringComparer.OrdinalIgnoreCase);

                bool foundNewUser = false;
                foreach (var log in logsDict.Values)
                {
                    if (log.status == "Online" || log.status == "Working")
                    {
                        // CHECK userName, userId, AND stripped versions ? if ANY matches, it's not new
                        bool isKnown = false;
                        if (!string.IsNullOrEmpty(log.userName) && knownNames.Contains(log.userName))
                            isKnown = true;
                        if (!string.IsNullOrEmpty(log.userId) && knownNames.Contains(log.userId))
                            isKnown = true;

                        // Also check after stripping prefix from mobile names
                        // Handles "6J82GG_Blagoy", "6J82GG Blagoy", etc.
                        if (!isKnown)
                        {
                            // Try StripJoinCodePrefix first
                            if (!string.IsNullOrEmpty(log.userId) && knownNames.Contains(StripJoinCodePrefix(log.userId)))
                                isKnown = true;
                            if (!string.IsNullOrEmpty(log.userName) && knownNames.Contains(StripJoinCodePrefix(log.userName)))
                                isKnown = true;

                            // Fallback: check if name contains separator + known user name
                            if (!isKnown)
                            {
                                string checkName = log.userId ?? log.userName ?? "";
                                if (IsMobileDuplicate(checkName, knownNames))
                                    isKnown = true;
                            }
                        }

                        if (!isKnown)
                        {
                            string logUser = !string.IsNullOrEmpty(log.userName) ? log.userName : log.userId;
                            if (!string.IsNullOrEmpty(logUser))
                            {
                                foundNewUser = true;
                                break;
                            }
                        }
                    }
                }
//                 DebugLogger.Log("[Form1] Branch: foundNewUser = true - new team member detected");
            if (foundNewUser)
                {
                    await SyncTeamMembersFromFirebaseAsync();
                    return; // RebuildOnlineUsersPanel was called, refresh will pick up on next cycle
                }

                foreach (var ctrl in onlineUserControls)
                {
                    // MATCH BY userId OR userName ? CASE-INSENSITIVE!
                    // MOBILE APPS MAY SEND "blagoy" WHILE DESKTOP SENDS "Blagoy"
                    string uName = ctrl.UserInfo.Name;

                    // FIND ALL LOGS FOR THIS USER (CASE-INSENSITIVE MATCH)
                    // Also match mobile names like "6J82GG_Blagoy" to user "Blagoy"
                    var userLogs = logsDict.Values.Where(log =>
                    {
                        // Direct match
                        if (string.Equals(log.userId, uName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(log.userName, uName, StringComparison.OrdinalIgnoreCase))
                            return true;

                        // Match after stripping JoinCode prefix
                        if (!string.IsNullOrEmpty(log.userId) &&
                            string.Equals(StripJoinCodePrefix(log.userId), uName, StringComparison.OrdinalIgnoreCase))
                            return true;
                        if (!string.IsNullOrEmpty(log.userName) &&
                            string.Equals(StripJoinCodePrefix(log.userName), uName, StringComparison.OrdinalIgnoreCase))
                            return true;

                        // Fallback: check if userId/userName contains separator + uName
                        // e.g. "6J82GG_Blagoy" contains "_Blagoy" ? matches "Blagoy"
                        char[] seps = { '_', ' ', '-', '.' };
                        string uid = log.userId ?? "";
                        string uname = log.userName ?? "";
                        foreach (char sep in seps)
                        {
                            if (uid.IndexOf(sep) > 0)
                            {
                                string after = uid.Substring(uid.IndexOf(sep) + 1);
                                if (string.Equals(after, uName, StringComparison.OrdinalIgnoreCase))
                                    return true;
                            }
                            if (uname.IndexOf(sep) > 0)
                            {
                                string after = uname.Substring(uname.IndexOf(sep) + 1);
                                if (string.Equals(after, uName, StringComparison.OrdinalIgnoreCase))
                                    return true;
                            }
                        }

                        return false;
                    });

                    var workingLog = userLogs.FirstOrDefault(log => log.status == "Working");
                    var onlineLog = userLogs.FirstOrDefault(log => log.status == "Online");

                    // COLLECT ALL PLATFORMS THIS USER IS ON (DESKTOP + ANDROID SIMULTANEOUSLY)
                    var platforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool hasJoinCodePrefixLog = false; // Track if any log came via "JoinCode_Name"
                    foreach (var log in userLogs.Where(l => l.status == "Online" || l.status == "Working"))
                    {
                        if (!string.IsNullOrEmpty(log.platform))
                            platforms.Add(log.platform.ToLower());

                        // Detect if this log matched via JoinCode prefix (= mobile app)
                        string lid = log.userId ?? "";
                        string lun = log.userName ?? "";
                        if ((lid.Contains("_") && string.Equals(lid.Substring(lid.IndexOf('_') + 1), uName, StringComparison.OrdinalIgnoreCase)) ||
                            (lun.Contains("_") && string.Equals(lun.Substring(lun.IndexOf('_') + 1), uName, StringComparison.OrdinalIgnoreCase)))
                        {
                            hasJoinCodePrefixLog = true;
                            if (!platforms.Contains("android"))
                                platforms.Add("android"); // Infer android if joined with invite code prefix
                        }
                    }

                    // BUILD DEVICE ICONS ? SHOW ALL CONNECTED DEVICES
                    string deviceIcons = "";
                    if (platforms.Contains("desktop")) deviceIcons += " \U0001f5a5";
                    if (platforms.Contains("android") || hasJoinCodePrefixLog) deviceIcons += " \U0001f4f1 ANDROID";
                    if (platforms.Contains("ios")) deviceIcons += " \U0001f34f";

                    string newStatus;
                    if (workingLog != null)
                    {
                        newStatus = "Working";
                        string desc = workingLog.description ?? "";
                        ctrl.SetStatus("Working", desc + deviceIcons);
                    }
                    else if (onlineLog != null)
                    {
                        newStatus = "Online";
                        // Include description from log (e.g., "(in ProjectAlpha)" for cross-team users)
                        string onlineDesc = onlineLog.description ?? "";
                        ctrl.SetStatus("Online", onlineDesc + deviceIcons);
                    }
                    else
                    {
                        newStatus = "Offline";
                        ctrl.SetStatus("Offline", "");
                    }

                    // Play sound on status change (skip self)
                    string userName = ctrl.UserInfo.Name;
                    if (userName != _currentUser.Name)
                    {
                        string prevStatus;
                        _previousUserStatuses.TryGetValue(userName, out prevStatus);
                        if (prevStatus != null && prevStatus != newStatus)
                        {
                            if (newStatus == "Online" || newStatus == "Working")
                            {
//                                 DebugLogger.Log($"[Form1] Playing sound: user {userName} came online");
                                SoundManager.PlayUserOnline();
                            }
                            else if (newStatus == "Offline")
                            {
//                                 DebugLogger.Log($"[Form1] Playing sound: user {userName} went offline");
                                SoundManager.PlayUserOffline();
                            }
                        }
                    }

                    _previousUserStatuses[userName] = newStatus;
                }

                // -- CROSS-DEVICE SYNC: Phone started Working ? Desktop reflects it --
                // If a mobile "Working" log exists for this user but desktop timer is NOT running,
                // auto-recover: show Working state on desktop UI so user sees it's synced
                if (_workingTimer == null || !_workingTimer.Enabled)
                {
                    var mobileWorkingLog = logsDict.Values.FirstOrDefault(log =>
                        IsCurrentUserLog(log) &&
                        !string.IsNullOrEmpty(log.status) &&
                        log.status.Equals("Working", StringComparison.OrdinalIgnoreCase));

//                     DebugLogger.Log("[Form1] Branch: mobile working log detected - syncing to desktop");
            if (mobileWorkingLog != null)
                    {
                        // Phone is working ? recover the key and start desktop timer
                        string mobileKey = logsDict.FirstOrDefault(kv => kv.Value == mobileWorkingLog).Key;
                        if (!string.IsNullOrEmpty(mobileKey) && currentLiveLogKey != mobileKey)
                        {
//                             DebugLogger.Log($"[Form1] currentLiveLogKey set to: {mobileKey}");
            currentLiveLogKey = mobileKey;

                            // Calculate elapsed time from mobile log's timestamp
                            DateTime mobileStart;
                            if (DateTime.TryParse(mobileWorkingLog.timestamp, null,
                                System.Globalization.DateTimeStyles.RoundtripKind, out mobileStart))
                            {
                                _startTime = mobileStart.ToLocalTime();
                                _elapsedTime = DateTime.Now - _startTime;
//             DebugLogger.Log($"[Form1] Time calculation: elapsed = {_elapsedTime:hh\\:mm\\:ss}");
                                if (_elapsedTime < TimeSpan.Zero) _elapsedTime = TimeSpan.Zero;
                            }
                            else
                            {
                                _startTime = DateTime.Now;
//             DebugLogger.Log($"[Form1] Timer started at {_startTime:HH:mm:ss}");
                                _elapsedTime = TimeSpan.Zero;
                            }

                            labelStartTime.Text = _startTime.ToString("HH:mm:ss");
                            labelWorkingTime.Text = _elapsedTime.ToString(@"hh\:mm\:ss");

                            // Start the desktop timer
                            if (_workingTimer == null)
                            {
                                _workingTimer = new Timer();
                                _workingTimer.Interval = 1000;
                                _workingTimer.Tick += WorkingTimer_Tick;
                            }
//                             DebugLogger.Log("[Form1] Timer started - work session begin");
                _workingTimer.Start();
                            _workStartedAt = _startTime;

                            // Show Working button state
//                             DebugLogger.Log("[Form1] Setting start button to Working state");
            SetStartButtonWorking(true);
                            _sessionLimitSnapshotReady = false;
                            _continuous2hWarned = _elapsedTime.TotalHours >= 2;
                            _continuous4hWarned = _elapsedTime.TotalHours >= 4;
                            _weeklyLimitWarnedInSession = false;
                            _dailyLimitReachedInSession = false;
                            _dailyLimitOverrideAccepted = false;
                            UpdateMainClockVisualState();

                            UpdateRichTextBox($"Mobile Working detected ? desktop synced! Key: {mobileKey}\r\n");
                        }
                    }
                }

                // -- CROSS-DEVICE SYNC: Phone stopped ? Desktop auto-stops --
                // If desktop timer IS running but NO Working log exists for this user anymore,
                // the phone (or another device) stopped it ? auto-stop desktop too
                if (_workingTimer != null && _workingTimer.Enabled)
                {
                    bool anyWorkingLogExists = logsDict.Values.Any(log =>
                        IsCurrentUserLog(log) &&
                        !string.IsNullOrEmpty(log.status) &&
                        log.status.Equals("Working", StringComparison.OrdinalIgnoreCase));

//                     DebugLogger.Log("[Form1] Branch: no working logs found - auto-stopping timer");
            if (!anyWorkingLogExists)
                    {
//                         DebugLogger.Log("[Form1] Timer stopped - work session ended");
                _workingTimer.Stop();
//                         DebugLogger.Log("[Form1] Setting start button to Stopped state");
            SetStartButtonWorking(false);
                        _sessionLimitSnapshotReady = false;
                        _continuous2hWarned = false;
                        _continuous4hWarned = false;
                        _weeklyLimitWarnedInSession = false;
                        _dailyLimitReachedInSession = false;
                        _dailyLimitOverrideAccepted = false;
                        UpdateMainClockVisualState();
//                         DebugLogger.Log("[Form1] currentLiveLogKey cleared (auto-stop)");
                        currentLiveLogKey = null;
                        UpdateRichTextBox("Mobile session stopped ? desktop auto-stopped.\r\n");
                    }
                }

                // Check for incoming ping alerts
                await CheckForPingAlertAsync(logsDict);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox($"[ERROR] Online status check failed: {ex.Message}\r\n");
            }
        }

        // --------------------------------------------------------------
        // SYNC TEAM MEMBERS FROM FIREBASE
        // IF NEW MEMBERS FOUND, REBUILD THE ONLINE USERS PANEL
        // SO EVERYONE CAN SEE ALL TEAM MEMBERS IN REAL-TIME
        // --------------------------------------------------------------
        private async Task SyncTeamMembersFromFirebaseAsync()
        {
            try
            {
//                 DebugLogger.Log("[Form1] Syncing team members from Firebase");
                var localTeam = UserStorage.LoadTeam();
                bool needsRebuild = false;
                // CASE-INSENSITIVE SET ? "Blagoy" and "blagoy" are the SAME user
                var localNames = new HashSet<string>(_allUsers.Select(u => u.Name), StringComparer.OrdinalIgnoreCase);

                // -- METHOD 1: SYNC FROM FIREBASE TEAM OBJECT --
                if (localTeam != null && !string.IsNullOrEmpty(localTeam.JoinCode))
                {
                    var remoteTeam = await UserStorage.FindTeamByJoinCodeAsync(localTeam.JoinCode);
                    if (remoteTeam != null && remoteTeam.Members != null)
                    {
                        // -- STRIP JOINCODE PREFIX FROM MOBILE-ADDED MEMBERS --
                        // Mobile app may add members as "6J82GG_Blagoy" or "6J82GG Blagoy"
                        // Detect and skip these if "Blagoy" already exists as a member
                        var cleanedRemoteMembers = new List<string>();
                        bool remoteListChanged = false;

                        // Build set of "real" names (non-prefixed) from remote + local
                        var allKnownReal = new HashSet<string>(localNames, StringComparer.OrdinalIgnoreCase);
                        foreach (string m in remoteTeam.Members)
                        {
                            string s = StripJoinCodePrefix(m);
                            if (string.Equals(s, m, StringComparison.OrdinalIgnoreCase))
                                allKnownReal.Add(m); // Not prefixed ? it's a real name
                        }

                        foreach (string name in remoteTeam.Members)
                        {
                            // Check if this name is a JoinCode-prefixed duplicate
                            if (IsJoinCodeDuplicate(name, allKnownReal))
                            {
                                remoteListChanged = true;
                                continue; // Skip ? "6J82GG_Blagoy" is duplicate of "Blagoy"
                            }
                            cleanedRemoteMembers.Add(name);
                        }

                        foreach (string name in cleanedRemoteMembers)
                        {
                            if (!localNames.Contains(name))
                            {
                                // -- BLOCK MOBILE DUPLICATES AT SOURCE --
                                // If name contains "_" or " " and the part after it matches
                                // an existing user, do NOT add it (e.g. "6J82GG_Blagoy" when "Blagoy" exists)
                                bool isDup = false;
                                if (name.Contains("_"))
                                {
                                    string after = name.Substring(name.IndexOf('_') + 1);
                                    if (after.Length > 0 && localNames.Contains(after)) isDup = true;
                                }
                                if (!isDup && name.Contains(" "))
                                {
                                    string after = name.Substring(name.IndexOf(' ') + 1);
                                    if (after.Length > 0 && localNames.Contains(after)) isDup = true;
                                }
                                if (isDup) continue; // Skip ? mobile duplicate

//                                 DebugLogger.Log($"[Form1] Added new user to list: {name}");
            _allUsers.Add(new UserInfo(name, false));
                                localNames.Add(name);
                                needsRebuild = true;
                            }
                        }

                        // UPDATE LOCAL TEAM DATA (use cleaned list without prefixed duplicates)
                        if (remoteListChanged)
                            remoteTeam.Members = cleanedRemoteMembers;
                        localTeam.Members = remoteTeam.Members;
                        if (remoteTeam.MembersMeta != null)
                            localTeam.MembersMeta = remoteTeam.MembersMeta;
                        if (remoteTeam.MutedMembers != null)
                            localTeam.MutedMembers = remoteTeam.MutedMembers;
                        if (remoteTeam.AssistantAdmins != null)
                            localTeam.AssistantAdmins = remoteTeam.AssistantAdmins;
                        if (remoteTeam.DailyWorkingLimitHours > 0)
                            localTeam.DailyWorkingLimitHours = remoteTeam.DailyWorkingLimitHours;
                        if (remoteTeam.WeeklyWorkingLimitHours > 0)
                            localTeam.WeeklyWorkingLimitHours = remoteTeam.WeeklyWorkingLimitHours;
                        UserStorage.SaveTeam(localTeam);

                        // -- APPLY Country & WeeklyHourLimit FROM MembersMeta TO ALL USERS --
                        // This allows every team member to see other members' local time & correct progress
                        if (remoteTeam.MembersMeta != null)
                        {
                            foreach (var user in _allUsers)
                            {
                                if (remoteTeam.MembersMeta.ContainsKey(user.Name))
                                {
                                    var meta = remoteTeam.MembersMeta[user.Name];
                                    if (!string.IsNullOrEmpty(meta.Country))
                                        user.Country = meta.Country;
                                    if (meta.WeeklyHourLimit > 0)
                                        user.WeeklyHourLimit = meta.WeeklyHourLimit;
                                    if (!string.IsNullOrEmpty(meta.Color))
                                        user.Color = meta.Color;
                                    if (!string.IsNullOrEmpty(meta.Title))
                                        user.Title = meta.Title;
                                    if (!string.IsNullOrEmpty(meta.Role))
                                        user.Role = meta.Role;
                                }
                            }
                            UserStorage.SaveUsers(_allUsers);
                        }
                    }
                }

                // -- METHOD 2: DISCOVER USERS FROM FIREBASE LOGS --
                // Users who have heartbeats/logs but aren't in the team list yet
                // This catches users who joined and are active but weren't in team.Members
                try
                {
                    var logsResp = await _httpClient.GetAsync(firebaseUrl);
                    if (logsResp.IsSuccessStatusCode)
                    {
                        string logsJson = await logsResp.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(logsJson) && logsJson != "null")
                        {
//                             DebugLogger.Log("[Form1] Deserializing Firebase logs from JSON");
                var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(logsJson);
                            if (logsDict != null)
                            {
                                // FIND ALL UNIQUE USER NAMES FROM LOGS
                                // PREFER userName OVER userId TO AVOID DUPLICATES FROM MOBILE
                                // e.g. mobile may send userId="6J82GG_Blagoy" but userName="Blagoy"
                                var discoveredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var log in logsDict.Values)
                                {
                                    // If userName matches an existing user, skip userId entirely
                                    // This prevents mobile device IDs from creating duplicate entries
                                    if (!string.IsNullOrEmpty(log.userName) && localNames.Contains(log.userName))
                                        continue; // Already known by userName ? no duplicate needed

                                    // Prefer userName over userId for discovery
                                    if (!string.IsNullOrEmpty(log.userName))
                                        discoveredNames.Add(log.userName);
                                    else if (!string.IsNullOrEmpty(log.userId))
                                        discoveredNames.Add(log.userId);
                                }

                                foreach (string name in discoveredNames)
                                {
                                    if (!localNames.Contains(name))
                                    {
                                        // -- BLOCK MOBILE DUPLICATES AT SOURCE --
                                        bool isDup2 = false;
                                        if (name.Contains("_"))
                                        {
                                            string after = name.Substring(name.IndexOf('_') + 1);
                                            if (after.Length > 0 && localNames.Contains(after)) isDup2 = true;
                                        }
                                        if (!isDup2 && name.Contains(" "))
                                        {
                                            string after = name.Substring(name.IndexOf(' ') + 1);
                                            if (after.Length > 0 && localNames.Contains(after)) isDup2 = true;
                                        }
                                        if (isDup2) continue; // Skip ? mobile duplicate

//                                         DebugLogger.Log($"[Form1] Added new user to list: {name}");
            _allUsers.Add(new UserInfo(name, false));
                                        localNames.Add(name);
                                        needsRebuild = true;

                                        // ALSO ADD TO TEAM MEMBERS SO THEY PERSIST
                                        if (localTeam != null && !localTeam.Members.Contains(name))
                                        {
                                            localTeam.Members.Add(name);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                // -- CLEANUP: REMOVE DUPLICATE USERS --
                // Handles both case duplicates ("Blagoy" vs "blagoy") AND
                // mobile duplicates ("6J82GG_Blagoy", "6J82GG Blagoy" vs "Blagoy")
                var dedupAllNames = new HashSet<string>(_allUsers.Select(u => u.Name), StringComparer.OrdinalIgnoreCase);

                // STEP 1: Remove mobile-prefixed duplicates (e.g. "6J82GG_Blagoy" when "Blagoy" exists)
                var deduped = _allUsers.Where(u => !IsMobileDuplicate(u.Name, dedupAllNames)).ToList();

                // STEP 2: Also remove case-only duplicates ("Blagoy" vs "blagoy")
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var finalList = new List<UserInfo>();
                foreach (var u in deduped)
                {
                    if (!seen.Contains(u.Name))
                    {
                        seen.Add(u.Name);
                        finalList.Add(u);
                    }
                }
                deduped = finalList;
                if (deduped.Count < _allUsers.Count)
                {
                    _allUsers.Clear();
                    _allUsers.AddRange(deduped);
                    needsRebuild = true;

                    // Also clean the team Members list to remove prefixed duplicates
                    if (localTeam != null)
                    {
                        var cleanNames = new HashSet<string>(deduped.Select(u => u.Name), StringComparer.OrdinalIgnoreCase);
                        int oldCount = localTeam.Members.Count;
                        localTeam.Members = localTeam.Members
                            .Where(m => cleanNames.Contains(m))
                            .ToList();

                        // Push cleaned Members list to Firebase so the duplicate doesn't come back
                        if (localTeam.Members.Count < oldCount)
//                             DebugLogger.Log("[Form1] Saving team data to Firebase");
            _ = UserStorage.SaveTeamToFirebaseAsync(localTeam);
                    }
                }

//                 DebugLogger.Log("[Form1] Branch: needsRebuild = true - rebuilding user panel");
            if (needsRebuild)
                {
                    if (localTeam != null) UserStorage.SaveTeam(localTeam);
                    UserStorage.SaveUsers(_allUsers);
//                     DebugLogger.Log("[Form1] Rebuilding online users panel");
            RebuildOnlineUsersPanel();
                }
            }
            catch { }
        }

        // --------------------------------------------------------------
        // REBUILD ONLINE USERS PANEL ? CALLED WHEN NEW MEMBERS ARE FOUND
        // REMOVES OLD CONTROLS AND RE-CREATES THEM WITH UPDATED USER LIST
        // --------------------------------------------------------------
        private void RebuildOnlineUsersPanel()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => RebuildOnlineUsersPanel()));
                return;
            }

            try
            {
                // Preserve current progress values so a rebuild does not flash cards back to gray/0h.
                var progressSnapshot = onlineUserControls
                    .GroupBy(c => c.UserInfo.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var first = g.First();
                            return (worked: first.GetWeeklyHours(), holiday: first.GetHolidayHours());
                        },
                        StringComparer.OrdinalIgnoreCase);

                // SAVE REFERENCE TO VERSION PANEL (docked at bottom)
                Panel versionPanel = null;
                foreach (Control c in panelOnlineUsers.Controls)
                {
                    if (c is Panel p && p.Dock == DockStyle.Bottom)
                    {
                        versionPanel = p;
                        break;
                    }
                }

                // REMOVE OLD USER CONTROLS + favorites labels (keep title label and version panel)
                var toRemove = new System.Collections.Generic.List<Control>();
                foreach (Control c in panelOnlineUsers.Controls)
                {
                    if (c is OnlineUserControl)
                        toRemove.Add(c);
                    else if (c is Label lbl && lbl.Tag?.ToString() == "favorites_label")
                        toRemove.Add(c);
                }
                foreach (var c in toRemove)
                {
                    panelOnlineUsers.Controls.Remove(c);
                    c.Dispose();
                }
                onlineUserControls.Clear();

                // RE-CREATE USER CONTROLS FOR ALL USERS
//                 DebugLogger.Log("[Form1] Loading team from storage");
                var team = UserStorage.LoadTeam();
                bool viewerIsAdmin = _currentUser.IsAdmin || (team != null && team.HasAdminPrivileges(_currentUser.Name));
                int yPos = 45;

                foreach (var user in _allUsers)
                {
                    // -- SKIP MOBILE DUPLICATES: "6J82GG_Blagoy" when "Blagoy" exists --
                    bool skipUser = false;
                    string uname = user.Name;
                    if (uname.Contains("_"))
                    {
                        string afterUnder = uname.Substring(uname.IndexOf('_') + 1);
                        if (afterUnder.Length > 0 && _allUsers.Any(other =>
                            !string.Equals(other.Name, uname, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(other.Name, afterUnder, StringComparison.OrdinalIgnoreCase)))
                            skipUser = true;
                    }
                    if (!skipUser && uname.Contains(" "))
                    {
                        string afterSpace = uname.Substring(uname.IndexOf(' ') + 1);
                        if (afterSpace.Length > 0 && _allUsers.Any(other =>
                            !string.Equals(other.Name, uname, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(other.Name, afterSpace, StringComparison.OrdinalIgnoreCase)))
                            skipUser = true;
                    }
                    if (skipUser) continue;

                    bool isCurrentUser = user.Name == _currentUser.Name;
                    var ctrl = new OnlineUserControl(user, isCurrentUser, viewerIsAdmin);
                    ctrl.ViewerName = _currentUser.Name;
                    ctrl.Location = new Point(10, yPos);
                    ctrl.Width = 290;

                    if (progressSnapshot.TryGetValue(user.Name, out var snap))
                    {
                        ctrl.SetWeeklyHours(snap.worked, snap.holiday);
                    }

                    // WIRE UP CONTEXT MENU EVENTS (SAME AS BuildOnlineUsersPanel)
                    ctrl.OnSendDirectMessage += (targetUser) =>
                    {
                        OpenOrFocusDmForm(targetUser);
                    };
                    ctrl.OnMuteUser += (targetUser) =>
                    {
//                         DebugLogger.Log("[Form1] Loading team from storage");
                        var t = UserStorage.LoadTeam();
                        if (t == null) return;
                        if (t.MutedMembers == null) t.MutedMembers = new System.Collections.Generic.List<string>();
                        if (t.MutedMembers.Contains(targetUser))
                            t.MutedMembers.Remove(targetUser);
                        else
                            t.MutedMembers.Add(targetUser);
//                         DebugLogger.Log("[Form1] Saving team data to storage");
            UserStorage.SaveTeam(t);
//                         DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show(t.IsMuted(targetUser) ? $"{targetUser} has been muted." : $"{targetUser} has been unmuted.",
                            "Mute Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    };
                    ctrl.OnSecurityCheckUser += async (targetUser) =>
                    {
                        await ShowUserSecurityCheckDialogAsync(targetUser);
                    };
                    ctrl.OnKickUser += (targetUser) =>
                    {
                        KickAndBanUser(targetUser);
                    };
                    ctrl.OnMakeAssistantAdmin += (targetUser) =>
                    {
                        var t = UserStorage.LoadTeam();
                        if (t == null) return;
                        if (t.AssistantAdmins == null) t.AssistantAdmins = new System.Collections.Generic.List<string>();
                        if (t.AssistantAdmins.Contains(targetUser))
                            t.AssistantAdmins.Remove(targetUser);
                        else
                            t.AssistantAdmins.Add(targetUser);
//                         DebugLogger.Log("[Form1] Saving team data to storage");
            UserStorage.SaveTeam(t);
//                         DebugLogger.Log("[Form1] Saving team data to Firebase");
            _ = UserStorage.SaveTeamToFirebaseAsync(t);
//                         DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show(t.HasAdminPrivileges(targetUser)
                            ? $"{targetUser} is now an Assistant Admin."
                            : $"{targetUser} has been demoted from Assistant Admin.",
                            "Admin Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    };

                    panelOnlineUsers.Controls.Add(ctrl);
                    onlineUserControls.Add(ctrl);
                    yPos += ctrl.Height + 6;
                }

                // -- FAVORITES SECTION ? show favorite users from other teams --
                var favorites = UserStorage.GetFavoriteUsers(_currentUser.Name);
                var teamMemberNames = new HashSet<string>(_allUsers.Select(u => u.Name), StringComparer.OrdinalIgnoreCase);
                var externalFavorites = favorites.Where(f => !teamMemberNames.Contains(f)).ToList();

                if (externalFavorites.Count > 0)
                {
                    yPos += 6;
                    var favLabel = new Label
                    {
                        Text = "\u2b50 Favorites (other projects)",
                        Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
                        ForeColor = isDarkMode ? Color.FromArgb(251, 146, 60) : Color.FromArgb(217, 119, 6),
                        Location = new Point(10, yPos),
                        Size = new Size(280, 22),
                        Tag = "favorites_label" // tag for cleanup
                    };
                    panelOnlineUsers.Controls.Add(favLabel);
                    yPos += 24;

                    foreach (var favName in externalFavorites)
                    {
                        var favUser = new UserInfo(favName, false);
                        var favCtrl = new OnlineUserControl(favUser, false, false);
                        favCtrl.ViewerName = _currentUser.Name;
                        favCtrl.Location = new Point(10, yPos);
                        favCtrl.Width = 290;

                        if (progressSnapshot.TryGetValue(favName, out var favSnap))
                        {
                            favCtrl.SetWeeklyHours(favSnap.worked, favSnap.holiday);
                        }

                        // DM only ? no admin actions for external favorites
                        favCtrl.OnSendDirectMessage += (targetUser) =>
                        {
                            OpenOrFocusDmForm(targetUser);
                        };

                        panelOnlineUsers.Controls.Add(favCtrl);
                        onlineUserControls.Add(favCtrl);
                        yPos += favCtrl.Height + 6;
                    }
                }

                RefreshAskAiPanelStatus();

                // RE-APPLY THEME TO NEW CONTROLS
                ApplyTheme();
            }
            catch { }
        }

        private async Task RemoveOnlineSignalAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(firebaseUrl);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json) && json != "null")
                    {
//                         DebugLogger.Log("[Form1] Deserializing Firebase logs from JSON");
                var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json);
                        var onlineSignal = logsDict.FirstOrDefault(l =>
                            l.Value.userId == GetCurrentUserId() &&
                            l.Value.status == "Online");

                        if (onlineSignal.Key != null)
                        {
                            string deleteUrl = firebaseUrl.Replace(".json", $"/{onlineSignal.Key}.json");
                            await _httpClient.DeleteAsync(deleteUrl);
                        }
                    }
                }
            }
            catch { }
        }

        // ============================================================
        // PING / RING ALERT SYSTEM
        // ============================================================
        private async Task CheckForPingAlertAsync(Dictionary<string, LogEntry> logsDict)
        {
            try
            {
                string pingUrl = firebaseUrl.Replace("logs.json", "pings.json");
                var resp = await _httpClient.GetAsync(pingUrl);
                if (!resp.IsSuccessStatusCode) return;

                string json = await resp.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null") return;

                var pings = JsonConvert.DeserializeObject<Dictionary<string, PingEntry>>(json);
                if (pings == null || pings.Count == 0) return;

                // + FIRST LOAD: silently record all existing pings so they don't show as alerts +
                // On first check after app start / team join, we just remember all existing
                // ping keys and timestamps WITHOUT showing any alert dialogs.
                if (!_firstPingCheckDone)
                {
                    _firstPingCheckDone = true;
                    foreach (var kv in pings)
                    {
                        var ping = kv.Value;
                        if (ping == null) continue;
                        _acknowledgedPingKeys.Add(kv.Key);
                        if (!string.IsNullOrEmpty(ping.timestamp) &&
                            string.Compare(ping.timestamp, _lastPingTimestamp, StringComparison.Ordinal) > 0)
                        {
                            _lastPingTimestamp = ping.timestamp;
                        }
                    }
                    // Skip showing any alerts on first load ? go straight to cleanup
                }
                else
                {
                    // Find pings targeted at current user (or "all") that are truly NEW
                    foreach (var kv in pings)
                    {
                        var ping = kv.Value;
                        if (ping == null) continue;

                        // Skip pings we already acknowledged (pressed OK on)
                        if (_acknowledgedPingKeys.Contains(kv.Key)) continue;

                        bool isForMe = ping.target == "all" ||
                                       string.Equals(ping.target, _currentUser.Name, StringComparison.OrdinalIgnoreCase);
                        if (!isForMe) continue;
                        if (ping.from == _currentUser.Name) continue; // skip own pings

                        // Only react to pings newer than last known
                        if (string.Compare(ping.timestamp, _lastPingTimestamp, StringComparison.Ordinal) > 0)
                        {
                            _lastPingTimestamp = ping.timestamp;
                            // Mark this ping as acknowledged so it never shows again
                            _acknowledgedPingKeys.Add(kv.Key);
//                             DebugLogger.Log("[Form1] Playing sound: ping alert");
            SoundManager.PlayPingAlert();

                            // Show a notification
                            this.BeginInvoke(new Action(() =>
                            {
                                string msg = $"{ping.from} is pinging {(ping.target == "all" ? "everyone" : "you")}!";
                                if (!string.IsNullOrEmpty(ping.message))
                                    msg += $"\n\n\"{ping.message}\"";
//                                 DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show(msg, "Ping Alert", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }));
                        }
                        else
                        {
                            // Even if not newer, mark as acknowledged so it won't re-check
                            _acknowledgedPingKeys.Add(kv.Key);
                        }
                    }
                }

                // Clean up old pings (older than 60 seconds)
                foreach (var kv in pings)
                {
                    if (DateTime.TryParse(kv.Value?.timestamp, out var pingTime))
                    {
                        if ((DateTime.UtcNow - pingTime).TotalSeconds > 60)
                        {
                            string delUrl = pingUrl.Replace(".json", $"/{kv.Key}.json");
                            await _httpClient.DeleteAsync(delUrl);
                        }
                    }
                }
            }
            catch { }
        }

        private async Task SendPingAsync(string target, string message = "")
        {
            try
            {
                string pingUrl = firebaseUrl.Replace("logs.json", "pings.json");
                var ping = new PingEntry
                {
                    from = _currentUser.Name,
                    target = target,
                    message = message,
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                string json = JsonConvert.SerializeObject(ping);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(pingUrl, content);
                SoundManager.PlayPingSent();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox($"[ERROR] Ping failed: {ex.Message}\r\n");
            }
        }

        private sealed class UserSecuritySnapshot
        {
            public string UserName { get; set; }
            public string Verification { get; set; }
            public Color VerificationColor { get; set; }
            public string Status { get; set; }
            public string Country { get; set; }
            public string LocalTime { get; set; }
            public string Platform { get; set; }
            public string Role { get; set; }
            public string LastActivity { get; set; }
            public string IpCountry { get; set; }
            public string Notes { get; set; }
            public string ChallengeMessage { get; set; }
        }

        private async Task ShowUserSecurityCheckDialogAsync(string targetUser)
        {
            var snapshot = await BuildUserSecuritySnapshotAsync(targetUser);
            if (snapshot == null)
            {
                MessageBox.Show("User verification data is not available right now.",
                    "User Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dlg = new Form
            {
                Text = $"User Check - {targetUser}",
                Size = new Size(640, 610),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = isDarkMode ? Color.FromArgb(18, 23, 31) : Color.FromArgb(248, 250, 253)
            };

            var header = new Label
            {
                Text = $"Security Check: {snapshot.UserName}",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = isDarkMode ? Color.FromArgb(240, 245, 255) : Color.FromArgb(22, 28, 40),
                AutoSize = false,
                Location = new Point(22, 18),
                Size = new Size(360, 30)
            };
            dlg.Controls.Add(header);

            var subHeader = new Label
            {
                Text = "Admin-only trust summary based on real app signals",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Italic),
                ForeColor = isDarkMode ? Color.FromArgb(148, 160, 182) : Color.FromArgb(95, 105, 120),
                AutoSize = false,
                Location = new Point(24, 48),
                Size = new Size(470, 22)
            };
            dlg.Controls.Add(subHeader);

            var badge = new Label
            {
                Text = snapshot.Verification,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = snapshot.VerificationColor,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(455, 18),
                Size = new Size(145, 36)
            };
            dlg.Controls.Add(badge);

            ApplyRoundedCorners(badge, 16);

            int leftX = 24;
            int rightX = 324;
            int y = 86;
            int rowGap = 52;

            Action<int, int, string, string, Color?> addInfoCard = (x, top, title, value, accent) =>
            {
                var card = new Panel
                {
                    Location = new Point(x, top),
                    Size = new Size(280, 64),
                    BackColor = isDarkMode ? Color.FromArgb(28, 35, 46) : Color.White
                };
                ApplyRoundedCorners(card, 14);

                var titleLabel = new Label
                {
                    Text = title,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = isDarkMode ? Color.FromArgb(142, 154, 176) : Color.FromArgb(105, 112, 126),
                    Location = new Point(14, 10),
                    Size = new Size(248, 18)
                };
                card.Controls.Add(titleLabel);

                var valueLabel = new Label
                {
                    Text = value,
                    Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
                    ForeColor = accent ?? (isDarkMode ? Color.FromArgb(236, 241, 248) : Color.FromArgb(24, 31, 42)),
                    Location = new Point(14, 29),
                    Size = new Size(252, 24)
                };
                card.Controls.Add(valueLabel);

                dlg.Controls.Add(card);
            };

            addInfoCard(leftX, y, "Status", snapshot.Status, snapshot.Status.Contains("Working")
                ? (Color?)Color.FromArgb(52, 211, 153)
                : snapshot.Status.Contains("Online")
                    ? (Color?)Color.FromArgb(96, 165, 250)
                    : (Color?)Color.FromArgb(148, 163, 184));
            addInfoCard(rightX, y, "Stored Country", snapshot.Country, null);
            y += rowGap + 18;
            addInfoCard(leftX, y, "Local Time", snapshot.LocalTime, null);
            addInfoCard(rightX, y, "Platform", snapshot.Platform, null);
            y += rowGap + 18;
            addInfoCard(leftX, y, "Role / Title", snapshot.Role, null);
            addInfoCard(rightX, y, "Last Activity", snapshot.LastActivity, null);

            var notesTitle = new Label
            {
                Text = "Verification Notes",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = isDarkMode ? Color.FromArgb(241, 168, 82) : Color.FromArgb(166, 92, 33),
                Location = new Point(24, 292),
                Size = new Size(200, 22)
            };
            dlg.Controls.Add(notesTitle);

            var notesBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = isDarkMode ? Color.FromArgb(14, 18, 25) : Color.FromArgb(250, 251, 253),
                ForeColor = isDarkMode ? Color.FromArgb(223, 229, 239) : Color.FromArgb(34, 39, 47),
                Font = new Font("Segoe UI", 9.5f),
                Location = new Point(24, 318),
                Size = new Size(580, 168),
                Text = snapshot.Notes
            };
            dlg.Controls.Add(notesBox);

            var btnPing = new Button
            {
                Text = "Challenge Ping",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(241, 140, 72),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Location = new Point(24, 508),
                Size = new Size(150, 36)
            };
            btnPing.FlatAppearance.BorderSize = 0;
            ApplyRoundedCorners(btnPing, 18);
            btnPing.Click += async (s, e) =>
            {
                btnPing.Enabled = false;
                try
                {
                    await SendPingAsync(targetUser, snapshot.ChallengeMessage);
                    MessageBox.Show($"{targetUser} received a security ping challenge.",
                        "User Check", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                finally
                {
                    btnPing.Enabled = true;
                }
            };
            dlg.Controls.Add(btnPing);

            var btnDm = new Button
            {
                Text = "Open DM",
                FlatStyle = FlatStyle.Flat,
                BackColor = isDarkMode ? Color.FromArgb(42, 52, 69) : Color.FromArgb(226, 232, 240),
                ForeColor = isDarkMode ? Color.FromArgb(235, 240, 248) : Color.FromArgb(26, 31, 39),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Location = new Point(190, 508),
                Size = new Size(120, 36)
            };
            btnDm.FlatAppearance.BorderSize = 0;
            ApplyRoundedCorners(btnDm, 18);
            btnDm.Click += (s, e) => OpenOrFocusDmForm(targetUser);
            dlg.Controls.Add(btnDm);

            var btnClose = new Button
            {
                Text = "Close",
                FlatStyle = FlatStyle.Flat,
                BackColor = isDarkMode ? Color.FromArgb(35, 43, 56) : Color.FromArgb(235, 238, 242),
                ForeColor = isDarkMode ? Color.FromArgb(219, 226, 236) : Color.FromArgb(30, 36, 44),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.OK,
                Location = new Point(484, 508),
                Size = new Size(120, 36)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            ApplyRoundedCorners(btnClose, 18);
            dlg.Controls.Add(btnClose);
            dlg.AcceptButton = btnClose;

            dlg.ShowDialog(this);
        }

        private async Task<UserSecuritySnapshot> BuildUserSecuritySnapshotAsync(string targetUser)
        {
            var team = UserStorage.LoadTeam();
            var target = _allUsers?.FirstOrDefault(u => string.Equals(u.Name, targetUser, StringComparison.OrdinalIgnoreCase));
            var logs = await TryLoadCurrentTeamLogsAsync();
            var userLogs = logs?
                .Values
                .Where(log => LogBelongsToUser(log, targetUser))
                .OrderByDescending(log => ParseLogTimestamp(log.timestamp) ?? DateTime.MinValue)
                .ToList() ?? new List<LogEntry>();

            var latestLog = userLogs.FirstOrDefault();
            var activeLog = userLogs.FirstOrDefault(log => string.Equals(log.status, "Working", StringComparison.OrdinalIgnoreCase))
                ?? userLogs.FirstOrDefault(log => string.Equals(log.status, "Online", StringComparison.OrdinalIgnoreCase));

            string countryCode = GetStoredCountryForUser(targetUser, target, team);
            string localTime = string.IsNullOrWhiteSpace(countryCode)
                ? $"{DateTime.Now:HH:mm} (local PC fallback)"
                : $"{PublicHolidays.GetLocalTimeString(countryCode)} {countryCode.ToUpper()}";

            var platforms = userLogs
                .Select(log => NormalizePlatform(log.platform))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string role = GetStoredRoleForUser(targetUser, target, team);
            string status = activeLog?.status ?? "Offline";
            string lastActivity = FormatLastActivity(ParseLogTimestamp(latestLog?.timestamp));

            var notes = new List<string>();
            int trustScore = 0;

            if (!string.IsNullOrWhiteSpace(countryCode))
            {
                trustScore += 1;
                notes.Add($"Stored country is set to {countryCode.ToUpper()}, so the app can estimate local time.");
            }
            else
            {
                notes.Add("Country is not set, so local time falls back to this computer's time.");
            }

            if (platforms.Count > 0)
            {
                trustScore += 1;
                notes.Add($"Device signal is present: {string.Join(", ", platforms)}.");
            }
            else
            {
                notes.Add("No platform hint was found in recent logs.");
            }

            if (latestLog != null)
            {
                trustScore += 1;
                notes.Add($"Recent activity exists: {lastActivity}.");
            }
            else
            {
                notes.Add("No recent team log exists for this user yet.");
            }

            if (string.Equals(status, "Working", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Online", StringComparison.OrdinalIgnoreCase))
            {
                trustScore += 1;
                notes.Add($"Current status is {status}, which means the user is actively visible in team presence.");
            }
            else
            {
                notes.Add("User is offline right now, so live verification is weaker.");
            }

            notes.Add("IP country is not currently shared by the client, so network origin cannot be fully verified.");
            notes.Add("Best admin check: compare role, country, platform, live status, then send a challenge ping or DM.");

            string verification;
            Color verificationColor;
            if (trustScore >= 4)
            {
                verification = "Likely Legit";
                verificationColor = Color.FromArgb(34, 197, 94);
            }
            else if (trustScore >= 2)
            {
                verification = "Needs Review";
                verificationColor = Color.FromArgb(245, 158, 11);
            }
            else
            {
                verification = "Unverified";
                verificationColor = Color.FromArgb(239, 68, 68);
            }

            return new UserSecuritySnapshot
            {
                UserName = targetUser,
                Verification = verification,
                VerificationColor = verificationColor,
                Status = status,
                Country = string.IsNullOrWhiteSpace(countryCode) ? "Not set" : countryCode.ToUpper(),
                LocalTime = localTime,
                Platform = platforms.Count == 0 ? "Unknown" : string.Join(", ", platforms),
                Role = string.IsNullOrWhiteSpace(role) ? "Not set" : role,
                LastActivity = lastActivity,
                IpCountry = "Not shared by client",
                Notes = string.Join(Environment.NewLine + Environment.NewLine, notes),
                ChallengeMessage = "Admin security check: please reply so we can verify this account is active."
            };
        }

        private async Task<Dictionary<string, LogEntry>> TryLoadCurrentTeamLogsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(firebaseUrl);
                if (!response.IsSuccessStatusCode) return new Dictionary<string, LogEntry>();

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                    return new Dictionary<string, LogEntry>();

                return JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json)
                    ?? new Dictionary<string, LogEntry>();
            }
            catch
            {
                return new Dictionary<string, LogEntry>();
            }
        }

        private bool LogBelongsToUser(LogEntry log, string targetUser)
        {
            if (log == null || string.IsNullOrWhiteSpace(targetUser)) return false;

            string userId = log.userId ?? string.Empty;
            string userName = log.userName ?? string.Empty;

            if (string.Equals(StripJoinCodePrefix(userId), targetUser, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(StripJoinCodePrefix(userName), targetUser, StringComparison.OrdinalIgnoreCase)) return true;

            foreach (char sep in new[] { '_', ' ', '-', '.' })
            {
                if (userId.IndexOf(sep) > 0 &&
                    string.Equals(userId.Substring(userId.IndexOf(sep) + 1), targetUser, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (userName.IndexOf(sep) > 0 &&
                    string.Equals(userName.Substring(userName.IndexOf(sep) + 1), targetUser, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private string GetStoredCountryForUser(string targetUser, UserInfo target, TeamInfo team)
        {
            if (!string.IsNullOrWhiteSpace(target?.Country))
                return target.Country.Trim().ToUpperInvariant();

            if (team?.MembersMeta != null && team.MembersMeta.ContainsKey(targetUser))
            {
                var metaCountry = team.MembersMeta[targetUser]?.Country;
                if (!string.IsNullOrWhiteSpace(metaCountry))
                    return metaCountry.Trim().ToUpperInvariant();
            }

            return string.Empty;
        }

        private string GetStoredRoleForUser(string targetUser, UserInfo target, TeamInfo team)
        {
            if (!string.IsNullOrWhiteSpace(target?.Role)) return target.Role.Trim();
            if (!string.IsNullOrWhiteSpace(target?.Title)) return target.Title.Trim();

            if (team?.MembersMeta != null && team.MembersMeta.ContainsKey(targetUser))
            {
                var meta = team.MembersMeta[targetUser];
                if (!string.IsNullOrWhiteSpace(meta?.Role)) return meta.Role.Trim();
                if (!string.IsNullOrWhiteSpace(meta?.Title)) return meta.Title.Trim();
            }

            return string.Empty;
        }

        private string NormalizePlatform(string platform)
        {
            if (string.IsNullOrWhiteSpace(platform)) return string.Empty;

            string value = platform.Trim();
            if (value.Equals("windows", StringComparison.OrdinalIgnoreCase)) return "Desktop";
            if (value.Equals("desktop", StringComparison.OrdinalIgnoreCase)) return "Desktop";
            if (value.Equals("android", StringComparison.OrdinalIgnoreCase)) return "Android";
            if (value.Equals("ios", StringComparison.OrdinalIgnoreCase)) return "iPhone";

            return char.ToUpper(value[0]) + value.Substring(1).ToLowerInvariant();
        }

        private DateTime? ParseLogTimestamp(string timestamp)
        {
            if (DateTime.TryParse(timestamp, out var parsed))
                return parsed.ToLocalTime();

            return null;
        }

        private string FormatLastActivity(DateTime? timestamp)
        {
            if (!timestamp.HasValue) return "No recent activity";

            var local = timestamp.Value;
            var age = DateTime.Now - local;
            if (age.TotalMinutes < 2) return $"Just now ({local:HH:mm})";
            if (age.TotalHours < 1) return $"{Math.Max(1, (int)age.TotalMinutes)} min ago";
            if (age.TotalDays < 1) return $"{Math.Max(1, (int)age.TotalHours)} h ago";

            return local.ToString("dd MMM yyyy HH:mm");
        }

        // --------------------------------------------------------------
        // KICK AND BAN USER ? CONFIRMATION POPUP WITH BAN OPTION
        // --------------------------------------------------------------
        private void KickAndBanUser(string targetUser)
        {
            // CREATE CUSTOM DIALOG WITH KICK + BAN OPTION
            var dlg = new Form
            {
                Text = "Kick User",
                Size = new Size(420, 220),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250)
            };

            var lblMsg = new Label
            {
                Text = $"Are you sure you want to kick \"{targetUser}\" from the team?\n\nThis will remove them from all team features.",
                Font = new Font("Segoe UI", 10),
                ForeColor = isDarkMode ? Color.FromArgb(220, 224, 230) : Color.Black,
                Location = new Point(20, 15),
                Size = new Size(370, 60)
            };
            dlg.Controls.Add(lblMsg);

            // CHECKBOX: ALSO BAN (PREVENT REJOINING)
            var chkBan = new CheckBox
            {
                Text = "Also BAN this user (cannot rejoin with team code)",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(239, 68, 68),
                Location = new Point(20, 80),
                Size = new Size(370, 24),
                Checked = false
            };
            dlg.Controls.Add(chkBan);

            // KICK BUTTON (RED)
            var btnKick = new Button
            {
                Text = "\U0001f6ab  Kick User",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(239, 68, 68),
                ForeColor = Color.White,
                Location = new Point(20, 120),
                Size = new Size(170, 44),
                DialogResult = DialogResult.OK
            };
            btnKick.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnKick);

            // CANCEL BUTTON
            var btnCancel = new Button
            {
                Text = "Cancel",
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = isDarkMode ? Color.FromArgb(38, 44, 56) : Color.FromArgb(200, 200, 210),
                ForeColor = isDarkMode ? Color.FromArgb(160, 170, 180) : Color.FromArgb(80, 80, 90),
                Location = new Point(210, 120),
                Size = new Size(170, 44),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnCancel);

            dlg.AcceptButton = btnKick;
            dlg.CancelButton = btnCancel;

            if (dlg.ShowDialog() != DialogResult.OK) return;

            // PERFORM KICK
            var t = UserStorage.LoadTeam();
            if (t == null) return;

            t.Members.Remove(targetUser);
            t.MembersMeta?.Remove(targetUser);
            t.MutedMembers?.Remove(targetUser);
            t.AssistantAdmins?.Remove(targetUser);

            // IF BAN CHECKED ? ADD TO BANNED LIST
            bool wasBanned = chkBan.Checked;
            if (wasBanned)
            {
                if (t.BannedMembers == null) t.BannedMembers = new List<string>();
                if (!t.IsBanned(targetUser))
                    t.BannedMembers.Add(targetUser);
            }

//             DebugLogger.Log("[Form1] Saving team data to storage");
            UserStorage.SaveTeam(t);
//             DebugLogger.Log("[Form1] Saving team data to Firebase");
            _ = UserStorage.SaveTeamToFirebaseAsync(t);
//             DebugLogger.Log($"[Form1] Removing users from list");
            _allUsers.RemoveAll(u => string.Equals(u.Name, targetUser, StringComparison.OrdinalIgnoreCase));
            UserStorage.SaveUsers(_allUsers);

            string msg = wasBanned
                ? $"{targetUser} has been kicked and BANNED.\nThey cannot rejoin with the team code."
                : $"{targetUser} has been kicked from the team.";
//             DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show(msg, "User Removed", MessageBoxButtons.OK, MessageBoxIcon.Information);

//             DebugLogger.Log("[Form1] Rebuilding online users panel");
            RebuildOnlineUsersPanel();
        }

        // --- TEAM SETTINGS ? OPENS ADMIN PANEL DIALOG ---
        // ----------------------------------------------------------
        //  SWITCH TEAM ? opens FormTeamSwitcher, restarts app on switch
        // ----------------------------------------------------------
        private void OnSwitchTeamClicked()
        {
            using (var switcher = new FormTeamSwitcher())
            {
                if (switcher.ShowDialog(this) == DialogResult.OK)
                {
                    // Team was switched ? restart the application to reload everything
                    // with the new team's data (Firebase URLs, members, settings, etc.)
                    UserStorage.SetSkipTeamSwitcherOnce(true);
                    Application.Restart();
                    Environment.Exit(0);
                }
            }
        }

        private void ShowTeamOptions()
        {
            var team = UserStorage.LoadTeam();
            if (team == null)
            {
//                 DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show("No team configured. Please restart the app to set up a team.",
                    "No Team", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            bool hasAdminPower = _currentUser.IsAdmin || team.HasAdminPrivileges(_currentUser.Name);

            // Pass current panel visibility states to Settings dialog
            var panelVis = new Dictionary<string, bool>
            {
                { "Board", _boardVisible },
                { "Chat", _chatVisible },
                { "Team", _teamVisible },
                { "Files", _filesVisible },
                { "Calendar", _calendarVisible },
                { "Weather", _weatherVisible },
                { "Personal Board", _personalBoardVisible },
                { "Ask AI", _askAiVisible },
                { "AI Chat", _aiChatVisible }
            };

            using (var dlg = new TeamOptionsPanel(team, _allUsers, hasAdminPower, isDarkMode, _currentUser.Name, panelVis))
            {
                // Subscribe to panel visibility changes from Settings
                dlg.PanelVisibilityChanged += (panelName, visible) =>
                {
                    switch (panelName)
                    {
                        case "Board":
                            _boardVisible = visible;
                            if (_stickerBoard != null) _stickerBoard.Visible = visible;
                            break;
                        case "Chat":
                            _chatVisible = visible;
                            if (_chatPanel != null) _chatPanel.Visible = visible;
                            break;
                        case "Team":
                            _teamVisible = visible;
                            if (panelOnlineUsers != null) panelOnlineUsers.Visible = visible;
                            break;
                        case "Files":
                            _filesVisible = visible;
                            if (_fileSharePanel != null) _fileSharePanel.Visible = visible;
                            break;
                        case "Calendar":
                            _calendarVisible = visible;
                            if (_calendarPanel != null) _calendarPanel.Visible = visible;
                            var calSettings = OrganizerStorage.LoadSettings();
                            calSettings.ShowCalendar = visible;
                            OrganizerStorage.SaveSettings(calSettings);
                            break;
                        case "Weather":
                            _weatherVisible = visible;
                            if (_weatherPanel != null) _weatherPanel.Visible = visible;
                            SaveWeatherPreferences();
                            ApplyMainColumnLayout();
                            _ = RefreshWeatherAsync();
                            break;
                        case "Personal Board":
                            _personalBoardVisible = visible;
                            SavePersonalBoardPreferences();
                            if (_personalBoardVisible)
                                ShowPersonalBoardWindow();
                            else
                                HidePersonalBoardWindow();
                            RefreshAskAiPanelStatus();
                            break;
                        case "Ask AI":
                            _askAiVisible = visible;
                            if (_orgSettings == null)
                                _orgSettings = OrganizerStorage.LoadSettings();
                            _orgSettings.ShowAiWidget = visible;
                            OrganizerStorage.SaveSettings(_orgSettings);
                            RefreshAskAiPanelStatus();
                            break;
                        case "AI Chat":
                            _aiChatVisible = visible;
                            if (_orgSettings == null)
                                _orgSettings = OrganizerStorage.LoadSettings();
                            _orgSettings.ShowAiChatPanel = visible;
                            OrganizerStorage.SaveSettings(_orgSettings);
                            if (_aiChatVisible)
                                ShowAiChatWindow();
                            else
                                HideAiChatWindow();
                            UpdateAiChatWindowBounds();
                            break;
                    }
                };

                // Subscribe to chat font size changes ? apply live to chat panel
                dlg.ChatFontSizeChanged += (size) =>
                {
                    if (_chatPanel != null)
                        _chatPanel.SetChatFontSize(size);
                };

                // Subscribe to dark mode toggle from settings
                dlg.DarkModeChanged += (dark) =>
                {
                    if (checkBoxTheme.Checked != dark)
                    {
                        checkBoxTheme.Checked = dark;
                        // CheckBoxTheme_CheckedChanged fires ? updates isDarkMode + ApplyTheme
                    }
                };

                // Subscribe to sound toggle from settings
                dlg.SoundToggled += (on) =>
                {
                    SoundManager.Enabled = on;
                };

                // Subscribe to custom theme changes
                dlg.CustomThemeChanged += (theme) =>
                {
                    _customTheme = theme;
                    ApplyTheme();
                };

                dlg.ResetSplittersRequested += () =>
                {
                    ResetSplittersToDefault();
                };

                dlg.ShowDialog(this);

                // Team/join/create switch must restart immediately BEFORE any live refresh,
                // otherwise old panel instances can write previous-team data into new team cache.
                if (dlg.NeedsRestart)
                {
                    MessageBox.Show(
                        "Team context changed. The app will restart now to load the selected team board, stickers, chat and users.",
                        "Switching Team", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    UserStorage.SetSkipTeamSwitcherOnce(true);
                    Application.Restart();
                    Environment.Exit(0);
                    return;
                }

                if (dlg.DataChanged)
                {
                    // Reload team and users from storage
                    var updatedTeam = UserStorage.LoadTeam();
//                     DebugLogger.Log("[Form1] Loading users from storage");
                    _allUsers = UserStorage.LoadUsers();

                    // Clean mobile duplicates (e.g. "6J82GG_Blagoy" when "Blagoy" exists)
                    var reloadNames = new HashSet<string>(_allUsers.Select(u => u.Name), StringComparer.OrdinalIgnoreCase);
//                     DebugLogger.Log($"[Form1] Removing users from list");
            _allUsers.RemoveAll(u =>
                    {
                        string rn = u.Name;
                        if (rn.Contains("_"))
                        {
                            string after = rn.Substring(rn.IndexOf('_') + 1);
                            if (after.Length > 0 && !string.Equals(after, rn, StringComparison.OrdinalIgnoreCase)
                                && reloadNames.Contains(after))
                                return true;
                        }
                        return false;
                    });

                    // Update current user reference (case-insensitive match)
                    var refreshedUser = _allUsers.FirstOrDefault(u => u.Name.Equals(_currentUser.Name, StringComparison.OrdinalIgnoreCase));
                    if (refreshedUser != null)
                        _currentUser = refreshedUser;

                    // Update title bar in case team name changed ? preserve admin tag
                    this.Text = _currentUser.IsAdmin
                        ? $"Work Time Counter \u2014 {_currentUser.Name} (Admin)"
                        : $"Work Time Counter \u2014 {_currentUser.Name}";

                    // Settings button is ALWAYS visible ? admin actions are restricted inside the panel
                    // if (btnSettings != null)
                    //     btnSettings.Visible = true;

                    // Refresh online user panel to pick up color/member changes
//                     DebugLogger.Log("[Form1] Rebuilding online users panel");
            RebuildOnlineUsersPanel();
                    _ = RefreshOnlineStatusAsync();
                }

            }
        }

        private void ShowPingDialog()
        {
            using (var dlg = new Form())
            {
                dlg.Text = "Ping Team";
                dlg.Size = new Size(340, 260);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = isDarkMode ? Color.FromArgb(30, 36, 46) : Color.FromArgb(248, 249, 252);
                dlg.ForeColor = isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59);

                var lblTarget = new Label
                {
                    Text = "Send ping to:",
                    Location = new Point(16, 16),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 10, FontStyle.Bold)
                };
                dlg.Controls.Add(lblTarget);

                var cmbTarget = new ComboBox
                {
                    Location = new Point(16, 42),
                    Size = new Size(290, 28),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new System.Drawing.Font("Segoe UI", 10),
                    BackColor = isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White,
                    ForeColor = isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59)
                };
                cmbTarget.Items.Add("Everyone");
                foreach (var user in _allUsers)
                {
                    if (user.Name != _currentUser.Name)
                        cmbTarget.Items.Add(user.Name);
                }
                cmbTarget.SelectedIndex = 0;
                dlg.Controls.Add(cmbTarget);

                var lblMsg = new Label
                {
                    Text = "Message (optional):",
                    Location = new Point(16, 80),
                    AutoSize = true,
                    Font = new System.Drawing.Font("Segoe UI", 10)
                };
                dlg.Controls.Add(lblMsg);

                var txtMsg = new TextBox
                {
                    Location = new Point(16, 106),
                    Size = new Size(290, 50),
                    Multiline = true,
                    Font = new System.Drawing.Font("Segoe UI", 10),
                    BackColor = isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White,
                    ForeColor = isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59),
                    MaxLength = 200
                };
                dlg.Controls.Add(txtMsg);

                var btnSend = new Button
                {
                    Text = "Send Ping",
                    Size = new Size(290, 38),
                    Location = new Point(16, 168),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(234, 179, 8),
                    ForeColor = Color.FromArgb(40, 30, 0),
                    Font = new System.Drawing.Font("Segoe UI", 10, FontStyle.Bold),
                    Cursor = Cursors.Hand,
                    DialogResult = DialogResult.OK
                };
                btnSend.FlatAppearance.BorderSize = 0;
                dlg.Controls.Add(btnSend);
                dlg.AcceptButton = btnSend;

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string target = cmbTarget.SelectedIndex == 0
                        ? "all"
                        : cmbTarget.SelectedItem.ToString();
                    _ = SendPingAsync(target, txtMsg.Text.Trim());
                }
            }
        }

        public class PingEntry
        {
            public string from { get; set; }
            public string target { get; set; }
            public string message { get; set; }
            public string timestamp { get; set; }
        }

        // ============================================================
        // TIMER & HELPERS
        // ============================================================
        /// <summary>
        /// Runs on a timer to update the current time display label.
        /// Updates every tick to show HH:mm:ss format.
        /// </summary>
        private void Timer_Tick(object sender, EventArgs e)
        {
//             DebugLogger.Log("[Form1] Timer_Tick: Updating current time display");
            labelTimerNow.Text = DateTime.Now.ToString("HH:mm:ss");
            UpdateMainClockVisualState();
        }

                /// <summary>
        /// Appends a message to the debug form's rich text box.
        /// Thread-safe: uses Invoke if required for cross-thread calls.
        /// </summary>
        private void UpdateRichTextBox(string message)
        {
            string upper = (message ?? "").ToUpperInvariant();
            bool important = upper.Contains("EXCEPTION") ||
                             upper.Contains("[ERROR]") ||
                             upper.Contains("FAILED") ||
                             upper.Contains("FATAL");
            if (!important)
                return;

            if (_debugForm == null)
                return;

            if (_debugForm.InvokeRequired)
                _debugForm.Invoke(new Action(() => _debugForm.AppendMessage(message)));
            else
                _debugForm.AppendMessage(message);
        }

        private void labelTimerNow_Click(object sender, EventArgs e) { }

        private FirebaseClient GetFirebaseClient()
        {
            return new FirebaseClient(UserStorage.GetFirebaseBaseUrl() + "/");
        }

        private string GetCurrentUserId()
        {
            return _currentUser?.Name ?? "defaultUser";
        }

        /// <summary>
        /// Strips the JoinCode prefix from a mobile-submitted name.
        /// e.g. "6J82GG_Blagoy" ? "Blagoy", "6J82GG Blagoy" ? "Blagoy"
        /// Handles separators: _ (space) - .  or no separator if JoinCode is followed by uppercase.
        /// Returns the original name if no JoinCode prefix is found.
        /// </summary>
        /// <summary>
        /// Strips the JoinCode prefix from a mobile-submitted name.
        /// e.g. "6J82GG_Blagoy" ? "Blagoy", "6J82GG Blagoy" ? "Blagoy"
        /// Uses the team's JoinCode loaded from storage.
        /// Returns the original name if no JoinCode prefix is found.
        /// </summary>
        // Removes JoinCode prefix from mobile-submitted names (e.g. "6J82GG_Blagoy" -> "Blagoy")
        private string StripJoinCodePrefix(string name)
        {
//             DebugLogger.Log($"[Form1] StripJoinCodePrefix: Processing name \'{name}\'");
            if (string.IsNullOrEmpty(name)) return name;
            var t = UserStorage.LoadTeam();
            if (t == null || string.IsNullOrEmpty(t.JoinCode)) return name;
            string code = t.JoinCode;

            if (name.StartsWith(code, StringComparison.OrdinalIgnoreCase) && name.Length > code.Length)
            {
                string remainder = name.Substring(code.Length);
                // Strip optional separator character
                if (remainder.Length > 0 && (remainder[0] == '_' || remainder[0] == ' ' ||
                    remainder[0] == '-' || remainder[0] == '.'))
                    remainder = remainder.Substring(1);
                if (!string.IsNullOrEmpty(remainder))
                    return remainder;
            }
            return name;
        }

        /// <summary>
        /// Checks if the given name is a JoinCode-prefixed version of an existing user.
        /// e.g. "6J82GG_Blagoy" returns true if "Blagoy" exists in the provided names set.
        /// </summary>
        private bool IsJoinCodeDuplicate(string name, HashSet<string> existingNames)
        {
            if (string.IsNullOrEmpty(name) || existingNames == null) return false;
            string stripped = StripJoinCodePrefix(name);
            // If stripping changed the name AND the stripped version exists, it's a duplicate
            return !string.Equals(stripped, name, StringComparison.OrdinalIgnoreCase) &&
                   existingNames.Contains(stripped);
        }

        /// <summary>
        /// Checks if a name is a "prefix+separator+realName" duplicate of any other name in the set.
        /// Works WITHOUT needing the JoinCode ? just checks if the name ends with
        /// a separator (_  - .) followed by an existing user name.
        /// e.g. "6J82GG_Blagoy" is a duplicate if "Blagoy" exists.
        /// </summary>
        private bool IsMobileDuplicate(string name, HashSet<string> allNames)
        {
            if (string.IsNullOrEmpty(name) || allNames == null) return false;
            char[] separators = { '_', ' ', '-', '.' };
            foreach (char sep in separators)
            {
                int idx = name.IndexOf(sep);
                if (idx > 0 && idx < name.Length - 1)
                {
                    string suffix = name.Substring(idx + 1);
                    // The suffix must match another user's name AND not be the same as the full name
                    if (!string.Equals(suffix, name, StringComparison.OrdinalIgnoreCase) &&
                        allNames.Contains(suffix))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a log's userId or userName belongs to the current user.
        /// Matches both desktop ("Blagoy") and mobile ("6J82GG_Blagoy") formats.
        /// </summary>
        private bool IsCurrentUserLog(LogEntry log)
        {
//             DebugLogger.Log("[Form1] IsCurrentUserLog: Checking if log belongs to current user");
            string myName = _currentUser?.Name ?? "";
            if (string.IsNullOrEmpty(myName)) return false;

            // Direct match
            if (string.Equals(log.userId, myName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(log.userName, myName, StringComparison.OrdinalIgnoreCase))
                return true;

            // Mobile match: "6J82GG_Blagoy" ? check if part after "_" matches
            string uid = log.userId ?? "";
            string uname = log.userName ?? "";
            if (uid.Contains("_"))
            {
                string after = uid.Substring(uid.IndexOf('_') + 1);
                if (string.Equals(after, myName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            if (uname.Contains("_"))
            {
                string after = uname.Substring(uname.IndexOf('_') + 1);
                if (string.Equals(after, myName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // -- Check if THIS user has a live signal (desktop OR mobile) --
        private async Task<bool> CheckIfLiveSignalExistsAsync()
        {
//             DebugLogger.Log("[Form1] CheckIfLiveSignalExistsAsync: ENTRY - Checking for existing live signal");
            UpdateRichTextBox("[DEBUG] Checking for existing live signal...\r\n");
            try
            {
                var firebase = GetFirebaseClient();
                var logs = await firebase.Child("logs").OnceAsync<LogEntry>();

                var liveLog = logs.FirstOrDefault(log =>
                    IsCurrentUserLog(log.Object) &&
                    !string.IsNullOrEmpty(log.Object.status) &&
                    log.Object.status.Equals("Working", StringComparison.OrdinalIgnoreCase)
                );

                bool exists = liveLog != null;
                UpdateRichTextBox("[i] Live signal check: " + (exists ? "Found" : "Not found") + "\r\n");
                return exists;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox("[ERROR] Could not check live signal: " + ex.Message + "\r\n");
                return false;
            }
        }

        private async Task DeletePreviousLiveSignalAsync()
        {
//             DebugLogger.Log("[Form1] DeletePreviousLiveSignalAsync: ENTRY - Deleting previous live signal");
            UpdateRichTextBox("[DEBUG] Deleting previous live signal...\r\n");
            try
            {
                var firebase = GetFirebaseClient();
                var logs = await firebase.Child("logs").OnceAsync<LogEntry>();
                // Match both desktop AND mobile live signals for this user
                var liveLog = logs.FirstOrDefault(log =>
                    IsCurrentUserLog(log.Object) &&
                    !string.IsNullOrEmpty(log.Object.status) &&
                    log.Object.status.Equals("Working", StringComparison.OrdinalIgnoreCase)
                );

                if (liveLog != null)
                {
                    await firebase.Child("logs").Child(liveLog.Key).DeleteAsync();
                    UpdateRichTextBox("[i] Previous live signal deleted.\r\n");
                }
                else
                {
                    UpdateRichTextBox("[i] No live signal to delete.\r\n");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox("[ERROR] Failed to delete live signal: " + ex.Message + "\r\n");
                throw;
            }
        }

        private async Task AddStopTimeToLiveSignalAsync(string manualWorkingTime)
        {
//             DebugLogger.Log($"[Form1] AddStopTimeToLiveSignalAsync: ENTRY - Adding stop time: {manualWorkingTime}");
            UpdateRichTextBox("[DEBUG] Adding stop time to live signal...\r\n");
            try
            {
                var firebase = GetFirebaseClient();
                var logs = await firebase.Child("logs").OnceAsync<LogEntry>();
                var liveLog = logs.FirstOrDefault(log =>
                    log.Object.userId == GetCurrentUserId() &&
                    !string.IsNullOrEmpty(log.Object.status) &&
                    log.Object.status.Equals("Working", StringComparison.OrdinalIgnoreCase)
                );

                if (liveLog != null)
                {
                    await firebase.Child("logs").Child(liveLog.Key).PatchAsync(new
                    {
                        workingTime = manualWorkingTime,
                        status = "Stopped"
                    });
                    UpdateRichTextBox("[i] Stop time added: " + manualWorkingTime + "\r\n");
                }
                else
                {
                    UpdateRichTextBox("[i] No live signal found to stop.\r\n");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox("[ERROR] Failed to add stop time: " + ex.Message + "\r\n");
                throw;
            }
        }

        // ============================================================
        // START BUTTON
        // ============================================================
        private async void buttonStart_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[Form1] buttonStart_Click: ENTRY - Start button clicked");
            buttonStart.Enabled = false;
            try
            {
                // Check if description is empty
                // Check if work description was provided
//                 DebugLogger.Log("[Form1] Checking if description is provided");
                if (string.IsNullOrWhiteSpace(richTextBoxDescription?.Text))
                {
                    using (var dlg = new Form())
                    {
                        dlg.Text = "No Description";
                        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                        dlg.StartPosition = FormStartPosition.CenterParent;
                        dlg.MaximizeBox = false;
                        dlg.MinimizeBox = false;
                        dlg.ShowInTaskbar = false;
                        dlg.ClientSize = new Size(500, 210);
                        dlg.BackColor = isDarkMode ? Color.FromArgb(28, 34, 44) : Color.FromArgb(248, 250, 252);

                        var lbl = new Label
                        {
                            Text = "Description is empty.\nYou can type description now or continue without description:",
                            AutoSize = false,
                            Width = 468,
                            Height = 44,
                            Location = new Point(16, 14),
                            Font = new Font("Segoe UI", 9.5f),
                            ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59)
                        };

                        var txt = new TextBox
                        {
                            Location = new Point(16, 66),
                            Width = 468,
                            Height = 28,
                            Font = new Font("Segoe UI", 10f)
                        };

                        var btnOk = new Button
                        {
                            Text = "OK",
                            Size = new Size(110, 34),
                            Location = new Point(140, 150),
                            BackColor = Color.FromArgb(34, 197, 94),
                            ForeColor = Color.White,
                            FlatStyle = FlatStyle.Flat
                        };
                        btnOk.FlatAppearance.BorderSize = 0;
                        btnOk.Click += (s2, e2) =>
                        {
                            if (string.IsNullOrWhiteSpace(txt.Text))
                            {
                                txt.Focus();
                                return;
                            }
                            dlg.DialogResult = DialogResult.OK;
                            dlg.Close();
                        };

                        var btnContinue = new Button
                        {
                            Text = "Continue Without Description",
                            Size = new Size(200, 34),
                            Location = new Point(258, 150),
                            BackColor = Color.FromArgb(71, 85, 105),
                            ForeColor = Color.White,
                            FlatStyle = FlatStyle.Flat
                        };
                        btnContinue.FlatAppearance.BorderSize = 0;
                        btnContinue.Click += (s2, e2) =>
                        {
                            dlg.DialogResult = DialogResult.Yes;
                            dlg.Close();
                        };

                        var btnCancel = new Button
                        {
                            Text = "Cancel",
                            Size = new Size(110, 34),
                            Location = new Point(24, 150),
                            BackColor = isDarkMode ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184),
                            ForeColor = Color.White,
                            FlatStyle = FlatStyle.Flat
                        };
                        btnCancel.FlatAppearance.BorderSize = 0;
                        btnCancel.Click += (s2, e2) =>
                        {
                            dlg.DialogResult = DialogResult.Cancel;
                            dlg.Close();
                        };

                        dlg.Controls.Add(lbl);
                        dlg.Controls.Add(txt);
                        dlg.Controls.Add(btnCancel);
                        dlg.Controls.Add(btnOk);
                        dlg.Controls.Add(btnContinue);
                        dlg.AcceptButton = btnOk;
                        dlg.CancelButton = btnCancel;

                        txt.Focus();
                        var descResult = dlg.ShowDialog(this);
                        if (descResult == DialogResult.OK)
                        {
                            richTextBoxDescription.Text = txt.Text.Trim();
                        }
                        else if (descResult == DialogResult.Yes)
                        {
                            // Continue intentionally with empty description.
                        }
                        else
                        {
                            richTextBoxDescription?.Focus();
                            return;
                        }
                    }
                }

                bool canStart = await PrepareSessionLimitsAndValidateStartAsync();
                if (!canStart)
                {
                    return;
                }

                bool liveExists = await CheckIfLiveSignalExistsAsync();
//                 DebugLogger.Log($"[Form1] Live signal existence check complete: {liveExists}");
                if (liveExists)
                {
                    UpdateRichTextBox("[i] Existing live signal detected.\r\n");

//                     DebugLogger.Log("[Form1] Showing MessageBox");
                    DialogResult result = MessageBox.Show(
                        "A previous live log is still active. What do you want to do?\n\n" +
                        "Yes: Add stop time and start new live\n" +
                        "No: Delete the previous log and start new\n" +
                        "Cancel: Cancel starting new live log.",
                        "Live Signal Detected",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question
                    );

                    if (result == DialogResult.Cancel)
                    {
                        UpdateRichTextBox("[INFO] Operation cancelled.\r\n");
                        return;
                    }
                    else if (result == DialogResult.No)
                    {
                        try
                        {
                            await DeletePreviousLiveSignalAsync();
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                            UpdateRichTextBox("[ERROR] Could not delete: " + ex.Message + "\r\n");
                            return;
                        }
                    }
                    else if (result == DialogResult.Yes)
                    {
                        try
                        {
                            string manualWorkingTime = "00:00:00";
                            using (var timeForm = new FormWorkingTimeInput())
                            {
                                if (timeForm.ShowDialog() == DialogResult.OK)
                                    manualWorkingTime = timeForm.WorkingTime;
                                else
                                    return;
                            }
                            await AddStopTimeToLiveSignalAsync(manualWorkingTime);
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                            UpdateRichTextBox("[ERROR] Could not stop old log: " + ex.Message + "\r\n");
                            return;
                        }

                        StartNewLiveLog();
                        try
                        {
                            await SendWorkStartedAsync(
                                richTextBoxDescription?.Text ?? "",
                                labelStartTime.Text,
                                "00:00:00"
                            );
                            UpdateRichTextBox("[i] New LIVE signal sent!\r\n");
                            _sessionStartedByPlatform = "Windows";
                            await SetActiveSessionAsync();
                            await RefreshOnlineStatusAsync();
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                            UpdateRichTextBox("[ERROR] Failed to send LIVE signal: " + ex.Message + "\r\n");
                        }
                        return;
                    }
                }

                // Normal start
                StartNewLiveLog();
                try
                {
                    await SendWorkStartedAsync(
                        richTextBoxDescription?.Text ?? "",
                        labelStartTime.Text,
                        "00:00:00"
                    );
                    UpdateRichTextBox("[i] LIVE signal sent!\r\n");
                    _sessionStartedByPlatform = "Windows";
                    await SetActiveSessionAsync();
                    await RefreshOnlineStatusAsync();
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                    UpdateRichTextBox("[ERROR] Failed to send LIVE signal: " + ex.Message + "\r\n");
                }
            }
            finally
            {
                // Only re-enable if the timer did NOT start (cancelled/error)
                if (_workingTimer == null || !_workingTimer.Enabled)
                {
                    buttonStart.Enabled = true;
                }
            }
        }

        private sealed class WorkedHoursSnapshot
        {
            public double TodayHours { get; set; }
            public double WeekHours { get; set; }
        }

        private double GetDailyWorkLimitHours()
        {
            var team = UserStorage.LoadTeam();
            if (team != null && team.DailyWorkingLimitHours > 0)
                return team.DailyWorkingLimitHours;
            return 6.0;
        }

        private double GetWeeklyWorkLimitHours()
        {
            var team = UserStorage.LoadTeam();
            if (team != null && team.WeeklyWorkingLimitHours > 0)
                return team.WeeklyWorkingLimitHours;

            if (_currentUser != null && _currentUser.WeeklyHourLimit > 0)
                return _currentUser.WeeklyHourLimit;

            return 10.0;
        }

        private bool AskDailyLimitContinueOverride(double dailyLimitHours, double currentTodayHours)
        {
            using (var dlg = new Form())
            {
                dlg.Text = "Daily Limit";
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(520, 205);
                dlg.BackColor = isDarkMode ? Color.FromArgb(28, 34, 44) : Color.FromArgb(248, 250, 252);

                var lblTitle = new Label
                {
                    Text = "Daily work limit reached",
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    AutoSize = true,
                    Location = new Point(16, 14),
                    ForeColor = isDarkMode ? Color.FromArgb(248, 113, 113) : Color.FromArgb(185, 28, 28)
                };

                var lblMsg = new Label
                {
                    AutoSize = false,
                    Width = 488,
                    Height = 104,
                    Location = new Point(16, 45),
                    Font = new Font("Segoe UI", 9.5f),
                    ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59),
                    Text =
                        $"You reached the max daily working limit ({dailyLimitHours:0.#}h)." + Environment.NewLine +
                        $"Current today total: {currentTodayHours:0.##}h." + Environment.NewLine + Environment.NewLine +
                        "If you continue, you accept health risk on your own responsibility." + Environment.NewLine +
                        "Do you understand and want to continue?"
                };

                var btnContinue = new Button
                {
                    Text = "Continue (I Understand)",
                    Size = new Size(190, 34),
                    Location = new Point(118, 160),
                    BackColor = Color.FromArgb(192, 57, 43),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    DialogResult = DialogResult.Yes
                };
                btnContinue.FlatAppearance.BorderSize = 0;

                var btnStop = new Button
                {
                    Text = "Ignore / Stop",
                    Size = new Size(140, 34),
                    Location = new Point(320, 160),
                    BackColor = isDarkMode ? Color.FromArgb(71, 85, 105) : Color.FromArgb(148, 163, 184),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    DialogResult = DialogResult.No
                };
                btnStop.FlatAppearance.BorderSize = 0;

                dlg.Controls.Add(lblTitle);
                dlg.Controls.Add(lblMsg);
                dlg.Controls.Add(btnContinue);
                dlg.Controls.Add(btnStop);
                dlg.AcceptButton = btnContinue;
                dlg.CancelButton = btnStop;

                return dlg.ShowDialog(this) == DialogResult.Yes;
            }
        }

        private async Task<bool> PrepareSessionLimitsAndValidateStartAsync()
        {
            try
            {
                var snapshot = await GetCurrentUserWorkedHoursSnapshotAsync();
                _sessionTodayHoursAtStart = snapshot.TodayHours;
                _sessionWeekHoursAtStart = snapshot.WeekHours;
                _sessionLimitSnapshotReady = true;
                _continuous2hWarned = false;
                _continuous4hWarned = false;
                _weeklyLimitWarnedInSession = false;
                _dailyLimitReachedInSession = false;
                _dailyLimitOverrideAccepted = false;

                double dailyLimit = GetDailyWorkLimitHours();
                if (snapshot.TodayHours >= dailyLimit - 0.0001)
                {
                    _dailyLimitReachedInSession = true;
                    bool continueWork = AskDailyLimitContinueOverride(dailyLimit, snapshot.TodayHours);
                    if (!continueWork)
                    {
                        string dailyMsg = $"You have reached max daily working limit ({dailyLimit:0.#}h). Timer start cancelled.";
                        ShowClockNotice("Daily Limit Reached", dailyMsg, ToolTipIcon.Warning);
                        return false;
                    }

                    _dailyLimitOverrideAccepted = true;
                    ShowClockNotice("Daily Limit Override", "Continuing work on user risk acceptance.", ToolTipIcon.Warning);
                }

                double weeklyLimit = GetWeeklyWorkLimitHours();
                if (snapshot.WeekHours >= weeklyLimit - 0.0001)
                {
                    _weeklyLimitWarnedInSession = true;
                    string weeklyMsg = $"You have reached weekly working limit ({weeklyLimit:0.#}h).";
                    ShowClockNotice("Weekly Limit Reached", weeklyMsg, ToolTipIcon.Warning);
                    MessageBox.Show(weeklyMsg, "Weekly Limit", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                return true;
            }
            catch
            {
                // If fetching limits fails, do not block start to keep app responsive offline.
                _sessionTodayHoursAtStart = 0;
                _sessionWeekHoursAtStart = 0;
                _sessionLimitSnapshotReady = false;
                _continuous2hWarned = false;
                _continuous4hWarned = false;
                _weeklyLimitWarnedInSession = false;
                _dailyLimitReachedInSession = false;
                _dailyLimitOverrideAccepted = false;
                return true;
            }
        }

        private async Task<WorkedHoursSnapshot> GetCurrentUserWorkedHoursSnapshotAsync()
        {
            var snapshot = new WorkedHoursSnapshot();
            string currentUserName = _currentUser?.Name ?? "";
            if (string.IsNullOrWhiteSpace(currentUserName))
                return snapshot;

            string fbBase = UserStorage.GetFirebaseBaseUrl();
            string logsUrl = $"{fbBase}/logs.json";
            var response = await _httpClient.GetAsync(logsUrl);
            if (!response.IsSuccessStatusCode)
                return snapshot;

            string json = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(json) || json == "null")
                return snapshot;

            var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntryWithIndex>>(json);
            if (logsDict == null)
                return snapshot;

            DateTime today = DateTime.Today;
            int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            DateTime weekStart = today.AddDays(-daysSinceMonday);
            DateTime weekEnd = weekStart.AddDays(6);

            foreach (var log in logsDict.Values)
            {
                if (log == null || string.IsNullOrWhiteSpace(log.userName) || string.IsNullOrWhiteSpace(log.timestamp))
                    continue;

                if (!log.userName.Equals(currentUserName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!DateTime.TryParse(log.timestamp, out var logDate))
                    continue;

                double workedHours = ParseWorkingTimeToHours(log.workingTime);
                if (workedHours <= 0)
                    continue;

                if (logDate.Date == today)
                    snapshot.TodayHours += workedHours;

                if (logDate.Date >= weekStart && logDate.Date <= weekEnd)
                    snapshot.WeekHours += workedHours;
            }

            return snapshot;
        }

        private void ShowClockNotice(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            try
            {
                if (_clockNoticeToolTip == null)
                {
                    _clockNoticeToolTip = new ToolTip
                    {
                        IsBalloon = true,
                        ToolTipIcon = ToolTipIcon.Info,
                        AutoPopDelay = 7000,
                        InitialDelay = 100,
                        ReshowDelay = 100
                    };
                }

                _clockNoticeToolTip.ToolTipTitle = string.IsNullOrWhiteSpace(title) ? "Work Notice" : title;
                _clockNoticeToolTip.ToolTipIcon = icon;

                Point popupPoint = new Point(
                    Math.Max(8, labelTimerNow.Left - 4),
                    Math.Max(8, labelTimerNow.Bottom + 8));
                _clockNoticeToolTip.Show(message, this, popupPoint, 7000);

                if (_trayInitialized && _trayIcon != null)
                {
                    _trayIcon.BalloonTipTitle = string.IsNullOrWhiteSpace(title) ? "Work Notice" : title;
                    _trayIcon.BalloonTipText = message;
                    _trayIcon.BalloonTipIcon = icon;
                    _trayIcon.ShowBalloonTip(7000);
                }
            }
            catch
            {
            }
        }

        private void StartNewLiveLog()
        {
            // Initialize new live work session: set start time and begin timer
//             DebugLogger.Log($"[Form1] StartNewLiveLog: ENTRY - Starting new live log for user \'{_currentUser?.Name}\'");
//             DebugLogger.Log("[Form1] StartNewLiveLog: ENTRY - Starting new live log for timer");
            _startTime = DateTime.Now;
//             DebugLogger.Log($"[Form1] Timer started at {_startTime:HH:mm:ss}");
            labelStartTime.Text = _startTime.ToString("HH:mm:ss");
            _elapsedTime = TimeSpan.Zero;
            labelWorkingTime.Text = "00:00:00";
            _continuous2hWarned = false;
            _continuous4hWarned = false;
            _weeklyLimitWarnedInSession = false;
            // Keep daily-limit flags as prepared by pre-start validation.
            // This prevents duplicate "Daily Limit" popup after user already pressed Continue.
            if (!_sessionLimitSnapshotReady)
            {
                _sessionTodayHoursAtStart = 0;
                _sessionWeekHoursAtStart = 0;
                _sessionLimitSnapshotReady = true;
            }

            if (_workingTimer == null)
            {
                _workingTimer = new Timer();
                _workingTimer.Interval = 1000;
                _workingTimer.Tick += WorkingTimer_Tick;
            }
//             DebugLogger.Log("[Form1] Timer started - work session begin");
                _workingTimer.Start();
            _workStartedAt = DateTime.Now;
            UpdateRichTextBox("[i] Timer started for " + _currentUser.Name + "\r\n");

            // -- Capture current weekly hours as base for live progress bar update --
            var myCtrl = onlineUserControls.FirstOrDefault(c =>
                c.UserInfo.Name.Equals(_currentUser.Name, StringComparison.OrdinalIgnoreCase));
            _baseWeeklyHoursAtStart = myCtrl?.GetWeeklyHours() ?? 0;

            // -- DISABLE START BUTTON & BEGIN ANIMATION --
//             DebugLogger.Log("[Form1] Setting start button to Working state");
            SetStartButtonWorking(true);
            UpdateMainClockVisualState();
        }

        private void WorkingTimer_Tick(object sender, EventArgs e)
        {
            // Increment elapsed time and update display
//             DebugLogger.Log("[Form1] WorkingTimer_Tick: Timer tick - incrementing elapsed time");
            _elapsedTime = _elapsedTime.Add(TimeSpan.FromSeconds(1));
            labelWorkingTime.Text = _elapsedTime.ToString(@"hh\:mm\:ss");
            ApplyContinuousWorkNotificationsAndLimits();
            UpdateMainClockVisualState();
        }

        private void ApplyContinuousWorkNotificationsAndLimits()
        {
            if (_workingTimer == null || !_workingTimer.Enabled)
                return;

            if (!_continuous2hWarned && _elapsedTime.TotalHours >= 2)
            {
                _continuous2hWarned = true;
                ShowClockNotice("Pause Reminder", "You have worked for 2 hours. Make coffee pause.", ToolTipIcon.Info);
            }

            if (!_continuous4hWarned && _elapsedTime.TotalHours >= 4)
            {
                _continuous4hWarned = true;
                ShowClockNotice("Break Reminder", "You have worked 4 hours. Make break.", ToolTipIcon.Warning);
            }

            if (_sessionLimitSnapshotReady)
            {
                double dailyLimit = GetDailyWorkLimitHours();
                double todayNow = _sessionTodayHoursAtStart + _elapsedTime.TotalHours;
                if (!_dailyLimitReachedInSession && todayNow >= dailyLimit)
                {
                    _dailyLimitReachedInSession = true;
                    bool continueWork = AskDailyLimitContinueOverride(dailyLimit, todayNow);
                    if (continueWork)
                    {
                        _dailyLimitOverrideAccepted = true;
                        ShowClockNotice("Daily Limit Override", "Continue mode active. Clock pulse indicates risk.", ToolTipIcon.Warning);
                    }
                    else
                    {
                        ShowClockNotice("Daily Limit Reached", "Timer stopped by user after limit warning.", ToolTipIcon.Warning);
                        BeginInvoke(new Action(() => buttonStop_Click(buttonStop, EventArgs.Empty)));
                        return;
                    }
                }

                double weeklyLimit = GetWeeklyWorkLimitHours();
                double weekNow = _sessionWeekHoursAtStart + _elapsedTime.TotalHours;
                if (!_weeklyLimitWarnedInSession && weekNow >= weeklyLimit)
                {
                    _weeklyLimitWarnedInSession = true;
                    ShowClockNotice("Weekly Limit Reached", "You have reached weekly working limit.", ToolTipIcon.Warning);
                }
            }
        }

        private void UpdateMainClockVisualState()
        {
            if (labelTimerNow == null) return;

            if (_workingTimer != null && _workingTimer.Enabled)
            {
                if (_dailyLimitReachedInSession && _dailyLimitOverrideAccepted)
                {
                    // Strong warning state while user continues after acknowledging risk.
                    double phase = (Environment.TickCount & int.MaxValue) * 0.014;
                    double t = (Math.Sin(phase) + 1.0) / 2.0;
                    var orange = Color.FromArgb(245, 158, 11);
                    var red = Color.FromArgb(239, 68, 68);
                    int r = (int)(orange.R + (red.R - orange.R) * t);
                    int g = (int)(orange.G + (red.G - orange.G) * t);
                    int b = (int)(orange.B + (red.B - orange.B) * t);
                    labelTimerNow.ForeColor = Color.FromArgb(r, g, b);
                }
                else if (_elapsedTime.TotalHours >= 4)
                {
                    labelTimerNow.ForeColor = Color.FromArgb(239, 68, 68);
                }
                else if (_elapsedTime.TotalHours >= 2)
                {
                    labelTimerNow.ForeColor = Color.FromArgb(245, 158, 11);
                }
                else
                {
                    labelTimerNow.ForeColor = Color.FromArgb(34, 197, 94);
                }
            }
            else
            {
                labelTimerNow.ForeColor = isDarkMode ? ThemeConstants.Dark.TextPrimary : ThemeConstants.Light.TextPrimary;
            }
        }

        private void UpdateOwnProgressBarImmediatelyAfterStop(TimeSpan workedThisSession)
        {
            try
            {
                var myCtrl = onlineUserControls.FirstOrDefault(c =>
                    c.UserInfo.Name.Equals(_currentUser.Name, StringComparison.OrdinalIgnoreCase));
                if (myCtrl == null)
                    return;

                DateTime today = DateTime.Today;
                int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                DateTime weekStart = today.AddDays(-daysSinceMonday);
                double holiday = PublicHolidays.GetHolidayHoursInWeek(myCtrl.UserInfo.Country, weekStart);

                // Deterministic local value:
                // 1) Prefer weekly snapshot from session start (captured before start).
                // 2) Fallback to live control value if snapshot missing.
                double baseHours = _sessionLimitSnapshotReady ? _sessionWeekHoursAtStart : myCtrl.GetWeeklyHours();
                if (baseHours < 0)
                    baseHours = 0;

                double localNow = Math.Max(0, baseHours + workedThisSession.TotalHours);
                myCtrl.SetWeeklyHours(localNow, holiday);
                myCtrl.UpdateLocalTime();
            }
            catch { }
        }

        private double GetOwnWeeklyHoursSafe()
        {
            try
            {
                var myCtrl = onlineUserControls.FirstOrDefault(c =>
                    c.UserInfo.Name.Equals(_currentUser.Name, StringComparison.OrdinalIgnoreCase));
                return Math.Max(0, myCtrl?.GetWeeklyHours() ?? 0);
            }
            catch
            {
                return 0;
            }
        }

        private void UpdateOwnProgressBarFromSnapshotAfterStop(double baseWeekHours, TimeSpan workedThisSession)
        {
            try
            {
                var myCtrl = onlineUserControls.FirstOrDefault(c =>
                    c.UserInfo.Name.Equals(_currentUser.Name, StringComparison.OrdinalIgnoreCase));
                if (myCtrl == null)
                    return;

                DateTime today = DateTime.Today;
                int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                DateTime weekStart = today.AddDays(-daysSinceMonday);
                double holiday = PublicHolidays.GetHolidayHoursInWeek(myCtrl.UserInfo.Country, weekStart);

                double localNow = Math.Max(0, baseWeekHours + workedThisSession.TotalHours);
                myCtrl.SetWeeklyHours(localNow, holiday);
                myCtrl.UpdateLocalTime();
            }
            catch { }
        }

        private void RefreshOwnProgressUiAfterStop(double baseWeekHours, TimeSpan workedThisSession)
        {
            UpdateOwnProgressBarFromSnapshotAfterStop(baseWeekHours, workedThisSession);
            try
            {
                var myCtrl = onlineUserControls.FirstOrDefault(c =>
                    c.UserInfo.Name.Equals(_currentUser.Name, StringComparison.OrdinalIgnoreCase));
                myCtrl?.Invalidate();
                myCtrl?.Refresh();
                panelOnlineUsers?.Invalidate();
                panelOnlineUsers?.Refresh();
            }
            catch { }
        }

        private async Task ForceProgressRefreshAfterStopAsync()
        {
            // Ensure UI reflects latest saved log right after Stop.
            // We do one immediate refresh plus one short delayed retry for eventual Firebase consistency.
            try { await RefreshWeeklyHoursAsync(); } catch { }
            try { await Task.Delay(300); } catch { }
            try { await RefreshWeeklyHoursAsync(); } catch { }
        }

        private async Task RefreshAfterStopLikeManualAsync(double expectedWeekHours)
        {
            // Mirror manual refresh behavior (status + weekly hours),
            // but retry briefly until backend updates are visible.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try { await RefreshOnlineStatusAsync(); } catch { }
                try { await RefreshWeeklyHoursAsync(); } catch { }

                double uiHours = GetOwnWeeklyHoursSafe();
                if (uiHours + 0.0005 >= expectedWeekHours)
                    break;

                try { await Task.Delay(350); } catch { }
            }

            try
            {
                panelOnlineUsers?.Invalidate();
                panelOnlineUsers?.Refresh();
            }
            catch { }
        }

        // ============================================================
        // START BUTTON ? WORKING STATE ANIMATION
        // When timer is running: button shows "[i] Working..." with
        // animated dots and a pulsing amber color. Disabled for clicks.
        // When timer stops: button restores to green "?  START".
        // ============================================================
        private void SetStartButtonWorking(bool isWorking)
        {
            bool useCircularStartButton = buttonStart.Width == buttonStart.Height;
            int circularIconSize = Math.Max(24, buttonStart.Width - 4);

            if (isWorking)
            {
                _startBtnOriginalColor = buttonStart.BackColor;
                _startBtnOriginalText = buttonStart.Text;

                // Working state: smooth breathing pulse with animated dots
                buttonStart.Enabled = false;
                buttonStart.Text = useCircularStartButton ? string.Empty : "  Working";
                buttonStart.Image = ThemeConstants.CreatePlayIcon(useCircularStartButton ? circularIconSize : 14, Color.White);
                buttonStart.TextImageRelation = useCircularStartButton
                    ? TextImageRelation.Overlay
                    : TextImageRelation.ImageBeforeText;
                buttonStart.BackColor = ThemeConstants.Dark.AccentPrimary;
                buttonStart.FlatAppearance.BorderColor = ThemeConstants.Dark.AccentHover;
                buttonStart.FlatAppearance.BorderSize = 2;
                buttonStart.ForeColor = Color.White;
                buttonStart.Cursor = Cursors.Default;
                _startBtnAnimDots = 0;
                _startBtnPulsePhase = 0f;

                if (_startBtnAnimTimer == null)
                {
                    _startBtnAnimTimer = new Timer();
                    _startBtnAnimTimer.Tick += StartBtnAnimTimer_Tick;
                }
                // Fast tick for smooth sine-wave breathing (30fps)
                _startBtnAnimTimer.Interval = 33;
                _startBtnAnimTimer.Start();
            }
            else
            {
                _startBtnAnimTimer?.Stop();

                buttonStart.Enabled = true;
                ThemeConstants.StyleActionButton(buttonStart, isDarkMode, true);
                if (useCircularStartButton)
                {
                    buttonStart.Text = string.Empty;
                    buttonStart.Image = ThemeConstants.CreatePlayIcon(circularIconSize, Color.White);
                    buttonStart.TextImageRelation = TextImageRelation.Overlay;
                }
                _startBtnOriginalColor = buttonStart.BackColor;
                _startBtnOriginalText = buttonStart.Text;
                buttonStart.Cursor = Cursors.Hand;
            }
        }

        private void StartBtnAnimTimer_Tick(object sender, EventArgs e)
        {
            bool useCircularStartButton = buttonStart.Width == buttonStart.Height;

            // Smooth sine-wave breathing pulse for "Working..." state
            // Phase advances ~0.08 rad per tick at 33ms = full cycle in ~2.5s
            _startBtnPulsePhase += 0.08f;
            if (_startBtnPulsePhase > 2f * (float)Math.PI)
            {
                _startBtnPulsePhase -= 2f * (float)Math.PI;
                // Update dots text only once per full cycle (every ~2.5s)
                _startBtnAnimDots = (_startBtnAnimDots + 1) % 4;
            }

            // Sine wave maps 0..1 for smooth breathing
            float t = (float)(Math.Sin(_startBtnPulsePhase) + 1f) / 2f;

            // Pulse between warm amber and brighter amber-gold
            var cFrom = ThemeConstants.Dark.AccentPrimary;  // 232,133,74
            var cTo   = Color.FromArgb(245, 170, 80);       // brighter warm gold
            int r = (int)(cFrom.R + (cTo.R - cFrom.R) * t);
            int g = (int)(cFrom.G + (cTo.G - cFrom.G) * t);
            int b = (int)(cFrom.B + (cTo.B - cFrom.B) * t);
            buttonStart.BackColor = Color.FromArgb(r, g, b);

            // Border also breathes subtly
            var bFrom = ThemeConstants.Dark.AccentMuted;
            var bTo   = ThemeConstants.Dark.AccentHover;
            int br = (int)(bFrom.R + (bTo.R - bFrom.R) * t);
            int bg2 = (int)(bFrom.G + (bTo.G - bFrom.G) * t);
            int bb = (int)(bFrom.B + (bTo.B - bFrom.B) * t);
            buttonStart.FlatAppearance.BorderColor = Color.FromArgb(br, bg2, bb);

            if (!useCircularStartButton)
            {
                // Animated dots text
                string dots = new string('.', _startBtnAnimDots);
                string newText = "  Working" + dots;
                if (buttonStart.Text != newText)
                    buttonStart.Text = newText;
            }

            // Keep clock pulse smooth while in daily-limit override warning mode.
            UpdateMainClockVisualState();
        }

        // ============================================================
        // STOP BUTTON
        // ============================================================
        private async void buttonStop_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[Form1] buttonStop_Click: ENTRY - Stop button clicked");
            buttonStop.Enabled = false;
            try
            {
                UpdateRichTextBox("[DEBUG] Stop button clicked.\r\n");

                if (_workingTimer == null || !_workingTimer.Enabled)
                {
                    UpdateRichTextBox("[i] Timer was not running.\r\n");
                    return;
                }

//                 DebugLogger.Log("[Form1] Timer stopped - work session ended");
                _workingTimer.Stop();
                UpdateRichTextBox("[i] Timer stopped at: " + labelWorkingTime.Text + "\r\n");

                TimeSpan stoppedSessionElapsed = _elapsedTime;
                double currentUiWeekHours = GetOwnWeeklyHoursSafe();
                double stoppedBaseWeekHours = _sessionLimitSnapshotReady
                    ? Math.Max(_sessionWeekHoursAtStart, Math.Max(0, currentUiWeekHours - stoppedSessionElapsed.TotalHours))
                    : currentUiWeekHours;
                double expectedWeekHoursAfterStop = Math.Max(0, stoppedBaseWeekHours + stoppedSessionElapsed.TotalHours);

                // Update progress instantly on Stop (no waiting for Firebase roundtrip).
                RefreshOwnProgressUiAfterStop(stoppedBaseWeekHours, stoppedSessionElapsed);

                // -- RE-ENABLE START BUTTON & STOP ANIMATION --
//                 DebugLogger.Log("[Form1] Setting start button to Stopped state");
            SetStartButtonWorking(false);
                _sessionLimitSnapshotReady = false;
                _continuous2hWarned = false;
                _continuous4hWarned = false;
                _weeklyLimitWarnedInSession = false;
                _dailyLimitReachedInSession = false;
                _dailyLimitOverrideAccepted = false;
                UpdateMainClockVisualState();

                if (string.IsNullOrWhiteSpace(currentLiveLogKey))
                {
                    UpdateRichTextBox("[i] No cached live log key found. Trying to recover it.\r\n");
                    await RecoverCurrentLiveLogKeyAsync();
                }

                await StopCurrentLiveLogAsync();
                await RefreshAfterStopLikeManualAsync(expectedWeekHoursAfterStop);
                // Keep deterministic final fallback in case remote refresh lags/fails.
                RefreshOwnProgressUiAfterStop(stoppedBaseWeekHours, stoppedSessionElapsed);
                RefreshLogs();

                // Auto-log to Jira if description contains issue key
                TimeSpan worked = DateTime.Now - _workStartedAt;
                await TryLogToJiraAsync(richTextBoxDescription?.Text, worked);

                // If user started from a sticker (double-click), ask if the task is finished
                if (_stickerBoard?.ActiveWorkingSticker != null)
                {
                    string stickerTitle = _stickerBoard.ActiveWorkingSticker.title ?? "(no title)";
//                     DebugLogger.Log("[Form1] Showing MessageBox");
                    var finishedResult = MessageBox.Show(
                        $"Is the task \"{stickerTitle}\" finished?",
                        "Task Completed?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (finishedResult == DialogResult.Yes)
                    {
                        await _stickerBoard.MarkActiveWorkingStickerDoneAsync();
                        UpdateRichTextBox($"\u2705 Sticker \"{stickerTitle}\" marked as finished.\r\n");
                    }
                    else
                    {
                        _stickerBoard.ClearActiveWorkingSticker();
                    }
                }

                // User-requested behavior: run the same logic as pressing Refresh.
                if (buttonRefresh != null && !buttonRefresh.IsDisposed)
                    buttonRefresh_Click(buttonRefresh, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox("[ERROR] Stop operation failed: " + ex.Message + "\r\n");
            }
            finally
            {
                buttonStop.Enabled = true;
            }
        }

        private LogEntry BuildCurrentStoppedLogEntry(string workTime)
        {
            return new LogEntry
            {
                description = richTextBoxDescription?.Text ?? "",
                startTime = labelStartTime?.Text ?? "",
                workingTime = workTime ?? "",
                timestamp = DateTime.UtcNow.ToString("o"),
                status = "Stopped",
                userId = GetCurrentUserId(),
                userName = _currentUser?.Name ?? GetCurrentUserId(),
                project = _cmbProject?.SelectedItem?.ToString() ?? "General",
                platform = "desktop"
            };
        }

        private async Task StopCurrentLiveLogAsync()
        {
//             DebugLogger.Log("[Form1] StopCurrentLiveLogAsync: ENTRY - Stopping live log");
            try
            {
                UpdateRichTextBox("[DEBUG] StopCurrentLiveLogAsync started.\r\n");

                string workTime = labelWorkingTime.Text;
                bool desktopStopped = false;
                string liveKeyBeforeStop = currentLiveLogKey;
                LogEntry stopEntry = BuildCurrentStoppedLogEntry(workTime);
                string localCacheKey = UpsertStoppedLogInLocalCache(stopEntry, liveKeyBeforeStop);

                // -- STOP DESKTOP'S OWN LIVE LOG --
                if (!string.IsNullOrWhiteSpace(liveKeyBeforeStop))
                {
                    var data = new
                    {
                        workingTime = workTime,
                        status = "Stopped",
                        timestamp = DateTime.UtcNow.ToString("o")
                    };

                    string url = firebaseUrl.Replace(".json", "/" + liveKeyBeforeStop + ".json");
                    UpdateRichTextBox("[DEBUG] Stop URL: " + url + "\r\n");

                    var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                    {
                        Content = new StringContent(
                            JsonConvert.SerializeObject(data),
                            Encoding.UTF8,
                            "application/json")
                    };

                    var response = await _httpClient.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        UpdateRichTextBox("[i] Desktop live log stopped.\r\n");
                        desktopStopped = true;
                    }
                    else
                    {
                        UpdateRichTextBox("[i] Failed to stop desktop live log. Status: " + response.StatusCode + "\r\n");
                    }
                }

                // -- ALSO STOP ANY MOBILE "Working" LOGS FOR THIS USER --
                // This handles cross-device stop: pressing Stop on desktop also stops phone's session
                try
                {
                    var firebase = GetFirebaseClient();
                    var logs = await firebase.Child("logs").OnceAsync<LogEntry>();
                    var mobileLiveLogs = logs.Where(log =>
                        IsCurrentUserLog(log.Object) &&
                        !string.IsNullOrEmpty(log.Object.status) &&
                        log.Object.status.Equals("Working", StringComparison.OrdinalIgnoreCase) &&
                        log.Key != currentLiveLogKey // Don't double-stop the desktop one
                    );

                    foreach (var mobileLog in mobileLiveLogs)
                    {
                        await firebase.Child("logs").Child(mobileLog.Key).PatchAsync(new
                        {
                            workingTime = mobileLog.Object.workingTime ?? workTime,
                            status = "Stopped",
                            timestamp = DateTime.UtcNow.ToString("o")
                        });
                        UpdateRichTextBox($"[i] Mobile live log stopped: {mobileLog.Key}\r\n");
                    }
                }
                catch (Exception mex)
                {
                    UpdateRichTextBox($"[i] Could not stop mobile logs: {mex.Message}\r\n");
                }

                // Clear active session so mobile app detects stop and stops its timer
                await ClearActiveSessionAsync();
                _sessionStartedByPlatform = null;

                if (!desktopStopped)
                {
                    QueuePendingStopLog(liveKeyBeforeStop, localCacheKey, stopEntry);
                    UpdateRichTextBox("[i] Saved stop log locally. Will sync when Firebase is available.\r\n");
                }

                // Immediately publish "Online" after stopping work so status shows
                // "Online - idle" instead of briefly falling to "Offline".
                await SendOnlineHeartbeatAsync();
                await TryFlushPendingStopLogsAsync();

                // Clear the key if desktop log was stopped
                if (desktopStopped)
                {
                    UpdateRichTextBox("[i] Live log stopped successfully.\r\n");
                    RemovePendingStopLog(liveKeyBeforeStop, localCacheKey);
                    currentLiveLogKey = null;
                }
                else if (string.IsNullOrWhiteSpace(currentLiveLogKey))
                {
                    UpdateRichTextBox("[i] No desktop live log key was set.\r\n");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox("[ERROR] StopCurrentLiveLogAsync failed: " + ex.Message + "\r\n");
            }
        }

        private async Task RecoverCurrentLiveLogKeyAsync()
        {
//             DebugLogger.Log("[Form1] RecoverCurrentLiveLogKeyAsync: ENTRY - Recovering live log key");
            try
            {
                UpdateRichTextBox("[DEBUG] Recovering active live log key from Firebase...\r\n");

                var firebase = GetFirebaseClient();
                var logs = await firebase.Child("logs").OnceAsync<LogEntry>();

                var liveLog = logs.FirstOrDefault(log =>
                    IsCurrentUserLog(log.Object) &&
                    !string.IsNullOrWhiteSpace(log.Object.status) &&
                    log.Object.status.Equals("Working", StringComparison.OrdinalIgnoreCase));

                if (liveLog != null)
                {
                    currentLiveLogKey = liveLog.Key;
                    UpdateRichTextBox($"[i] Recovered live log key: {currentLiveLogKey}\r\n");
                }
                else
                {
                    UpdateRichTextBox("[i] No live Firebase record found to recover.\r\n");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox($"[ERROR] Failed to recover live key: {ex.Message}\r\n");
            }
        }

        // ============================================================
        // SEND DATA TO FIREBASE
        // ============================================================
        public async Task SendDataToFirebaseAsync(string description, string startTime, string workingTime)
        {
//             DebugLogger.Log($"[Form1] SendDataToFirebaseAsync: ENTRY - Sending data for \'{description}\' at {startTime}");
            var data = new
            {
                description = description ?? "",
                startTime = startTime ?? "",
                workingTime = workingTime ?? "",
                timestamp = DateTime.UtcNow.ToString("o"),
                userId = GetCurrentUserId(),
                userName = _currentUser.Name
            };

            string json = JsonConvert.SerializeObject(data);

            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(firebaseUrl, content);

                if (response.IsSuccessStatusCode)
                    UpdateRichTextBox("[i] Data sent successfully!\r\n");
                else
                    UpdateRichTextBox("[i] Failed. Status: " + response.StatusCode + "\r\n");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox("[i] Exception: " + ex.Message + "\r\n");
            }
        }

        public async Task SendWorkStartedAsync(string description, string startTime, string workingTime)
        {
//             DebugLogger.Log($"[Form1] SendWorkStartedAsync: ENTRY - Sending work started for \'{description}\'");
            string statusValue = (workingTime == "00:00:00") ? "Working" : "Stopped";
            UpdateRichTextBox($"[DEBUG] Sending status '{statusValue}' for user '{_currentUser.Name}'\r\n");

            var startedEntry = new LogEntry
            {
                description = description ?? "",
                startTime = startTime ?? "",
                workingTime = workingTime ?? "",
                timestamp = DateTime.UtcNow.ToString("o"),
                status = statusValue,
                userId = GetCurrentUserId(),
                userName = _currentUser.Name,
                project = _cmbProject?.SelectedItem?.ToString() ?? "General",
                platform = "desktop"
            };

            // Always persist a local provisional entry first to avoid data loss if Firebase is offline.
            string provisionalKey = $"LOCAL_WORKING_{Guid.NewGuid():N}";
            UpsertLocalLogByKey(provisionalKey, startedEntry);

            string json = JsonConvert.SerializeObject(startedEntry);

            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(firebaseUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    var responseObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseBody);
                    if (responseObj != null && responseObj.ContainsKey("name"))
                    {
                        currentLiveLogKey = responseObj["name"];
                        RemoveLocalLogByKey(provisionalKey);
                        UpsertLocalLogByKey(currentLiveLogKey, startedEntry);
                        UpdateRichTextBox($"[i] Live log key saved: {currentLiveLogKey}\r\n");
                    }
                    UpdateRichTextBox("[i] Work started log sent.\r\n");
                }
                else
                {
                    // Keep local provisional key so STOP can safely finalize and queue sync.
                    currentLiveLogKey = provisionalKey;
                    UpdateRichTextBox($"[i] Failed. Status: {response.StatusCode}\r\n");
                    UpdateRichTextBox("[i] Start saved locally. Will sync on next refresh/stop.\r\n");
                }
            }
            catch (Exception ex)
            {
                currentLiveLogKey = provisionalKey;
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox("[i] Exception: " + ex.Message + "\r\n");
                UpdateRichTextBox("[i] Start saved locally. Will sync on next refresh/stop.\r\n");
            }
        }

        // ============================================================
        // ACTIVE SESSION SYNC ? cross-device start/stop
        // Writes to /activeSession/{userId} in Firebase so mobile
        // and desktop can detect each other's timer state.
        // ============================================================

        /// <summary>
        /// Writes the active session so the mobile app can detect it and start its timer.
        /// Called when the user presses START on desktop.
        /// </summary>
        private async Task SetActiveSessionAsync()
        {
            try
            {
                string baseUrl = UserStorage.GetFirebaseBaseUrl();
                string userId = GetCurrentUserId();
                string url = $"{baseUrl}/activeSession/{userId}.json";

                var data = new
                {
                    userName = _currentUser.Name,
                    description = richTextBoxDescription?.Text ?? "",
                    startTime = labelStartTime.Text,
                    startTimestamp = DateTime.UtcNow.ToString("o"),
                    logKey = currentLiveLogKey ?? "",
                    platform = "Windows",
                    project = _cmbProject?.SelectedItem?.ToString() ?? "General",
                    status = "Working"
                };

                string json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PutAsync(url, content);
//                 DebugLogger.Log("[Form1] Active session written to Firebase for cross-device sync");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Error setting active session: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes the active session when the user presses STOP.
        /// Mobile app will detect this and stop its timer.
        /// </summary>
        private async Task ClearActiveSessionAsync()
        {
            try
            {
                string baseUrl = UserStorage.GetFirebaseBaseUrl();
                string userId = GetCurrentUserId();
                string url = $"{baseUrl}/activeSession/{userId}.json";
                await _httpClient.DeleteAsync(url);
//                 DebugLogger.Log("[Form1] Active session cleared from Firebase");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Error clearing active session: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the active session for the current user from Firebase.
        /// Used to detect if the mobile app started a session that desktop should sync to.
        /// </summary>
        private async Task<Dictionary<string, string>> GetActiveSessionAsync()
        {
            try
            {
                string baseUrl = UserStorage.GetFirebaseBaseUrl();
                string userId = GetCurrentUserId();
                string url = $"{baseUrl}/activeSession/{userId}.json";

                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (json != "null" && !string.IsNullOrEmpty(json))
                    {
                        return JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Error getting active session: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Polls Firebase every 10 seconds. If mobile started a session, auto-starts desktop timer.
        /// If mobile stopped the session, auto-stops desktop timer.
        /// </summary>
        private async Task CheckRemoteSessionSync()
        {
            if (_currentUser == null) return;

            try
            {
                var session = await GetActiveSessionAsync();

                if (session != null && session.ContainsKey("status") && session["status"] == "Working")
                {
                    // There IS an active session in Firebase
                    bool timerRunning = _workingTimer != null && _workingTimer.Enabled;
                    if (!timerRunning)
                    {
                        // We are NOT running locally ? another device started it, sync up!
                        this.BeginInvoke((Action)(() => SyncStartFromRemoteSession(session)));
                    }
                }
                else
                {
                    // No active session in Firebase
                    bool timerRunning = _workingTimer != null && _workingTimer.Enabled;
                    if (timerRunning && _sessionStartedByPlatform != null && _sessionStartedByPlatform != "Windows")
                    {
                        // We were running because of a remote session, but it was stopped remotely
                        this.BeginInvoke((Action)(() => SyncStopFromRemoteSession()));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Error checking remote session sync: {ex.Message}");
            }
        }

        /// <summary>
        /// Starts the local timer to match a session that was started on mobile.
        /// </summary>
        private void SyncStartFromRemoteSession(Dictionary<string, string> session)
        {
            bool timerRunning = _workingTimer != null && _workingTimer.Enabled;
            if (timerRunning) return;

//             DebugLogger.Log("[Form1] Syncing timer from remote session (mobile start detected)");

            // Calculate elapsed time from the original start
            DateTime remoteStart = DateTime.Now;
            if (session.ContainsKey("startTimestamp") &&
                DateTime.TryParse(session["startTimestamp"], null, System.Globalization.DateTimeStyles.RoundtripKind, out var utcStart))
            {
                remoteStart = utcStart.ToLocalTime();
            }
            else if (session.ContainsKey("startTime") && DateTime.TryParse(session["startTime"], out var localParsed))
            {
                remoteStart = localParsed;
            }

            _startTime = remoteStart;
            _elapsedTime = DateTime.Now - _startTime;
            if (_elapsedTime < TimeSpan.Zero) _elapsedTime = TimeSpan.Zero;

            // Get log key and platform from session
            session.TryGetValue("logKey", out string logKey);
            session.TryGetValue("platform", out string platform);
            session.TryGetValue("description", out string description);

            currentLiveLogKey = logKey;
            _sessionStartedByPlatform = platform ?? "Mobile";

            // Update UI
            labelStartTime.Text = _startTime.ToString("HH:mm:ss");
            labelWorkingTime.Text = _elapsedTime.ToString(@"hh\:mm\:ss");

            if (!string.IsNullOrEmpty(description) && richTextBoxDescription != null)
                richTextBoxDescription.Text = description;

            // Start local timer
            if (_workingTimer == null)
            {
                _workingTimer = new Timer();
                _workingTimer.Interval = 1000;
                _workingTimer.Tick += WorkingTimer_Tick;
            }
            _workingTimer.Start();
            _workStartedAt = DateTime.Now;

            // Update button state
            SetStartButtonWorking(true);
            _sessionLimitSnapshotReady = false;
            _continuous2hWarned = _elapsedTime.TotalHours >= 2;
            _continuous4hWarned = _elapsedTime.TotalHours >= 4;
            _weeklyLimitWarnedInSession = false;
            _dailyLimitReachedInSession = false;
            _dailyLimitOverrideAccepted = false;
            UpdateMainClockVisualState();

            UpdateRichTextBox($"Timer synced from {platform}: started at {_startTime:HH:mm:ss}\r\n");
        }

        /// <summary>
        /// Stops the local timer because the remote device stopped the session.
        /// </summary>
        private void SyncStopFromRemoteSession()
        {
            bool timerRunning = _workingTimer != null && _workingTimer.Enabled;
            if (!timerRunning) return;

//             DebugLogger.Log("[Form1] Stopping timer ? remote session ended (mobile stop detected)");

            _workingTimer.Stop();
            _sessionStartedByPlatform = null;

            SetStartButtonWorking(false);
            _sessionLimitSnapshotReady = false;
            _continuous2hWarned = false;
            _continuous4hWarned = false;
            _weeklyLimitWarnedInSession = false;
            _dailyLimitReachedInSession = false;
            _dailyLimitOverrideAccepted = false;
            UpdateMainClockVisualState();
            currentLiveLogKey = null;

            UpdateRichTextBox("Timer stopped by remote device sync\r\n");
        }

        private async void SendData(object sender, EventArgs e)
        {
            await SendDataToFirebaseAsync(
                richTextBoxDescription?.Text,
                labelStartTime?.Text,
                labelWorkingTime?.Text
            );
        }

        // ============================================================
        // DATAGRIDVIEW & REFRESH
        // ============================================================
        // ============================================================
        // USER FILTER DROPDOWN ? shows next to grid buttons
        // ============================================================
        private void SetupUserFilterComboBox()
        {
            // Setup combo box for filtering logs by user
//             DebugLogger.Log("[Form1] SetupUserFilterComboBox: ENTRY - Setting up user filter");
            comboBoxUserFilter.Items.Clear();

            // Everyone gets "My Work" and "Team"
            comboBoxUserFilter.Items.Add("My Work");
            comboBoxUserFilter.Items.Add("Team");

            // Admin also gets "All Users" and individual user names
            if (_currentUser.IsAdmin)
            {
                comboBoxUserFilter.Items.Add("All Users");
                if (_allUsers != null)
                {
                    foreach (var user in _allUsers)
                    {
                        if (user.Name != _currentUser.Name)
                            comboBoxUserFilter.Items.Add(user.Name);
                    }
                }
            }

            comboBoxUserFilter.SelectedIndex = 0; // default to "My Work"

            // Position: above the grid, right-aligned next to version label area
            comboBoxUserFilter.Location = new Point(720, 350);
            comboBoxUserFilter.Size = new Size(140, 28);
            comboBoxUserFilter.Font = new System.Drawing.Font("Segoe UI", 9, FontStyle.Bold);
            comboBoxUserFilter.FlatStyle = FlatStyle.Flat;
            comboBoxUserFilter.SelectedIndexChanged += (s, e) => RefreshLogs();

            // Theme will be applied by ApplyTheme()
        }

        // --- DATA GRID ? RIGHT-CLICK CONTEXT MENU AND AUTO-REFRESH ---
        private void InitializeDataGridFeatures()
        {
            // Initialize data grid view with context menu and auto-refresh
//             DebugLogger.Log("[Form1] InitializeDataGridFeatures: ENTRY - Initializing grid features");
            dataGridView1.DataBindingComplete += DataGridView1_DataBindingComplete;
            RefreshLogs();

            // Right-click context menu for the data grid
            var gridContextMenu = new ContextMenuStrip();

            var gridEditItem = new ToolStripMenuItem("Edit Entry");
            gridEditItem.Click += (s, e) => EditSelectedEntry();
            gridContextMenu.Items.Add(gridEditItem);

            var gridDeleteItem = new ToolStripMenuItem("Delete Entry");
            gridDeleteItem.Click += (s, e) => buttonDelete_Click(s, e);
            gridContextMenu.Items.Add(gridDeleteItem);

            gridContextMenu.Items.Add(new ToolStripSeparator());

            var gridRefreshItem = new ToolStripMenuItem("Refresh");
            gridRefreshItem.Click += (s, e) => RefreshLogs();
            gridContextMenu.Items.Add(gridRefreshItem);

            dataGridView1.ContextMenuStrip = gridContextMenu;
            dataGridView1.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    var hitTest = dataGridView1.HitTest(e.X, e.Y);
                    if (hitTest.RowIndex >= 0)
                    {
                        dataGridView1.ClearSelection();
                        dataGridView1.Rows[hitTest.RowIndex].Selected = true;
                    }
                }
            };

            autoRefreshTimer = new System.Windows.Forms.Timer();
            autoRefreshTimer.Interval = 300000;
            autoRefreshTimer.Tick += (s, e) => RefreshLogs();
            autoRefreshTimer.Start();
        }

        private void StyleDataGridView()
        {
            // Apply modern styling to the data grid view
//             DebugLogger.Log("[Form1] StyleDataGridView: ENTRY - Applying grid styling");
            // -- Modern flat styling --
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dataGridView1.GridColor = isDarkMode ? Color.FromArgb(50, 56, 68) : Color.FromArgb(225, 228, 235);
            dataGridView1.BackgroundColor = isDarkMode ? Color.FromArgb(26, 30, 38) : Color.FromArgb(248, 249, 252);
            dataGridView1.DefaultCellStyle.SelectionBackColor = isDarkMode ? Color.FromArgb(55, 90, 140) : Color.FromArgb(210, 228, 255);
            dataGridView1.DefaultCellStyle.SelectionForeColor = isDarkMode ? Color.White : Color.FromArgb(30, 30, 50);

            // Header styling
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = isDarkMode ? Color.FromArgb(34, 40, 52) : Color.FromArgb(240, 242, 248);
            dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = isDarkMode ? Color.FromArgb(180, 190, 210) : Color.FromArgb(80, 90, 110);
            dataGridView1.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI Semibold", 9.5f);
            dataGridView1.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            dataGridView1.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dataGridView1.ColumnHeadersHeight = 38;

            // Row styling
            dataGridView1.DefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9.5f);
            dataGridView1.DefaultCellStyle.ForeColor = isDarkMode ? Color.FromArgb(220, 224, 235) : Color.FromArgb(40, 45, 60);
            dataGridView1.DefaultCellStyle.BackColor = isDarkMode ? Color.FromArgb(26, 30, 38) : Color.White;
            dataGridView1.DefaultCellStyle.Padding = new Padding(6, 3, 6, 3);
            dataGridView1.RowTemplate.Height = 34;

            // Alternating rows
            dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = isDarkMode ? Color.FromArgb(32, 38, 50) : Color.FromArgb(245, 247, 252);
            dataGridView1.AlternatingRowsDefaultCellStyle.ForeColor = isDarkMode ? Color.FromArgb(220, 224, 235) : Color.FromArgb(40, 45, 60);

            // Column sizing
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            if (dataGridView1.Columns.Contains("Nr"))
            {
                dataGridView1.Columns["Nr"].Width = 44;
                dataGridView1.Columns["Nr"].HeaderText = "#";
                dataGridView1.Columns["Nr"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            if (dataGridView1.Columns.Contains("userName"))
            {
                dataGridView1.Columns["userName"].Width = 110;
                dataGridView1.Columns["userName"].HeaderText = "User";
            }

            if (dataGridView1.Columns.Contains("project"))
            {
                dataGridView1.Columns["project"].Width = 105;
                dataGridView1.Columns["project"].HeaderText = "Project";
            }

            if (dataGridView1.Columns.Contains("description"))
            {
                dataGridView1.Columns["description"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                dataGridView1.Columns["description"].HeaderText = "Description";
            }

            if (dataGridView1.Columns.Contains("startTime"))
            {
                dataGridView1.Columns["startTime"].Width = 80;
                dataGridView1.Columns["startTime"].HeaderText = "Start";
            }

            if (dataGridView1.Columns.Contains("workingTime"))
            {
                dataGridView1.Columns["workingTime"].Width = 80;
                dataGridView1.Columns["workingTime"].HeaderText = "Duration";
            }

            if (dataGridView1.Columns.Contains("timestamp"))
            {
                dataGridView1.Columns["timestamp"].Width = 110;
                dataGridView1.Columns["timestamp"].HeaderText = "Date";
            }

            if (dataGridView1.Columns.Contains("status"))
            {
                dataGridView1.Columns["status"].Width = 78;
                dataGridView1.Columns["status"].HeaderText = "Status";
                dataGridView1.Columns["status"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }

            // Hide internal Key column
            if (dataGridView1.Columns.Contains("Key"))
                dataGridView1.Columns["Key"].Visible = false;
        }

        private bool _isRefreshing = false;
        private const string LogsCacheFileName = "logs_local.json";
        private const string PendingStopLogsFileName = "pending_stop_logs.json";
        private const string PendingLogUpsertsFileName = "pending_log_upserts.json";
        private const string PendingLogDeletesFileName = "pending_log_deletes.json";

        private sealed class PendingStopLogItem
        {
            public string LiveLogKey { get; set; }
            public string LocalCacheKey { get; set; }
            public LogEntry Entry { get; set; }
        }

        private sealed class PendingLogUpsertItem
        {
            public string RemoteKey { get; set; }
            public LogEntry Entry { get; set; }
        }

        private sealed class PendingLogDeleteItem
        {
            public string RemoteKey { get; set; }
            public string DeletedUtc { get; set; }
        }

        // Wrapper for backward compatibility (auto-refresh timer, combobox change, etc.)
        private async void RefreshLogs()
        {
            await RefreshLogsAsync();
        }

        // Core refresh method ? returns Task so callers can await it
        private async Task RefreshLogsAsync(bool forceRefresh = false)
        {
//             DebugLogger.Log("[Form1] RefreshLogsAsync: ENTRY - Refresh logs async called");
            if (_isRefreshing && !forceRefresh) return;
            _isRefreshing = true;

            UpdateRichTextBox("[DEBUG] Refreshing logs from database...\r\n");
            try { await TryFlushPendingLogUpsertsAsync(); } catch { }
            try { await TryFlushPendingStopLogsAsync(); } catch { }
            try { await TryFlushPendingLogDeletesAsync(); } catch { }
            var localLogs = LoadLocalLogsCache();
            localLogs = MergePendingStopLogsIntoDictionary(localLogs);
            localLogs = MergePendingLogUpsertsIntoDictionary(localLogs);
            localLogs = ApplyPendingLogDeletesToDictionary(localLogs);

            if ((dataGridView1.DataSource == null || dataGridView1.Rows.Count == 0) && localLogs.Count > 0)
                ApplyLogsToGrid(localLogs);

            try
            {
                // Always fetch fresh data from Firebase with cache-busting
                string url = firebaseUrl.Contains("?")
                    ? $"{firebaseUrl}&_cb={DateTime.UtcNow.Ticks}"
                    : $"{firebaseUrl}?_cb={DateTime.UtcNow.Ticks}";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };

                var response = await _httpClient.SendAsync(request);
                UpdateRichTextBox($"[DEBUG] Refresh response status: {response.StatusCode}\r\n");

                if (!response.IsSuccessStatusCode)
                {
                    UpdateRichTextBox($"[i] Refresh failed. HTTP: {response.StatusCode}\r\n");
                    if (localLogs.Count > 0)
                        UpdateRichTextBox("[i] Using local cached logs.\r\n");
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                UpdateRichTextBox($"[DEBUG] Refresh JSON length: {json?.Length ?? 0}\r\n");

                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    if (localLogs.Count > 0)
                    {
                        UpdateRichTextBox("[i] Firebase returned empty. Keeping local cached logs.\r\n");
                        ApplyLogsToGrid(localLogs);
                        return;
                    }

                    UpdateRichTextBox("[i] No logs found in Firebase.\r\n");
                    dataGridView1.DataSource = null;
                    currentLiveLogKey = null;
                    return;
                }

//                 DebugLogger.Log("[Form1] Deserializing Firebase logs from JSON");
                var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json);
                if (logsDict == null || logsDict.Count == 0)
                {
                    if (localLogs.Count > 0)
                    {
                        UpdateRichTextBox("[i] Remote logs were empty after deserialize. Keeping local cache.\r\n");
                        ApplyLogsToGrid(localLogs);
                        return;
                    }

                    UpdateRichTextBox("[i] Deserialized logs dictionary is empty.\r\n");
                    dataGridView1.DataSource = null;
                    currentLiveLogKey = null;
                    return;
                }

                logsDict = MergePendingStopLogsIntoDictionary(logsDict);
                logsDict = MergePendingLogUpsertsIntoDictionary(logsDict);
                logsDict = ApplyPendingLogDeletesToDictionary(logsDict);

                // ── SYNC ACCEPTANCE: Check if Firebase modified YOUR logs ──
                var syncChanges = SyncChangeDetector.DetectLogChanges(localLogs, logsDict, _currentUser?.Name);
                if (SyncChangeDetector.HasSignificantChanges(syncChanges))
                {
                    // Show dialog on UI thread — let user accept/reject changes
                    HashSet<string> acceptedKeys = null;
                    bool rejectedAll = false;

                    this.Invoke(new Action(() =>
                    {
                        using (var form = new SyncAcceptanceForm(syncChanges))
                        {
                            form.ShowDialog(this);
                            acceptedKeys = form.AcceptedKeys;
                            rejectedAll = form.RejectedAll;
                        }
                    }));

                    if (rejectedAll)
                    {
                        // User chose to keep local — DON'T overwrite with Firebase data
                        // Re-apply pending merges to local and save
                        var keptLocal = MergePendingStopLogsIntoDictionary(localLogs);
                        keptLocal = MergePendingLogUpsertsIntoDictionary(keptLocal);
                        SaveLocalLogsCache(keptLocal);
                        ApplyLogsToGrid(keptLocal);
                        UpdateRichTextBox("[i] Sync changes rejected — keeping local data.\r\n");
                        return;
                    }

                    if (acceptedKeys != null && acceptedKeys.Count < syncChanges.Count)
                    {
                        // Partial acceptance — revert unaccepted changes to local version
                        foreach (var change in syncChanges)
                        {
                            if (!acceptedKeys.Contains(change.RecordKey) && localLogs.ContainsKey(change.RecordKey))
                            {
                                logsDict[change.RecordKey] = localLogs[change.RecordKey];
                            }
                        }
                    }
                }

                SaveLocalLogsCache(logsDict);
                ApplyLogsToGrid(logsDict);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                if (localLogs.Count > 0)
                {
                    UpdateRichTextBox("[i] Refresh failed. Keeping local cached logs.\r\n");
                    ApplyLogsToGrid(localLogs);
                }
                UpdateRichTextBox($"[ERROR] Refresh failed: {ex.Message}\r\n");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private Dictionary<string, LogEntry> LoadLocalLogsCache()
        {
            return TeamLocalCacheStore.LoadDictionary<LogEntry>(LogsCacheFileName);
        }

        private void SaveLocalLogsCache(Dictionary<string, LogEntry> logsDict)
        {
            TeamLocalCacheStore.SaveDictionary(LogsCacheFileName, logsDict);
        }

        private List<PendingStopLogItem> LoadPendingStopLogs()
        {
            return TeamLocalCacheStore.LoadList<PendingStopLogItem>(PendingStopLogsFileName);
        }

        private void SavePendingStopLogs(List<PendingStopLogItem> pending)
        {
            TeamLocalCacheStore.SaveList(PendingStopLogsFileName, pending ?? new List<PendingStopLogItem>());
        }

        private List<PendingLogUpsertItem> LoadPendingLogUpserts()
        {
            return TeamLocalCacheStore.LoadList<PendingLogUpsertItem>(PendingLogUpsertsFileName);
        }

        private void SavePendingLogUpserts(List<PendingLogUpsertItem> pending)
        {
            TeamLocalCacheStore.SaveList(PendingLogUpsertsFileName, pending ?? new List<PendingLogUpsertItem>());
        }

        private List<PendingLogDeleteItem> LoadPendingLogDeletes()
        {
            return TeamLocalCacheStore.LoadList<PendingLogDeleteItem>(PendingLogDeletesFileName);
        }

        private void SavePendingLogDeletes(List<PendingLogDeleteItem> pending)
        {
            TeamLocalCacheStore.SaveList(PendingLogDeletesFileName, pending ?? new List<PendingLogDeleteItem>());
        }

        private void UpsertLocalLogByKey(string key, LogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(key) || entry == null) return;
            var localLogs = LoadLocalLogsCache();
            localLogs[key] = entry;
            SaveLocalLogsCache(localLogs);
        }

        private void RemoveLocalLogByKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var localLogs = LoadLocalLogsCache();
            if (localLogs.Remove(key))
                SaveLocalLogsCache(localLogs);
        }

        private void QueuePendingLogUpsert(string remoteKey, LogEntry entry)
        {
            if (string.IsNullOrWhiteSpace(remoteKey) || entry == null) return;

            var pending = LoadPendingLogUpserts();
            pending.RemoveAll(p => string.Equals(p.RemoteKey, remoteKey, StringComparison.OrdinalIgnoreCase));
            pending.Add(new PendingLogUpsertItem
            {
                RemoteKey = remoteKey,
                Entry = entry
            });
            SavePendingLogUpserts(pending);
        }

        private void RemovePendingLogUpsert(string remoteKey)
        {
            if (string.IsNullOrWhiteSpace(remoteKey)) return;

            var pending = LoadPendingLogUpserts();
            int before = pending.Count;
            pending.RemoveAll(p => string.Equals(p.RemoteKey, remoteKey, StringComparison.OrdinalIgnoreCase));
            if (pending.Count != before)
                SavePendingLogUpserts(pending);
        }

        private void QueuePendingLogDelete(string remoteKey)
        {
            if (string.IsNullOrWhiteSpace(remoteKey)) return;

            var pendingDeletes = LoadPendingLogDeletes();
            pendingDeletes.RemoveAll(p => string.Equals(p.RemoteKey, remoteKey, StringComparison.OrdinalIgnoreCase));
            pendingDeletes.Add(new PendingLogDeleteItem
            {
                RemoteKey = remoteKey,
                DeletedUtc = DateTime.UtcNow.ToString("o")
            });
            SavePendingLogDeletes(pendingDeletes);
        }

        private void RemovePendingLogDelete(string remoteKey)
        {
            if (string.IsNullOrWhiteSpace(remoteKey)) return;

            var pendingDeletes = LoadPendingLogDeletes();
            int before = pendingDeletes.Count;
            pendingDeletes.RemoveAll(p => string.Equals(p.RemoteKey, remoteKey, StringComparison.OrdinalIgnoreCase));
            if (pendingDeletes.Count != before)
                SavePendingLogDeletes(pendingDeletes);
        }

        private string UpsertStoppedLogInLocalCache(LogEntry stopEntry, string preferredKey)
        {
            var localLogs = LoadLocalLogsCache();
            string key = preferredKey;

            if (string.IsNullOrWhiteSpace(key))
            {
                var existing = localLogs
                    .Where(x => x.Value != null && IsCurrentUserLog(x.Value) &&
                                string.Equals(x.Value.status, "Working", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x =>
                    {
                        DateTime dt;
                        return DateTime.TryParse(x.Value.timestamp, out dt) ? dt : DateTime.MinValue;
                    })
                    .FirstOrDefault();

                key = !string.IsNullOrWhiteSpace(existing.Key)
                    ? existing.Key
                    : $"LOCAL_{Guid.NewGuid():N}";
            }

            localLogs[key] = stopEntry;
            SaveLocalLogsCache(localLogs);
            return key;
        }

        private void QueuePendingStopLog(string liveLogKey, string localCacheKey, LogEntry entry)
        {
            if (entry == null) return;

            var pending = LoadPendingStopLogs();
            pending.RemoveAll(p =>
                (!string.IsNullOrWhiteSpace(liveLogKey) &&
                 string.Equals(p.LiveLogKey, liveLogKey, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(localCacheKey) &&
                 string.Equals(p.LocalCacheKey, localCacheKey, StringComparison.OrdinalIgnoreCase)));

            pending.Add(new PendingStopLogItem
            {
                LiveLogKey = liveLogKey,
                LocalCacheKey = localCacheKey,
                Entry = entry
            });

            SavePendingStopLogs(pending);
        }

        private void RemovePendingStopLog(string liveLogKey, string localCacheKey)
        {
            var pending = LoadPendingStopLogs();
            int before = pending.Count;
            pending.RemoveAll(p =>
                (!string.IsNullOrWhiteSpace(liveLogKey) &&
                 string.Equals(p.LiveLogKey, liveLogKey, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(localCacheKey) &&
                 string.Equals(p.LocalCacheKey, localCacheKey, StringComparison.OrdinalIgnoreCase)));

            if (pending.Count != before)
                SavePendingStopLogs(pending);
        }

        private Dictionary<string, LogEntry> MergePendingStopLogsIntoDictionary(Dictionary<string, LogEntry> source)
        {
            var merged = source ?? new Dictionary<string, LogEntry>(StringComparer.OrdinalIgnoreCase);
            var pending = LoadPendingStopLogs();
            if (pending.Count == 0) return merged;

            foreach (var item in pending)
            {
                if (item?.Entry == null) continue;
                string key = !string.IsNullOrWhiteSpace(item.LocalCacheKey)
                    ? item.LocalCacheKey
                    : (!string.IsNullOrWhiteSpace(item.LiveLogKey) ? item.LiveLogKey : $"LOCAL_{Guid.NewGuid():N}");

                if (!merged.ContainsKey(key))
                    merged[key] = item.Entry;
            }

            return merged;
        }

        private Dictionary<string, LogEntry> MergePendingLogUpsertsIntoDictionary(Dictionary<string, LogEntry> source)
        {
            var merged = source ?? new Dictionary<string, LogEntry>(StringComparer.OrdinalIgnoreCase);
            var pending = LoadPendingLogUpserts();
            if (pending.Count == 0) return merged;

            foreach (var item in pending)
            {
                if (item?.Entry == null || string.IsNullOrWhiteSpace(item.RemoteKey))
                    continue;
                merged[item.RemoteKey] = item.Entry;
            }

            return merged;
        }

        private Dictionary<string, LogEntry> ApplyPendingLogDeletesToDictionary(Dictionary<string, LogEntry> source)
        {
            var merged = source ?? new Dictionary<string, LogEntry>(StringComparer.OrdinalIgnoreCase);
            var pendingDeletes = LoadPendingLogDeletes();
            if (pendingDeletes.Count == 0) return merged;

            foreach (var item in pendingDeletes)
            {
                if (string.IsNullOrWhiteSpace(item?.RemoteKey))
                    continue;
                merged.Remove(item.RemoteKey);
            }

            return merged;
        }

        private async Task TryFlushPendingStopLogsAsync()
        {
            var pending = LoadPendingStopLogs();
            if (pending.Count == 0) return;

            var remaining = new List<PendingStopLogItem>();

            foreach (var item in pending)
            {
                if (item?.Entry == null)
                    continue;

                bool synced = false;
                string resolvedRemoteKey = item.LiveLogKey;

                try
                {
                    if (!string.IsNullOrWhiteSpace(item.LiveLogKey))
                    {
                        var patchData = new
                        {
                            workingTime = item.Entry.workingTime ?? "",
                            status = "Stopped",
                            timestamp = item.Entry.timestamp ?? DateTime.UtcNow.ToString("o"),
                            description = item.Entry.description ?? "",
                            project = item.Entry.project ?? "General"
                        };

                        string patchUrl = firebaseUrl.Replace(".json", $"/{item.LiveLogKey}.json");
                        var req = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl)
                        {
                            Content = new StringContent(
                                JsonConvert.SerializeObject(patchData),
                                Encoding.UTF8,
                                "application/json")
                        };
                        var patchResp = await _httpClient.SendAsync(req);
                        synced = patchResp.IsSuccessStatusCode;
                    }

                    if (!synced)
                    {
                        var postContent = new StringContent(
                            JsonConvert.SerializeObject(item.Entry),
                            Encoding.UTF8,
                            "application/json");
                        var postResp = await _httpClient.PostAsync(firebaseUrl, postContent);
                        synced = postResp.IsSuccessStatusCode;

                        if (synced)
                        {
                            string body = await postResp.Content.ReadAsStringAsync();
                            var obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(body);
                            if (obj != null && obj.ContainsKey("name"))
                                resolvedRemoteKey = obj["name"];
                        }
                    }
                }
                catch
                {
                    synced = false;
                }

                if (synced)
                {
                    var localLogs = LoadLocalLogsCache();
                    if (!string.IsNullOrWhiteSpace(item.LocalCacheKey) &&
                        !string.IsNullOrWhiteSpace(resolvedRemoteKey) &&
                        !string.Equals(item.LocalCacheKey, resolvedRemoteKey, StringComparison.OrdinalIgnoreCase))
                    {
                        localLogs.Remove(item.LocalCacheKey);
                    }

                    string cacheKey = !string.IsNullOrWhiteSpace(resolvedRemoteKey) ? resolvedRemoteKey : item.LocalCacheKey;
                    if (!string.IsNullOrWhiteSpace(cacheKey))
                    {
                        localLogs[cacheKey] = item.Entry;
                        SaveLocalLogsCache(localLogs);
                    }
                }
                else
                {
                    remaining.Add(item);
                }
            }

            SavePendingStopLogs(remaining);
        }

        private async Task TryFlushPendingLogUpsertsAsync()
        {
            var pending = LoadPendingLogUpserts();
            if (pending.Count == 0) return;

            var remaining = new List<PendingLogUpsertItem>();
            foreach (var item in pending)
            {
                if (item?.Entry == null || string.IsNullOrWhiteSpace(item.RemoteKey))
                    continue;

                bool synced = false;
                try
                {
                    string putUrl = firebaseUrl.Replace(".json", $"/{item.RemoteKey}.json");
                    var payload = new StringContent(
                        JsonConvert.SerializeObject(item.Entry),
                        Encoding.UTF8,
                        "application/json");
                    var resp = await _httpClient.PutAsync(putUrl, payload);
                    synced = resp.IsSuccessStatusCode;
                }
                catch
                {
                    synced = false;
                }

                if (!synced)
                    remaining.Add(item);
            }

            SavePendingLogUpserts(remaining);
        }

        private async Task TryFlushPendingLogDeletesAsync()
        {
            var pendingDeletes = LoadPendingLogDeletes();
            if (pendingDeletes.Count == 0) return;

            var remaining = new List<PendingLogDeleteItem>();
            foreach (var item in pendingDeletes)
            {
                if (string.IsNullOrWhiteSpace(item?.RemoteKey))
                    continue;

                bool deleted = false;
                try
                {
                    string deleteUrl = firebaseUrl.Replace(".json", $"/{item.RemoteKey}.json");
                    var resp = await _httpClient.DeleteAsync(deleteUrl);
                    deleted = resp.IsSuccessStatusCode;
                }
                catch
                {
                    deleted = false;
                }

                if (!deleted)
                    remaining.Add(item);
            }

            SavePendingLogDeletes(remaining);
        }

        private void ApplyLogsToGrid(Dictionary<string, LogEntry> logsDict)
        {
            if (logsDict == null || logsDict.Count == 0)
                return;

            UpdateRichTextBox($"[DEBUG] Total raw log entries: {logsDict.Count}\r\n");

            var liveLogPair = logsDict.FirstOrDefault(x =>
                x.Value != null &&
                x.Value.userId == GetCurrentUserId() &&
                !string.IsNullOrWhiteSpace(x.Value.status) &&
                x.Value.status.Equals("Working", StringComparison.OrdinalIgnoreCase));

            currentLiveLogKey = string.IsNullOrWhiteSpace(liveLogPair.Key) ? null : liveLogPair.Key;

            if (!string.IsNullOrWhiteSpace(currentLiveLogKey))
                UpdateRichTextBox($"[i] Active live key detected: {currentLiveLogKey}\r\n");
            else
                UpdateRichTextBox("[i] No active live key detected for current user.\r\n");

            DateTime dateFrom = _dtpFrom?.Value.Date ?? DateTime.MinValue;
            DateTime dateTo = (_dtpTo?.Value.Date ?? DateTime.Today).AddDays(1);

            var filteredLogs = logsDict
                .Select(x => new { Key = x.Key, Log = x.Value })
                .Where(x => x.Log != null)
                .Where(x => !string.Equals(x.Log.status, "Online", StringComparison.OrdinalIgnoreCase))
                .Where(x =>
                {
                    DateTime dt;
                    if (DateTime.TryParse(x.Log.timestamp, out dt))
                        return dt >= dateFrom && dt < dateTo;
                    return true;
                })
                .OrderByDescending(x =>
                {
                    DateTime dt;
                    return DateTime.TryParse(x.Log.timestamp, out dt) ? dt : DateTime.MinValue;
                })
                .Select((x, idx) =>
                {
                    string rawName = x.Log.userName ?? x.Log.userId ?? "Unknown";
                    rawName = StripJoinCodePrefix(rawName);
                    if (!string.IsNullOrEmpty(rawName))
                    {
                        char[] seps = { '_', ' ', '-', '.' };
                        foreach (char sep in seps)
                        {
                            int si = rawName.IndexOf(sep);
                            if (si > 0 && si < rawName.Length - 1)
                            {
                                string suffix = rawName.Substring(si + 1);
                                if (_allUsers.Any(u => string.Equals(u.Name, suffix, StringComparison.OrdinalIgnoreCase)))
                                {
                                    rawName = suffix;
                                    break;
                                }
                            }
                        }
                    }

                    return new LogEntryWithIndex
                    {
                        Nr = idx + 1,
                        userName = rawName,
                        project = x.Log.project ?? "General",
                        description = x.Log.description ?? "",
                        startTime = x.Log.startTime ?? "",
                        workingTime = x.Log.workingTime ?? "",
                        timestamp = ParseDate(x.Log.timestamp),
                        status = x.Log.status ?? "",
                        Key = x.Key
                    };
                })
                .ToList();

            string selectedFilter = comboBoxUserFilter?.SelectedItem?.ToString() ?? "My Work";

            if (selectedFilter == "My Work")
            {
                filteredLogs = filteredLogs
                    .Where(l => string.Equals(l.userName, _currentUser.Name, StringComparison.OrdinalIgnoreCase))
                    .Select((l, idx) => { l.Nr = idx + 1; return l; })
                    .ToList();
                UpdateRichTextBox($"[DEBUG] My Work: filtered for '{_currentUser.Name}': {filteredLogs.Count}\r\n");
            }
            else if (selectedFilter == "Team" || selectedFilter == "All Users")
            {
                UpdateRichTextBox($"[DEBUG] Showing all team logs. Visible: {filteredLogs.Count}\r\n");
            }
            else
            {
                filteredLogs = filteredLogs
                    .Where(l => l.userName == selectedFilter)
                    .Select((l, idx) => { l.Nr = idx + 1; return l; })
                    .ToList();
                UpdateRichTextBox($"[DEBUG] Filtered logs for user '{selectedFilter}': {filteredLogs.Count}\r\n");
            }

            dataGridView1.DataSource = null;
            dataGridView1.DataSource = filteredLogs;

            if (dataGridView1.Columns.Contains("Key"))
                dataGridView1.Columns["Key"].Visible = false;

            StyleDataGridView();
            ApplyTheme();

            UpdateRichTextBox($"[i] Grid updated with {filteredLogs.Count} entries.\r\n");
        }

        private string settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorkTimeCounter", "settings.json");

        private void SaveSettings()
        {
            // Save application settings (dark mode) to file
//             DebugLogger.Log("[Form1] SaveSettings: ENTRY - Saving settings");
            try
            {
                string dir = Path.GetDirectoryName(settingsFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var settings = new { darkMode = isDarkMode };
                File.WriteAllText(settingsFilePath, JsonConvert.SerializeObject(settings));
            }
            catch { }
        }

        private void LoadSettings()
        {
            // Load application settings from file
//             DebugLogger.Log("[Form1] LoadSettings: ENTRY - Loading settings");
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    var settings = JsonConvert.DeserializeAnonymousType(json, new { darkMode = true });
                    isDarkMode = settings.darkMode;
                }
            }
            catch { }

            // Load custom theme (if enabled, it overrides dark/light)
            try
            {
                var ct = CustomTheme.LoadActive();
                if (ct != null && ct.Enabled)
                    _customTheme = ct;
                else
                    _customTheme = null;
            }
            catch { _customTheme = null; }
        }

        private void DataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            // Apply conditional formatting to rows (highlight "Working" status in red)
//             DebugLogger.Log($"[Form1] DataGridView1_DataBindingComplete: Formatting {dataGridView1.Rows.Count} rows");
            if (_customTheme != null && _customTheme.Enabled)
            {
                RecolorDataGridRows();
                return;
            }

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                var status = row.Cells["status"].Value?.ToString();
                if (status == "Working")
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(231, 76, 60);
                    row.DefaultCellStyle.ForeColor = Color.White;
                }
                else
                {
                    if (isDarkMode)
                    {
                        row.DefaultCellStyle.BackColor = row.Index % 2 == 0
                            ? Color.FromArgb(47, 61, 74)
                            : Color.FromArgb(39, 51, 62);
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(236, 240, 241);
                    }
                    else
                    {
                        row.DefaultCellStyle.BackColor = row.Index % 2 == 0
                            ? Color.FromArgb(245, 245, 250)
                            : Color.FromArgb(225, 228, 234);
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
                    }
                }
            }
        }

        private string ParseDate(string isoDate)
        {
            // Parse ISO date string to dd-MMM-yyyy format
//             DebugLogger.Log($"[Form1] ParseDate: Parsing date \'{isoDate}\'");
            if (DateTime.TryParse(isoDate, out DateTime dt))
                return dt.ToString("dd-MMM-yyyy");
            return "";
        }

        // ============================================================
        // REPORT
        // ============================================================
        private void buttonReport_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[Form1] buttonReport_Click: ENTRY - Generate report button clicked");
            try
            {
                bool isAdmin = _currentUser.IsAdmin;

                List<DataGridViewRow> rowsToPrint = new List<DataGridViewRow>();

                if (isAdmin)
                {
//                     DebugLogger.Log("[Form1] Showing MessageBox");
                    DialogResult choice = MessageBox.Show(
                        "Print ALL users' logs?\n\nYes = All users\nNo = Only your logs",
                        "Admin Print Options",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (choice == DialogResult.Cancel) return;

                    if (choice == DialogResult.Yes)
                    {
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                            if (!row.IsNewRow) rowsToPrint.Add(row);
                    }
                    else
                    {
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                        {
                            if (row.IsNewRow) continue;
                            string name = row.Cells["userName"]?.Value?.ToString() ?? "";
                            if (name == _currentUser.Name)
                                rowsToPrint.Add(row);
                        }
                    }
                }
                else
                {
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        if (row.IsNewRow) continue;
                        string name = row.Cells["userName"]?.Value?.ToString() ?? "";
                        if (name == _currentUser.Name)
                            rowsToPrint.Add(row);
                    }
                }

                if (rowsToPrint.Count == 0)
                {
//                     DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show("No logs found to print.", "Empty Report",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Report_{_currentUser.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                Document doc = new Document(PageSize.A4.Rotate());
                PdfWriter.GetInstance(doc, new FileStream(sfd.FileName, FileMode.Create));
                doc.Open();

                // Team name header
                var teamInfo = UserStorage.LoadTeam();
                string teamDisplayName = teamInfo?.TeamName ?? "Unknown Team";

                var teamFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, new BaseColor(100, 120, 140));
                doc.Add(new Paragraph($"Team: {teamDisplayName}", teamFont));

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                string title = isAdmin && rowsToPrint.Count > 0
                    ? "Work Time Report ? All Users"
                    : $"Work Time Report ? {_currentUser.Name}";
                doc.Add(new Paragraph(title, titleFont));
                doc.Add(new Paragraph($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}",
                    FontFactory.GetFont(FontFactory.HELVETICA, 10)));
                doc.Add(new Paragraph(" "));

                int visibleCols = dataGridView1.Columns.Cast<DataGridViewColumn>()
                    .Count(c => c.Visible);
                PdfPTable table = new PdfPTable(visibleCols);
                table.WidthPercentage = 100;

                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                foreach (DataGridViewColumn col in dataGridView1.Columns)
                {
                    if (!col.Visible) continue;
                    var cell = new PdfPCell(new Phrase(col.HeaderText, headerFont));
                    cell.BackgroundColor = new BaseColor(52, 73, 94);
                    cell.Padding = 5;
                    table.AddCell(cell);
                }

                var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                var workingFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.RED);
                bool alternate = false;

                int totalPdfSeconds = 0;

                foreach (DataGridViewRow row in rowsToPrint)
                {
                    foreach (DataGridViewCell cell in row.Cells)
                    {
                        if (!cell.OwningColumn.Visible) continue;

                        string val = cell.Value?.ToString() ?? "";
                        var font = (cell.OwningColumn.Name == "status" && val == "Working")
                            ? workingFont : dataFont;

                        var pdfCell = new PdfPCell(new Phrase(val, font));
                        pdfCell.BackgroundColor = alternate
                            ? new BaseColor(241, 245, 249)
                            : BaseColor.WHITE;
                        pdfCell.Padding = 4;
                        table.AddCell(pdfCell);

                        // Accumulate working time
                        if (cell.OwningColumn.Name == "workingTime" && !string.IsNullOrEmpty(val) && val != "LIVE")
                        {
                            var parts = val.Split(':');
                            if (parts.Length == 3)
                            {
                                int.TryParse(parts[0], out int h);
                                int.TryParse(parts[1], out int m);
                                int.TryParse(parts[2], out int s);
                                totalPdfSeconds += h * 3600 + m * 60 + s;
                            }
                        }
                    }
                    alternate = !alternate;
                }

                doc.Add(table);

                // Add total hours below the table
                string totalStr = $"{totalPdfSeconds / 3600:D2}:{(totalPdfSeconds % 3600) / 60:D2}:{totalPdfSeconds % 60:D2}";
                var totalFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11);
                var totalParagraph = new Paragraph($"Total Working Hours: {totalStr}", totalFont);
                totalParagraph.Alignment = Element.ALIGN_RIGHT;
                totalParagraph.SpacingBefore = 10;
                doc.Add(totalParagraph);

                doc.Close();

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = sfd.FileName,
                    UseShellExecute = true
                });

                UpdateRichTextBox($"[i] PDF saved: {sfd.FileName}\r\n");
//                 DebugLogger.Log("[Form1] Showing MessageBox");
            MessageBox.Show("Report saved successfully!", "Done",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox($"[ERROR] Report failed: {ex.Message}\r\n");
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            // Old button - redirect to new handler
            await RefreshLogsAsync(forceRefresh: true);
        }

        private async void buttonRefresh_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[Form1] buttonRefresh_Click: ENTRY - Manual refresh requested");
            // Disable button while refreshing to prevent double-clicks
            buttonRefresh.Enabled = false;
            buttonRefresh.Image = ThemeConstants.CreateRefreshIcon(Math.Max(22, buttonRefresh.Width - 10), Color.FromArgb(230, 248, 255, 250));

            try
            {
                // Force fetch fresh data from Firebase and reload DataGridView
                await RefreshLogsAsync(forceRefresh: true);

                // Refresh online status FIRST (syncs MembersMeta with Country & WeeklyHourLimit)
                // then refresh weekly hours (uses Country for local time & holiday calculation)
                await RefreshOnlineStatusAsync();
                _ = RefreshWeeklyHoursAsync();
                if (_stickerBoard != null) _ = _stickerBoard.RefreshAsync();
                if (_chatPanel != null) _ = _chatPanel.RefreshAsync();
                if (_fileSharePanel != null) _ = _fileSharePanel.RefreshAsync();
                if (_helperPanel != null) _ = _helperPanel.RefreshAsync();
            }
            finally
            {
                buttonRefresh.Image = ThemeConstants.CreateRefreshIcon(Math.Max(22, buttonRefresh.Width - 8), Color.White);
                buttonRefresh.Enabled = true;
            }
        }

        private void buttonPrint_Click(object sender, EventArgs e)
        {
            // Show context menu with report export options
//             DebugLogger.Log("[Form1] buttonPrint_Click: ENTRY - Print menu requested");
            var menu = new ContextMenuStrip();
            menu.BackColor = isDarkMode ? Color.FromArgb(32, 38, 50) : Color.White;
            menu.ForeColor = isDarkMode ? Color.FromArgb(220, 225, 240) : Color.FromArgb(30, 35, 50);

            var printItem = new ToolStripMenuItem("Print Report");
            printItem.Click += (s2, e2) => buttonReport_Click(sender, e);
            menu.Items.Add(printItem);

            var pdfItem = new ToolStripMenuItem("Monthly PDF Report (Ctrl+P)");
            pdfItem.Click += (s2, e2) => GenerateMonthlyPdfReport();
            menu.Items.Add(pdfItem);

            var csvItem = new ToolStripMenuItem("Export CSV (Ctrl+E)");
            csvItem.Click += (s2, e2) => BtnExport_Click(sender, e);
            menu.Items.Add(csvItem);

            menu.Show(buttonPrint, new Point(0, buttonPrint.Height));
        }

        private async void buttonSave_Click(object sender, EventArgs e)
        {
//             DebugLogger.Log("[Form1] buttonSave_Click: ENTRY - Save button clicked");
            if (dataGridView1.SelectedRows.Count == 0)
            {
                UpdateRichTextBox("[i] No row selected.\r\n");
                return;
            }

            var row = dataGridView1.SelectedRows[0];
            var logEntry = row.DataBoundItem as LogEntryWithIndex;
            if (logEntry == null || string.IsNullOrEmpty(logEntry.Key))
            {
                UpdateRichTextBox("[i] Invalid row.\r\n");
                return;
            }

            var editedLog = new LogEntry
            {
                description = row.Cells["description"].Value?.ToString() ?? "",
                startTime = row.Cells["startTime"].Value?.ToString() ?? "",
                workingTime = row.Cells["workingTime"].Value?.ToString() ?? "",
                status = row.Cells["status"].Value?.ToString() ?? "",
                timestamp = row.Cells["timestamp"].Value?.ToString() ?? DateTime.UtcNow.ToString("o"),
                userId = row.Cells["userId"]?.Value?.ToString() ?? (logEntry.userId ?? GetCurrentUserId()),
                userName = row.Cells["userName"]?.Value?.ToString() ?? (logEntry.userName ?? _currentUser.Name),
                project = row.Cells["project"]?.Value?.ToString() ?? (logEntry.project ?? "General"),
                platform = logEntry.platform ?? "desktop"
            };

            try
            {
                // Persist locally first so user data is never lost.
                UpsertLocalLogByKey(logEntry.Key, editedLog);

                string updateUrl = firebaseUrl.Replace(".json", $"/{logEntry.Key}.json");
                var json = JsonConvert.SerializeObject(editedLog);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync(updateUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    RemovePendingLogUpsert(logEntry.Key);
                    UpdateRichTextBox("[i] Entry updated.\r\n");
                    RefreshLogs();
                    _ = RefreshWeeklyHoursAsync();
                }
                else
                {
                    UpdateRichTextBox($"[i] Update failed. Status: {response.StatusCode}\r\n");
                    QueuePendingLogUpsert(logEntry.Key, editedLog);
                    UpdateRichTextBox("[i] Entry saved locally and queued for sync.\r\n");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    UpsertLocalLogByKey(logEntry.Key, editedLog);
                    QueuePendingLogUpsert(logEntry.Key, editedLog);
                }
                catch { }
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox($"[ERROR] Update exception: {ex.Message}\r\n");
                UpdateRichTextBox("[i] Entry saved locally and queued for sync.\r\n");
            }
        }

        // --- EDIT TIME ENTRY ? ADMIN CAN EDIT ANY, USERS ONLY THEIR OWN ---
        private async void EditSelectedEntry()
        {
//             DebugLogger.Log("[Form1] EditSelectedEntry: ENTRY - Edit entry dialog opened");
            if (dataGridView1.SelectedRows.Count == 0)
            {
                UpdateRichTextBox("[i] No row selected.\r\n");
                return;
            }

            var row = dataGridView1.SelectedRows[0];
            var logEntry = row.DataBoundItem as LogEntryWithIndex;
            if (logEntry == null || string.IsNullOrEmpty(logEntry.Key))
            {
                UpdateRichTextBox("[i] Invalid row.\r\n");
                return;
            }

            // -- Permission check: admin can edit any, users only their own --
            var editTeam = UserStorage.LoadTeam();
            bool canEditAll = _currentUser.IsAdmin || (editTeam != null && editTeam.HasAdminPrivileges(_currentUser.Name));
            if (!canEditAll && !string.Equals(logEntry.userName, _currentUser.Name, StringComparison.OrdinalIgnoreCase))
            {
                UpdateRichTextBox("[i] Only admins can edit other users' entries.\r\n");
                return;
            }

            // -- Show edit dialog with current values --
            using (var editForm = new FormEditEntry(
                logEntry.userName ?? "",
                logEntry.description ?? "",
                logEntry.project ?? "",
                logEntry.startTime ?? "",
                logEntry.workingTime ?? "",
                logEntry.status ?? "Stopped",
                logEntry.timestamp ?? ""))
            {
                if (editForm.ShowDialog(this) != DialogResult.OK)
                    return;

                // -- Save edited values to Firebase --
                try
                {
                    var editedLog = new LogEntry
                    {
                        description = editForm.EntryDescription,
                        project = editForm.EntryProject,
                        startTime = editForm.EntryStartTime,
                        workingTime = editForm.EntryWorkingTime,
                        status = editForm.EntryStatus,
                        timestamp = logEntry.timestamp,  // Keep original date
                        userName = logEntry.userName,     // Keep original user
                        userId = logEntry.userName,
                        platform = "desktop"
                    };

                    // Always persist locally first so edits are never lost.
                    UpsertLocalLogByKey(logEntry.Key, editedLog);

                    string updateUrl = firebaseUrl.Replace(".json", $"/{logEntry.Key}.json");
                    var json = JsonConvert.SerializeObject(editedLog);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await _httpClient.PutAsync(updateUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        RemovePendingLogUpsert(logEntry.Key);
                        UpdateRichTextBox($"[i] Entry for {logEntry.userName} updated successfully.\r\n");
                        RefreshLogs();
                        _ = RefreshWeeklyHoursAsync();
                    }
                    else
                    {
                        QueuePendingLogUpsert(logEntry.Key, editedLog);
                        UpdateRichTextBox($"[i] Update failed. Status: {response.StatusCode}\r\n");
                        UpdateRichTextBox("[i] Edit saved locally and queued for sync.\r\n");
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        var editedLog = new LogEntry
                        {
                            description = editForm.EntryDescription,
                            project = editForm.EntryProject,
                            startTime = editForm.EntryStartTime,
                            workingTime = editForm.EntryWorkingTime,
                            status = editForm.EntryStatus,
                            timestamp = logEntry.timestamp,
                            userName = logEntry.userName,
                            userId = logEntry.userName,
                            platform = "desktop"
                        };
                        UpsertLocalLogByKey(logEntry.Key, editedLog);
                        QueuePendingLogUpsert(logEntry.Key, editedLog);
                    }
                    catch { }

                    DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                    UpdateRichTextBox($"[ERROR] Edit exception: {ex.Message}\r\n");
                    UpdateRichTextBox("[i] Edit saved locally and queued for sync.\r\n");
                }
            }
        }

        // --- DELETE TIME ENTRY ? ADMIN CAN DELETE ANY, USERS ONLY THEIR OWN ---
        private async void buttonDelete_Click(object sender, EventArgs e)
        {
            // Only admin or assistant admin can delete time entries
            var delTeam = UserStorage.LoadTeam();
            bool canDelete = _currentUser.IsAdmin || (delTeam != null && delTeam.HasAdminPrivileges(_currentUser.Name));
            if (!canDelete)
            {
                // Regular users can only delete their own entries
                if (dataGridView1.SelectedRows.Count > 0)
                {
                    var selRow = dataGridView1.SelectedRows[0];
                    var selEntry = selRow.DataBoundItem as LogEntryWithIndex;
                    if (selEntry != null && selEntry.userName != _currentUser.Name)
                    {
                        UpdateRichTextBox("[i] Only admins can delete other users' entries.\r\n");
                        return;
                    }
                }
            }

            if (dataGridView1.SelectedRows.Count == 0)
            {
                UpdateRichTextBox("[i] No row selected.\r\n");
                return;
            }

            var row = dataGridView1.SelectedRows[0];
            var logEntry = row.DataBoundItem as LogEntryWithIndex;
            if (logEntry == null || string.IsNullOrEmpty(logEntry.Key))
            {
                UpdateRichTextBox("[i] Invalid row.\r\n");
                return;
            }

            if (MessageBox.Show("Delete this entry?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                // Remove locally first so UI/data stays consistent even when offline.
                RemoveLocalLogByKey(logEntry.Key);
                string deleteUrl = firebaseUrl.Replace(".json", $"/{logEntry.Key}.json");
                var response = await _httpClient.DeleteAsync(deleteUrl);
                if (response.IsSuccessStatusCode)
                {
                    RemovePendingLogDelete(logEntry.Key);
                    UpdateRichTextBox("[i] Entry deleted.\r\n");
                    if (currentLiveLogKey == logEntry.Key)
                        currentLiveLogKey = null;
                    RefreshLogs();
                    await RefreshOnlineStatusAsync();
                    _ = RefreshWeeklyHoursAsync();
                }
                else
                {
                    UpdateRichTextBox("[i] Delete failed. Status: " + response.StatusCode + "\r\n");
                    QueuePendingLogDelete(logEntry.Key);
                    UpdateRichTextBox("[i] Delete saved locally and queued for sync.\r\n");
                    RefreshLogs();
                    _ = RefreshWeeklyHoursAsync();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    RemoveLocalLogByKey(logEntry.Key);
                    QueuePendingLogDelete(logEntry.Key);
                }
                catch { }
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");
                UpdateRichTextBox("[ERROR] Delete exception: " + ex.Message + "\r\n");
                UpdateRichTextBox("[i] Delete saved locally and queued for sync.\r\n");
                RefreshLogs();
                _ = RefreshWeeklyHoursAsync();
            }
        }

        private void checkBoxTheme_CheckedChanged(object sender, EventArgs e)
        {
            // Theme toggle is handled by CheckBoxTheme_CheckedChanged (wired in InitializeTheme)
        }

        // ============================================================
        // UPDATE CHECK
        // ============================================================
        private async Task CheckForUpdateAsync()
        {
            try
            {
                UpdateRichTextBox($"[DEBUG] Current app version: v{currentAppVersion}\r\n");
                labelVersion.Text = $"v{currentAppVersion}";
                labelVersion.ForeColor = Color.FromArgb(149, 165, 176);
                labelMessage.Visible = false;

                string versionUrl = firebaseUrl.Replace("/logs.json", "/appVersion.json");
                UpdateRichTextBox($"[DEBUG] Checking for updates at: {versionUrl}\r\n");

                var response = await _httpClient.GetAsync(versionUrl);
                UpdateRichTextBox($"[DEBUG] Version check response: {response.StatusCode}\r\n");

                if (!response.IsSuccessStatusCode)
                {
                    UpdateRichTextBox($"[i] Update check returned non-success status: {response.StatusCode}\r\n");
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                UpdateRichTextBox($"[DEBUG] Version JSON length: {json?.Length ?? 0}\r\n");

                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    UpdateRichTextBox("[i] Version JSON is empty.\r\n");
                    return;
                }

                var versionInfo = JsonConvert.DeserializeObject<AppVersionInfo>(json);
                if (versionInfo == null || string.IsNullOrWhiteSpace(versionInfo.latestVersion))
                {
                    UpdateRichTextBox("[i] Version info is missing latestVersion.\r\n");
                    return;
                }

                bool isNewer = IsNewerVersion(versionInfo.latestVersion, currentAppVersion);
                UpdateRichTextBox($"[DEBUG] Latest version: {versionInfo.latestVersion}\r\n");
                UpdateRichTextBox($"[DEBUG] Is newer: {isNewer}\r\n");

                if (isNewer)
                {
                    // Version label: red, clickable
                    labelVersion.Text = $"v{currentAppVersion} (outdated)";
                    labelVersion.ForeColor = Color.FromArgb(231, 76, 60);
                    labelVersion.Cursor = Cursors.Hand;

                    // Update message: clickable link style
                    labelMessage.Text = $"Update to v{versionInfo.latestVersion} - Click here";
                    labelMessage.ForeColor = Color.FromArgb(231, 76, 60);
                    labelMessage.Font = new System.Drawing.Font(labelMessage.Font.FontFamily,
                        labelMessage.Font.Size, FontStyle.Bold | FontStyle.Underline);
                    labelMessage.Cursor = Cursors.Hand;
                    labelMessage.Visible = true;

                    labelMessage.Click -= LabelMessage_Click;
                    labelMessage.Click += LabelMessage_Click;

                    _pendingVersionInfo = versionInfo;

                    UpdateRichTextBox("[i] Update available.\r\n");
                }
                else
                {
                    // Version label: green with checkmark
                    labelVersion.Text = $"v{currentAppVersion} \u2714";
                    labelVersion.ForeColor = Color.FromArgb(46, 204, 113);
                    labelVersion.Cursor = Cursors.Default;
                    labelMessage.Visible = false;
                    labelMessage.Click -= LabelMessage_Click;
                    _pendingVersionInfo = null;

                    UpdateRichTextBox("[i] App is up to date.\r\n");
                }
            }
            catch (Exception ex)
            {
                labelVersion.Text = $"v{currentAppVersion}";
                labelVersion.Cursor = Cursors.Default;
                labelMessage.Visible = false;
                labelMessage.Click -= LabelMessage_Click;
                _pendingVersionInfo = null;
                UpdateRichTextBox($"[ERROR] Update check failed: {ex.Message}\r\n");
            }
        }

        private AppVersionInfo _pendingVersionInfo;

        private void LabelMessage_Click(object sender, EventArgs e)
        {
            try
            {
                if (_pendingVersionInfo == null)
                {
                    UpdateRichTextBox("[i] No pending version info available.\r\n");
                    return;
                }

                DialogResult result = MessageBox.Show(
                    $"New version v{_pendingVersionInfo.latestVersion} is available!\n\n" +
                    $"What's new:\n{_pendingVersionInfo.releaseNotes}\n\n" +
                    "Download now?",
                    "Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes && !string.IsNullOrWhiteSpace(_pendingVersionInfo.downloadUrl))
                {
                    UpdateRichTextBox($"[DEBUG] Opening download URL: {_pendingVersionInfo.downloadUrl}\r\n");

                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _pendingVersionInfo.downloadUrl,
                        UseShellExecute = true
                    });

                    UpdateRichTextBox("[i] Download link opened.\r\n");
                }
                else
                {
                    UpdateRichTextBox("[INFO] Update download cancelled by user.\r\n");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");

                UpdateRichTextBox($"[ERROR] Failed to open update link: {ex.Message}\r\n");
            }
        }

        private bool IsNewerVersion(string latest, string current)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(latest) || string.IsNullOrWhiteSpace(current))
                    return false;

                latest = latest.Trim().Trim('"');
                current = current.Trim().Trim('"');

                System.Version latestVersion = new System.Version(latest);
                System.Version currentVersion = new System.Version(current);

                return latestVersion > currentVersion;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // THEME
        // ============================================================
        private void InitializeTheme()
        {
            LoadSettings();
            checkBoxTheme.Text = "Dark Mode";
            checkBoxTheme.Checked = isDarkMode;
            checkBoxTheme.CheckedChanged += CheckBoxTheme_CheckedChanged;
            ApplyTheme();
        }

        private void CheckBoxTheme_CheckedChanged(object sender, EventArgs e)
        {
            isDarkMode = checkBoxTheme.Checked;
            ApplyTheme();
            SaveSettings();
        }

        private void ApplyTheme()
        {
            Color backMain, backPanel, backInput, foreMain, foreSecondary, accentColor;
            Color startBg, stopBg, buttonText, sidebarBg, chatCardBg, gridLineColor, selectionColor;

            if (_customTheme != null && _customTheme.Enabled)
            {
                // -- CUSTOM THEME OVERRIDES EVERYTHING --
                backMain = _customTheme.GetBackground();
                backPanel = _customTheme.GetCard();
                backInput = _customTheme.GetInput();
                foreMain = _customTheme.GetText();
                foreSecondary = _customTheme.GetSecondaryText();
                accentColor = _customTheme.GetAccent();
                startBg = _customTheme.GetStart();
                stopBg = _customTheme.GetStop();
                buttonText = _customTheme.GetButtonText();
                sidebarBg = _customTheme.GetSidebar();
                chatCardBg = _customTheme.GetChatCard();
                gridLineColor = _customTheme.GetGridLine();
                selectionColor = _customTheme.GetSelection();
            }
            else if (isDarkMode)
            {
                backMain = ThemeConstants.Dark.BgBase;
                backPanel = ThemeConstants.Dark.BgElevated;
                backInput = ThemeConstants.Dark.BgInput;
                foreMain = ThemeConstants.Dark.TextPrimary;
                foreSecondary = ThemeConstants.Dark.TextSecondary;
                accentColor = ThemeConstants.Dark.AccentPrimary;
                startBg = ThemeConstants.Dark.Green;
                stopBg = ThemeConstants.Dark.Red;
                buttonText = Color.White;
                sidebarBg = backPanel;
                chatCardBg = backInput;
                gridLineColor = ThemeConstants.Dark.Border;
                selectionColor = ThemeConstants.Dark.AccentPrimary;
            }
            else
            {
                backMain = ThemeConstants.Light.BgBase;
                backPanel = ThemeConstants.Light.BgSurface;
                backInput = ThemeConstants.Light.BgInput;
                foreMain = ThemeConstants.Light.TextPrimary;
                foreSecondary = ThemeConstants.Light.TextSecondary;
                accentColor = ThemeConstants.Light.AccentPrimary;
                startBg = ThemeConstants.Light.Green;
                stopBg = ThemeConstants.Light.Red;
                buttonText = Color.White;
                sidebarBg = backPanel;
                chatCardBg = backInput;
                gridLineColor = ThemeConstants.Light.Border;
                selectionColor = ThemeConstants.Light.AccentPrimary;
            }

            this.BackColor = backMain;
            this.ForeColor = foreMain;

            if (panelOnlineUsers != null)
            {
                panelOnlineUsers.BackColor = sidebarBg;
                if (labelOnlineTitle != null)
                    labelOnlineTitle.ForeColor = accentColor;
            }

            if (_labelActiveTeam != null)
            {
                _labelActiveTeam.ForeColor = accentColor;
                _activeTeamBadge?.Invalidate();
            }

            // Apply custom font if custom theme is active
            if (_customTheme != null && _customTheme.Enabled)
            {
                try
                {
                    var baseFont = _customTheme.GetFont();
                    this.Font = baseFont;
                }
                catch { }
            }

            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is GroupBox gb)
                {
                    gb.ForeColor = accentColor;
                    gb.BackColor = backMain;
                }
            }

            if (richTextBoxDescription != null)
                richTextBoxDescription.BorderStyle = BorderStyle.FixedSingle;

            ApplyToControls(this.Controls, backMain, backPanel, backInput,
                foreMain, foreSecondary, accentColor, startBg, stopBg, buttonText,
                chatCardBg, gridLineColor, selectionColor);

            if (panelOnlineUsers != null)
                panelOnlineUsers.BackColor = sidebarBg;

            if (label1 != null && panelOnlineUsers != null)
            {
                int badgeRightLimit = this.ClientSize.Width - panelOnlineUsers.Width - ThemeConstants.SpaceL - 240 - ThemeConstants.SpaceL;
            UpdateActiveTeamBadge(new Point(label1.Right + ThemeConstants.SpaceM + 10, label1.Top + 11), badgeRightLimit);
            }

            if (dataGridView1 != null && dataGridView1.Rows.Count > 0)
                RecolorDataGridRows();

            foreach (var ctrl in onlineUserControls)
            {
                if (ctrl != null)
                    ctrl.ApplyTheme(isDarkMode, _customTheme);
            }

            // Theme for user filter combo
            if (comboBoxUserFilter != null)
            {
                comboBoxUserFilter.BackColor = backInput;
                comboBoxUserFilter.ForeColor = foreMain;
            }

            // Theme for new panels (pass custom theme for override)
            _stickerBoard?.ApplyTheme(isDarkMode, _customTheme);
            _chatPanel?.ApplyTheme(isDarkMode, _customTheme);
            _fileSharePanel?.ApplyTheme(isDarkMode, _customTheme);
            _projectFolderPanel?.ApplyTheme(isDarkMode, _customTheme);
            _helperPanel?.ApplyTheme(isDarkMode, _customTheme);
            _personalStickerBoard?.ApplyTheme(isDarkMode, _customTheme);
            if (_personalBoardWindow != null && !_personalBoardWindow.IsDisposed)
                _personalBoardWindow.BackColor = backMain;

            // Theme for calendar panel
            _calendarPanel?.ApplyTheme(isDarkMode, _customTheme);
            if (_weatherPanel != null)
                _weatherPanel.BackColor = isDarkMode ? ThemeConstants.Dark.BgElevated : ThemeConstants.Light.BgSurface;
            if (_weatherTitle != null)
                _weatherTitle.ForeColor = isDarkMode ? ThemeConstants.Dark.AccentPrimary : ThemeConstants.Light.AccentPrimary;
            if (_weatherSummary != null)
                _weatherSummary.ForeColor = isDarkMode ? ThemeConstants.Dark.TextPrimary : ThemeConstants.Light.TextPrimary;
            if (_weatherDetails != null)
                _weatherDetails.ForeColor = isDarkMode ? ThemeConstants.Dark.TextSecondary : ThemeConstants.Light.TextSecondary;
            if (_askAiPanel != null)
                _askAiPanel.BackColor = isDarkMode ? Color.FromArgb(20, 24, 34) : Color.White;
            if (_btnAiChatSend != null)
            {
                _btnAiChatSend.BackColor = Color.FromArgb(31, 132, 255);
                _btnAiChatSend.ForeColor = Color.White;
            }
            if (_aiChatWindow != null && !_aiChatWindow.IsDisposed)
                _aiChatWindow.BackColor = backMain;
            if (_aiChatHistory != null)
            {
                _aiChatHistory.BackColor = isDarkMode ? Color.FromArgb(20, 24, 34) : Color.White;
                _aiChatHistory.ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59);
            }
            if (_aiChatInput != null)
            {
                _aiChatInput.BackColor = isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White;
                _aiChatInput.ForeColor = isDarkMode ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 41, 59);
            }
            ApplyAiChatFontSize(_aiChatFontSizeName, false);
            RefreshAskAiPanelStatus();

            // Enforce action button palette after theme recursion so other branches can't overwrite it.
            ThemeConstants.StyleActionButton(buttonStop, isDarkMode, false);
            if (_workingTimer == null || !_workingTimer.Enabled)
                ThemeConstants.StyleActionButton(buttonStart, isDarkMode, true);

            UpdateMainClockVisualState();
        }

        private void ApplyToControls(Control.ControlCollection controls,
            Color backMain, Color backPanel, Color backInput,
            Color foreMain, Color foreSecondary, Color accentColor,
            Color startBg, Color stopBg, Color buttonText,
            Color chatCardBg, Color gridLineColor, Color selectionColor)
        {
            foreach (Control ctrl in controls)
            {
                if (ctrl is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;

                    if (ReferenceEquals(btn, buttonStart))
                    {
                        ThemeConstants.StyleActionButton(buttonStart, isDarkMode, true);
                        continue;
                    }
                    else if (ReferenceEquals(btn, buttonStop))
                    {
                        ThemeConstants.StyleActionButton(buttonStop, isDarkMode, false);
                        continue;
                    }
                    else if (_toolbarPanel != null && btn.Parent == _toolbarPanel)
                    {
                        // Toolbar tabs manage their own color/icon theme state.
                        // Do not override here, otherwise they lose initial pill colors on startup.
                    }
                    else if (btn.Text.Contains("START"))
                    {
                        btn.FlatAppearance.BorderSize = 0;
                        btn.BackColor = startBg;
                        btn.ForeColor = buttonText;
                    }
                    else if (btn.Text.Contains("STOP"))
                    {
                        btn.FlatAppearance.BorderSize = 0;
                        btn.BackColor = stopBg;
                        btn.ForeColor = buttonText;
                    }
                    else
                    {
                        var borderCol = (_customTheme != null && _customTheme.Enabled)
                            ? gridLineColor
                            : (isDarkMode ? ThemeConstants.Dark.Border : ThemeConstants.Light.Border);
                        btn.FlatAppearance.BorderSize = 1;
                        btn.FlatAppearance.BorderColor = borderCol;
                        btn.BackColor = (_customTheme != null && _customTheme.Enabled)
                            ? backPanel
                            : (isDarkMode ? ThemeConstants.Dark.BgElevated : ThemeConstants.Light.BgElevated);
                        btn.ForeColor = (_customTheme != null && _customTheme.Enabled) ? buttonText : foreMain;
                    }
                }
                else if (ctrl is RichTextBox rtb)
                {
                    rtb.BackColor = backInput;
                    rtb.ForeColor = foreMain;
                }
                else if (ctrl is TextBox tb)
                {
                    tb.BackColor = backInput;
                    tb.ForeColor = foreMain;
                }
                else if (ctrl is ComboBox combo)
                {
                    combo.BackColor = backInput;
                    combo.ForeColor = foreMain;
                }
                else if (ctrl is NumericUpDown nud)
                {
                    nud.BackColor = backInput;
                    nud.ForeColor = foreMain;
                }
                else if (ctrl is Label lbl)
                {
                    if (lbl.Text.Contains("WORK COUNTER") || lbl.Text.Contains("Work Counter") ||
                        lbl.Text.Contains("BOARD") || lbl.Text.Contains("TEAM CHAT") ||
                        lbl.Text.Contains("Team Status"))
                        lbl.ForeColor = accentColor;
                    else if (lbl.Text.Contains("Offline"))
                        lbl.ForeColor = foreSecondary;
                    else
                        lbl.ForeColor = foreMain;
                }
                else if (ctrl is Panel pnl)
                {
                    pnl.BackColor = backPanel;
                }
                else if (ctrl is CheckBox chk)
                {
                    chk.ForeColor = foreMain;
                    // Keep DarkMode and Sound checkboxes transparent
                    if (chk.BackColor != Color.Transparent)
                        chk.BackColor = backMain;
                }
                else if (ctrl is DataGridView dgv)
                {
                    if (_customTheme != null && _customTheme.Enabled)
                    {
                        // Custom theme colors for DataGridView
                        dgv.EnableHeadersVisualStyles = false;
                        dgv.BorderStyle = BorderStyle.None;
                        dgv.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
                        dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
                        dgv.GridColor = gridLineColor;
                        dgv.BackgroundColor = backMain;
                        dgv.DefaultCellStyle.BackColor = backMain;
                        dgv.DefaultCellStyle.ForeColor = _customTheme.GetText();
                        dgv.DefaultCellStyle.SelectionBackColor = selectionColor;
                        dgv.DefaultCellStyle.SelectionForeColor = _customTheme.GetButtonText();
                        dgv.AlternatingRowsDefaultCellStyle.BackColor = chatCardBg;
                        dgv.AlternatingRowsDefaultCellStyle.ForeColor = _customTheme.GetText();
                        dgv.ColumnHeadersDefaultCellStyle.BackColor = backPanel;
                        dgv.ColumnHeadersDefaultCellStyle.ForeColor = _customTheme.GetSecondaryText();
                    }
                    else
                    {
                        // Use ThemeConstants for consistent premium grid styling
                        ThemeConstants.StyleDataGrid(dgv, isDarkMode);
                    }

                    string fontFamily = (_customTheme != null && _customTheme.Enabled) ? _customTheme.FontFamily : ThemeConstants.FontFamily;
                    float fontSize = (_customTheme != null && _customTheme.Enabled) ? _customTheme.FontSize : 9.5f;
                    dgv.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font(fontFamily, fontSize, FontStyle.Bold);
                    dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgv.ColumnHeadersDefaultCellStyle.BackColor;
                    dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;
                    dgv.RowsDefaultCellStyle.BackColor = dgv.DefaultCellStyle.BackColor;

                    dgv.DefaultCellStyle.Font = new System.Drawing.Font(fontFamily, fontSize);
                    dgv.AlternatingRowsDefaultCellStyle.Font = new System.Drawing.Font(fontFamily, fontSize);
                }
                else if (ctrl is PictureBox)
                {
                    // skip
                }
                else
                {
                    ctrl.BackColor = backMain;
                    ctrl.ForeColor = foreMain;
                }

                if (ctrl.HasChildren)
                    ApplyToControls(ctrl.Controls, backMain, backPanel, backInput,
                        foreMain, foreSecondary, accentColor, startBg, stopBg, buttonText,
                        chatCardBg, gridLineColor, selectionColor);
            }
        }

        private void RecolorDataGridRows()
        {
            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                var status = row.Cells["status"].Value?.ToString();
                if (status == "Working")
                {
                    if (_customTheme != null && _customTheme.Enabled)
                    {
                        row.DefaultCellStyle.BackColor = BlendColors(_customTheme.GetBackground(), _customTheme.GetAccent(), 0.25f);
                        row.DefaultCellStyle.ForeColor = _customTheme.GetText();
                        row.DefaultCellStyle.SelectionBackColor = _customTheme.GetSelection();
                        row.DefaultCellStyle.SelectionForeColor = _customTheme.GetButtonText();
                    }
                    else
                    {
                        row.DefaultCellStyle.BackColor = isDarkMode
                            ? Color.FromArgb(70, 30, 30)
                            : Color.FromArgb(255, 230, 230);
                        row.DefaultCellStyle.ForeColor = isDarkMode
                            ? Color.FromArgb(255, 140, 140)
                            : Color.FromArgb(180, 30, 30);
                    }
                }
                else
                {
                    if (_customTheme != null && _customTheme.Enabled)
                    {
                        row.DefaultCellStyle.BackColor = row.Index % 2 == 0
                            ? _customTheme.GetBackground()
                            : _customTheme.GetChatCard();
                        row.DefaultCellStyle.ForeColor = _customTheme.GetText();
                        row.DefaultCellStyle.SelectionBackColor = _customTheme.GetSelection();
                        row.DefaultCellStyle.SelectionForeColor = _customTheme.GetButtonText();
                    }
                    else if (isDarkMode)
                    {
                        row.DefaultCellStyle.BackColor = row.Index % 2 == 0
                            ? Color.FromArgb(26, 30, 38)
                            : Color.FromArgb(32, 38, 50);
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(220, 224, 235);
                    }
                    else
                    {
                        row.DefaultCellStyle.BackColor = row.Index % 2 == 0
                            ? Color.White
                            : Color.FromArgb(245, 247, 252);
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(40, 45, 60);
                    }
                }
            }
            dataGridView1.Invalidate();
        }

        private static Color BlendColors(Color baseColor, Color overlayColor, float overlayAmount)
        {
            overlayAmount = Math.Max(0f, Math.Min(1f, overlayAmount));
            float baseAmount = 1f - overlayAmount;

            return Color.FromArgb(
                (int)(baseColor.R * baseAmount + overlayColor.R * overlayAmount),
                (int)(baseColor.G * baseAmount + overlayColor.G * overlayAmount),
                (int)(baseColor.B * baseAmount + overlayColor.B * overlayAmount));
        }

        private void StyleButtons()
        {
            // START button - green bg, orange accent border, play icon
            ThemeConstants.StyleActionButton(buttonStart, isDarkMode, true);
            buttonStart.Size = new Size(120, ThemeConstants.ButtonHeight);
            ApplyRoundedCorners(buttonStart, ThemeConstants.RadiusMedium);

            // STOP button - red bg, orange accent border, stop icon
            ThemeConstants.StyleActionButton(buttonStop, isDarkMode, false);
            buttonStop.Size = new Size(120, ThemeConstants.ButtonHeight);
            ApplyRoundedCorners(buttonStop, ThemeConstants.RadiusMedium);

            // OLD buttons hidden - replaced by buttonRefresh/buttonPrint
            button1.Visible = false;
            buttonReport.Visible = false;

            // REFRESH button - orange bg, maroon border, swaps to teal on hover
            int roundActionBtnSize = 42;
            ThemeConstants.StyleRefreshButton(buttonRefresh, isDarkMode);
            buttonRefresh.Text = string.Empty;
            buttonRefresh.Size = new Size(roundActionBtnSize, roundActionBtnSize);
            buttonRefresh.Image = ThemeConstants.CreateRefreshIcon(roundActionBtnSize - 3, Color.White);
            buttonRefresh.TextImageRelation = TextImageRelation.Overlay;
            ApplyCircularButton(buttonRefresh);

            // PRINT button - teal bg, orange border, swaps to orange on hover
            ThemeConstants.StylePrintButton(buttonPrint, isDarkMode);
            buttonPrint.Text = string.Empty;
            buttonPrint.Size = new Size(roundActionBtnSize, roundActionBtnSize);
            buttonPrint.Image = ThemeConstants.CreatePrintIcon(roundActionBtnSize - 3, Color.White);
            buttonPrint.TextImageRelation = TextImageRelation.Overlay;
            ApplyCircularButton(buttonPrint);
        }

        // ============================================================
        // 8-BIT PIXEL ART ICONS
        // ============================================================
        private Bitmap Create8BitRefreshIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;       // keep it pixelated!
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.Clear(Color.Transparent);

                int px = size / 8; // pixel block size
                Color c1 = Color.FromArgb(0, 255, 200);    // cyan-green
                Color c2 = Color.FromArgb(255, 220, 50);   // yellow
                Color c3 = Color.White;

                // Draw a circular arrow in pixel art style
                // Top arc
                using (var b = new SolidBrush(c1))
                {
                    g.FillRectangle(b, 2 * px, 0 * px, px, px);
                    g.FillRectangle(b, 3 * px, 0 * px, px, px);
                    g.FillRectangle(b, 4 * px, 0 * px, px, px);
                    g.FillRectangle(b, 5 * px, 0 * px, px, px);
                    g.FillRectangle(b, 1 * px, 1 * px, px, px);
                    g.FillRectangle(b, 6 * px, 1 * px, px, px);
                    g.FillRectangle(b, 0 * px, 2 * px, px, px);
                    g.FillRectangle(b, 7 * px, 2 * px, px, px);
                    g.FillRectangle(b, 0 * px, 3 * px, px, px);
                    g.FillRectangle(b, 7 * px, 3 * px, px, px);
                    g.FillRectangle(b, 0 * px, 4 * px, px, px);
                    g.FillRectangle(b, 0 * px, 5 * px, px, px);
                    g.FillRectangle(b, 1 * px, 6 * px, px, px);
                    g.FillRectangle(b, 2 * px, 7 * px, px, px);
                    g.FillRectangle(b, 3 * px, 7 * px, px, px);
                    g.FillRectangle(b, 4 * px, 7 * px, px, px);
                    g.FillRectangle(b, 5 * px, 7 * px, px, px);
                    g.FillRectangle(b, 6 * px, 6 * px, px, px);
                    g.FillRectangle(b, 7 * px, 5 * px, px, px);
                    g.FillRectangle(b, 7 * px, 4 * px, px, px);
                }
                // Arrow head (top-right)
                using (var b = new SolidBrush(c2))
                {
                    g.FillRectangle(b, 5 * px, 0 * px, px, px);
                    g.FillRectangle(b, 6 * px, 0 * px, px, px);
                    g.FillRectangle(b, 7 * px, 0 * px, px, px);
                    g.FillRectangle(b, 7 * px, 1 * px, px, px);
                    g.FillRectangle(b, 6 * px, 2 * px, px, px);
                }
            }
            return bmp;
        }

        private Bitmap Create8BitPrintIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.Clear(Color.Transparent);

                int px = size / 8;
                Color cBody = Color.FromArgb(255, 100, 150);   // pink
                Color cPaper = Color.FromArgb(255, 255, 200);  // light yellow
                Color cDetail = Color.FromArgb(100, 200, 255); // sky blue

                // Paper top (sticking out)
                using (var b = new SolidBrush(cPaper))
                {
                    g.FillRectangle(b, 2 * px, 0 * px, 4 * px, 2 * px);
                }
                // Printer body
                using (var b = new SolidBrush(cBody))
                {
                    g.FillRectangle(b, 1 * px, 2 * px, 6 * px, 3 * px);
                }
                // Detail dots on printer
                using (var b = new SolidBrush(cDetail))
                {
                    g.FillRectangle(b, 5 * px, 3 * px, px, px);
                    g.FillRectangle(b, 6 * px, 3 * px, px, px);
                }
                // Paper output (bottom)
                using (var b = new SolidBrush(cPaper))
                {
                    g.FillRectangle(b, 2 * px, 5 * px, 4 * px, 3 * px);
                }
                // Text lines on output paper
                using (var b = new SolidBrush(Color.FromArgb(120, 120, 120)))
                {
                    g.FillRectangle(b, 3 * px, 6 * px, 2 * px, px);
                    g.FillRectangle(b, 3 * px, 7 * px, 2 * px, px);
                }
            }
            return bmp;
        }

        // 8-BIT PIXEL ART: Wiki/Book icon
        private Bitmap Create8BitWikiIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.Clear(Color.Transparent);
                int px = size / 8;
                Color cCover = Color.FromArgb(100, 180, 255);   // blue cover
                Color cPages = Color.FromArgb(255, 255, 220);   // cream pages
                using (var b = new SolidBrush(cCover))
                {
                    g.FillRectangle(b, 1 * px, 0 * px, 6 * px, 7 * px);
                }
                using (var b = new SolidBrush(cPages))
                {
                    g.FillRectangle(b, 2 * px, 1 * px, 4 * px, 5 * px);
                }
                using (var b = new SolidBrush(Color.FromArgb(70, 130, 200)))
                {
                    g.FillRectangle(b, 1 * px, 7 * px, 6 * px, px);
                }
            }
            return bmp;
        }

        // 8-BIT PIXEL ART: Ping/Bell icon
        private Bitmap Create8BitPingIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.Clear(Color.Transparent);
                int px = size / 8;
                Color cBell = Color.FromArgb(255, 190, 50);    // golden bell
                using (var b = new SolidBrush(cBell))
                {
                    g.FillRectangle(b, 3 * px, 0 * px, 2 * px, px);
                    g.FillRectangle(b, 2 * px, 1 * px, 4 * px, px);
                    g.FillRectangle(b, 1 * px, 2 * px, 6 * px, px);
                    g.FillRectangle(b, 1 * px, 3 * px, 6 * px, px);
                    g.FillRectangle(b, 1 * px, 4 * px, 6 * px, px);
                    g.FillRectangle(b, 0 * px, 5 * px, 8 * px, px);
                }
                using (var b = new SolidBrush(Color.FromArgb(255, 100, 80)))
                {
                    g.FillRectangle(b, 3 * px, 6 * px, 2 * px, px);
                }
            }
            return bmp;
        }

        // 8-BIT PIXEL ART: Notes/Document icon
        private Bitmap Create8BitNotesIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.Clear(Color.Transparent);
                int px = size / 8;
                Color cPaper = Color.FromArgb(220, 240, 255);
                Color cLine = Color.FromArgb(100, 160, 220);
                using (var b = new SolidBrush(cPaper))
                {
                    g.FillRectangle(b, 1 * px, 0 * px, 6 * px, 8 * px);
                }
                using (var b = new SolidBrush(cLine))
                {
                    g.FillRectangle(b, 2 * px, 2 * px, 4 * px, px);
                    g.FillRectangle(b, 2 * px, 4 * px, 3 * px, px);
                    g.FillRectangle(b, 2 * px, 6 * px, 4 * px, px);
                }
            }
            return bmp;
        }

        // 8-BIT PIXEL ART: Lightning/Quick icon
        private Bitmap Create8BitQuickIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.Clear(Color.Transparent);
                int px = size / 8;
                Color cBolt = Color.FromArgb(255, 220, 50);
                using (var b = new SolidBrush(cBolt))
                {
                    g.FillRectangle(b, 4 * px, 0 * px, 2 * px, px);
                    g.FillRectangle(b, 3 * px, 1 * px, 2 * px, px);
                    g.FillRectangle(b, 2 * px, 2 * px, 2 * px, px);
                    g.FillRectangle(b, 1 * px, 3 * px, 4 * px, px);
                    g.FillRectangle(b, 3 * px, 4 * px, 2 * px, px);
                    g.FillRectangle(b, 4 * px, 5 * px, 2 * px, px);
                    g.FillRectangle(b, 3 * px, 6 * px, 2 * px, px);
                    g.FillRectangle(b, 2 * px, 7 * px, 2 * px, px);
                }
            }
            return bmp;
        }

        // 8-BIT PIXEL ART: Timer/Pomodoro icon
        private Bitmap Create8BitPomodoroIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.Clear(Color.Transparent);
                int px = size / 8;
                Color cBody = Color.FromArgb(255, 80, 60);     // tomato red
                Color cLeaf = Color.FromArgb(80, 200, 80);     // green stem
                using (var b = new SolidBrush(cLeaf))
                {
                    g.FillRectangle(b, 3 * px, 0 * px, 2 * px, px);
                }
                using (var b = new SolidBrush(cBody))
                {
                    g.FillRectangle(b, 2 * px, 1 * px, 4 * px, px);
                    g.FillRectangle(b, 1 * px, 2 * px, 6 * px, px);
                    g.FillRectangle(b, 1 * px, 3 * px, 6 * px, px);
                    g.FillRectangle(b, 1 * px, 4 * px, 6 * px, px);
                    g.FillRectangle(b, 1 * px, 5 * px, 6 * px, px);
                    g.FillRectangle(b, 2 * px, 6 * px, 4 * px, px);
                }
            }
            return bmp;
        }

        // 8-BIT PIXEL ART: Teams/People icon
        private Bitmap Create8BitTeamsIcon(int size)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.Clear(Color.Transparent);
                int px = size / 8;
                Color c1 = Color.FromArgb(100, 200, 255);      // person 1 (blue)
                Color c2 = Color.FromArgb(255, 140, 90);       // person 2 (orange)
                // Person 1 (left)
                using (var b = new SolidBrush(c1))
                {
                    g.FillRectangle(b, 1 * px, 1 * px, 2 * px, 2 * px);  // head
                    g.FillRectangle(b, 0 * px, 4 * px, 4 * px, 3 * px);  // body
                }
                // Person 2 (right)
                using (var b = new SolidBrush(c2))
                {
                    g.FillRectangle(b, 5 * px, 1 * px, 2 * px, 2 * px);  // head
                    g.FillRectangle(b, 4 * px, 4 * px, 4 * px, 3 * px);  // body
                }
            }
            return bmp;
        }

        private void ApplyRoundedCorners(Button btn, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(btn.Width - d, 0, d, d, 270, 90);
            path.AddArc(btn.Width - d, btn.Height - d, d, d, 0, 90);
            path.AddArc(0, btn.Height - d, d, d, 90, 90);
            path.CloseFigure();
            btn.Region = new Region(path);
        }

        private void ApplyRoundedCorners(Control ctrl, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(ctrl.Width - d, 0, d, d, 270, 90);
            path.AddArc(ctrl.Width - d, ctrl.Height - d, d, d, 0, 90);
            path.AddArc(0, ctrl.Height - d, d, d, 90, 90);
            path.CloseFigure();
            ctrl.Region = new Region(path);
        }

        private void ApplyCircularButton(Button btn)
        {
            var path = new GraphicsPath();
            path.AddEllipse(0, 0, Math.Max(1, btn.Width - 1), Math.Max(1, btn.Height - 1));
            btn.Region = new Region(path);
        }

        private void labelMessage_Click_1(object sender, EventArgs e)
        {
            // Redirect to the version update click handler
            LabelMessage_Click(sender, e);
        }

        private void labelVersion_Click(object sender, EventArgs e)
        {
            // If update is available, show the update dialog
            if (_pendingVersionInfo != null)
            {
                LabelMessage_Click(sender, e);
            }
        }

        // --------------------------------------------------------
        //  CSV EXPORT
        // --------------------------------------------------------
        private void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                if (dataGridView1.Rows.Count == 0)
                {
                    MessageBox.Show("No data to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*";
                    sfd.FileName = $"WorkTimeExport_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                    sfd.Title = "Export Work Time Data";

                    if (sfd.ShowDialog() != DialogResult.OK) return;

                    using (var sw = new StreamWriter(sfd.FileName, false, System.Text.Encoding.UTF8))
                    {
                        // Write header row
                        var visibleColumns = new List<DataGridViewColumn>();
                        foreach (DataGridViewColumn col in dataGridView1.Columns)
                        {
                            if (col.Visible)
                                visibleColumns.Add(col);
                        }

                        var headers = new List<string>();
                        foreach (var c in visibleColumns) headers.Add(EscapeCsv(c.HeaderText));
                        sw.WriteLine(string.Join(",", headers));

                        // Write data rows
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                        {
                            if (row.IsNewRow) continue;
                            var values = new List<string>();
                            foreach (var col in visibleColumns)
                            {
                                values.Add(EscapeCsv(row.Cells[col.Index].Value?.ToString() ?? ""));
                            }
                            sw.WriteLine(string.Join(",", values));
                        }
                    }

                    MessageBox.Show($"Exported {dataGridView1.Rows.Count} records to:\n{sfd.FileName}",
                        "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        // --------------------------------------------------------
        //  AUTO-STOP TIMER (safety ? stops after 10 hours)
        // --------------------------------------------------------
        private void AutoStopTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (_workingTimer != null && _workingTimer.Enabled)
                {
                    var elapsed = DateTime.Now - _workStartedAt;
                    if (elapsed.TotalHours >= AUTO_STOP_HOURS)
                    {
                        UpdateRichTextBox($"[i] Auto-stop triggered after {AUTO_STOP_HOURS} hours.\r\n");
                        buttonStop_Click(buttonStop, EventArgs.Empty);
                        MessageBox.Show(
                            $"Your timer has been running for {AUTO_STOP_HOURS} hours and was automatically stopped.\n\nDid you forget to stop it?",
                            "Auto-Stop", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch { }
        }

        // --------------------------------------------------------
        //  JIRA SYNC ? STICKER BOARD
        // --------------------------------------------------------
        private async Task SyncJiraToStickersAsync()
        {
            try
            {
                UpdateRichTextBox("[JIRA] Syncing issues to sticker board...\r\n");
                var issues = await _jira.GetMyIssuesAsync();

                if (issues == null || issues.Count == 0)
                {
                    MessageBox.Show("No open Jira issues assigned to you.", "Jira Sync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int created = 0;
                foreach (var issue in issues)
                {
                    // Create a sticker for each issue (via Firebase, same as sticker board)
                    string stickerUrl = firebaseUrl.Replace("logs.json", "stickers.json");

                    // Check if sticker with this Jira key already exists
                    var existingResp = await _httpClient.GetAsync(stickerUrl);
                    string existingJson = await existingResp.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(existingJson) && existingJson != "null" && existingJson.Contains(issue.Key))
                        continue; // skip duplicate

                    string priority = issue.Priority ?? "Medium";
                    string label = issue.IssueType == "Epic" ? "EPIC" :
                                   issue.IssueType == "Feature" ? "FEATURE" : "TODO";

                    var stickerData = new
                    {
                        title = $"[{issue.Key}] {issue.Summary}",
                        body = $"Status: {issue.Status} | Priority: {priority}\nAssignee: {issue.Assignee ?? "Unassigned"}",
                        labels = new[] { label, priority.ToUpper() },
                        author = _currentUser.Name,
                        createdAt = DateTime.UtcNow.ToString("o"),
                        done = issue.Status == "Done"
                    };

                    var content = new StringContent(JsonConvert.SerializeObject(stickerData), Encoding.UTF8, "application/json");
                    await _httpClient.PostAsync(stickerUrl, content);
                    created++;
                }

                await _stickerBoard.RefreshAsync();
                UpdateRichTextBox($"[JIRA] Synced {created} new issues as stickers.\r\n");

                if (created > 0)
                    MessageBox.Show($"Synced {created} Jira issues to the sticker board!", "Jira Sync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                else
                    MessageBox.Show("All Jira issues are already on the board.", "Jira Sync", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");

                UpdateRichTextBox($"[JIRA] Sync failed: {ex.Message}\r\n");
                MessageBox.Show($"Jira sync failed: {ex.Message}\n\nCheck your Jira settings.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --------------------------------------------------------
        //  JIRA WORKLOG ? auto-log hours when stopping timer
        // --------------------------------------------------------
        private async Task TryLogToJiraAsync(string description, TimeSpan duration)
        {
            try
            {
                if (!_jira.IsConfigured || duration.TotalMinutes < 1) return;

                // Check if description contains a Jira issue key like "SCRUM-123"
                var match = System.Text.RegularExpressions.Regex.Match(description ?? "", @"[A-Z]+-\d+");
                if (!match.Success) return;

                string issueKey = match.Value;
                bool logged = await _jira.AddWorklogAsync(issueKey, duration, description);
                if (logged)
                    UpdateRichTextBox($"[JIRA] Logged {duration:hh\\:mm} to {issueKey}\r\n");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] Exception caught: {ex.GetType().Name} - {ex.Message}");

                UpdateRichTextBox($"[JIRA] Worklog failed: {ex.Message}\r\n");
            }
        }

        // --------------------------------------------------------
        //  STANDUP SUMMARY
        // --------------------------------------------------------
        /// <summary>
        /// Build a standup section from today's organizer entries.
        /// Shows planned meetings, tasks, interviews, and reminders.
        /// </summary>
        private string BuildOrganizerStandupSection()
        {
            try
            {
                string today = DateTime.Today.ToString("yyyy-MM-dd");
                var entries = OrganizerStorage.GetEntriesForDate(today);
                if (entries == null || entries.Count == 0)
                    return "";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine();
                sb.AppendLine("Today's Schedule (Calendar):");

                // Group by category for clean display
                var grouped = entries
                    .Where(e => e.Status != OrganizerStatus.Cancelled)
                    .OrderBy(e => e.TimeFrom)
                    .ToList();

                foreach (var entry in grouped)
                {
                    string icon = "-";
                    switch (entry.Category)
                    {
                        case OrganizerCategory.Meeting: icon = "[M]"; break;
                        case OrganizerCategory.Interview: icon = "[I]"; break;
                        case OrganizerCategory.Call: icon = "[C]"; break;
                        case OrganizerCategory.Task: icon = "-"; break;
                        case OrganizerCategory.Reminder: icon = "[R]"; break;
                        case OrganizerCategory.Personal: icon = "[P]"; break;
                    }

                    string timeStr = "";
                    if (!string.IsNullOrEmpty(entry.TimeFrom))
                    {
                        timeStr = entry.TimeFrom;
                        if (!string.IsNullOrEmpty(entry.TimeTo))
                            timeStr += "-" + entry.TimeTo;
                        timeStr = $" ({timeStr})";
                    }

                    string status = "";
                    if (entry.IsCompleted || entry.Status == OrganizerStatus.Done)
                        status = " ? done";
                    else if (entry.Status == OrganizerStatus.Postponed)
                        status = " ? postponed";

                    sb.AppendLine($"{icon} {entry.Title}{timeStr}{status}");
                }

                // Summary line
                int total = grouped.Count;
                int done = grouped.Count(e => e.IsCompleted || e.Status == OrganizerStatus.Done);
                sb.AppendLine($"   ? {total} items, {done} completed");

                // Daily notes preview
                string notes = OrganizerStorage.GetDailyNotes(today);
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    string preview = notes.Length > 120 ? notes.Substring(0, 120) + "..." : notes;
                    sb.AppendLine();
                    sb.AppendLine($"Notes: {preview}");
                }

                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        private void ShowStandupSummary()
        {
            try
            {
                // Get current grid data
                var logs = new List<LogEntryWithIndex>();
                if (dataGridView1.DataSource is List<LogEntryWithIndex> gridLogs)
                    logs = gridLogs;

                string standup = StandupGenerator.GenerateStandup(logs, _currentUser.Name, DateTime.Today);
                string weekly = StandupGenerator.GenerateWeeklySummary(logs, _currentUser.Name, DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1));

                // -- CALENDAR SYNC: Inject today's organizer entries into the standup --
                string organizerSection = BuildOrganizerStandupSection();

                var form = new Form
                {
                    Text = "Daily Standup Summary",
                    Size = new System.Drawing.Size(550, 500),
                    StartPosition = FormStartPosition.CenterParent,
                    BackColor = isDarkMode ? Color.FromArgb(24, 28, 38) : Color.White,
                    ForeColor = isDarkMode ? Color.FromArgb(220, 225, 240) : Color.FromArgb(30, 35, 50),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var txt = new RichTextBox
                {
                    Text = standup + organizerSection + "\n" + new string('-', 50) + "\n\n" + weekly,
                    Dock = DockStyle.Fill,
                    ReadOnly = true,
                    BorderStyle = BorderStyle.None,
                    BackColor = form.BackColor,
                    ForeColor = form.ForeColor,
                    Font = new System.Drawing.Font("Consolas", 9.5f)
                };

                var btnCopy = new Button
                {
                    Text = "Copy to Clipboard",
                    Dock = DockStyle.Bottom,
                    Height = 36,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(124, 58, 237),
                    ForeColor = Color.White,
                    Font = new System.Drawing.Font("Segoe UI", 9, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                btnCopy.FlatAppearance.BorderSize = 0;
                btnCopy.Click += (s, e) =>
                {
                    Clipboard.SetText(txt.Text);
                    MessageBox.Show("Copied to clipboard!", "Standup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                };

                form.Controls.Add(txt);
                form.Controls.Add(btnCopy);
                form.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to generate standup: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --------------------------------------------------------
        //  POMODORO PANEL TOGGLE
        // --------------------------------------------------------
        private void TogglePomodoroPanel()
        {
            if (_pomodoroPanel == null)
            {
                _pomodoroPanel = new PomodoroPanel(isDarkMode);
                _pomodoroPanel.Size = new System.Drawing.Size(280, 340);
                _pomodoroPanel.Location = new Point(
                    this.ClientSize.Width / 2 - 140,
                    this.ClientSize.Height / 2 - 170);
                _pomodoroPanel.Anchor = AnchorStyles.None;
                _pomodoroPanel.BorderStyle = BorderStyle.FixedSingle;
                this.Controls.Add(_pomodoroPanel);
                _pomodoroPanel.BringToFront();
            }
            else
            {
                _pomodoroPanel.Visible = !_pomodoroPanel.Visible;
                if (_pomodoroPanel.Visible)
                    _pomodoroPanel.BringToFront();
            }
        }

        // --------------------------------------------------------
        //  RECURRING TASKS / QUICK-START PRESETS
        // --------------------------------------------------------
        private void LoadRecurringTasks()
        {
            try
            {
                if (File.Exists(_recurringTasksPath))
                {
                    string json = File.ReadAllText(_recurringTasksPath);
                    _recurringTasks = JsonConvert.DeserializeObject<List<RecurringTask>>(json) ?? new List<RecurringTask>();
                }

                if (_recurringTasks.Count == 0)
                {
                    // Default presets
                    _recurringTasks = new List<RecurringTask>
                    {
                        new RecurringTask { Description = "Daily standup meeting", Project = "Meeting", Shortcut = "F2" },
                        new RecurringTask { Description = "Code review", Project = "Development", Shortcut = "F3" },
                        new RecurringTask { Description = "Bug fixing", Project = "Support", Shortcut = "F4" },
                        new RecurringTask { Description = "Documentation", Project = "Documentation", Shortcut = "" },
                        new RecurringTask { Description = "Testing", Project = "Testing", Shortcut = "" }
                    };
                    SaveRecurringTasks();
                }
            }
            catch { }
        }

        private void SaveRecurringTasks()
        {
            try
            {
                string dir = Path.GetDirectoryName(_recurringTasksPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_recurringTasksPath, JsonConvert.SerializeObject(_recurringTasks, Formatting.Indented));
            }
            catch { }
        }

        private void ShowQuickTaskMenu(Control anchor)
        {
            var menu = new ContextMenuStrip();
            menu.BackColor = isDarkMode ? Color.FromArgb(32, 38, 50) : Color.White;
            menu.ForeColor = isDarkMode ? Color.FromArgb(220, 225, 240) : Color.FromArgb(30, 35, 50);
            menu.Font = new System.Drawing.Font("Segoe UI", 9);

            foreach (var task in _recurringTasks)
            {
                string shortcutText = string.IsNullOrEmpty(task.Shortcut) ? "" : $"  [{task.Shortcut}]";
                var item = new ToolStripMenuItem($"{task.Description} ({task.Project}){shortcutText}");
                item.Click += async (s, e) =>
                {
                    // Set the description and project, then auto-start
                    richTextBoxDescription.Text = task.Description;
                    if (_cmbProject != null)
                    {
                        int idx = _cmbProject.Items.IndexOf(task.Project);
                        if (idx >= 0) _cmbProject.SelectedIndex = idx;
                    }
                    buttonStart_Click(buttonStart, EventArgs.Empty);
                };
                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());

            var manageItem = new ToolStripMenuItem("Manage Presets...");
            manageItem.Click += (s, e) => ShowManageRecurringTasksDialog();
            menu.Items.Add(manageItem);

            menu.Show(anchor, new Point(0, anchor.Height));
        }

        private void ShowManageRecurringTasksDialog()
        {
            var form = new Form
            {
                Text = "Manage Quick-Start Presets",
                Size = new System.Drawing.Size(500, 400),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = isDarkMode ? Color.FromArgb(24, 28, 38) : Color.White,
                ForeColor = isDarkMode ? Color.FromArgb(220, 225, 240) : Color.FromArgb(30, 35, 50),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = form.BackColor,
                ForeColor = Color.FromArgb(30, 35, 50),
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToDeleteRows = true,
                AllowUserToAddRows = true
            };
            dgv.Columns.Add("Description", "Description");
            dgv.Columns.Add("Project", "Project");
            dgv.Columns.Add("Shortcut", "Shortcut (F2-F12)");

            foreach (var task in _recurringTasks)
                dgv.Rows.Add(task.Description, task.Project, task.Shortcut);

            var btnSave = new Button
            {
                Text = "Save",
                Dock = DockStyle.Bottom,
                Height = 36,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(16, 185, 129),
                ForeColor = Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                _recurringTasks.Clear();
                foreach (DataGridViewRow row in dgv.Rows)
                {
                    if (row.IsNewRow) continue;
                    string desc = row.Cells[0].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(desc)) continue;
                    _recurringTasks.Add(new RecurringTask
                    {
                        Description = desc,
                        Project = row.Cells[1].Value?.ToString() ?? "General",
                        Shortcut = row.Cells[2].Value?.ToString() ?? ""
                    });
                }
                SaveRecurringTasks();
                form.Close();
            };

            form.Controls.Add(dgv);
            form.Controls.Add(btnSave);
            form.ShowDialog(this);
        }

        // --------------------------------------------------------
        //  MONTHLY PDF REPORT
        // --------------------------------------------------------
        private void GenerateMonthlyPdfReport()
        {
            try
            {
                var logs = new List<LogEntryWithIndex>();
                if (dataGridView1.DataSource is List<LogEntryWithIndex> gridLogs)
                    logs = gridLogs;

                if (logs.Count == 0)
                {
                    MessageBox.Show("No data to generate report.", "Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PDF files (*.pdf)|*.pdf";
                    sfd.FileName = $"MonthlyReport_{_currentUser.Name}_{DateTime.Now:yyyyMM}.pdf";
                    sfd.Title = "Save Monthly Report";

                    if (sfd.ShowDialog() != DialogResult.OK) return;

                    using (var fs = new FileStream(sfd.FileName, FileMode.Create))
                    {
                        var doc = new iTextSharp.text.Document(PageSize.A4, 40, 40, 40, 40);
                        PdfWriter.GetInstance(doc, fs);
                        doc.Open();

                        // Team name header
                        var teamInfo = UserStorage.LoadTeam();
                        string teamDisplayName = teamInfo?.TeamName ?? "Unknown Team";

                        var teamFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 12, iTextSharp.text.Font.BOLD, new BaseColor(100, 120, 140));
                        doc.Add(new Paragraph($"Team: {teamDisplayName}", teamFont));

                        // Title
                        var titleFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 18, iTextSharp.text.Font.BOLD, new BaseColor(30, 35, 50));
                        doc.Add(new Paragraph($"Monthly Work Report ? {_currentUser.Name}", titleFont));
                        doc.Add(new Paragraph($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}", new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10, iTextSharp.text.Font.NORMAL, BaseColor.GRAY)));
                        doc.Add(new Paragraph(" "));

                        // Summary section
                        var sectionFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 13, iTextSharp.text.Font.BOLD, new BaseColor(59, 130, 246));
                        var bodyFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10);

                        // Group by date
                        var byDate = logs.Where(l => l.userName == _currentUser.Name)
                            .GroupBy(l =>
                            {
                                DateTime d;
                                DateTime.TryParse(l.timestamp, out d);
                                return d.Date;
                            })
                            .OrderBy(g => g.Key);

                        TimeSpan grandTotal = TimeSpan.Zero;

                        doc.Add(new Paragraph("Daily Breakdown", sectionFont));
                        doc.Add(new Paragraph(" "));

                        // Table
                        var table = new PdfPTable(4) { WidthPercentage = 100 };
                        table.SetWidths(new float[] { 25, 35, 20, 20 });

                        var headerBgColor = new BaseColor(59, 130, 246);
                        var headerFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 10, iTextSharp.text.Font.BOLD, BaseColor.WHITE);
                        foreach (string h in new[] { "Date", "Description", "Project", "Duration" })
                        {
                            var cell = new PdfPCell(new Phrase(h, headerFont)) { BackgroundColor = headerBgColor, Padding = 6 };
                            table.AddCell(cell);
                        }

                        foreach (var group in byDate)
                        {
                            foreach (var log in group)
                            {
                                TimeSpan dur = TimeSpan.Zero;
                                if (!string.IsNullOrEmpty(log.workingTime) && log.workingTime.Contains(":"))
                                    TimeSpan.TryParse(log.workingTime, out dur);
                                grandTotal += dur;

                                table.AddCell(new PdfPCell(new Phrase(group.Key.ToString("dd MMM yyyy"), bodyFont)) { Padding = 4 });
                                table.AddCell(new PdfPCell(new Phrase(log.description ?? "", bodyFont)) { Padding = 4 });
                                table.AddCell(new PdfPCell(new Phrase(log.project ?? "General", bodyFont)) { Padding = 4 });
                                table.AddCell(new PdfPCell(new Phrase(log.workingTime ?? "00:00:00", bodyFont)) { Padding = 4 });
                            }
                        }

                        doc.Add(table);
                        doc.Add(new Paragraph(" "));

                        // Grand total
                        var totalFont = new iTextSharp.text.Font(iTextSharp.text.Font.FontFamily.HELVETICA, 12, iTextSharp.text.Font.BOLD, new BaseColor(16, 185, 129));
                        doc.Add(new Paragraph($"Total Hours: {(int)grandTotal.TotalHours}h {grandTotal.Minutes:D2}m", totalFont));

                        // Project breakdown
                        doc.Add(new Paragraph(" "));
                        doc.Add(new Paragraph("By Project", sectionFont));
                        var byProject = logs.Where(l => l.userName == _currentUser.Name)
                            .GroupBy(l => l.project ?? "General");
                        foreach (var pg in byProject)
                        {
                            TimeSpan projTotal = TimeSpan.Zero;
                            foreach (var l in pg)
                            {
                                TimeSpan d2 = TimeSpan.Zero;
                                if (!string.IsNullOrEmpty(l.workingTime) && l.workingTime.Contains(":"))
                                    TimeSpan.TryParse(l.workingTime, out d2);
                                projTotal += d2;
                            }
                            doc.Add(new Paragraph($"  ? {pg.Key}: {(int)projTotal.TotalHours}h {projTotal.Minutes:D2}m ({pg.Count()} entries)", bodyFont));
                        }

                        doc.Close();
                    }

                    MessageBox.Show($"Report saved to:\n{sfd.FileName}", "Report Generated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Report generation failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // --------------------------------------------------------
        //  TODAY'S HOURS CALCULATION
        // --------------------------------------------------------
        private void UpdateTodayHours(List<LogEntryWithIndex> logs)
        {
            try
            {
                if (_lblHoursToday == null) return;

                string currentUserName = _currentUser?.Name ?? "";
                TimeSpan totalToday = TimeSpan.Zero;
                TimeSpan totalWeek = TimeSpan.Zero;

                // Calculate start of current week (Monday)
                DateTime today = DateTime.Today;
                int diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateTime weekStart = today.AddDays(-diff);

                foreach (var log in logs)
                {
                    if (log.userName != currentUserName) continue;

                    // Parse working time (format: "HH:mm:ss" or "H:mm:ss")
                    TimeSpan duration = TimeSpan.Zero;
                    if (!string.IsNullOrEmpty(log.workingTime))
                    {
                        string wt = log.workingTime.Trim();
                        // Only parse if it looks like a time format (contains ":")
                        // Plain numbers like "1" would be misread as 1 DAY by TimeSpan.TryParse
                        if (wt.Contains(":"))
                        {
                            TimeSpan.TryParse(wt, out duration);
                        }
                        else
                        {
                            // Try to treat plain number as seconds
                            int sec;
                            if (int.TryParse(wt, out sec) && sec >= 0 && sec < 86400)
                                duration = TimeSpan.FromSeconds(sec);
                        }
                    }

                    // Parse date
                    DateTime logDate;
                    if (!DateTime.TryParse(log.timestamp, out logDate))
                        continue;

                    if (logDate.Date == today)
                        totalToday += duration;

                    if (logDate.Date >= weekStart && logDate.Date <= today)
                        totalWeek += duration;
                }

                string todayStr = $"{(int)totalToday.TotalHours}h {totalToday.Minutes:D2}m";
                string weekStr = $"{(int)totalWeek.TotalHours}h {totalWeek.Minutes:D2}m";
                _lblHoursToday.Text = $"Today: {todayStr}  |  Week: {weekStr}";
            }
            catch
            {
                if (_lblHoursToday != null)
                    _lblHoursToday.Text = "Today: --";
            }
        }

        // --------------------------------------------------------
        //  KEYBOARD SHORTCUTS
        // --------------------------------------------------------
        protected override bool ProcessCmdKey(ref System.Windows.Forms.Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5:
                    RefreshLogs();
                    return true;

                case Keys.Control | Keys.Enter:
                    // Toggle Start/Stop ? if currently working, stop; otherwise start
                    if (buttonStop.Enabled && buttonStart.Enabled)
                    {
                        // Both enabled = idle state, so start
                        buttonStart_Click(buttonStart, EventArgs.Empty);
                    }
                    else if (!buttonStart.Enabled)
                    {
                        // Start disabled = currently working, so stop
                        buttonStop_Click(buttonStop, EventArgs.Empty);
                    }
                    else
                    {
                        buttonStart_Click(buttonStart, EventArgs.Empty);
                    }
                    return true;

                case Keys.Control | Keys.E:
                    // Export CSV (reusing existing handler)
                    BtnExport_Click(null, EventArgs.Empty);
                    return true;

                case Keys.Control | Keys.P:
                    GenerateMonthlyPdfReport();
                    return true;

                case Keys.Control | Keys.D:
                    ShowStandupSummary();
                    return true;
            }

            // Handle F2-F12 for recurring task quick-start
            if (keyData >= Keys.F2 && keyData <= Keys.F12)
            {
                string keyName = keyData.ToString(); // "F2", "F3", etc.
                var task = _recurringTasks.FirstOrDefault(t => t.Shortcut == keyName);
                if (task != null)
                {
                    richTextBoxDescription.Text = task.Description;
                    if (_cmbProject != null)
                    {
                        int idx = _cmbProject.Items.IndexOf(task.Project);
                        if (idx >= 0) _cmbProject.SelectedIndex = idx;
                    }
                    buttonStart_Click(buttonStart, EventArgs.Empty);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ----------------------------------------------------------------------------
        // OPEN OR FOCUS DM WINDOW ? prevents duplicate chat windows per user
        // ----------------------------------------------------------------------------

        /// <summary>
        /// Opens a DM chat window for the target user.
        /// If a window is already open for that user, brings it to the front instead.
        /// </summary>
        private void OpenOrFocusDmForm(string targetUser)
        {
            // Check if we already have an open window for this user
            if (_openDmForms.ContainsKey(targetUser))
            {
                var existingForm = _openDmForms[targetUser];
                if (!existingForm.IsDisposed)
                {
                    // Bring existing window to front
                    existingForm.WindowState = FormWindowState.Normal;
                    existingForm.BringToFront();
                    existingForm.Activate();
                    return;
                }
                else
                {
                    // Window was closed/disposed ? remove from tracking
                    _openDmForms.Remove(targetUser);
                }
            }

            // Create new DM form
            string fbBase = UserStorage.GetGlobalFirebaseUrl();
            var dmForm = new DirectMessageForm(fbBase, _currentUser.Name, targetUser, isDarkMode);

            // Track the form and remove when closed
            _openDmForms[targetUser] = dmForm;
            dmForm.FormClosed += (s, e) =>
            {
                _openDmForms.Remove(targetUser);
            };

            dmForm.Show();
        }

        // ----------------------------------------------------------------------------
        // SYSTEM TRAY ? NOTIFYICON NEAR CLOCK + BALLOON NOTIFICATIONS
        // ----------------------------------------------------------------------------

        /// <summary>
        /// Sets up the system tray icon near the Windows clock.
        /// Shows context menu with Open/Exit options.
        /// Clicking the balloon tip or double-clicking the icon brings the app to front.
        /// </summary>
        private void SetupSystemTray()
        {
//             DebugLogger.Log("[Form1] SetupSystemTray() ENTRY");
            try
            {
                // Build context menu for right-click on tray icon with Open and Exit options
//                 DebugLogger.Log("[Form1] Creating context menu strip for tray icon");
                _trayMenu = new ContextMenuStrip();
                _trayMenu.Items.Add("Open WorkFlow", null, (s, e) => ShowFromTray());
                _trayMenu.Items.Add(new ToolStripSeparator());
                _trayMenu.Items.Add("Exit", null, (s, e) => { Application.Exit(); });

                // Create the system tray icon with user-friendly display name
//                 DebugLogger.Log($"[Form1] Creating NotifyIcon for user: {_currentUser.Name}");
                _trayIcon = new NotifyIcon
                {
                    Text = $"WorkFlow - {_currentUser.Name}",
                    ContextMenuStrip = _trayMenu,
                    Visible = true
                };

                // Load icon from project directory or fallback to main form icon
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
//                     DebugLogger.Log($"[Form1] Loading tray icon from: {iconPath}");
                    _trayIcon.Icon = new System.Drawing.Icon(iconPath);
                }
                else if (this.Icon != null)
                {
//                     DebugLogger.Log("[Form1] Using fallback icon from main form");
                    _trayIcon.Icon = this.Icon;
                }
                else
                {
//                     DebugLogger.Log("[Form1] WARNING: No icon file found, tray icon will display with default appearance");
                }

                // Double-click tray icon event ? restores window from minimized/hidden state
                _trayIcon.DoubleClick += (s, e) =>
                {
//                     DebugLogger.Log("[Form1] Tray icon double-clicked - restoring window to front");
                    ShowFromTray();
                };

                // Balloon tip clicked event ? brings window to front and marks milestone as permanently dismissed
                _trayIcon.BalloonTipClicked += (s, e) =>
                {
//                     DebugLogger.Log("[Form1] Balloon tip notification clicked");
                    ShowFromTray();
                    // Mark the current milestone as dismissed so it never shows again this week
                    if (_lastBalloonMilestoneKey != null)
                    {
//                         DebugLogger.Log($"[Form1] Dismissing milestone permanently: {_lastBalloonMilestoneKey}");
                        _milestoneDismissed.Add(_lastBalloonMilestoneKey);
                        SaveDismissedMilestones();
                        _lastBalloonMilestoneKey = null;
                    }
                };

                // Load previously dismissed milestones from local storage for session persistence
//                 DebugLogger.Log("[Form1] Loading previously dismissed milestones from local storage");
                LoadDismissedMilestones();

                _trayInitialized = true;
//                 DebugLogger.Log("[Form1] SetupSystemTray() SUCCESS - system tray icon initialized and ready");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[Form1] SetupSystemTray() ERROR: {ex.Message}");
                _trayInitialized = false;
            }
        }

        /// <summary>
        /// Restores the form from minimized/background state.
        /// </summary>
        private void ShowFromTray()
        {
            // Restore window: show, set to normal state, bring to foreground, and activate input focus
//             DebugLogger.Log("[Form1] ShowFromTray() ENTRY - restoring window from background");
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
//             DebugLogger.Log("[Form1] ShowFromTray() SUCCESS - window restored to foreground");
        }

        // ----------------------------------------------------------------------------
        // DM CHECK TIMER ? POLLS FIREBASE FOR NEW DIRECT MESSAGES EVERY 30 SECONDS
        // ----------------------------------------------------------------------------

        /// <summary>
        /// Sets up a background timer that checks all conversations for new DMs.
        /// Shows a balloon tip and flashes the taskbar when a new message arrives.
        /// </summary>
        private void SetupDmCheckTimer()
        {
//             DebugLogger.Log("[Form1] SetupDmCheckTimer() ENTRY - setting up background DM polling");
            // Create recurring timer that checks all conversations for new DMs every 30 seconds
            _dmCheckTimer = new Timer();
            _dmCheckTimer.Interval = 30000; // Check every 30 seconds
            _dmCheckTimer.Tick += async (s, e) =>
            {
                await CheckForNewDmAsync();
                await CheckProjectStageDeadlinesAsync();
            };
            _dmCheckTimer.Start();

            // Also do a first check after 5 seconds (to learn baseline message counts before checking for changes)
            var initialCheck = new Timer { Interval = 5000 };
            initialCheck.Tick += async (s, e) =>
            {
                initialCheck.Stop();
                initialCheck.Dispose();
                // Initial silent check to establish baseline message counts
                await CheckForNewDmAsync(isInitialCheck: true);
            };
            initialCheck.Start();
//             DebugLogger.Log("[Form1] SetupDmCheckTimer() SUCCESS - timer initialized");
        }

        /// <summary>
        /// Checks Firebase for new DMs in all conversations involving the current user.
        /// Compares message counts to detect new messages.
        /// </summary>
        private async Task CheckForNewDmAsync(bool isInitialCheck = false)
        {
            try
            {
                // Entry logging for non-initial checks (avoid spam from initial baseline check)
                                if (!isInitialCheck)
                {
//                     DebugLogger.Log("[Form1] CheckForNewDmAsync() ENTRY - polling Firebase for new DMs");
                }

                string fbBase = UserStorage.GetGlobalFirebaseUrl();

                // Check conversations with all known users
                foreach (var user in _allUsers)
                {
                    if (string.Equals(user.Name, _currentUser.Name, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Generate conversation key (same logic as DirectMessageForm)
                    var names = new[] { _currentUser.Name, user.Name };
                    Array.Sort(names);
                    string conversationId = string.Join("_", names);

                    string url = $"{fbBase}/global_dm/{conversationId}.json?shallow=true";

                    var response = await _httpClient.GetAsync(url);
                    if (!response.IsSuccessStatusCode) continue;

                    string json = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(json) || json == "null") continue;

                    // Count messages by counting keys in shallow response
                    var keys = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json);
                    int messageCount = keys?.Count ?? 0;

                    if (!_lastKnownDmCounts.ContainsKey(conversationId))
                    {
                        // First time seeing this conversation ? save baseline
                        _lastKnownDmCounts[conversationId] = messageCount;
                        continue;
                    }

                    int previousCount = _lastKnownDmCounts[conversationId];
                    _lastKnownDmCounts[conversationId] = messageCount;

                    // New messages detected and this is not the initial baseline check
                    if (!isInitialCheck && messageCount > previousCount)
                    {
                        // Fetch the latest message to check if it's from the other user
                        string detailUrl = $"{fbBase}/global_dm/{conversationId}.json?orderBy=\"timestamp\"&limitToLast=1";
                        var detailResponse = await _httpClient.GetAsync(detailUrl);
                        if (!detailResponse.IsSuccessStatusCode) continue;

                        string detailJson = await detailResponse.Content.ReadAsStringAsync();
                        if (string.IsNullOrWhiteSpace(detailJson) || detailJson == "null") continue;

                        var messages = JsonConvert.DeserializeObject<Dictionary<string, DirectMessage>>(detailJson);
                        var lastMsg = messages?.Values.FirstOrDefault();

                        // Only notify if the message is FROM the other user (not our own)
                        if (lastMsg != null && !string.Equals(lastMsg.fromUser, _currentUser.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            ShowDmNotification(lastMsg.fromUser, lastMsg.message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!isInitialCheck)
                    DebugLogger.Log($"[Form1] CheckForNewDmAsync() ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Shows a balloon tip notification near the clock and flashes the taskbar.
        /// </summary>
        private void ShowDmNotification(string fromUser, string messageText)
        {
//             DebugLogger.Log($"[Form1] ShowDmNotification() ENTRY - from {fromUser}");
            if (!_trayInitialized || _trayIcon == null) return;

            // Truncate long messages to fit in balloon notification (80 char limit with ellipsis)
            string preview = string.IsNullOrEmpty(messageText) ? "(sticker)" : messageText;
            if (preview.Length > 80) preview = preview.Substring(0, 77) + "...";

            // Display balloon tip notification near Windows clock with sender name and message preview
            _trayIcon.BalloonTipTitle = $"New message from {fromUser}";
            _trayIcon.BalloonTipText = preview;
            _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            _trayIcon.ShowBalloonTip(5000);

            // Also flash the taskbar icon to attract user attention
            FlashMainTaskbar();
//             DebugLogger.Log("[Form1] ShowDmNotification() SUCCESS - balloon displayed and taskbar flashed");
        }

        /// <summary>
        /// Flashes the main form taskbar icon until focused.
        /// </summary>
        private void FlashMainTaskbar()
        {
            // Flash the main form taskbar icon to attract user attention until window is focused
//             DebugLogger.Log("[Form1] FlashMainTaskbar() - flashing taskbar to notify user");
            if (this.ContainsFocus) return;
            var fi = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                hwnd = this.Handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = uint.MaxValue,
                dwTimeout = 0
            };
            FlashWindowEx(ref fi);
        }

        // ----------------------------------------------------------------------------
        // PROJECT STAGE DEADLINE CHECK ? NOTIFY WHEN MILESTONES ARE "IN PROGRESS"
        // ----------------------------------------------------------------------------

        private DateTime _lastStageNotification = DateTime.MinValue;

        /// <summary>
        /// Checks Firebase for project stages and notifies about active milestones.
        /// Runs once per hour (controlled by timestamp check) to avoid spam.
        /// </summary>
        private async Task CheckProjectStageDeadlinesAsync()
        {
            try
            {
                // Only check once per hour to avoid notification spam
                if ((DateTime.Now - _lastStageNotification).TotalHours < 1)
                    return;

                string fbBase = UserStorage.GetFirebaseLogsUrl();
                string url = $"{fbBase}/project_stages.json";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null") return;

                var stages = JsonConvert.DeserializeObject<Dictionary<string, ProjectStage>>(json);
                if (stages == null) return;

                var inProgressStages = stages.Values
                    .Where(s => s.status == "In Progress")
                    .OrderBy(s => s.order)
                    .ToList();

                if (inProgressStages.Count > 0 && _trayInitialized && _trayIcon != null)
                {
                    string stageNames = string.Join(", ", inProgressStages.Select(s => s.name));
                    _trayIcon.BalloonTipTitle = "Active Milestones";
                    _trayIcon.BalloonTipText = $"In Progress: {stageNames}";
                    _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    _trayIcon.ShowBalloonTip(5000);

                    _lastStageNotification = DateTime.Now;
                }
            }
            catch
            {
                // Silently ignore
            }
        }

        // ----------------------------------------------------------------------------
        // WEEKLY HOURS ? REFRESH PROGRESS BARS + MOTIVATIONAL MESSAGES
        // ----------------------------------------------------------------------------

        /// <summary>
        /// Fetches all time logs from Firebase, calculates weekly hours per user,
        /// updates OnlineUserControl progress bars, local time displays,
        /// and shows motivational balloon tips at milestones.
        /// </summary>
        private async Task RefreshWeeklyHoursAsync()
        {
            try
            {
                Dictionary<string, LogEntryWithIndex> logsDict = null;

                // -- Try Firebase first --
                try
                {
                    string fbBase = UserStorage.GetFirebaseBaseUrl();
                    string logsUrl = $"{fbBase}/logs.json";
                    var response = await _httpClient.GetAsync(logsUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json) && json != "null")
                            logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntryWithIndex>>(json);
                    }
                }
                catch
                {
                    logsDict = null;
                }

                // -- Fallback: local cache + pending merges/deletes (offline-safe progress bars) --
                if (logsDict == null || logsDict.Count == 0)
                {
                    var localLogs = LoadLocalLogsCache();
                    localLogs = MergePendingStopLogsIntoDictionary(localLogs);
                    localLogs = MergePendingLogUpsertsIntoDictionary(localLogs);
                    localLogs = ApplyPendingLogDeletesToDictionary(localLogs);

                    logsDict = localLogs.ToDictionary(
                        kv => kv.Key,
                        kv => new LogEntryWithIndex
                        {
                            Key = kv.Key,
                            description = kv.Value?.description,
                            startTime = kv.Value?.startTime,
                            workingTime = kv.Value?.workingTime,
                            timestamp = kv.Value?.timestamp,
                            status = kv.Value?.status,
                            userId = kv.Value?.userId,
                            userName = kv.Value?.userName,
                            project = kv.Value?.project,
                            platform = kv.Value?.platform
                        },
                        StringComparer.OrdinalIgnoreCase);
                }

                if (logsDict == null || logsDict.Count == 0) return;

                // -- Calculate week boundaries (Monday to Sunday) --
                var now = DateTime.Now;
                int daysSinceMonday = ((int)now.DayOfWeek + 6) % 7; // Monday=0
                var weekStart = now.Date.AddDays(-daysSinceMonday);

                // -- Aggregate hours per user --
                var userHours = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var ctrl in onlineUserControls)
                    userHours[ctrl.UserInfo.Name] = 0;

                // -- Track "Working" log entries to calculate live elapsed time for other users --
                var userWorkingTimestamp = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

                foreach (var log in logsDict.Values)
                {
                    if (string.IsNullOrEmpty(log.timestamp) || string.IsNullOrEmpty(log.userName))
                        continue;

                    if (!DateTime.TryParse(log.timestamp, out var logDate))
                        continue;

                    if (logDate.Date >= weekStart && logDate.Date <= weekStart.AddDays(6))
                    {
                        if (userHours.ContainsKey(log.userName))
                        {
                            userHours[log.userName] += ParseWorkingTimeToHours(log.workingTime);
                        }
                    }

                    // -- Remember when other users started "Working" so we can calculate their live elapsed time --
                    if (log.status == "Working" && userHours.ContainsKey(log.userName))
                    {
                        userWorkingTimestamp[log.userName] = logDate;
                    }
                }

                // -- Update each OnlineUserControl --
                foreach (var ctrl in onlineUserControls)
                {
                    string name = ctrl.UserInfo.Name;
                    double worked = userHours.ContainsKey(name) ? userHours[name] : 0;
                    double holiday = PublicHolidays.GetHolidayHoursInWeek(ctrl.UserInfo.Country, weekStart);

                    // -- If timer is running for current user, add elapsed time to Firebase hours --
                    if (name.Equals(_currentUser.Name, StringComparison.OrdinalIgnoreCase)
                        && _workingTimer != null && _workingTimer.Enabled)
                    {
                        _baseWeeklyHoursAtStart = worked;
                        double liveHours = worked + _elapsedTime.TotalHours;
                        ctrl.SetWeeklyHours(liveHours, holiday);
                    }
                    // -- For OTHER users currently "Working", calculate their live elapsed time from timestamp --
                    else if (!name.Equals(_currentUser.Name, StringComparison.OrdinalIgnoreCase)
                        && userWorkingTimestamp.ContainsKey(name))
                    {
                        double liveElapsed = (DateTime.UtcNow - userWorkingTimestamp[name]).TotalHours;
                        if (liveElapsed < 0) liveElapsed = 0;
                        if (liveElapsed > 24) liveElapsed = 0; // Safety: ignore stale "Working" entries older than 24h
                        ctrl.SetWeeklyHours(worked + liveElapsed, holiday);
                    }
                    else
                    {
                        ctrl.SetWeeklyHours(worked, holiday);
                    }
                    ctrl.UpdateLocalTime();

                    // -- Motivational balloon messages (only for current user) --
                    if (name.Equals(_currentUser.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        double effectiveWorked = (_workingTimer != null && _workingTimer.Enabled)
                            ? worked + _elapsedTime.TotalHours : worked;
                        CheckAndSendMotivationalMessage(name, effectiveWorked, GetWeeklyWorkLimitHours());
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Parses working time to total hours as double.
        /// Supports formats: "HH:mm:ss", "H:mm:ss", "2h 30m", "1h", "45m", or plain seconds "1".
        /// </summary>
        private static double ParseWorkingTimeToHours(string workingTime)
        {
            if (string.IsNullOrWhiteSpace(workingTime))
                return 0;

            workingTime = workingTime.Trim();

            // -- Try HH:mm:ss or H:mm:ss format (e.g. "05:00:00", "00:01:00") --
            // Only use TimeSpan.TryParse when string contains ":" to avoid
            // plain numbers like "1" being parsed as 1 DAY (24 hours)
            if (workingTime.Contains(":") && TimeSpan.TryParse(workingTime, out var ts))
                return ts.TotalHours;

            // -- Try "2h 30m" / "1h" / "45m" format --
            int hours = 0, minutes = 0;
            var parts = workingTime.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.EndsWith("h") && int.TryParse(part.TrimEnd('h'), out var h))
                    hours = h;
                else if (part.EndsWith("m") && int.TryParse(part.TrimEnd('m'), out var m))
                    minutes = m;
            }
            if (hours > 0 || minutes > 0)
                return hours + (minutes / 60.0);

            // -- Try plain number as seconds (e.g. "1") --
            if (double.TryParse(workingTime, out var sec))
                return sec / 3600.0;

            return 0;
        }

        /// <summary>
        /// Shows motivational tray balloon messages at hour milestones.
        /// Rules:
        ///   - Each milestone shows max 2 times total (persisted across sessions via weekly reset).
        ///   - If user clicks the balloon, that milestone is dismissed permanently for the week.
        ///   - Milestones reset each Monday (new week = fresh start).
        /// </summary>
        private void CheckAndSendMotivationalMessage(string userName, double workedHours, double weeklyLimit)
        {
            if (!_trayInitialized || _trayIcon == null) return;

            double lastNotified = 0;
            _lastNotifiedHours.TryGetValue(userName, out lastNotified);

            string title = null;
            string message = null;
            string milestoneKey = null;

            // -- MILESTONE: 50% of weekly limit --
            double halfLimit = weeklyLimit * 0.5;
            if (workedHours >= halfLimit && lastNotified < halfLimit)
            {
                milestoneKey = $"{userName}_half";
                title = "Halfway There!";
                message = $"You've worked {workedHours:F1} hours this week. Keep up the great momentum!";
            }
            // -- MILESTONE: Reached weekly limit --
            else if (workedHours >= weeklyLimit && lastNotified < weeklyLimit)
            {
                milestoneKey = $"{userName}_limit";
                title = "Weekly Goal Reached!";
                message = $"Congratulations! You've reached your {weeklyLimit}h weekly target. Take a rest and enjoy your free time!";
            }
            // -- MILESTONE: 20% over limit (orange zone) --
            else if (workedHours >= weeklyLimit * 1.2 && lastNotified < weeklyLimit * 1.2)
            {
                milestoneKey = $"{userName}_over20";
                title = "Over Your Limit";
                message = $"You're at {workedHours:F1}h ? that's {((workedHours / weeklyLimit - 1) * 100):F0}% over your weekly target. Consider taking a break!";
            }
            // -- MILESTONE: 40% over limit (red zone / stress alert) --
            else if (workedHours >= weeklyLimit * 1.4 && lastNotified < weeklyLimit * 1.4)
            {
                milestoneKey = $"{userName}_over40";
                title = "Stress Alert!";
                message = $"You've been working {workedHours:F1}h this week. Your well-being matters ? please take some time to rest and recharge!";
            }

            if (title != null && message != null && milestoneKey != null)
            {
                // -- Check if user already clicked/dismissed this milestone --
                if (_milestoneDismissed.Contains(milestoneKey))
                    return;

                // -- Check if already shown 2 times this week --
                _milestoneShowCount.TryGetValue(milestoneKey, out int showCount);
                if (showCount >= 2)
                    return;

                _lastNotifiedHours[userName] = workedHours;
                _milestoneShowCount[milestoneKey] = showCount + 1;
                _lastBalloonMilestoneKey = milestoneKey;

                // Persist show count
                SaveDismissedMilestones();

                try
                {
                    _trayIcon.BalloonTipTitle = title;
                    _trayIcon.BalloonTipText = message;
                    _trayIcon.BalloonTipIcon = workedHours >= weeklyLimit * 1.4 ? ToolTipIcon.Warning : ToolTipIcon.Info;
                    _trayIcon.ShowBalloonTip(8000);
                }
                catch { }

                if (workedHours >= weeklyLimit * 1.4
                    && !_sleepWarningShown
                    && string.Equals(userName, _currentUser?.Name, StringComparison.OrdinalIgnoreCase))
                {
                    _sleepWarningShown = true;
                    try
                    {
                        MessageBox.Show(
                            $"You worked {workedHours:F1}h this week. Please go to sleep and rest.",
                            "Health Warning",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                    catch { }

                    AppendAiChat("assistant", "You have worked many hours. Please rest now and get some sleep.");
                }
            }
        }

        // -- MILESTONE PERSISTENCE ? save/load dismissed milestones + show counts --

        private string GetMilestoneFilePath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WorkFlow");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "milestones.json");
        }

        /// <summary>
        /// Returns the Monday of the current week as "yyyy-MM-dd" for weekly reset key.
        /// </summary>
        private static string GetCurrentWeekKey()
        {
            var now = DateTime.Now;
            int daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
            return now.Date.AddDays(-daysSinceMonday).ToString("yyyy-MM-dd");
        }

        private void SaveDismissedMilestones()
        {
            try
            {
                var data = new
                {
                    weekKey = GetCurrentWeekKey(),
                    dismissed = _milestoneDismissed.ToArray(),
                    showCounts = _milestoneShowCount
                };
                File.WriteAllText(GetMilestoneFilePath(), JsonConvert.SerializeObject(data));
            }
            catch { }
        }

        private void LoadDismissedMilestones()
        {
            try
            {
                string path = GetMilestoneFilePath();
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeAnonymousType(json, new
                {
                    weekKey = "",
                    dismissed = new string[0],
                    showCounts = new Dictionary<string, int>()
                });

                if (data == null) return;

                // -- Reset if it's a new week --
                if (data.weekKey != GetCurrentWeekKey())
                {
                    // New week ? clear old milestones
                    File.Delete(path);
                    return;
                }

                if (data.dismissed != null)
                    _milestoneDismissed = new HashSet<string>(data.dismissed, StringComparer.OrdinalIgnoreCase);
                if (data.showCounts != null)
                    _milestoneShowCount = new Dictionary<string, int>(data.showCounts, StringComparer.OrdinalIgnoreCase);
            }
            catch { }
        }
    }

    // ================================================================
    // DATA MODELS ? kept here ONLY if no separate .cs files exist
    // --- DATA MODELS ? LOG ENTRY, LOG ENTRY WITH INDEX, RECURRING TASK ---
    // If you have LogEntry.cs / LogEntryWithIndex.cs, DELETE these.
    // ================================================================
    public class LogEntry
    {
        public string description { get; set; }
        public string startTime { get; set; }
        public string workingTime { get; set; }
        public string timestamp { get; set; }
        public string status { get; set; }
        public string userId { get; set; }
        public string userName { get; set; }
        public string project { get; set; }
        public string platform { get; set; }
    }

    public class LogEntryWithIndex : LogEntry
    {
        public int Nr { get; set; }
        public string Key { get; set; }
    }
}
