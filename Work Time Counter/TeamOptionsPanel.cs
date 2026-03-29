// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        TeamOptionsPanel.cs                                          ║
// ║  PURPOSE:     ADMIN SETTINGS PANEL — TEAM, MEMBERS, WIKI, JIRA, STAGES    ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Full admin settings dialog opened from the toolbar gear button.           ║
// ║  Organized into cards (sections) for clean navigation.                     ║
// ║                                                                            ║
// ║  CARDS (SECTIONS):                                                         ║
// ║    CARD 1: Team Info — Team name, join code, copy/regenerate               ║
// ║    CARD 2: Database — Custom Firebase URL configuration                    ║
// ║    CARD 3: Members — Invite, Remove, Edit, Reset PW, Mute, Kick, AsstAdmin║
// ║    CARD 4: Project Wiki — Links, datasheets, project documentation         ║
// ║    CARD 5: Jira Integration — Import from Jira projects                    ║
// ║    CARD 6: Project Stages — Track project progress milestones              ║
// ║    CARD 7: Firebase Traffic Meter — Monitor bandwidth vs free tier         ║
// ║    CARD 9: Join Another Team — Enter invite code (all users)               ║
// ║    CARD 10: Create a New Team — Start fresh team as admin (all users)      ║
// ║    CARD 11: Custom Theme — Color palette, fonts, presets (all users)       ║
// ║                                                                            ║
// ║  ADMIN FEATURES:                                                           ║
// ║  - MUTE USER: Prevents user from posting in chat/stickers                  ║
// ║  - KICK USER: Removes user from team + deletes their Firebase data         ║
// ║  - ASSISTANT ADMIN: Promotes user to have full admin powers                ║
// ║  - JIRA IMPORT: Connect and import issues from Jira Cloud                  ║
// ║  - PROJECT STAGES: Define and track project milestones                     ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    /// <summary>
    /// Admin Options panel — opened from the toolbar ⚙ button.
    /// Features:
    ///   • Firebase database URL (add/change custom URL)
    ///   • Team info (name, join code — regenerate code)
    ///   • Member management (add, remove, set color/title/role)
    ///   • Reset member password
    ///   • Export team data
    /// Only admin can edit; participants see read-only info.
    /// </summary>

    // ═══ CLASS: TEAMOPTIONSPANEL — ADMIN SETTINGS DIALOG ═══
    /// <summary>
    /// TeamOptionsPanel — Admin settings dialog for team management.
    /// Features team info, members, wiki, settings, and administrative functions.
    /// </summary>
    public class TeamOptionsPanel : Form
    {
        private TeamInfo _team;
        private List<UserInfo> _allUsers;
        private bool _isAdmin;
        private bool _isDarkMode;
        private bool _dataChanged = false;
        private string _currentUserName;
        public bool NeedsRestart { get; private set; } = false; // Set true when team switch/join requires restart

        // ═══ CHAT FONT SIZE CHANGED EVENT ═══
        // Fired when user clicks Small/Medium/Big in settings.
        // Form1 subscribes to this to call _chatPanel.SetChatFontSize().
        public event Action<string> ChatFontSizeChanged;

        // ═══ DARK MODE / SOUND / CUSTOM THEME TOGGLE EVENTS ═══
        public event Action<bool> DarkModeChanged;
        public event Action<bool> SoundToggled;
        public event Action<CustomTheme> CustomThemeChanged;
        public event Action ResetSplittersRequested;

        // ═══ PANEL VISIBILITY CHANGED EVENT ═══
        // Fired when user toggles panel visibility in settings.
        // Form1 subscribes to show/hide panels. Args: panelName, visible
        public event Action<string, bool> PanelVisibilityChanged;
        private Dictionary<string, bool> _panelVisibility;

        // ═══ THEME COLORS — DARK/LIGHT MODE PALETTE ═══
        // Each color is chosen for readability and visual hierarchy:
        // bgColor = main form background, panelBg = card background,
        // fgColor = text foreground, dimColor = subdued text,
        // accentColor = primary UI highlights (buttons, icons),
        // fieldBg = input fields, dangerColor = destructive actions,
        // successColor = positive feedback messages
        private readonly Color bgColor;
        private readonly Color panelBg;
        private readonly Color fgColor;
        private readonly Color dimColor;
        private readonly Color accentColor = Color.FromArgb(255, 127, 80);
        private readonly Color fieldBg;
        private readonly Color dangerColor = Color.FromArgb(220, 53, 69);
        private readonly Color successColor = Color.FromArgb(34, 197, 94);

        // ═══ PREDEFINED OPTIONS — MEMBER COLORS AND ROLES ═══
        // MemberColors: 15 vibrant palette colors for user color assignment
        // Roles: Job titles/positions selectable when editing member profiles
        private static readonly string[] MemberColors = new string[]
        {
            "#3498DB", "#2ECC71", "#E74C3C", "#9B59B6", "#F1C40F",
            "#1ABC9C", "#E67E22", "#E91E63", "#00BCD4", "#8BC34A",
            "#FF5722", "#607D8B", "#795548", "#FF9800", "#673AB7"
        };

        // Predefined roles for team member job titles
        private static readonly string[] Roles = new string[]
        {
            "Developer", "Frontend Dev", "Backend Dev", "Full Stack Dev",
            "Designer", "QA Tester", "DevOps", "Manager", "Team Lead",
            "Product Owner", "Scrum Master", "Intern", "Freelancer", "Other"
        };

        // ═══ UI CONTROLS — FORM ELEMENTS ═══
        // These controls are used throughout the dialog and referenced by
        // handler methods for data binding and updates:
        // txtFirebaseUrl = custom Firebase database URL input
        // txtTeamName = team display name
        // lblJoinCode = generated code for team joining
        // listMembers = ListView displaying all team members with columns:
        //   [0] Name, [1] Role, [2] Title, [3] Color, [4] Admin, [5] Muted, [6] Asst⭐, [7] Status
        // lblStatus = temporary status message label (success/error feedback)
        private TextBox txtFirebaseUrl;
        private TextBox txtTeamAiEndpoint;
        private TextBox txtTeamAiModel;
        private TextBox txtTeamAiApiKey;
        private TextBox txtTeamName;
        private Label lblJoinCode;
        private ListView listMembers;
        private Label lblStatus;

        // Wiki controls
        private ListView listWiki;
        private List<HelperEntry> _wikiEntries = new List<HelperEntry>();
        private List<string> _wikiCategories = new List<string>();
        private string _firebaseBaseUrl;
        private static readonly HttpClient _http = new HttpClient();

        public bool DataChanged => _dataChanged;

        // ═══ CONSTRUCTOR — LOADS TEAM DATA AND BUILDS THE UI ═══
        // Initializes the TeamOptionsPanel with team info, user list, and admin status.
        // Sets theme colors based on isDarkMode, loads Firebase base URL,
        // and triggers BuildUI to render all card sections.
        // The wiki entries are loaded asynchronously after UI is built (admin only).
        /// <summary>Initializes TeamOptionsPanel with team data and builds the settings UI.</summary>
        public TeamOptionsPanel(TeamInfo team, List<UserInfo> allUsers, bool isAdmin, bool isDarkMode, string currentUserName = null, Dictionary<string, bool> panelVisibility = null)
        {
//             DebugLogger.Log("[TeamOptions] Initializing TeamOptionsPanel");
            _team = team ?? new TeamInfo();
            _allUsers = allUsers ?? new List<UserInfo>();
            _isAdmin = isAdmin;
            _isDarkMode = isDarkMode;
            _currentUserName = currentUserName ?? UserStorage.GetLastUser() ?? "";
            _panelVisibility = panelVisibility ?? new Dictionary<string, bool>
            {
                { "Board", true }, { "Chat", true }, { "Team", true }, { "Files", true }, { "Calendar", true }, { "Weather", true }, { "Personal Board", false }, { "Ask AI", false }, { "AI Chat", false }
            };
//             DebugLogger.Log($"[TeamOptions] Team: {_team.TeamName}, Admin: {_isAdmin}, Users: {_allUsers.Count}");

            // Set theme colors based on dark/light mode preference
            // Dark mode: cool blues/grays; Light mode: warm neutrals
            if (isDarkMode)
            {
                bgColor = Color.FromArgb(24, 28, 36);
                panelBg = Color.FromArgb(30, 36, 46);
                fgColor = Color.FromArgb(220, 224, 230);
                dimColor = Color.FromArgb(120, 130, 145);
                fieldBg = Color.FromArgb(38, 44, 56);
            }
            else
            {
                bgColor = Color.FromArgb(245, 247, 250);
                panelBg = Color.White;
                fgColor = Color.FromArgb(30, 41, 59);
                dimColor = Color.FromArgb(100, 116, 139);
                fieldBg = Color.White;
            }

            // Build Firebase base URL for wiki CRUD (already includes /teams/{joinCode})
            _firebaseBaseUrl = UserStorage.GetFirebaseBaseUrl();

            this.Text = "  Team Settings";
            this.Width = 1160;
            this.Height = 860;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.BackColor = bgColor;
            this.ForeColor = fgColor;
            this.Font = new Font("Segoe UI", 9);

            BuildUI();
//             DebugLogger.Log("[TeamOptions] TeamOptionsPanel UI built successfully");

            // Load wiki entries from Firebase (async, admin only)
            if (_isAdmin)
            {
//                 DebugLogger.Log("[TeamOptions] Loading wiki entries (admin mode)");
                _ = LoadWikiEntriesAsync();
            }
        }

        // ═══ BUILD UI — CREATES ALL CARD SECTIONS IN A SCROLLABLE PANEL ═══
        // The main UI layout builder. Creates a scrollable panel and organizes 6 cards:
        // Card 1: Team Info (name, join code, regenerate)
        // Card 2: Database (custom Firebase URL)
        // Card 3: Members (list view + CRUD buttons)
        // Card 4: Project Wiki (admin only)
        // Card 5: Jira Integration (admin only, placeholder)
        // Card 6: Project Stages (admin only, placeholder)
        // Card positioning uses a vertical (y) coordinate that advances after each card.
        // Each card is built with MakeCard() helper, which adds borders and styling.
        /// <summary>Builds the main UI with team settings cards.</summary>
        private void BuildUI()
        {
//             DebugLogger.Log("[TeamOptions] Building UI with team settings cards");
            int colW = 530;    // width of each column's cards
            int formPad = 16;  // padding from form edge
            int colGap = 16;   // gap between left and right columns
            int col1X = 0;     // left column X
            int col2X = colW + colGap; // right column X

            // Create main scrollable panel — fills form and auto-scrolls vertically
            // All cards are added to this panel
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(formPad, formPad, formPad, formPad),
                BackColor = bgColor
            };
            this.Controls.Add(mainPanel);

            int cardW = colW;  // alias for card width (used by existing card code)
            int y = 6;         // LEFT COLUMN vertical position tracker
            int yR = 6;        // RIGHT COLUMN vertical position tracker

            // ══════════════════════════════════════════════════
            // CARD 1: TEAM INFO — NAME, JOIN CODE, REGENERATE
            // ══════════════════════════════════════════════════
            // Shows team name (editable by admin), displays join code with copy button,
            // and regenerate button (admin only) to issue a new code.
            // Join code is displayed in large monospace font, center-aligned,
            // with subtle border to make it visually distinct.
            int card1H = _isAdmin ? 170 : 110;
            var card1 = MakeCard(col1X, y, cardW, card1H);
            mainPanel.Controls.Add(card1);

            int cy = 12, cx = 16; // inner card padding (cy = card y, cx = card x)

            card1.Controls.Add(MakeSectionIcon("\U0001f465", cx, cy));
            card1.Controls.Add(MakeSectionTitle("Team Info", cx + 28, cy));
            cy += 30;

            card1.Controls.Add(MakeFieldLabel("TEAM NAME", cx, cy));
            txtTeamName = MakeTextBox(cx, cy + 16, 220);
            txtTeamName.Text = _team.TeamName ?? "";
            txtTeamName.ReadOnly = !_isAdmin;
            card1.Controls.Add(txtTeamName);

            // Join Code block — positioned on right side of card
            int codeX = 260;
            card1.Controls.Add(MakeFieldLabel("JOIN CODE", codeX, cy - 30 + 2));
            lblJoinCode = new Label
            {
                Text = _team.JoinCode ?? "------",
                Font = new Font("Consolas", 18, FontStyle.Bold),
                ForeColor = accentColor,
                BackColor = _isDarkMode ? Color.FromArgb(34, 40, 52) : Color.FromArgb(252, 252, 255),
                Location = new Point(codeX, cy - 30 + 20),
                Size = new Size(140, 34),
                TextAlign = ContentAlignment.MiddleCenter,
                BorderStyle = BorderStyle.None
            };
            // Paint rounded join code box with subtle border
            lblJoinCode.Paint += (s, ev) =>
            {
                using (var pen = new Pen(_isDarkMode ? Color.FromArgb(60, 68, 82) : Color.FromArgb(200, 205, 215), 1))
                    ev.Graphics.DrawRectangle(pen, 0, 0, lblJoinCode.Width - 1, lblJoinCode.Height - 1);
            };
            card1.Controls.Add(lblJoinCode);

            var btnCopy = MakePillButton("\U0001f4cb Copy", codeX + 148, cy - 30 + 22);
            btnCopy.Click += (s, e) => { Clipboard.SetText(lblJoinCode.Text); ShowStatus("\u2713 Copied!"); };
            card1.Controls.Add(btnCopy);

            // Regenerate button (admin only) — generates new join code,
            // warns user that old code stops working, updates label
            if (_isAdmin)
            {
                var btnNewCode = MakePillButton("\u21bb New", codeX + 148 + 68, cy - 30 + 22);
                btnNewCode.Click += async (s, e) =>
                {
                    if (MessageBox.Show(
                        "Generate a new join code?\n\n" +
                        "The old code will stop working.\n" +
                        "All data (logs, stickers, chat, etc.) will be migrated to the new code.\n\n" +
                        "This may take a moment.",
                        "Regenerate Code", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        string oldCode = _team.JoinCode;
                        string newCode = TeamInfo.GenerateJoinCode();

                        btnNewCode.Enabled = false;
                        ShowStatus("\U0001f504 Migrating data to new code...");

                        // Migrate all Firebase data from old path to new path
                        bool migrated = await UserStorage.MigrateFirebaseDataAsync(oldCode, newCode);

                        _team.JoinCode = newCode;
                        lblJoinCode.Text = newCode;
                        _dataChanged = true;
                        NeedsRestart = true;
                        btnNewCode.Enabled = true;

                        if (migrated)
                            ShowStatus($"\u2713 New code: {newCode} — all data migrated! Restart required.");
                        else
                            ShowStatus($"\u26a0 New code: {newCode} — some data may not have migrated.", true);
                    }
                };
                card1.Controls.Add(btnNewCode);

                // ── IMPORT / RECOVER DATA FROM ANOTHER CODE ──
                int recoverY = cy + 54;  // Below team name textbox with spacing
                card1.Controls.Add(new Label
                {
                    Text = "IMPORT FROM CODE:",
                    Font = new Font("Segoe UI", 7, FontStyle.Bold),
                    ForeColor = dimColor,
                    Location = new Point(cx, recoverY + 4),
                    AutoSize = true
                });
                var txtOldCode = MakeTextBox(cx + 120, recoverY, 80);
                txtOldCode.Font = new Font("Consolas", 10, FontStyle.Bold);
                txtOldCode.MaxLength = 6;
                txtOldCode.CharacterCasing = CharacterCasing.Upper;
                card1.Controls.Add(txtOldCode);

                var btnRecover = MakePillButton("\U0001f4e5 Import", cx + 210, recoverY);
                btnRecover.Click += async (s, e) =>
                {
                    string oldCode = txtOldCode.Text.Trim().ToUpper();
                    if (oldCode.Length != 6)
                    { ShowStatus("\u274c Enter a 6-character code", true); return; }
                    if (oldCode == _team.JoinCode)
                    { ShowStatus("\u274c That is the current code", true); return; }

                    btnRecover.Enabled = false;
                    ShowStatus("\U0001f504 Importing data...");

                    bool ok = await UserStorage.MigrateFirebaseDataAsync(oldCode, _team.JoinCode);

                    btnRecover.Enabled = true;
                    if (ok)
                    {
                        _dataChanged = true;
                        NeedsRestart = true;
                        ShowStatus("\u2705 Data imported! Restart the app to see it.");
                        txtOldCode.Clear();
                    }
                    else
                        ShowStatus("\u274c Import failed. Check the code and try again.", true);
                };
                card1.Controls.Add(btnRecover);
            }

            y += card1H + 10;  // Advance y position for next card

            // ══════════════════════════════════════════════════
            // CARD 2: DATABASE — CUSTOM FIREBASE URL
            // ══════════════════════════════════════════════════
            // Allows admin to set a custom Firebase database URL for this team.
            // If empty, uses the default shared database.
            // Includes placeholder text and validation on save (must start with https://).
            int card2H = _isAdmin ? 248 : 80;
            var card2 = MakeCard(col1X, y, cardW, card2H);
            mainPanel.Controls.Add(card2);

            cy = 12; cx = 16;
            card2.Controls.Add(MakeSectionIcon("\U0001f5c4", cx, cy));
            card2.Controls.Add(MakeSectionTitle("Database", cx + 28, cy));
            cy += 30;

            card2.Controls.Add(MakeFieldLabel("FIREBASE URL", cx, cy));
            txtFirebaseUrl = MakeTextBox(cx, cy + 16, cardW - 36);
            txtFirebaseUrl.Text = _team.CustomFirebaseUrl ?? "";
            txtFirebaseUrl.ReadOnly = !_isAdmin;
            if (string.IsNullOrEmpty(txtFirebaseUrl.Text))
            {
                txtFirebaseUrl.Text = "https://your-project.firebasedatabase.app";
                txtFirebaseUrl.ForeColor = dimColor;
            }
            // Show placeholder text on focus/blur cycle
            txtFirebaseUrl.GotFocus += (s, ev) =>
            {
                if (txtFirebaseUrl.Text == "https://your-project.firebasedatabase.app")
                { txtFirebaseUrl.Text = ""; txtFirebaseUrl.ForeColor = fgColor; }
            };
            txtFirebaseUrl.LostFocus += (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtFirebaseUrl.Text))
                { txtFirebaseUrl.Text = "https://your-project.firebasedatabase.app"; txtFirebaseUrl.ForeColor = dimColor; }
            };
            card2.Controls.Add(txtFirebaseUrl);

            if (_isAdmin)
            {
                var lblHint = new Label
                {
                    Text = "Leave empty to use the default shared database.",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                    Location = new Point(cx, cy + 42),
                    AutoSize = true
                };
                card2.Controls.Add(lblHint);

                cy += 62;
                card2.Controls.Add(MakeFieldLabel("TEAM AI ENDPOINT (OPENROUTER)", cx, cy));
                txtTeamAiEndpoint = MakeTextBox(cx, cy + 16, cardW - 36);
                txtTeamAiEndpoint.Text = string.IsNullOrWhiteSpace(_team.TeamAiEndpoint)
                    ? "https://openrouter.ai/api/v1/chat/completions"
                    : _team.TeamAiEndpoint.Trim();
                txtTeamAiEndpoint.ReadOnly = !_isAdmin;
                card2.Controls.Add(txtTeamAiEndpoint);

                cy += 46;
                card2.Controls.Add(MakeFieldLabel("TEAM AI MODEL", cx, cy));
                txtTeamAiModel = MakeTextBox(cx, cy + 16, 230);
                txtTeamAiModel.Text = string.IsNullOrWhiteSpace(_team.TeamAiModel)
                    ? "openai/gpt-4o-mini"
                    : _team.TeamAiModel.Trim();
                txtTeamAiModel.ReadOnly = !_isAdmin;
                card2.Controls.Add(txtTeamAiModel);

                txtTeamAiApiKey = MakeTextBox(cx + 240, cy + 16, cardW - 36 - 240);
                txtTeamAiApiKey.Text = _team.TeamAiApiKey ?? "";
                txtTeamAiApiKey.UseSystemPasswordChar = true;
                txtTeamAiApiKey.ReadOnly = !_isAdmin;
                card2.Controls.Add(txtTeamAiApiKey);

                var lblAiHint = new Label
                {
                    Text = "If key is empty, users can use their own personal API key.",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                    Location = new Point(cx, cy + 42),
                    AutoSize = true
                };
                card2.Controls.Add(lblAiHint);
            }

            y += card2H + 10;

            // ══════════════════════════════════════════════════
            // CARD 3: MEMBERS — MANAGEMENT & MODERATION
            // ══════════════════════════════════════════════════
            // Core admin feature. Displays all team members in a ListView with columns:
            // [0] Name, [1] Role, [2] Title, [3] Color, [4] Admin (★), [5] Muted (Yes),
            // [6] Asst⭐ (Assistant Admin star), [7] Status (Default PW / Custom PW)
            // Admin can: Add, Remove, Edit (color/title/role), Reset Password,
            // Mute (prevent chat), Kick (remove + delete Firebase data),
            // Promote to Assistant Admin.
            // Member colors are displayed in the list as text ForeColor.
            int memberBtnH = _isAdmin ? 80 : 0;  // Increased to fit two rows of buttons
            int card3H = 30 + 200 + memberBtnH + 16;
            var card3 = MakeCard(col1X, y, cardW, card3H);
            mainPanel.Controls.Add(card3);

            cy = 12; cx = 16;
            card3.Controls.Add(MakeSectionIcon("\U0001f4cb", cx, cy));
            card3.Controls.Add(MakeSectionTitle("Members", cx + 28, cy));

            // Member count badge — shows total team members
            var lblCount = new Label
            {
                Text = _allUsers.Count.ToString(),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = _isDarkMode ? Color.FromArgb(70, 80, 100) : Color.FromArgb(160, 170, 190),
                Size = new Size(24, 18),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(cx + 28 + 72, cy + 2)
            };
            card3.Controls.Add(lblCount);
            cy += 30;

            // ListView for members — column mapping for PopulateMemberList():
            // Column 0: Name (100px) — member username
            // Column 1: Role (85px) — job role from Roles array
            // Column 2: Title (100px) — custom job title
            // Column 3: Color (50px) — hex color code
            // Column 4: Admin (45px) — shows ★ if team admin
            // Column 5: Muted (50px) — shows "Yes" if muted
            // Column 6: Asst⭐ (55px) — shows ★ if assistant admin
            // Column 7: Status (85px) — "Default PW" or "Custom PW"
            listMembers = new ListView
            {
                Location = new Point(cx, cy),
                Size = new Size(cardW - 36, 200),
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = _isDarkMode ? Color.FromArgb(28, 33, 43) : Color.FromArgb(250, 251, 253),
                ForeColor = fgColor,
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.None,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            listMembers.Columns.Add("Name", 100);
            listMembers.Columns.Add("Role", 85);
            listMembers.Columns.Add("Title", 100);
            listMembers.Columns.Add("Color", 50);
            listMembers.Columns.Add("Admin", 45);
            listMembers.Columns.Add("Muted", 50);
            listMembers.Columns.Add("Asst ⭐", 55);
            listMembers.Columns.Add("Status", 85);
            // Subtle bottom border line for the list
            listMembers.Paint += (s, ev) =>
            {
                using (var pen = new Pen(_isDarkMode ? Color.FromArgb(48, 55, 68) : Color.FromArgb(225, 228, 235)))
                    ev.Graphics.DrawRectangle(pen, 0, 0, listMembers.Width - 1, listMembers.Height - 1);
            };
            PopulateMemberList();
            card3.Controls.Add(listMembers);
            cy += 206;

            if (_isAdmin)
            {
                // Button grid — two rows of action buttons for member management
                // Row 1: Invite, Remove, Edit, Reset PW
                // Row 2: Mute, Kick, Asst Admin
                int btnGap = 6;
                int bx = cx;

                // Row 1: Invite, Remove, Edit, Reset PW
                var btnInvite = MakeActionButton("\u2709 Invite", accentColor, Color.White, bx, cy, 85);
                btnInvite.Click += OnInviteMember;
                card3.Controls.Add(btnInvite);
                bx += 85 + btnGap;

                var btnRemove = MakeActionButton("\u2796 Remove", dangerColor, Color.White, bx, cy, 85);
                btnRemove.Click += OnRemoveMember;
                card3.Controls.Add(btnRemove);
                bx += 85 + btnGap;

                var btnEdit = MakeActionButton("\u270f Edit", Color.FromArgb(59, 130, 246), Color.White, bx, cy, 75);
                btnEdit.Click += OnEditMember;
                card3.Controls.Add(btnEdit);
                bx += 75 + btnGap;

                var btnResetPw = MakeActionButton("\U0001f511 Reset PW", Color.FromArgb(124, 58, 237), Color.White, bx, cy, 95);
                btnResetPw.Click += OnResetPassword;
                card3.Controls.Add(btnResetPw);

                // Row 2: Mute, Kick, Asst Admin — destructive/moderation actions
                bx = cx;
                cy += 40;

                var btnMute = MakeActionButton("\U0001f507 Mute", Color.FromArgb(168, 85, 247), Color.White, bx, cy, 80);
                btnMute.Click += OnMuteUser;
                card3.Controls.Add(btnMute);
                bx += 80 + btnGap;

                var btnKick = MakeActionButton("\U0001f9b6 Kick", dangerColor, Color.White, bx, cy, 75);
                btnKick.Click += OnKickUser;
                card3.Controls.Add(btnKick);
                bx += 75 + btnGap;

                var btnUnban = MakeActionButton("\u2705 Unban", Color.FromArgb(34, 197, 94), Color.White, bx, cy, 75);
                btnUnban.Click += OnUnbanUser;
                card3.Controls.Add(btnUnban);
                bx += 75 + btnGap;

                var btnAsstAdmin = MakeActionButton("\u2b50 Asst Admin", Color.FromArgb(251, 146, 60), Color.White, bx, cy, 100);
                btnAsstAdmin.Click += OnToggleAssistantAdmin;
                card3.Controls.Add(btnAsstAdmin);
            }

            y += card3H + 10;

            // ══════════════════════════════════════════════════
            // CARD 4: PROJECT WIKI — LINKS, DATASHEETS, DOCS
            // ══════════════════════════════════════════════════
            // Admin-only card. Displays project documentation, useful links,
            // datasheets, sticky notes, and other reference materials.
            // Wiki entries are loaded from Firebase /helper endpoint.
            // Admins can Add, Edit, Delete wiki entries.
            // Categories can be customized (default: Useful Links, Sticky Notes, etc).
            // Each entry shows: Title, Category, URL/Description, Author, Date Added.
            if (_isAdmin)
            {
                int card4H = 30 + 150 + 42 + 20;
                var card4 = MakeCard(col2X, yR, cardW, card4H);
                mainPanel.Controls.Add(card4);

                cy = 12; cx = 16;
                card4.Controls.Add(MakeSectionIcon("\U0001f4d6", cx, cy));
                card4.Controls.Add(MakeSectionTitle("Project Wiki", cx + 28, cy));

                var lblWikiSub = new Label
                {
                    Text = "Links, datasheets, project plans — visible to all team members",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                    Location = new Point(cx + 28 + 106, cy + 4),
                    AutoSize = true
                };
                card4.Controls.Add(lblWikiSub);
                cy += 30;

                listWiki = new ListView
                {
                    Location = new Point(cx, cy),
                    Size = new Size(cardW - 36, 150),
                    View = View.Details,
                    FullRowSelect = true,
                    GridLines = false,
                    BackColor = _isDarkMode ? Color.FromArgb(28, 33, 43) : Color.FromArgb(250, 251, 253),
                    ForeColor = fgColor,
                    Font = new Font("Segoe UI", 9),
                    BorderStyle = BorderStyle.None,
                    HeaderStyle = ColumnHeaderStyle.Nonclickable
                };
                listWiki.Columns.Add("Title", 155);
                listWiki.Columns.Add("Category", 95);
                listWiki.Columns.Add("URL / Info", 210);
                listWiki.Columns.Add("Author", 75);
                listWiki.Columns.Add("Date", 60);
                listWiki.Paint += (s, ev) =>
                {
                    using (var pen = new Pen(_isDarkMode ? Color.FromArgb(48, 55, 68) : Color.FromArgb(225, 228, 235)))
                        ev.Graphics.DrawRectangle(pen, 0, 0, listWiki.Width - 1, listWiki.Height - 1);
                };
                card4.Controls.Add(listWiki);
                cy += 156;

                int bx2 = cx;
                int btnGap2 = 6;

                var btnWikiAdd = MakeActionButton("\u2795 Add", Color.FromArgb(100, 200, 255), Color.FromArgb(15, 15, 25), bx2, cy, 80);
                btnWikiAdd.Click += OnAddWikiEntry;
                card4.Controls.Add(btnWikiAdd);
                bx2 += 80 + btnGap2;

                var btnWikiEdit = MakeActionButton("\u270f Edit", Color.FromArgb(59, 130, 246), Color.White, bx2, cy, 80);
                btnWikiEdit.Click += OnEditWikiEntry;
                card4.Controls.Add(btnWikiEdit);
                bx2 += 80 + btnGap2;

                var btnWikiDel = MakeActionButton("\U0001f5d1 Delete", dangerColor, Color.White, bx2, cy, 90);
                btnWikiDel.Click += OnDeleteWikiEntry;
                card4.Controls.Add(btnWikiDel);
                bx2 += 90 + btnGap2;

                var btnWikiRefresh = MakeActionButton("\u21bb Refresh", _isDarkMode ? Color.FromArgb(50, 58, 72) : Color.FromArgb(210, 215, 225),
                    _isDarkMode ? Color.FromArgb(170, 180, 195) : Color.FromArgb(60, 70, 85), bx2, cy, 90);
                btnWikiRefresh.Click += async (s, ev) => await LoadWikiEntriesAsync();
                card4.Controls.Add(btnWikiRefresh);


                yR += card4H + 10;
            }
            // ══════════════════════════════════════════════════
            // CARD 5: INTEGRATIONS — CONNECT EXTERNAL PLATFORMS
            // ══════════════════════════════════════════════════
            // Admin-only card. Opens a multi-platform integration manager.
            // Supports: Jira, ClickUp, Linear, GitHub, Trello, Asana, monday.com, Notion
            if (_isAdmin)
            {
                int card5H = 100;
                var card5 = MakeCard(col2X, yR, cardW, card5H);
                mainPanel.Controls.Add(card5);

                cy = 12; cx = 16;
                card5.Controls.Add(MakeSectionIcon("\U0001f517", cx, cy));
                card5.Controls.Add(MakeSectionTitle("Integrations", cx + 28, cy));

                var lblIntSub = new Label
                {
                    Text = "Connect external project management tools",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                    Location = new Point(cx + 28 + 100, cy + 4),
                    AutoSize = true
                };
                card5.Controls.Add(lblIntSub);
                cy += 30;

                // Platform icons row
                var lblPlatforms = new Label
                {
                    Text = "Jira \u2022 ClickUp \u2022 Linear \u2022 GitHub \u2022 Trello \u2022 Asana \u2022 monday \u2022 Notion",
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = _isDarkMode ? Color.FromArgb(140, 150, 170) : Color.FromArgb(100, 110, 130),
                    Location = new Point(cx, cy),
                    AutoSize = true
                };
                card5.Controls.Add(lblPlatforms);
                cy += 22;

                var btnOpenIntegrations = MakeActionButton("\U0001f517 Open Integrations", Color.FromArgb(59, 130, 246), Color.White, cx, cy, 160);
                btnOpenIntegrations.Click += (s, e) =>
                {
                    using (var form = new IntegrationsForm(_isDarkMode, _team.TeamName ?? "Team"))
                    {
                        form.ShowDialog(this);
                    }
                };
                card5.Controls.Add(btnOpenIntegrations);

                yR += card5H + 10;
            }

            // ══════════════════════════════════════════════════
            // CARD 6: PROJECT STAGES — MILESTONE TRACKING
            // ══════════════════════════════════════════════════
            // Admin can define project stages (Planning, Development, Testing, etc.)
            // Stages are stored in Firebase at: {baseUrl}/project_stages/{key}.json
            // Each stage has: name, description, status, color, order
            if (_isAdmin)
            {
                int card6H = 30 + 150 + 42 + 20;
                var card6 = MakeCard(col2X, yR, cardW, card6H);
                mainPanel.Controls.Add(card6);

                cy = 12; cx = 16;
                card6.Controls.Add(MakeSectionIcon("\U0001f4ca", cx, cy));
                card6.Controls.Add(MakeSectionTitle("Project Stages", cx + 28, cy));

                var lblStagesSub = new Label
                {
                    Text = "Track project progress through defined stages",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                    Location = new Point(cx + 28 + 126, cy + 4),
                    AutoSize = true
                };
                card6.Controls.Add(lblStagesSub);
                cy += 30;

                var listStages = new ListView
                {
                    Location = new Point(cx, cy),
                    Size = new Size(cardW - 36, 150),
                    View = View.Details,
                    FullRowSelect = true,
                    GridLines = false,
                    BackColor = _isDarkMode ? Color.FromArgb(28, 33, 43) : Color.FromArgb(250, 251, 253),
                    ForeColor = fgColor,
                    Font = new Font("Segoe UI", 9),
                    BorderStyle = BorderStyle.None,
                    HeaderStyle = ColumnHeaderStyle.Nonclickable
                };
                listStages.Columns.Add("Stage Name", 150);
                listStages.Columns.Add("Status", 120);
                listStages.Columns.Add("Description", 200);
                listStages.Columns.Add("Color", 60);
                listStages.Paint += (s, ev) =>
                {
                    using (var pen = new Pen(_isDarkMode ? Color.FromArgb(48, 55, 68) : Color.FromArgb(225, 228, 235)))
                        ev.Graphics.DrawRectangle(pen, 0, 0, listStages.Width - 1, listStages.Height - 1);
                };
                card6.Controls.Add(listStages);
                cy += 156;

                // Load existing stages from Firebase
                _ = LoadProjectStagesAsync(listStages);

                int bx6 = cx;
                var btnStageAdd = MakeActionButton("\u2795 Add", Color.FromArgb(100, 200, 255), Color.FromArgb(15, 15, 25), bx6, cy, 80);
                btnStageAdd.Click += async (s, e) =>
                {
                    await ShowStageDialog(listStages, null, null); // null = new stage
                };
                card6.Controls.Add(btnStageAdd);
                bx6 += 80 + 6;

                var btnStageEdit = MakeActionButton("\u270f Edit", Color.FromArgb(59, 130, 246), Color.White, bx6, cy, 80);
                btnStageEdit.Click += async (s, e) =>
                {
                    if (listStages.SelectedItems.Count == 0)
                    {
                        ShowStatus("Select a stage to edit.", false);
                        return;
                    }
                    var item = listStages.SelectedItems[0];
                    string key = item.Tag?.ToString();
                    if (string.IsNullOrEmpty(key)) return;

                    // Build stage from ListView item
                    var stage = new ProjectStage
                    {
                        name = item.SubItems[0].Text,
                        status = item.SubItems[1].Text,
                        description = item.SubItems[2].Text,
                        color = item.SubItems[3].Text,
                        Key = key
                    };
                    await ShowStageDialog(listStages, stage, key);
                };
                card6.Controls.Add(btnStageEdit);
                bx6 += 80 + 6;

                var btnStageDel = MakeActionButton("\U0001f5d1 Delete", dangerColor, Color.White, bx6, cy, 90);
                btnStageDel.Click += async (s, e) =>
                {
                    if (listStages.SelectedItems.Count == 0)
                    {
                        ShowStatus("Select a stage to delete.", false);
                        return;
                    }
                    var item = listStages.SelectedItems[0];
                    string key = item.Tag?.ToString();
                    string stageName = item.SubItems[0].Text;

                    var confirm = MessageBox.Show(
                        $"Delete stage \"{stageName}\"?",
                        "Delete Stage", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (confirm == DialogResult.Yes)
                    {
                        try
                        {
                            string url = _firebaseBaseUrl + "/project_stages/" + key + ".json";
                            var resp = await _http.DeleteAsync(url);
                            if (resp.IsSuccessStatusCode)
                            {
                                ShowStatus($"\u2705 Stage \"{stageName}\" deleted.", true);
                                await LoadProjectStagesAsync(listStages);
                            }
                            else
                            {
                                ShowStatus("Failed to delete stage.", false);
                            }
                        }
                        catch (Exception ex)
                        {
                            ShowStatus($"Error: {ex.Message}", false);
                        }
                    }
                };
                card6.Controls.Add(btnStageDel);

                yR += card6H + 10;
            }

            // ══════════════════════════════════════════════════
            // CARD 7: FIREBASE TRAFFIC METER — SHOWS BANDWIDTH USED THIS MONTH
            // ══════════════════════════════════════════════════
            {
                int card7H = 190;
                var card7 = MakeCard(col2X, yR, cardW, card7H);
                mainPanel.Controls.Add(card7);
                int c7y = 12;

                card7.Controls.Add(MakeSectionIcon("\U0001f4e1", cx, c7y));
                card7.Controls.Add(MakeSectionTitle("Firebase Traffic Meter", cx + 28, c7y));
                c7y += 30;

                card7.Controls.Add(new Label
                {
                    Text = "Monitor your Firebase bandwidth usage to stay within free tier limits",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                    Location = new Point(cx + 28, c7y - 6),
                    AutoSize = true
                });
                c7y += 18;

                // MONTH LABEL
                card7.Controls.Add(new Label
                {
                    Text = "MONTH: " + FirebaseTrafficTracker.MonthKey,
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = _isDarkMode ? Color.FromArgb(180, 190, 200) : Color.FromArgb(60, 70, 80),
                    Location = new Point(cx, c7y),
                    AutoSize = true
                });
                c7y += 20;

                // TRAFFIC BAR — VISUAL PROGRESS VS FREE TIER (360 MB/month for Spark plan)
                long totalBytes = FirebaseTrafficTracker.TotalBytes;
                long freeLimit = 360L * 1024 * 1024; // 360 MB FIREBASE SPARK FREE TIER
                int pct = (int)Math.Min(100, (totalBytes * 100) / Math.Max(freeLimit, 1));

                var barBg = new Panel
                {
                    Location = new Point(cx, c7y),
                    Size = new Size(cardW - 30, 18),
                    BackColor = _isDarkMode ? Color.FromArgb(40, 46, 56) : Color.FromArgb(220, 225, 235)
                };
                card7.Controls.Add(barBg);

                Color barColor = pct < 60 ? Color.FromArgb(34, 197, 94)   // GREEN
                               : pct < 85 ? Color.FromArgb(234, 179, 8)    // YELLOW
                               : Color.FromArgb(239, 68, 68);              // RED

                var barFill = new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(Math.Max(2, (int)((barBg.Width * pct) / 100.0)), 18),
                    BackColor = barColor
                };
                barBg.Controls.Add(barFill);

                var lblPct = new Label
                {
                    Text = pct + "%",
                    Font = new Font("Segoe UI", 7, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(4, 1),
                    AutoSize = true
                };
                barFill.Controls.Add(lblPct);
                c7y += 24;

                // STATS ROW
                string statsText =
                    $"\u2b06 Sent: {FirebaseTrafficTracker.FormattedSent}    " +
                    $"\u2b07 Received: {FirebaseTrafficTracker.FormattedReceived}    " +
                    $"\U0001f4ca Total: {FirebaseTrafficTracker.FormattedTotal}";
                card7.Controls.Add(new Label
                {
                    Text = statsText,
                    Font = new Font("Segoe UI", 8),
                    ForeColor = _isDarkMode ? Color.FromArgb(160, 170, 180) : Color.FromArgb(80, 90, 100),
                    Location = new Point(cx, c7y),
                    AutoSize = true
                });
                c7y += 18;

                // REQUEST COUNT
                card7.Controls.Add(new Label
                {
                    Text = $"\U0001f310 Requests this month: {FirebaseTrafficTracker.RequestCount}    " +
                           $"\U0001f6e1 Free tier: 360 MB/month",
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = _isDarkMode ? Color.FromArgb(120, 130, 140) : Color.FromArgb(100, 110, 120),
                    Location = new Point(cx, c7y),
                    AutoSize = true
                });
                c7y += 22;

                // RESET BUTTON (ADMIN ONLY)
                if (_isAdmin)
                {
                    var btnReset = MakeActionButton("\U0001f504 Reset Counters", Color.FromArgb(100, 116, 139), Color.White, cx, c7y, 120);
                    btnReset.Click += (s, e) =>
                    {
                        var confirm = MessageBox.Show("Reset all traffic counters to zero?",
                            "Reset Counters", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (confirm == DialogResult.Yes)
                        {
                            FirebaseTrafficTracker.ResetCounters();
                            ShowStatus("\u2705 Traffic counters reset!", false);
                        }
                    };
                    card7.Controls.Add(btnReset);
                }

                yR += card7H + 10;
            }

            // ══════════════════════════════════════════════════
            // CARD 7B: BACKUP & GITHUB — ADMIN-ONLY CLOUD BACKUP CONFIG
            // Mirror backup status shown to all, GitHub config admin-only.
            // ══════════════════════════════════════════════════
            {
                int card7bH = _isAdmin ? 380 : 130;
                var card7b = MakeCard(col2X, yR, cardW, card7bH);
                mainPanel.Controls.Add(card7b);
                int cby = 12;

                card7b.Controls.Add(MakeSectionIcon("\U0001f4be", cx, cby));
                card7b.Controls.Add(MakeSectionTitle("Backup & Data Protection", cx + 28, cby));
                cby += 30;

                // STATUS LINE — shows last backup times
                string backupStatus = BackupManager.GetBackupStatus();
                var lblStatus = new Label
                {
                    Text = backupStatus,
                    Font = new Font("Segoe UI", 8),
                    ForeColor = _isDarkMode ? Color.FromArgb(120, 200, 120) : Color.FromArgb(30, 120, 30),
                    Location = new Point(cx, cby),
                    AutoSize = true
                };
                card7b.Controls.Add(lblStatus);
                cby += 20;

                // MIRROR PATH
                card7b.Controls.Add(new Label
                {
                    Text = "Mirror: " + BackupManager.GetMirrorPath(),
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = dimColor,
                    Location = new Point(cx, cby),
                    Size = new Size(cardW - 40, 16)
                });
                cby += 20;

                // MANUAL BACKUP BUTTON (all users)
                var btnBackupNow = MakeActionButton("\U0001f4be Backup Now", Color.FromArgb(34, 120, 190), Color.White, cx, cby, 120);
                btnBackupNow.Click += (s, e) =>
                {
                    BackupManager.RunMirrorBackup();
                    lblStatus.Text = BackupManager.GetBackupStatus();
                    ShowStatus("\u2705 Mirror backup completed!", false);
                };
                card7b.Controls.Add(btnBackupNow);
                cby += 38;

                // ── ADMIN-ONLY: GitHub Repo Backup Configuration ──
                if (_isAdmin)
                {
                    card7b.Controls.Add(new Label
                    {
                        Text = "\u2500\u2500\u2500  GitHub Repo Backup (Optional, Admin Only)  \u2500\u2500\u2500",
                        Font = new Font("Segoe UI", 8, FontStyle.Bold),
                        ForeColor = dimColor,
                        Location = new Point(cx, cby),
                        AutoSize = true
                    });
                    cby += 22;

                    card7b.Controls.Add(new Label
                    {
                        Text = "Every backup creates a versioned commit. Full history preserved on GitHub.",
                        Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                        ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                        Location = new Point(cx, cby),
                        AutoSize = true
                    });
                    cby += 20;

                    var ghSettings = BackupManager.GetSettings();

                    // REPO OWNER
                    card7b.Controls.Add(new Label { Text = "Repo Owner:", Font = new Font("Segoe UI", 8), ForeColor = fgColor, Location = new Point(cx, cby), AutoSize = true });
                    var txtOwner = new TextBox
                    {
                        Text = ghSettings.GitHubRepoOwner ?? "",
                        Location = new Point(cx + 90, cby - 2),
                        Size = new Size(200, 22),
                        BackColor = fieldBg,
                        ForeColor = fgColor,
                        Font = new Font("Segoe UI", 8.5f)
                    };
                    card7b.Controls.Add(txtOwner);
                    cby += 26;

                    // REPO NAME
                    card7b.Controls.Add(new Label { Text = "Repo Name:", Font = new Font("Segoe UI", 8), ForeColor = fgColor, Location = new Point(cx, cby), AutoSize = true });
                    var txtRepo = new TextBox
                    {
                        Text = ghSettings.GitHubRepoName ?? "",
                        Location = new Point(cx + 90, cby - 2),
                        Size = new Size(200, 22),
                        BackColor = fieldBg,
                        ForeColor = fgColor,
                        Font = new Font("Segoe UI", 8.5f)
                    };
                    card7b.Controls.Add(txtRepo);
                    cby += 26;

                    // TOKEN (masked)
                    card7b.Controls.Add(new Label { Text = "Token:", Font = new Font("Segoe UI", 8), ForeColor = fgColor, Location = new Point(cx, cby), AutoSize = true });
                    var txtToken = new TextBox
                    {
                        Text = ghSettings.GitHubToken ?? "",
                        Location = new Point(cx + 90, cby - 2),
                        Size = new Size(200, 22),
                        BackColor = fieldBg,
                        ForeColor = fgColor,
                        Font = new Font("Segoe UI", 8.5f),
                        UseSystemPasswordChar = true
                    };
                    card7b.Controls.Add(txtToken);
                    cby += 26;

                    // AUTO-BACKUP INTERVAL
                    card7b.Controls.Add(new Label { Text = "Auto every:", Font = new Font("Segoe UI", 8), ForeColor = fgColor, Location = new Point(cx, cby), AutoSize = true });
                    var txtInterval = new TextBox
                    {
                        Text = ghSettings.GitHubAutoBackupMinutes.ToString(),
                        Location = new Point(cx + 90, cby - 2),
                        Size = new Size(50, 22),
                        BackColor = fieldBg,
                        ForeColor = fgColor,
                        Font = new Font("Segoe UI", 8.5f)
                    };
                    card7b.Controls.Add(txtInterval);
                    card7b.Controls.Add(new Label
                    {
                        Text = "min (0 = manual only)",
                        Font = new Font("Segoe UI", 7.5f),
                        ForeColor = dimColor,
                        Location = new Point(cx + 148, cby + 1),
                        AutoSize = true
                    });
                    cby += 30;

                    // STATUS LABEL for test/push results
                    var lblGhStatus = new Label
                    {
                        Text = !string.IsNullOrEmpty(ghSettings.GitHubRepoUrl)
                            ? $"Repo: {ghSettings.GitHubRepoUrl}  |  Commits: {ghSettings.GitHubCommitCount}"
                            : "Not connected",
                        Font = new Font("Segoe UI", 7.5f),
                        ForeColor = dimColor,
                        Location = new Point(cx, cby),
                        Size = new Size(cardW - 40, 16)
                    };
                    card7b.Controls.Add(lblGhStatus);
                    cby += 20;

                    // BUTTONS ROW: Save | Test | Push Now | Open Repo
                    int btnX = cx;

                    var btnSaveGh = MakeActionButton("\U0001f4be Save", Color.FromArgb(34, 120, 190), Color.White, btnX, cby, 70);
                    btnSaveGh.Click += (s, e) =>
                    {
                        var updated = BackupManager.GetSettings();
                        string ownerText = txtOwner.Text.Trim();
                        string repoText = txtRepo.Text.Trim();
                        string tokenText = txtToken.Text.Trim();
                        updated.GitHubRepoOwner = ownerText;
                        updated.GitHubRepoName = repoText;
                        updated.GitHubToken = tokenText;
                        updated.GitHubBackupEnabled =
                            !string.IsNullOrWhiteSpace(ownerText) &&
                            !string.IsNullOrWhiteSpace(repoText) &&
                            !string.IsNullOrWhiteSpace(tokenText);
                        int mins;
                        int.TryParse(txtInterval.Text.Trim(), out mins);
                        updated.GitHubAutoBackupMinutes = Math.Max(0, mins);
                        BackupManager.UpdateSettings(updated);
                        BackupManager.ResetGitHubClient();
                        ShowStatus(updated.GitHubBackupEnabled
                            ? "\u2705 GitHub backup settings saved!"
                            : "\u26a0 GitHub fields incomplete. Backup is disabled until all fields are filled.", false);
                    };
                    card7b.Controls.Add(btnSaveGh);
                    btnX += 78;

                    var btnTestGh = MakeActionButton("\U0001f50d Test", Color.FromArgb(100, 116, 139), Color.White, btnX, cby, 70);
                    btnTestGh.Click += async (s, e) =>
                    {
                        lblGhStatus.Text = "Testing connection...";
                        var result = await BackupManager.TestGitHubConnectionAsync();
                        lblGhStatus.Text = result.message;
                        lblGhStatus.ForeColor = result.success
                            ? (_isDarkMode ? Color.FromArgb(120, 200, 120) : Color.Green)
                            : (_isDarkMode ? Color.FromArgb(220, 100, 100) : Color.Red);
                    };
                    card7b.Controls.Add(btnTestGh);
                    btnX += 78;

                    var btnPushGh = MakeActionButton("\U0001f680 Push Now", Color.FromArgb(40, 140, 60), Color.White, btnX, cby, 90);
                    btnPushGh.Click += async (s, e) =>
                    {
                        lblGhStatus.Text = "Pushing backup to GitHub...";
                        bool ok = await BackupManager.PushToGitHubAsync();
                        if (ok)
                        {
                            var s2 = BackupManager.GetSettings();
                            lblGhStatus.Text = $"\u2705 Pushed! Commit #{s2.GitHubCommitCount} | {s2.GitHubRepoUrl}";
                            lblGhStatus.ForeColor = _isDarkMode ? Color.FromArgb(120, 200, 120) : Color.Green;
                            lblStatus.Text = BackupManager.GetBackupStatus();
                        }
                        else
                        {
                            lblGhStatus.Text = "\u274c Push failed — check Debug log for details.";
                            lblGhStatus.ForeColor = _isDarkMode ? Color.FromArgb(220, 100, 100) : Color.Red;
                        }
                    };
                    card7b.Controls.Add(btnPushGh);
                    btnX += 98;

                    // Open Repo link (always visible, resolves latest saved URL at click-time)
                    var btnOpenRepo = MakeActionButton("\U0001f517 Open Repo", Color.FromArgb(80, 80, 80), Color.White, btnX, cby, 100);
                    btnOpenRepo.Click += (s, e) =>
                    {
                        string url = BackupManager.GetSettings().GitHubRepoUrl;
                        if (string.IsNullOrWhiteSpace(url))
                        {
                            ShowStatus("\u26a0 No repo URL yet. Click Test after saving valid owner/repo/token.", true);
                            return;
                        }
                        try { System.Diagnostics.Process.Start(url); } catch { }
                    };
                    card7b.Controls.Add(btnOpenRepo);
                }

                yR += card7bH + 10;
            }

            // ══════════════════════════════════════════════════
            // CARD 8: YOUR PROFILE — NAME CHANGE, FAVORITES
            // ══════════════════════════════════════════════════
            {
            int card8H = 980;
                var card8 = MakeCard(col1X, y, cardW, card8H);
                mainPanel.Controls.Add(card8);
                int c8y = 12;
                int c8x = 16;

                card8.Controls.Add(MakeSectionIcon("\U0001f464", c8x, c8y));
                card8.Controls.Add(MakeSectionTitle("Your Profile", c8x + 28, c8y));
                c8y += 30;

                // Current name display
                card8.Controls.Add(MakeFieldLabel("YOUR NAME", c8x, c8y));
                var txtDisplayName = MakeTextBox(c8x, c8y + 16, 220);
                txtDisplayName.Text = _currentUserName;
                card8.Controls.Add(txtDisplayName);

                var btnChangeName = MakeActionButton("\u270f Rename", Color.FromArgb(59, 130, 246), Color.White, 260, c8y + 14, 90);
                btnChangeName.Click += (s, e) =>
                {
                    string newName = txtDisplayName.Text.Trim();
                    if (string.IsNullOrEmpty(newName))
                    { ShowStatus("\u274c Name cannot be empty", true); return; }
                    if (newName == _currentUserName)
                    { ShowStatus("\u2139 Name unchanged", false); return; }
                    if (_team.Members.Any(m => m.Equals(newName, StringComparison.OrdinalIgnoreCase) && !m.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase)))
                    { ShowStatus("\u274c Name already taken in this team", true); return; }

                    var confirm = MessageBox.Show(
                        $"Change your name from \"{_currentUserName}\" to \"{newName}\"?\n\nThis will update your name across the team.",
                        "Change Name", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) return;

                    // Update in team members list
                    int idx = _team.Members.FindIndex(m => m.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) _team.Members[idx] = newName;

                    // Update MembersMeta key
                    if (_team.MembersMeta != null && _team.MembersMeta.ContainsKey(_currentUserName))
                    {
                        var meta = _team.MembersMeta[_currentUserName];
                        _team.MembersMeta.Remove(_currentUserName);
                        _team.MembersMeta[newName] = meta;
                    }

                    // Update admin name if this user is admin
                    if (_team.AdminName != null && _team.AdminName.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase))
                        _team.AdminName = newName;

                    // Update assistant admin list
                    if (_team.AssistantAdmins != null)
                    {
                        int aIdx = _team.AssistantAdmins.FindIndex(a => a.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase));
                        if (aIdx >= 0) _team.AssistantAdmins[aIdx] = newName;
                    }

                    // Update user info
                    var userInfo = _allUsers.FirstOrDefault(u => u.Name.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase));
                    if (userInfo != null) userInfo.Name = newName;

                    // Save locally
                    UserStorage.SaveTeam(_team);
                    UserStorage.SaveUsers(_allUsers);
                    UserStorage.SaveLastUser(newName);

                    // Save to Firebase
                    _ = UserStorage.SaveTeamToFirebaseAsync(_team);

                    string oldName = _currentUserName;
                    _currentUserName = newName;
                    _dataChanged = true;
                    NeedsRestart = true;
                    ShowStatus($"\u2705 Name changed to \"{newName}\" — restart app to apply fully", false);
                };
                card8.Controls.Add(btnChangeName);
                c8y += 50;

                var currentUserForColor = _allUsers.FirstOrDefault(u => u.Name.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase));
                Color currentColorParsed = currentUserForColor?.GetDrawingColor(Color.FromArgb(100, 100, 120)) ?? Color.FromArgb(100, 100, 120);
                string currentAvatarBase64 = _team?.MembersMeta != null && _team.MembersMeta.ContainsKey(_currentUserName)
                    ? (_team.MembersMeta[_currentUserName].AvatarBase64 ?? "")
                    : "";

                card8.Controls.Add(MakeFieldLabel("YOUR AVATAR", c8x, c8y));

                var picAvatarPreview = new PictureBox
                {
                    Location = new Point(c8x, c8y + 18),
                    Size = new Size(52, 52),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Transparent
                };

                Action refreshAvatarPreview = () =>
                {
                    if (picAvatarPreview.Image != null)
                    {
                        var oldImage = picAvatarPreview.Image;
                        picAvatarPreview.Image = null;
                        oldImage.Dispose();
                    }

                    picAvatarPreview.Image = string.IsNullOrWhiteSpace(currentAvatarBase64)
                        ? CreateInitialAvatarImage(52, currentColorParsed, _currentUserName)
                        : AvatarBase64ToImage(currentAvatarBase64, 52, currentColorParsed, _currentUserName);
                };

                refreshAvatarPreview();
                card8.Controls.Add(picAvatarPreview);

                var btnAddAvatar = MakeActionButton("Add Avatar", Color.FromArgb(59, 130, 246), Color.White, c8x + 66, c8y + 18, 100);
                btnAddAvatar.Click += (s, e) =>
                {
                    using (var ofd = new OpenFileDialog())
                    {
                        ofd.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
                        ofd.Title = "Choose avatar image";
                        if (ofd.ShowDialog(this) != DialogResult.OK) return;

                        try
                        {
                            currentAvatarBase64 = ConvertImageFileToBase64(ofd.FileName, 96);
                            EnsureCurrentUserMetaExists();
                            _team.MembersMeta[_currentUserName].AvatarBase64 = currentAvatarBase64;
                            UserStorage.SaveTeam(_team);
                            _ = UserStorage.SaveTeamToFirebaseAsync(_team);
                            refreshAvatarPreview();
                            _dataChanged = true;
                            ShowStatus("\u2705 Avatar updated", false);
                        }
                        catch (Exception ex)
                        {
                            ShowStatus("\u274c Avatar update failed: " + ex.Message, true);
                        }
                    }
                };
                card8.Controls.Add(btnAddAvatar);

                c8y += 84;

                // ── YOUR COLOR — USER COLOR PICKER ──
                card8.Controls.Add(MakeFieldLabel("YOUR COLOR", c8x, c8y));
                c8y += 16;

                // Current user color preview panel (32x32 circle)
                var pnlColorPreview = new Panel
                {
                    Size = new Size(32, 32),
                    Location = new Point(c8x, c8y),
                    BackColor = Color.Transparent
                };
                pnlColorPreview.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (var brush = new SolidBrush(pnlColorPreview.Tag is Color c ? c : currentColorParsed))
                        e.Graphics.FillEllipse(brush, 0, 0, 31, 31);
                    // Draw initial
                    string init = _currentUserName.Length > 0 ? _currentUserName[0].ToString().ToUpper() : "?";
                    using (var font = new Font("Segoe UI", 12, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(Color.White))
                    {
                        var textSize = e.Graphics.MeasureString(init, font);
                        float tx = (32 - textSize.Width) / 2;
                        float ty = (32 - textSize.Height) / 2;
                        e.Graphics.DrawString(init, font, textBrush, tx, ty);
                    }
                };
                card8.Controls.Add(pnlColorPreview);

                // Palette color buttons (same 15 as admin uses)
                int colorBtnSize = 24;
                int colorsPerRow = 8;
                int colorStartX = c8x + 40;
                for (int ci = 0; ci < MemberColors.Length; ci++)
                {
                    string hexColor = MemberColors[ci];
                    Color parsedColor = ColorTranslator.FromHtml(hexColor);
                    int row = ci / colorsPerRow;
                    int col = ci % colorsPerRow;
                    var btnColor = new Panel
                    {
                        Size = new Size(colorBtnSize, colorBtnSize),
                        Location = new Point(colorStartX + col * (colorBtnSize + 4), c8y + row * (colorBtnSize + 4)),
                        BackColor = parsedColor,
                        Cursor = Cursors.Hand
                    };
                    btnColor.Paint += (s, e) =>
                    {
                        // Show selection border if this is current color
                        string curHex = currentUserForColor?.Color ?? "";
                        if (curHex.Equals(hexColor, StringComparison.OrdinalIgnoreCase))
                        {
                            using (var pen = new Pen(Color.White, 2))
                                e.Graphics.DrawRectangle(pen, 1, 1, colorBtnSize - 3, colorBtnSize - 3);
                        }
                    };
                    btnColor.Click += (s, e) =>
                    {
                        if (currentUserForColor == null) return;
                        currentUserForColor.Color = hexColor;
                        UserStorage.SaveUsers(_allUsers);

                        // Sync to Firebase MembersMeta
                        if (_team != null)
                        {
                            if (_team.MembersMeta == null)
                                _team.MembersMeta = new Dictionary<string, MemberMeta>();
                            if (!_team.MembersMeta.ContainsKey(currentUserForColor.Name))
                                _team.MembersMeta[currentUserForColor.Name] = new MemberMeta();
                            _team.MembersMeta[currentUserForColor.Name].Color = hexColor;
                            UserStorage.SaveTeam(_team);
                            _ = UserStorage.SaveTeamToFirebaseAsync(_team);
                        }

                        // Update preview
                        pnlColorPreview.Tag = parsedColor;
                        pnlColorPreview.Invalidate();
                        // Refresh all color buttons to show new selection
                        foreach (Control ctrl in card8.Controls)
                        {
                            if (ctrl is Panel p && p.Size == new Size(colorBtnSize, colorBtnSize) && p.Cursor == Cursors.Hand)
                                p.Invalidate();
                        }
                        _dataChanged = true;
                        ShowStatus($"\u2705 Color set to {hexColor}", false);
                    };
                    card8.Controls.Add(btnColor);
                }

                // Custom color button (opens ColorDialog)
                int lastRow = (MemberColors.Length - 1) / colorsPerRow;
                int lastCol = MemberColors.Length % colorsPerRow;
                var btnCustomColor = MakeActionButton("\u2026", Color.FromArgb(70, 80, 95), Color.White,
                    colorStartX + lastCol * (colorBtnSize + 4), c8y + lastRow * (colorBtnSize + 4), colorBtnSize + 10);
                btnCustomColor.Size = new Size(colorBtnSize + 12, colorBtnSize);
                btnCustomColor.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                btnCustomColor.Click += (s, e) =>
                {
                    if (currentUserForColor == null) return;
                    using (var cd = new ColorDialog { Color = currentColorParsed, FullOpen = true })
                    {
                        if (cd.ShowDialog() == DialogResult.OK)
                        {
                            string hex = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
                            currentUserForColor.Color = hex;
                            UserStorage.SaveUsers(_allUsers);

                            if (_team != null)
                            {
                                if (_team.MembersMeta == null)
                                    _team.MembersMeta = new Dictionary<string, MemberMeta>();
                                if (!_team.MembersMeta.ContainsKey(currentUserForColor.Name))
                                    _team.MembersMeta[currentUserForColor.Name] = new MemberMeta();
                                _team.MembersMeta[currentUserForColor.Name].Color = hex;
                                UserStorage.SaveTeam(_team);
                                _ = UserStorage.SaveTeamToFirebaseAsync(_team);
                            }

                            pnlColorPreview.Tag = cd.Color;
                            pnlColorPreview.Invalidate();
                            foreach (Control ctrl in card8.Controls)
                            {
                                if (ctrl is Panel p && p.Size == new Size(colorBtnSize, colorBtnSize) && p.Cursor == Cursors.Hand)
                                    p.Invalidate();
                            }
                            _dataChanged = true;
                            ShowStatus($"\u2705 Color set to {hex}", false);
                        }
                    }
                };
                card8.Controls.Add(btnCustomColor);

                c8y += (lastRow + 1) * (colorBtnSize + 4) + 12;

                // Favorites label
                card8.Controls.Add(MakeFieldLabel("FAVORITE USERS", c8x, c8y));
                c8y += 16;
                var lblFavorites = new Label
                {
                    Text = GetFavoritesDisplayText(),
                    Font = new Font("Segoe UI", 8.5f),
                    ForeColor = _isDarkMode ? Color.FromArgb(160, 170, 185) : Color.FromArgb(80, 90, 100),
                    Location = new Point(c8x, c8y),
                    Size = new Size(cardW - 40, 20)
                };
                card8.Controls.Add(lblFavorites);
                c8y += 26;

                var btnEditFav = MakeActionButton("\u2b50 Edit Favorites", Color.FromArgb(251, 146, 60), Color.White, c8x, c8y, 150);
                btnEditFav.Click += (s, e) =>
                {
                    ShowEditFavoritesDialog();
                    lblFavorites.Text = GetFavoritesDisplayText();
                };
                card8.Controls.Add(btnEditFav);

                // Reset Panel Positions button
                var btnResetLayout = MakeActionButton("\U0001f504 Reset Positions", Color.FromArgb(100, 116, 139), Color.White, c8x + 160, c8y, 150);
                btnResetLayout.Click += (s, e) =>
                {
                    var confirm = MessageBox.Show(
                        "Reset all panel positions to default?\nApp will restart.",
                        "Reset Layout", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm == DialogResult.Yes)
                    {
                        PanelLayoutManager.ResetLayout();
                        NeedsRestart = true;
                        ShowStatus("\u2705 Panel positions reset — restart app to apply", false);
                    }
                };
                card8.Controls.Add(btnResetLayout);

                var btnResetSplitters = MakeActionButton("\u21f3 Reset Splitters", Color.FromArgb(59, 130, 246), Color.White, c8x + 320, c8y, 150);
                btnResetSplitters.Click += (s, e) =>
                {
                    ResetSplittersRequested?.Invoke();
                    ShowStatus("\u2705 Splitters reset to default", false);
                };
                card8.Controls.Add(btnResetSplitters);
                c8y += 44;

                // ── CHAT FONT SIZE SETTING ──
                card8.Controls.Add(MakeFieldLabel("CHAT FONT SIZE", c8x, c8y));
                c8y += 16;

                string currentSize = UserStorage.GetChatFontSize(_currentUserName);

                var btnSmall = MakeActionButton("Small",
                    currentSize == "Small" ? Color.FromArgb(255, 127, 80) : Color.FromArgb(70, 80, 95),
                    Color.White, c8x, c8y, 80);
                var btnMedium = MakeActionButton("Medium",
                    currentSize == "Medium" ? Color.FromArgb(255, 127, 80) : Color.FromArgb(70, 80, 95),
                    Color.White, c8x + 90, c8y, 80);
                var btnBig = MakeActionButton("Big",
                    currentSize == "Big" ? Color.FromArgb(255, 127, 80) : Color.FromArgb(70, 80, 95),
                    Color.White, c8x + 180, c8y, 80);

                Color activeCol = Color.FromArgb(255, 127, 80);
                Color inactiveCol = Color.FromArgb(70, 80, 95);

                btnSmall.Click += (s, e) =>
                {
                    UserStorage.SaveChatFontSize(_currentUserName, "Small");
                    btnSmall.BackColor = activeCol; btnMedium.BackColor = inactiveCol; btnBig.BackColor = inactiveCol;
                    ChatFontSizeChanged?.Invoke("Small");
                    ShowStatus("\u2705 Chat font: Small", false);
                };
                btnMedium.Click += (s, e) =>
                {
                    UserStorage.SaveChatFontSize(_currentUserName, "Medium");
                    btnSmall.BackColor = inactiveCol; btnMedium.BackColor = activeCol; btnBig.BackColor = inactiveCol;
                    ChatFontSizeChanged?.Invoke("Medium");
                    ShowStatus("\u2705 Chat font: Medium", false);
                };
                btnBig.Click += (s, e) =>
                {
                    UserStorage.SaveChatFontSize(_currentUserName, "Big");
                    btnSmall.BackColor = inactiveCol; btnMedium.BackColor = inactiveCol; btnBig.BackColor = activeCol;
                    ChatFontSizeChanged?.Invoke("Big");
                    ShowStatus("\u2705 Chat font: Big", false);
                };

                card8.Controls.Add(btnSmall);
                card8.Controls.Add(btnMedium);
                card8.Controls.Add(btnBig);
                c8y += 44;

                // ── SOUND TOGGLE ──
                card8.Controls.Add(MakeFieldLabel("SOUND", c8x, c8y));
                c8y += 16;

                bool soundOn = SoundManager.Enabled;
                var btnSoundToggle = MakeSwitchButton(
                    soundOn ? "\U0001f50a Sound ON" : "\U0001f507 Sound OFF",
                    soundOn, c8x, c8y, 190);
                btnSoundToggle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                btnSoundToggle.Click += (s, e) =>
                {
                    SoundManager.Enabled = !SoundManager.Enabled;
                    bool on = SoundManager.Enabled;
                    btnSoundToggle.Text = on ? "\U0001f50a Sound ON" : "\U0001f507 Sound OFF";
                    btnSoundToggle.AccessibleDescription = on ? "on" : "off";
                    btnSoundToggle.Invalidate();
                    SoundToggled?.Invoke(on);
                    ShowStatus(on ? "\u2705 Sound enabled" : "\U0001f507 Sound muted", false);
                };
                card8.Controls.Add(btnSoundToggle);
                c8y += 40;

                // ── DARK MODE TOGGLE ──
                card8.Controls.Add(MakeFieldLabel("THEME", c8x, c8y));
                c8y += 16;

                var btnThemeToggle = MakeSwitchButton(
                    _isDarkMode ? "\U0001f319 Dark Mode ON" : "\u2600 Light Mode OFF",
                    _isDarkMode, c8x, c8y, 190);
                btnThemeToggle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                btnThemeToggle.Click += (s, e) =>
                {
                    _isDarkMode = !_isDarkMode;
                    btnThemeToggle.Text = _isDarkMode ? "\U0001f319 Dark Mode ON" : "\u2600 Light Mode OFF";
                    btnThemeToggle.AccessibleDescription = _isDarkMode ? "on" : "off";
                    btnThemeToggle.ForeColor = Color.White;
                    btnThemeToggle.Invalidate();
                    DarkModeChanged?.Invoke(_isDarkMode);
                    ShowStatus(_isDarkMode ? "\U0001f319 Dark mode enabled" : "\u2600 Light mode enabled", false);
                };
                card8.Controls.Add(btnThemeToggle);
                c8y += 54;

                // ── PANEL VISIBILITY TOGGLES ──
                card8.Controls.Add(MakeFieldLabel("PANEL VISIBILITY", c8x, c8y));
                c8y += 18;

            string[] panelNames = { "Board", "Chat", "Team", "Files", "Calendar", "Weather", "Personal Board", "Ask AI", "AI Chat" };
                string[] panelIcons = { "\U0001f4cb", "\U0001f4ac", "\U0001f465", "\U0001f4c1", "\U0001f4c6", "\u2601", "\U0001f4cc", "\U0001f916", "\U0001f4ac" };
                int panelSwitchWidth = 214;
                int panelSwitchHeight = 38;
                int panelSwitchGapY = 10;
                for (int pi = 0; pi < panelNames.Length; pi++)
                {
                    string pName = panelNames[pi];
                    string pIcon = panelIcons[pi];
                    bool pVisible = _panelVisibility.ContainsKey(pName) && _panelVisibility[pName];
                    int panelX = c8x;
                    int panelY = c8y + pi * (panelSwitchHeight + panelSwitchGapY);

                    var btnPanelToggle = MakeSwitchButton(
                        pIcon + " " + pName,
                        pVisible, panelX, panelY, panelSwitchWidth);
                    btnPanelToggle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
                    btnPanelToggle.Tag = pName;

                    string localName = pName;
                    string localIcon = pIcon;
                    btnPanelToggle.Click += (s, e) =>
                    {
                        bool nowVisible = !_panelVisibility[localName];
                        _panelVisibility[localName] = nowVisible;
                        ((Button)s).Text = localIcon + " " + localName;
                        ((Button)s).AccessibleDescription = nowVisible ? "on" : "off";
                        ((Button)s).Invalidate();
                        PanelVisibilityChanged?.Invoke(localName, nowVisible);
                        ShowStatus(localName + (nowVisible ? " shown" : " hidden"), false);
                    };
                    card8.Controls.Add(btnPanelToggle);
                }
                c8y += (panelSwitchHeight * panelNames.Length) + (panelSwitchGapY * (panelNames.Length - 1)) + 10;

                // ── COUNTRY SETTING ──
                card8.Controls.Add(MakeFieldLabel("YOUR COUNTRY", c8x, c8y));
                c8y += 16;

                var cmbCountry = new ComboBox
                {
                    Location = new Point(c8x, c8y),
                    Width = 220,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 9f),
                    BackColor = _isDarkMode ? Color.FromArgb(40, 46, 56) : Color.White,
                    ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.Black
                };
                cmbCountry.Items.Add("-- Not set --");
                foreach (var kvp in PublicHolidays.Countries)
                    cmbCountry.Items.Add($"{kvp.Value} ({kvp.Key})");

                // Select current country
                var currentUser = _allUsers.FirstOrDefault(u => u.Name.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase));
                if (currentUser != null && !string.IsNullOrEmpty(currentUser.Country) && PublicHolidays.Countries.ContainsKey(currentUser.Country))
                {
                    string displayName = $"{PublicHolidays.Countries[currentUser.Country]} ({currentUser.Country.ToUpper()})";
                    int idx2 = cmbCountry.Items.IndexOf(displayName);
                    if (idx2 >= 0) cmbCountry.SelectedIndex = idx2;
                    else cmbCountry.SelectedIndex = 0;
                }
                else
                    cmbCountry.SelectedIndex = 0;

                cmbCountry.SelectedIndexChanged += (s, e) =>
                {
                    if (currentUser == null) return;
                    string selected = cmbCountry.SelectedItem?.ToString() ?? "";
                    if (selected.Contains("(") && selected.Contains(")"))
                    {
                        int start = selected.LastIndexOf('(') + 1;
                        int end = selected.LastIndexOf(')');
                        currentUser.Country = selected.Substring(start, end - start).Trim();
                    }
                    else
                        currentUser.Country = "";

                    UserStorage.SaveUsers(_allUsers);

                    // ── SYNC COUNTRY TO FIREBASE MembersMeta so ALL team members can see local time ──
                    if (_team != null)
                    {
                        if (_team.MembersMeta == null)
                            _team.MembersMeta = new Dictionary<string, MemberMeta>();
                        if (!_team.MembersMeta.ContainsKey(currentUser.Name))
                            _team.MembersMeta[currentUser.Name] = new MemberMeta();
                        _team.MembersMeta[currentUser.Name].Country = currentUser.Country;
                        UserStorage.SaveTeam(_team);
                        _ = UserStorage.SaveTeamToFirebaseAsync(_team);
                    }

                    _dataChanged = true;
                    ShowStatus($"\u2705 Country set to {cmbCountry.SelectedItem}", false);
                };
                card8.Controls.Add(cmbCountry);
                c8y += 36;

                // ── WEEKLY HOUR LIMIT ──
                card8.Controls.Add(MakeFieldLabel("WEEKLY HOUR LIMIT", c8x, c8y));
                c8y += 16;

                var nudHourLimit = new NumericUpDown
                {
                    Location = new Point(c8x, c8y),
                    Width = 100,
                    Minimum = 1,
                    Maximum = 80,
                    DecimalPlaces = 1,
                    Increment = 0.5m,
                    Value = currentUser != null && currentUser.WeeklyHourLimit > 0
                        ? (decimal)currentUser.WeeklyHourLimit : 10m,
                    Font = new Font("Segoe UI", 9f),
                    BackColor = _isDarkMode ? Color.FromArgb(40, 46, 56) : Color.White,
                    ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.Black
                };

                var lblHourInfo = new Label
                {
                    Text = "hours / week",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(120, 130, 150) : Color.FromArgb(100, 110, 130),
                    Location = new Point(c8x + 108, c8y + 3),
                    AutoSize = true
                };

                nudHourLimit.ValueChanged += (s, e) =>
                {
                    if (currentUser == null) return;
                    currentUser.WeeklyHourLimit = (double)nudHourLimit.Value;
                    UserStorage.SaveUsers(_allUsers);

                    // ── SYNC WEEKLY HOUR LIMIT TO FIREBASE MembersMeta so ALL team members see correct progress ──
                    if (_team != null)
                    {
                        if (_team.MembersMeta == null)
                            _team.MembersMeta = new Dictionary<string, MemberMeta>();
                        if (!_team.MembersMeta.ContainsKey(currentUser.Name))
                            _team.MembersMeta[currentUser.Name] = new MemberMeta();
                        _team.MembersMeta[currentUser.Name].WeeklyHourLimit = currentUser.WeeklyHourLimit;
                        UserStorage.SaveTeam(_team);
                        _ = UserStorage.SaveTeamToFirebaseAsync(_team);
                    }

                    _dataChanged = true;
                    ShowStatus($"\u2705 Weekly limit set to {nudHourLimit.Value}h", false);
                };

                card8.Controls.Add(nudHourLimit);
                card8.Controls.Add(lblHourInfo);

                c8y += 36;

                // —— TEAM WORK LIMITS (admin only) ——
                if (_isAdmin)
                {
                    card8.Controls.Add(MakeFieldLabel("TEAM DAILY LIMIT", c8x, c8y));
                    c8y += 16;

                    var nudTeamDailyLimit = new NumericUpDown
                    {
                        Location = new Point(c8x, c8y),
                        Width = 100,
                        Minimum = 1,
                        Maximum = 24,
                        DecimalPlaces = 1,
                        Increment = 0.5m,
                        Value = _team != null && _team.DailyWorkingLimitHours > 0
                            ? (decimal)_team.DailyWorkingLimitHours : 6m,
                        Font = new Font("Segoe UI", 9f),
                        BackColor = _isDarkMode ? Color.FromArgb(40, 46, 56) : Color.White,
                        ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.Black
                    };

                    var lblTeamDailyInfo = new Label
                    {
                        Text = "hours / day",
                        Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                        ForeColor = _isDarkMode ? Color.FromArgb(120, 130, 150) : Color.FromArgb(100, 110, 130),
                        Location = new Point(c8x + 108, c8y + 3),
                        AutoSize = true
                    };

                    nudTeamDailyLimit.ValueChanged += (s, e) =>
                    {
                        if (_team == null) return;
                        _team.DailyWorkingLimitHours = (double)nudTeamDailyLimit.Value;
                        UserStorage.SaveTeam(_team);
                        _ = UserStorage.SaveTeamToFirebaseAsync(_team);
                        _dataChanged = true;
                        ShowStatus($"✅ Team daily limit set to {nudTeamDailyLimit.Value}h", false);
                    };

                    card8.Controls.Add(nudTeamDailyLimit);
                    card8.Controls.Add(lblTeamDailyInfo);
                    c8y += 36;

                    card8.Controls.Add(MakeFieldLabel("TEAM WEEKLY LIMIT", c8x, c8y));
                    c8y += 16;

                    var nudTeamWeeklyLimit = new NumericUpDown
                    {
                        Location = new Point(c8x, c8y),
                        Width = 100,
                        Minimum = 1,
                        Maximum = 200,
                        DecimalPlaces = 1,
                        Increment = 0.5m,
                        Value = _team != null && _team.WeeklyWorkingLimitHours > 0
                            ? (decimal)_team.WeeklyWorkingLimitHours : 40m,
                        Font = new Font("Segoe UI", 9f),
                        BackColor = _isDarkMode ? Color.FromArgb(40, 46, 56) : Color.White,
                        ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.Black
                    };

                    var lblTeamWeeklyInfo = new Label
                    {
                        Text = "hours / week",
                        Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                        ForeColor = _isDarkMode ? Color.FromArgb(120, 130, 150) : Color.FromArgb(100, 110, 130),
                        Location = new Point(c8x + 108, c8y + 3),
                        AutoSize = true
                    };

                    nudTeamWeeklyLimit.ValueChanged += (s, e) =>
                    {
                        if (_team == null) return;
                        _team.WeeklyWorkingLimitHours = (double)nudTeamWeeklyLimit.Value;
                        UserStorage.SaveTeam(_team);
                        _ = UserStorage.SaveTeamToFirebaseAsync(_team);
                        _dataChanged = true;
                        ShowStatus($"✅ Team weekly limit set to {nudTeamWeeklyLimit.Value}h", false);
                    };

                    card8.Controls.Add(nudTeamWeeklyLimit);
                    card8.Controls.Add(lblTeamWeeklyInfo);
                }

                card8.Height = Math.Max(card8H, c8y + (_isAdmin ? 54 : 44));
                y += card8.Height + 10;
            }

            // ══════════════════════════════════════════════════
            // CARD 9: JOIN ANOTHER TEAM — ENTER INVITE CODE
            // ══════════════════════════════════════════════════
            {
                int card9H = 140;
                var card9 = MakeCard(col2X, yR, cardW, card9H);
                mainPanel.Controls.Add(card9);
                int c9y = 12;
                int c9x = 16;

                card9.Controls.Add(MakeSectionIcon("\U0001f517", c9x, c9y));
                card9.Controls.Add(MakeSectionTitle("Join Another Team", c9x + 28, c9y));

                var lblJoinSub = new Label
                {
                    Text = "Enter an invite code to join a different project/team",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                    Location = new Point(c9x + 28 + 150, c9y + 4),
                    AutoSize = true
                };
                card9.Controls.Add(lblJoinSub);
                c9y += 30;

                card9.Controls.Add(MakeFieldLabel("INVITE CODE", c9x, c9y));
                var txtInviteCode = MakeTextBox(c9x, c9y + 16, 160);
                txtInviteCode.Font = new Font("Consolas", 14, FontStyle.Bold);
                txtInviteCode.MaxLength = 6;
                txtInviteCode.TextAlign = HorizontalAlignment.Center;
                txtInviteCode.CharacterCasing = CharacterCasing.Upper;
                card9.Controls.Add(txtInviteCode);

                var btnJoinTeam = MakeActionButton("\U0001f680 Join Team", Color.FromArgb(34, 197, 94), Color.White, 200, c9y + 14, 120);
                btnJoinTeam.Size = new Size(120, 34);
                btnJoinTeam.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                btnJoinTeam.Click += async (s, e) =>
                {
                    string code = txtInviteCode.Text.Trim().ToUpper();
                    if (code.Length != 6)
                    { ShowStatus("\u274c Invite code must be 6 characters", true); return; }

                    // Check if already joined
                    var joinedTeams = UserStorage.GetJoinedTeams();
                    if (joinedTeams.Any(t => t.JoinCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
                    { ShowStatus("\u2139 You already joined this team", false); return; }

                    ShowStatus("\U0001f50d Looking up team...", false);
                    btnJoinTeam.Enabled = false;

                    try
                    {
                        var foundTeam = await UserStorage.FindTeamByJoinCodeAsync(code);
                        if (foundTeam == null)
                        { ShowStatus("\u274c Team not found. Check the code.", true); return; }

                        if (foundTeam.IsBanned(_currentUserName))
                        { ShowStatus("\U0001f6ab You are banned from this team", true); return; }

                        // Add member to Firebase team
                        bool alreadyMember = foundTeam.Members.Any(m => m.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase));
                        if (!alreadyMember)
                        {
                            await UserStorage.AddMemberToFirebaseTeamAsync(code, _currentUserName);
                            foundTeam.Members.Add(_currentUserName);
                        }

                        // Save team locally
                        UserStorage.SaveTeamByCode(code, foundTeam);

                        // Create user list for the new team
                        var newTeamUsers = new List<UserInfo>();
                        foreach (var memberName in foundTeam.Members)
                        {
                            bool isAdmin = memberName.Equals(foundTeam.AdminName, StringComparison.OrdinalIgnoreCase);
                            newTeamUsers.Add(new UserInfo(memberName, isAdmin, foundTeam.JoinCode));
                        }
                        UserStorage.SaveUsersByCode(code, newTeamUsers);

                        // Add to teams index
                        UserStorage.AddTeamToIndex(code, foundTeam.TeamName, _currentUserName);

                        _dataChanged = true;
                        ShowStatus($"\u2705 Joined \"{foundTeam.TeamName}\"! You can switch teams from the Teams button.", false);
                        txtInviteCode.Clear();
                    }
                    catch (Exception ex)
                    {
                        ShowStatus("\u274c Error joining: " + ex.Message, true);
                    }
                    finally
                    {
                        btnJoinTeam.Enabled = true;
                    }
                };
                card9.Controls.Add(btnJoinTeam);

                // Show currently joined teams
                c9y += 56;
                var joinedList = UserStorage.GetJoinedTeams();
                string teamsText = joinedList.Count > 0
                    ? "Joined: " + string.Join(", ", joinedList.Select(t => t.TeamName ?? t.JoinCode))
                    : "No other teams joined";
                card9.Controls.Add(new Label
                {
                    Text = teamsText,
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = _isDarkMode ? Color.FromArgb(120, 130, 140) : Color.FromArgb(100, 110, 120),
                    Location = new Point(c9x, c9y),
                    Size = new Size(cardW - 36, 18)
                });

                yR += card9H + 10;
            }

            // ══════════════════════════════════════════════════
            // CARD 9B: UNITE WITH ANOTHER TEAM (ADMIN ONLY)
            // Permanent merge: both admins must enter each other's code.
            // Merges all users, stickers, logs, chat into one team.
            // ══════════════════════════════════════════════════
            if (_isAdmin)
            {
                int card9bH = 200;
                var card9b = MakeCard(col2X, yR, cardW, card9bH);
                mainPanel.Controls.Add(card9b);
                int c9by = 12;
                int c9bx = 16;

                card9b.Controls.Add(MakeSectionIcon("\U0001f91d", c9bx, c9by));
                card9b.Controls.Add(MakeSectionTitle("Unite With Another Team", c9bx + 28, c9by));

                var lblUniteSub = new Label
                {
                    Text = "Permanently merge two teams into one",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                    Location = new Point(c9bx + 28, c9by + 20),
                    AutoSize = true
                };
                card9b.Controls.Add(lblUniteSub);
                c9by += 42;

                // Instructions
                var lblInstructions = new Label
                {
                    Text = "Both teams must enter each other's code and press Unite.\n" +
                           "All members, stickers, logs, and chat will be combined.\n" +
                           "Both admins will have admin powers in the merged team.",
                    Font = new Font("Segoe UI", 7.5f),
                    ForeColor = _isDarkMode ? Color.FromArgb(140, 150, 170) : Color.FromArgb(80, 90, 110),
                    Location = new Point(c9bx, c9by),
                    Size = new Size(cardW - 36, 40)
                };
                card9b.Controls.Add(lblInstructions);
                c9by += 44;

                // Other team code input
                card9b.Controls.Add(MakeFieldLabel("OTHER TEAM'S CODE", c9bx, c9by));
                var txtMergeCode = MakeTextBox(c9bx, c9by + 16, 120);
                txtMergeCode.Font = new Font("Consolas", 14, FontStyle.Bold);
                txtMergeCode.MaxLength = 6;
                txtMergeCode.TextAlign = HorizontalAlignment.Center;
                txtMergeCode.CharacterCasing = CharacterCasing.Upper;
                card9b.Controls.Add(txtMergeCode);

                var lblMergeStatus = new Label
                {
                    Text = "",
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = successColor,
                    Location = new Point(c9bx, c9by + 50),
                    Size = new Size(cardW - 36, 36),
                    Visible = false
                };
                card9b.Controls.Add(lblMergeStatus);

                var btnUnite = MakeActionButton("\U0001f91d Unite Teams", Color.FromArgb(139, 92, 246), Color.White, 150, c9by + 14, 140);
                btnUnite.Size = new Size(140, 34);
                btnUnite.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                btnUnite.Click += async (s, e) =>
                {
                    string otherCode = txtMergeCode.Text.Trim().ToUpper();
                    if (otherCode.Length != 6)
                    {
                        lblMergeStatus.Text = "\u274c Enter the other team's 6-character code";
                        lblMergeStatus.ForeColor = dangerColor;
                        lblMergeStatus.Visible = true;
                        return;
                    }
                    if (otherCode == _team.JoinCode)
                    {
                        lblMergeStatus.Text = "\u274c That's your own team code";
                        lblMergeStatus.ForeColor = dangerColor;
                        lblMergeStatus.Visible = true;
                        return;
                    }

                    // Check if other team exists
                    var otherTeam = await UserStorage.FindTeamByJoinCodeAsync(otherCode);
                    if (otherTeam == null)
                    {
                        lblMergeStatus.Text = "\u274c Team not found. Check the code.";
                        lblMergeStatus.ForeColor = dangerColor;
                        lblMergeStatus.Visible = true;
                        return;
                    }

                    btnUnite.Enabled = false;

                    // Step 1: Post our merge request so the other team can see it
                    await UserStorage.SetMergeRequestAsync(_team.JoinCode, otherCode);

                    // Step 2: Check if the other team already requested merge with us
                    bool otherRequested = await UserStorage.CheckMergeRequestAsync(otherCode, _team.JoinCode);

                    if (!otherRequested)
                    {
                        // Other team hasn't pressed Unite yet — waiting
                        lblMergeStatus.Text = $"\u23f3 Waiting for \"{otherTeam.TeamName}\" admin to also press Unite with your code: {_team.JoinCode}";
                        lblMergeStatus.ForeColor = _isDarkMode ? Color.FromArgb(251, 191, 36) : Color.FromArgb(180, 130, 0);
                        lblMergeStatus.Visible = true;
                        btnUnite.Enabled = true;
                        return;
                    }

                    // Both teams agreed! Ask for confirmation
                    string mergeMsg = $"Both teams confirmed the merge!\n\n" +
                        $"YOUR TEAM: \"{_team.TeamName}\" ({_team.Members?.Count ?? 0} members)\n" +
                        $"OTHER TEAM: \"{otherTeam.TeamName}\" ({otherTeam.Members?.Count ?? 0} members)\n\n" +
                        $"This will permanently combine all users, stickers, logs,\n" +
                        $"and chat into \"{_team.TeamName}\".\n" +
                        $"\"{otherTeam.AdminName}\" will become a co-admin.\n\n" +
                        $"This action cannot be undone. Continue?";

                    if (MessageBox.Show(mergeMsg, "Confirm Team Merge",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    {
                        btnUnite.Enabled = true;
                        return;
                    }

                    lblMergeStatus.Text = "\U0001f504 Merging teams...";
                    lblMergeStatus.ForeColor = _isDarkMode ? Color.FromArgb(140, 150, 170) : Color.FromArgb(100, 110, 130);
                    lblMergeStatus.Visible = true;

                    // Perform the merge
                    var merged = await UserStorage.MergeTeamsAsync(_team.JoinCode, otherCode);

                    btnUnite.Enabled = true;

                    if (merged != null)
                    {
                        _team.Members = merged.Members;
                        _team.MembersMeta = merged.MembersMeta;
                        _team.AssistantAdmins = merged.AssistantAdmins;

                        lblMergeStatus.Text = $"\u2705 Teams merged! {merged.Members.Count} total members. Restart required.";
                        lblMergeStatus.ForeColor = successColor;
                        lblMergeStatus.Visible = true;

                        _dataChanged = true;
                        NeedsRestart = true;
                        txtMergeCode.Clear();

                        MessageBox.Show(
                            $"Teams merged successfully!\n\n" +
                            $"Total members: {merged.Members.Count}\n" +
                            $"Admin: {merged.AdminName}\n" +
                            $"Co-Admin: {otherTeam.AdminName}\n\n" +
                            "The app will restart to load the merged team.",
                            "Merge Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        lblMergeStatus.Text = "\u274c Merge failed. Check connection and try again.";
                        lblMergeStatus.ForeColor = dangerColor;
                        lblMergeStatus.Visible = true;
                    }
                };
                card9b.Controls.Add(btnUnite);

                yR += card9bH + 10;
            }

            // ══════════════════════════════════════════════════
            // CARD 10: CREATE A NEW TEAM — AVAILABLE TO ALL USERS
            // ══════════════════════════════════════════════════
            {
                int card10H = 390;
                var card10 = MakeCard(col2X, yR, cardW, card10H);
                mainPanel.Controls.Add(card10);
                int c10y = 12;
                int c10x = 16;

                card10.Controls.Add(MakeSectionIcon("\U0001f6e1", c10x, c10y));
                card10.Controls.Add(MakeSectionTitle("Create a New Team", c10x + 28, c10y));

                var lblCreateSub = new Label
                {
                    Text = "Start a fresh team where you are the admin",
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(90, 100, 120) : Color.FromArgb(150, 160, 175),
                    Location = new Point(c10x + 28 + 150, c10y + 4),
                    AutoSize = true
                };
                card10.Controls.Add(lblCreateSub);
                c10y += 30;

                // ── TEAM NAME INPUT ──
                card10.Controls.Add(MakeFieldLabel("TEAM NAME", c10x, c10y));
                var txtNewTeamName = MakeTextBox(c10x, c10y + 16, cardW - 36);
                card10.Controls.Add(txtNewTeamName);
                c10y += 50;

                // ── OPTIONAL FIREBASE URL FOR THIS NEW TEAM ──
                card10.Controls.Add(MakeFieldLabel("TEAM FIREBASE URL (OPTIONAL)", c10x, c10y));
                var txtNewTeamFirebase = MakeTextBox(c10x, c10y + 16, cardW - 36);
                txtNewTeamFirebase.Text = "https://your-project.firebasedatabase.app";
                txtNewTeamFirebase.ForeColor = dimColor;
                txtNewTeamFirebase.GotFocus += (s, ev) =>
                {
                    if (txtNewTeamFirebase.Text == "https://your-project.firebasedatabase.app")
                    {
                        txtNewTeamFirebase.Text = "";
                        txtNewTeamFirebase.ForeColor = fgColor;
                    }
                };
                txtNewTeamFirebase.LostFocus += (s, ev) =>
                {
                    if (string.IsNullOrWhiteSpace(txtNewTeamFirebase.Text))
                    {
                        txtNewTeamFirebase.Text = "https://your-project.firebasedatabase.app";
                        txtNewTeamFirebase.ForeColor = dimColor;
                    }
                };
                card10.Controls.Add(txtNewTeamFirebase);
                c10y += 50;

                // ── INVITE MEMBERS INPUT ──
                card10.Controls.Add(MakeFieldLabel("INVITE MEMBERS (OPTIONAL)", c10x, c10y));
                var txtInviteMembers = new TextBox
                {
                    Location = new Point(c10x, c10y + 16),
                    Size = new Size(cardW - 36, 64),
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = fieldBg,
                    ForeColor = fgColor,
                    Font = new Font("Segoe UI", 9)
                };
                card10.Controls.Add(txtInviteMembers);
                c10y += 84;

                // ── PASSWORD FIELD (for account verification) ──
                card10.Controls.Add(MakeFieldLabel("YOUR PASSWORD", c10x, c10y));
                var txtCreatePw = MakeTextBox(c10x, c10y + 16, cardW - 36);
                txtCreatePw.UseSystemPasswordChar = true;
                card10.Controls.Add(txtCreatePw);
                c10y += 50;

                // ── STATUS LABEL FOR FEEDBACK ──
                var lblCreateStatus = new Label
                {
                    Text = "",
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = dangerColor,
                    Location = new Point(c10x, c10y),
                    Size = new Size(cardW - 36, 18),
                    Visible = false
                };
                card10.Controls.Add(lblCreateStatus);
                c10y += 20;

                // ── CREATE TEAM BUTTON ──
                var btnCreateTeam = MakeActionButton("\U0001f6e1 Create Team", accentColor, Color.White, c10x, c10y, cardW - 36);
                btnCreateTeam.Size = new Size(cardW - 36, 36);
                btnCreateTeam.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                btnCreateTeam.Click += async (s, e) =>
                {
                    string newTeamName = txtNewTeamName.Text.Trim();
                    string password = txtCreatePw.Text;
                    string teamFirebaseUrl = txtNewTeamFirebase.Text.Trim();
                    if (teamFirebaseUrl == "https://your-project.firebasedatabase.app")
                        teamFirebaseUrl = "";

                    // Validate team name
                    if (string.IsNullOrWhiteSpace(newTeamName))
                    {
                        lblCreateStatus.Text = "\u274c Enter a team name";
                        lblCreateStatus.ForeColor = dangerColor;
                        lblCreateStatus.Visible = true;
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(teamFirebaseUrl) &&
                        !teamFirebaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        lblCreateStatus.Text = "\u274c Firebase URL must start with https://";
                        lblCreateStatus.ForeColor = dangerColor;
                        lblCreateStatus.Visible = true;
                        return;
                    }

                    // Verify password against current user
                    var currentUser = _allUsers.FirstOrDefault(u =>
                        u.Name.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase));
                    if (currentUser == null || !currentUser.VerifyPassword(password))
                    {
                        lblCreateStatus.Text = "\u274c Incorrect password";
                        lblCreateStatus.ForeColor = dangerColor;
                        lblCreateStatus.Visible = true;
                        return;
                    }

                    btnCreateTeam.Enabled = false;
                    lblCreateStatus.Text = "\U0001f504 Creating team...";
                    lblCreateStatus.ForeColor = _isDarkMode ? Color.FromArgb(140, 150, 170) : Color.FromArgb(100, 110, 130);
                    lblCreateStatus.Visible = true;

                    try
                    {
                        // Create a new team with current user as admin
                        var newTeam = new TeamInfo(newTeamName, _currentUserName);
                        newTeam.CustomFirebaseUrl = teamFirebaseUrl;

                        // Add invited members immediately (one per line, comma, or semicolon)
                        var invitedMembers = txtInviteMembers.Text
                            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(n => n.Trim())
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Where(n => !n.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        foreach (var member in invitedMembers)
                        {
                            if (!newTeam.Members.Any(m => m.Equals(member, StringComparison.OrdinalIgnoreCase)))
                                newTeam.Members.Add(member);
                        }

                        // Set active team to the new one for saving to correct subfolder
                        UserStorage.SetActiveTeamCode(newTeam.JoinCode);
                        UserStorage.ClearTeamLocalRuntimeCache(newTeam.JoinCode);

                        // Save to Firebase so others can find it
                        await UserStorage.SaveTeamToFirebaseAsync(newTeam);

                        // Save locally
                        UserStorage.SaveTeam(newTeam);

                        // Create user list — admin with verified password
                        var newTeamUsers = new List<UserInfo>();
                        string pwHash;
                        using (var sha = System.Security.Cryptography.SHA256.Create())
                        {
                            byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                            pwHash = Convert.ToBase64String(bytes);
                        }
                        var adminUser = new UserInfo(_currentUserName, pwHash, false, true, newTeam.JoinCode);
                        // Carry over existing profile settings
                        if (currentUser != null)
                        {
                            adminUser.Color = currentUser.Color;
                            adminUser.Title = currentUser.Title;
                            adminUser.Role = currentUser.Role;
                            adminUser.Country = currentUser.Country;
                            adminUser.WeeklyHourLimit = currentUser.WeeklyHourLimit;
                        }
                        newTeamUsers.Add(adminUser);
                        foreach (var member in invitedMembers)
                        {
                            bool isAdmin = member.Equals(newTeam.AdminName, StringComparison.OrdinalIgnoreCase);
                            if (!newTeamUsers.Any(u => u.Name.Equals(member, StringComparison.OrdinalIgnoreCase)))
                                newTeamUsers.Add(new UserInfo(member, isAdmin, newTeam.JoinCode));
                        }
                        UserStorage.SaveUsers(newTeamUsers);

                        // Register in teams index
                        UserStorage.AddTeamToIndex(newTeam.JoinCode, newTeam.TeamName, _currentUserName);

                        // Add team to global account
                        await UserStorage.AddTeamToAccountAsync(_currentUserName, newTeam.JoinCode);

                        // Sync profile metadata to MembersMeta
                        if (newTeam.MembersMeta == null)
                            newTeam.MembersMeta = new Dictionary<string, MemberMeta>();
                        newTeam.MembersMeta[_currentUserName] = new MemberMeta
                        {
                            Color = adminUser.Color,
                            Title = adminUser.Title,
                            Role = adminUser.Role,
                            Country = adminUser.Country,
                            WeeklyHourLimit = adminUser.WeeklyHourLimit
                        };
                        UserStorage.SaveTeam(newTeam);
                        await UserStorage.SaveTeamToFirebaseAsync(newTeam);

                        // Show success with join code
                        lblCreateStatus.Text = $"\u2705 Team created! Join code: {newTeam.JoinCode}";
                        lblCreateStatus.ForeColor = successColor;
                        lblCreateStatus.Visible = true;

                        // Copy join code to clipboard
                        Clipboard.SetText(newTeam.JoinCode);

                        txtNewTeamName.Clear();
                        txtNewTeamFirebase.Text = "https://your-project.firebasedatabase.app";
                        txtNewTeamFirebase.ForeColor = dimColor;
                        txtInviteMembers.Clear();
                        txtCreatePw.Clear();

                        _dataChanged = true;
                        NeedsRestart = true;

                        string syncModeText = string.IsNullOrWhiteSpace(teamFirebaseUrl)
                            ? "No Firebase sync link is set. Team will run in local/P2P mode until admin sets it in Team Settings > Database."
                            : "Team is linked to Firebase sync.";

                        MessageBox.Show(
                            $"Team \"{newTeamName}\" created successfully!\n\n" +
                            $"Join Code: {newTeam.JoinCode}\n" +
                            $"(copied to clipboard)\n\n" +
                            $"Invited members: {Math.Max(0, newTeam.Members.Count - 1)}\n\n" +
                            syncModeText + "\n\n" +
                            "Share this code with your team members.\n" +
                            "The app will restart to switch to your new team.",
                            "Team Created",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        lblCreateStatus.Text = "\u274c Error: " + ex.Message;
                        lblCreateStatus.ForeColor = dangerColor;
                        lblCreateStatus.Visible = true;

                        // Switch back to original team
                        if (_team != null && !string.IsNullOrEmpty(_team.JoinCode))
                            UserStorage.SetActiveTeamCode(_team.JoinCode);
                    }
                    finally
                    {
                        btnCreateTeam.Enabled = true;
                    }
                };
                card10.Controls.Add(btnCreateTeam);

                yR += card10H + 10;
            }

            // ══════════════════════════════════════════════════
            // CARD 11: CUSTOM THEME — COLOR PALETTE, FONTS, PRESETS
            // ══════════════════════════════════════════════════
            {
                int card11H = 620;
                var card11 = MakeCard(col2X, yR, cardW, card11H);
                mainPanel.Controls.Add(card11);
                int c11y = 12;
                int c11x = 16;

                card11.Controls.Add(MakeSectionIcon("\U0001f3a8", c11x, c11y));
                card11.Controls.Add(MakeSectionTitle("Custom Theme", c11x + 28, c11y));
                c11y += 30;

                // Load current custom theme (or default)
                var customTheme = CustomTheme.LoadActive() ?? CustomTheme.DarkDefault();

                // ── ENABLE / DISABLE TOGGLE ──
                var chkEnabled = new CheckBox
                {
                    Text = "Enable Custom Theme (overrides Dark/Light mode)",
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = _isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59),
                    Checked = customTheme.Enabled,
                    AutoSize = true,
                    Location = new Point(c11x, c11y),
                    BackColor = Color.Transparent
                };
                card11.Controls.Add(chkEnabled);
                c11y += 28;

                // ── PRESET SELECTOR ──
                card11.Controls.Add(MakeFieldLabel("PRESET", c11x, c11y));
                var cmbPreset = new ComboBox
                {
                    Location = new Point(c11x, c11y + 16),
                    Width = 200,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 9f),
                    BackColor = _isDarkMode ? Color.FromArgb(40, 46, 56) : Color.White,
                    ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.Black
                };
                // Populate presets
                var builtInPresets = CustomTheme.GetBuiltInPresets();
                var userPresets = CustomTheme.LoadPresets();
                foreach (var p in builtInPresets) cmbPreset.Items.Add(p.PresetName);
                if (userPresets.Count > 0)
                {
                    cmbPreset.Items.Add("── User Presets ──");
                    foreach (var p in userPresets) cmbPreset.Items.Add("\u2605 " + p.PresetName);
                }
                // Select current
                int selIdx = cmbPreset.Items.IndexOf(customTheme.PresetName);
                if (selIdx < 0) selIdx = cmbPreset.Items.IndexOf("\u2605 " + customTheme.PresetName);
                if (selIdx >= 0) cmbPreset.SelectedIndex = selIdx;

                card11.Controls.Add(cmbPreset);

                // Save preset button
                var btnSavePreset = MakeActionButton("\U0001f4be Save As...", Color.FromArgb(59, 130, 246), Color.White,
                    c11x + 210, c11y + 14, 100);
                card11.Controls.Add(btnSavePreset);

                // Delete preset button
                var btnDeletePreset = MakeActionButton("\U0001f5d1 Delete", Color.FromArgb(120, 130, 145), Color.White,
                    c11x + 316, c11y + 14, 70);
                card11.Controls.Add(btnDeletePreset);
                c11y += 50;

                // ── COLOR PICKERS — Two columns of labeled color buttons ──
                // Each color button opens a ColorDialog and updates the theme + preview

                // Helper: create a color row with label + color button + hex display
                Action<string, string, Func<string>, Action<string>> addColorRow = null;
                var colorButtons = new List<Tuple<Panel, Label, Func<string>>>();

                addColorRow = (label, hex, getter, setter) =>
                {
                    // Use two columns: left half (0-180) and right half (200-380)
                    int colIdx = colorButtons.Count % 2;
                    int rowIdx = colorButtons.Count / 2;
                    int colX = c11x + colIdx * 195;
                    int ry = c11y + rowIdx * 34;

                    var lbl = new Label
                    {
                        Text = label,
                        Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                        ForeColor = _isDarkMode ? Color.FromArgb(160, 170, 185) : Color.FromArgb(80, 90, 110),
                        Location = new Point(colX, ry),
                        AutoSize = true
                    };
                    card11.Controls.Add(lbl);

                    Color parsed = Color.White;
                    try { parsed = ColorTranslator.FromHtml(getter()); } catch { }

                    var pnlColor = new Panel
                    {
                        Size = new Size(24, 16),
                        Location = new Point(colX + 100, ry),
                        BackColor = parsed,
                        Cursor = Cursors.Hand,
                        BorderStyle = BorderStyle.FixedSingle
                    };

                    var lblHex = new Label
                    {
                        Text = getter(),
                        Font = new Font("Consolas", 7.5f),
                        ForeColor = _isDarkMode ? Color.FromArgb(140, 150, 165) : Color.FromArgb(100, 110, 125),
                        Location = new Point(colX + 128, ry + 1),
                        AutoSize = true
                    };

                    pnlColor.Click += (s, e) =>
                    {
                        using (var cd = new ColorDialog { Color = pnlColor.BackColor, FullOpen = true })
                        {
                            if (cd.ShowDialog() == DialogResult.OK)
                            {
                                string newHex = CustomTheme.ColorToHex(cd.Color);
                                setter(newHex);
                                pnlColor.BackColor = cd.Color;
                                lblHex.Text = newHex;
                            }
                        }
                    };

                    card11.Controls.Add(pnlColor);
                    card11.Controls.Add(lblHex);
                    colorButtons.Add(Tuple.Create(pnlColor, lblHex, getter));
                };

                card11.Controls.Add(MakeFieldLabel("PALETTE COLORS", c11x, c11y));
                c11y += 18;

                // Add all color rows
                addColorRow("Background", customTheme.BackgroundColor,
                    () => customTheme.BackgroundColor, v => customTheme.BackgroundColor = v);
                addColorRow("Cards/Panels", customTheme.CardColor,
                    () => customTheme.CardColor, v => customTheme.CardColor = v);
                addColorRow("Input Fields", customTheme.InputColor,
                    () => customTheme.InputColor, v => customTheme.InputColor = v);
                addColorRow("Sidebar", customTheme.SidebarColor,
                    () => customTheme.SidebarColor, v => customTheme.SidebarColor = v);
                addColorRow("Text", customTheme.TextColor,
                    () => customTheme.TextColor, v => customTheme.TextColor = v);
                addColorRow("Secondary Text", customTheme.SecondaryTextColor,
                    () => customTheme.SecondaryTextColor, v => customTheme.SecondaryTextColor = v);
                addColorRow("Accent", customTheme.AccentColor,
                    () => customTheme.AccentColor, v => customTheme.AccentColor = v);
                addColorRow("Buttons Text", customTheme.ButtonTextColor,
                    () => customTheme.ButtonTextColor, v => customTheme.ButtonTextColor = v);
                addColorRow("Start Button", customTheme.StartColor,
                    () => customTheme.StartColor, v => customTheme.StartColor = v);
                addColorRow("Stop Button", customTheme.StopColor,
                    () => customTheme.StopColor, v => customTheme.StopColor = v);
                addColorRow("Chat Cards", customTheme.ChatCardColor,
                    () => customTheme.ChatCardColor, v => customTheme.ChatCardColor = v);
                addColorRow("Grid Lines", customTheme.GridLineColor,
                    () => customTheme.GridLineColor, v => customTheme.GridLineColor = v);
                addColorRow("Selection", customTheme.SelectionColor,
                    () => customTheme.SelectionColor, v => customTheme.SelectionColor = v);

                // Position after all color rows (13 items = 7 rows)
                c11y += 7 * 34 + 10;

                // ── FONT SETTINGS ──
                card11.Controls.Add(MakeFieldLabel("FONT", c11x, c11y));
                c11y += 16;

                var cmbFont = new ComboBox
                {
                    Location = new Point(c11x, c11y),
                    Width = 160,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 9f),
                    BackColor = _isDarkMode ? Color.FromArgb(40, 46, 56) : Color.White,
                    ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.Black
                };
                string[] fonts = { "Segoe UI", "Consolas", "Arial", "Verdana", "Tahoma",
                    "Calibri", "Courier New", "Lucida Console", "Trebuchet MS", "Georgia" };
                foreach (var f in fonts) cmbFont.Items.Add(f);
                int fontIdx = Array.IndexOf(fonts, customTheme.FontFamily);
                cmbFont.SelectedIndex = fontIdx >= 0 ? fontIdx : 0;
                card11.Controls.Add(cmbFont);

                // Font size
                var lblSize = new Label
                {
                    Text = "Size:",
                    Font = new Font("Segoe UI", 8f),
                    ForeColor = _isDarkMode ? Color.FromArgb(160, 170, 185) : Color.FromArgb(80, 90, 110),
                    Location = new Point(c11x + 170, c11y + 3),
                    AutoSize = true
                };
                card11.Controls.Add(lblSize);

                var nudFontSize = new NumericUpDown
                {
                    Location = new Point(c11x + 200, c11y),
                    Width = 60,
                    Minimum = 7,
                    Maximum = 16,
                    DecimalPlaces = 1,
                    Increment = 0.5m,
                    Value = (decimal)customTheme.FontSize,
                    Font = new Font("Segoe UI", 9f),
                    BackColor = _isDarkMode ? Color.FromArgb(40, 46, 56) : Color.White,
                    ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.Black
                };
                card11.Controls.Add(nudFontSize);

                // Bold / Italic checkboxes
                var chkBold = new CheckBox
                {
                    Text = "Bold",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.FromArgb(30, 41, 59),
                    Checked = customTheme.FontBold,
                    AutoSize = true,
                    Location = new Point(c11x + 270, c11y),
                    BackColor = Color.Transparent
                };
                card11.Controls.Add(chkBold);

                var chkItalic = new CheckBox
                {
                    Text = "Italic",
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                    ForeColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.FromArgb(30, 41, 59),
                    Checked = customTheme.FontItalic,
                    AutoSize = true,
                    Location = new Point(c11x + 330, c11y),
                    BackColor = Color.Transparent
                };
                card11.Controls.Add(chkItalic);
                c11y += 36;

                // ── APPLY BUTTON ──
                var btnApply = MakeActionButton("\u2705 Apply Theme", Color.FromArgb(34, 197, 94), Color.White,
                    c11x, c11y, 140);
                btnApply.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                btnApply.Click += (s, e) =>
                {
                    // Gather font settings
                    customTheme.FontFamily = cmbFont.SelectedItem?.ToString() ?? "Segoe UI";
                    customTheme.FontSize = (float)nudFontSize.Value;
                    customTheme.FontBold = chkBold.Checked;
                    customTheme.FontItalic = chkItalic.Checked;
                    customTheme.Enabled = chkEnabled.Checked;

                    // Save and fire event
                    customTheme.SaveActive();
                    ApplyThemePreview(customTheme.Enabled ? customTheme : null);
                    CustomThemeChanged?.Invoke(customTheme.Enabled ? customTheme : null);
                    _dataChanged = true;
                    ShowStatus(customTheme.Enabled
                        ? $"\u2705 Custom theme \"{customTheme.PresetName}\" applied!"
                        : "\u2705 Custom theme disabled — using dark/light mode", false);
                };
                card11.Controls.Add(btnApply);

                // ── RESET BUTTON ──
                var btnReset = MakeActionButton("\U0001f504 Reset to Default", Color.FromArgb(100, 116, 139), Color.White,
                    c11x + 150, c11y, 150);
                btnReset.Click += (s, e) =>
                {
                    var def = _isDarkMode ? CustomTheme.DarkDefault() : CustomTheme.LightDefault();
                    // Copy all colors back
                    customTheme.BackgroundColor = def.BackgroundColor;
                    customTheme.CardColor = def.CardColor;
                    customTheme.InputColor = def.InputColor;
                    customTheme.TextColor = def.TextColor;
                    customTheme.SecondaryTextColor = def.SecondaryTextColor;
                    customTheme.AccentColor = def.AccentColor;
                    customTheme.StartColor = def.StartColor;
                    customTheme.StopColor = def.StopColor;
                    customTheme.ButtonTextColor = def.ButtonTextColor;
                    customTheme.SidebarColor = def.SidebarColor;
                    customTheme.ChatCardColor = def.ChatCardColor;
                    customTheme.GridLineColor = def.GridLineColor;
                    customTheme.SelectionColor = def.SelectionColor;
                    customTheme.FontFamily = def.FontFamily;
                    customTheme.FontSize = def.FontSize;
                    customTheme.FontBold = def.FontBold;
                    customTheme.FontItalic = def.FontItalic;
                    customTheme.PresetName = def.PresetName;

                    // Update UI — refresh color buttons
                    foreach (var tuple in colorButtons)
                    {
                        try
                        {
                            tuple.Item1.BackColor = ColorTranslator.FromHtml(tuple.Item3());
                            tuple.Item2.Text = tuple.Item3();
                        }
                        catch { }
                    }
                    cmbFont.SelectedItem = customTheme.FontFamily;
                    nudFontSize.Value = (decimal)customTheme.FontSize;
                    chkBold.Checked = customTheme.FontBold;
                    chkItalic.Checked = customTheme.FontItalic;

                    ShowStatus("\U0001f504 Theme reset to default", false);
                };
                card11.Controls.Add(btnReset);
                c11y += 40;

                // ── PRESET HANDLERS ──
                cmbPreset.SelectedIndexChanged += (s, e) =>
                {
                    string selected = cmbPreset.SelectedItem?.ToString() ?? "";
                    if (selected.StartsWith("──")) return; // Separator

                    CustomTheme preset = null;
                    // Check built-in presets
                    foreach (var p in builtInPresets)
                    {
                        if (p.PresetName == selected) { preset = p; break; }
                    }
                    // Check user presets (prefixed with ★)
                    if (preset == null && selected.StartsWith("\u2605 "))
                    {
                        string name = selected.Substring(2);
                        foreach (var p in userPresets)
                        {
                            if (p.PresetName == name) { preset = p; break; }
                        }
                    }

                    if (preset == null) return;

                    // Apply preset colors to customTheme
                    customTheme.BackgroundColor = preset.BackgroundColor;
                    customTheme.CardColor = preset.CardColor;
                    customTheme.InputColor = preset.InputColor;
                    customTheme.TextColor = preset.TextColor;
                    customTheme.SecondaryTextColor = preset.SecondaryTextColor;
                    customTheme.AccentColor = preset.AccentColor;
                    customTheme.StartColor = preset.StartColor;
                    customTheme.StopColor = preset.StopColor;
                    customTheme.ButtonTextColor = preset.ButtonTextColor;
                    customTheme.SidebarColor = preset.SidebarColor;
                    customTheme.ChatCardColor = preset.ChatCardColor;
                    customTheme.GridLineColor = preset.GridLineColor;
                    customTheme.SelectionColor = preset.SelectionColor;
                    customTheme.FontFamily = preset.FontFamily;
                    customTheme.FontSize = preset.FontSize;
                    customTheme.FontBold = preset.FontBold;
                    customTheme.FontItalic = preset.FontItalic;
                    customTheme.PresetName = preset.PresetName;

                    // Update all color buttons
                    foreach (var tuple in colorButtons)
                    {
                        try
                        {
                            tuple.Item1.BackColor = ColorTranslator.FromHtml(tuple.Item3());
                            tuple.Item2.Text = tuple.Item3();
                        }
                        catch { }
                    }
                    cmbFont.SelectedItem = customTheme.FontFamily;
                    nudFontSize.Value = Math.Min(16, Math.Max(7, (decimal)customTheme.FontSize));
                    chkBold.Checked = customTheme.FontBold;
                    chkItalic.Checked = customTheme.FontItalic;

                    ShowStatus($"\U0001f3a8 Preset \"{customTheme.PresetName}\" loaded — click Apply to activate", false);
                };

                btnSavePreset.Click += (s, e) =>
                {
                    string name = ShowInputDialog("Save Theme Preset", "Enter a name for this preset:", customTheme.PresetName);
                    if (string.IsNullOrWhiteSpace(name)) return;

                    customTheme.FontFamily = cmbFont.SelectedItem?.ToString() ?? "Segoe UI";
                    customTheme.FontSize = (float)nudFontSize.Value;
                    customTheme.FontBold = chkBold.Checked;
                    customTheme.FontItalic = chkItalic.Checked;
                    customTheme.SavePreset(name);

                    // Add to combo if not already there
                    string displayName = "\u2605 " + name;
                    if (!cmbPreset.Items.Contains(displayName))
                    {
                        if (!cmbPreset.Items.Contains("── User Presets ──"))
                            cmbPreset.Items.Add("── User Presets ──");
                        cmbPreset.Items.Add(displayName);
                    }
                    cmbPreset.SelectedItem = displayName;
                    ShowStatus($"\U0001f4be Theme preset \"{name}\" saved!", false);
                };

                btnDeletePreset.Click += (s, e) =>
                {
                    string selected = cmbPreset.SelectedItem?.ToString() ?? "";
                    if (!selected.StartsWith("\u2605 "))
                    { ShowStatus("\u26a0 Can only delete user-created presets", true); return; }

                    string name = selected.Substring(2);
                    var confirm = MessageBox.Show($"Delete preset \"{name}\"?", "Delete Preset",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes) return;

                    CustomTheme.DeletePreset(name);
                    cmbPreset.Items.Remove(selected);
                    ShowStatus($"\U0001f5d1 Preset \"{name}\" deleted", false);
                };

                yR += card11H + 10;
            }

            // ══════════════════════════════════════════════════
            // Bottom bar: Status + Save / Close
            // ══════════════════════════════════════════════════
            // Status label appears temporarily when actions are taken (success/error messages).
            // Admin sees "Save All Changes" + Close buttons.
            // Non-admin sees only Close button (read-only view).
            y = Math.Max(y, yR) + 4;

            lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = successColor,
                Location = new Point(0, y),
                Size = new Size(400, 18),
                Visible = false
            };
            mainPanel.Controls.Add(lblStatus);
            y += 24;

            if (_isAdmin)
            {
                var btnSave = MakeActionButton("\U0001f4be  Save All Changes", accentColor, Color.White, 0, y, 210);
                btnSave.Size = new Size(210, 44);
                btnSave.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                btnSave.Click += OnSaveAll;
                mainPanel.Controls.Add(btnSave);

                var btnClose = MakeActionButton("Close", _isDarkMode ? Color.FromArgb(50, 58, 72) : Color.FromArgb(210, 215, 225),
                    dimColor, 220, y, 100);
                btnClose.Size = new Size(100, 44);
                btnClose.Click += (s, e) => this.Close();
                mainPanel.Controls.Add(btnClose);
            }
            else
            {
                var btnClose = MakeActionButton("Close", accentColor, Color.White, 0, y, 120);
                btnClose.Size = new Size(120, 44);
                btnClose.Click += (s, e) => this.Close();
                mainPanel.Controls.Add(btnClose);
            }
        }

        // ═══ MEMBER MANAGEMENT — ADD, REMOVE, EDIT, RESET PASSWORD ═══
        /// <summary>Populates the member ListView from _team.Members and _allUsers.</summary>
        /// <summary>Populates the member list with team members and their metadata.</summary>
        private void PopulateMemberList()
        {
//             DebugLogger.Log($"[TeamOptions] Populating member list with {_team.Members.Count} members");
            listMembers.Items.Clear();
            foreach (var name in _team.Members)
            {
                var user = _allUsers.FirstOrDefault(u => u.Name == name);
                var meta = (_team.MembersMeta != null && _team.MembersMeta.ContainsKey(name))
                    ? _team.MembersMeta[name]
                    : null;

                string role = meta?.Role ?? user?.Role ?? "";
                string title = meta?.Title ?? user?.Title ?? "";
                string color = meta?.Color ?? user?.Color ?? "";
                bool isAdmin = name.Equals(_team.AdminName, StringComparison.OrdinalIgnoreCase);
                string pwStatus = (user != null && user.IsDefaultPassword) ? "Default PW" : "Custom PW";
                bool isMuted = _team.MutedMembers != null && _team.MutedMembers.Contains(name);
                bool isAsstAdmin = _team.AssistantAdmins != null && _team.AssistantAdmins.Contains(name);

                // Create row with columns: Name, Role, Title, Color, Admin, Muted, Asst⭐, Status
                var item = new ListViewItem(new string[]
                {
                    name,
                    role,
                    title,
                    color,
                    isAdmin ? "\u2605" : "",
                    isMuted ? "Yes" : "",
                    isAsstAdmin ? "\u2605" : "",
                    pwStatus
                });

                // Color the row with user's color if set
                if (!string.IsNullOrEmpty(color) && color.StartsWith("#"))
                {
                    try
                    {
                        item.ForeColor = ColorTranslator.FromHtml(color);
                    }
                    catch { }
                }

                listMembers.Items.Add(item);
            }
        }

        /// <summary>Opens MemberEditDialog for adding a new team member.</summary>
        /// <summary>
        /// Opens an invite dialog where admin can paste an email address.
        /// Sends an invite email with the app download link and join code.
        /// Users now join by themselves using the join code — no manual adding.
        /// </summary>
        private void OnInviteMember(object sender, EventArgs e)
        {
            using (var dlg = new InviteEmailDialog(_team.TeamName, _team.JoinCode, _isDarkMode))
            {
                dlg.ShowDialog(this);
            }
        }

        /// <summary>Removes selected member from team (cannot remove admin).</summary>
        private void OnRemoveMember(object sender, EventArgs e)
        {
//             DebugLogger.Log("[TeamOptions] OnRemoveMember called");
            if (listMembers.SelectedItems.Count == 0)
            {
                DebugLogger.Log("[TeamOptions] Remove member failed: no member selected");
                ShowStatus("\u274c Select a member first", true);
                return;
            }

            string name = listMembers.SelectedItems[0].Text;
//             DebugLogger.Log($"[TeamOptions] Remove member requested: {name}");
            // Permission check: prevent removing the admin
            if (name.Equals(_team.AdminName, StringComparison.OrdinalIgnoreCase))
            {
                DebugLogger.Log("[TeamOptions] Remove member failed: cannot remove admin");
                ShowStatus("\u274c Cannot remove the admin", true);
                return;
            }

            if (MessageBox.Show($"Remove {name} from the team?\n\nThis cannot be undone.",
                "Remove Member", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
//                 DebugLogger.Log($"[TeamOptions] Removing member: {name}");
                _team.Members.Remove(name);
                if (_team.MembersMeta != null)
                    _team.MembersMeta.Remove(name);
                _allUsers.RemoveAll(u => u.Name == name);

                _dataChanged = true;
                PopulateMemberList();
                ShowStatus("\u2713 Removed: " + name);
            }
        }

        /// <summary>Opens MemberEditDialog to edit color, title, role for selected member.</summary>
        private void OnEditMember(object sender, EventArgs e)
        {
//             DebugLogger.Log("[TeamOptions] OnEditMember called");
            if (listMembers.SelectedItems.Count == 0)
            {
                DebugLogger.Log("[TeamOptions] Edit member failed: no member selected");
                ShowStatus("\u274c Select a member first", true);
                return;
            }

            string name = listMembers.SelectedItems[0].Text;
//             DebugLogger.Log($"[TeamOptions] Editing member: {name}");
            var meta = (_team.MembersMeta != null && _team.MembersMeta.ContainsKey(name))
                ? _team.MembersMeta[name]
                : new MemberMeta();

            // Dialog opened in edit mode (nameEditable=false)
            using (var dlg = new MemberEditDialog("Edit Member", name, meta.Color, meta.Title, meta.Role, _isDarkMode, false))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
//                     DebugLogger.Log($"[TeamOptions] Updating member metadata: {name}");
                    if (_team.MembersMeta == null)
                        _team.MembersMeta = new Dictionary<string, MemberMeta>();

                    // Preserve existing Country & WeeklyHourLimit when editing color/title/role
                    var updatedMeta = new MemberMeta(dlg.MemberColor, dlg.MemberTitle, dlg.MemberRole);
                    if (_team.MembersMeta.ContainsKey(name))
                    {
                        updatedMeta.Country = _team.MembersMeta[name].Country;
                        updatedMeta.WeeklyHourLimit = _team.MembersMeta[name].WeeklyHourLimit;
                        updatedMeta.AvatarBase64 = _team.MembersMeta[name].AvatarBase64;
                    }
                    _team.MembersMeta[name] = updatedMeta;

                    // Also update UserInfo in _allUsers
                    var user = _allUsers.FirstOrDefault(u => u.Name == name);
                    if (user != null)
                    {
                        user.Color = dlg.MemberColor;
                        user.Title = dlg.MemberTitle;
                        user.Role = dlg.MemberRole;
                    }

                    _dataChanged = true;
                    PopulateMemberList();
                    ShowStatus("\u2713 Updated: " + name);
                }
            }
        }

        /// <summary>Resets selected member's password to default (111111).</summary>
        private void OnResetPassword(object sender, EventArgs e)
        {
//             DebugLogger.Log("[TeamOptions] OnResetPassword called");
            if (listMembers.SelectedItems.Count == 0)
            {
                DebugLogger.Log("[TeamOptions] Reset password failed: no member selected");
                ShowStatus("\u274c Select a member first", true);
                return;
            }

            string name = listMembers.SelectedItems[0].Text;
//             DebugLogger.Log($"[TeamOptions] Reset password requested for: {name}");
            if (MessageBox.Show($"Reset password for {name} to default (111111)?",
                "Reset Password", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var user = _allUsers.FirstOrDefault(u => u.Name == name);
                if (user != null)
                {
//                     DebugLogger.Log($"[TeamOptions] Resetting password for {name}");
                    user.SetPassword("111111");
                    user.IsDefaultPassword = true;
                    _dataChanged = true;
                    PopulateMemberList();
                    ShowStatus("\u2713 Password reset for: " + name);
                }
            }
        }

        // ═══ MODERATION — MUTE, KICK, AND ASSISTANT ADMIN CONTROLS ═══
        /// <summary>Toggles mute status for selected member. Muted users cannot post in chat.</summary>
        private void OnMuteUser(object sender, EventArgs e)
        {
//             DebugLogger.Log("[TeamOptions] OnMuteUser called");
            if (listMembers.SelectedItems.Count == 0)
            {
                DebugLogger.Log("[TeamOptions] Mute user failed: no member selected");
                ShowStatus("\u274c Select a member first", true);
                return;
            }

            string name = listMembers.SelectedItems[0].Text;
//             DebugLogger.Log($"[TeamOptions] Mute toggle requested for: {name}");
            // Permission check: cannot mute the admin
            if (name.Equals(_team.AdminName, StringComparison.OrdinalIgnoreCase))
            {
                DebugLogger.Log("[TeamOptions] Mute failed: cannot mute admin");
                ShowStatus("\u274c Cannot mute the admin", true);
                return;
            }

            // Initialize MutedMembers list if null
            if (_team.MutedMembers == null)
                _team.MutedMembers = new List<string>();

            bool isMuted = _team.MutedMembers.Contains(name);
            if (isMuted)
            {
                // Unmute: remove from list
//                 DebugLogger.Log($"[TeamOptions] Unmuting user: {name}");
                _team.MutedMembers.Remove(name);
                _dataChanged = true;
                PopulateMemberList();
                ShowStatus("\u2713 Unmuted: " + name);
            }
            else
            {
                // Mute: add to list — prevents posting in chat
//                 DebugLogger.Log($"[TeamOptions] Muting user: {name}");
                _team.MutedMembers.Add(name);
                _dataChanged = true;
                PopulateMemberList();
                ShowStatus("\u2713 Muted: " + name);
            }
        }

        /// <summary>Kicks (removes) selected member from team and deletes their Firebase data.</summary>
        private async void OnKickUser(object sender, EventArgs e)
        {
//             DebugLogger.Log("[TeamOptions] OnKickUser called");
            if (listMembers.SelectedItems.Count == 0)
            {
                DebugLogger.Log("[TeamOptions] Kick user failed: no member selected");
                ShowStatus("\u274c Select a member first", true);
                return;
            }

            string name = listMembers.SelectedItems[0].Text;
//             DebugLogger.Log($"[TeamOptions] Kick requested for: {name}");
            // Permission check: cannot kick the admin
            if (name.Equals(_team.AdminName, StringComparison.OrdinalIgnoreCase))
            {
                DebugLogger.Log("[TeamOptions] Kick failed: cannot kick admin");
                ShowStatus("\u274c Cannot kick the admin", true);
                return;
            }

            if (MessageBox.Show($"Kick {name} from the team?\n\nThis will remove all their data (messages, status, etc.) and cannot be undone.",
                "Kick Member", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
//                 DebugLogger.Log($"[TeamOptions] Kicking user: {name}");
                ShowStatus("Kicking " + name + "...");

                // Delete Firebase data: online status and chat messages
                // Firebase sync: DELETE /teams/{joinCode}/online_status/{name}.json
                //               DELETE /teams/{joinCode}/chat_messages/* (filtered by user)
                try
                {
                    string statusUrl = _firebaseBaseUrl + "/online_status/" + name + ".json";
//                     DebugLogger.Log($"[TeamOptions] Deleting Firebase status for {name}");
                    await _http.DeleteAsync(statusUrl);

                    string messagesUrl = _firebaseBaseUrl + "/chat_messages.json";
                    // Note: Full message deletion would require more sophisticated filtering in Firebase
                    // For now, mark this as future enhancement
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[TeamOptions] Firebase deletion failed (continuing): {ex.Message}");
                    // Continue even if Firebase deletion fails (user removed locally regardless)
                }

                // Remove from team locally
//                 DebugLogger.Log($"[TeamOptions] Removing {name} from team locally");
                _team.Members.Remove(name);
                if (_team.MembersMeta != null)
                    _team.MembersMeta.Remove(name);
                if (_team.MutedMembers != null)
                    _team.MutedMembers.Remove(name);
                if (_team.AssistantAdmins != null)
                    _team.AssistantAdmins.Remove(name);
                _allUsers.RemoveAll(u => u.Name == name);

                _dataChanged = true;
                PopulateMemberList();
                ShowStatus("\u2713 Kicked: " + name);
            }
        }

        /// <summary>Unban a previously banned user so they can rejoin the team.</summary>
        private void OnUnbanUser(object sender, EventArgs e)
        {
            // SHOW LIST OF BANNED USERS TO PICK FROM
            if (_team.BannedMembers == null || _team.BannedMembers.Count == 0)
            {
                ShowStatus("No banned users to unban", true);
                return;
            }

            // CREATE A SIMPLE SELECTION DIALOG
            var dlg = new Form
            {
                Text = "Unban User",
                Size = new Size(320, 280),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = _isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250)
            };

            dlg.Controls.Add(new Label
            {
                Text = "Select a banned user to unban:",
                Font = new Font("Segoe UI", 10),
                ForeColor = _isDarkMode ? Color.FromArgb(220, 224, 230) : Color.Black,
                Location = new Point(15, 10),
                AutoSize = true
            });

            var listBox = new ListBox
            {
                Location = new Point(15, 40),
                Size = new Size(275, 140),
                Font = new Font("Segoe UI", 10),
                BackColor = _isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White,
                ForeColor = _isDarkMode ? Color.FromArgb(220, 224, 230) : Color.Black
            };
            foreach (string banned in _team.BannedMembers)
                listBox.Items.Add(banned);
            dlg.Controls.Add(listBox);

            var btnUnban = new Button
            {
                Text = "\u2705 Unban Selected",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Location = new Point(15, 190),
                Size = new Size(275, 40),
                DialogResult = DialogResult.OK
            };
            btnUnban.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnUnban);
            dlg.AcceptButton = btnUnban;

            if (dlg.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
            {
                string name = listBox.SelectedItem.ToString();
                _team.BannedMembers.Remove(name);
                _dataChanged = true;
                ShowStatus($"\u2705 {name} has been unbanned. They can rejoin now.");
            }
        }

        /// <summary>Toggles assistant admin status for selected member. Asst admins have full admin powers.</summary>
        private void OnToggleAssistantAdmin(object sender, EventArgs e)
        {
            if (listMembers.SelectedItems.Count == 0)
            { ShowStatus("\u274c Select a member first", true); return; }

            string name = listMembers.SelectedItems[0].Text;
            // Permission check: admin is always admin
            if (name.Equals(_team.AdminName, StringComparison.OrdinalIgnoreCase))
            { ShowStatus("\u274c The admin is already an admin", true); return; }

            // Initialize AssistantAdmins list if null
            if (_team.AssistantAdmins == null)
                _team.AssistantAdmins = new List<string>();

            bool isAsstAdmin = _team.AssistantAdmins.Contains(name);
            if (isAsstAdmin)
            {
                // Demote: remove from assistant admins
                _team.AssistantAdmins.Remove(name);
                _dataChanged = true;
                PopulateMemberList();
                ShowStatus("\u2713 Demoted: " + name);
            }
            else
            {
                // Promote: add to assistant admins — full admin permissions
                _team.AssistantAdmins.Add(name);
                _dataChanged = true;
                PopulateMemberList();
                ShowStatus("\u2713 Promoted to Assistant Admin: " + name);
            }
        }

        // ═══ SAVE — PERSISTS ALL CHANGES TO LOCAL STORAGE AND FIREBASE ═══
        /// <summary>Saves all changes to local storage and syncs to Firebase.</summary>
        private async void OnSaveAll(object sender, EventArgs e)
        {
            // Update team name from textbox
            _team.TeamName = txtTeamName.Text.Trim();

            // Update Firebase URL (ignore placeholder text)
            string fbUrl = txtFirebaseUrl.Text.Trim();
            if (fbUrl == "https://your-project.firebasedatabase.app")
                fbUrl = "";
            if (!string.IsNullOrEmpty(fbUrl) && !fbUrl.StartsWith("https://"))
            {
                ShowStatus("\u274c Firebase URL must start with https://", true);
                return;
            }
            _team.CustomFirebaseUrl = fbUrl;

            if (_isAdmin)
            {
                string aiEndpoint = txtTeamAiEndpoint?.Text?.Trim() ?? "";
                string aiModel = txtTeamAiModel?.Text?.Trim() ?? "";
                string aiKey = txtTeamAiApiKey?.Text?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(aiEndpoint))
                    aiEndpoint = "https://openrouter.ai/api/v1/chat/completions";
                if (!aiEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    ShowStatus("\u274c Team AI endpoint must start with https://", true);
                    return;
                }

                if (string.IsNullOrWhiteSpace(aiModel))
                    aiModel = "openai/gpt-4o-mini";

                _team.TeamAiEndpoint = aiEndpoint;
                _team.TeamAiModel = aiModel;
                _team.TeamAiApiKey = aiKey;
            }

            ShowStatus("Saving...");

            // Firebase sync flow:
            // 1. Save locally to UserStorage (local JSON files)
            // 2. Save to Firebase: PUT /teams/{joinCode}.json with full team data
            // 3. Save users: PUT /users/{userId}.json for each modified user
            UserStorage.SaveTeam(_team);
            UserStorage.SaveUsers(_allUsers);

            await UserStorage.SaveTeamToFirebaseAsync(_team);

            _dataChanged = true;
            ShowStatus("\u2713 All changes saved!");
        }

        // ═══ WIKI MANAGEMENT — ADD, EDIT, DELETE WIKI ENTRIES ═══
        /// <summary>Loads wiki entries from Firebase /helper endpoint and populates list.</summary>
        private async Task LoadWikiEntriesAsync()
        {
            try
            {
                // Load entries from Firebase: GET /teams/{joinCode}/helper.json
                string url = _firebaseBaseUrl + "/helper.json";
                var response = await _http.GetAsync(url + "?_cb=" + DateTime.UtcNow.Ticks);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(json) && json != "null")
                    {
                        var dict = JsonConvert.DeserializeObject<Dictionary<string, HelperEntry>>(json);
                        if (dict != null)
                        {
                            _wikiEntries = dict.Select(kv =>
                            {
                                kv.Value.Key = kv.Key;
                                return kv.Value;
                            }).ToList();
                        }
                    }
                    else
                    {
                        _wikiEntries.Clear();
                    }
                }

                // Load custom categories from Firebase: GET /teams/{joinCode}/helperCategories.json
                string catUrl = _firebaseBaseUrl + "/helperCategories.json";
                var catResp = await _http.GetAsync(catUrl + "?_cb=" + DateTime.UtcNow.Ticks);
                if (catResp.IsSuccessStatusCode)
                {
                    string catJson = await catResp.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(catJson) && catJson != "null")
                    {
                        var catDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(catJson);
                        _wikiCategories = catDict?.Values.ToList() ?? new List<string>();
                    }
                }

                PopulateWikiList();
            }
            catch { }
        }

        /// <summary>Populates the wiki ListView from loaded _wikiEntries.</summary>
        private void PopulateWikiList()
        {
            if (listWiki == null) return;
            listWiki.Items.Clear();

            var ordered = _wikiEntries
                .OrderBy(e => e.category)
                .ThenByDescending(e => e.createdAt)
                .ToList();

            foreach (var entry in ordered)
            {
                string urlOrDesc = !string.IsNullOrWhiteSpace(entry.url)
                    ? entry.url
                    : (entry.description?.Length > 50 ? entry.description.Substring(0, 50) + "..." : entry.description ?? "");

                string dateStr = "";
                if (DateTime.TryParse(entry.createdAt, out var dt))
                    dateStr = dt.ToLocalTime().ToString("dd MMM");

                var item = new ListViewItem(new string[]
                {
                    entry.title ?? "(no title)",
                    entry.category ?? "",
                    urlOrDesc,
                    entry.createdBy ?? "",
                    dateStr
                });
                item.Tag = entry;
                listWiki.Items.Add(item);
            }
        }

        /// <summary>Opens dialog to add a new wiki entry.</summary>
        private void OnAddWikiEntry(object sender, EventArgs e)
        {
            ShowWikiEntryDialog(null);
        }

        /// <summary>Opens dialog to edit selected wiki entry.</summary>
        private void OnEditWikiEntry(object sender, EventArgs e)
        {
            if (listWiki.SelectedItems.Count == 0)
            { ShowStatus("\u274c Select a wiki entry first", true); return; }

            var entry = listWiki.SelectedItems[0].Tag as HelperEntry;
            if (entry != null)
                ShowWikiEntryDialog(entry);
        }

        /// <summary>Deletes selected wiki entry from Firebase.</summary>
        private async void OnDeleteWikiEntry(object sender, EventArgs e)
        {
            if (listWiki.SelectedItems.Count == 0)
            { ShowStatus("\u274c Select a wiki entry first", true); return; }

            var entry = listWiki.SelectedItems[0].Tag as HelperEntry;
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key)) return;

            if (MessageBox.Show($"Delete wiki entry \"{entry.title}\"?",
                "Delete Wiki Entry", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    string url = _firebaseBaseUrl + "/helper/" + entry.Key + ".json";
                    await _http.DeleteAsync(url);
                    ShowStatus("\u2713 Deleted: " + entry.title);
                    await LoadWikiEntriesAsync();
                }
                catch
                {
                    ShowStatus("\u274c Failed to delete entry", true);
                }
            }
        }

        /// <summary>Shows wiki entry add/edit dialog with form fields and color picker.</summary>
        private void ShowWikiEntryDialog(HelperEntry existing)
        {
            bool isEdit = existing != null;
            var dlg = new Form
            {
                Text = isEdit ? "\u270f  Edit Wiki Entry" : "\u2795  Add Wiki Entry",
                Size = new Size(460, 480),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = _isDarkMode ? Color.FromArgb(30, 36, 46) : Color.FromArgb(245, 247, 250)
            };

            Color inputBg = _isDarkMode ? Color.FromArgb(44, 50, 64) : Color.White;
            Color fg = _isDarkMode ? Color.White : Color.FromArgb(30, 41, 59);
            Color lblFg = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.FromArgb(100, 116, 139);
            var font = new Font("Segoe UI", 9);
            int y = 15, x = 15, w = 410;

            // ── Category dropdown ──
            var lblCat = new Label { Text = "CATEGORY", Location = new Point(x, y), ForeColor = lblFg, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = true };
            dlg.Controls.Add(lblCat);
            y += 20;

            var cmbCat = new ComboBox
            {
                Location = new Point(x, y), Size = new Size(260, 24),
                DropDownStyle = ComboBoxStyle.DropDown, Font = font,
                BackColor = inputBg, ForeColor = fg
            };
            string[] defaultCats = { "Useful Links", "Sticky Notes", "Datasheets", "Project Links", "Project Plan", "Documentation" };
            foreach (var cat in defaultCats) cmbCat.Items.Add(cat);
            foreach (var cat in _wikiCategories)
            {
                if (!cmbCat.Items.Contains(cat)) cmbCat.Items.Add(cat);
            }
            cmbCat.Text = existing?.category ?? "Useful Links";
            dlg.Controls.Add(cmbCat);
            y += 34;

            // ── Title field ──
            var lblTitle = new Label { Text = "TITLE", Location = new Point(x, y), ForeColor = lblFg, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = true };
            dlg.Controls.Add(lblTitle);
            y += 20;

            var txtTitle = new TextBox
            {
                Location = new Point(x, y), Size = new Size(w, 26), Font = font,
                BackColor = inputBg, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle,
                Text = existing?.title ?? ""
            };
            dlg.Controls.Add(txtTitle);
            y += 34;

            // ── URL field (optional) ──
            var lblUrl = new Label { Text = "URL (optional — for links, datasheets, etc.)", Location = new Point(x, y), ForeColor = lblFg, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = true };
            dlg.Controls.Add(lblUrl);
            y += 20;

            var txtUrl = new TextBox
            {
                Location = new Point(x, y), Size = new Size(w, 26), Font = font,
                BackColor = inputBg, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle,
                Text = existing?.url ?? ""
            };
            dlg.Controls.Add(txtUrl);
            y += 34;

            // ── Description field ──
            var lblDesc = new Label { Text = "DESCRIPTION / NOTES", Location = new Point(x, y), ForeColor = lblFg, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = true };
            dlg.Controls.Add(lblDesc);
            y += 20;

            var txtDesc = new TextBox
            {
                Location = new Point(x, y), Size = new Size(w, 100), Font = font,
                BackColor = inputBg, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle,
                Multiline = true, ScrollBars = ScrollBars.Vertical,
                Text = existing?.description ?? ""
            };
            dlg.Controls.Add(txtDesc);
            y += 110;

            // ── Color picker (for sticky notes) ──
            var lblColorPick = new Label { Text = "NOTE COLOR (for sticky notes)", Location = new Point(x, y), ForeColor = lblFg, Font = new Font("Segoe UI", 8, FontStyle.Bold), AutoSize = true };
            dlg.Controls.Add(lblColorPick);
            y += 20;

            string[] colorNames = { "Yellow", "Blue", "Green", "Orange", "Purple", "Red", "White" };
            Color[] noteColors = {
                Color.FromArgb(255, 235, 59), Color.FromArgb(129, 212, 250),
                Color.FromArgb(165, 214, 167), Color.FromArgb(255, 171, 145),
                Color.FromArgb(206, 147, 216), Color.FromArgb(239, 83, 80),
                Color.FromArgb(255, 255, 255)
            };

            int selectedColorIdx = 0;
            if (existing != null)
            {
                for (int i = 0; i < colorNames.Length; i++)
                    if (colorNames[i] == existing.color) { selectedColorIdx = i; break; }
            }

            var colorButtons = new List<Button>();
            for (int i = 0; i < noteColors.Length; i++)
            {
                int idx = i;
                var btn = new Button
                {
                    Size = new Size(28, 28),
                    Location = new Point(x + i * 34, y),
                    BackColor = noteColors[i],
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Text = idx == selectedColorIdx ? "\u2713" : "",
                    Font = new Font("Segoe UI", 8, FontStyle.Bold),
                    ForeColor = Color.FromArgb(40, 40, 40)
                };
                btn.FlatAppearance.BorderSize = idx == selectedColorIdx ? 2 : 0;
                btn.FlatAppearance.BorderColor = Color.White;
                btn.Click += (s, ev) =>
                {
                    selectedColorIdx = idx;
                    foreach (var b in colorButtons) { b.Text = ""; b.FlatAppearance.BorderSize = 0; }
                    btn.Text = "\u2713";
                    btn.FlatAppearance.BorderSize = 2;
                };
                colorButtons.Add(btn);
                dlg.Controls.Add(btn);
            }
            y += 42;

            // ── Save / Cancel buttons ──
            var btnSave = new Button
            {
                Text = isEdit ? "\U0001f4be  Update" : "\U0001f4be  Save",
                Size = new Size(140, 40),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += async (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtTitle.Text))
                {
                    MessageBox.Show("Title is required.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var entry = existing ?? new HelperEntry();
                entry.title = txtTitle.Text.Trim();
                entry.url = txtUrl.Text.Trim();
                entry.description = txtDesc.Text.Trim();
                entry.category = cmbCat.Text.Trim();
                entry.color = colorNames[selectedColorIdx];
                entry.createdBy = _allUsers.FirstOrDefault(u => u.IsAdmin)?.Name ?? "Admin";
                if (!isEdit) entry.createdAt = DateTime.UtcNow.ToString("o");

                // Save new custom category if it's not in defaults
                bool isDefaultCat = Array.Exists(defaultCats, c => c == entry.category);
                if (!isDefaultCat && !_wikiCategories.Contains(entry.category))
                {
                    await SaveWikiCategoryAsync(entry.category);
                }

                await SaveWikiEntryAsync(entry);
                dlg.Close();
                await LoadWikiEntriesAsync();
                ShowStatus("\u2713 Wiki entry saved: " + entry.title);
            };
            dlg.Controls.Add(btnSave);

            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(100, 40),
                Location = new Point(x + 150, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = _isDarkMode ? Color.FromArgb(55, 62, 76) : Color.FromArgb(200, 200, 210),
                ForeColor = _isDarkMode ? Color.FromArgb(160, 170, 180) : Color.FromArgb(80, 80, 90),
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, ev) => dlg.Close();
            dlg.Controls.Add(btnCancel);

            dlg.ShowDialog(this);
        }

        /// <summary>Saves wiki entry to Firebase (create or update).</summary>
        private async Task SaveWikiEntryAsync(HelperEntry entry)
        {
            try
            {
                string json = JsonConvert.SerializeObject(entry);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                if (!string.IsNullOrWhiteSpace(entry.Key))
                {
                    // Update existing: PUT /teams/{joinCode}/helper/{key}.json
                    string url = _firebaseBaseUrl + "/helper/" + entry.Key + ".json";
                    await _http.PutAsync(url, content);
                }
                else
                {
                    // Create new: POST /teams/{joinCode}/helper.json (Firebase generates key)
                    string url = _firebaseBaseUrl + "/helper.json";
                    await _http.PostAsync(url, content);
                }
            }
            catch { }
        }

        /// <summary>Saves new wiki category to Firebase.</summary>
        private async Task SaveWikiCategoryAsync(string categoryName)
        {
            try
            {
                string json = JsonConvert.SerializeObject(categoryName);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                string url = _firebaseBaseUrl + "/helperCategories.json";
                await _http.PostAsync(url, content);
            }
            catch { }
        }

        // ═══ UI HELPERS — REUSABLE CARD, BUTTON, AND LABEL BUILDERS ═══
        /// <summary>Shows temporary status message (auto-hides after 4 seconds).</summary>
        // ═══ FAVORITES HELPERS ═══
        private string GetFavoritesDisplayText()
        {
            var favs = UserStorage.GetFavoriteUsers(_currentUserName);
            return favs.Count > 0 ? string.Join(", ", favs) : "(no favorites set)";
        }

        private void ShowEditFavoritesDialog()
        {
            var currentFavs = UserStorage.GetFavoriteUsers(_currentUserName);

            // Collect all known users across all joined teams
            var allKnownUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in _team.Members)
                if (!member.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase))
                    allKnownUsers.Add(member);

            // Also add users from other joined teams
            var joinedTeams = UserStorage.GetJoinedTeams();
            foreach (var teamEntry in joinedTeams)
            {
                var otherTeam = UserStorage.LoadTeamByCode(teamEntry.JoinCode);
                if (otherTeam?.Members != null)
                    foreach (var m in otherTeam.Members)
                        if (!m.Equals(_currentUserName, StringComparison.OrdinalIgnoreCase))
                            allKnownUsers.Add(m);
            }

            using (var dlg = new Form())
            {
                dlg.Text = "\u2b50 Edit Favorite Users";
                dlg.Width = 360;
                dlg.Height = 420;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = _isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250);
                dlg.ForeColor = fgColor;

                var lbl = new Label
                {
                    Text = "Check users to add to favorites.\nFavorites appear in DM panel across all projects.",
                    Font = new Font("Segoe UI", 9),
                    ForeColor = dimColor,
                    Location = new Point(16, 12),
                    Size = new Size(320, 36)
                };
                dlg.Controls.Add(lbl);

                var checklist = new CheckedListBox
                {
                    Location = new Point(16, 52),
                    Size = new Size(310, 280),
                    Font = new Font("Segoe UI", 10),
                    BackColor = fieldBg,
                    ForeColor = fgColor,
                    BorderStyle = BorderStyle.FixedSingle,
                    CheckOnClick = true
                };
                foreach (var name in allKnownUsers.OrderBy(n => n))
                {
                    int idx = checklist.Items.Add(name);
                    if (currentFavs.Contains(name))
                        checklist.SetItemChecked(idx, true);
                }
                dlg.Controls.Add(checklist);

                var btnSaveFav = new Button
                {
                    Text = "\u2705 Save Favorites",
                    Location = new Point(16, 340),
                    Size = new Size(310, 36),
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = accentColor,
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand
                };
                btnSaveFav.FlatAppearance.BorderSize = 0;
                btnSaveFav.Click += (s2, e2) =>
                {
                    var newFavs = new List<string>();
                    foreach (var item in checklist.CheckedItems)
                        newFavs.Add(item.ToString());
                    UserStorage.SaveFavoriteUsers(_currentUserName, newFavs);
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                };
                dlg.Controls.Add(btnSaveFav);

                dlg.ShowDialog(this);
            }
        }

        // ════════════════════════════════════════════════════════════════════════════
        // PROJECT STAGES — LOAD, ADD, EDIT (Firebase CRUD)
        // ════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads all project stages from Firebase and populates the ListView.
        /// </summary>
        private async Task LoadProjectStagesAsync(ListView listStages)
        {
            try
            {
                string url = _firebaseBaseUrl + "/project_stages.json";
                var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    listStages.Items.Clear();
                    return;
                }

                var stages = JsonConvert.DeserializeObject<Dictionary<string, ProjectStage>>(json);
                if (stages == null) return;

                listStages.Items.Clear();
                foreach (var kvp in stages.OrderBy(s => s.Value.order))
                {
                    var s = kvp.Value;
                    var item = new ListViewItem(s.name ?? "");
                    item.SubItems.Add(s.status ?? "Not Started");
                    item.SubItems.Add(s.description ?? "");
                    item.SubItems.Add(s.color ?? "#808080");
                    item.Tag = kvp.Key; // Firebase key for edit/delete

                    // Color the row based on status
                    if (s.status == "Completed")
                        item.ForeColor = Color.FromArgb(46, 204, 113);
                    else if (s.status == "In Progress")
                        item.ForeColor = Color.FromArgb(241, 196, 15);

                    listStages.Items.Add(item);
                }
            }
            catch { }
        }

        /// <summary>
        /// Shows a dialog to add or edit a project stage.
        /// Pass null stage and key to create a new one.
        /// </summary>
        private async Task ShowStageDialog(ListView listStages, ProjectStage existing, string existingKey)
        {
            bool isEdit = existing != null;

            var dlg = new Form
            {
                Text = isEdit ? "Edit Stage" : "Add Stage",
                Size = new Size(380, 340),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = _isDarkMode ? Color.FromArgb(30, 36, 48) : Color.FromArgb(248, 249, 252)
            };

            Color lblColor = _isDarkMode ? Color.FromArgb(200, 210, 225) : Color.FromArgb(40, 40, 40);
            Color inputBg = _isDarkMode ? Color.FromArgb(38, 46, 60) : Color.White;
            Color inputFg = _isDarkMode ? Color.White : Color.Black;

            int y = 16;

            // Stage Name
            dlg.Controls.Add(new Label { Text = "Stage Name:", Location = new Point(16, y), AutoSize = true, ForeColor = lblColor, Font = new Font("Segoe UI", 9) });
            y += 22;
            var txtName = new TextBox { Location = new Point(16, y), Size = new Size(330, 28), BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 10), Text = existing?.name ?? "" };
            dlg.Controls.Add(txtName);
            y += 36;

            // Description
            dlg.Controls.Add(new Label { Text = "Description:", Location = new Point(16, y), AutoSize = true, ForeColor = lblColor, Font = new Font("Segoe UI", 9) });
            y += 22;
            var txtDesc = new TextBox { Location = new Point(16, y), Size = new Size(330, 28), BackColor = inputBg, ForeColor = inputFg, Font = new Font("Segoe UI", 10), Text = existing?.description ?? "" };
            dlg.Controls.Add(txtDesc);
            y += 36;

            // Status dropdown
            dlg.Controls.Add(new Label { Text = "Status:", Location = new Point(16, y), AutoSize = true, ForeColor = lblColor, Font = new Font("Segoe UI", 9) });
            y += 22;
            var cmbStatus = new ComboBox
            {
                Location = new Point(16, y),
                Size = new Size(200, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = inputBg,
                ForeColor = inputFg,
                Font = new Font("Segoe UI", 10)
            };
            cmbStatus.Items.AddRange(new[] { "Not Started", "In Progress", "Completed" });
            cmbStatus.SelectedItem = existing?.status ?? "Not Started";
            dlg.Controls.Add(cmbStatus);
            y += 36;

            // Color picker
            dlg.Controls.Add(new Label { Text = "Color:", Location = new Point(16, y), AutoSize = true, ForeColor = lblColor, Font = new Font("Segoe UI", 9) });
            y += 22;
            string selectedColor = existing?.color ?? "#FF7F50";
            var btnColor = new Button
            {
                Location = new Point(16, y),
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                Text = selectedColor,
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White
            };
            btnColor.FlatAppearance.BorderSize = 1;
            try { btnColor.BackColor = ColorTranslator.FromHtml(selectedColor); } catch { btnColor.BackColor = Color.Coral; }

            btnColor.Click += (s, e) =>
            {
                using (var cd = new ColorDialog { Color = btnColor.BackColor })
                {
                    if (cd.ShowDialog() == DialogResult.OK)
                    {
                        btnColor.BackColor = cd.Color;
                        selectedColor = $"#{cd.Color.R:X2}{cd.Color.G:X2}{cd.Color.B:X2}";
                        btnColor.Text = selectedColor;
                    }
                }
            };
            dlg.Controls.Add(btnColor);
            y += 44;

            // Save / Cancel buttons
            var btnSave = new Button
            {
                Text = isEdit ? "Save Changes" : "Add Stage",
                Location = new Point(16, y),
                Size = new Size(140, 36),
                BackColor = Color.FromArgb(59, 130, 246),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(170, y),
                Size = new Size(100, 36),
                BackColor = _isDarkMode ? Color.FromArgb(55, 60, 75) : Color.FromArgb(200, 200, 200),
                ForeColor = _isDarkMode ? Color.White : Color.Black,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => dlg.Close();

            btnSave.Click += async (s, e) =>
            {
                string name = txtName.Text.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("Stage name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var stage = new ProjectStage
                {
                    name = name,
                    description = txtDesc.Text.Trim(),
                    status = cmbStatus.SelectedItem?.ToString() ?? "Not Started",
                    color = selectedColor,
                    order = isEdit ? (existing?.order ?? 0) : listStages.Items.Count
                };

                try
                {
                    string jsonPayload = JsonConvert.SerializeObject(stage);
                    var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                    HttpResponseMessage resp;
                    if (isEdit && !string.IsNullOrEmpty(existingKey))
                    {
                        // UPDATE existing stage
                        string url = _firebaseBaseUrl + "/project_stages/" + existingKey + ".json";
                        resp = await _http.PutAsync(url, content);
                    }
                    else
                    {
                        // ADD new stage
                        string url = _firebaseBaseUrl + "/project_stages.json";
                        resp = await _http.PostAsync(url, content);
                    }

                    if (resp.IsSuccessStatusCode)
                    {
                        ShowStatus(isEdit ? "\u2705 Stage updated!" : "\u2705 Stage added!", true);
                        await LoadProjectStagesAsync(listStages);
                        dlg.Close();
                    }
                    else
                    {
                        ShowStatus("Failed to save stage.", false);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            dlg.Controls.Add(btnSave);
            dlg.Controls.Add(btnCancel);
            dlg.ShowDialog(this);
        }

        /// <summary>Simple input dialog — replacement for VB InputBox.</summary>
        private string ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            using (var dlg = new Form())
            {
                dlg.Text = title;
                dlg.Size = new Size(380, 160);
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.BackColor = _isDarkMode ? Color.FromArgb(30, 36, 46) : Color.FromArgb(245, 247, 250);

                var lbl = new Label
                {
                    Text = prompt,
                    Location = new Point(12, 14),
                    AutoSize = true,
                    ForeColor = _isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59)
                };
                var txt = new TextBox
                {
                    Text = defaultValue,
                    Location = new Point(12, 40),
                    Width = 340,
                    Font = new Font("Segoe UI", 10f),
                    BackColor = _isDarkMode ? Color.FromArgb(40, 46, 56) : Color.White,
                    ForeColor = _isDarkMode ? Color.FromArgb(220, 224, 230) : Color.Black
                };
                var btnOk = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(196, 80),
                    Size = new Size(75, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(59, 130, 246),
                    ForeColor = Color.White
                };
                btnOk.FlatAppearance.BorderSize = 0;
                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(278, 80),
                    Size = new Size(75, 30),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(100, 116, 139),
                    ForeColor = Color.White
                };
                btnCancel.FlatAppearance.BorderSize = 0;

                dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;

                return dlg.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
            }
        }

        private void ShowStatus(string msg, bool isError = false)
        {
            lblStatus.Text = msg;
            lblStatus.ForeColor = isError ? dangerColor : successColor;
            lblStatus.Visible = true;

            var t = new Timer { Interval = 4000 };
            t.Tick += (s, e) => { lblStatus.Visible = false; t.Stop(); t.Dispose(); };
            t.Start();
        }

        /// <summary>Creates a card panel with subtle border and rounded appearance.</summary>
        private Panel MakeCard(int x, int y, int w, int h)
        {
            var card = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = panelBg,
                Padding = new Padding(0)
            };
            // Draw subtle border with top accent stripe
            card.Paint += (s, ev) =>
            {
                Color border = _isDarkMode ? Color.FromArgb(44, 52, 66) : Color.FromArgb(218, 222, 230);
                using (var pen = new Pen(border, 1))
                {
                    ev.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    ev.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
                }
                // Top accent stripe (semi-transparent accent color)
                using (var brush = new SolidBrush(Color.FromArgb(30, accentColor)))
                    ev.Graphics.FillRectangle(brush, 0, 0, card.Width, 3);
            };
            return card;
        }

        /// <summary>Section icon (emoji).</summary>
        private Label MakeSectionIcon(string emoji, int x, int y)
        {
            return new Label
            {
                Text = emoji,
                Font = new Font("Segoe UI", 12),
                Location = new Point(x, y - 1),
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        /// <summary>Section title (bold, primary color).</summary>
        private Label MakeSectionTitle(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = _isDarkMode ? Color.FromArgb(235, 240, 250) : Color.FromArgb(30, 40, 60),
                Location = new Point(x, y + 1),
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        /// <summary>Field label (small, uppercase, dim).</summary>
        private Label MakeFieldLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = _isDarkMode ? Color.FromArgb(100, 112, 130) : Color.FromArgb(120, 130, 150),
                Location = new Point(x, y),
                AutoSize = true,
                BackColor = Color.Transparent
            };
        }

        private TextBox MakeTextBox(int x, int y, int width)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                Font = new Font("Segoe UI", 10),
                BackColor = _isDarkMode ? Color.FromArgb(34, 40, 52) : Color.FromArgb(250, 251, 253),
                ForeColor = fgColor,
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        /// <summary>Compact pill-shaped button (for Copy, New, etc.).</summary>
        private Button MakePillButton(string text, int x, int y)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(62, 28),
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                BackColor = _isDarkMode ? Color.FromArgb(50, 58, 72) : Color.FromArgb(228, 232, 240),
                ForeColor = _isDarkMode ? Color.FromArgb(180, 190, 210) : Color.FromArgb(60, 70, 90),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            Color hoverBg = _isDarkMode ? Color.FromArgb(62, 70, 86) : Color.FromArgb(210, 216, 228);
            btn.MouseEnter += (s, e) => btn.BackColor = hoverBg;
            btn.MouseLeave += (s, e) => btn.BackColor = _isDarkMode ? Color.FromArgb(50, 58, 72) : Color.FromArgb(228, 232, 240);
            return btn;
        }

        /// <summary>Action button with hover effect (for CRUD operations).</summary>
        private Button MakeActionButton(string text, Color bg, Color fg, int x, int y, int width)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 34),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            // Darken on hover for visual feedback
            Color hoverBg = Color.FromArgb(
                Math.Max(0, bg.R - 20),
                Math.Max(0, bg.G - 20),
                Math.Max(0, bg.B - 20));
            btn.MouseEnter += (s, e) => btn.BackColor = hoverBg;
            btn.MouseLeave += (s, e) => btn.BackColor = bg;
            return btn;
        }

        private void EnsureCurrentUserMetaExists()
        {
            if (_team.MembersMeta == null)
                _team.MembersMeta = new Dictionary<string, MemberMeta>();

            if (!_team.MembersMeta.ContainsKey(_currentUserName))
                _team.MembersMeta[_currentUserName] = new MemberMeta();
        }

        private static string ConvertImageFileToBase64(string filePath, int size)
        {
            using (var image = Image.FromFile(filePath))
            using (var bitmap = new Bitmap(size, size))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var ms = new MemoryStream())
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.Clear(Color.Transparent);
                graphics.DrawImage(image, new Rectangle(0, 0, size, size));
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        private static Image AvatarBase64ToImage(string avatarBase64, int size, Color fallbackColor, string userName)
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(avatarBase64);
                using (var ms = new MemoryStream(bytes))
                using (var image = Image.FromStream(ms))
                {
                    return new Bitmap(image, new Size(size, size));
                }
            }
            catch
            {
                return CreateInitialAvatarImage(size, fallbackColor, userName);
            }
        }

        private static Image CreateInitialAvatarImage(int size, Color backColor, string userName)
        {
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(backColor))
            using (var textBrush = new SolidBrush(Color.White))
            using (var font = new Font("Segoe UI", Math.Max(10, size / 3f), FontStyle.Bold))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(brush, 0, 0, size - 1, size - 1);
                string initial = string.IsNullOrWhiteSpace(userName) ? "?" : userName.Substring(0, 1).ToUpperInvariant();
                g.DrawString(initial, font, textBrush, new RectangleF(0, 0, size, size), format);
            }
            return bmp;
        }

        /// <summary>Slider-style switch button for ON/OFF settings.</summary>
        private Button MakeSwitchButton(string text, bool isOn, int x, int y, int width)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 38),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = _isDarkMode ? Color.FromArgb(230, 236, 244) : Color.FromArgb(36, 42, 52),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                AccessibleDescription = isOn ? "on" : "off",
                BackColor = Color.Transparent,
                UseVisualStyleBackColor = false
            };

            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseDownBackColor = Color.Transparent;
            btn.FlatAppearance.MouseOverBackColor = Color.Transparent;

            btn.Paint += (s, e) =>
            {
                bool on = string.Equals(btn.AccessibleDescription, "on", StringComparison.OrdinalIgnoreCase);
                bool hover = btn.ClientRectangle.Contains(btn.PointToClient(Cursor.Position));

                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                Color cardBg = _isDarkMode ? Color.FromArgb(34, 40, 52) : Color.FromArgb(242, 245, 249);
                Color border = _isDarkMode ? Color.FromArgb(70, 80, 96) : Color.FromArgb(204, 210, 220);
                Color labelColor = _isDarkMode ? Color.FromArgb(232, 238, 245) : Color.FromArgb(48, 56, 68);
                Color trackOn = Color.FromArgb(34, 197, 94);
                Color trackOff = _isDarkMode ? Color.FromArgb(90, 98, 112) : Color.FromArgb(188, 194, 204);
                Color trackColor = hover
                    ? (on ? Color.FromArgb(22, 163, 74) : (_isDarkMode ? Color.FromArgb(105, 114, 128) : Color.FromArgb(174, 181, 192)))
                    : (on ? trackOn : trackOff);

                var bounds = new Rectangle(0, 0, btn.Width - 1, btn.Height - 1);
                using (var path = ThemeConstants.RoundedRect(bounds, 10))
                using (var bgBrush = new SolidBrush(cardBg))
                using (var borderPen = new Pen(border))
                {
                    e.Graphics.FillPath(bgBrush, path);
                    e.Graphics.DrawPath(borderPen, path);
                }

                int switchWidth = 48;
                int switchHeight = 24;
                int switchX = btn.Width - switchWidth - 10;
                int switchY = (btn.Height - switchHeight) / 2;
                var switchRect = new Rectangle(switchX, switchY, switchWidth, switchHeight);

                using (var switchPath = ThemeConstants.RoundedRect(switchRect, switchHeight / 2))
                using (var trackBrush = new SolidBrush(trackColor))
                {
                    e.Graphics.FillPath(trackBrush, switchPath);
                }

                int knobSize = 20;
                int knobY = switchY + 2;
                int knobX = on ? switchX + switchWidth - knobSize - 2 : switchX + 2;
                var knobRect = new Rectangle(knobX, knobY, knobSize, knobSize);
                using (var knobBrush = new SolidBrush(Color.White))
                using (var knobPen = new Pen(Color.FromArgb(210, 214, 220)))
                {
                    e.Graphics.FillEllipse(knobBrush, knobRect);
                    e.Graphics.DrawEllipse(knobPen, knobRect);
                }

                var textRect = new Rectangle(12, 0, switchX - 24, btn.Height);
                TextRenderer.DrawText(
                    e.Graphics,
                    text,
                    btn.Font,
                    textRect,
                    labelColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };

            btn.MouseEnter += (s, e) => btn.Invalidate();
            btn.MouseLeave += (s, e) => btn.Invalidate();

            return btn;
        }

        // Legacy helpers — keep for compatibility with any remaining callers
        private Label MakeSectionHeader(string text, int y) => MakeSectionTitle(text, 0, y);
        private Label MakeLabel(string text, int x, int y) => MakeFieldLabel(text, x, y);
        private Button MakeButton(string text, Color bg, Color fg, int x, int y)
        {
            return MakeActionButton(text, bg, fg, x, y, 140);
        }
        private Button MakeSmallButton(string text, int x, int y, int width)
        {
            return MakePillButton(text, x, y);
        }

        private void ApplyThemePreview(CustomTheme customTheme)
        {
            bool useCustomTheme = customTheme != null && customTheme.Enabled;
            Color previewBack = useCustomTheme
                ? customTheme.GetBackground()
                : (_isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250));
            Color previewCard = useCustomTheme
                ? customTheme.GetCard()
                : (_isDarkMode ? Color.FromArgb(30, 36, 46) : Color.White);
            Color previewInput = useCustomTheme
                ? customTheme.GetInput()
                : (_isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White);
            Color previewText = useCustomTheme
                ? customTheme.GetText()
                : (_isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59));
            Color previewSecondary = useCustomTheme
                ? customTheme.GetSecondaryText()
                : (_isDarkMode ? Color.FromArgb(120, 130, 145) : Color.FromArgb(100, 116, 139));
            Color previewAccent = useCustomTheme
                ? customTheme.GetAccent()
                : Color.FromArgb(255, 127, 80);
            Color previewButtonText = useCustomTheme ? customTheme.GetButtonText() : Color.White;
            Color previewBorder = useCustomTheme
                ? customTheme.GetGridLine()
                : (_isDarkMode ? Color.FromArgb(60, 70, 85) : Color.FromArgb(215, 223, 235));

            SuspendLayout();
            this.BackColor = previewBack;
            this.ForeColor = previewText;

            ApplyThemePreviewToControls(this.Controls, previewBack, previewCard, previewInput,
                previewText, previewSecondary, previewAccent, previewBorder, previewButtonText);

            if (useCustomTheme)
            {
                try
                {
                    this.Font = customTheme.GetFont();
                }
                catch { }
            }
            else
            {
                this.Font = new Font("Segoe UI", 9f);
            }

            ResumeLayout(true);
            Invalidate(true);
        }

        private void ApplyThemePreviewToControls(Control.ControlCollection controls,
            Color backMain, Color backCard, Color backInput, Color foreMain, Color foreSecondary,
            Color accentColor, Color borderColor, Color buttonText)
        {
            foreach (Control ctrl in controls)
            {
                if (ctrl is Panel pnl)
                {
                    pnl.BackColor = pnl.Dock == DockStyle.Fill ? backMain : backCard;
                }
                else if (ctrl is TextBox tb)
                {
                    tb.BackColor = backInput;
                    tb.ForeColor = foreMain;
                }
                else if (ctrl is RichTextBox rtb)
                {
                    rtb.BackColor = backInput;
                    rtb.ForeColor = foreMain;
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
                else if (ctrl is CheckBox chk)
                {
                    chk.ForeColor = foreMain;
                }
                else if (ctrl is Button btn)
                {
                    bool isPrimaryAction = btn.Text.Contains("Apply Theme") || btn.Text.Contains("Create Team") ||
                        btn.Text.Contains("Save") || btn.Text.Contains("Invite") || btn.Text.Contains("Create");
                    bool isDangerAction = btn.Text.Contains("Delete") || btn.Text.Contains("Remove") ||
                        btn.Text.Contains("Kick") || btn.Text.Contains("Reset") || btn.Text.Contains("Stop");

                    btn.ForeColor = isPrimaryAction || isDangerAction ? Color.White : buttonText;
                    btn.BackColor = isPrimaryAction
                        ? accentColor
                        : isDangerAction
                            ? Color.FromArgb(220, 53, 69)
                            : backCard;
                    btn.FlatAppearance.BorderColor = borderColor;
                }
                else if (ctrl is ListView list)
                {
                    list.BackColor = backCard;
                    list.ForeColor = foreMain;
                }
                else if (ctrl is Label lbl)
                {
                    string text = lbl.Text ?? string.Empty;
                    if (text == text.ToUpperInvariant() || text.Contains("Custom Theme") || text.Contains("Team Settings"))
                        lbl.ForeColor = accentColor;
                    else if (text.IndexOf("offline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             text.IndexOf("hint", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             text.IndexOf("copied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             text.IndexOf("preset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             text.IndexOf("click apply", StringComparison.OrdinalIgnoreCase) >= 0)
                        lbl.ForeColor = foreSecondary;
                    else
                        lbl.ForeColor = foreMain;
                }
                else if (!(ctrl is PictureBox))
                {
                    ctrl.BackColor = backCard;
                    ctrl.ForeColor = foreMain;
                }

                if (ctrl.HasChildren)
                {
                    ApplyThemePreviewToControls(ctrl.Controls, backMain, backCard, backInput, foreMain,
                        foreSecondary, accentColor, borderColor, buttonText);
                }
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // TeamOptionsPanel
            // 
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "TeamOptionsPanel";
            this.Load += new System.EventHandler(this.TeamOptionsPanel_Load);
            this.ResumeLayout(false);

        }

        private void TeamOptionsPanel_Load(object sender, EventArgs e)
        {

        }
    }

    // ═══════════════════════════════════════════════════════════
    // MEMBER EDIT DIALOG — REUSABLE DIALOG FOR ADD/EDIT MEMBERS
    // ═══════════════════════════════════════════════════════════
    /// <summary>
    /// Dialog for adding or editing team members.
    /// Allows selecting: Name, Role (from predefined list), Job Title (custom text),
    /// and Color (from 15-color palette).
    /// In add mode: name is editable. In edit mode: name is read-only.
    /// </summary>
    public class MemberEditDialog : Form
    {
        public string MemberName { get; private set; }
        public string MemberColor { get; private set; }
        public string MemberTitle { get; private set; }
        public string MemberRole { get; private set; }

        private TextBox txtName;
        private TextBox txtTitle;
        private ComboBox cmbRole;
        private Panel pnlColorPreview;
        private string _selectedColor = "";

        // Color palette (same as TeamOptionsPanel)
        private static readonly string[] Colors = new string[]
        {
            "#3498DB", "#2ECC71", "#E74C3C", "#9B59B6", "#F1C40F",
            "#1ABC9C", "#E67E22", "#E91E63", "#00BCD4", "#8BC34A",
            "#FF5722", "#607D8B", "#795548", "#FF9800", "#673AB7"
        };

        private static readonly string[] Roles = new string[]
        {
            "Developer", "Frontend Dev", "Backend Dev", "Full Stack Dev",
            "Designer", "QA Tester", "DevOps", "Manager", "Team Lead",
            "Product Owner", "Scrum Master", "Intern", "Freelancer", "Other"
        };

        public MemberEditDialog(string title, string name, string color, string memberTitle, string role,
            bool isDarkMode, bool nameEditable = true)
        {
            Color bg = isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250);
            Color inputBg = isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White;
            Color fg = isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59);
            Color labelFg = isDarkMode ? Color.FromArgb(120, 130, 145) : Color.FromArgb(100, 116, 139);
            Color accent = Color.FromArgb(255, 127, 80);

            this.Text = title;
            this.Size = new Size(420, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = bg;
            this.ForeColor = fg;

            int y = 15, x = 20, w = 360;

            // Name field
            AddLabel("NAME", x, y, labelFg);
            y += 20;
            txtName = new TextBox
            {
                Text = name,
                Location = new Point(x, y),
                Size = new Size(w, 28),
                Font = new Font("Segoe UI", 10),
                BackColor = inputBg,
                ForeColor = fg,
                BorderStyle = BorderStyle.FixedSingle,
                ReadOnly = !nameEditable  // Read-only in edit mode
            };
            this.Controls.Add(txtName);
            y += 36;

            // Role dropdown
            AddLabel("ROLE", x, y, labelFg);
            y += 20;
            cmbRole = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 28),
                Font = new Font("Segoe UI", 10),
                BackColor = inputBg,
                ForeColor = fg,
                DropDownStyle = ComboBoxStyle.DropDown
            };
            cmbRole.Items.AddRange(Roles);
            if (!string.IsNullOrEmpty(role))
                cmbRole.Text = role;
            this.Controls.Add(cmbRole);
            y += 36;

            // Title field (custom job title)
            AddLabel("JOB TITLE", x, y, labelFg);
            y += 20;
            txtTitle = new TextBox
            {
                Text = memberTitle,
                Location = new Point(x, y),
                Size = new Size(w, 28),
                Font = new Font("Segoe UI", 10),
                BackColor = inputBg,
                ForeColor = fg,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(txtTitle);
            y += 40;

            // Color picker section
            AddLabel("COLOR", x, y, labelFg);
            y += 20;

            _selectedColor = color ?? "";

            // Color preview swatch
            pnlColorPreview = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(30, 30),
                BorderStyle = BorderStyle.FixedSingle
            };
            UpdateColorPreview();
            this.Controls.Add(pnlColorPreview);

            // Color selection buttons (8 columns × 2 rows = 16 colors, but we have 15)
            int cx = x + 38;
            for (int i = 0; i < Colors.Length; i++)
            {
                string c = Colors[i];
                var btn = new Panel
                {
                    Location = new Point(cx + (i % 8) * 34, y + (i / 8) * 34),
                    Size = new Size(28, 28),
                    BackColor = ColorTranslator.FromHtml(c),
                    BorderStyle = BorderStyle.FixedSingle,
                    Cursor = Cursors.Hand
                };
                string colorCopy = c;
                btn.Click += (s, ev) =>
                {
                    _selectedColor = colorCopy;
                    UpdateColorPreview();
                };
                this.Controls.Add(btn);
            }

            y += 76;

            // OK button
            var btnOK = new Button
            {
                Text = nameEditable ? "\u2714  Add Member" : "\u2714  Save",
                Location = new Point(x, y),
                Size = new Size(160, 38),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Name is required.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                MemberName = txtName.Text.Trim();
                MemberColor = _selectedColor;
                MemberTitle = txtTitle.Text.Trim();
                MemberRole = cmbRole.Text.Trim();
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(btnOK);

            // Cancel button
            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(x + 170, y),
                Size = new Size(120, 38),
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = isDarkMode ? Color.FromArgb(38, 44, 56) : Color.FromArgb(200, 200, 210),
                ForeColor = isDarkMode ? Color.FromArgb(160, 170, 180) : Color.FromArgb(80, 80, 90),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void UpdateColorPreview()
        {
            if (!string.IsNullOrEmpty(_selectedColor) && _selectedColor.StartsWith("#"))
            {
                try
                {
                    pnlColorPreview.BackColor = ColorTranslator.FromHtml(_selectedColor);
                    return;
                }
                catch { }
            }
            pnlColorPreview.BackColor = Color.Gray;
        }

        private void AddLabel(string text, int x, int y, Color fg)
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
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  INVITE EMAIL DIALOG — Send invite with download link + join code
    //  Users join the team themselves using the join code.
    //  Admin pastes the invitee's email and clicks Send.
    //  Opens the default mail client with a pre-filled invite message.
    // ═══════════════════════════════════════════════════════════════════════════
    public class InviteEmailDialog : Form
    {
        private TextBox txtEmail;
        private Label lblStatus;
        private string _teamName;
        private string _joinCode;

        // ── Download link for the app (update this when you have a real URL) ──
        private const string DownloadLink = "https://github.com/8BitLabEngineering/WorkFlow/releases/latest";

        public InviteEmailDialog(string teamName, string joinCode, bool isDarkMode)
        {
            _teamName = teamName;
            _joinCode = joinCode;

            // ── Theme colors ──
            Color bg = isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250);
            Color inputBg = isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White;
            Color fg = isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59);
            Color labelFg = isDarkMode ? Color.FromArgb(120, 130, 145) : Color.FromArgb(100, 116, 139);
            Color accent = Color.FromArgb(255, 127, 80);

            this.Text = "Invite Team Member";
            this.Size = new Size(460, 280);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = bg;
            this.ForeColor = fg;

            int y = 18, x = 20, w = 400;

            // ── Header label ──
            var lblHeader = new Label
            {
                Text = "\u2709  Send Invite to Join \"" + _teamName + "\"",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = accent,
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(lblHeader);
            y += 36;

            // ── Email label ──
            var lblEmail = new Label
            {
                Text = "EMAIL ADDRESS",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = labelFg,
                Location = new Point(x, y),
                AutoSize = true
            };
            this.Controls.Add(lblEmail);
            y += 20;

            // ── Email text box with placeholder workaround (.NET 4.7.2) ──
            Color emailFg = fg;  // capture for lambda
            txtEmail = new TextBox
            {
                Text = "colleague@company.com",
                Location = new Point(x, y),
                Size = new Size(w, 32),
                Font = new Font("Segoe UI", 11f),
                BackColor = inputBg,
                ForeColor = Color.Gray,
                BorderStyle = BorderStyle.FixedSingle
            };
            txtEmail.GotFocus += (s, ev) =>
            {
                if (txtEmail.Text == "colleague@company.com")
                {
                    txtEmail.Text = "";
                    txtEmail.ForeColor = emailFg;
                }
            };
            txtEmail.LostFocus += (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtEmail.Text))
                {
                    txtEmail.Text = "colleague@company.com";
                    txtEmail.ForeColor = Color.Gray;
                }
            };
            this.Controls.Add(txtEmail);
            y += 44;

            // ── Info label showing what will be sent ──
            var lblInfo = new Label
            {
                Text = "The invite email will include the download link and join code: " + _joinCode,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = labelFg,
                Location = new Point(x, y),
                Size = new Size(w, 18)
            };
            this.Controls.Add(lblInfo);
            y += 28;

            // ── Buttons: Send + Cancel ──
            var btnSend = new Button
            {
                Text = "\u2709  Send Invite",
                FlatStyle = FlatStyle.Flat,
                BackColor = accent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Size = new Size(140, 36),
                Location = new Point(x, y),
                Cursor = Cursors.Hand
            };
            btnSend.FlatAppearance.BorderSize = 0;
            btnSend.MouseEnter += (s, e) => btnSend.BackColor = Color.FromArgb(255, 150, 100);
            btnSend.MouseLeave += (s, e) => btnSend.BackColor = accent;
            btnSend.Click += OnSendInvite;
            this.Controls.Add(btnSend);

            var btnCancel = new Button
            {
                Text = "Cancel",
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 80, 95),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                Size = new Size(100, 36),
                Location = new Point(btnSend.Right + 10, y),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => this.Close();
            this.Controls.Add(btnCancel);

            // ── Status label ──
            lblStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 197, 94),
                Location = new Point(btnCancel.Right + 14, y + 8),
                AutoSize = true
            };
            this.Controls.Add(lblStatus);

            this.AcceptButton = btnSend;
        }

        private void OnSendInvite(object sender, EventArgs e)
        {
            string email = txtEmail.Text.Trim();

            // ── Validate email (also reject placeholder text) ──
            if (string.IsNullOrEmpty(email) || email == "colleague@company.com" ||
                !email.Contains("@") || !email.Contains("."))
            {
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                lblStatus.Text = "\u274c Invalid email";
                return;
            }

            // ── Build the invite email content ──
            string subject = Uri.EscapeDataString("You're invited to join \"" + _teamName + "\" on WorkFlow!");
            string body = Uri.EscapeDataString(
                "Hi!\r\n\r\n" +
                "You have been invited to join the team \"" + _teamName + "\" on WorkFlow — 8BitLab's Team Time Tracker.\r\n\r\n" +
                "To get started:\r\n" +
                "1. Download the app here: " + DownloadLink + "\r\n" +
                "2. Install and launch WorkFlow\r\n" +
                "3. Choose \"Join a Team\" and enter this join code:\r\n\r\n" +
                "    JOIN CODE:  " + _joinCode + "\r\n\r\n" +
                "4. Pick a username and you're in!\r\n\r\n" +
                "See you on the team!\r\n" +
                "— " + _teamName + " Admin"
            );

            // ── Open default mail client via mailto: ──
            string mailto = "mailto:" + email + "?subject=" + subject + "&body=" + body;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = mailto,
                    UseShellExecute = true
                });
                lblStatus.ForeColor = Color.FromArgb(34, 197, 94);
                lblStatus.Text = "\u2713 Opened!";
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Color.FromArgb(220, 53, 69);
                lblStatus.Text = "\u274c " + ex.Message;
            }
        }
    }
}
