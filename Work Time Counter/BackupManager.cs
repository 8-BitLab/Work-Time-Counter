// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        BackupManager.cs                                             ║
// ║  PURPOSE:     DUAL-LOCATION LOCAL BACKUP + OPTIONAL GITHUB REPO BACKUP      ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Mirrors all critical data files to a SECOND local folder, so that even    ║
// ║  if %AppData%\WorkFlow is lost, a full copy exists elsewhere.              ║
// ║                                                                            ║
// ║  Backup locations:                                                         ║
// ║    1. PRIMARY: %AppData%\WorkFlow\teams\{code}\  (normal app data)         ║
// ║    2. MIRROR:  %LOCALAPPDATA%\WorkFlowBackup\{code}\  (2nd local copy)     ║
// ║    3. OPTIONAL: GitHub private repo (versioned commits, admin-only setup)   ║
// ║                                                                            ║
// ║  Backup happens:                                                           ║
// ║    - After every save (critical files only)                                ║
// ║    - On app startup (full verify + repair)                                 ║
// ║    - On app close (full mirror sync)                                       ║
// ║    - On manual trigger (Settings panel button)                             ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  BACKUP SETTINGS — persisted to backup_settings.json
    //  GitHub repo settings are ADMIN-ONLY (set via TeamOptionsPanel).
    // ═══════════════════════════════════════════════════════════════════════════
    public class BackupSettings
    {
        /// <summary>Enable automatic mirror backup to second local folder.</summary>
        public bool MirrorBackupEnabled { get; set; } = true;

        /// <summary>Custom mirror path (null = use default %LOCALAPPDATA%\WorkFlowBackup).</summary>
        public string CustomMirrorPath { get; set; }

        // ── GITHUB REPO BACKUP (OPTIONAL — ADMIN SETS THIS) ──

        /// <summary>Enable GitHub repo backup. Only admin can toggle this.</summary>
        public bool GitHubBackupEnabled { get; set; } = false;

        /// <summary>GitHub Personal Access Token (with 'repo' scope). Admin sets this.
        /// Stored locally only — never sent to Firebase.</summary>
        public string GitHubToken { get; set; }

        /// <summary>GitHub repo owner (e.g. "8BitLabEngineering"). Admin sets this.</summary>
        public string GitHubRepoOwner { get; set; }

        /// <summary>GitHub repo name (e.g. "workflow-backup"). Admin sets this.</summary>
        public string GitHubRepoName { get; set; }

        /// <summary>Branch to push backups to (default "main").</summary>
        public string GitHubBranch { get; set; } = "main";

        /// <summary>Auto-push to GitHub every N minutes (0 = manual only).</summary>
        public int GitHubAutoBackupMinutes { get; set; } = 0;

        /// <summary>Last successful GitHub backup timestamp.</summary>
        public string LastGitHubBackupUtc { get; set; }

        /// <summary>Last successful mirror backup timestamp.</summary>
        public string LastMirrorBackupUtc { get; set; }

        /// <summary>Total number of GitHub commits made by this instance.</summary>
        public int GitHubCommitCount { get; set; } = 0;

        /// <summary>GitHub repo HTML URL for display in UI.</summary>
        public string GitHubRepoUrl { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  BACKUP MANAGER — handles all backup operations
    // ═══════════════════════════════════════════════════════════════════════════
    public static class BackupManager
    {
        private static readonly object _lock = new object();
        private static BackupSettings _settings;
        private static DateTime _lastGitHubPush = DateTime.MinValue;

        /// <summary>
        /// Critical files that MUST be backed up (relative to team folder).
        /// These contain user work data that cannot be recreated.
        /// </summary>
        private static readonly string[] CriticalTeamFiles = new[]
        {
            "team.bit",
            "users.bit",
            "settings.bit",
            "chat_local.json",
            "team_local_cache.db"
        };

        /// <summary>
        /// Critical global files (relative to %AppData%\WorkFlow).
        /// </summary>
        private static readonly string[] CriticalGlobalFiles = new[]
        {
            "teams_index.bit",
            "active_team.bit",
            "dm_local.json"
        };

        /// <summary>
        /// Critical organizer files (relative to %AppData%\WorkFlow\organizer).
        /// </summary>
        private static readonly string[] CriticalOrganizerFiles = new[]
        {
            "organizer_data.json",
            "organizer_settings.json"
        };

        // ═══════════════════════════════════════════════════════════════════
        //  INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize the backup manager. Call once at app startup.
        /// Loads settings and runs startup verify.
        /// </summary>
        public static void Initialize()
        {
            LoadSettings();
            DebugLogger.Log("[BackupManager] Initialized. Mirror=" + _settings.MirrorBackupEnabled +
                           ", GitHub=" + _settings.GitHubBackupEnabled);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  MIRROR BACKUP — copy critical files to 2nd local location
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Mirrors all critical data files to the backup location.
        /// Call this: on startup, on close, and after critical saves.
        /// </summary>
        public static void RunMirrorBackup()
        {
            if (!_settings.MirrorBackupEnabled) return;

            lock (_lock)
            {
                try
                {
                    string mirrorRoot = GetMirrorRoot();
                    string primaryRoot = GetPrimaryRoot();

                    // 1. Mirror global files
                    foreach (string file in CriticalGlobalFiles)
                    {
                        string src = Path.Combine(primaryRoot, file);
                        string dst = Path.Combine(mirrorRoot, file);
                        CopyIfNewer(src, dst);
                    }

                    // 2. Mirror organizer files
                    string orgSrc = Path.Combine(primaryRoot, "organizer");
                    string orgDst = Path.Combine(mirrorRoot, "organizer");
                    foreach (string file in CriticalOrganizerFiles)
                    {
                        CopyIfNewer(Path.Combine(orgSrc, file), Path.Combine(orgDst, file));
                    }

                    // 3. Mirror ALL team folders
                    string teamsDir = Path.Combine(primaryRoot, "teams");
                    if (Directory.Exists(teamsDir))
                    {
                        foreach (string teamDir in Directory.GetDirectories(teamsDir))
                        {
                            string teamCode = Path.GetFileName(teamDir);
                            string dstTeamDir = Path.Combine(mirrorRoot, "teams", teamCode);

                            foreach (string file in CriticalTeamFiles)
                            {
                                CopyIfNewer(Path.Combine(teamDir, file), Path.Combine(dstTeamDir, file));
                            }

                            // Also backup .bak files (SafeFileWriter creates these)
                            foreach (string file in CriticalTeamFiles)
                            {
                                string bakFile = file + ".bak";
                                CopyIfNewer(Path.Combine(teamDir, bakFile), Path.Combine(dstTeamDir, bakFile));
                            }
                        }
                    }

                    _settings.LastMirrorBackupUtc = DateTime.UtcNow.ToString("o");
                    SaveSettings();

                    DebugLogger.Log("[BackupManager] Mirror backup completed successfully.");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[BackupManager] Mirror backup ERROR: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Quick mirror of a single file after save.
        /// Call this from LocalChatStore, OrganizerStorage etc. after critical saves.
        /// </summary>
        public static void MirrorSingleFile(string sourceFilePath)
        {
            if (!_settings.MirrorBackupEnabled) return;
            if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath)) return;

            try
            {
                string primaryRoot = GetPrimaryRoot();
                string mirrorRoot = GetMirrorRoot();

                // Calculate relative path
                if (!sourceFilePath.StartsWith(primaryRoot, StringComparison.OrdinalIgnoreCase))
                    return; // Not in our domain

                string relativePath = sourceFilePath.Substring(primaryRoot.Length).TrimStart('\\', '/');
                string dstPath = Path.Combine(mirrorRoot, relativePath);

                CopyIfNewer(sourceFilePath, dstPath);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BackupManager] MirrorSingleFile ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Verify backup integrity on startup. Restores missing primary files from mirror.
        /// Returns number of files restored.
        /// </summary>
        public static int VerifyAndRepair()
        {
            if (!_settings.MirrorBackupEnabled) return 0;
            int restored = 0;

            lock (_lock)
            {
                try
                {
                    string mirrorRoot = GetMirrorRoot();
                    string primaryRoot = GetPrimaryRoot();

                    // Check global files
                    foreach (string file in CriticalGlobalFiles)
                    {
                        if (TryRestoreFromMirror(primaryRoot, mirrorRoot, file))
                            restored++;
                    }

                    // Check organizer files
                    foreach (string file in CriticalOrganizerFiles)
                    {
                        string relPath = Path.Combine("organizer", file);
                        if (TryRestoreFromMirror(primaryRoot, mirrorRoot, relPath))
                            restored++;
                    }

                    // Check team files
                    string mirrorTeams = Path.Combine(mirrorRoot, "teams");
                    if (Directory.Exists(mirrorTeams))
                    {
                        foreach (string mirrorTeamDir in Directory.GetDirectories(mirrorTeams))
                        {
                            string teamCode = Path.GetFileName(mirrorTeamDir);
                            foreach (string file in CriticalTeamFiles)
                            {
                                string relPath = Path.Combine("teams", teamCode, file);
                                if (TryRestoreFromMirror(primaryRoot, mirrorRoot, relPath))
                                    restored++;
                            }
                        }
                    }

                    if (restored > 0)
                        DebugLogger.Log($"[BackupManager] Verify & Repair: Restored {restored} files from mirror backup.");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[BackupManager] VerifyAndRepair ERROR: {ex.Message}");
                }
            }

            return restored;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  GITHUB REPO BACKUP — optional versioned cloud backup
        //  Admin sets: repo owner, repo name, personal access token.
        //  Every push creates a NEW COMMIT with full version history.
        //  Uses GitHub Contents API (no git binary needed on the machine).
        // ═══════════════════════════════════════════════════════════════════

        private static readonly HttpClient _ghHttp = new HttpClient();
        private static bool _ghHttpReady = false;

        /// <summary>
        /// Prepare the GitHub HTTP client with auth headers.
        /// </summary>
        private static void EnsureGitHubClient()
        {
            if (_ghHttpReady) return;
            _ghHttp.DefaultRequestHeaders.Clear();
            _ghHttp.DefaultRequestHeaders.Add("Authorization", $"token {_settings.GitHubToken}");
            _ghHttp.DefaultRequestHeaders.Add("User-Agent", "WorkFlow-8BitLab-Backup");
            _ghHttp.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
            _ghHttpReady = true;
        }

        /// <summary>
        /// Returns true if GitHub backup is fully configured (admin has set all fields).
        /// </summary>
        public static bool IsGitHubConfigured()
        {
            return _settings != null &&
                   _settings.GitHubBackupEnabled &&
                   !string.IsNullOrEmpty(_settings.GitHubToken) &&
                   !string.IsNullOrEmpty(_settings.GitHubRepoOwner) &&
                   !string.IsNullOrEmpty(_settings.GitHubRepoName);
        }

        /// <summary>
        /// Push all critical data files to the GitHub repo as a single commit.
        /// Each file is stored under: backup/{subfolder}/{filename}
        /// Every push creates a NEW commit → full version history preserved.
        /// ADMIN ONLY — called from settings panel or auto-timer.
        ///
        /// FLOW:
        ///   1. Get current commit SHA of the branch (HEAD)
        ///   2. Get the tree SHA from that commit
        ///   3. Create blobs for each data file
        ///   4. Create a new tree with all blobs
        ///   5. Create a new commit pointing to the new tree
        ///   6. Update the branch ref to the new commit
        /// </summary>
        public static async Task<bool> PushToGitHubAsync()
        {
            if (!IsGitHubConfigured())
            {
                DebugLogger.Log("[BackupManager] GitHub backup not configured (admin must set repo + token).");
                return false;
            }

            try
            {
                EnsureGitHubClient();

                string owner = _settings.GitHubRepoOwner;
                string repo = _settings.GitHubRepoName;
                string branch = _settings.GitHubBranch ?? "main";
                string apiBase = $"https://api.github.com/repos/{owner}/{repo}";

                // ── STEP 1: Get the current HEAD commit SHA ──
                string refUrl = $"{apiBase}/git/refs/heads/{branch}";
                var refResp = await _ghHttp.GetAsync(refUrl);

                string parentCommitSha = null;
                string baseTreeSha = null;

                if (refResp.IsSuccessStatusCode)
                {
                    string refJson = await refResp.Content.ReadAsStringAsync();
                    var refObj = JsonConvert.DeserializeAnonymousType(refJson,
                        new { @object = new { sha = "" } });
                    parentCommitSha = refObj?.@object?.sha;

                    // Get the tree from the parent commit
                    if (!string.IsNullOrEmpty(parentCommitSha))
                    {
                        var commitResp = await _ghHttp.GetAsync($"{apiBase}/git/commits/{parentCommitSha}");
                        if (commitResp.IsSuccessStatusCode)
                        {
                            string commitJson = await commitResp.Content.ReadAsStringAsync();
                            var commitObj = JsonConvert.DeserializeAnonymousType(commitJson,
                                new { tree = new { sha = "" } });
                            baseTreeSha = commitObj?.tree?.sha;
                        }
                    }
                }
                else
                {
                    // Branch doesn't exist or repo is empty — that's OK, first commit
                    DebugLogger.Log($"[BackupManager] GitHub branch '{branch}' not found — will create initial commit.");
                }

                // ── STEP 2: Collect all data files ──
                string primaryRoot = GetPrimaryRoot();
                var treeItems = new List<object>();
                int fileCount = 0;

                // Collect global files
                fileCount += await CreateBlobsForFiles(apiBase, treeItems, primaryRoot,
                    CriticalGlobalFiles, "backup/global");

                // Collect organizer files
                string orgFolder = Path.Combine(primaryRoot, "organizer");
                fileCount += await CreateBlobsForFiles(apiBase, treeItems, orgFolder,
                    CriticalOrganizerFiles, "backup/organizer");

                // Collect per-team files (including team_local_cache.db because it contains
                // local/pending log sync state and must be recoverable from backup history).
                string teamsDir = Path.Combine(primaryRoot, "teams");
                if (Directory.Exists(teamsDir))
                {
                    foreach (string teamDir in Directory.GetDirectories(teamsDir))
                    {
                        string teamCode = Path.GetFileName(teamDir);
                        fileCount += await CreateBlobsForFiles(apiBase, treeItems, teamDir,
                            CriticalTeamFiles, $"backup/teams/{teamCode}");
                    }
                }

                // Add metadata file
                string metadataJson = JsonConvert.SerializeObject(new
                {
                    app = "WorkFlow by 8BitLab",
                    backupUtc = DateTime.UtcNow.ToString("o"),
                    machine = Environment.MachineName,
                    userName = Environment.UserName,
                    fileCount = fileCount,
                    commitNumber = _settings.GitHubCommitCount + 1
                }, Formatting.Indented);

                string metaSha = await CreateBlob(apiBase, metadataJson);
                if (!string.IsNullOrEmpty(metaSha))
                {
                    treeItems.Add(new { path = "backup/_metadata.json", mode = "100644", type = "blob", sha = metaSha });
                }

                if (treeItems.Count == 0)
                {
                    DebugLogger.Log("[BackupManager] No files to push to GitHub.");
                    return false;
                }

                // ── STEP 3: Create a new tree ──
                var treePayload = new Dictionary<string, object>
                {
                    { "tree", treeItems }
                };
                // Use base_tree so we don't delete files not in our backup set
                if (!string.IsNullOrEmpty(baseTreeSha))
                    treePayload["base_tree"] = baseTreeSha;

                string treeRespJson = await PostJson($"{apiBase}/git/trees",
                    JsonConvert.SerializeObject(treePayload));
                if (treeRespJson == null) return false;

                var treeResult = JsonConvert.DeserializeAnonymousType(treeRespJson, new { sha = "" });
                string newTreeSha = treeResult?.sha;
                if (string.IsNullOrEmpty(newTreeSha))
                {
                    DebugLogger.Log("[BackupManager] GitHub: Failed to create tree.");
                    return false;
                }

                // ── STEP 4: Create a new commit ──
                string commitMsg = $"WorkFlow backup — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n" +
                                   $"Files: {fileCount} | Machine: {Environment.MachineName}";

                var commitPayload = new Dictionary<string, object>
                {
                    { "message", commitMsg },
                    { "tree", newTreeSha }
                };
                if (!string.IsNullOrEmpty(parentCommitSha))
                    commitPayload["parents"] = new[] { parentCommitSha };

                string commitRespJson = await PostJson($"{apiBase}/git/commits",
                    JsonConvert.SerializeObject(commitPayload));
                if (commitRespJson == null) return false;

                var commitResult = JsonConvert.DeserializeAnonymousType(commitRespJson,
                    new { sha = "", html_url = "" });
                string newCommitSha = commitResult?.sha;
                if (string.IsNullOrEmpty(newCommitSha))
                {
                    DebugLogger.Log("[BackupManager] GitHub: Failed to create commit.");
                    return false;
                }

                // ── STEP 5: Update branch ref to point to new commit ──
                bool refUpdated;
                if (!string.IsNullOrEmpty(parentCommitSha))
                {
                    // Update existing ref
                    string updateJson = JsonConvert.SerializeObject(new { sha = newCommitSha, force = false });
                    var updateReq = new HttpRequestMessage(new HttpMethod("PATCH"), refUrl)
                    {
                        Content = new StringContent(updateJson, Encoding.UTF8, "application/json")
                    };
                    var updateResp = await _ghHttp.SendAsync(updateReq);
                    refUpdated = updateResp.IsSuccessStatusCode;

                    if (!refUpdated)
                    {
                        string err = await updateResp.Content.ReadAsStringAsync();
                        DebugLogger.Log($"[BackupManager] GitHub: Failed to update ref: {err}");
                    }
                }
                else
                {
                    // Create new ref (first commit)
                    string createRefJson = JsonConvert.SerializeObject(new
                    {
                        @ref = $"refs/heads/{branch}",
                        sha = newCommitSha
                    });
                    string createRefResp = await PostJson($"{apiBase}/git/refs", createRefJson);
                    refUpdated = createRefResp != null;
                }

                if (refUpdated)
                {
                    _settings.LastGitHubBackupUtc = DateTime.UtcNow.ToString("o");
                    _settings.GitHubCommitCount++;
                    _settings.GitHubRepoUrl = $"https://github.com/{owner}/{repo}";
                    _lastGitHubPush = DateTime.UtcNow;
                    SaveSettings();

                    DebugLogger.Log($"[BackupManager] GitHub backup committed successfully. " +
                                   $"Files: {fileCount}, Commit #{_settings.GitHubCommitCount}, SHA: {newCommitSha.Substring(0, 7)}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BackupManager] GitHub backup ERROR: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if it's time for an automatic GitHub push (based on interval setting).
        /// Call from a timer tick. Does nothing if not configured or interval not reached.
        /// </summary>
        public static async Task CheckAutoGitHubBackupAsync()
        {
            if (!IsGitHubConfigured() || _settings.GitHubAutoBackupMinutes <= 0)
                return;

            if ((DateTime.UtcNow - _lastGitHubPush).TotalMinutes >= _settings.GitHubAutoBackupMinutes)
            {
                await PushToGitHubAsync();
            }
        }

        /// <summary>
        /// Test GitHub connection. Returns (success, message) for UI feedback.
        /// ADMIN ONLY.
        /// </summary>
        public static async Task<(bool success, string message)> TestGitHubConnectionAsync()
        {
            if (!IsGitHubConfigured())
                return (false, "GitHub backup not configured. Set repo owner, name, and token.");

            try
            {
                EnsureGitHubClient();
                string url = $"https://api.github.com/repos/{_settings.GitHubRepoOwner}/{_settings.GitHubRepoName}";
                var resp = await _ghHttp.GetAsync(url);

                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    var repoInfo = JsonConvert.DeserializeAnonymousType(json,
                        new { full_name = "", @private = false, html_url = "" });

                    _settings.GitHubRepoUrl = repoInfo?.html_url;
                    SaveSettings();

                    string visibility = repoInfo?.@private == true ? "private" : "PUBLIC (consider making private!)";
                    return (true, $"Connected to {repoInfo?.full_name} ({visibility})");
                }
                else if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (false, $"Repo '{_settings.GitHubRepoOwner}/{_settings.GitHubRepoName}' not found. " +
                                   "Check the name or create it on GitHub first.");
                }
                else if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "Token is invalid or expired. Generate a new one at GitHub → Settings → Developer Settings → Personal Access Tokens.");
                }
                else
                {
                    string body = await resp.Content.ReadAsStringAsync();
                    return (false, $"GitHub returned {resp.StatusCode}: {body}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  GITHUB API HELPERS — blob creation, JSON posting
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Create a blob for a single file. Returns the blob SHA, or null on failure.
        /// </summary>
        private static async Task<string> CreateBlob(string apiBase, string content)
        {
            try
            {
                string payload = JsonConvert.SerializeObject(new
                {
                    content = content,
                    encoding = "utf-8"
                });

                string resp = await PostJson($"{apiBase}/git/blobs", payload);
                if (resp == null) return null;

                var result = JsonConvert.DeserializeAnonymousType(resp, new { sha = "" });
                return result?.sha;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create a blob for binary file content (base64-encoded).
        /// </summary>
        private static async Task<string> CreateBlobBase64(string apiBase, byte[] contentBytes)
        {
            try
            {
                if (contentBytes == null || contentBytes.Length == 0)
                    return null;

                string payload = JsonConvert.SerializeObject(new
                {
                    content = Convert.ToBase64String(contentBytes),
                    encoding = "base64"
                });

                string resp = await PostJson($"{apiBase}/git/blobs", payload);
                if (resp == null) return null;

                var result = JsonConvert.DeserializeAnonymousType(resp, new { sha = "" });
                return result?.sha;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create blobs for multiple files and add them to the tree items list.
        /// Returns the number of files successfully added.
        /// </summary>
        private static async Task<int> CreateBlobsForFiles(
            string apiBase,
            List<object> treeItems,
            string folder,
            string[] fileNames,
            string repoPath)
        {
            int count = 0;
            foreach (string file in fileNames)
            {
                string fullPath = Path.Combine(folder, file);
                if (!File.Exists(fullPath)) continue;

                try
                {
                    string blobSha = null;
                    if (file.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
                    {
                        byte[] bytes = File.ReadAllBytes(fullPath);
                        blobSha = await CreateBlobBase64(apiBase, bytes);
                    }
                    else
                    {
                        string content = File.ReadAllText(fullPath);
                        if (string.IsNullOrWhiteSpace(content)) continue;
                        blobSha = await CreateBlob(apiBase, content);
                    }

                    if (!string.IsNullOrEmpty(blobSha))
                    {
                        treeItems.Add(new
                        {
                            path = $"{repoPath}/{file}",
                            mode = "100644",
                            type = "blob",
                            sha = blobSha
                        });
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[BackupManager] GitHub blob error for {file}: {ex.Message}");
                }
            }
            return count;
        }

        /// <summary>
        /// POST JSON to a GitHub API endpoint. Returns response body or null on failure.
        /// </summary>
        private static async Task<string> PostJson(string url, string jsonPayload)
        {
            try
            {
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var resp = await _ghHttp.PostAsync(url, content);

                string body = await resp.Content.ReadAsStringAsync();
                if (resp.IsSuccessStatusCode)
                    return body;

                DebugLogger.Log($"[BackupManager] GitHub POST failed ({resp.StatusCode}): {body}");
                return null;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BackupManager] GitHub POST error: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SETTINGS ACCESSORS — for UI (TeamOptionsPanel / Settings)
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>Current backup settings (read-only copy).</summary>
        public static BackupSettings GetSettings()
        {
            return _settings ?? new BackupSettings();
        }

        /// <summary>Update settings from UI.</summary>
        public static void UpdateSettings(BackupSettings newSettings)
        {
            _settings = newSettings ?? new BackupSettings();
            SaveSettings();
        }

        /// <summary>Returns the mirror backup folder path for display.</summary>
        public static string GetMirrorPath()
        {
            return GetMirrorRoot();
        }

        /// <summary>Returns formatted last backup times for display.</summary>
        public static string GetBackupStatus()
        {
            var parts = new List<string>();

            if (_settings.MirrorBackupEnabled)
            {
                string mirrorTime = FormatBackupTime(_settings.LastMirrorBackupUtc);
                parts.Add($"Mirror: {mirrorTime}");
            }

            if (_settings.GitHubBackupEnabled)
            {
                string ghTime = FormatBackupTime(_settings.LastGitHubBackupUtc);
                parts.Add($"GitHub: {ghTime}");
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : "Backup disabled";
        }

        private static string FormatBackupTime(string utcText)
        {
            if (string.IsNullOrWhiteSpace(utcText))
                return "Never";

            DateTime parsed;
            if (!DateTime.TryParse(utcText, out parsed))
                return "Never";

            return parsed.ToLocalTime().ToString("g");
        }

        // ═══════════════════════════════════════════════════════════════════
        //  INTERNAL HELPERS
        // ═══════════════════════════════════════════════════════════════════

        private static string GetPrimaryRoot()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "WorkFlow");
        }

        private static string GetMirrorRoot()
        {
            if (!string.IsNullOrEmpty(_settings?.CustomMirrorPath))
                return _settings.CustomMirrorPath;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "WorkFlowBackup");
        }

        private static string GetSettingsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "WorkFlow");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "backup_settings.json");
        }

        private static void LoadSettings()
        {
            try
            {
                string path = GetSettingsPath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    _settings = JsonConvert.DeserializeObject<BackupSettings>(json) ?? new BackupSettings();
                }
                else
                {
                    _settings = new BackupSettings();
                }
            }
            catch
            {
                _settings = new BackupSettings();
            }
        }

        private static void SaveSettings()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(GetSettingsPath(), json);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BackupManager] SaveSettings ERROR: {ex.Message}");
            }
        }

        /// <summary>
        /// Copy source to destination only if source is newer or destination doesn't exist.
        /// Creates destination directory if needed.
        /// </summary>
        private static void CopyIfNewer(string src, string dst)
        {
            try
            {
                if (!File.Exists(src)) return;

                string dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dstDir) && !Directory.Exists(dstDir))
                    Directory.CreateDirectory(dstDir);

                if (!File.Exists(dst))
                {
                    File.Copy(src, dst, true);
                    return;
                }

                // Only copy if source is newer
                if (File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dst))
                    File.Copy(src, dst, true);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BackupManager] CopyIfNewer ERROR ({Path.GetFileName(src)}): {ex.Message}");
            }
        }

        /// <summary>
        /// Try to restore a file from mirror to primary (if primary is missing/corrupted).
        /// Returns true if restoration happened.
        /// </summary>
        private static bool TryRestoreFromMirror(string primaryRoot, string mirrorRoot, string relativePath)
        {
            string primary = Path.Combine(primaryRoot, relativePath);
            string mirror = Path.Combine(mirrorRoot, relativePath);

            try
            {
                // Primary exists and is valid — nothing to do
                if (File.Exists(primary))
                {
                    var fi = new FileInfo(primary);
                    if (fi.Length > 0) return false;
                }

                // Primary missing or empty — restore from mirror
                if (!File.Exists(mirror)) return false;

                string dir = Path.GetDirectoryName(primary);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.Copy(mirror, primary, true);
                DebugLogger.Log($"[BackupManager] RESTORED from mirror: {relativePath}");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[BackupManager] Restore failed for {relativePath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Force-reset the GitHub HTTP client (e.g. when admin changes the token).
        /// </summary>
        public static void ResetGitHubClient()
        {
            _ghHttpReady = false;
        }
    }
}
