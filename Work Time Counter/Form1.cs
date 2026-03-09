using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Http;
using Newtonsoft.Json;
using Firebase.Database;
using Firebase.Database.Query;
using System.Drawing.Printing;
using System.IO;
using System.Xml.Linq;
using iTextSharp.text;
using iTextSharp.text.pdf;

namespace Work_Time_Counter
{
    public partial class Form1 : Form
    {
        // ============================================================
        // CONFIGURATION: Define your users here (max 10)
        // ============================================================
        private static readonly List<UserInfo> AllUsers = new List<UserInfo>
        {
            new UserInfo("Admin",    "Administrator"),
            new UserInfo("eng_anna",   "Blagoy Georgiev"),
            new UserInfo("tech_peter", "Mr. Technician Peter Müller"),
            new UserInfo("mgr_lisa",   "Ms. Manager Lisa Weber"),
            new UserInfo("dev_tom",    "Mr. Developer Tom Fischer"),
            // Add more users here up to 10...
            // new UserInfo("user6_id", "Display Name 6"),
            // new UserInfo("user7_id", "Display Name 7"),
            // new UserInfo("user8_id", "Display Name 8"),
            // new UserInfo("user9_id", "Display Name 9"),
            // new UserInfo("user10_id","Display Name 10"),
        };

        // ============================================================
        // FIELDS
        // ============================================================
        private string currentAppVersion = "1.0.0";  // update this with each release
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
        private readonly string firebaseUrl =
            "https://csharptimelogger-default-rtdb.europe-west1.firebasedatabase.app/logs.json";
        private System.Windows.Forms.Timer autoRefreshTimer;
        private System.Windows.Forms.Timer onlineCheckTimer;
        private string currentLiveLogKey = null;

        // Current logged-in user
        private UserInfo _currentUser;

        // Online status panel controls
        private Panel panelOnlineUsers;
        private Label labelOnlineTitle;
        private List<OnlineUserControl> onlineUserControls = new List<OnlineUserControl>();
        private bool isDarkMode = true;
       // private CheckBox chkDarkMode;
        private DateTime _mouseDownTime;
        private Timer _holdTimer;
      //  bool isAdmin = _currentUser.UserId == "Admin";
        public Form1()
        {
            InitializeComponent();

            // ── Step 1: Ask which user is logging in ──
            _currentUser = ShowUserSelectionDialog();
            if (_currentUser == null)
            {
                // User cancelled → close app
                this.Load += (s, e) => this.Close();
                return;
            }

            // ── Step 2: Setup window ──
            this.Text = $"Work Time Counter — {_currentUser.DisplayName}";
            this.Size = new System.Drawing.Size(1200, 600);

            // ── Step 3: Build the "Who's Online" panel ──
            BuildOnlineUsersPanel();

            // ── Step 4: Initialize timer for clock ──
            timer1 = new Timer();
            timer1.Interval = 1000;
            timer1.Tick += Timer_Tick;
            timer1.Start();

            buttonDelete.Click += buttonDelete_Click;
            dataGridView1.ReadOnly = false;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            // ── Step 5: Start online-check timer (every 60 seconds) ──
            onlineCheckTimer = new System.Windows.Forms.Timer();
            onlineCheckTimer.Interval = 60000; // 1 minute
            onlineCheckTimer.Tick += async (s, e) =>
            {
                await RefreshOnlineStatusAsync();
                await SendOnlineHeartbeatAsync();  // keep heartbeat alive
            };
            onlineCheckTimer.Start();

            InitializeTheme();
            StyleButtons();
            InitializeDataGridFeatures();
            InitializeHoldToToggleDebug();
            _debugForm.Show();  // comment later to hide debug win
            _ = CheckForUpdateAsync();
            // Send initial heartbeat
            _ = SendOnlineHeartbeatAsync();
            _ = RefreshOnlineStatusAsync();

            // Remove online signal when app closes
            this.FormClosing += async (s, e) =>
            {
                await RemoveOnlineSignalAsync();
            };
            this.AutoSize = true;
          //  this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        }

        // ============================================================
        // USER SELECTION DIALOG (shown at startup)
        // ============================================================
        private UserInfo ShowUserSelectionDialog()
        {
            using (var dlg = new FormUserSelection(AllUsers))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    return dlg.SelectedUser;
                return null;
            }
        }
        private void InitializeHoldToToggleDebug()
        {
            _debugForm = new DebugForm();
            _debugForm.FormClosing += (s, e) =>
            {
                // Don't actually close — just hide
                e.Cancel = true;
                _debugForm.Hide();
            };

            _holdTimer = new Timer();
            _holdTimer.Interval = 5000;
            _holdTimer.Tick += (s, e) =>
            {
                _holdTimer.Stop();
                if (_debugForm.Visible)
                    _debugForm.Hide();
                else
                    _debugForm.Show();
            };

            pictureBox1.MouseDown += (s, e) =>
            {
                _holdTimer.Start();
            };

            pictureBox1.MouseUp += (s, e) =>
            {
                _holdTimer.Stop();
            };
        }
        // ============================================================
        // BUILD THE "WHO'S ONLINE" PANEL (right side)
        // ============================================================
        private void BuildOnlineUsersPanel()
        {
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
                Text = "👥 Team Status",
                Font = new System.Drawing.Font("Segoe UI", 13, System.Drawing.FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 70),
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };
            panelOnlineUsers.Controls.Add(labelOnlineTitle);

            int yPos = 45;
            foreach (var user in AllUsers)
            {
                var ctrl = new OnlineUserControl(user);
                ctrl.Location = new Point(10, yPos);
                ctrl.Width = 290;
                panelOnlineUsers.Controls.Add(ctrl);
                onlineUserControls.Add(ctrl);
                yPos += ctrl.Height + 6;
            }

            this.Controls.Add(panelOnlineUsers);
        }

        // ============================================================
        // REFRESH ONLINE STATUS (check Firebase for "Working" logs)
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
                    userName = _currentUser.DisplayName
                };

                using (HttpClient client = new HttpClient())
                {
                    // Check if an Online signal already exists
                    var response = await client.GetAsync(firebaseUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json) && json != "null")
                        {
                            var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json);
                            var existing = logsDict.FirstOrDefault(l =>
                                l.Value.userId == GetCurrentUserId() &&
                                l.Value.status == "Online");

                            if (existing.Key != null)
                            {
                                // Update existing heartbeat timestamp
                                string updateUrl = firebaseUrl.Replace(".json", $"/{existing.Key}.json");
                                var patch = new { timestamp = DateTime.UtcNow.ToString("o") };
                                var request = new HttpRequestMessage(new HttpMethod("PATCH"), updateUrl)
                                {
                                    Content = new StringContent(
                                        JsonConvert.SerializeObject(patch), Encoding.UTF8, "application/json")
                                };
                                await client.SendAsync(request);
                                return;
                            }
                        }
                    }

                    // No existing Online signal — create one
                    var content = new StringContent(
                        JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
                    await client.PostAsync(firebaseUrl, content);
                }
            }
            catch (Exception ex)
            {
                UpdateRichTextBox($"❌ [ERROR] Heartbeat failed: {ex.Message}\r\n");
            }
        }

        private async Task RefreshOnlineStatusAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(firebaseUrl);
                    if (!response.IsSuccessStatusCode) return;

                    string json = await response.Content.ReadAsStringAsync();
                    if (string.IsNullOrWhiteSpace(json) || json == "null")
                    {
                        foreach (var ctrl in onlineUserControls)
                            ctrl.SetStatus("Offline", "");
                        return;
                    }

                    var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json);

                    foreach (var ctrl in onlineUserControls)
                    {
                        var workingLog = logsDict.Values.FirstOrDefault(log =>
                            log.userId == ctrl.UserInfo.UserId &&
                            log.status == "Working");

                        var onlineLog = logsDict.Values.FirstOrDefault(log =>
                            log.userId == ctrl.UserInfo.UserId &&
                            log.status == "Online");

                        if (workingLog != null)
                            ctrl.SetStatus("Working", workingLog.description ?? "");
                        else if (onlineLog != null)
                            ctrl.SetStatus("Online", "");
                        else
                            ctrl.SetStatus("Offline", "");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateRichTextBox($"❌ [ERROR] Online status check failed: {ex.Message}\r\n");
            }
        }
        private async Task RemoveOnlineSignalAsync()
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(firebaseUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        if (!string.IsNullOrWhiteSpace(json) && json != "null")
                        {
                            var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json);
                            var onlineSignal = logsDict.FirstOrDefault(l =>
                                l.Value.userId == GetCurrentUserId() &&
                                l.Value.status == "Online");

                            if (onlineSignal.Key != null)
                            {
                                string deleteUrl = firebaseUrl.Replace(".json", $"/{onlineSignal.Key}.json");
                                await client.DeleteAsync(deleteUrl);
                            }
                        }
                    }
                }
            }
            catch { }
        }
        // ============================================================
        // EXISTING METHODS (updated to include userId)
        // ============================================================
        private void Timer_Tick(object sender, EventArgs e)
        {
            labelTimerNow.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        //private void UpdateRichTextBox(string message)
        //{
        //    if (InvokeRequired)
        //        Invoke(new Action(() => richTextBoxDebug.AppendText(message)));
        //    else
        //        richTextBoxDebug.AppendText(message);
        //}
        private void UpdateRichTextBox(string message)
        {
            if (_debugForm == null) return;

            if (_debugForm.InvokeRequired)
                _debugForm.Invoke(new Action(() => _debugForm.AppendMessage(message)));
            else
                _debugForm.AppendMessage(message);
        }
        private void labelTimerNow_Click(object sender, EventArgs e) { }

        private FirebaseClient GetFirebaseClient()
        {
            return new FirebaseClient(
                "https://csharptimelogger-default-rtdb.europe-west1.firebasedatabase.app/");
        }

        private string GetCurrentUserId()
        {
            return _currentUser?.UserId ?? "defaultUser";
        }

        // ── Check if THIS user has a live signal ──
        private async Task<bool> CheckIfLiveSignalExistsAsync()
        {
            UpdateRichTextBox("[DEBUG] Checking for existing live signal...\r\n");
            try
            {
                var firebase = GetFirebaseClient();
                var logs = await firebase.Child("logs").OnceAsync<LogEntry>();

                var liveLog = logs.FirstOrDefault(log =>
                    log.Object.userId == GetCurrentUserId() &&
                    !string.IsNullOrEmpty(log.Object.status) &&
                    log.Object.status.Equals("Working", StringComparison.OrdinalIgnoreCase)
                );

                bool exists = liveLog != null;
                UpdateRichTextBox("✅ Live signal check: " + (exists ? "Found" : "Not found") + "\r\n");
                return exists;
            }
            catch (Exception ex)
            {
                UpdateRichTextBox("❌ [ERROR] Could not check live signal: " + ex.Message + "\r\n");
                return false;
            }
        }

        private async Task DeletePreviousLiveSignalAsync()
        {
            UpdateRichTextBox("[DEBUG] Deleting previous live signal...\r\n");
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
                    await firebase.Child("logs").Child(liveLog.Key).DeleteAsync();
                    UpdateRichTextBox("✅ Previous live signal deleted.\r\n");
                }
                else
                {
                    UpdateRichTextBox("⚠ No live signal to delete.\r\n");
                }
            }
            catch (Exception ex)
            {
                UpdateRichTextBox("❌ [ERROR] Failed to delete live signal: " + ex.Message + "\r\n");
                throw;
            }
        }

        private async Task AddStopTimeToLiveSignalAsync(string manualWorkingTime)
        {
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
                    UpdateRichTextBox("✅ Stop time added: " + manualWorkingTime + "\r\n");
                }
                else
                {
                    UpdateRichTextBox("⚠ No live signal found to stop.\r\n");
                }
            }
            catch (Exception ex)
            {
                UpdateRichTextBox("❌ [ERROR] Failed to add stop time: " + ex.Message + "\r\n");
                throw;
            }
        }
      
        // ============================================================
        // START BUTTON (updated to include userId + userName)
        // ============================================================
        private async void buttonStart_Click(object sender, EventArgs e)
        {
            bool liveExists = await CheckIfLiveSignalExistsAsync();
            if (liveExists)
            {
                UpdateRichTextBox("⚠ Existing live signal detected.\r\n");

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
                        UpdateRichTextBox("❌ [ERROR] Could not delete: " + ex.Message + "\r\n");
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
                        UpdateRichTextBox("❌ [ERROR] Could not stop old log: " + ex.Message + "\r\n");
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
                        UpdateRichTextBox("✅ New LIVE signal sent!\r\n");
                        await RefreshOnlineStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        UpdateRichTextBox("❌ [ERROR] Failed to send LIVE signal: " + ex.Message + "\r\n");
                    }
                    return;
                }
            }

            // Normal start (no previous live signal)
            StartNewLiveLog();
            try
            {
                await SendWorkStartedAsync(
                    richTextBoxDescription?.Text ?? "",
                    labelStartTime.Text,
                    "00:00:00"
                );
                UpdateRichTextBox("✅ LIVE signal sent!\r\n");
                await RefreshOnlineStatusAsync();
            }
            catch (Exception ex)
            {
                UpdateRichTextBox("❌ [ERROR] Failed to send LIVE signal: " + ex.Message + "\r\n");
            }
        }

        private void StartNewLiveLog()
        {
            _startTime = DateTime.Now;
            labelStartTime.Text = _startTime.ToString("HH:mm:ss");
            _elapsedTime = TimeSpan.Zero;
            labelWorkingTime.Text = "00:00:00";

            if (_workingTimer == null)
            {
                _workingTimer = new Timer();
                _workingTimer.Interval = 1000;
                _workingTimer.Tick += WorkingTimer_Tick;
            }
            _workingTimer.Start();
            UpdateRichTextBox("✅ Timer started for " + _currentUser.DisplayName + "\r\n");
        }

        private void WorkingTimer_Tick(object sender, EventArgs e)
        {
            _elapsedTime = _elapsedTime.Add(TimeSpan.FromSeconds(1));
            labelWorkingTime.Text = _elapsedTime.ToString(@"hh\:mm\:ss");
        }

        // ============================================================
        // STOP BUTTON
        // ============================================================
        private async void buttonStop_Click(object sender, EventArgs e)
        {
            if (_workingTimer != null && _workingTimer.Enabled)
            {
                _workingTimer.Stop();
                UpdateRichTextBox("✅ Timer stopped at: " + labelWorkingTime.Text + "\r\n");
                await StopCurrentLiveLogAsync();
                await RefreshOnlineStatusAsync(); // Refresh online panel immediately
            }
            else
            {
                UpdateRichTextBox("⚠ Timer was not running.\r\n");
            }
        }

        private async Task StopCurrentLiveLogAsync()
        {
            if (string.IsNullOrEmpty(currentLiveLogKey))
            {
                UpdateRichTextBox("❌ No active live log to stop.\r\n");
                return;
            }

            var data = new
            {
                workingTime = labelWorkingTime.Text,
                status = "Stopped",
                timestamp = DateTime.UtcNow.ToString("o")
            };

            string url = firebaseUrl.Replace(".json", $"/{currentLiveLogKey}.json");

            using (HttpClient client = new HttpClient())
            {
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = new StringContent(
                        JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json")
                };

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                    UpdateRichTextBox("✅ Live log stopped.\r\n");
                else
                    UpdateRichTextBox($"❌ Failed to stop live log. Status: {response.StatusCode}\r\n");
            }
        }

        // ============================================================
        // SEND DATA TO FIREBASE (now includes userId + userName)
        // ============================================================
        public async Task SendDataToFirebaseAsync(string description, string startTime, string workingTime)
        {
            var data = new
            {
                description = description ?? "",
                startTime = startTime ?? "",
                workingTime = workingTime ?? "",
                timestamp = DateTime.UtcNow.ToString("o"),
                userId = GetCurrentUserId(),
                userName = _currentUser.DisplayName
            };

            string json = JsonConvert.SerializeObject(data);

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(firebaseUrl, content);

                    if (response.IsSuccessStatusCode)
                        UpdateRichTextBox("✅ Data sent successfully!\r\n");
                    else
                        UpdateRichTextBox("❌ Failed. Status: " + response.StatusCode + "\r\n");
                }
            }
            catch (Exception ex)
            {
                UpdateRichTextBox("❌ Exception: " + ex.Message + "\r\n");
            }
        }

        public async Task SendWorkStartedAsync(string description, string startTime, string workingTime)
        {
            string statusValue = (workingTime == "00:00:00") ? "Working" : "Stopped";
            UpdateRichTextBox($"[DEBUG] Sending status '{statusValue}' for user '{_currentUser.DisplayName}'\r\n");

            var data = new
            {
                description = description ?? "",
                startTime = startTime ?? "",
                workingTime = workingTime ?? "",
                timestamp = DateTime.UtcNow.ToString("o"),
                status = statusValue,
                userId = GetCurrentUserId(),
                userName = _currentUser.DisplayName
            };

            string json = JsonConvert.SerializeObject(data);

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(firebaseUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        // Extract the key from Firebase response
                        string responseBody = await response.Content.ReadAsStringAsync();
                        var responseObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseBody);
                        if (responseObj != null && responseObj.ContainsKey("name"))
                        {
                            currentLiveLogKey = responseObj["name"];
                            UpdateRichTextBox($"✅ Live log key saved: {currentLiveLogKey}\r\n");
                        }
                        UpdateRichTextBox("✅ Work started log sent.\r\n");
                    }
                    else
                    {
                        UpdateRichTextBox($"❌ Failed. Status: {response.StatusCode}\r\n");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateRichTextBox("❌ Exception: " + ex.Message + "\r\n");
            }
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
        // DATAGRIDVIEW & REFRESH (now shows userName column)
        // ============================================================
        private void InitializeDataGridFeatures()
        {
            dataGridView1.DataBindingComplete += DataGridView1_DataBindingComplete;
            RefreshLogs();

            autoRefreshTimer = new System.Windows.Forms.Timer();
            autoRefreshTimer.Interval = 300000; // 5 minutes
            autoRefreshTimer.Tick += (s, e) => RefreshLogs();
            autoRefreshTimer.Start();
        }
        private void StyleDataGridView()
        {
            // Hide or shrink the row header column
            dataGridView1.RowHeadersVisible = false;  // hides it completely
                                                      // OR if you want to keep it small:
                                                      // dataGridView1.RowHeadersWidth = 20;
                                                      // dataGridView1.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(39, 51, 62);

            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            if (dataGridView1.Columns.Contains("Nr"))
                dataGridView1.Columns["Nr"].Width = 40;

            if (dataGridView1.Columns.Contains("userName"))
                dataGridView1.Columns["userName"].Width = 150;

            if (dataGridView1.Columns.Contains("description"))
                dataGridView1.Columns["description"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            if (dataGridView1.Columns.Contains("startTime"))
                dataGridView1.Columns["startTime"].Width = 80;

            if (dataGridView1.Columns.Contains("workingTime"))
                dataGridView1.Columns["workingTime"].Width = 80;

            if (dataGridView1.Columns.Contains("timestamp"))
                dataGridView1.Columns["timestamp"].Width = 110;

            if (dataGridView1.Columns.Contains("status"))
                dataGridView1.Columns["status"].Width = 80;
        }
        private async void RefreshLogs()
        {
            UpdateRichTextBox("[DEBUG] Refreshing logs...\r\n");
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(firebaseUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        if (string.IsNullOrWhiteSpace(json) || json == "null")
                        {
                            dataGridView1.DataSource = null;
                            return;
                        }

                        var logsDict = JsonConvert.DeserializeObject<Dictionary<string, LogEntry>>(json);
                        var keys = logsDict.Keys.ToList();
                        var logs = logsDict.Values.ToList();

                        //var filteredLogs = logs.Select((log, i) => new { Log = log, Key = keys[i] })
                        //    .GroupBy(x => (x.Log.description ?? "") + "|" + (x.Log.userId ?? ""))
                        //    .Select(g => g.OrderByDescending(x =>
                        //        DateTime.TryParse(x.Log.timestamp, out var dt) ? dt : DateTime.MinValue).First())
                        //    .Select((x, idx) => new LogEntryWithIndex
                        //    {
                        //        Nr = idx + 1,
                        //        userName = x.Log.userName ?? x.Log.userId ?? "Unknown",
                        //        description = x.Log.description,
                        //        startTime = x.Log.startTime,
                        //        workingTime = x.Log.workingTime,
                        //        timestamp = ParseDate(x.Log.timestamp),
                        //        status = x.Log.status,
                        //        Key = x.Key
                        //    })
                        //    .ToList();

                        //// Find THIS user's live log key
                        //currentLiveLogKey = filteredLogs
                        //    .FirstOrDefault(l => l.status == "Working" &&
                        //        l.userName == _currentUser.DisplayName)?.Key;

                        //dataGridView1.DataSource = filteredLogs;

                        var filteredLogs = logs.Select((log, i) => new { Log = log, Key = keys[i] })
    .GroupBy(x => (x.Log.description ?? "") + "|" + (x.Log.userId ?? ""))
    .Select(g => g.OrderByDescending(x =>
        DateTime.TryParse(x.Log.timestamp, out var dt) ? dt : DateTime.MinValue).First())
    .Select((x, idx) => new LogEntryWithIndex
    {
        Nr = idx + 1,
        userName = x.Log.userName ?? x.Log.userId ?? "Unknown",
        description = x.Log.description,
        startTime = x.Log.startTime,
        workingTime = x.Log.workingTime,
        timestamp = ParseDate(x.Log.timestamp),
        status = x.Log.status,
        Key = x.Key
    })
    .ToList();

                        // Remove "Online" heartbeat entries from the grid
                        filteredLogs = filteredLogs
                            .Where(l => l.status != "Online")
                            .Select((l, idx) => { l.Nr = idx + 1; return l; })
                            .ToList();

                        // Filter: only show current user's logs (Admin sees all)
                        // Filter: only show current user's logs (Admin sees all)
                        if (_currentUser.DisplayName != "Administrator")
                        {
                            filteredLogs = filteredLogs
                                .Where(l => l.userName == _currentUser.DisplayName)
                                .Select((l, idx) => { l.Nr = idx + 1; return l; })
                                .ToList();
                        }

                        // Find THIS user's live log key
                        currentLiveLogKey = filteredLogs
                            .FirstOrDefault(l => l.status == "Working" &&
                                l.userName == _currentUser.DisplayName)?.Key;

                        dataGridView1.DataSource = filteredLogs;
                        // Hide the Key column from display
                        if (dataGridView1.Columns.Contains("Key"))
                            dataGridView1.Columns["Key"].Visible = false;

                        UpdateRichTextBox($"✅ Grid updated with {filteredLogs.Count} entries.\r\n");
                        StyleDataGridView();
                        ApplyTheme();
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateRichTextBox($"❌ [ERROR] Refresh failed: {ex.Message}\r\n");
            }
        }
        private string settingsFilePath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "WorkTimeCounter", "settings.json");

        private void SaveSettings()
        {
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
        }
        private void DataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
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
            if (DateTime.TryParse(isoDate, out DateTime dt))
                return dt.ToString("dd-MMM-yyyy");
            return "";
        }

        // ============================================================
        // REPORT, REFRESH, SAVE, DELETE (unchanged logic)
        // ============================================================
        private void buttonReport_Click(object sender, EventArgs e)
        {
            try
            {
                // Check if current user is Admin
                bool isAdmin = _currentUser.DisplayName == "Administrator";

                List<DataGridViewRow> rowsToPrint = new List<DataGridViewRow>();

                if (isAdmin)
                {
                    // Admin: ask what to print
                    DialogResult choice = MessageBox.Show(
                        "Print ALL users' logs?\n\nYes = All users\nNo = Only your logs",
                        "Admin Print Options",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (choice == DialogResult.Cancel) return;

                    if (choice == DialogResult.Yes)
                    {
                        // All rows
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                            if (!row.IsNewRow) rowsToPrint.Add(row);
                    }
                    else
                    {
                        // Only admin's own logs
                        foreach (DataGridViewRow row in dataGridView1.Rows)
                        {
                            if (row.IsNewRow) continue;
                            string name = row.Cells["userName"]?.Value?.ToString() ?? "";
                            if (name == _currentUser.DisplayName)
                                rowsToPrint.Add(row);
                        }
                    }
                }
                else
                {
                    // Non-admin: only their own logs
                    foreach (DataGridViewRow row in dataGridView1.Rows)
                    {
                        if (row.IsNewRow) continue;
                        string name = row.Cells["userName"]?.Value?.ToString() ?? "";
                        if (name == _currentUser.DisplayName)
                            rowsToPrint.Add(row);
                    }
                }

                if (rowsToPrint.Count == 0)
                {
                    MessageBox.Show("No logs found to print.", "Empty Report",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                SaveFileDialog sfd = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Report_{_currentUser.DisplayName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                if (sfd.ShowDialog() != DialogResult.OK) return;

                Document doc = new Document(PageSize.A4.Rotate());
                PdfWriter.GetInstance(doc, new FileStream(sfd.FileName, FileMode.Create));
                doc.Open();

                // Title
                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                string title = isAdmin && rowsToPrint.Count > 0
                    ? "Work Time Report — All Users"
                    : $"Work Time Report — {_currentUser.DisplayName}";
                doc.Add(new Paragraph(title, titleFont));
                doc.Add(new Paragraph($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}",
                    FontFactory.GetFont(FontFactory.HELVETICA, 10)));
                doc.Add(new Paragraph(" ")); // spacing

                // Table
                int visibleCols = dataGridView1.Columns.Cast<DataGridViewColumn>()
                    .Count(c => c.Visible);
                PdfPTable table = new PdfPTable(visibleCols);
                table.WidthPercentage = 100;

                // Header
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10, BaseColor.WHITE);
                foreach (DataGridViewColumn col in dataGridView1.Columns)
                {
                    if (!col.Visible) continue;
                    var cell = new PdfPCell(new Phrase(col.HeaderText, headerFont));
                    cell.BackgroundColor = new BaseColor(52, 73, 94);
                    cell.Padding = 5;
                    table.AddCell(cell);
                }

                // Data rows
                var dataFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);
                var workingFont = FontFactory.GetFont(FontFactory.HELVETICA, 9, BaseColor.RED);
                bool alternate = false;

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
                    }
                    alternate = !alternate;
                }

                doc.Add(table);
                doc.Close();

                // Open the PDF automatically
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = sfd.FileName,
                    UseShellExecute = true
                });

                UpdateRichTextBox($"✅ PDF saved: {sfd.FileName}\r\n");
                MessageBox.Show("Report saved successfully!", "Done",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                UpdateRichTextBox($"❌ [ERROR] Report failed: {ex.Message}\r\n");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            RefreshLogs();
            _ = RefreshOnlineStatusAsync();
        }

        private async void buttonSave_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                UpdateRichTextBox("⚠ No row selected.\r\n");
                return;
            }

            var row = dataGridView1.SelectedRows[0];
            var logEntry = row.DataBoundItem as LogEntryWithIndex;
            if (logEntry == null || string.IsNullOrEmpty(logEntry.Key))
            {
                UpdateRichTextBox("❌ Invalid row.\r\n");
                return;
            }

            var data = new
            {
                description = row.Cells["description"].Value?.ToString() ?? "",
                startTime = row.Cells["startTime"].Value?.ToString() ?? "",
                workingTime = row.Cells["workingTime"].Value?.ToString() ?? "",
                status = row.Cells["status"].Value?.ToString() ?? "",
                timestamp = DateTime.UtcNow.ToString("o"),
                userId = GetCurrentUserId(),
                userName = _currentUser.DisplayName
            };

            try
            {
                string updateUrl = firebaseUrl.Replace(".json", $"/{logEntry.Key}.json");
                using (HttpClient client = new HttpClient())
                {
                    var json = JsonConvert.SerializeObject(data);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PutAsync(updateUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        UpdateRichTextBox("✅ Entry updated.\r\n");
                        RefreshLogs();
                    }
                    else
                    {
                        UpdateRichTextBox($"❌ Update failed. Status: {response.StatusCode}\r\n");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateRichTextBox($"❌ [ERROR] Update exception: {ex.Message}\r\n");
            }
        }

        private async void buttonDelete_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
            {
                UpdateRichTextBox("⚠ No row selected.\r\n");
                return;
            }

            var row = dataGridView1.SelectedRows[0];
            var logEntry = row.DataBoundItem as LogEntryWithIndex;
            if (logEntry == null || string.IsNullOrEmpty(logEntry.Key))
            {
                UpdateRichTextBox("❌ Invalid row.\r\n");
                return;
            }

            if (MessageBox.Show("Delete this entry?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                string deleteUrl = firebaseUrl.Replace(".json", $"/{logEntry.Key}.json");
                using (HttpClient client = new HttpClient())
                {
                    var response = await client.DeleteAsync(deleteUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        UpdateRichTextBox("✅ Entry deleted.\r\n");
                        if (currentLiveLogKey == logEntry.Key)
                            currentLiveLogKey = null;
                        RefreshLogs();
                        await RefreshOnlineStatusAsync();
                    }
                    else
                    {
                        UpdateRichTextBox("❌ Delete failed. Status: " + response.StatusCode + "\r\n");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateRichTextBox("❌ [ERROR] Delete exception: " + ex.Message + "\r\n");
            }
        }

        private void checkBoxTheme_CheckedChanged(object sender, EventArgs e)
        {

        }


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

                using (HttpClient client = new HttpClient())
                {
                    var response = await client.GetAsync(versionUrl);
                    UpdateRichTextBox($"[DEBUG] Version check response: {response.StatusCode}\r\n");
                    if (!response.IsSuccessStatusCode) return;

                    string json = await response.Content.ReadAsStringAsync();
                    UpdateRichTextBox($"[DEBUG] Version JSON: {json}\r\n");
                    if (string.IsNullOrWhiteSpace(json) || json == "null") return;

                    var versionInfo = JsonConvert.DeserializeObject<AppVersionInfo>(json);
                    if (versionInfo == null || string.IsNullOrEmpty(versionInfo.latestVersion))
                    {
                        UpdateRichTextBox("[DEBUG] Version info is null or empty\r\n");
                        return;
                    }

                    UpdateRichTextBox($"[DEBUG] Latest version from Firebase: {versionInfo.latestVersion}\r\n");
                    bool isNewer = IsNewerVersion(versionInfo.latestVersion, currentAppVersion);
                    UpdateRichTextBox($"[DEBUG] Is newer: {isNewer}\r\n");

                    if (isNewer)
                    {
                        labelVersion.Text = $"v{currentAppVersion}";
                        labelVersion.ForeColor = Color.FromArgb(231, 76, 60);

                        labelMessage.Text = $"⚠ New version v{versionInfo.latestVersion} available!";
                        labelMessage.ForeColor = Color.FromArgb(231, 76, 60);
                        labelMessage.Font = new System.Drawing.Font(labelMessage.Font.FontFamily,
                            labelMessage.Font.Size, FontStyle.Bold);
                        labelMessage.Cursor = Cursors.Hand;
                        labelMessage.Visible = true;

                        labelMessage.Click += (s, ev) =>
                        {
                            DialogResult result = MessageBox.Show(
                                $"New version v{versionInfo.latestVersion} is available!\n\n" +
                                $"What's new:\n{versionInfo.releaseNotes}\n\n" +
                                "Download now?",
                                "Update Available",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information);

                            if (result == DialogResult.Yes && !string.IsNullOrEmpty(versionInfo.downloadUrl))
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = versionInfo.downloadUrl,
                                    UseShellExecute = true
                                });
                            }
                        };
                        UpdateRichTextBox("⚠ Update available!\r\n");
                    }
                    else
                    {
                        labelVersion.Text = $"v{currentAppVersion} ✔";
                        labelVersion.ForeColor = Color.FromArgb(46, 204, 113);
                        labelMessage.Visible = false;
                        UpdateRichTextBox("✅ App is up to date.\r\n");
                    }
                }
            }
            catch (Exception ex)
            {
                labelVersion.Text = $"v{currentAppVersion}";
                labelMessage.Visible = false;
                UpdateRichTextBox($"❌ [ERROR] Update check failed: {ex.Message}\r\n");
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

        private void InitializeTheme()
        {
            LoadSettings();  // load saved preference
            checkBoxTheme.Text = "Dark Mode";
            checkBoxTheme.Checked = isDarkMode;
            checkBoxTheme.CheckedChanged += CheckBoxTheme_CheckedChanged;
            ApplyTheme();
        }

        private void CheckBoxTheme_CheckedChanged(object sender, EventArgs e)
        {
            isDarkMode = checkBoxTheme.Checked;
            ApplyTheme();
            SaveSettings();  // remember the choice
        }
        //public void ApplyTheme(bool darkMode)
        //{
        //    Color nameOnline = darkMode ? Color.FromArgb(236, 240, 241) : Color.FromArgb(20, 20, 40);
        //    Color nameOffline = darkMode ? Color.FromArgb(100, 110, 120) : Color.FromArgb(150, 150, 160);
        //    Color descColor = darkMode ? Color.FromArgb(80, 90, 100) : Color.FromArgb(100, 100, 120);

        //  //  labelName.ForeColor = _isOnline ? nameOnline : nameOffline;
        //    labelDescription.ForeColor = descColor;
        //    this.BackColor = Color.Transparent;
        //    foreach (var ctrl in onlineUserControls)
        //    {
        //        ctrl.ApplyTheme(isDarkMode);
        //    }
        //}
        private void ApplyTheme()
        {
            Color backMain, backPanel, backInput, foreMain, foreSecondary, accentColor;
            Color startBg, stopBg, buttonText;

            if (isDarkMode)
            {
                backMain = Color.FromArgb(30, 39, 46);
                backPanel = Color.FromArgb(39, 51, 62);
                backInput = Color.FromArgb(47, 61, 74);
                foreMain = Color.FromArgb(236, 240, 241);
                foreSecondary = Color.FromArgb(149, 165, 176);
                accentColor = Color.FromArgb(52, 152, 219);
                startBg = Color.FromArgb(46, 204, 113);
                stopBg = Color.FromArgb(231, 76, 60);
                buttonText = Color.White;
            }
            else

                {
                    backMain = Color.FromArgb(240, 242, 245);      // soft gray background
                    backPanel = Color.FromArgb(255, 255, 255);      // white panels
                    backInput = Color.FromArgb(250, 250, 252);      // near-white inputs
                    foreMain = Color.FromArgb(44, 62, 80);          // dark text
                    foreSecondary = Color.FromArgb(127, 140, 141);  // muted text
                    accentColor = Color.FromArgb(41, 128, 185);     // blue accent
                    startBg = Color.FromArgb(39, 174, 96);          // green
                    stopBg = Color.FromArgb(192, 57, 43);           // red
                    buttonText = Color.White;
                }

            this.BackColor = backMain;
            this.ForeColor = foreMain;

            // Also theme the online panel
            if (panelOnlineUsers != null)
            {
                panelOnlineUsers.BackColor = backPanel;
                if (labelOnlineTitle != null)
                    labelOnlineTitle.ForeColor = foreMain;
            }
            // Theme the GroupBox
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is GroupBox gb)
                {
                    gb.ForeColor = accentColor;
                    gb.BackColor = backMain;
                }
            }

            // Fix description textbox border visibility
            richTextBoxDescription.BorderStyle = BorderStyle.FixedSingle;
            ApplyToControls(this.Controls, backMain, backPanel, backInput,
                foreMain, foreSecondary, accentColor, startBg, stopBg, buttonText);
            // Recolor grid rows for current theme
            if (dataGridView1.Rows.Count > 0)
                RecolorDataGridRows();

            // Theme online user controls
            foreach (var ctrl in onlineUserControls)
            {
                ctrl.ApplyTheme(isDarkMode);
            }
        }

        private void ApplyToControls(Control.ControlCollection controls,
            Color backMain, Color backPanel, Color backInput,
            Color foreMain, Color foreSecondary, Color accentColor,
            Color startBg, Color stopBg, Color buttonText)
        {
            foreach (Control ctrl in controls)
            {
                if (ctrl is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 0;
                    btn.ForeColor = buttonText;

                    if (btn.Text.Contains("START"))
                        btn.BackColor = startBg;
                    else if (btn.Text.Contains("STOP"))
                        btn.BackColor = stopBg;
                    else
                        btn.BackColor = accentColor;
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
                else if (ctrl is Label lbl)
                {
                    if (lbl.Text.Contains("WORK COUNTER"))
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
                    chk.BackColor = backMain;
                }
                else if (ctrl is DataGridView dgv)
                {
                    dgv.EnableHeadersVisualStyles = false;
                    dgv.BorderStyle = BorderStyle.None;

                    if (isDarkMode)
                    {
                        dgv.GridColor = Color.FromArgb(55, 70, 85);
                        dgv.BackgroundColor = Color.FromArgb(30, 39, 46);
                        dgv.DefaultCellStyle.BackColor = Color.FromArgb(47, 61, 74);
                        dgv.DefaultCellStyle.ForeColor = Color.FromArgb(236, 240, 241);
                        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219);
                        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
                        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(39, 51, 62);
                        dgv.AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(236, 240, 241);
                        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 39, 46);
                        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(236, 240, 241);
                    }
                    else
                    {
                        dgv.GridColor = Color.FromArgb(215, 218, 222);
                        dgv.BackgroundColor = Color.FromArgb(240, 242, 245);
                        dgv.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 255);
                        dgv.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
                        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(41, 128, 185);
                        dgv.DefaultCellStyle.SelectionForeColor = Color.White;
                        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(232, 236, 241);
                        dgv.AlternatingRowsDefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
                        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(210, 215, 222);
                        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
                    }

                    dgv.ColumnHeadersDefaultCellStyle.Font = new System.Drawing.Font("Segoe UI", 9, FontStyle.Bold);
                    dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgv.ColumnHeadersDefaultCellStyle.BackColor;
                    dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = dgv.ColumnHeadersDefaultCellStyle.ForeColor;
                    dgv.RowsDefaultCellStyle.BackColor = dgv.DefaultCellStyle.BackColor;
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
                        foreMain, foreSecondary, accentColor, startBg, stopBg, buttonText);
            }
        }
        private void RecolorDataGridRows()
        {
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
                            ? Color.FromArgb(255, 255, 255)
                            : Color.FromArgb(232, 236, 241);
                        row.DefaultCellStyle.ForeColor = Color.FromArgb(44, 62, 80);
                    }
                }
            }
            dataGridView1.Invalidate();
        }
        private void StyleButtons()
        {
            // START button
            buttonStart.FlatStyle = FlatStyle.Flat;
            buttonStart.FlatAppearance.BorderSize = 0;
            buttonStart.BackColor = Color.FromArgb(46, 204, 113);
            buttonStart.ForeColor = Color.White;
            buttonStart.Font = new System.Drawing.Font("Segoe UI", 11, FontStyle.Bold);
            buttonStart.Size = new Size(140, 45);
            buttonStart.Cursor = Cursors.Hand;
            buttonStart.Text = "▶  START";

            // Rounded corners for START
            var startPath = new GraphicsPath();
            startPath.AddArc(0, 0, 20, 20, 180, 90);
            startPath.AddArc(buttonStart.Width - 20, 0, 20, 20, 270, 90);
            startPath.AddArc(buttonStart.Width - 20, buttonStart.Height - 20, 20, 20, 0, 90);
            startPath.AddArc(0, buttonStart.Height - 20, 20, 20, 90, 90);
            startPath.CloseFigure();
            buttonStart.Region = new Region(startPath);

            // Hover effects for START
            buttonStart.MouseEnter += (s, e) => buttonStart.BackColor = Color.FromArgb(39, 174, 96);
            buttonStart.MouseLeave += (s, e) => buttonStart.BackColor = Color.FromArgb(46, 204, 113);

            // STOP button
            buttonStop.FlatStyle = FlatStyle.Flat;
            buttonStop.FlatAppearance.BorderSize = 0;
            buttonStop.BackColor = Color.FromArgb(231, 76, 60);
            buttonStop.ForeColor = Color.White;
            buttonStop.Font = new System.Drawing.Font("Segoe UI", 11, FontStyle.Bold);
            buttonStop.Size = new Size(140, 45);
            buttonStop.Cursor = Cursors.Hand;
            buttonStop.Text = "■  STOP";

            // Rounded corners for STOP
            var stopPath = new GraphicsPath();
            stopPath.AddArc(0, 0, 20, 20, 180, 90);
            stopPath.AddArc(buttonStop.Width - 20, 0, 20, 20, 270, 90);
            stopPath.AddArc(buttonStop.Width - 20, buttonStop.Height - 20, 20, 20, 0, 90);
            stopPath.AddArc(0, buttonStop.Height - 20, 20, 20, 90, 90);
            stopPath.CloseFigure();
            buttonStop.Region = new Region(stopPath);

            // Hover effects for STOP
            buttonStop.MouseEnter += (s, e) => buttonStop.BackColor = Color.FromArgb(192, 57, 43);
            buttonStop.MouseLeave += (s, e) => buttonStop.BackColor = Color.FromArgb(231, 76, 60);

            // REFRESH button
            button1.FlatStyle = FlatStyle.Flat;
            button1.FlatAppearance.BorderSize = 0;
            button1.BackColor = Color.FromArgb(52, 152, 219);
            button1.ForeColor = Color.White;
            button1.Font = new System.Drawing.Font("Segoe UI", 10, FontStyle.Bold);
            button1.Cursor = Cursors.Hand;
            button1.Text = "🔄 Refresh";
            button1.MouseEnter += (s, e) => button1.BackColor = Color.FromArgb(41, 128, 185);
            button1.MouseLeave += (s, e) => button1.BackColor = Color.FromArgb(52, 152, 219);

            // PRINT REPORT button
            buttonReport.FlatStyle = FlatStyle.Flat;
            buttonReport.FlatAppearance.BorderSize = 0;
            buttonReport.BackColor = Color.FromArgb(52, 152, 219);
            buttonReport.ForeColor = Color.White;
            buttonReport.Font = new System.Drawing.Font("Segoe UI", 10, FontStyle.Bold);
            buttonReport.Cursor = Cursors.Hand;
            buttonReport.Text = "📄 Print Report";
            buttonReport.MouseEnter += (s, e) => buttonReport.BackColor = Color.FromArgb(41, 128, 185);
            buttonReport.MouseLeave += (s, e) => buttonReport.BackColor = Color.FromArgb(52, 152, 219);
        }
    }

    // ================================================================
    // DATA MODELS (updated with userId/userName)
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
    }

    public class LogEntryWithIndex
    {
        public int Nr { get; set; }
        public string userName { get; set; }
        public string description { get; set; }
        public string startTime { get; set; }
        public string workingTime { get; set; }
        public string timestamp { get; set; }
        public string status { get; set; }
        public string Key { get; set; }
    }

    // ================================================================
    // USER INFO CLASS
    // ================================================================
    public class UserInfo
    {
        public string UserId { get; set; }
        public string DisplayName { get; set; }

        public UserInfo(string id, string name)
        {
            UserId = id;
            DisplayName = name;
        }

        public override string ToString() => DisplayName;
    }

    // ================================================================
    // ONLINE USER CONTROL (circle indicator + name + description)
    // ================================================================
    public class OnlineUserControl : UserControl
    {
        public UserInfo UserInfo { get; private set; }

        private Panel circleIndicator;
        private Label labelName;
        private Label labelDescription;
        private bool _isOnline = false;

        public OnlineUserControl(UserInfo user)
        {
            UserInfo = user;
            this.Height = 52;
            this.Width = 290;
            this.BackColor = Color.Transparent;

            // Green/Red circle
            circleIndicator = new Panel
            {
                Size = new Size(14, 14),
                Location = new Point(4, 8),
                BackColor = Color.Gray
            };
            circleIndicator.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color dotColor;
                if (_status == "Working")
                    dotColor = Color.FromArgb(46, 204, 113);   // green
                else if (_status == "Online")
                    dotColor = Color.FromArgb(52, 152, 219);   // blue
                else
                    dotColor = Color.FromArgb(140, 140, 150);   // gray

                using (var brush = new SolidBrush(dotColor))
                {
                    e.Graphics.FillEllipse(brush, 0, 0, 13, 13);
                }
            };

            labelName = new Label
            {
                Text = user.DisplayName,
                Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 60),
                Location = new Point(24, 4),
                AutoSize = true
            };

            labelDescription = new Label
            {
                Text = "",
                Font = new System.Drawing.Font("Segoe UI", 8.5f, System.Drawing.FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 120),
                Location = new Point(24, 26),
                AutoSize = true,
                MaximumSize = new Size(250, 0)
            };

            this.Controls.Add(circleIndicator);
            this.Controls.Add(labelName);
            this.Controls.Add(labelDescription);
        }

        private string _status = "Offline"; // "Working", "Online", "Offline"

        public void SetStatus(string status, string description)
        {
            _status = status;
            _isOnline = status != "Offline";

            if (InvokeRequired)
                Invoke(new Action(() => SetStatusInternal(status, description)));
            else
                SetStatusInternal(status, description);
        }

        private void SetStatusInternal(string status, string description)
        {
            _status = status;
            _isOnline = status != "Offline";
            circleIndicator.Invalidate();

            if (status == "Working")
            {
                labelName.ForeColor = Color.FromArgb(236, 240, 241);
                labelDescription.Text = string.IsNullOrWhiteSpace(description)
                    ? "Working..."
                    : (description.Length > 50 ? description.Substring(0, 47) + "..." : description);
                labelDescription.ForeColor = Color.FromArgb(46, 204, 113); // green
                labelDescription.Visible = true;
            }
            else if (status == "Online")
            {
                labelName.ForeColor = Color.FromArgb(200, 210, 220);
                labelDescription.Text = "Online — idle";
                labelDescription.ForeColor = Color.FromArgb(52, 152, 219); // blue
                labelDescription.Visible = true;
            }
            else
            {
                labelName.ForeColor = Color.FromArgb(100, 110, 120);
                labelDescription.Text = "Offline";
                labelDescription.ForeColor = Color.FromArgb(80, 90, 100);
                labelDescription.Visible = true;
            }
        }

        public void ApplyTheme(bool darkMode)
        {
            Color nameOnline = darkMode ? Color.FromArgb(236, 240, 241) : Color.FromArgb(20, 20, 40);
            Color nameOffline = darkMode ? Color.FromArgb(100, 110, 120) : Color.FromArgb(150, 150, 160);
            Color descColor = darkMode ? Color.FromArgb(80, 90, 100) : Color.FromArgb(100, 100, 120);

            labelName.ForeColor = _isOnline ? nameOnline : nameOffline;
            labelDescription.ForeColor = descColor;
            this.BackColor = Color.Transparent;
        }
    }

    // ================================================================
    // USER SELECTION DIALOG (shown at app startup)
    // ================================================================
    public class FormUserSelection : Form
    {
        public UserInfo SelectedUser { get; private set; }
        private ListBox listBoxUsers;
        private Button buttonLogin;
        private Button buttonCancel;
        private Label labelTitle;

        public FormUserSelection(List<UserInfo> users)
        {
            this.Text = "Select Your Name";
            this.Width = 400;
            this.Height = 380;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(245, 245, 250);

            labelTitle = new Label
            {
                Text = "Who are you?",
                Font = new System.Drawing.Font("Segoe UI", 14, System.Drawing.FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true
            };

            listBoxUsers = new ListBox
            {
                Location = new Point(20, 55),
                Size = new Size(340, 210),
                Font = new System.Drawing.Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle
            };
            foreach (var u in users)
                listBoxUsers.Items.Add(u);
            if (listBoxUsers.Items.Count > 0)
                listBoxUsers.SelectedIndex = 0;

            // Double-click to login
            listBoxUsers.DoubleClick += (s, e) => DoLogin();

            buttonLogin = new Button
            {
                Text = "Login",
                Location = new Point(20, 280),
                Size = new Size(160, 38),
                Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                BackColor = Color.FromArgb(34, 197, 94),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            buttonLogin.Click += (s, e) => DoLogin();

            buttonCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(200, 280),
                Size = new Size(160, 38),
                Font = new System.Drawing.Font("Segoe UI", 10),
                FlatStyle = FlatStyle.Flat
            };
            buttonCancel.Click += (s, e) =>
            {
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.Controls.Add(labelTitle);
            this.Controls.Add(listBoxUsers);
            this.Controls.Add(buttonLogin);
            this.Controls.Add(buttonCancel);
        }

        private void DoLogin()
        {
            if (listBoxUsers.SelectedItem is UserInfo user)
            {
                SelectedUser = user;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please select your name.", "No Selection",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    // ================================================================
    // WORKING TIME INPUT DIALOG (unchanged)
    // ================================================================
    public partial class FormWorkingTimeInput : Form
    {
        public string WorkingTime { get; private set; }

        private TextBox textBoxWorkingTime;
        private Button buttonOK;
        private Button buttonCancel;
        private Label labelPrompt;

        public FormWorkingTimeInput()
        {
            this.Width = 360;
            this.Height = 170;
            this.Text = "Enter Working Time";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;

            labelPrompt = new Label() { Left = 16, Top = 15, Width = 300, Text = "Enter working time (hh:mm:ss):" };
            textBoxWorkingTime = new TextBox() { Left = 16, Top = 40, Width = 200, Text = "00:00:00" };
            buttonOK = new Button() { Text = "OK", Left = 16, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            buttonCancel = new Button() { Text = "Cancel", Left = 110, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };

            buttonOK.Click += buttonOK_Click;
            buttonCancel.Click += buttonCancel_Click;

            this.Controls.Add(labelPrompt);
            this.Controls.Add(textBoxWorkingTime);
            this.Controls.Add(buttonOK);
            this.Controls.Add(buttonCancel);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            WorkingTime = textBoxWorkingTime.Text.Trim();
            if (TimeSpan.TryParse(WorkingTime, out _))
            {
                DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Please enter time in format hh:mm:ss", "Invalid input",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            this.Close();
        }

    }
}