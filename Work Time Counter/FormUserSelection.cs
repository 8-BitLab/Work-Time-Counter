// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        FormUserSelection.cs                                         ║
// ║  PURPOSE:     USER LOGIN SELECTION FORM                                    ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    /// <summary>
    /// FormUserSelection — User login form for existing teams.
    /// On first launch, shows a list of users to select from.
    /// On returning launches, shows a welcome screen with auto-login support.
    /// </summary>
    public class FormUserSelection : Form
    {
        public UserInfo SelectedUser { get; private set; }

        private List<UserInfo> _users;
        private bool _isFirstLaunch;

        // First-launch controls
        private ListBox listBoxUsers;
        private Label labelUserLabel;
        private Label labelHint;

        // Returning-user controls
        private Label labelWelcomeBack;

        // Shared controls
        private TextBox textBoxPassword;
        private CheckBox checkBoxRemember;
        private Button buttonLogin;
        private Button buttonCancel;
        private Button buttonChangePassword;
        private Label labelTitle;
        private Label labelSubtitle;
        private Label labelPasswordLabel;
        private Label labelError;

        /// <summary>
        /// Tries to auto-login using remembered password hash without showing the login form.
        /// Returns true when a valid remembered session is found.
        /// </summary>
        public static bool TryAutoLoginWithRememberedPassword(List<UserInfo> users, out UserInfo autoUser)
        {
            autoUser = null;
            if (users == null || users.Count == 0) return false;

            string lastUser = UserStorage.GetLastUser();
            if (string.IsNullOrEmpty(lastUser)) return false;

            var candidate = users.Find(u => u.Name.Equals(lastUser, StringComparison.OrdinalIgnoreCase));
            if (candidate == null) return false;

            string rememberedHash = UserStorage.GetRememberedPassword(lastUser);
            if (string.IsNullOrEmpty(rememberedHash)) return false;

            if (!candidate.VerifyPasswordHash(rememberedHash)) return false;

            var team = UserStorage.LoadTeam();
            if (team != null && team.IsBanned(lastUser))
            {
                // Keep behavior consistent with login form: banned users lose remembered login.
                UserStorage.SaveRememberedPassword(lastUser, null);
                return false;
            }

            autoUser = candidate;
            return true;
        }

        public FormUserSelection(List<UserInfo> users)
        {
//             DebugLogger.Log("[UserSelection] Initializing FormUserSelection");
            _users = users;

            string lastUser = UserStorage.GetLastUser();
            _isFirstLaunch = string.IsNullOrEmpty(lastUser);
//             DebugLogger.Log($"[UserSelection] First launch: {_isFirstLaunch}, Last user: {lastUser}, Total users: {users.Count}");

            this.Text = "WorkFlow — Login";
            this.Width = 440;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(24, 28, 36);
            this.ForeColor = Color.FromArgb(220, 224, 230);

            // ── Title ──
            labelTitle = new Label
            {
                Text = "⑂  WorkFlow Monitor",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 127, 80),
                Location = new Point(20, 20),
                AutoSize = true
            };
            this.Controls.Add(labelTitle);

            // ── Subtitle — show team name if available ──
            var team = UserStorage.LoadTeam();
            string subtitleText = team != null
                ? $"Team: {team.TeamName}"
                : "Engineering Working Hours";

            labelSubtitle = new Label
            {
                Text = subtitleText,
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(120, 130, 145),
                Location = new Point(22, 54),
                AutoSize = true
            };
            this.Controls.Add(labelSubtitle);

            if (_isFirstLaunch)
                BuildFirstLaunchUI();
            else
                BuildReturningUserUI(lastUser);
        }

        // ════════════════════════════════════════════════════════
        // FIRST LAUNCH — show user list + default password hint
        // ════════════════════════════════════════════════════════
        /// <summary>Builds the first launch UI with user list selection.</summary>
        private void BuildFirstLaunchUI()
        {
//             DebugLogger.Log("[UserSelection] Building first launch UI");
            this.Height = 560;

            // User label
            labelUserLabel = new Label
            {
                Text = "SELECT YOUR NAME",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 130, 145),
                Location = new Point(20, 95),
                AutoSize = true
            };
            this.Controls.Add(labelUserLabel);

            // User listbox
            listBoxUsers = new ListBox
            {
                Location = new Point(20, 118),
                Size = new Size(380, 120),
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(30, 36, 46),
                ForeColor = Color.FromArgb(220, 224, 230),
                SelectionMode = SelectionMode.One
            };
            foreach (var u in _users)
                listBoxUsers.Items.Add(u.IsAdmin ? $"\u2605 {u.Name} (Admin)" : u.Name);
            if (listBoxUsers.Items.Count > 0)
                listBoxUsers.SelectedIndex = 0;
            this.Controls.Add(listBoxUsers);

            // Password label
            labelPasswordLabel = new Label
            {
                Text = "PASSWORD",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 130, 145),
                Location = new Point(20, 255),
                AutoSize = true
            };
            this.Controls.Add(labelPasswordLabel);

            // Password textbox
            textBoxPassword = new TextBox
            {
                Location = new Point(20, 278),
                Size = new Size(380, 32),
                Font = new Font("Segoe UI", 12),
                BackColor = Color.FromArgb(30, 36, 46),
                ForeColor = Color.FromArgb(220, 224, 230),
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true
            };
            textBoxPassword.KeyPress += OnPasswordKeyPress;
            this.Controls.Add(textBoxPassword);

            // Remember Password checkbox
            checkBoxRemember = new CheckBox
            {
                Text = "Remember password",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(160, 170, 185),
                Location = new Point(20, 316),
                AutoSize = true
            };
            this.Controls.Add(checkBoxRemember);

            // Error label
            labelError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 53, 69),
                Location = new Point(20, 340),
                Size = new Size(380, 20),
                Visible = false
            };
            this.Controls.Add(labelError);

            // Hint — only on first launch
            labelHint = new Label
            {
                Text = "First login — default password: 111111\nYou will be asked to set a new password.",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(255, 193, 7),
                Location = new Point(20, 365),
                Size = new Size(380, 32),
                TextAlign = ContentAlignment.TopCenter
            };
            this.Controls.Add(labelHint);

            // Login button
            buttonLogin = new Button
            {
                Text = "🔓  Login",
                Location = new Point(20, 390),
                Size = new Size(180, 44),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 127, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            buttonLogin.FlatAppearance.BorderSize = 0;
            buttonLogin.Click += (s, e) => DoLogin();
            this.Controls.Add(buttonLogin);

            // Cancel button
            buttonCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(220, 390),
                Size = new Size(180, 44),
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 44, 56),
                ForeColor = Color.FromArgb(160, 170, 180),
                Cursor = Cursors.Hand
            };
            buttonCancel.FlatAppearance.BorderSize = 0;
            buttonCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(buttonCancel);

            this.Shown += (s, e) => textBoxPassword.Focus();
        }

        // ════════════════════════════════════════════════════════
        // RETURNING USER — no list, just name + password + change pw
        // ════════════════════════════════════════════════════════
        /// <summary>Builds the returning user UI with auto-login support and password management.</summary>
        private void BuildReturningUserUI(string userName)
        {
//             DebugLogger.Log($"[UserSelection] Building returning user UI for: {userName}");
            this.Height = 410;

            // Check if this user is admin
            var returningUser = _users.Find(u => u.Name.Equals(userName, StringComparison.OrdinalIgnoreCase));
            bool isAdmin = returningUser != null && returningUser.IsAdmin;
//             DebugLogger.Log($"[UserSelection] Returning user found: {returningUser != null}, Is admin: {isAdmin}");

            // CHECK IF PASSWORD IS REMEMBERED — AUTO-LOGIN
            string rememberedHash = UserStorage.GetRememberedPassword(userName);
            if (returningUser != null && rememberedHash != null && returningUser.VerifyPasswordHash(rememberedHash))
            {
//                 DebugLogger.Log($"[UserSelection] Found remembered password for {userName}, checking ban status");
                // CHECK BAN FIRST — even with remembered password
                var teamCheck = UserStorage.LoadTeam();
                if (teamCheck != null && teamCheck.IsBanned(userName))
                {
                    // Banned — clear remembered password and show normal login with error
//                     DebugLogger.Log($"[UserSelection] User {userName} is banned, clearing remembered password");
                    UserStorage.SaveRememberedPassword(userName, null);
                }
                else
                {
                    // Auto-login: password is remembered and still valid
//                     DebugLogger.Log($"[UserSelection] Auto-logging in user: {userName}");
                    SelectedUser = returningUser;
                    // Set DialogResult and close INSIDE the Shown event
                    // (setting it earlier gets reset by WinForms when showing as dialog)
                    this.Shown += (s, e) =>
                    {
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    };
                    return;
                }
            }

            // Welcome back label with user name
            labelWelcomeBack = new Label
            {
                Text = isAdmin ? $"★  Welcome back, {userName}" : $"👋  Welcome back, {userName}",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = isAdmin ? Color.FromArgb(100, 220, 120) : Color.FromArgb(220, 224, 230),
                Location = new Point(20, 95),
                Size = new Size(380, 35)
            };
            this.Controls.Add(labelWelcomeBack);

            // Admin badge
            if (isAdmin)
            {
                var lblAdminBadge = new Label
                {
                    Text = "🛡 Team Admin",
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 220, 120),
                    Location = new Point(22, 128),
                    AutoSize = true
                };
                this.Controls.Add(lblAdminBadge);
            }

            // Password label
            labelPasswordLabel = new Label
            {
                Text = "PASSWORD",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 130, 145),
                Location = new Point(20, 145),
                AutoSize = true
            };
            this.Controls.Add(labelPasswordLabel);

            // Password textbox
            textBoxPassword = new TextBox
            {
                Location = new Point(20, 168),
                Size = new Size(380, 32),
                Font = new Font("Segoe UI", 12),
                BackColor = Color.FromArgb(30, 36, 46),
                ForeColor = Color.FromArgb(220, 224, 230),
                BorderStyle = BorderStyle.FixedSingle,
                UseSystemPasswordChar = true
            };
            textBoxPassword.KeyPress += OnPasswordKeyPress;
            this.Controls.Add(textBoxPassword);

            // Remember Password checkbox
            checkBoxRemember = new CheckBox
            {
                Text = "Remember password",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(160, 170, 185),
                Location = new Point(20, 206),
                AutoSize = true,
                Checked = false
            };
            this.Controls.Add(checkBoxRemember);

            // Error label
            labelError = new Label
            {
                Text = "",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(220, 53, 69),
                Location = new Point(20, 230),
                Size = new Size(380, 20),
                Visible = false
            };
            this.Controls.Add(labelError);

            // Login button
            buttonLogin = new Button
            {
                Text = "🔓  Login",
                Location = new Point(20, 258),
                Size = new Size(180, 44),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(255, 127, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            buttonLogin.FlatAppearance.BorderSize = 0;
            buttonLogin.Click += (s, e) => DoLogin();
            this.Controls.Add(buttonLogin);

            // Cancel button
            buttonCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(220, 258),
                Size = new Size(180, 44),
                Font = new Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 44, 56),
                ForeColor = Color.FromArgb(160, 170, 180),
                Cursor = Cursors.Hand
            };
            buttonCancel.FlatAppearance.BorderSize = 0;
            buttonCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };
            this.Controls.Add(buttonCancel);

            // Change Password button
            buttonChangePassword = new Button
            {
                Text = "🔑  Change Password",
                Location = new Point(20, 316),
                Size = new Size(380, 36),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(38, 44, 56),
                ForeColor = Color.FromArgb(150, 160, 175),
                Cursor = Cursors.Hand
            };
            buttonChangePassword.FlatAppearance.BorderSize = 1;
            buttonChangePassword.FlatAppearance.BorderColor = Color.FromArgb(60, 70, 85);
            buttonChangePassword.Click += (s, e) => DoChangePassword();
            this.Controls.Add(buttonChangePassword);

            this.Shown += (s, e) => textBoxPassword.Focus();
        }

        // ════════════════════════════════════════════
        // SHARED: Enter key in password field
        // ════════════════════════════════════════════
        /// <summary>Handles Enter key press in password field to trigger login.</summary>
        private void OnPasswordKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                DoLogin();
            }
        }

        // ════════════════════════════════════════════
        // LOGIN LOGIC
        // ════════════════════════════════════════════
        /// <summary>Gets the currently selected user from either the list (first launch) or stored last user (returning launch).</summary>
        private UserInfo GetSelectedUser()
        {
            if (_isFirstLaunch)
            {
                if (listBoxUsers == null || listBoxUsers.SelectedIndex < 0) return null;
                // Items are displayed as "★ Name (Admin)" or just "Name"
                // Match by index since list order matches _users order
                return _users[listBoxUsers.SelectedIndex];
            }
            else
            {
                // Returning user — find by saved name
                string lastUser = UserStorage.GetLastUser();
                return _users.Find(u => u.Name.Equals(lastUser, StringComparison.OrdinalIgnoreCase));
            }
        }

        /// <summary>Performs login validation: password check, ban check, default password enforcement.</summary>
        private void DoLogin()
        {
//             DebugLogger.Log("[UserSelection] DoLogin called");
            UserInfo user = GetSelectedUser();
            if (user == null)
            {
                DebugLogger.Log("[UserSelection] Login failed: no user selected");
                ShowError("\u274c Please select a user");
                return;
            }

//             DebugLogger.Log($"[UserSelection] Login attempt for user: {user.Name}");

            // CHECK IF USER IS BANNED
            var team = UserStorage.LoadTeam();
            if (team != null && team.IsBanned(user.Name))
            {
//                 DebugLogger.Log($"[UserSelection] User {user.Name} is banned");
                ShowError("\U0001f6ab You have been banned from this team. Contact the admin.");
                return;
            }

            string password = textBoxPassword.Text;
            if (string.IsNullOrEmpty(password))
            {
                DebugLogger.Log("[UserSelection] Login failed: no password entered");
                ShowError("\u274c Please enter your password");
                return;
            }

            if (!user.VerifyPassword(password))
            {
                DebugLogger.Log($"[UserSelection] Login failed: incorrect password for {user.Name}");
                ShowError("\u274c Incorrect password");
                textBoxPassword.Clear();
                textBoxPassword.Focus();
                return;
            }

//             DebugLogger.Log($"[UserSelection] Password verified for {user.Name}");

            // First login with default password → force change
            if (user.IsDefaultPassword)
            {
//                 DebugLogger.Log($"[UserSelection] User {user.Name} has default password, forcing change");
                if (DoForcePasswordChange(user))
                {
                    // Save remember preference AFTER password change
                    SaveRememberPreference(user);
                    SelectedUser = user;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
//                     DebugLogger.Log("[UserSelection] User cancelled password change");
                    ShowError("⚠ You must change the default password");
                    textBoxPassword.Clear();
                    textBoxPassword.Focus();
                }
                return;
            }

            // Save remember preference
            SaveRememberPreference(user);

            // Normal login
//             DebugLogger.Log($"[UserSelection] User {user.Name} logged in successfully");
            SelectedUser = user;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // ════════════════════════════════════════════
        // CHANGE PASSWORD (voluntary — from button)
        // ════════════════════════════════════════════
        /// <summary>Handles voluntary password change (initiated by user button click).</summary>
        private void DoChangePassword()
        {
//             DebugLogger.Log("[UserSelection] DoChangePassword called");
            UserInfo user = GetSelectedUser();
            if (user == null)
            {
                DebugLogger.Log("[UserSelection] Change password failed: no user found");
                ShowError("❌ No user found");
                return;
            }

//             DebugLogger.Log($"[UserSelection] Password change requested for {user.Name}");

            string currentPassword = textBoxPassword.Text;
            if (string.IsNullOrEmpty(currentPassword))
            {
                DebugLogger.Log("[UserSelection] Change password failed: no current password entered");
                ShowError("❌ Enter your current password first");
                textBoxPassword.Focus();
                return;
            }

            if (!user.VerifyPassword(currentPassword))
            {
                DebugLogger.Log("[UserSelection] Change password failed: current password incorrect");
                ShowError("❌ Current password is incorrect");
                textBoxPassword.Clear();
                textBoxPassword.Focus();
                return;
            }

//             DebugLogger.Log("[UserSelection] Current password verified, opening change dialog");
            using (var dlg = new FormChangePassword(user.Name))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
//                     DebugLogger.Log($"[UserSelection] Setting new password for {user.Name}");
                    user.SetPassword(dlg.NewPassword);
                    user.IsDefaultPassword = false;
                    UserStorage.SaveUsers(_users);

                    // Update remembered password if checkbox is checked
                    if (checkBoxRemember != null && checkBoxRemember.Checked)
                    {
                        UserStorage.SaveRememberedPassword(user.Name, user.GetPasswordHash());
//                         DebugLogger.Log($"[UserSelection] Saved remembered password for {user.Name}");
                    }
                    else
                        UserStorage.SaveRememberedPassword(user.Name, null);

                    // GLOBAL ACCOUNT: Sync password change to Firebase
//                     DebugLogger.Log("[UserSelection] Syncing password change to global account");
                    _ = UserStorage.UpdateAccountPasswordAsync(user.Name, user.GetPasswordHash());

                    ShowSuccess("\u2705 Password changed!");
                    textBoxPassword.Clear();
                    textBoxPassword.Focus();
                }
                else
                {
//                     DebugLogger.Log("[UserSelection] User cancelled password change");
                }
            }
        }

        // ════════════════════════════════════════════
        // FORCED PASSWORD CHANGE (first login)
        // ════════════════════════════════════════════
        /// <summary>Forces password change on first login with default password. Returns true if successful.</summary>
        private bool DoForcePasswordChange(UserInfo user)
        {
//             DebugLogger.Log($"[UserSelection] Forcing password change for {user.Name}");
            using (var dlg = new FormChangePassword(user.Name))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
//                     DebugLogger.Log($"[UserSelection] Setting new password for {user.Name}");
                    user.SetPassword(dlg.NewPassword);
                    user.IsDefaultPassword = false;
                    UserStorage.SaveUsers(_users);

                    // GLOBAL ACCOUNT: Sync password change to Firebase
//                     DebugLogger.Log("[UserSelection] Syncing forced password change to global account");
                    _ = UserStorage.UpdateAccountPasswordAsync(user.Name, user.GetPasswordHash());

                    return true;
                }
            }
            return false;
        }

        // ════════════════════════════════════════════
        // REMEMBER PASSWORD HELPER
        // ════════════════════════════════════════════
        /// <summary>Saves or clears the remember password preference based on checkbox state.</summary>
        private void SaveRememberPreference(UserInfo user)
        {
            if (checkBoxRemember != null && checkBoxRemember.Checked)
            {
                // SAVE the password hash locally so next login skips password entry
//                 DebugLogger.Log($"[UserSelection] Saving remembered password for {user.Name}");
                UserStorage.SaveRememberedPassword(user.Name, user.GetPasswordHash());
            }
            else
            {
                // CLEAR any previously remembered password
//                 DebugLogger.Log($"[UserSelection] Clearing remembered password for {user.Name}");
                UserStorage.SaveRememberedPassword(user.Name, null);
            }
        }

        // ════════════════════════════════════════════
        // UI HELPERS
        // ════════════════════════════════════════════
        /// <summary>Shows an error message in the error label with auto-hide.</summary>
        private void ShowError(string msg)
        {
            DebugLogger.Log($"[UserSelection] Error: {msg}");
            labelError.Text = msg;
            labelError.ForeColor = Color.FromArgb(220, 53, 69);
            labelError.Visible = true;
            AutoHide();
        }

        /// <summary>Shows a success message in the error label with auto-hide.</summary>
        private void ShowSuccess(string msg)
        {
//             DebugLogger.Log($"[UserSelection] Success: {msg}");
            labelError.Text = msg;
            labelError.ForeColor = Color.FromArgb(34, 197, 94);
            labelError.Visible = true;
            AutoHide();
        }

        /// <summary>Auto-hides the error label after 4 seconds.</summary>
        private void AutoHide()
        {
            var t = new Timer { Interval = 4000 };
            t.Tick += (s, e) => { labelError.Visible = false; t.Stop(); t.Dispose(); };
            t.Start();
        }
    }
}
