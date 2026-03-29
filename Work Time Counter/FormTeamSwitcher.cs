// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        FormTeamSwitcher.cs                                          ║
// ║  PURPOSE:     TEAM SWITCHER — SWITCH BETWEEN TEAMS, JOIN NEW TEAMS         ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Allows the user to:                                                       ║
// ║    1. See all teams they have joined                                       ║
// ║    2. Switch the active team (loads that team's data)                      ║
// ║    3. Join a new team via invite code                                      ║
// ║    4. Leave a team (removes from local index)                             ║
// ║                                                                            ║
// ║  RESULT:                                                                   ║
// ║    DialogResult.OK   → user selected/switched a team (check SelectedCode)  ║
// ║    DialogResult.Cancel → user cancelled                                    ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    /// <summary>
    /// FormTeamSwitcher — Allows users to switch between joined teams or join new teams.
    /// Displays current teams with switching capability and provides an option to join new teams via invite code.
    /// </summary>
    public class FormTeamSwitcher : Form
    {
        /// <summary>The join code of the team the user selected.</summary>
        public string SelectedCode { get; private set; }

        /// <summary>True if the user joined a brand-new team (needs user setup).</summary>
        public bool JoinedNewTeam { get; private set; }

        /// <summary>The TeamInfo of the newly joined team (only set if JoinedNewTeam is true).</summary>
        public TeamInfo NewTeamInfo { get; private set; }

        /// <summary>The user's name in the newly joined team.</summary>
        public string NewTeamUserName { get; private set; }

        // ── Colors ──
        private readonly Color bgColor = Color.FromArgb(24, 28, 36);
        private readonly Color fgColor = Color.FromArgb(220, 224, 230);
        private readonly Color accentColor = Color.FromArgb(255, 127, 80);
        private readonly Color dimColor = Color.FromArgb(120, 130, 145);
        private readonly Color fieldBg = Color.FromArgb(30, 36, 46);
        private readonly Color cardBg = Color.FromArgb(32, 38, 50);
        private readonly Color cardHover = Color.FromArgb(40, 48, 62);
        private readonly Color activeCard = Color.FromArgb(45, 55, 72);
        private readonly Color btnBg = Color.FromArgb(38, 44, 56);
        private readonly Color errorColor = Color.FromArgb(220, 53, 69);
        private readonly Color successColor = Color.FromArgb(34, 197, 94);

        // ── Controls ──
        private Panel panelTeamList;
        private Panel panelJoinNew;
        private FlowLayoutPanel flowTeams;
        private Label lblError;
        private CheckBox chkSkipStartup;
        private TextBox txtJoinCode;
        private TextBox txtJoinName;
        private Label lblJoinError;
        private Label lblJoinTeamInfo;

        private string _activeCode;
        private List<TeamIndexEntry> _teams;

        public FormTeamSwitcher()
        {
//             DebugLogger.Log("[TeamSwitcher] Initializing FormTeamSwitcher");
            _activeCode = UserStorage.GetActiveTeamCode() ?? "";
            _teams = UserStorage.GetJoinedTeams();
//             DebugLogger.Log($"[TeamSwitcher] Loaded {_teams.Count} joined teams. Active code: {_activeCode}");

            this.Text = "WorkFlow — Switch Team";
            this.Width = 520;
            this.Height = 560;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = bgColor;
            this.ForeColor = fgColor;

            BuildTeamListPage();
            BuildJoinNewPage();

            ShowPage(panelTeamList);
//             DebugLogger.Log("[TeamSwitcher] FormTeamSwitcher initialized successfully");
        }

        // ══════════════════════════════════════════════════════════
        //  PAGE 1 — Team List (all joined teams)
        // ══════════════════════════════════════════════════════════
        /// <summary>Builds the team list page UI with all joined teams.</summary>
        private void BuildTeamListPage()
        {
//             DebugLogger.Log("[TeamSwitcher] Building team list page");
            panelTeamList = new Panel { Dock = DockStyle.Fill, BackColor = bgColor };

            var lblTitle = new Label
            {
                Text = "⑂  Your Teams",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = accentColor,
                Location = new Point(20, 20),
                AutoSize = true
            };
            panelTeamList.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = "Select a team to switch to, or join a new one.",
                Font = new Font("Segoe UI", 10),
                ForeColor = dimColor,
                Location = new Point(22, 56),
                AutoSize = true
            };
            panelTeamList.Controls.Add(lblSub);

            // Scrollable team cards area
            flowTeams = new FlowLayoutPanel
            {
                Location = new Point(20, 90),
                Size = new Size(465, 330),
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = bgColor
            };
            panelTeamList.Controls.Add(flowTeams);

            PopulateTeamCards();

            chkSkipStartup = new CheckBox
            {
                Text = "Do not show this at startup (open last active team)",
                Font = new Font("Segoe UI", 9),
                ForeColor = dimColor,
                Location = new Point(20, 430),
                AutoSize = true,
                Checked = UserStorage.GetSkipTeamSwitcherOnStartup()
            };
            chkSkipStartup.CheckedChanged += (s, e) =>
            {
                UserStorage.SetSkipTeamSwitcherOnStartup(chkSkipStartup.Checked);
            };
            panelTeamList.Controls.Add(chkSkipStartup);

            // Error label
            lblError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = errorColor,
                Location = new Point(20, 452),
                Size = new Size(460, 20),
                Visible = false
            };
            panelTeamList.Controls.Add(lblError);

            // Join New Team button
            var btnJoinNew = new Button
            {
                Text = "➕  Join Another Team",
                Location = new Point(20, 478),
                Size = new Size(220, 44),
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                BackColor = accentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnJoinNew.FlatAppearance.BorderSize = 0;
            btnJoinNew.Click += (s, e) => ShowPage(panelJoinNew);
            panelTeamList.Controls.Add(btnJoinNew);

            // Cancel button
            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(260, 478),
                Size = new Size(220, 44),
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = btnBg,
                ForeColor = dimColor,
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            panelTeamList.Controls.Add(btnCancel);

            this.Controls.Add(panelTeamList);
        }

        /// <summary>Populates team cards in the flow panel. Clears existing cards and adds new ones.</summary>
        private void PopulateTeamCards()
        {
//             DebugLogger.Log("[TeamSwitcher] Populating team cards");
            flowTeams.Controls.Clear();

            if (_teams.Count == 0)
            {
//                 DebugLogger.Log("[TeamSwitcher] No teams found, showing empty message");
                var lblEmpty = new Label
                {
                    Text = "No teams joined yet. Click below to join one!",
                    Font = new Font("Segoe UI", 11),
                    ForeColor = dimColor,
                    Size = new Size(440, 40),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                flowTeams.Controls.Add(lblEmpty);
                return;
            }

//             DebugLogger.Log($"[TeamSwitcher] Creating cards for {_teams.Count} teams");
            foreach (var entry in _teams)
            {
                var card = CreateTeamCard(entry);
                flowTeams.Controls.Add(card);
            }
        }

        /// <summary>Creates a UI card for a single team entry.</summary>
        private Panel CreateTeamCard(TeamIndexEntry entry)
        {
//             DebugLogger.Log($"[TeamSwitcher] Creating card for team: {entry.TeamName} ({entry.JoinCode})");
            bool isActive = entry.JoinCode.Equals(_activeCode, StringComparison.OrdinalIgnoreCase);

            var card = new Panel
            {
                Size = new Size(440, 72),
                BackColor = isActive ? activeCard : cardBg,
                Margin = new Padding(0, 0, 0, 6),
                Cursor = Cursors.Hand,
                Tag = entry.JoinCode
            };

            // Active indicator (left border)
            if (isActive)
            {
                var indicator = new Panel
                {
                    Size = new Size(4, 72),
                    Location = new Point(0, 0),
                    BackColor = accentColor
                };
                card.Controls.Add(indicator);
            }

            // Team name
            var lblName = new Label
            {
                Text = entry.TeamName,
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = isActive ? accentColor : fgColor,
                Location = new Point(16, 10),
                AutoSize = true
            };
            card.Controls.Add(lblName);

            // Subtitle: code + user name
            var lblInfo = new Label
            {
                Text = $"Code: {entry.JoinCode}  •  You: {entry.UserName}" + (isActive ? "  •  ACTIVE" : ""),
                Font = new Font("Segoe UI", 9),
                ForeColor = isActive ? successColor : dimColor,
                Location = new Point(16, 40),
                AutoSize = true
            };
            card.Controls.Add(lblInfo);

            // Switch button (only for non-active teams)
            if (!isActive)
            {
                var btnSwitch = new Button
                {
                    Text = "Switch",
                    Size = new Size(70, 30),
                    Location = new Point(290, 20),
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    BackColor = accentColor,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Tag = entry.JoinCode
                };
                btnSwitch.FlatAppearance.BorderSize = 0;
                btnSwitch.Click += OnSwitchTeam;
                card.Controls.Add(btnSwitch);
            }

            // Leave button
            var btnLeave = new Button
            {
                Text = "Leave",
                Size = new Size(60, 30),
                Location = new Point(370, 20),
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(60, 20, 20),
                ForeColor = Color.FromArgb(220, 80, 80),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Tag = entry.JoinCode
            };
            btnLeave.FlatAppearance.BorderSize = 1;
            btnLeave.FlatAppearance.BorderColor = Color.FromArgb(120, 40, 40);
            btnLeave.Click += OnLeaveTeam;
            card.Controls.Add(btnLeave);

            // Hover effect
            card.MouseEnter += (s, e) => { if (!isActive) card.BackColor = cardHover; };
            card.MouseLeave += (s, e) => { if (!isActive) card.BackColor = cardBg; };

            // Click card to switch
            if (!isActive)
            {
                card.Click += (s, e) => OnSwitchTeam(card, e);
                lblName.Click += (s, e) => OnSwitchTeam(card, e);
                lblInfo.Click += (s, e) => OnSwitchTeam(card, e);
            }

            return card;
        }

        /// <summary>Handles team switch button click. Sets active team and closes the dialog.</summary>
        private void OnSwitchTeam(object sender, EventArgs e)
        {
//             DebugLogger.Log("[TeamSwitcher] OnSwitchTeam called");
            string code = null;
            if (sender is Button btn) code = btn.Tag as string;
            else if (sender is Panel pnl) code = pnl.Tag as string;
            else if (sender is Control ctrl) code = ctrl.Parent?.Tag as string;

            if (string.IsNullOrEmpty(code))
            {
//                 DebugLogger.Log("[TeamSwitcher] OnSwitchTeam: No team code found");
                return;
            }

//             DebugLogger.Log($"[TeamSwitcher] Switching to team: {code}");
            bool switched = UserStorage.SwitchTeam(code);
            if (!switched)
            {
                lblError.Text = "Could not switch team. Please try again.";
                lblError.Visible = true;
                return;
            }
            SelectedCode = code;
            JoinedNewTeam = false;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>Handles team leave button click. Removes team from index and updates active team if needed.</summary>
        private void OnLeaveTeam(object sender, EventArgs e)
        {
//             DebugLogger.Log("[TeamSwitcher] OnLeaveTeam called");
            var btn = sender as Button;
            string code = btn?.Tag as string;
            if (string.IsNullOrEmpty(code))
            {
//                 DebugLogger.Log("[TeamSwitcher] OnLeaveTeam: No team code found");
                return;
            }

            var entry = _teams.FirstOrDefault(t => t.JoinCode.Equals(code, StringComparison.OrdinalIgnoreCase));
            string teamName = entry?.TeamName ?? code;
//             DebugLogger.Log($"[TeamSwitcher] User requested to leave team: {teamName} ({code})");

            var result = MessageBox.Show(
                $"Are you sure you want to leave \"{teamName}\"?\n\n" +
                "Your local data for this team will be removed.\n" +
                "You can rejoin later with the invite code.",
                "Leave Team",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != System.Windows.Forms.DialogResult.Yes)
            {
//                 DebugLogger.Log("[TeamSwitcher] User cancelled leave team operation");
                return;
            }

            // Remove from index
//             DebugLogger.Log($"[TeamSwitcher] Removing team from index: {code}");
            UserStorage.RemoveTeamFromIndex(code);
            _teams = UserStorage.GetJoinedTeams();

            // GLOBAL ACCOUNT: Remove team from the user's global account
            string userName = entry?.UserName ?? "";
            if (!string.IsNullOrEmpty(userName))
            {
//                 DebugLogger.Log($"[TeamSwitcher] Removing team from user account: {userName}");
                _ = UserStorage.RemoveTeamFromAccountAsync(userName, code);
            }

            // If we left the active team, switch to another one or clear
            if (code.Equals(_activeCode, StringComparison.OrdinalIgnoreCase))
            {
//                 DebugLogger.Log("[TeamSwitcher] Left the active team, updating active team");
                if (_teams.Count > 0)
                {
                    _activeCode = _teams[0].JoinCode;
                    UserStorage.SetActiveTeamCode(_activeCode);
//                     DebugLogger.Log($"[TeamSwitcher] Set new active team: {_activeCode}");
                }
                else
                {
                    _activeCode = "";
                    UserStorage.SetActiveTeamCode("");
//                     DebugLogger.Log("[TeamSwitcher] No teams left, cleared active team");
                }
            }

            PopulateTeamCards();
        }

        // ══════════════════════════════════════════════════════════
        //  PAGE 2 — Join a New Team
        // ══════════════════════════════════════════════════════════
        /// <summary>Builds the join new team page UI.</summary>
        private void BuildJoinNewPage()
        {
//             DebugLogger.Log("[TeamSwitcher] Building join new team page");
            panelJoinNew = new Panel { Dock = DockStyle.Fill, BackColor = bgColor, Visible = false };

            var lblTitle = new Label
            {
                Text = "🔗  Join Another Team",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = accentColor,
                Location = new Point(20, 20),
                AutoSize = true
            };
            panelJoinNew.Controls.Add(lblTitle);

            // Join Code
            var lblCode = new Label
            {
                Text = "JOIN CODE (from the team admin)",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = dimColor,
                Location = new Point(20, 75),
                AutoSize = true
            };
            panelJoinNew.Controls.Add(lblCode);

            txtJoinCode = new TextBox
            {
                Location = new Point(20, 98),
                Size = new Size(460, 32),
                Font = new Font("Consolas", 16, FontStyle.Bold),
                BackColor = fieldBg,
                ForeColor = fgColor,
                BorderStyle = BorderStyle.FixedSingle,
                MaxLength = 6,
                TextAlign = HorizontalAlignment.Center,
                CharacterCasing = CharacterCasing.Upper
            };
            panelJoinNew.Controls.Add(txtJoinCode);

            // Team info label (shown after code is validated)
            lblJoinTeamInfo = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = successColor,
                Location = new Point(20, 140),
                Size = new Size(460, 25),
                Visible = false
            };
            panelJoinNew.Controls.Add(lblJoinTeamInfo);

            // Show current user name (read-only info — user keeps their identity)
            string currentUserName = _teams.FirstOrDefault()?.UserName ?? "";
            var lblNameInfo = new Label
            {
                Text = $"You will join as:  {currentUserName}",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(34, 197, 94),
                Location = new Point(20, 180),
                Size = new Size(460, 25),
                Visible = !string.IsNullOrEmpty(currentUserName)
            };
            panelJoinNew.Controls.Add(lblNameInfo);

            // Hidden text field — auto-populated with the user's existing name
            txtJoinName = new TextBox { Visible = false };
            txtJoinName.Text = currentUserName;
            panelJoinNew.Controls.Add(txtJoinName);

            // Error
            lblJoinError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = errorColor,
                Location = new Point(20, 220),
                Size = new Size(460, 30),
                Visible = false
            };
            panelJoinNew.Controls.Add(lblJoinError);

            // Join button
            var btnJoin = new Button
            {
                Text = "🔗  Join Team",
                Location = new Point(20, 265),
                Size = new Size(460, 44),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = accentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnJoin.FlatAppearance.BorderSize = 0;
            btnJoin.Click += OnJoinNewTeam;
            panelJoinNew.Controls.Add(btnJoin);

            // Back button
            var btnBack = new Button
            {
                Text = "◀  Back to Team List",
                Location = new Point(20, 325),
                Size = new Size(460, 40),
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = btnBg,
                ForeColor = dimColor,
                Cursor = Cursors.Hand
            };
            btnBack.FlatAppearance.BorderSize = 0;
            btnBack.Click += (s, e) =>
            {
                // Refresh list in case we joined while on this page
                _teams = UserStorage.GetJoinedTeams();
                PopulateTeamCards();
                ShowPage(panelTeamList);
            };
            panelJoinNew.Controls.Add(btnBack);

            this.Controls.Add(panelJoinNew);
        }

        /// <summary>Handles join new team operation. Validates code, looks up team, and adds user to team.</summary>
        private async void OnJoinNewTeam(object sender, EventArgs e)
        {
//             DebugLogger.Log("[TeamSwitcher] OnJoinNewTeam called");
            string joinCode = txtJoinCode.Text.Trim().ToUpper();
            // Use existing user name from their first team (no need to re-enter)
            string name = txtJoinName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                // Fallback: try to get from team index
                var firstTeam = _teams.FirstOrDefault();
                name = firstTeam?.UserName ?? "";
            }

//             DebugLogger.Log($"[TeamSwitcher] Join attempt - Code: {joinCode}, Name: {name}");

            // Validation
            if (joinCode.Length != 6)
            {
                DebugLogger.Log("[TeamSwitcher] Join validation failed: code length != 6");
                ShowJoinError("Join code must be 6 characters");
                return;
            }
            if (string.IsNullOrEmpty(name))
            {
                DebugLogger.Log("[TeamSwitcher] Join validation failed: no user name available");
                ShowJoinError("Could not determine your user name. Please restart the app.");
                return;
            }

            // Check if already joined this team
            if (_teams.Any(t => t.JoinCode.Equals(joinCode, StringComparison.OrdinalIgnoreCase)))
            {
//                 DebugLogger.Log("[TeamSwitcher] User already joined this team");
                ShowJoinError("You have already joined this team. Switch to it from the list.");
                return;
            }

            // Look up team in Firebase
//             DebugLogger.Log($"[TeamSwitcher] Looking up team by code: {joinCode}");
            lblJoinError.Text = "Looking up team...";
            lblJoinError.ForeColor = dimColor;
            lblJoinError.Visible = true;

            var team = await UserStorage.FindTeamByJoinCodeAsync(joinCode);
            if (team == null)
            {
//                 DebugLogger.Log("[TeamSwitcher] Team not found by join code");
                ShowJoinError("Team not found. Check your join code.");
                return;
            }

//             DebugLogger.Log($"[TeamSwitcher] Found team: {team.TeamName} (Admin: {team.AdminName})");

            // CHECK IF USER IS BANNED
            if (team.IsBanned(name))
            {
//                 DebugLogger.Log($"[TeamSwitcher] User {name} is banned from team {team.TeamName}");
                ShowJoinError("\U0001f6ab You have been banned from this team. Contact the admin.");
                return;
            }

            // Show team info
            lblJoinTeamInfo.Text = $"\u2713 Found team: {team.TeamName} (by {team.AdminName})";
            lblJoinTeamInfo.Visible = true;

            // Check if name is in the team's member list
            bool nameInTeam = team.Members.Any(m => m.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (!nameInTeam)
            {
                // Add this person to the team on Firebase
//                 DebugLogger.Log($"[TeamSwitcher] Adding user {name} to Firebase team");
                await UserStorage.AddMemberToFirebaseTeamAsync(joinCode, name);
                team.Members.Add(name);
            }

            // Switch to this new team's folder and save data there
//             DebugLogger.Log($"[TeamSwitcher] Setting active team to {joinCode}");
            UserStorage.SetActiveTeamCode(joinCode);
            UserStorage.SaveTeam(team);

            // Create the user list from team members
//             DebugLogger.Log($"[TeamSwitcher] Creating user list from {team.Members.Count} members");
            var allUsers = new List<UserInfo>();
            foreach (var memberName in team.Members)
            {
                bool isAdmin = memberName.Equals(team.AdminName, StringComparison.OrdinalIgnoreCase);
                allUsers.Add(new UserInfo(memberName, isAdmin, team.JoinCode));
            }
            UserStorage.SaveUsers(allUsers);

            // Add to the teams index
//             DebugLogger.Log("[TeamSwitcher] Adding team to index");
            UserStorage.AddTeamToIndex(joinCode, team.TeamName, name);
            _teams = UserStorage.GetJoinedTeams();

            // GLOBAL ACCOUNT: Add this team to the user's account
//             DebugLogger.Log("[TeamSwitcher] Adding team to user's global account");
            _ = UserStorage.AddTeamToAccountAsync(name, joinCode);

            // WELCOME MESSAGE: Post welcome chat for new member
            if (!nameInTeam)
            {
//                 DebugLogger.Log("[TeamSwitcher] Posting welcome message");
                _ = UserStorage.PostWelcomeChatMessageAsync(joinCode, name);
            }

            // Set result
//             DebugLogger.Log($"[TeamSwitcher] Successfully joined team. Setting result values");
            SelectedCode = joinCode;
            JoinedNewTeam = true;
            NewTeamInfo = team;
            NewTeamUserName = name;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════
        /// <summary>Shows a specific page panel and hides others.</summary>
        private void ShowPage(Panel page)
        {
//             DebugLogger.Log("[TeamSwitcher] Switching page view");
            panelTeamList.Visible = false;
            panelJoinNew.Visible = false;
            page.Visible = true;
            page.BringToFront();
        }

        /// <summary>Displays an error message in the join error label.</summary>
        private void ShowJoinError(string msg)
        {
            DebugLogger.Log($"[TeamSwitcher] Error: {msg}");
            lblJoinError.Text = "❌ " + msg;
            lblJoinError.ForeColor = errorColor;
            lblJoinError.Visible = true;
        }
    }
}
