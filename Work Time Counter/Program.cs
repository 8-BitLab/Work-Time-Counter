// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        Program.cs                                                   ║
// ║  PURPOSE:     APPLICATION ENTRY POINT AND USER LOGIN FLOW                  ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ║                                                                            ║
// ║  MULTI-TEAM STARTUP FLOW:                                                  ║
// ║    1. Migrate legacy single-team data (if needed)                          ║
// ║    2. If no teams → show FormFirstLaunch (create or join)                  ║
// ║    3. If multiple teams → show FormTeamSwitcher                            ║
// ║    4. If one team → auto-select it                                         ║
// ║    5. Show FormUserSelection for login                                     ║
// ║    6. Launch main app                                                      ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Windows.Forms;

namespace Work_Time_Counter
{
    internal static class Program
    {
        private const int PremiumSplashMinMs = 1000;
        private const string StartupSplashVersionText = "v1.0.3";

        private static void RunMainWithStartupSplash(UserInfo currentUser, List<UserInfo> allUsers)
        {
            using (var mainForm = new WorlFlow(currentUser, allUsers))
            {
                mainForm.Opacity = 0;
                mainForm.Show();
                mainForm.Refresh();
                Application.DoEvents();

                using (var splash = new StartupSplashForm(mainForm.Bounds, StartupSplashVersionText))
                {
                    splash.Show();
                    splash.SetProgress(0);
                    splash.Refresh();
                    Application.DoEvents();

                    var sw = Stopwatch.StartNew();

                    while (sw.ElapsedMilliseconds < PremiumSplashMinMs)
                    {
                        splash.SetProgress(
                            sw.ElapsedMilliseconds / (double)PremiumSplashMinMs,
                            sw.ElapsedMilliseconds);
                        Application.DoEvents();
                        Thread.Sleep(15);
                    }

                    splash.SetProgress(1, PremiumSplashMinMs);
                    splash.Close();
                }

                mainForm.Opacity = 1;
                mainForm.Activate();
                Application.Run(mainForm);
            }
        }

        /// <summary>
        /// Ensures at least one user is tagged as admin.
        /// Checks team.AdminName first, then prompts user to pick if needed.
        /// This is critical for team operations — every team must have a designated admin.
        /// </summary>
        private static void EnsureAdminLinked(List<UserInfo> users)
        {
            // Guard: no users to validate
            if (users == null || users.Count == 0)
            {
                return;
            }

            // Check 1: Is there already an admin in the local user list?
            if (users.Any(u => u.IsAdmin))
            {
                return;
            }

            // Check 2: Try to recover admin from team storage (team.bit may have AdminName field)
            var team = UserStorage.LoadTeam();
            if (team != null && !string.IsNullOrEmpty(team.AdminName))
            {
                var adminUser = users.FirstOrDefault(u =>
                    u.Name.Equals(team.AdminName, StringComparison.OrdinalIgnoreCase));
                if (adminUser != null)
                {
                    adminUser.IsAdmin = true;
                    UserStorage.SaveUsers(users);
                    return;
                }
                else
                {
                }
            }
            else
            {
            }

            // Check 3: No admin found — present dialog for user to select admin
            using (var pickForm = new Form())
            {
                pickForm.Text = "WorkFlow — Select Admin";
                pickForm.Width = 400;
                pickForm.Height = 320;
                pickForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                pickForm.StartPosition = FormStartPosition.CenterScreen;
                pickForm.MaximizeBox = false;
                pickForm.MinimizeBox = false;
                pickForm.BackColor = System.Drawing.Color.FromArgb(24, 28, 36);
                pickForm.ForeColor = System.Drawing.Color.FromArgb(220, 224, 230);

                // Label: Explain why we're asking
                var lbl = new Label
                {
                    Text = "⚠ No admin is linked to this team.\nPlease select which user is the admin:",
                    Font = new System.Drawing.Font("Segoe UI", 11),
                    ForeColor = System.Drawing.Color.FromArgb(255, 193, 7),
                    Location = new System.Drawing.Point(20, 20),
                    Size = new System.Drawing.Size(350, 50)
                };
                pickForm.Controls.Add(lbl);

                // ListBox: Show all available users
                var listBox = new ListBox
                {
                    Location = new System.Drawing.Point(20, 80),
                    Size = new System.Drawing.Size(350, 120),
                    Font = new System.Drawing.Font("Segoe UI", 12),
                    BackColor = System.Drawing.Color.FromArgb(30, 36, 46),
                    ForeColor = System.Drawing.Color.FromArgb(220, 224, 230),
                    BorderStyle = BorderStyle.FixedSingle
                };
                foreach (var u in users)
                    listBox.Items.Add(u.Name);
                if (listBox.Items.Count > 0)
                    listBox.SelectedIndex = 0;
                pickForm.Controls.Add(listBox);

                // Button: Confirm selection
                var btnOk = new Button
                {
                    Text = "🛡 Set as Admin",
                    Location = new System.Drawing.Point(20, 220),
                    Size = new System.Drawing.Size(350, 44),
                    Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold),
                    BackColor = System.Drawing.Color.FromArgb(255, 127, 80),
                    ForeColor = System.Drawing.Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand
                };
                btnOk.FlatAppearance.BorderSize = 0;
                btnOk.Click += (s, e) =>
                {
                    if (listBox.SelectedIndex >= 0)
                    {
                        pickForm.DialogResult = DialogResult.OK;
                        pickForm.Close();
                    }
                };
                pickForm.Controls.Add(btnOk);

                // Dialog result: User either selected an admin or cancelled
                if (pickForm.ShowDialog() == DialogResult.OK && listBox.SelectedIndex >= 0)
                {
                    // User confirmed a selection
                    string selectedName = listBox.SelectedItem.ToString();
                    var selectedUser = users.First(u => u.Name == selectedName);
                    selectedUser.IsAdmin = true;

                    // Persist the admin choice in both team storage and user storage
                    if (team != null)
                    {
                        team.AdminName = selectedName;
                        UserStorage.SaveTeam(team);
                    }

                    UserStorage.SaveUsers(users);
                }
                else
                {
                    // User cancelled dialog — default to first user as admin (failsafe)
                    users[0].IsAdmin = true;
                    UserStorage.SaveUsers(users);
                }
            }
        }

        [STAThread]
        static void Main()
        {
            // Initialize WinForms visual styles and rendering
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // ═══════════════════════════════════════════════════════════════════════════════
            // GLOBAL EXCEPTION HANDLERS
            // Purpose: Catch unhandled exceptions and display them to user (prevents silent crashes)
            // These handlers catch:
            //   - ThreadException: Exceptions from the UI thread
            //   - UnhandledException: Exceptions from background threads
            // ═══════════════════════════════════════════════════════════════════════════════
            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show("ThreadException:\n" + e.Exception.ToString(), "Crash Details", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = (Exception)e.ExceptionObject;
                MessageBox.Show("UnhandledException:\n" + ex.ToString(), "Crash Details", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            try
            {

                // ═══════════════════════════════════════════════════════════════════════════════
                // STEP 0: MIGRATE LEGACY DATA
                // Purpose: Convert old single-team storage structure to new multi-team structure
                // This ensures users upgrading from v1.x → v2.x don't lose data
                // ═══════════════════════════════════════════════════════════════════════════════
                UserStorage.MigrateLegacyData();

                // ═══════════════════════════════════════════════════════════════════════════════
                // STEP 1: CHECK IF ANY TEAM EXISTS
                // Purpose: Determine if this is first launch or returning user
                // First-time users see FormFirstLaunch to create or join a team
                // Returning users proceed to team selection (Step 2) or login (Step 3)
                // ═══════════════════════════════════════════════════════════════════════════════
                if (!UserStorage.HasTeam())
                {

                    // First launch: Show FormFirstLaunch to let user create a team or join one
                    using (var setupForm = new FormFirstLaunch())
                    {
                        if (setupForm.ShowDialog() != DialogResult.OK)
                        {
                            // User cancelled setup dialog
                            return;
                        }

                        // Extract user who just created/joined the team
                        UserInfo currentUser = setupForm.CreatedUser;
                        List<UserInfo> users = setupForm.AllUsers;

                        // Remember which user is currently logged in (for next launch)
                        UserStorage.SaveLastUser(currentUser.Name);

                        // Launch main app immediately (no need for team switcher or login form)
                        RunMainWithStartupSplash(currentUser, users);
                        return;
                    }
                }


                // ═══════════════════════════════════════════════════════════════════════════════
                // STEP 2: HANDLE MULTIPLE TEAMS (TEAM SWITCHER)
                // Purpose: If user has joined multiple teams, show switcher to pick which one to use
                // Variable 'joinedTeams' contains list of all teams this machine is part of
                // ═══════════════════════════════════════════════════════════════════════════════
                var joinedTeams = UserStorage.GetJoinedTeams();
                bool skipTeamSwitcher = UserStorage.GetSkipTeamSwitcherOnStartup();
                bool skipTeamSwitcherOnce = UserStorage.GetAndClearSkipTeamSwitcherOnce();

                if (joinedTeams.Count > 1)
                {
                    if (skipTeamSwitcher || skipTeamSwitcherOnce)
                    {
                        // Auto-open last active team when switcher is disabled at startup
                        string activeCode = UserStorage.GetActiveTeamCode();
                        bool activeExists = !string.IsNullOrWhiteSpace(activeCode) &&
                            joinedTeams.Any(t => t.JoinCode.Equals(activeCode, StringComparison.OrdinalIgnoreCase));
                        if (!activeExists)
                        {
                            UserStorage.SetActiveTeamCode(joinedTeams[0].JoinCode);
                        }
                    }
                    else
                    {
                        // Multiple teams: Show FormTeamSwitcher
                        using (var switcherForm = new FormTeamSwitcher())
                        {
                            if (switcherForm.ShowDialog() != DialogResult.OK)
                            {
                                // User cancelled team selection
                                return;
                            }
                            if (!string.IsNullOrWhiteSpace(switcherForm.SelectedCode))
                            {
                                UserStorage.SetActiveTeamCode(switcherForm.SelectedCode);
                            }
                            // User selected a team — form sets it as active internally
                        }
                    }
                }
                else if (joinedTeams.Count == 1)
                {
                    // Only one team: Auto-select it
                    UserStorage.SetActiveTeamCode(joinedTeams[0].JoinCode);
                }

                // Auto-heal: if local chat cache contains users outside current team members,
                // clear runtime caches for this active team to prevent cross-team leakage.
                var activeTeam = UserStorage.LoadTeam();
                if (activeTeam != null && UserStorage.HasForeignUsersInLocalChatCache(activeTeam))
                {
                    UserStorage.ClearTeamLocalRuntimeCache(activeTeam.JoinCode);
                }

                // ═══════════════════════════════════════════════════════════════════════════════
                // STEP 3: LOAD USERS AND SHOW LOGIN FORM
                // Purpose: Load all team members and let current user select themselves from list
                // Variable 'allUsers' contains list of UserInfo objects (name, admin status, join code)
                // ═══════════════════════════════════════════════════════════════════════════════
                List<UserInfo> allUsers = UserStorage.LoadUsers();

                // If user list is somehow empty, reconstruct from Firebase team data
                if (allUsers.Count == 0)
                {
                    var team = UserStorage.LoadTeam();
                    if (team != null)
                    {
                        foreach (var name in team.Members)
                        {
                            // Check if this user is the designated admin
                            bool isAdmin = name.Equals(team.AdminName, StringComparison.OrdinalIgnoreCase);
                            allUsers.Add(new UserInfo(name, isAdmin, team.JoinCode));
                        }
                        UserStorage.SaveUsers(allUsers);
                    }
                    else
                    {
                    }
                }

                // ═══════════════════════════════════════════════════════════════════════════════
                // STEP 3B: ENSURE ADMIN EXISTS
                // Purpose: Verify that at least one user is marked as admin
                // Every team must have an admin for team operations (settings, member management)
                // If no admin found, user is prompted to designate one
                // ═══════════════════════════════════════════════════════════════════════════════
                EnsureAdminLinked(allUsers);

                // STEP 3C: FAST AUTO-LOGIN BEFORE SHOWING LOGIN FORM
                // Avoids login form flash when remembered password is valid.
                if (FormUserSelection.TryAutoLoginWithRememberedPassword(allUsers, out UserInfo rememberedUser))
                {
                    UserStorage.SaveLastUser(rememberedUser.Name);
                    RunMainWithStartupSplash(rememberedUser, allUsers);
                    return;
                }

                // ═══════════════════════════════════════════════════════════════════════════════
                // STEP 4: SHOW LOGIN FORM
                // Purpose: Let user select which team member they are
                // FormUserSelection shows list of all team members; user picks themselves
                // ═══════════════════════════════════════════════════════════════════════════════
                using (var loginForm = new FormUserSelection(allUsers))
                {
                    if (loginForm.ShowDialog() == DialogResult.OK)
                    {
                        // User selected a team member (themselves)
                        UserInfo currentUser = loginForm.SelectedUser;

                        // Remember this user for next launch (auto-select next time)
                        UserStorage.SaveLastUser(currentUser.Name);

                        // ═════════════════════════════════════════════════════════════════════════
                        // STEP 5: LAUNCH MAIN APPLICATION
                        // Purpose: Show main WorlFlow form with all UI, features, and functionality
                        // Passes currentUser (who is logged in) and allUsers (team roster)
                        // ═════════════════════════════════════════════════════════════════════════
                        RunMainWithStartupSplash(currentUser, allUsers);
                    }
                    else
                    {
                        // User cancelled login form
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup Error:\n" + ex.ToString(), "WorkFlow Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}



