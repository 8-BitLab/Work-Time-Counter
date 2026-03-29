// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        FormFirstLaunch.cs                                           ║
// ║  PURPOSE:     FIRST-LAUNCH SETUP WIZARD (CREATE, JOIN, OR LOGIN)           ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ║                                                                            ║
// ║  GLOBAL ACCOUNTS:                                                          ║
// ║  Users now create a global account (name + password) stored in Firebase.   ║
// ║  This allows logging in from any device and syncing all teams.             ║
// ║  Firebase path: /accounts/{username_key}/                                  ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    /// <summary>
    /// Shown on the very first launch when no team exists on this machine.
    /// The user can:
    ///   1. Create a new team (Admin) — creates global account + team
    ///   2. Join an existing team (Participant) — creates global account + joins team
    ///   3. Login (Existing Account) — syncs all teams from Firebase
    /// </summary>
    public class FormFirstLaunch : Form
    {
        // Result
        public TeamInfo CreatedTeam { get; private set; }
        public UserInfo CreatedUser { get; private set; }
        public List<UserInfo> AllUsers { get; private set; }

        // ── Shared colors ──
        private readonly Color bgColor = Color.FromArgb(24, 28, 36);
        private readonly Color fgColor = Color.FromArgb(220, 224, 230);
        private readonly Color accentColor = Color.FromArgb(255, 127, 80);
        private readonly Color dimColor = Color.FromArgb(120, 130, 145);
        private readonly Color fieldBg = Color.FromArgb(30, 36, 46);
        private readonly Color btnBg = Color.FromArgb(38, 44, 56);
        private readonly Color errorColor = Color.FromArgb(220, 53, 69);
        private readonly Color successColor = Color.FromArgb(34, 197, 94);
        private readonly Color warnColor = Color.FromArgb(255, 193, 7);

        // ── Page containers ──
        private Panel panelChoice;        // page 0: Create / Join / Login
        private Panel panelAdminSetup;    // page 1a: Admin creates team
        private Panel panelJoinTeam;      // page 1b: Participant joins team
        private Panel panelLogin;         // page 1c: Login with existing account
        private Panel panelAdminMembers;  // page 2a: Admin adds member names
        private Panel panelJoinCodeShow;  // page 3a: Show join code to admin

        // ── Admin setup controls ──
        private TextBox txtAdminName;
        private TextBox txtTeamName;
        private TextBox txtAdminFirebaseUrl;
        private TextBox txtAdminPassword;
        private TextBox txtAdminPasswordConfirm;
        private Label lblAdminError;

        // ── Admin members controls ──
        private TextBox txtMemberName;
        private ListBox listMembers;
        private Button btnAddMember;
        private Button btnRemoveMember;
        private Label lblMembersError;

        // ── Join team controls ──
        private TextBox txtJoinCode;
        private TextBox txtJoinName;
        private TextBox txtJoinPassword;
        private TextBox txtJoinPasswordConfirm;
        private Label lblJoinError;
        private Label lblJoinTeamInfo;

        // ── Login controls ──
        private TextBox txtLoginName;
        private TextBox txtLoginPassword;
        private Label lblLoginError;
        private Label lblLoginStatus;

        // ── Join code display ──
        private Label lblJoinCodeDisplay;

        // ── Internal state ──
        private TeamInfo _pendingTeam;
        private List<string> _pendingMembers = new List<string>();

        public FormFirstLaunch()
        {
//             DebugLogger.Log("[FirstLaunch] Initializing FormFirstLaunch");
            this.Text = "WorkFlow — First Time Setup";
            this.Width = 520;
            this.Height = 680;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = bgColor;
            this.ForeColor = fgColor;

            BuildChoicePage();
            BuildAdminSetupPage();
            BuildAdminMembersPage();
            BuildJoinCodeShowPage();
            BuildJoinTeamPage();
            BuildLoginPage();

            ShowPage(panelChoice);
//             DebugLogger.Log("[FirstLaunch] FormFirstLaunch initialized successfully");
        }

        // ══════════════════════════════════════════════════════════
        //  PAGE 0 — Choose: Create Team / Join Team / Login
        // ══════════════════════════════════════════════════════════
        /// <summary>Builds the initial choice page with three options: Create, Join, or Login.</summary>
        private void BuildChoicePage()
        {
//             DebugLogger.Log("[FirstLaunch] Building choice page");
            panelChoice = new Panel { Dock = DockStyle.Fill, BackColor = bgColor };

            var lblTitle = MakeLabel("\u2442  WorkFlow Monitor", 18, FontStyle.Bold, accentColor);
            lblTitle.Location = new Point(20, 25);
            panelChoice.Controls.Add(lblTitle);

            var lblSub = MakeLabel("First Time Setup", 12, FontStyle.Regular, dimColor);
            lblSub.Location = new Point(22, 63);
            panelChoice.Controls.Add(lblSub);

            var lblDesc = MakeLabel(
                "Welcome! Create a new team, join an existing one, or log in to your account.",
                10, FontStyle.Regular, fgColor);
            lblDesc.Location = new Point(20, 105);
            lblDesc.Size = new Size(460, 40);
            panelChoice.Controls.Add(lblDesc);

            // ── Create Team button ──
            var btnAdmin = new Button
            {
                Text = "\U0001f6e1  Create Team (Admin)",
                Location = new Point(40, 160),
                Size = new Size(420, 70),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                BackColor = accentColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnAdmin.FlatAppearance.BorderSize = 0;
            btnAdmin.Click += (s, e) => ShowPage(panelAdminSetup);
            panelChoice.Controls.Add(btnAdmin);

            var lblAdminHint = MakeLabel(
                "Create a team, add members, and get a join code to share.",
                8, FontStyle.Regular, dimColor);
            lblAdminHint.Location = new Point(40, 236);
            lblAdminHint.Size = new Size(420, 20);
            lblAdminHint.TextAlign = ContentAlignment.TopCenter;
            panelChoice.Controls.Add(lblAdminHint);

            // ── Join Team button ──
            var btnJoin = new Button
            {
                Text = "\U0001f517  Join Existing Team",
                Location = new Point(40, 278),
                Size = new Size(420, 70),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                BackColor = btnBg,
                ForeColor = fgColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnJoin.FlatAppearance.BorderSize = 1;
            btnJoin.FlatAppearance.BorderColor = Color.FromArgb(60, 70, 85);
            btnJoin.Click += (s, e) => ShowPage(panelJoinTeam);
            panelChoice.Controls.Add(btnJoin);

            var lblJoinHint = MakeLabel(
                "Enter a join code and create your account to join a team.",
                8, FontStyle.Regular, dimColor);
            lblJoinHint.Location = new Point(40, 354);
            lblJoinHint.Size = new Size(420, 20);
            lblJoinHint.TextAlign = ContentAlignment.TopCenter;
            panelChoice.Controls.Add(lblJoinHint);

            // ── Login button (existing account) ──
            var btnLogin = new Button
            {
                Text = "\U0001f511  Login (Existing Account)",
                Location = new Point(40, 396),
                Size = new Size(420, 70),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                BackColor = Color.FromArgb(45, 55, 72),
                ForeColor = fgColor,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            btnLogin.FlatAppearance.BorderSize = 1;
            btnLogin.FlatAppearance.BorderColor = accentColor;
            btnLogin.Click += (s, e) => ShowPage(panelLogin);
            panelChoice.Controls.Add(btnLogin);

            var lblLoginHint = MakeLabel(
                "Already have an account? Log in to sync all your teams.",
                8, FontStyle.Regular, dimColor);
            lblLoginHint.Location = new Point(40, 472);
            lblLoginHint.Size = new Size(420, 20);
            lblLoginHint.TextAlign = ContentAlignment.TopCenter;
            panelChoice.Controls.Add(lblLoginHint);

            this.Controls.Add(panelChoice);
        }

        // ══════════════════════════════════════════════════════════
        //  PAGE 1a — Admin: Enter your name + team name + password
        //  Also creates a global account in Firebase
        // ══════════════════════════════════════════════════════════
        /// <summary>Builds the admin setup page for team creation with account details.</summary>
        private void BuildAdminSetupPage()
        {
//             DebugLogger.Log("[FirstLaunch] Building admin setup page");
            panelAdminSetup = new Panel { Dock = DockStyle.Fill, BackColor = bgColor, Visible = false };

            var lblTitle = MakeLabel("\U0001f6e1  Create Your Team", 16, FontStyle.Bold, accentColor);
            lblTitle.Location = new Point(20, 20);
            panelAdminSetup.Controls.Add(lblTitle);

            var lblAccountHint = MakeLabel(
                "This will also create your global account for cross-device login.",
                9, FontStyle.Italic, dimColor);
            lblAccountHint.Location = new Point(20, 50);
            lblAccountHint.Size = new Size(460, 20);
            panelAdminSetup.Controls.Add(lblAccountHint);

            // Your Name
            panelAdminSetup.Controls.Add(MakeSectionLabel("YOUR NAME", 20, 80));
            txtAdminName = MakeTextBox(20, 103, false);
            panelAdminSetup.Controls.Add(txtAdminName);

            // Team Name
            panelAdminSetup.Controls.Add(MakeSectionLabel("TEAM NAME", 20, 150));
            txtTeamName = MakeTextBox(20, 173, false);
            panelAdminSetup.Controls.Add(txtTeamName);

            // Optional team Firebase URL
            panelAdminSetup.Controls.Add(MakeSectionLabel("TEAM FIREBASE URL (optional)", 20, 220));
            txtAdminFirebaseUrl = MakeTextBox(20, 243, false);
            txtAdminFirebaseUrl.Text = "https://your-project.firebasedatabase.app";
            txtAdminFirebaseUrl.ForeColor = dimColor;
            txtAdminFirebaseUrl.GotFocus += (s, ev) =>
            {
                if (txtAdminFirebaseUrl.Text == "https://your-project.firebasedatabase.app")
                {
                    txtAdminFirebaseUrl.Text = "";
                    txtAdminFirebaseUrl.ForeColor = fgColor;
                }
            };
            txtAdminFirebaseUrl.LostFocus += (s, ev) =>
            {
                if (string.IsNullOrWhiteSpace(txtAdminFirebaseUrl.Text))
                {
                    txtAdminFirebaseUrl.Text = "https://your-project.firebasedatabase.app";
                    txtAdminFirebaseUrl.ForeColor = dimColor;
                }
            };
            panelAdminSetup.Controls.Add(txtAdminFirebaseUrl);

            // Password
            panelAdminSetup.Controls.Add(MakeSectionLabel("YOUR PASSWORD (min 6 chars)", 20, 290));
            txtAdminPassword = MakeTextBox(20, 313, true);
            panelAdminSetup.Controls.Add(txtAdminPassword);

            // Confirm Password
            panelAdminSetup.Controls.Add(MakeSectionLabel("CONFIRM PASSWORD", 20, 360));
            txtAdminPasswordConfirm = MakeTextBox(20, 383, true);
            panelAdminSetup.Controls.Add(txtAdminPasswordConfirm);

            // Error
            lblAdminError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = errorColor,
                Location = new Point(20, 428),
                Size = new Size(460, 40),
                Visible = false
            };
            panelAdminSetup.Controls.Add(lblAdminError);

            // Next button
            var btnNext = MakeButton("Next \u2014 Add Team Members  \u279C", accentColor, Color.White, 20, 480);
            btnNext.Click += OnAdminSetupNext;
            panelAdminSetup.Controls.Add(btnNext);

            // Back button
            var btnBack = MakeButton("\u25C0  Back", btnBg, dimColor, 20, 535);
            btnBack.Size = new Size(200, 40);
            btnBack.Click += (s, e) => ShowPage(panelChoice);
            panelAdminSetup.Controls.Add(btnBack);

            this.Controls.Add(panelAdminSetup);
        }

        /// <summary>Validates admin setup inputs and moves to member setup page.</summary>
        private async void OnAdminSetupNext(object sender, EventArgs e)
        {
//             DebugLogger.Log("[FirstLaunch] OnAdminSetupNext called");
            string adminName = txtAdminName.Text.Trim();
            string teamName = txtTeamName.Text.Trim();
            string teamFirebaseUrl = txtAdminFirebaseUrl.Text.Trim();
            string pw = txtAdminPassword.Text;
            string pw2 = txtAdminPasswordConfirm.Text;
            if (teamFirebaseUrl == "https://your-project.firebasedatabase.app")
                teamFirebaseUrl = "";

//             DebugLogger.Log($"[FirstLaunch] Admin setup: name={adminName}, team={teamName}");

            // Validation
            if (string.IsNullOrEmpty(adminName))
            {
                DebugLogger.Log("[FirstLaunch] Validation failed: empty admin name");
                ShowError(lblAdminError, "Enter your name");
                return;
            }
            if (string.IsNullOrEmpty(teamName))
            {
                DebugLogger.Log("[FirstLaunch] Validation failed: empty team name");
                ShowError(lblAdminError, "Enter a team name");
                return;
            }
            if (pw.Length < 6)
            {
                DebugLogger.Log("[FirstLaunch] Validation failed: password too short");
                ShowError(lblAdminError, "Password must be at least 6 characters");
                return;
            }
            if (pw != pw2)
            {
                DebugLogger.Log("[FirstLaunch] Validation failed: passwords don't match");
                ShowError(lblAdminError, "Passwords do not match");
                return;
            }
            if (!string.IsNullOrWhiteSpace(teamFirebaseUrl) &&
                !teamFirebaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                ShowError(lblAdminError, "Firebase URL must start with https://");
                return;
            }

            // Try to register global account (or check if it already exists)
//             DebugLogger.Log($"[FirstLaunch] Creating global account for {adminName}");
            lblAdminError.Text = "Creating account...";
            lblAdminError.ForeColor = dimColor;
            lblAdminError.Visible = true;

            // Hash the password for the global account
            string pwHash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(pw));
                pwHash = Convert.ToBase64String(bytes);
            }

            // Check if account already exists
            var existingAccount = await UserStorage.GetAccountAsync(adminName);
            if (existingAccount != null)
            {
                // Account exists — verify password matches
                if (existingAccount.PasswordHash != pwHash)
                {
                    ShowError(lblAdminError, "Username already taken. Use a different name or login with your existing password.");
                    return;
                }
                // Same password — proceed (user is re-creating a team with their existing account)
            }
            else
            {
                // Register new global account
                bool registered = await UserStorage.RegisterAccountAsync(adminName, pwHash);
                if (!registered)
                {
                    ShowError(lblAdminError, "Could not create account. Check your connection.");
                    return;
                }
            }

            lblAdminError.Visible = false;

            // Create team
            _pendingTeam = new TeamInfo(teamName, adminName);
            _pendingTeam.CustomFirebaseUrl = teamFirebaseUrl;
            _pendingMembers.Clear();
            _pendingMembers.Add(adminName); // admin is always a member
            listMembers.Items.Clear();
            listMembers.Items.Add($"{adminName}  (Admin)");

            ShowPage(panelAdminMembers);
        }

        // ══════════════════════════════════════════════════════════
        //  PAGE 2a — Admin: Add team member names
        // ══════════════════════════════════════════════════════════
        /// <summary>Builds the page for admin to add team member names.</summary>
        private void BuildAdminMembersPage()
        {
//             DebugLogger.Log("[FirstLaunch] Building admin members page");
            panelAdminMembers = new Panel { Dock = DockStyle.Fill, BackColor = bgColor, Visible = false };

            var lblTitle = MakeLabel("\U0001f465  Add Team Members", 16, FontStyle.Bold, accentColor);
            lblTitle.Location = new Point(20, 20);
            panelAdminMembers.Controls.Add(lblTitle);

            var lblHint = MakeLabel(
                "Add the names of people who will use this app.\nThey will log in with default password 111111 and change it on first use.",
                9, FontStyle.Regular, dimColor);
            lblHint.Location = new Point(20, 55);
            lblHint.Size = new Size(460, 36);
            panelAdminMembers.Controls.Add(lblHint);

            // Name input + Add button
            txtMemberName = MakeTextBox(20, 105, false);
            txtMemberName.Size = new Size(340, 32);
            txtMemberName.KeyPress += (s, e) => { if (e.KeyChar == (char)Keys.Enter) { e.Handled = true; AddMember(); } };
            panelAdminMembers.Controls.Add(txtMemberName);

            btnAddMember = MakeButton("+ Add", accentColor, Color.White, 370, 103);
            btnAddMember.Size = new Size(110, 34);
            btnAddMember.Click += (s, e) => AddMember();
            panelAdminMembers.Controls.Add(btnAddMember);

            // Members list
            listMembers = new ListBox
            {
                Location = new Point(20, 150),
                Size = new Size(340, 200),
                Font = new Font("Segoe UI", 11),
                BackColor = fieldBg,
                ForeColor = fgColor,
                BorderStyle = BorderStyle.FixedSingle
            };
            panelAdminMembers.Controls.Add(listMembers);

            // Remove button
            btnRemoveMember = MakeButton("Remove Selected", Color.FromArgb(180, 50, 50), Color.White, 370, 150);
            btnRemoveMember.Size = new Size(110, 34);
            btnRemoveMember.Click += (s, e) => RemoveMember();
            panelAdminMembers.Controls.Add(btnRemoveMember);

            // Error
            lblMembersError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = errorColor,
                Location = new Point(20, 360),
                Size = new Size(460, 20),
                Visible = false
            };
            panelAdminMembers.Controls.Add(lblMembersError);

            // Done button
            var btnDone = MakeButton("\u2713  Create Team & Get Join Code", accentColor, Color.White, 20, 395);
            btnDone.Click += OnAdminMembersDone;
            panelAdminMembers.Controls.Add(btnDone);

            // Back button
            var btnBack = MakeButton("\u25C0  Back", btnBg, dimColor, 20, 450);
            btnBack.Size = new Size(200, 40);
            btnBack.Click += (s, e) => ShowPage(panelAdminSetup);
            panelAdminMembers.Controls.Add(btnBack);

            this.Controls.Add(panelAdminMembers);
        }

        /// <summary>Adds a member name to the team members list.</summary>
        private void AddMember()
        {
//             DebugLogger.Log("[FirstLaunch] AddMember called");
            string name = txtMemberName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                DebugLogger.Log("[FirstLaunch] AddMember failed: empty name");
                ShowError(lblMembersError, "Enter a member name");
                return;
            }
            if (_pendingMembers.Any(m => m.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                DebugLogger.Log($"[FirstLaunch] AddMember failed: {name} already added");
                ShowError(lblMembersError, "Member already added");
                return;
            }

//             DebugLogger.Log($"[FirstLaunch] Adding member: {name}");
            _pendingMembers.Add(name);
            listMembers.Items.Add(name);
            txtMemberName.Clear();
            txtMemberName.Focus();
            lblMembersError.Visible = false;
        }

        /// <summary>Removes a selected member from the team members list.</summary>
        private void RemoveMember()
        {
//             DebugLogger.Log("[FirstLaunch] RemoveMember called");
            if (listMembers.SelectedIndex < 0) return;
            string selected = listMembers.SelectedItem.ToString();
            // Don't allow removing the admin
            if (selected.Contains("(Admin)"))
            {
                DebugLogger.Log("[FirstLaunch] RemoveMember failed: cannot remove admin");
                ShowError(lblMembersError, "Cannot remove the admin");
                return;
            }
//             DebugLogger.Log($"[FirstLaunch] Removing member: {selected}");
            _pendingMembers.Remove(selected);
            listMembers.Items.RemoveAt(listMembers.SelectedIndex);
        }

        /// <summary>Finalizes team creation: saves to Firebase, creates user list, shows join code.</summary>
        private async void OnAdminMembersDone(object sender, EventArgs e)
        {
//             DebugLogger.Log("[FirstLaunch] OnAdminMembersDone called");
            if (_pendingMembers.Count < 1)
            {
                DebugLogger.Log("[FirstLaunch] Team creation failed: no members");
                ShowError(lblMembersError, "Add at least one member");
                return;
            }

            // Finalize team
            _pendingTeam.Members = new List<string>(_pendingMembers);
//             DebugLogger.Log($"[FirstLaunch] Finalizing team: {_pendingTeam.TeamName} with {_pendingMembers.Count} members");

            // MULTI-TEAM: Set this team as active BEFORE saving (so files go to correct subfolder)
            UserStorage.SetActiveTeamCode(_pendingTeam.JoinCode);
            UserStorage.ClearTeamLocalRuntimeCache(_pendingTeam.JoinCode);

            // Save to Firebase so participants can find the team
//             DebugLogger.Log($"[FirstLaunch] Saving team to Firebase: {_pendingTeam.JoinCode}");
            await UserStorage.SaveTeamToFirebaseAsync(_pendingTeam);

            // Save locally (now goes to teams/{JoinCode}/team.bit)
//             DebugLogger.Log("[FirstLaunch] Saving team locally");
            UserStorage.SaveTeam(_pendingTeam);

            // Create user list — admin gets custom password, others get default
//             DebugLogger.Log("[FirstLaunch] Creating user list");
            AllUsers = new List<UserInfo>();
            foreach (var name in _pendingTeam.Members)
            {
                bool isAdmin = name.Equals(_pendingTeam.AdminName, StringComparison.OrdinalIgnoreCase);
                var user = new UserInfo(name, isAdmin, _pendingTeam.JoinCode);

                if (isAdmin)
                {
                    // Admin already set a real password
//                     DebugLogger.Log($"[FirstLaunch] Setting custom password for admin {name}");
                    user.SetPassword(txtAdminPassword.Text);
                    user.IsDefaultPassword = false;
                }
                AllUsers.Add(user);
            }
            UserStorage.SaveUsers(AllUsers);

            // MULTI-TEAM: Register this team in the index
            string adminName = _pendingTeam.AdminName;
//             DebugLogger.Log($"[FirstLaunch] Adding team to index for {adminName}");
            UserStorage.AddTeamToIndex(_pendingTeam.JoinCode, _pendingTeam.TeamName, adminName);

            // GLOBAL ACCOUNT: Add this team to the admin's global account
//             DebugLogger.Log("[FirstLaunch] Adding team to global account");
            await UserStorage.AddTeamToAccountAsync(adminName, _pendingTeam.JoinCode);

            CreatedTeam = _pendingTeam;
            CreatedUser = AllUsers.First(u => u.IsAdmin);
//             DebugLogger.Log($"[FirstLaunch] Team creation complete. Join code: {_pendingTeam.JoinCode}");

            // Show join code
            lblJoinCodeDisplay.Text = _pendingTeam.JoinCode;
            ShowPage(panelJoinCodeShow);
        }

        // ══════════════════════════════════════════════════════════
        //  PAGE 3a — Admin: Show the join code + Share via Email
        // ══════════════════════════════════════════════════════════
        /// <summary>Builds the page that displays the team join code.</summary>
        private void BuildJoinCodeShowPage()
        {
//             DebugLogger.Log("[FirstLaunch] Building join code display page");
            panelJoinCodeShow = new Panel { Dock = DockStyle.Fill, BackColor = bgColor, Visible = false };

            var lblTitle = MakeLabel("\u2713  Team Created!", 18, FontStyle.Bold, successColor);
            lblTitle.Location = new Point(20, 30);
            panelJoinCodeShow.Controls.Add(lblTitle);

            var lblInfo = MakeLabel(
                "Share this join code with your team members.\nThey will enter it on their first launch to join your team.",
                10, FontStyle.Regular, fgColor);
            lblInfo.Location = new Point(20, 80);
            lblInfo.Size = new Size(460, 50);
            panelJoinCodeShow.Controls.Add(lblInfo);

            panelJoinCodeShow.Controls.Add(MakeSectionLabel("JOIN CODE", 20, 150));

            lblJoinCodeDisplay = new Label
            {
                Text = "------",
                Font = new Font("Consolas", 36, FontStyle.Bold),
                ForeColor = accentColor,
                Location = new Point(20, 178),
                Size = new Size(460, 60),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = fieldBg,
                BorderStyle = BorderStyle.FixedSingle
            };
            panelJoinCodeShow.Controls.Add(lblJoinCodeDisplay);

            // Copy button
            var btnCopy = MakeButton("\U0001f4cb  Copy to Clipboard", btnBg, fgColor, 30, 260);
            btnCopy.Size = new Size(210, 40);
            btnCopy.Click += (s, e) =>
            {
                Clipboard.SetText(lblJoinCodeDisplay.Text);
                btnCopy.Text = "\u2713  Copied!";
            };
            panelJoinCodeShow.Controls.Add(btnCopy);

            // ── Share via Email button ──
            var btnEmail = MakeButton("\u2709  Share via Email", Color.FromArgb(52, 120, 200), Color.White, 260, 260);
            btnEmail.Size = new Size(210, 40);
            btnEmail.Click += (s, e) =>
            {
                string code = lblJoinCodeDisplay.Text;
                string teamName = _pendingTeam?.TeamName ?? "our team";
                string subject = Uri.EscapeDataString($"Join {teamName} on WorkFlow Monitor");
                string body = Uri.EscapeDataString(
                    $"Hi!\n\n" +
                    $"You've been invited to join \"{teamName}\" on WorkFlow Monitor.\n\n" +
                    $"Your Join Code: {code}\n\n" +
                    $"Steps:\n" +
                    $"1. Download WorkFlow Monitor\n" +
                    $"2. Click \"Join Existing Team\"\n" +
                    $"3. Enter the join code: {code}\n" +
                    $"4. Create your account with your name and password\n\n" +
                    $"See you there!");
                string mailto = $"mailto:?subject={subject}&body={body}";
                try { Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true }); }
                catch { }
            };
            panelJoinCodeShow.Controls.Add(btnEmail);

            var lblPassHint = MakeLabel(
                "Team members will create their own account when they join.\nDefault password for pre-added members: 111111",
                9, FontStyle.Regular, warnColor);
            lblPassHint.Location = new Point(20, 318);
            lblPassHint.Size = new Size(460, 36);
            lblPassHint.TextAlign = ContentAlignment.TopCenter;
            panelJoinCodeShow.Controls.Add(lblPassHint);

            // Start button
            var btnStart = MakeButton("\U0001f680  Start WorkFlow", accentColor, Color.White, 100, 380);
            btnStart.Size = new Size(300, 54);
            btnStart.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            btnStart.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            panelJoinCodeShow.Controls.Add(btnStart);

            this.Controls.Add(panelJoinCodeShow);
        }

        // ══════════════════════════════════════════════════════════
        //  PAGE 1b — Participant: Join code + Create Account
        //  Now requires name + password (creates global account)
        // ══════════════════════════════════════════════════════════
        /// <summary>Builds the page for users to join an existing team.</summary>
        private void BuildJoinTeamPage()
        {
//             DebugLogger.Log("[FirstLaunch] Building join team page");
            panelJoinTeam = new Panel { Dock = DockStyle.Fill, BackColor = bgColor, Visible = false };

            var lblTitle = MakeLabel("\U0001f517  Join a Team", 16, FontStyle.Bold, accentColor);
            lblTitle.Location = new Point(20, 15);
            panelJoinTeam.Controls.Add(lblTitle);

            // Join Code
            panelJoinTeam.Controls.Add(MakeSectionLabel("JOIN CODE (from your admin)", 20, 55));
            txtJoinCode = MakeTextBox(20, 78, false);
            txtJoinCode.Font = new Font("Consolas", 16, FontStyle.Bold);
            txtJoinCode.MaxLength = 6;
            txtJoinCode.TextAlign = HorizontalAlignment.Center;
            txtJoinCode.CharacterCasing = CharacterCasing.Upper;
            panelJoinTeam.Controls.Add(txtJoinCode);

            // Team info label (shown after code is validated)
            lblJoinTeamInfo = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = successColor,
                Location = new Point(20, 120),
                Size = new Size(460, 25),
                Visible = false
            };
            panelJoinTeam.Controls.Add(lblJoinTeamInfo);

            // Your Name
            panelJoinTeam.Controls.Add(MakeSectionLabel("YOUR NAME (this is your account name)", 20, 155));
            txtJoinName = MakeTextBox(20, 178, false);
            panelJoinTeam.Controls.Add(txtJoinName);

            // Password
            panelJoinTeam.Controls.Add(MakeSectionLabel("CREATE PASSWORD (min 6 chars)", 20, 225));
            txtJoinPassword = MakeTextBox(20, 248, true);
            panelJoinTeam.Controls.Add(txtJoinPassword);

            // Confirm Password
            panelJoinTeam.Controls.Add(MakeSectionLabel("CONFIRM PASSWORD", 20, 295));
            txtJoinPasswordConfirm = MakeTextBox(20, 318, true);
            panelJoinTeam.Controls.Add(txtJoinPasswordConfirm);

            var lblPassHint = MakeLabel(
                "Your account will be saved so you can log in from any device.",
                9, FontStyle.Italic, dimColor);
            lblPassHint.Location = new Point(20, 360);
            lblPassHint.Size = new Size(460, 20);
            panelJoinTeam.Controls.Add(lblPassHint);

            // Error
            lblJoinError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = errorColor,
                Location = new Point(20, 385),
                Size = new Size(460, 40),
                Visible = false
            };
            panelJoinTeam.Controls.Add(lblJoinError);

            // Join button
            var btnJoin = MakeButton("\U0001f4e5  Create Account & Join Team", accentColor, Color.White, 20, 430);
            btnJoin.Click += OnJoinTeam;
            panelJoinTeam.Controls.Add(btnJoin);

            // Back button
            var btnBack = MakeButton("\u25C0  Back", btnBg, dimColor, 20, 485);
            btnBack.Size = new Size(200, 40);
            btnBack.Click += (s, e) => ShowPage(panelChoice);
            panelJoinTeam.Controls.Add(btnBack);

            this.Controls.Add(panelJoinTeam);
        }

        /// <summary>Handles team join: validates inputs, creates account, adds user to team.</summary>
        private async void OnJoinTeam(object sender, EventArgs e)
        {
//             DebugLogger.Log("[FirstLaunch] OnJoinTeam called");
            string joinCode = txtJoinCode.Text.Trim().ToUpper();
            string name = txtJoinName.Text.Trim();
            string pw = txtJoinPassword.Text;
            string pw2 = txtJoinPasswordConfirm.Text;

//             DebugLogger.Log($"[FirstLaunch] Join attempt - Code: {joinCode}, Name: {name}");

            // Validation
            if (joinCode.Length != 6)
            {
                DebugLogger.Log("[FirstLaunch] Join validation failed: code length != 6");
                ShowError(lblJoinError, "Join code must be 6 characters");
                return;
            }
            if (string.IsNullOrEmpty(name))
            {
                DebugLogger.Log("[FirstLaunch] Join validation failed: empty name");
                ShowError(lblJoinError, "Enter your name");
                return;
            }
            if (pw.Length < 6)
            {
                DebugLogger.Log("[FirstLaunch] Join validation failed: password too short");
                ShowError(lblJoinError, "Password must be at least 6 characters");
                return;
            }
            if (pw != pw2)
            {
                DebugLogger.Log("[FirstLaunch] Join validation failed: passwords don't match");
                ShowError(lblJoinError, "Passwords do not match");
                return;
            }

            // Hash the password
//             DebugLogger.Log("[FirstLaunch] Hashing password for account creation");
            string pwHash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(pw));
                pwHash = Convert.ToBase64String(bytes);
            }

            // Try to register or verify existing global account
//             DebugLogger.Log($"[FirstLaunch] Creating/verifying global account for {name}");
            lblJoinError.Text = "Creating account...";
            lblJoinError.ForeColor = dimColor;
            lblJoinError.Visible = true;

            var existingAccount = await UserStorage.GetAccountAsync(name);
            if (existingAccount != null)
            {
//                 DebugLogger.Log($"[FirstLaunch] Account exists for {name}, verifying password");
                // Account exists — verify password
                if (existingAccount.PasswordHash != pwHash)
                {
//                     DebugLogger.Log("[FirstLaunch] Password mismatch for existing account");
                    ShowError(lblJoinError, "Username already taken. Use a different name or login with your existing password.");
                    return;
                }
            }
            else
            {
//                 DebugLogger.Log($"[FirstLaunch] Registering new account: {name}");
                bool registered = await UserStorage.RegisterAccountAsync(name, pwHash);
                if (!registered)
                {
                    DebugLogger.Log("[FirstLaunch] Account registration failed");
                    ShowError(lblJoinError, "Could not create account. Check your connection.");
                    return;
                }
            }

            // Look up team in Firebase
//             DebugLogger.Log($"[FirstLaunch] Looking up team by code: {joinCode}");
            lblJoinError.Text = "Looking up team...";
            lblJoinError.ForeColor = dimColor;

            var team = await UserStorage.FindTeamByJoinCodeAsync(joinCode);
            if (team == null)
            {
//                 DebugLogger.Log("[FirstLaunch] Team not found by join code");
                ShowError(lblJoinError, "Team not found. Check your join code.");
                return;
            }

//             DebugLogger.Log($"[FirstLaunch] Found team: {team.TeamName} (Admin: {team.AdminName})");

            // CHECK IF USER IS BANNED
            if (team.IsBanned(name))
            {
//                 DebugLogger.Log($"[FirstLaunch] User {name} is banned from team");
                ShowError(lblJoinError, "\U0001f6ab You have been banned from this team. Contact the admin.");
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
//                 DebugLogger.Log($"[FirstLaunch] Adding {name} to Firebase team");
                await UserStorage.AddMemberToFirebaseTeamAsync(joinCode, name);
                team.Members.Add(name);
            }

            // MULTI-TEAM: Set this team as active BEFORE saving
//             DebugLogger.Log($"[FirstLaunch] Setting active team: {joinCode}");
            UserStorage.SetActiveTeamCode(team.JoinCode);

            // Save team locally (now goes to teams/{JoinCode}/team.bit)
//             DebugLogger.Log("[FirstLaunch] Saving team locally");
            UserStorage.SaveTeam(team);

            // Create the user list from team members
//             DebugLogger.Log("[FirstLaunch] Creating user list from team members");
            AllUsers = new List<UserInfo>();
            foreach (var memberName in team.Members)
            {
                bool isAdmin = memberName.Equals(team.AdminName, StringComparison.OrdinalIgnoreCase);

                if (memberName.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    // The joining user — set their real password
//                     DebugLogger.Log($"[FirstLaunch] Creating user entry for joining user: {memberName}");
                    var user = new UserInfo(memberName, pwHash, false, isAdmin, team.JoinCode);
                    if (team.MembersMeta != null && team.MembersMeta.ContainsKey(memberName))
                    {
                        var meta = team.MembersMeta[memberName];
                        user.Color = meta.Color;
                        user.Title = meta.Title;
                        user.Role = meta.Role;
                        if (!string.IsNullOrEmpty(meta.Country))
                            user.Country = meta.Country;
                        if (meta.WeeklyHourLimit > 0)
                            user.WeeklyHourLimit = meta.WeeklyHourLimit;
                    }
                    AllUsers.Add(user);
                }
                else
                {
//                     DebugLogger.Log($"[FirstLaunch] Creating user entry for team member: {memberName}");
                    var otherUser = new UserInfo(memberName, isAdmin, team.JoinCode);
                    // Apply metadata so other members' local time & progress are visible
                    if (team.MembersMeta != null && team.MembersMeta.ContainsKey(memberName))
                    {
                        var meta = team.MembersMeta[memberName];
                        otherUser.Color = meta.Color;
                        otherUser.Title = meta.Title;
                        otherUser.Role = meta.Role;
                        if (!string.IsNullOrEmpty(meta.Country))
                            otherUser.Country = meta.Country;
                        if (meta.WeeklyHourLimit > 0)
                            otherUser.WeeklyHourLimit = meta.WeeklyHourLimit;
                    }
                    AllUsers.Add(otherUser);
                }
            }
            UserStorage.SaveUsers(AllUsers);

            // MULTI-TEAM: Register this team in the index
//             DebugLogger.Log("[FirstLaunch] Adding team to index");
            UserStorage.AddTeamToIndex(team.JoinCode, team.TeamName, name);

            // GLOBAL ACCOUNT: Add this team to the user's global account
//             DebugLogger.Log("[FirstLaunch] Adding team to user's global account");
            await UserStorage.AddTeamToAccountAsync(name, team.JoinCode);

            // WELCOME MESSAGE: Post a welcome chat message for the new member
            if (!nameInTeam)
            {
//                 DebugLogger.Log("[FirstLaunch] Posting welcome message");
                await UserStorage.PostWelcomeChatMessageAsync(joinCode, name);
            }

            // The joining user is the current user
//             DebugLogger.Log($"[FirstLaunch] Join complete for user: {name}");
            CreatedUser = AllUsers.First(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            CreatedTeam = team;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // ══════════════════════════════════════════════════════════
        //  PAGE 1c — Login: Existing Account (sync all teams)
        // ══════════════════════════════════════════════════════════
        /// <summary>Builds the login page for existing accounts to sync teams.</summary>
        private void BuildLoginPage()
        {
//             DebugLogger.Log("[FirstLaunch] Building login page");
            panelLogin = new Panel { Dock = DockStyle.Fill, BackColor = bgColor, Visible = false };

            var lblTitle = MakeLabel("\U0001f511  Login to Your Account", 16, FontStyle.Bold, accentColor);
            lblTitle.Location = new Point(20, 20);
            panelLogin.Controls.Add(lblTitle);

            var lblHint = MakeLabel(
                "Enter your account name and password to sync all your teams\nfrom any device.",
                10, FontStyle.Regular, fgColor);
            lblHint.Location = new Point(20, 55);
            lblHint.Size = new Size(460, 45);
            panelLogin.Controls.Add(lblHint);

            // Name
            panelLogin.Controls.Add(MakeSectionLabel("YOUR NAME", 20, 115));
            txtLoginName = MakeTextBox(20, 138, false);
            panelLogin.Controls.Add(txtLoginName);

            // Password
            panelLogin.Controls.Add(MakeSectionLabel("PASSWORD", 20, 185));
            txtLoginPassword = MakeTextBox(20, 208, true);
            panelLogin.Controls.Add(txtLoginPassword);

            // Status label (for sync progress)
            lblLoginStatus = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                ForeColor = dimColor,
                Location = new Point(20, 260),
                Size = new Size(460, 60),
                Visible = false
            };
            panelLogin.Controls.Add(lblLoginStatus);

            // Error
            lblLoginError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = errorColor,
                Location = new Point(20, 330),
                Size = new Size(460, 40),
                Visible = false
            };
            panelLogin.Controls.Add(lblLoginError);

            // Login button
            var btnLogin = MakeButton("\U0001f512  Login & Sync Teams", accentColor, Color.White, 20, 380);
            btnLogin.Click += OnLogin;
            panelLogin.Controls.Add(btnLogin);

            // Back button
            var btnBack = MakeButton("\u25C0  Back", btnBg, dimColor, 20, 435);
            btnBack.Size = new Size(200, 40);
            btnBack.Click += (s, e) => ShowPage(panelChoice);
            panelLogin.Controls.Add(btnBack);

            this.Controls.Add(panelLogin);
        }

        /// <summary>Handles login: verifies account and syncs all teams from Firebase.</summary>
        private async void OnLogin(object sender, EventArgs e)
        {
//             DebugLogger.Log("[FirstLaunch] OnLogin called");
            string name = txtLoginName.Text.Trim();
            string pw = txtLoginPassword.Text;

//             DebugLogger.Log($"[FirstLaunch] Login attempt for account: {name}");

            // Validation
            if (string.IsNullOrEmpty(name))
            {
                DebugLogger.Log("[FirstLaunch] Login validation failed: empty name");
                ShowError(lblLoginError, "Enter your name");
                return;
            }
            if (string.IsNullOrEmpty(pw))
            {
                DebugLogger.Log("[FirstLaunch] Login validation failed: empty password");
                ShowError(lblLoginError, "Enter your password");
                return;
            }

            // Hash the password
//             DebugLogger.Log("[FirstLaunch] Hashing password for login");
            string pwHash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(pw));
                pwHash = Convert.ToBase64String(bytes);
            }

            // Show progress
            lblLoginStatus.Text = "Verifying account...";
            lblLoginStatus.ForeColor = dimColor;
            lblLoginStatus.Visible = true;
            lblLoginError.Visible = false;

            // Try to login
//             DebugLogger.Log("[FirstLaunch] Verifying account credentials");
            var account = await UserStorage.LoginAccountAsync(name, pwHash);
            if (account == null)
            {
                DebugLogger.Log("[FirstLaunch] Login failed: invalid credentials");
                lblLoginStatus.Visible = false;
                ShowError(lblLoginError, "Invalid name or password. Check your credentials.");
                return;
            }

//             DebugLogger.Log($"[FirstLaunch] Account verified. Teams count: {account.Teams.Count}");

            // Account verified — sync all teams
            string displayName = account.DisplayName ?? name;
            lblLoginStatus.Text = $"Welcome back, {displayName}!\nSyncing {account.Teams.Count} team(s)...";
            lblLoginStatus.ForeColor = successColor;

            if (account.Teams.Count == 0)
            {
//                 DebugLogger.Log("[FirstLaunch] No teams associated with account");
                lblLoginStatus.Text = $"Welcome back, {displayName}!\nNo teams found. You can create or join a team.";
                lblLoginStatus.ForeColor = warnColor;
                ShowError(lblLoginError, "No teams associated with this account. Use Create Team or Join Team.");
                return;
            }

            // Sync all teams from Firebase to local
//             DebugLogger.Log("[FirstLaunch] Syncing all teams from Firebase");
            var syncedTeams = await UserStorage.SyncAccountTeamsAsync(displayName, pwHash);

            if (syncedTeams.Count == 0)
            {
                DebugLogger.Log("[FirstLaunch] Team sync failed: no valid teams");
                lblLoginStatus.ForeColor = warnColor;
                lblLoginStatus.Text = $"Could not sync any teams. They may have been deleted.";
                ShowError(lblLoginError, "No valid teams could be synced. Try creating or joining a team.");
                return;
            }

//             DebugLogger.Log($"[FirstLaunch] Successfully synced {syncedTeams.Count} team(s)");

            // Set the first synced team as active
            var firstTeam = syncedTeams[0];
            UserStorage.SetActiveTeamCode(firstTeam.JoinCode);
//             DebugLogger.Log($"[FirstLaunch] Set active team: {firstTeam.JoinCode}");

            lblLoginStatus.Text = $"Synced {syncedTeams.Count} team(s) successfully!";
            lblLoginStatus.ForeColor = successColor;

            // Load users for the active team
            AllUsers = UserStorage.LoadUsers();

            // Find the current user in the team
            CreatedUser = AllUsers.FirstOrDefault(u =>
                u.Name.Equals(displayName, StringComparison.OrdinalIgnoreCase) ||
                u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            CreatedTeam = firstTeam;

            // If user not found in list (edge case), create entry
            if (CreatedUser == null)
            {
//                 DebugLogger.Log($"[FirstLaunch] User {displayName} not found in team, creating entry");
                bool isAdmin = displayName.Equals(firstTeam.AdminName, StringComparison.OrdinalIgnoreCase);
                CreatedUser = new UserInfo(displayName, pwHash, false, isAdmin, firstTeam.JoinCode);
                AllUsers.Add(CreatedUser);
                UserStorage.SaveUsers(AllUsers);
            }

//             DebugLogger.Log($"[FirstLaunch] Login complete for {displayName}");
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // ══════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════
        /// <summary>Shows a specific page panel and hides all others.</summary>
        private void ShowPage(Panel page)
        {
//             DebugLogger.Log("[FirstLaunch] Switching page view");
            panelChoice.Visible = false;
            panelAdminSetup.Visible = false;
            panelAdminMembers.Visible = false;
            panelJoinCodeShow.Visible = false;
            panelJoinTeam.Visible = false;
            panelLogin.Visible = false;
            page.Visible = true;
            page.BringToFront();
        }

        /// <summary>Shows an error message in the provided label.</summary>
        private void ShowError(Label lbl, string msg)
        {
            DebugLogger.Log($"[FirstLaunch] Error: {msg}");
            lbl.Text = "\u274C " + msg;
            lbl.ForeColor = errorColor;
            lbl.Visible = true;
        }

        private Label MakeLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", size, style),
                ForeColor = color,
                AutoSize = true
            };
        }

        private Label MakeSectionLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = dimColor,
                Location = new Point(x, y),
                AutoSize = true
            };
        }

        private TextBox MakeTextBox(int x, int y, bool isPassword)
        {
            return new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(460, 32),
                Font = new Font("Segoe UI", 12),
                BackColor = fieldBg,
                ForeColor = fgColor,
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = isPassword
            };
        }

        private Button MakeButton(string text, Color bg, Color fg, int x, int y)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(460, 44),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = bg,
                ForeColor = fg,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }
    }
}
