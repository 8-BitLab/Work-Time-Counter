// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        FileSharePanel.cs                                            ║
// ║  PURPOSE:     P2P FILE SHARING — FILES GO DIRECTLY BETWEEN USERS VIA TCP   ║
// ║               FIREBASE STORES ONLY TINY METADATA (~200 BYTES PER FILE)     ║
// ║               SUPPORTS UP TO 50 MB PER FILE WITH ZERO FIREBASE BANDWIDTH   ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝
//
// ══════════════════════════════════════════════════════════════════════════════
// HOW P2P FILE SHARING WORKS:
// ══════════════════════════════════════════════════════════════════════════════
//
//   1. APP STARTS → TCP LISTENER STARTS ON A RANDOM PORT
//      - Listens for incoming file requests from other team members
//      - Port is registered in Firebase with each file share
//
//   2. USER SHARES A FILE:
//      - File is saved to local SharedFiles folder (%APPDATA%/WorkTimeCounter/SharedFiles/)
//      - Only METADATA goes to Firebase: file name, size, hash, uploader IP:PORT
//      - Firebase bandwidth: ~200 bytes. File data: ZERO bytes through Firebase!
//
//   3. ANOTHER USER DOWNLOADS:
//      - Reads metadata from Firebase (tiny, ~200 bytes)
//      - Connects directly to seeder's IP:PORT via TCP
//      - File transfers peer-to-peer over the local network
//      - Downloader becomes a new seeder (their IP:PORT added to Firebase)
//
//   4. MULTIPLE SEEDERS:
//      - If first seeder is offline, tries next seeder automatically
//      - More downloads = more seeders = better availability
//
// ══════════════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    public class FileSharePanel : UserControl
    {
        // ══════════════════════════════════════════════════════════════════
        // CONSTANTS
        // ══════════════════════════════════════════════════════════════════

        // MAX FILE SIZE: 500 MB — FILES GO VIA TCP, NOT FIREBASE!
        private const long MAX_FILE_SIZE = 500L * 1024 * 1024;

        // LOCAL TEAM FOLDER FOR STORING SHARED FILES (ISOLATED PER TEAM)
        private readonly string _sharedFolder;

        // TCP BUFFER SIZE FOR FILE TRANSFER
        private const int TCP_BUFFER_SIZE = 8192;

        // ══════════════════════════════════════════════════════════════════
        // FIELDS
        // ══════════════════════════════════════════════════════════════════
        private readonly string _firebaseBaseUrl;
        private readonly string _currentUserName;

        // TCP FILE SERVER — LISTENS FOR INCOMING DOWNLOAD REQUESTS
        private TcpListener _tcpListener;
        private int _tcpPort;
        private string _localIP;
        private CancellationTokenSource _serverCts;

        // UI CONTROLS
        private Panel panelDropZone;
        private Label lblDropText;
        private FlowLayoutPanel flowFiles;
        private Label lblTitle;
        private ProgressBar progressBar;
        private Label lblProgress;

        // IN-MEMORY FILE LIST (METADATA ONLY)
        private List<FileShareEntry> _files = new List<FileShareEntry>();
        private bool _isDarkMode = true;
        private bool _isTransferring = false;

        // CALLBACK TO VERIFY PASSWORD
        public Func<string, bool> VerifyPassword { get; set; }

        // ADMIN PERMISSION — ONLY ADMINS CAN DELETE SHARED FILES
        public bool IsAdmin { get; set; } = false;

        // ONLINE STATUS CHECK — USED TO DETERMINE PEER AVAILABILITY
        public Func<string, bool> IsPeerOnline { get; set; }

        // SHARED FOLDER SETTINGS
        private SharedFolderSettings _sharedSettings;

        // AUTO-REFRESH TIMER
        private System.Windows.Forms.Timer _autoRefreshTimer;

        // ══════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════════
        public FileSharePanel(string firebaseBaseUrl, string currentUserName)
        {
            _firebaseBaseUrl = firebaseBaseUrl.TrimEnd('/');
            _currentUserName = currentUserName;
            _sharedFolder = GetSharedFolderPathForActiveTeam();

            this.BackColor = ThemeConstants.Dark.BgElevated;
            this.BorderStyle = BorderStyle.None;
            this.Padding = new Padding(ThemeConstants.SpaceS);

            // Draw subtle top border line
            this.Paint += (s, e) =>
            {
                ThemeConstants.DrawTopBorder(e, this.Width, true);
            };

            // ENSURE LOCAL SHARED FILES FOLDER EXISTS
            try { Directory.CreateDirectory(_sharedFolder); } catch { }
//             DebugLogger.Log("[FileShare] File operation: Directory.CreateDirectory");

            // DETECT LOCAL IP ADDRESS
            _localIP = GetLocalIPAddress();

            BuildUI();

            // START TCP FILE SERVER IN BACKGROUND
            StartTcpFileServer();

            // LOAD SHARED FOLDER SETTINGS
            _sharedSettings = SharedFolderSettings.LoadSettings();

            // AUTO-REFRESH TIMER
            if (_sharedSettings.AutoRefreshEnabled)
            {
                _autoRefreshTimer = new System.Windows.Forms.Timer { Interval = _sharedSettings.RefreshIntervalSeconds * 1000 };
                _autoRefreshTimer.Tick += async (s, e) => await RefreshAsync();
                _autoRefreshTimer.Start();
            }
        }

        private static string GetSharedFolderPathForActiveTeam()
        {
            string joinCode = UserStorage.GetActiveTeamCode();
            if (string.IsNullOrWhiteSpace(joinCode))
                joinCode = "DEFAULT";
            joinCode = joinCode.Trim().ToUpperInvariant();

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WorkFlow",
                "teams",
                joinCode,
                "SharedFiles");
        }

        // ══════════════════════════════════════════════════════════════════
        // TCP FILE SERVER — SERVES FILES TO OTHER TEAM MEMBERS
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// STARTS A TCP LISTENER ON A RANDOM AVAILABLE PORT.
        /// WHEN ANOTHER USER CONNECTS AND SENDS A FILE KEY,
        /// WE LOOK UP THE FILE LOCALLY AND STREAM IT BACK.
        /// </summary>
        private void StartTcpFileServer()
        {
            try
            {
                // LET THE OS PICK AN AVAILABLE PORT
                _tcpListener = new TcpListener(IPAddress.Any, 0);
                _tcpListener.Start();
                _tcpPort = ((IPEndPoint)_tcpListener.LocalEndpoint).Port;

                _serverCts = new CancellationTokenSource();

                // ACCEPT CONNECTIONS IN BACKGROUND
                Task.Run(() => AcceptConnectionsAsync(_serverCts.Token));
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[FileShare] Exception caught: " + ex.ToString());
                _tcpPort = 0;
            }
        }

        /// <summary>
        /// BACKGROUND LOOP: ACCEPTS INCOMING TCP CONNECTIONS FROM DOWNLOADERS.
        /// PROTOCOL:
        ///   1. Client sends file key as UTF8 string (terminated by newline)
        ///   2. Server looks up file in local SharedFiles folder
        ///   3. Server sends 8-byte file length (big-endian long)
        ///   4. Server streams the file bytes
        /// </summary>
        private async Task AcceptConnectionsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    // HANDLE EACH CONNECTION IN ITS OWN TASK
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    DebugLogger.Log("[FileShare] Exception caught: " + ex.ToString());
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    client.ReceiveTimeout = 10000;
                    client.SendTimeout = 30000;

                    // READ FILE KEY FROM CLIENT (LINE-TERMINATED)
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    string fileKey = await reader.ReadLineAsync();

                    if (string.IsNullOrWhiteSpace(fileKey))
                    {
                        return;
                    }

                    // LOOK UP FILE LOCALLY
                    string localPath = Path.Combine(_sharedFolder, fileKey);
                    if (!File.Exists(localPath))
                    {
//                         DebugLogger.Log("[FileShare] File operation: File.Exists");
                        // SEND ZERO LENGTH = FILE NOT FOUND
                        byte[] zeroLen = BitConverter.GetBytes(0L);
                        if (BitConverter.IsLittleEndian) Array.Reverse(zeroLen);
                        await stream.WriteAsync(zeroLen, 0, 8);
                        return;
                    }

                    // READ FILE AND SEND IT
                    byte[] fileData = File.ReadAllBytes(localPath);
//                     DebugLogger.Log("[FileShare] File operation: File.ReadAllBytes");

                    // SEND FILE LENGTH (8 BYTES, BIG-ENDIAN)
                    byte[] lenBytes = BitConverter.GetBytes((long)fileData.Length);
                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                    await stream.WriteAsync(lenBytes, 0, 8);

                    // SEND FILE DATA IN CHUNKS
                    int offset = 0;
                    while (offset < fileData.Length)
                    {
                        int toSend = Math.Min(TCP_BUFFER_SIZE, fileData.Length - offset);
                        await stream.WriteAsync(fileData, offset, toSend);
                        offset += toSend;
                    }

                    await stream.FlushAsync();
                }
            }
            catch { }
        }

        /// <summary>
        /// STOP THE TCP SERVER — CALL WHEN FORM IS CLOSING
        /// </summary>
        public void StopTcpFileServer()
        {
            try
            {
                _autoRefreshTimer?.Stop();
                _autoRefreshTimer?.Dispose();
                _serverCts?.Cancel();
                _tcpListener?.Stop();
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════════
        // BUILD USER INTERFACE
        // ══════════════════════════════════════════════════════════════════
        private void BuildUI()
        {
            // ── TITLE ──
            lblTitle = new Label
            {
                Text = "📁 SHARED TEAM FOLDER (up to 500 MB)",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = ThemeConstants.Dark.AccentPrimary,
                Dock = DockStyle.Top,
                Height = 24,
                Padding = new Padding(6, 4, 0, 0)
            };
            this.Controls.Add(lblTitle);

            // ── DROP ZONE ──
            panelDropZone = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(24, 28, 36),
                Margin = new Padding(4),
                AllowDrop = true,
                Cursor = Cursors.Hand
            };
            panelDropZone.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(100, 255, 127, 80), 2))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    e.Graphics.DrawRectangle(pen, 2, 2, panelDropZone.Width - 5, panelDropZone.Height - 5);
                }
            };
            panelDropZone.DragEnter += DropZone_DragEnter;
            panelDropZone.DragDrop += DropZone_DragDrop;
            panelDropZone.Click += DropZone_Click;

            lblDropText = new Label
            {
                Text = "📥  Drop files here to share with team\n(up to 500 MB • P2P distributed sync)",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(140, 150, 165),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblDropText.Click += DropZone_Click;
            lblDropText.AllowDrop = true;
            lblDropText.DragEnter += DropZone_DragEnter;
            lblDropText.DragDrop += DropZone_DragDrop;
            panelDropZone.Controls.Add(lblDropText);
            this.Controls.Add(panelDropZone);

            // ── PROGRESS BAR (HIDDEN UNTIL TRANSFER) ──
            var progressPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 34,
                Padding = new Padding(4, 2, 4, 2)
            };

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 14,
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            progressPanel.Controls.Add(progressBar);

            lblProgress = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 16,
                Font = new Font("Segoe UI", 7),
                ForeColor = Color.FromArgb(140, 150, 165),
                Text = "",
                Visible = false
            };
            progressPanel.Controls.Add(lblProgress);
            this.Controls.Add(progressPanel);

            // ── FILE LIST ──
            flowFiles = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Padding = new Padding(2)
            };
            this.Controls.Add(flowFiles);
        }

        // ══════════════════════════════════════════════════════════════════
        // DRAG AND DROP
        // ══════════════════════════════════════════════════════════════════
        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                panelDropZone.BackColor = Color.FromArgb(40, 48, 60);
            }
        }

        private async void DropZone_DragDrop(object sender, DragEventArgs e)
        {
            panelDropZone.BackColor = _isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(235, 238, 245);
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string fp in files)
                await ShareFileAsync(fp);
        }

        private async void DropZone_Click(object sender, EventArgs e)
        {
            if (_isTransferring) return;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Select file to share with team (max 500 MB)";
                ofd.Multiselect = true;
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    foreach (string fp in ofd.FileNames)
                        await ShareFileAsync(fp);
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // SHARE FILE — SAVES LOCALLY + REGISTERS METADATA IN FIREBASE
        // FIREBASE BANDWIDTH: ~200 BYTES. FILE DATA: STAYS LOCAL!
        // ══════════════════════════════════════════════════════════════════════
        private async Task ShareFileAsync(string filePath)
        {
//             DebugLogger.Log("[FileShare] ShareFileAsync() started for file: " + Path.GetFileName(filePath));
            if (_isTransferring)
            {
                MessageBox.Show("Please wait for the current transfer to finish.",
                    "Transfer In Progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var fi = new FileInfo(filePath);

                if (fi.Length > MAX_FILE_SIZE)
                {
                    MessageBox.Show($"File \"{fi.Name}\" is too large.\nMax: 500 MB. Yours: {FormatFileSize(fi.Length)}",
                        "File Too Large", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _isTransferring = true;
                ShowProgress(true);
                UpdateProgress(0, 3, $"Sharing {fi.Name}...");

                // STEP 1: COMPUTE SHA256 HASH
                byte[] fileBytes = File.ReadAllBytes(filePath);
//                 DebugLogger.Log("[FileShare] File operation: File.ReadAllBytes");
                string hash = ComputeHash(fileBytes);
                UpdateProgress(1, 3, "Registering in Firebase (metadata only)...");

                // STEP 2: REGISTER METADATA IN FIREBASE (TINY! ~200 BYTES)
                string myEndpoint = _localIP + ":" + _tcpPort;

                var entry = new FileShareEntry
                {
                    fileName = fi.Name,
                    fileSize = FormatFileSize(fi.Length),
                    fileSizeBytes = fi.Length,
                    uploadedBy = _currentUserName,
                    uploadedAt = DateTime.UtcNow.ToString("o"),
                    fileHash = hash,
                    seeders = new Dictionary<string, string> { { _currentUserName, myEndpoint } }
                };

                string url = _firebaseBaseUrl + "/shared_files.json";
                string json = JsonConvert.SerializeObject(entry);
                var resp = await FirebaseTrafficTracker.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

                if (!resp.IsSuccessStatusCode)
                {
                    MessageBox.Show("Failed to register file.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // GET FIREBASE KEY
                string respJson = await resp.Content.ReadAsStringAsync();
                var respObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(respJson);
                string fileKey = respObj["name"];
                entry.Key = fileKey;

                // STEP 3: SAVE FILE LOCALLY (NAMED BY FIREBASE KEY FOR EASY LOOKUP)
                UpdateProgress(2, 3, "Saving file locally...");
                string localPath = Path.Combine(_sharedFolder, fileKey);
                File.Copy(filePath, localPath, true);
//                 DebugLogger.Log("[FileShare] File operation: File.Copy");

                _files.RemoveAll(f => f.Key == fileKey);
                _files.Add(entry);
                _files = _files.OrderByDescending(f => f.uploadedAt).ToList();
                TeamLocalCacheStore.SaveList("shared_files_local.json", _files);

                UpdateProgress(3, 3, $"Shared: {fi.Name}");
                await Task.Delay(600);
                ShowProgress(false);
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[FileShare] Exception caught: " + ex.ToString());
                MessageBox.Show("Share failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isTransferring = false;
                ShowProgress(false);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // REFRESH — LOAD FILE METADATA FROM FIREBASE (TINY PAYLOAD)
        // ══════════════════════════════════════════════════════════════════════
        public async Task RefreshAsync()
        {
//             DebugLogger.Log("[FileShare] RefreshAsync() loading file list from Firebase");
            var localFiles = TeamLocalCacheStore.LoadList<FileShareEntry>("shared_files_local.json");
            if (_files.Count == 0 && localFiles.Count > 0)
            {
                _files = localFiles.OrderByDescending(f => f.uploadedAt).ToList();
                RenderFiles();
            }

            try
            {
                string url = _firebaseBaseUrl + "/shared_files.json";
                var response = await FirebaseTrafficTracker.GetAsync(url);
//                 DebugLogger.Log("[FileShare] Firebase operation: FirebaseTrafficTracker.GetAsync");
                if (!response.IsSuccessStatusCode)
                {
                    if (localFiles.Count > 0)
                    {
                        _files = localFiles.OrderByDescending(f => f.uploadedAt).ToList();
                        RenderFiles();
                    }
                    return;
                }

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    if (localFiles.Count > 0)
                    {
                        _files = localFiles.OrderByDescending(f => f.uploadedAt).ToList();
                        RenderFiles();
                    }
                    else
                    {
                        _files.Clear();
                        RenderFiles();
                    }
                    return;
                }

                var dict = JsonConvert.DeserializeObject<Dictionary<string, FileShareEntry>>(json);
                _files = dict.Select(kv =>
                {
                    var f = kv.Value;
                    f.Key = kv.Key;
                    return f;
                })
                .OrderByDescending(f => f.uploadedAt)
                .ToList();
                TeamLocalCacheStore.SaveList("shared_files_local.json", _files);

                RenderFiles();
            }
            catch
            {
                if (localFiles.Count > 0)
                {
                    _files = localFiles.OrderByDescending(f => f.uploadedAt).ToList();
                    RenderFiles();
                }
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // RENDER FILE LIST
        // ══════════════════════════════════════════════════════════════════════
        private void RenderFiles()
        {
            flowFiles.SuspendLayout();
            flowFiles.Controls.Clear();

            if (_files.Count == 0)
            {
                var lblEmpty = new Label
                {
                    Text = "No shared files yet.\nDrag & drop to share with your team!",
                    Font = new Font("Segoe UI", 9),
                    ForeColor = Color.FromArgb(100, 110, 120),
                    Size = new Size(flowFiles.Width - 20, 50),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(4, 20, 4, 4)
                };
                flowFiles.Controls.Add(lblEmpty);
            }

            foreach (var file in _files)
                flowFiles.Controls.Add(CreateFileRow(file));

            flowFiles.ResumeLayout(true);
        }

        // ══════════════════════════════════════════════════════════════════════
        // CREATE FILE ROW — WITH SEEDER COUNT AND P2P INDICATOR
        // ══════════════════════════════════════════════════════════════════════
        private Panel CreateFileRow(FileShareEntry file)
        {
            bool isCached = IsFileLocal(file.Key);

            var row = new Panel
            {
                Width = flowFiles.Width - 26,
                Height = 62,
                BackColor = _isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White,
                Margin = new Padding(2, 1, 2, 1)
            };

            // FILE ICON
            string ext = Path.GetExtension(file.fileName ?? "").ToLower();
            row.Controls.Add(new Label
            {
                Text = GetFileIcon(ext),
                Font = new Font("Segoe UI", 14),
                Location = new Point(4, 12),
                Size = new Size(28, 30),
                TextAlign = ContentAlignment.MiddleCenter
            });

            // FILE NAME
            row.Controls.Add(new Label
            {
                Text = file.fileName ?? "unknown",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = _isDarkMode ? Color.FromArgb(220, 224, 230) : Color.FromArgb(30, 41, 59),
                Location = new Point(34, 2),
                Size = new Size(row.Width - 120, 17),
                AutoEllipsis = true
            });

            // META LINE: uploader, size, date
            string date = DateTime.TryParse(file.uploadedAt, out var dt)
                ? dt.ToLocalTime().ToString("dd MMM HH:mm") : "";
            row.Controls.Add(new Label
            {
                Text = $"{file.uploadedBy}  ·  {file.fileSize}  ·  {date}",
                Font = new Font("Segoe UI", 7),
                ForeColor = Color.FromArgb(100, 110, 120),
                Location = new Point(34, 19),
                Size = new Size(row.Width - 120, 14),
                AutoEllipsis = true
            });

            // STATUS LINE: peer availability + download status
            int seeders = file.SeederCount;
            int onlineSeeders = GetOnlineSeederCount(file);
            string statusText;
            Color statusColor;

            if (isCached && file.uploadedBy == _currentUserName)
            {
                statusText = $"✅ Shared by You  ·  🌱 {seeders} seeder{(seeders != 1 ? "s" : "")}  ·  {onlineSeeders} online";
                statusColor = Color.FromArgb(34, 197, 94);
            }
            else if (isCached)
            {
                statusText = $"✅ Downloaded  ·  🌱 {seeders} seeder{(seeders != 1 ? "s" : "")}  ·  {onlineSeeders} online";
                statusColor = Color.FromArgb(34, 197, 94);
            }
            else if (onlineSeeders > 0)
            {
                statusText = $"📥 Available  ·  🌱 {onlineSeeders} source{(onlineSeeders != 1 ? "s" : "")} online";
                statusColor = Color.FromArgb(66, 165, 245);
            }
            else
            {
                statusText = "⚠ No Source Online — waiting for a peer";
                statusColor = Color.FromArgb(180, 130, 40);
            }

            row.Controls.Add(new Label
            {
                Text = statusText,
                Font = new Font("Segoe UI", 6.5f),
                ForeColor = statusColor,
                Location = new Point(34, 33),
                Size = new Size(row.Width - 120, 14),
                AutoEllipsis = true
            });

            // SEEDER BADGE (small circle with count)
            var seederBadge = new Label
            {
                Text = onlineSeeders.ToString(),
                Font = new Font("Segoe UI", 6.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = onlineSeeders > 0 ? Color.FromArgb(34, 197, 94) : Color.FromArgb(120, 120, 130),
                Size = new Size(18, 18),
                Location = new Point(34, 47),
                TextAlign = ContentAlignment.MiddleCenter
            };
            seederBadge.Paint += (s, ev) =>
            {
                ev.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var brush = new SolidBrush(seederBadge.BackColor))
                {
                    ev.Graphics.FillEllipse(brush, 0, 0, 17, 17);
                }
                TextRenderer.DrawText(ev.Graphics, seederBadge.Text, seederBadge.Font,
                    new Rectangle(0, 0, 18, 18), Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            row.Controls.Add(seederBadge);

            var seederLabel = new Label
            {
                Text = $"{onlineSeeders} online source{(onlineSeeders != 1 ? "s" : "")}",
                Font = new Font("Segoe UI", 6.5f),
                ForeColor = Color.FromArgb(90, 100, 110),
                Location = new Point(54, 49),
                Size = new Size(120, 13)
            };
            row.Controls.Add(seederLabel);

            // DOWNLOAD / OPEN BUTTON
            var btnDl = new Label
            {
                Text = isCached ? "⚡" : (onlineSeeders > 0 ? "⬇" : "⏳"),
                Font = new Font("Segoe UI", 13),
                ForeColor = isCached ? Color.FromArgb(34, 197, 94)
                    : (onlineSeeders > 0 ? Color.FromArgb(66, 165, 245) : Color.FromArgb(120, 120, 130)),
                Location = new Point(row.Width - 56, 14),
                Size = new Size(26, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = onlineSeeders > 0 || isCached ? Cursors.Hand : Cursors.Default
            };
            var f = file;
            btnDl.Click += async (s, ev) =>
            {
                if (isCached)
                {
                    // Open locally if cached
                    await DownloadFromPeerAsync(f);
                }
                else if (onlineSeeders > 0)
                {
                    await DownloadFromPeerAsync(f);
                }
                else
                {
                    MessageBox.Show("No sources are online for this file.\nPlease wait until a peer with the file comes online.",
                        "No Source Available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            };
            row.Controls.Add(btnDl);

            // DELETE BUTTON — ADMIN ONLY
            if (IsAdmin)
            {
                var btnDel = new Label
                {
                    Text = "✕",
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = Color.FromArgb(100, 110, 120),
                    Location = new Point(row.Width - 26, 18),
                    Size = new Size(20, 22),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Cursor = Cursors.Hand
                };
                btnDel.Click += async (s, ev) => await DeleteFileAsync(f);
                btnDel.MouseEnter += (s, ev) => btnDel.ForeColor = Color.FromArgb(239, 68, 68);
                btnDel.MouseLeave += (s, ev) => btnDel.ForeColor = Color.FromArgb(100, 110, 120);
                row.Controls.Add(btnDel);
            }

            // DOUBLE-CLICK TO OPEN IF LOCAL
            row.DoubleClick += (s, ev) =>
            {
                if (isCached)
                {
                    string localPath = Path.Combine(_sharedFolder, f.Key);
                    if (File.Exists(localPath))
                    {
//                         DebugLogger.Log("[FileShare] File operation: File.Exists");
                        try { System.Diagnostics.Process.Start(localPath); }
                        catch { }
                    }
                }
            };

            // RIGHT-CLICK CONTEXT MENU
            var ctx = new ContextMenuStrip();
            if (isCached)
            {
                ctx.Items.Add("Open File", null, (s, ev) =>
                {
                    try { System.Diagnostics.Process.Start(Path.Combine(_sharedFolder, f.Key)); } catch { }
                });
                ctx.Items.Add("Open Containing Folder", null, (s, ev) =>
                {
                    try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{Path.Combine(_sharedFolder, f.Key)}\""); } catch { }
                });
            }
            else if (onlineSeeders > 0)
            {
                ctx.Items.Add("Download", null, async (s, ev) => await DownloadFromPeerAsync(f));
            }
            ctx.Items.Add("Refresh Status", null, async (s, ev) => await RefreshAsync());
            if (IsAdmin)
            {
                ctx.Items.Add(new ToolStripSeparator());
                ctx.Items.Add("Delete (Admin)", null, async (s, ev) => await DeleteFileAsync(f));
                ((ToolStripMenuItem)ctx.Items[ctx.Items.Count - 1]).ForeColor = Color.FromArgb(239, 68, 68);
            }
            row.ContextMenuStrip = ctx;

            // ROW BORDER
            row.Paint += (s, ev) =>
            {
                using (var pen = new Pen(_isDarkMode ? Color.FromArgb(50, 58, 70) : Color.FromArgb(210, 215, 225)))
                    ev.Graphics.DrawRectangle(pen, 0, 0, row.Width - 1, row.Height - 1);
            };

            // HOVER EFFECT
            row.MouseEnter += (s, ev) => row.BackColor = _isDarkMode ? Color.FromArgb(44, 52, 66) : Color.FromArgb(245, 248, 252);
            row.MouseLeave += (s, ev) => row.BackColor = _isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White;

            return row;
        }

        // ══════════════════════════════════════════════════════════════════════
        // DOWNLOAD FROM PEER — CONNECTS VIA TCP TO A SEEDER
        // ZERO FIREBASE BANDWIDTH FOR THE ACTUAL FILE DATA!
        // ══════════════════════════════════════════════════════════════════════
        private async Task DownloadFromPeerAsync(FileShareEntry file)
        {
//             DebugLogger.Log("[FileShare] DownloadFromPeerAsync() started for file: " + file.fileName);
            if (_isTransferring)
            {
                MessageBox.Show("Please wait for the current transfer to finish.",
                    "Busy", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                byte[] fileData = null;

                // ── OPTION A: FILE IS ALREADY LOCAL (INSTANT!) ──
                if (IsFileLocal(file.Key))
                {
                    fileData = File.ReadAllBytes(Path.Combine(_sharedFolder, file.Key));
//                     DebugLogger.Log("[FileShare] File operation: File.ReadAllBytes");
                }
                else
                {
                    // ── OPTION B: DOWNLOAD FROM A PEER VIA TCP ──
                    if (file.seeders == null || file.seeders.Count == 0)
                    {
                        MessageBox.Show("No seeders available for this file.\nThe uploader may be offline.",
                            "No Seeders", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _isTransferring = true;
                    ShowProgress(true);

                    // TRY EACH SEEDER UNTIL ONE WORKS
                    foreach (var kv in file.seeders)
                    {
                        string seederName = kv.Key;
                        string endpoint = kv.Value;
                        if (string.IsNullOrEmpty(endpoint)) continue;

                        string[] parts = endpoint.Split(':');
                        if (parts.Length != 2 || !int.TryParse(parts[1], out int port)) continue;
                        string ip = parts[0];

                        UpdateProgress(0, 100, $"Connecting to {seederName} ({ip})...");

                        fileData = await TryDownloadFromPeerAsync(ip, port, file.Key, file.fileSizeBytes, file.fileName);

                        if (fileData != null)
                        {
                            // VERIFY HASH
                            if (!string.IsNullOrEmpty(file.fileHash))
                            {
                                string dlHash = ComputeHash(fileData);
                                if (dlHash != file.fileHash)
                                {
                                    fileData = null; // CORRUPTED — TRY NEXT SEEDER
                                    continue;
                                }
                            }
                            break; // SUCCESS!
                        }
                    }

                    if (fileData == null)
                    {
                        ShowProgress(false);
                        _isTransferring = false;
                        MessageBox.Show("Could not connect to any seeder.\nAll seeders may be offline.",
                            "Download Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    // SAVE LOCALLY AND REGISTER AS SEEDER
                    File.WriteAllBytes(Path.Combine(_sharedFolder, file.Key), fileData);
//                     DebugLogger.Log("[FileShare] File operation: File.WriteAllBytes");
                    await RegisterAsSeederAsync(file.Key);

                    UpdateProgress(100, 100, "Transfer complete!");
                    await Task.Delay(500);
                    ShowProgress(false);
                }

                // ASK WHERE TO SAVE
                using (var sfd = new SaveFileDialog())
                {
                    sfd.FileName = file.fileName;
                    sfd.Title = "Save file";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllBytes(sfd.FileName, fileData);
//                         DebugLogger.Log("[FileShare] File operation: File.WriteAllBytes");
                        MessageBox.Show(
                            $"File saved to:\n{sfd.FileName}\n\n\U0001f331 You are now a seeder for this file!",
                            "Downloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[FileShare] Exception caught: " + ex.ToString());
                MessageBox.Show("Download failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isTransferring = false;
                ShowProgress(false);
            }
        }

        /// <summary>
        /// TRIES TO DOWNLOAD A FILE FROM A SPECIFIC PEER VIA TCP.
        /// RETURNS THE FILE BYTES OR NULL IF CONNECTION FAILED.
        /// </summary>
        private async Task<byte[]> TryDownloadFromPeerAsync(string ip, int port, string fileKey, long expectedSize, string fileName)
        {
//             DebugLogger.Log("[FileShare] TryDownloadFromPeerAsync() connecting to peer: " + ip + ":" + port);
            try
            {
                using (var client = new TcpClient())
                {
                    // CONNECT WITH TIMEOUT (5 SECONDS)
                    var connectTask = client.ConnectAsync(ip, port);
                    if (await Task.WhenAny(connectTask, Task.Delay(5000)) != connectTask)
                        return null; // TIMEOUT

                    if (!client.Connected) return null;

                    using (var stream = client.GetStream())
                    {
                        stream.ReadTimeout = 30000;
                        stream.WriteTimeout = 10000;

                        // SEND FILE KEY (LINE-TERMINATED)
                        byte[] keyBytes = Encoding.UTF8.GetBytes(fileKey + "\n");
                        await stream.WriteAsync(keyBytes, 0, keyBytes.Length);
                        await stream.FlushAsync();

                        // READ FILE LENGTH (8 BYTES BIG-ENDIAN)
                        byte[] lenBuf = new byte[8];
                        int read = 0;
                        while (read < 8)
                        {
                            int n = await stream.ReadAsync(lenBuf, read, 8 - read);
                            if (n == 0) return null;
                            read += n;
                        }
                        if (BitConverter.IsLittleEndian) Array.Reverse(lenBuf);
                        long fileLen = BitConverter.ToInt64(lenBuf, 0);

                        if (fileLen <= 0) return null; // FILE NOT FOUND ON PEER

                        // READ FILE DATA WITH PROGRESS
                        byte[] data = new byte[fileLen];
                        long totalRead = 0;
                        while (totalRead < fileLen)
                        {
                            int toRead = (int)Math.Min(TCP_BUFFER_SIZE, fileLen - totalRead);
                            int n = await stream.ReadAsync(data, (int)totalRead, toRead);
                            if (n == 0) return null; // CONNECTION DROPPED
                            totalRead += n;

                            // UPDATE PROGRESS BAR
                            int pct = (int)((totalRead * 100) / fileLen);
                            if (this.InvokeRequired)
                                this.Invoke(new Action(() => UpdateProgress(pct, 100,
                                    $"Downloading {fileName}: {FormatFileSize(totalRead)} / {FormatFileSize(fileLen)}")));
                            else
                                UpdateProgress(pct, 100,
                                    $"Downloading {fileName}: {FormatFileSize(totalRead)} / {FormatFileSize(fileLen)}");
                        }

                        return data;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // REGISTER AS SEEDER — ADD MY IP:PORT TO FIREBASE SEEDER MAP
        // (ONLY ~50 BYTES IN FIREBASE!)
        // ══════════════════════════════════════════════════════════════════════
        private async Task RegisterAsSeederAsync(string fileKey)
        {
            try
            {
                string myEndpoint = _localIP + ":" + _tcpPort;
                string url = _firebaseBaseUrl + "/shared_files/" + fileKey + "/seeders.json";
                var payload = new Dictionary<string, string> { { _currentUserName, myEndpoint } };
                string json = JsonConvert.SerializeObject(payload);
                await FirebaseTrafficTracker.PatchAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════════════════
        // DELETE FILE — WITH PASSWORD AND AUDIT LOG
        // ══════════════════════════════════════════════════════════════════════
        private async Task DeleteFileAsync(FileShareEntry file)
        {
//             DebugLogger.Log("[FileShare] DeleteFileAsync() file: " + file.fileName);
            string password = PromptForPassword($"Enter your password to delete:\n\"{file.fileName}\"");
            if (password == null) return;

            if (VerifyPassword == null || !VerifyPassword(password))
            {
                MessageBox.Show("Incorrect password.", "Access Denied",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"Delete \"{file.fileName}\"?\n{file.fileSize}\n\nThis will be logged.",
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            try
            {
                // DELETE METADATA FROM FIREBASE
                await FirebaseTrafficTracker.DeleteAsync(_firebaseBaseUrl + "/shared_files/" + file.Key + ".json");
//                 DebugLogger.Log("[FileShare] Firebase operation: FirebaseTrafficTracker.DeleteAsync");

                // DELETE LOCAL COPY
                string localPath = Path.Combine(_sharedFolder, file.Key);
                if (File.Exists(localPath))
//                 DebugLogger.Log("[FileShare] File operation: File.Exists");
                    try { File.Delete(localPath); } catch { }
//                     DebugLogger.Log("[FileShare] File operation: File.Delete");

                _files.RemoveAll(f => f.Key == file.Key);
                TeamLocalCacheStore.SaveList("shared_files_local.json", _files);

                // AUDIT LOG
                var log = new FileDeleteLog
                {
                    fileName = file.fileName,
                    deletedBy = _currentUserName,
                    deletedAt = DateTime.UtcNow.ToString("o")
                };
                string logUrl = _firebaseBaseUrl + "/file_delete_logs.json";
                await FirebaseTrafficTracker.PostAsync(logUrl,
                    new StringContent(JsonConvert.SerializeObject(log), Encoding.UTF8, "application/json"));
//                 DebugLogger.Log("[FileShare] Firebase operation: FirebaseTrafficTracker.PostAsync");

                await RefreshAsync();
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[FileShare] Exception caught: " + ex.ToString());
                MessageBox.Show("Delete failed: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Count how many seeders are currently online for a given file.
        /// Uses the IsPeerOnline callback if available, otherwise assumes all seeders might be online.
        /// </summary>
        private int GetOnlineSeederCount(FileShareEntry file)
        {
            if (file.seeders == null || file.seeders.Count == 0) return 0;
            if (IsPeerOnline == null) return file.seeders.Count; // Fallback: assume all online

            int count = 0;
            foreach (var seeder in file.seeders.Keys)
            {
                if (seeder == _currentUserName || IsPeerOnline(seeder))
                    count++;
            }
            return count;
        }

        private bool IsFileLocal(string fileKey)
        {
            if (string.IsNullOrEmpty(fileKey)) return false;
            return File.Exists(Path.Combine(_sharedFolder, fileKey));
//             DebugLogger.Log("[FileShare] File operation: File.Exists");
        }

        private string ComputeHash(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>
        /// GET LOCAL LAN IP ADDRESS — USED FOR P2P CONNECTIONS
        /// </summary>
        private string GetLocalIPAddress()
        {
            try
            {
                // CONNECT TO A PUBLIC ADDRESS TO DETERMINE WHICH LOCAL INTERFACE IS USED
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 80);
                    var ep = socket.LocalEndPoint as IPEndPoint;
                    return ep?.Address.ToString() ?? "127.0.0.1";
                }
            }
            catch
            {
                return "127.0.0.1";
            }
        }

        // ── PASSWORD PROMPT ──
        private string PromptForPassword(string message)
        {
            var dlg = new Form
            {
                Text = "Password Required",
                Size = new Size(350, 180),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false, MinimizeBox = false,
                BackColor = _isDarkMode ? Color.FromArgb(24, 28, 36) : Color.FromArgb(245, 247, 250)
            };

            dlg.Controls.Add(new Label
            {
                Text = message, Location = new Point(15, 15), Size = new Size(310, 40),
                ForeColor = _isDarkMode ? Color.FromArgb(220, 224, 230) : Color.Black,
                Font = new Font("Segoe UI", 9)
            });

            var txt = new TextBox
            {
                Location = new Point(15, 60), Size = new Size(305, 26),
                UseSystemPasswordChar = true, Font = new Font("Segoe UI", 10),
                BackColor = _isDarkMode ? Color.FromArgb(38, 44, 56) : Color.White,
                ForeColor = _isDarkMode ? Color.FromArgb(220, 224, 230) : Color.Black,
                BorderStyle = BorderStyle.FixedSingle
            };
            dlg.Controls.Add(txt);

            var btnOK = new Button
            {
                Text = "Confirm", Location = new Point(15, 95), Size = new Size(145, 35),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(255, 127, 80),
                ForeColor = Color.White, Font = new Font("Segoe UI", 9, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnOK.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnOK);

            var btnCancel = new Button
            {
                Text = "Cancel", Location = new Point(175, 95), Size = new Size(145, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = _isDarkMode ? Color.FromArgb(38, 44, 56) : Color.FromArgb(200, 200, 210),
                ForeColor = _isDarkMode ? Color.FromArgb(160, 170, 180) : Color.FromArgb(80, 80, 90),
                Font = new Font("Segoe UI", 9), DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            dlg.Controls.Add(btnCancel);

            dlg.AcceptButton = btnOK;
            dlg.CancelButton = btnCancel;

            return dlg.ShowDialog() == DialogResult.OK ? txt.Text : null;
        }

        // ── PROGRESS HELPERS ──
        private void ShowProgress(bool visible)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => ShowProgress(visible))); return; }
            progressBar.Visible = visible;
            lblProgress.Visible = visible;
            if (!visible) { progressBar.Value = 0; lblProgress.Text = ""; }
        }

        private void UpdateProgress(int current, int total, string text)
        {
            if (this.InvokeRequired) { this.Invoke(new Action(() => UpdateProgress(current, total, text))); return; }
            progressBar.Maximum = Math.Max(total, 1);
            progressBar.Value = Math.Min(current, progressBar.Maximum);
            lblProgress.Text = text;
            lblProgress.Refresh();
            progressBar.Refresh();
        }

        // ── THEME ──
        public void ApplyTheme(bool darkMode, CustomTheme customTheme = null)
        {
            _isDarkMode = darkMode;
            if (customTheme != null && customTheme.Enabled)
            {
                this.BackColor = customTheme.GetCard();
                panelDropZone.BackColor = customTheme.GetBackground();
                lblTitle.ForeColor = customTheme.GetAccent();
                lblDropText.ForeColor = customTheme.GetSecondaryText();
            }
            else if (darkMode)
            {
                this.BackColor = ThemeConstants.Dark.BgElevated;
                panelDropZone.BackColor = ThemeConstants.Dark.BgBase;
                lblTitle.ForeColor = ThemeConstants.Dark.AccentPrimary;
                lblDropText.ForeColor = ThemeConstants.Dark.TextMuted;
            }
            else
            {
                this.BackColor = ThemeConstants.Light.BgBase;
                panelDropZone.BackColor = ThemeConstants.Light.BgElevated;
                lblTitle.ForeColor = ThemeConstants.Light.AccentPrimary;
                lblDropText.ForeColor = ThemeConstants.Light.TextMuted;
            }
            RenderFiles();
        }

        // ── FORMAT FILE SIZE ──
        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024L * 1024L * 1024L) return $"{bytes / 1024.0 / 1024.0:F1} MB";
            return $"{bytes / 1024.0 / 1024.0 / 1024.0:F1} GB";
        }

        private string GetFileIcon(string ext)
        {
            switch ((ext ?? "").ToLowerInvariant())
            {
                case ".pdf": return "PDF";
                case ".doc":
                case ".docx": return "DOC";
                case ".xls":
                case ".xlsx": return "XLS";
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".gif":
                case ".bmp":
                case ".webp": return "IMG";
                case ".zip":
                case ".rar":
                case ".7z": return "ZIP";
                case ".mp4":
                case ".avi":
                case ".mov":
                case ".mkv": return "VID";
                case ".mp3":
                case ".wav":
                case ".ogg": return "AUD";
                case ".txt":
                case ".cs":
                case ".json":
                case ".xml":
                case ".log": return "TXT";
                default: return "FILE";
            }
        }
    }
}
