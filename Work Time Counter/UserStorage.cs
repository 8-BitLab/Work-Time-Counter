// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        UserStorage.cs                                               ║
// ║  PURPOSE:     LOCAL AND FIREBASE DATA PERSISTENCE LAYER                    ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  Github:      https://github.com/8BitLabEngineering                        ║
// ║                                                                            ║
// ║  MULTI-TEAM SUPPORT:                                                       ║
// ║  Each team's data is stored in its own subfolder under:                     ║
// ║    %AppData%\WorkFlow\teams\{JoinCode}\                                    ║
// ║      team.bit      — TeamInfo JSON                                         ║
// ║      users.bit     — user passwords/metadata JSON                          ║
// ║      settings.bit  — last logged-in user for this team                     ║
// ║      remember.bit  — remembered password hash for this team                ║
// ║                                                                            ║
// ║  Root-level index files:                                                   ║
// ║    %AppData%\WorkFlow\teams_index.bit  — list of joined teams              ║
// ║    %AppData%\WorkFlow\active_team.bit  — currently active join code        ║
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
    //  TEAM INDEX ENTRY — one entry per joined team in teams_index.bit
    // ═══════════════════════════════════════════════════════════════════════════
    public class TeamIndexEntry
    {
        public string JoinCode { get; set; }
        public string TeamName { get; set; }
        public string UserName { get; set; }  // this user's name in the team
    }

    public class UserStorageEntry
    {
        public string PasswordHash { get; set; }
        public bool IsDefaultPassword { get; set; }
        public bool IsAdmin { get; set; }
        public string TeamJoinCode { get; set; }
        public string Color { get; set; }
        public string Title { get; set; }
        public string Role { get; set; }
        public string Country { get; set; }
        public double WeeklyHourLimit { get; set; }
    }

    public static class UserStorage
    {
        private static readonly HttpClient _http = new HttpClient();
        private static readonly string DefaultFirebaseBase =
            "https://csharptimelogger-default-rtdb.europe-west1.firebasedatabase.app";

        // ══════════════════════════════════════════════════════════
        // LOCAL FOLDER PATHS — Helper methods to resolve file paths
        // These methods construct paths for storing local team data
        // in %AppData%\WorkFlow\ directory structure
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the root WorkFlow folder: %AppData%\WorkFlow\
        /// Creates it if it doesn't exist. This is where the teams_index.bit
        /// and active_team.bit files are stored.
        /// </summary>
        private static string GetFolderPath()
        {
//             DebugLogger.Log("[UserStorage] >> GetFolderPath() called");
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "WorkFlow");
            if (!Directory.Exists(folder))
            {
//                 DebugLogger.Log($"[UserStorage] Creating root folder: {folder}");
                Directory.CreateDirectory(folder);
            }
//             DebugLogger.Log($"[UserStorage] Root folder path: {folder}");
            return folder;
        }

        /// <summary>
        /// Returns the subfolder for a specific team: %AppData%\WorkFlow\teams\{joinCode}\
        /// Each team gets its own subfolder named by its join code (uppercase).
        /// This isolates each team's data (team.bit, users.bit, settings.bit, remember.bit).
        /// </summary>
        private static string GetTeamFolderPath(string joinCode)
        {
//             DebugLogger.Log("[UserStorage] >> GetTeamFolderPath() called");
//             DebugLogger.Log($"[UserStorage] Team join code: {joinCode}");

            string folder = Path.Combine(GetFolderPath(), "teams", joinCode.ToUpper());
            if (!Directory.Exists(folder))
            {
//                 DebugLogger.Log($"[UserStorage] Creating team folder: {folder}");
                Directory.CreateDirectory(folder);
            }
//             DebugLogger.Log($"[UserStorage] Team folder path: {folder}");
            return folder;
        }

        /// <summary>Returns the subfolder for the currently active team.</summary>
        private static string GetActiveTeamFolderPath()
        {
//             DebugLogger.Log("[UserStorage] >> GetActiveTeamFolderPath() called");
            string activeCode = GetActiveTeamCode();
            if (string.IsNullOrEmpty(activeCode)) return GetFolderPath(); // fallback to root (legacy)
            return GetTeamFolderPath(activeCode);
        }

        // ── Per-team file paths (inside the active team's subfolder) ──
        private static string GetStorageFilePath()
        {
//             DebugLogger.Log("[UserStorage] >> GetStorageFilePath() called");
            return Path.Combine(GetActiveTeamFolderPath(), "users.bit");
        }

        private static string GetSettingsFilePath()
        {
//             DebugLogger.Log("[UserStorage] >> GetSettingsFilePath() called");
            return Path.Combine(GetActiveTeamFolderPath(), "settings.bit");
        }

        private static string GetTeamFilePath()
        {
//             DebugLogger.Log("[UserStorage] >> GetTeamFilePath() called");
            return Path.Combine(GetActiveTeamFolderPath(), "team.bit");
        }

        private static string GetRememberFilePath()
        {
//             DebugLogger.Log("[UserStorage] >> GetRememberFilePath() called");
            return Path.Combine(GetActiveTeamFolderPath(), "remember.bit");
        }

        // ── Root-level index files ──
        private static string GetTeamsIndexFilePath()
        {
//             DebugLogger.Log("[UserStorage] >> GetTeamsIndexFilePath() called");
            return Path.Combine(GetFolderPath(), "teams_index.bit");
        }

        private static string GetActiveTeamFilePath()
        {
//             DebugLogger.Log("[UserStorage] >> GetActiveTeamFilePath() called");
            return Path.Combine(GetFolderPath(), "active_team.bit");
        }

        private static string GetTeamSwitcherStartupPrefPath()
        {
            return Path.Combine(GetFolderPath(), "team_switcher_startup_pref.bit");
        }

        private static string GetTeamSwitcherOnceSkipPath()
        {
            return Path.Combine(GetFolderPath(), "team_switcher_once_skip.bit");
        }

        // ══════════════════════════════════════════════════════════
        // MULTI-TEAM INDEX — tracks all teams this machine has joined
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the list of all teams this machine has joined.
        /// Reads from %AppData%\WorkFlow\teams_index.bit which contains a JSON array
        /// of TeamIndexEntry objects. Each entry tracks JoinCode, TeamName, and the user's
        /// login name in that team.
        /// Returns empty list if file doesn't exist or on error.
        /// </summary>
        public static List<TeamIndexEntry> GetJoinedTeams()
        {
//             DebugLogger.Log("[UserStorage] >> GetJoinedTeams() called");
            try
            {
                string path = GetTeamsIndexFilePath();
//                 DebugLogger.Log($"[UserStorage] Teams index path: {path}");

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
//                     DebugLogger.Log($"[UserStorage] 📖 Loaded teams_index.bit from: {path}");

                    var list = JsonConvert.DeserializeObject<List<TeamIndexEntry>>(json);
                    if (list != null)
                    {
//                         DebugLogger.Log($"[UserStorage] ✓ Deserialized {list.Count} teams from index");
                        return list;
                    }
                }
                else
                {
//                     DebugLogger.Log($"[UserStorage] Teams index file not found at: {path} (first-time setup?)");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception loading teams index: {ex.Message}");
            }
//             DebugLogger.Log("[UserStorage] Returning empty teams list");
            return new List<TeamIndexEntry>();
        }

        /// <summary>
        /// Saves the teams index to %AppData%\WorkFlow\teams_index.bit
        /// Serializes the list of TeamIndexEntry objects to JSON with indentation.
        /// This persists the list of all teams this machine has joined.
        /// </summary>
        public static void SaveJoinedTeams(List<TeamIndexEntry> teams)
        {
//             DebugLogger.Log("[UserStorage] >> SaveJoinedTeams() called");
//             DebugLogger.Log($"[UserStorage] Saving {teams.Count} teams to index");

            try
            {
                string json = JsonConvert.SerializeObject(teams, Formatting.Indented);
                string path = GetTeamsIndexFilePath();
                File.WriteAllText(path, json);
//                 DebugLogger.Log($"[UserStorage] ✍️ Successfully wrote teams index ({teams.Count} teams) to: {path}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception saving teams index: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a team to the index, or updates it if already present.
        /// Case-insensitive lookup by join code. Used when joining a team or updating
        /// team metadata locally.
        /// </summary>
        public static void AddTeamToIndex(string joinCode, string teamName, string userName)
        {
//             DebugLogger.Log("[UserStorage] >> AddTeamToIndex() called");
//             DebugLogger.Log($"[UserStorage] Adding/updating team: joinCode={joinCode}, teamName={teamName}, userName={userName}");

            var teams = GetJoinedTeams();
            var existing = teams.FirstOrDefault(t =>
                t.JoinCode.Equals(joinCode, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
//                 DebugLogger.Log($"[UserStorage] Team already in index, updating metadata");
                existing.TeamName = teamName;
                existing.UserName = userName;
            }
            else
            {
//                 DebugLogger.Log($"[UserStorage] New team being added to index");
                teams.Add(new TeamIndexEntry
                {
                    JoinCode = joinCode.ToUpper(),
                    TeamName = teamName,
                    UserName = userName
                });
            }
            SaveJoinedTeams(teams);
        }

        /// <summary>Removes a team from the index.</summary>
        public static void RemoveTeamFromIndex(string joinCode)
        {
//             DebugLogger.Log("[UserStorage] >> RemoveTeamFromIndex() called");
            
            var teams = GetJoinedTeams();
            teams.RemoveAll(t => t.JoinCode.Equals(joinCode, StringComparison.OrdinalIgnoreCase));
            SaveJoinedTeams(teams);
        }

        // ══════════════════════════════════════════════════════════
        // ACTIVE TEAM — which team is currently loaded
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the join code of the currently active team.
        /// Reads from %AppData%\WorkFlow\active_team.bit which contains a single
        /// team join code. Returns null if not set.
        /// </summary>
        public static string GetActiveTeamCode()
        {
//             DebugLogger.Log("[UserStorage] >> GetActiveTeamCode() called");
            try
            {
                string path = GetActiveTeamFilePath();
                if (File.Exists(path))
                {
                    string activeCode = File.ReadAllText(path).Trim().ToUpper();
//                     DebugLogger.Log($"[UserStorage] 📖 Active team code loaded: {activeCode} from {path}");
                    return activeCode;
                }
                else
                {
//                     DebugLogger.Log($"[UserStorage] Active team file not found at: {path}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Error reading active team: {ex.Message}");
            }
            return null;
        }

        /// <summary>Sets the active team by join code.</summary>
        public static void SetActiveTeamCode(string joinCode)
        {
//             DebugLogger.Log("[UserStorage] >> SetActiveTeamCode() called");
            
            try
            {
                File.WriteAllText(GetActiveTeamFilePath(), joinCode?.ToUpper() ?? "");
            }
            catch { }
        }

        /// <summary>Switches the active team. Returns true if the team exists in the index.</summary>
        public static bool SwitchTeam(string joinCode)
        {
//             DebugLogger.Log("[UserStorage] >> SwitchTeam() called");
            string normalizedCode = (joinCode ?? "").Trim().ToUpper();
            if (string.IsNullOrWhiteSpace(normalizedCode))
                return false;

            var teams = GetJoinedTeams();
            var entry = teams.FirstOrDefault(t =>
                !string.IsNullOrWhiteSpace(t?.JoinCode) &&
                t.JoinCode.Trim().Equals(normalizedCode, StringComparison.OrdinalIgnoreCase));
            if (entry == null) return false;

            SetActiveTeamCode(normalizedCode);
            string activeCode = GetActiveTeamCode();
            return !string.IsNullOrWhiteSpace(activeCode) &&
                   activeCode.Equals(normalizedCode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Clears local runtime/cache artifacts for a specific team folder.
        /// Keeps team.bit/users.bit/settings/remember and removes transient board/chat/log/file caches.
        /// </summary>
        public static void ClearTeamLocalRuntimeCache(string joinCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(joinCode))
                    return;

                string teamFolder = GetTeamFolderPath(joinCode.Trim().ToUpperInvariant());
                if (!Directory.Exists(teamFolder))
                    return;

                string[] filesToDelete =
                {
                    "chat_local.json",
                    "stickers_local.json",
                    "logs_local.json",
                    "shared_files_local.json",
                    "pending_stop_logs.json",
                    "pending_log_upserts.json",
                    "team_local_cache.db"
                };

                foreach (string file in filesToDelete)
                {
                    string path = Path.Combine(teamFolder, file);
                    try
                    {
                        if (File.Exists(path))
                            File.Delete(path);
                    }
                    catch { }
                }

                string sharedFilesFolder = Path.Combine(teamFolder, "SharedFiles");
                try
                {
                    if (Directory.Exists(sharedFilesFolder))
                    {
                        foreach (string filePath in Directory.GetFiles(sharedFilesFolder))
                        {
                            try { File.Delete(filePath); } catch { }
                        }
                    }
                }
                catch { }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Returns true when local chat cache appears contaminated with users not in this team's member list.
        /// Used to auto-heal cross-team cache leakage.
        /// </summary>
        public static bool HasForeignUsersInLocalChatCache(TeamInfo team)
        {
            try
            {
                if (team == null || string.IsNullOrWhiteSpace(team.JoinCode))
                    return false;

                string teamFolder = GetTeamFolderPath(team.JoinCode.Trim().ToUpperInvariant());
                string chatPath = Path.Combine(teamFolder, "chat_local.json");
                if (!File.Exists(chatPath))
                    return false;

                string json = File.ReadAllText(chatPath);
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                    return false;

                var chat = JsonConvert.DeserializeObject<Dictionary<string, ChatMessage>>(json);
                if (chat == null || chat.Count == 0)
                    return false;

                var members = new HashSet<string>(
                    (team.Members ?? new List<string>()).Where(m => !string.IsNullOrWhiteSpace(m)),
                    StringComparer.OrdinalIgnoreCase);

                if (!string.IsNullOrWhiteSpace(team.AdminName))
                    members.Add(team.AdminName);

                if (members.Count == 0)
                    return false;

                foreach (var msg in chat.Values)
                {
                    string user = msg?.user?.Trim();
                    if (string.IsNullOrWhiteSpace(user))
                        continue;
                    if (!members.Contains(user))
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true when team switcher should be skipped at startup and the app should
        /// auto-open the last active team. Default is false (show switcher).
        /// </summary>
        public static bool GetSkipTeamSwitcherOnStartup()
        {
            try
            {
                string path = GetTeamSwitcherStartupPrefPath();
                if (!File.Exists(path))
                    return false;

                string value = File.ReadAllText(path).Trim();
                return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                       value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Sets whether team switcher is skipped at startup.
        /// true = auto-open last active team, false = show team switcher.
        /// </summary>
        public static void SetSkipTeamSwitcherOnStartup(bool skip)
        {
            try
            {
                File.WriteAllText(GetTeamSwitcherStartupPrefPath(), skip ? "1" : "0");
            }
            catch
            {
            }
        }

        /// <summary>
        /// Sets a one-time flag to skip team switcher on next startup only.
        /// Used after explicit in-app team switch so app opens directly to main panel.
        /// </summary>
        public static void SetSkipTeamSwitcherOnce(bool skip)
        {
            try
            {
                string path = GetTeamSwitcherOnceSkipPath();
                if (skip)
                    File.WriteAllText(path, "1");
                else if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Returns true if one-time startup skip is set, then clears it.
        /// </summary>
        public static bool GetAndClearSkipTeamSwitcherOnce()
        {
            try
            {
                string path = GetTeamSwitcherOnceSkipPath();
                if (!File.Exists(path))
                    return false;

                string value = File.ReadAllText(path).Trim();
                File.Delete(path);

                return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                       value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════
        // LEGACY MIGRATION — moves old single-team data to new structure
        // Called once on first launch after update.
        // ══════════════════════════════════════════════════════════

        /// <summary>Migrates legacy single-team data into the new per-team subfolder structure.
        /// Safe to call multiple times — does nothing if already migrated.</summary>
        public static void MigrateLegacyData()
        {
//             DebugLogger.Log("[UserStorage] >> MigrateLegacyData() called");
            string root = GetFolderPath();
            string oldTeamFile = Path.Combine(root, "team.bit");

            // Already migrated or no legacy data
            if (!File.Exists(oldTeamFile)) return;
            // If we already have a teams index, skip
            if (File.Exists(GetTeamsIndexFilePath())) return;

            try
            {
                string json = File.ReadAllText(oldTeamFile);
//                 DebugLogger.Log($"[UserStorage] 📖 Loaded legacy team.bit from: {oldTeamFile}");
                var team = JsonConvert.DeserializeObject<TeamInfo>(json);
                if (team == null || string.IsNullOrEmpty(team.JoinCode)) return;

                string joinCode = team.JoinCode.ToUpper();
                string teamFolder = GetTeamFolderPath(joinCode);

                // Move team.bit
                File.Copy(oldTeamFile, Path.Combine(teamFolder, "team.bit"), true);

                // Move users.bit
                string oldUsers = Path.Combine(root, "users.bit");
                if (File.Exists(oldUsers))
                    File.Copy(oldUsers, Path.Combine(teamFolder, "users.bit"), true);

                // Move settings.bit
                string oldSettings = Path.Combine(root, "settings.bit");
                if (File.Exists(oldSettings))
                    File.Copy(oldSettings, Path.Combine(teamFolder, "settings.bit"), true);

                // Move remember.bit
                string oldRemember = Path.Combine(root, "remember.bit");
                if (File.Exists(oldRemember))
                    File.Copy(oldRemember, Path.Combine(teamFolder, "remember.bit"), true);

                // Determine user name from settings.bit (last logged-in user)
                string userName = "";
                if (File.Exists(oldSettings))
                    userName = File.ReadAllText(oldSettings).Trim();
//                         DebugLogger.Log($"[UserStorage] 📖 File read: {oldSettings}");
                if (string.IsNullOrEmpty(userName) && team.Members.Count > 0)
                    userName = team.Members[0];

                // Create the index with this one team
                AddTeamToIndex(joinCode, team.TeamName, userName);

                // Set as active
                SetActiveTeamCode(joinCode);

                // Clean up old root-level files (keep backups just in case)
                try
                {
                    if (File.Exists(oldTeamFile)) File.Move(oldTeamFile, oldTeamFile + ".bak");
                    if (File.Exists(oldUsers)) File.Move(oldUsers, oldUsers + ".bak");
                    if (File.Exists(oldSettings)) File.Move(oldSettings, oldSettings + ".bak");
                    if (File.Exists(oldRemember)) File.Move(oldRemember, oldRemember + ".bak");
                }
                catch { } // Non-critical — old files can stay
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        // TEAM STORAGE (local)
        // ══════════════════════════════════════════════════════════

        /// <summary>Returns true if at least one team has been set up on this machine.</summary>
        public static bool HasTeam()
        {
//             DebugLogger.Log("[UserStorage] >> HasTeam() called");
            // Check new multi-team index first
            var teams = GetJoinedTeams();
            if (teams.Count > 0) return true;

            // Fallback: check legacy single-team file
            string oldTeamFile = Path.Combine(GetFolderPath(), "team.bit");
            return File.Exists(oldTeamFile);
        }

        /// <summary>Returns true if this machine has more than one team joined.</summary>
        public static bool HasMultipleTeams()
        {
//             DebugLogger.Log("[UserStorage] >> HasMultipleTeams() called");
            return GetJoinedTeams().Count > 1;
        }

        /// <summary>Saves the current team info locally (into the active team's subfolder).</summary>
        public static void SaveTeam(TeamInfo team)
        {
//             DebugLogger.Log("[UserStorage] >> SaveTeam() called");
            
            try
            {
                // Ensure this team has a subfolder and is in the index
                if (!string.IsNullOrEmpty(team.JoinCode))
                {
                    string teamFolder = GetTeamFolderPath(team.JoinCode);
                    string json = JsonConvert.SerializeObject(team, Formatting.Indented);
                    File.WriteAllText(Path.Combine(teamFolder, "team.bit"), json);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the current team info from the active team's subfolder.
        /// Reads %AppData%\WorkFlow\teams\{JoinCode}\team.bit which contains the TeamInfo JSON.
        /// Includes automatic deduplication of member names (case-insensitive comparison).
        /// If duplicates are found (e.g., "Blagoy" and "blagoy"), keeps the properly capitalized version
        /// and merges their metadata.
        /// </summary>
        public static TeamInfo LoadTeam()
        {
//             DebugLogger.Log("[UserStorage] >> LoadTeam() called");
            try
            {
                string path = GetTeamFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
//                     DebugLogger.Log($"[UserStorage] 📖 Loaded team.bit from: {path}");
                    var team = JsonConvert.DeserializeObject<TeamInfo>(json);

                    // DEDUP: Remove case-duplicate member names (e.g. "Blagoy" vs "blagoy")
                    // This can happen when members join with different name capitalizations
                    // Logic: keeps first occurrence, prefers capitalized versions, merges metadata
                    if (team != null && team.Members != null)
                    {
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var deduped = new List<string>();
                        foreach (var name in team.Members)
                        {
                            if (!seen.Contains(name))
                            {
                                seen.Add(name);
                                deduped.Add(name);
                            }
                            else
                            {
                                // Prefer capitalized version: if current is uppercase and existing is lowercase, replace
                                int idx = deduped.FindIndex(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                                if (idx >= 0 && char.IsLower(deduped[idx][0]) && char.IsUpper(name[0]))
                                    deduped[idx] = name;

                                // Also merge MembersMeta: copy data from duplicate to the kept name
                                if (team.MembersMeta != null && team.MembersMeta.ContainsKey(name))
                                {
                                    string keptName = deduped.Find(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
                                    if (keptName != null && !team.MembersMeta.ContainsKey(keptName))
                                    {
                                        team.MembersMeta[keptName] = team.MembersMeta[name];
                                    }
                                    team.MembersMeta.Remove(name);
                                }
                            }
                        }

                        if (deduped.Count != team.Members.Count)
                        {
                            team.Members = deduped;
                            // Auto-save cleaned team locally + sync to Firebase
                            try
                            {
                                SaveTeam(team);
                                _ = SaveTeamToFirebaseAsync(team);
                            }
                            catch { }
                        }
                    }

                    return team;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
            return null;
        }

        /// <summary>Loads a specific team by join code (not necessarily the active one).</summary>
        public static TeamInfo LoadTeamByCode(string joinCode)
        {
//             DebugLogger.Log("[UserStorage] >> LoadTeamByCode() called");
            
            try
            {
                string folder = GetTeamFolderPath(joinCode);
                string path = Path.Combine(folder, "team.bit");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
//                         DebugLogger.Log($"[UserStorage] 📖 File read: {path}");
                    return JsonConvert.DeserializeObject<TeamInfo>(json);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
            return null;
        }

        // ══════════════════════════════════════════════════════════
        // TEAM STORAGE (Firebase — so other machines can join)
        // Firebase enables P2P team discovery without a central server.
        // All teams are synced to a public Firebase so joiners can discover them.
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the raw Firebase base URL (default or custom).
        /// If the team has a CustomFirebaseUrl set, returns that (for private Firebase instances).
        /// Otherwise returns the default 8BitLab Firebase.
        /// </summary>
        private static string GetRawFirebaseBase()
        {
//             DebugLogger.Log("[UserStorage] >> GetRawFirebaseBase() called");
            var team = LoadTeam();
            if (team != null && !string.IsNullOrWhiteSpace(team.CustomFirebaseUrl))
            {
//                 DebugLogger.Log($"[UserStorage] Using custom Firebase URL: {team.CustomFirebaseUrl}");
                return team.CustomFirebaseUrl.TrimEnd('/');
            }
//             DebugLogger.Log($"[UserStorage] Using default Firebase URL: {DefaultFirebaseBase}");
            return DefaultFirebaseBase;
        }

        /// <summary>
        /// Saves team info to Firebase under /teams/{joinCode}.json
        /// DUAL-SAVE STRATEGY:
        ///   1. Always saves to the DEFAULT Firebase so new participants can discover the team
        ///   2. Also saves to CustomFirebaseUrl (if set) for private Firebase instances
        /// This allows teams to work on custom Firebase while still being discoverable via default one.
        /// </summary>
        public static async System.Threading.Tasks.Task SaveTeamToFirebaseAsync(TeamInfo team)
        {
//             DebugLogger.Log("[UserStorage] >> SaveTeamToFirebaseAsync() called");
//             DebugLogger.Log($"[UserStorage] Saving team {team.JoinCode} to Firebase");

            try
            {
                // Always save to the DEFAULT Firebase so participants can find the team
                // and discover the CustomFirebaseUrl when they join with the code
                string defaultUrl = $"{DefaultFirebaseBase}/teams/{team.JoinCode}.json";
                string json = JsonConvert.SerializeObject(team);
                var defaultContent = new StringContent(json, Encoding.UTF8, "application/json");
//                 DebugLogger.Log($"[UserStorage] Firebase PUT (default): {defaultUrl}");
                await _http.PutAsync(defaultUrl, defaultContent);
//                 DebugLogger.Log($"[UserStorage] ✓ Team saved to default Firebase");

                // If a custom Firebase URL is set, also save to the custom database
                if (!string.IsNullOrWhiteSpace(team.CustomFirebaseUrl))
                {
                    string customBase = team.CustomFirebaseUrl.TrimEnd('/');
                    string customUrl = $"{customBase}/teams/{team.JoinCode}.json";
                    var customContent = new StringContent(json, Encoding.UTF8, "application/json");
//                     DebugLogger.Log($"[UserStorage] Firebase PUT (custom): {customUrl}");
                    await _http.PutAsync(customUrl, customContent);
//                     DebugLogger.Log($"[UserStorage] ✓ Team also saved to custom Firebase");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception saving to Firebase: {ex.Message}");
            }
        }

        /// <summary>Looks up a team by join code from Firebase. Returns null if not found.</summary>
        public static async Task<TeamInfo> FindTeamByJoinCodeAsync(string joinCode, string customFirebaseUrl = null)
        {
            try
            {
                string baseUrl = !string.IsNullOrWhiteSpace(customFirebaseUrl)
                    ? customFirebaseUrl.TrimEnd('/')
                    : DefaultFirebaseBase;
                string url = $"{baseUrl}/teams/{joinCode.ToUpper()}.json";
                var response = await _http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(json) && json != "null")
                    {
                        return JsonConvert.DeserializeObject<TeamInfo>(json);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
            return null;
        }

        /// <summary>Adds a member to the team on Firebase.</summary>
        public static async System.Threading.Tasks.Task AddMemberToFirebaseTeamAsync(string joinCode, string memberName)
        {
            try
            {
                var team = await FindTeamByJoinCodeAsync(joinCode);
                if (team != null && !team.Members.Contains(memberName))
                {
                    team.Members.Add(memberName);
                    await SaveTeamToFirebaseAsync(team);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
        }

        /// <summary>Removes a member from the team on Firebase.</summary>
        public static async System.Threading.Tasks.Task RemoveMemberFromFirebaseTeamAsync(string joinCode, string memberName)
        {
            try
            {
                var team = await FindTeamByJoinCodeAsync(joinCode);
                if (team != null)
                {
                    team.Members.Remove(memberName);
                    if (team.MembersMeta != null)
                        team.MembersMeta.Remove(memberName);
                    await SaveTeamToFirebaseAsync(team);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        // USER STORAGE (local — passwords stay on machine)
        // Users.bit stores hashed passwords and metadata locally.
        // Passwords NEVER leave the machine - they stay for offline login.
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Save all users for the current team to %AppData%\WorkFlow\teams\{JoinCode}\users.bit
        /// Converts UserInfo objects to UserStorageEntry (with hashed passwords) and saves as JSON.
        /// Deduplicates users first to prevent name collisions in the dictionary.
        /// </summary>
        public static void SaveUsers(List<UserInfo> users)
        {
//             DebugLogger.Log("[UserStorage] >> SaveUsers() called");
//             DebugLogger.Log($"[UserStorage] Saving {users.Count} users to local storage");

            try
            {
                // DEDUP before saving to prevent dictionary key collision
                // (ensures each username appears only once)
                users = DeduplicateUsers(users);
//                 DebugLogger.Log($"[UserStorage] After dedup: {users.Count} users");

                var data = users.ToDictionary(
                    u => u.Name,
                    u => new UserStorageEntry
                    {
                        PasswordHash = u.GetPasswordHash(),
                        IsDefaultPassword = u.IsDefaultPassword,
                        IsAdmin = u.IsAdmin,
                        TeamJoinCode = u.TeamJoinCode,
                        Color = u.Color,
                        Title = u.Title,
                        Role = u.Role,
                        Country = u.Country,
                        WeeklyHourLimit = u.WeeklyHourLimit
                    }
                );
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                string storagePath = GetStorageFilePath();
                File.WriteAllText(storagePath, json);
//                 DebugLogger.Log($"[UserStorage] ✍️ Successfully saved {users.Count} users to: {storagePath}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception saving users: {ex.Message}");
            }
        }

        /// <summary>
        /// Load all users from %AppData%\WorkFlow\teams\{JoinCode}\users.bit
        /// LOADING LOGIC:
        ///   1. Reads users.bit (UserStorageEntry objects with hashed passwords)
        ///   2. Cross-references with TeamInfo.Members to include all current team members
        ///   3. For members not in users.bit, creates new UserInfo with default password
        ///   4. Merges metadata from team.MembersMeta (Color, Title, Role, Country, etc)
        ///   5. Ensures AdminName user always has admin privileges
        /// This handles cases where:
        ///   - New members join the team (exist in team.Members but not in users.bit yet)
        ///   - Members have been removed from team (in users.bit but not in team.Members - skipped)
        ///   - Admin status is controlled by team.AdminName not by stored IsAdmin flag
        /// </summary>
        public static List<UserInfo> LoadUsers()
        {
//             DebugLogger.Log("[UserStorage] >> LoadUsers() called");
            string filePath = GetStorageFilePath();
            TeamInfo team = LoadTeam();
//             DebugLogger.Log($"[UserStorage] Storage file path: {filePath}");

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
//                     DebugLogger.Log($"[UserStorage] 📖 Loaded users.bit from: {filePath}");

                    // Try new format with all fields
                    try
                    {
                        var data = JsonConvert.DeserializeObject<Dictionary<string, UserStorageEntry>>(json);
                        if (data != null)
                        {
//                             DebugLogger.Log($"[UserStorage] Deserialized {data.Count} user entries");
                            var users = new List<UserInfo>();

                            if (team != null)
                            {
                                foreach (var name in team.Members)
                                {
                                    if (data.ContainsKey(name))
                                    {
                                        var entry = data[name];
                                        // Always grant admin if this user matches the team's AdminName
                                        bool isAdmin = entry.IsAdmin || name.Equals(team.AdminName, StringComparison.OrdinalIgnoreCase);
                                        users.Add(new UserInfo(name, entry.PasswordHash, entry.IsDefaultPassword,
                                            isAdmin, entry.TeamJoinCode,
                                            entry.Color, entry.Title, entry.Role,
                                            entry.Country, entry.WeeklyHourLimit));
                                    }
                                    else
                                    {
                                        bool memberIsAdmin = name.Equals(team.AdminName, StringComparison.OrdinalIgnoreCase);
                                        var u = new UserInfo(name, memberIsAdmin, team.JoinCode);
                                        // Apply metadata from team if available
                                        if (team.MembersMeta != null && team.MembersMeta.ContainsKey(name))
                                        {
                                            var meta = team.MembersMeta[name];
                                            u.Color = meta.Color;
                                            u.Title = meta.Title;
                                            u.Role = meta.Role;
                                            if (!string.IsNullOrEmpty(meta.Country))
                                                u.Country = meta.Country;
                                            if (meta.WeeklyHourLimit > 0)
                                                u.WeeklyHourLimit = meta.WeeklyHourLimit;
                                        }
                                        users.Add(u);
                                    }
                                }
                            }
                            else
                            {
                                // No team exists — legacy data. First user becomes admin.
                                bool firstUser = true;
                                foreach (var kvp in data)
                                {
                                    bool isAdmin = kvp.Value.IsAdmin || firstUser;
                                    users.Add(new UserInfo(kvp.Key, kvp.Value.PasswordHash,
                                        kvp.Value.IsDefaultPassword, isAdmin, kvp.Value.TeamJoinCode,
                                        kvp.Value.Color, kvp.Value.Title, kvp.Value.Role,
                                        kvp.Value.Country, kvp.Value.WeeklyHourLimit));
                                    firstUser = false;
                                }
                            }

                            // DEDUP: Remove case-duplicate names (e.g. "Blagoy" vs "blagoy")
                            users = DeduplicateUsers(users);
                            return users;
                        }
                    }
                    catch { }

                    // Fallback: old format (just name → hash string)
                    try
                    {
                        var oldData = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        if (oldData != null)
                        {
                            var users = new List<UserInfo>();
                            bool firstUser = true;
                            foreach (var kvp in oldData)
                            {
                                // Old format has no admin flag — first user becomes admin
                                users.Add(new UserInfo(kvp.Key, kvp.Value, true, firstUser));
                                firstUser = false;
                            }
                            SaveUsers(users);
                            return users;
                        }
                    }
                    catch { }
                }
                catch { }
            }

            // No saved data but we have a team
            if (team != null)
            {
                var users = new List<UserInfo>();
                foreach (var name in team.Members)
                {
                    bool memberIsAdmin = name.Equals(team.AdminName, StringComparison.OrdinalIgnoreCase);
                    var u = new UserInfo(name, memberIsAdmin, team.JoinCode);
                    if (team.MembersMeta != null && team.MembersMeta.ContainsKey(name))
                    {
                        var meta = team.MembersMeta[name];
                        u.Color = meta.Color;
                        u.Title = meta.Title;
                        u.Role = meta.Role;
                        if (!string.IsNullOrEmpty(meta.Country))
                            u.Country = meta.Country;
                        if (meta.WeeklyHourLimit > 0)
                            u.WeeklyHourLimit = meta.WeeklyHourLimit;
                    }
                    users.Add(u);
                }
                users = DeduplicateUsers(users);
                SaveUsers(users);
                return users;
            }

            return new List<UserInfo>();
        }

        /// <summary>DEDUPLICATE USERS — removes case-duplicate entries (e.g. "Blagoy" vs "blagoy").
        /// Keeps the "better" entry: prefers admin, prefers custom password, prefers capitalized name.</summary>
        private static List<UserInfo> DeduplicateUsers(List<UserInfo> users)
        {
//             DebugLogger.Log("[UserStorage] >> DeduplicateUsers() called");
            var seen = new Dictionary<string, UserInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in users)
            {
                if (seen.ContainsKey(u.Name))
                {
                    // MERGE: keep the better entry
                    var existing = seen[u.Name];

                    // Prefer the one with admin status
                    if (!existing.IsAdmin && u.IsAdmin) { seen[u.Name] = u; continue; }

                    // Prefer the one with a custom (non-default) password
                    if (existing.IsDefaultPassword && !u.IsDefaultPassword) { seen[u.Name] = u; continue; }

                    // Prefer the one with a capitalized first letter (proper name)
                    if (char.IsLower(existing.Name[0]) && char.IsUpper(u.Name[0])) { seen[u.Name] = u; continue; }

                    // Otherwise keep existing
                }
                else
                {
                    seen[u.Name] = u;
                }
            }
            return seen.Values.ToList();
        }

        // ══════════════════════════════════════════════════════════
        // LAST USER (for auto-login on returning launch)
        // ══════════════════════════════════════════════════════════

        public static string GetLastUser()
        {
//             DebugLogger.Log("[UserStorage] >> GetLastUser() called");
            try
            {
                string path = GetSettingsFilePath();
                if (File.Exists(path))
                    return File.ReadAllText(path).Trim();
//                         DebugLogger.Log($"[UserStorage] 📖 File read: {path}");
            }
            catch { }
            return null;
        }

        public static void SaveLastUser(string username)
        {
//             DebugLogger.Log("[UserStorage] >> SaveLastUser() called");
            
            try
            {
                File.WriteAllText(GetSettingsFilePath(), username);
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════
        // REMEMBER PASSWORD (stores password hash locally)
        // Now per-team in: %AppData%/WorkFlow/teams/{code}/remember.bit
        // ══════════════════════════════════════════════════════════

        /// <summary>Save the remembered password hash for the given user.
        /// Pass null/empty passwordHash to clear the remembered password.</summary>
        public static void SaveRememberedPassword(string username, string passwordHash)
        {
//             DebugLogger.Log("[UserStorage] >> SaveRememberedPassword() called");
            
            try
            {
                if (string.IsNullOrEmpty(passwordHash))
                {
                    // CLEAR remembered password
                    string path = GetRememberFilePath();
                    if (File.Exists(path))
                        File.Delete(path);
                    return;
                }

                // SAVE as simple JSON: { "user": "Name", "hash": "abc123..." }
                var data = new Dictionary<string, string>
                {
                    { "user", username },
                    { "hash", passwordHash }
                };
                string json = JsonConvert.SerializeObject(data);
                File.WriteAllText(GetRememberFilePath(), json);
            }
            catch { }
        }

        /// <summary>Get the remembered password hash for the given user.
        /// Returns null if no remembered password or if it's for a different user.</summary>
        public static string GetRememberedPassword(string username)
        {
//             DebugLogger.Log("[UserStorage] >> GetRememberedPassword() called");
            
            try
            {
                string path = GetRememberFilePath();
                if (!File.Exists(path)) return null;

                string json = File.ReadAllText(path);
//                     DebugLogger.Log($"[UserStorage] 📖 File read: {path}");
                var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (data != null
                    && data.ContainsKey("user")
                    && data.ContainsKey("hash")
                    && string.Equals(data["user"], username, StringComparison.OrdinalIgnoreCase))
                {
                    return data["hash"];
                }
            }
            catch { }
            return null;
        }

        /// <summary>Check if "Remember Password" is active for the given user.</summary>
        public static bool IsPasswordRemembered(string username)
        {
//             DebugLogger.Log("[UserStorage] >> IsPasswordRemembered() called");
            
            return GetRememberedPassword(username) != null;
        }

        // ══════════════════════════════════════════════════════════
        // FIREBASE BASE URL — namespaced by team
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the Firebase base URL for the current team.
        /// Uses custom URL if set, otherwise default.
        /// </summary>
        public static string GetFirebaseBaseUrl()
        {
//             DebugLogger.Log("[UserStorage] >> GetFirebaseBaseUrl() called");
            var team = LoadTeam();
            string baseUrl = (team != null && !string.IsNullOrWhiteSpace(team.CustomFirebaseUrl))
                ? team.CustomFirebaseUrl.TrimEnd('/')
                : DefaultFirebaseBase;

            if (team != null && !string.IsNullOrEmpty(team.JoinCode))
                return $"{baseUrl}/teams/{team.JoinCode}";
            return baseUrl;
        }

        /// <summary>
        /// Returns the Firebase logs URL for the current team.
        /// </summary>
        public static string GetFirebaseLogsUrl()
        {
//             DebugLogger.Log("[UserStorage] >> GetFirebaseLogsUrl() called");
            return GetFirebaseBaseUrl() + "/logs.json";
        }

        // ══════════════════════════════════════════════════════════
        // MIGRATE FIREBASE DATA — MOVE ALL DATA FROM OLD JOIN CODE TO NEW
        // Used when admin regenerates the join code so data is NOT lost.
        // Copies: logs, stickers, chat, dm, online_status, helper, project_stages
        // ══════════════════════════════════════════════════════════
        public static async Task<bool> MigrateFirebaseDataAsync(string oldJoinCode, string newJoinCode)
        {
            try
            {
                var team = LoadTeam();
                string baseUrl = (team != null && !string.IsNullOrWhiteSpace(team.CustomFirebaseUrl))
                    ? team.CustomFirebaseUrl.TrimEnd('/')
                    : DefaultFirebaseBase;

                string oldPath = $"{baseUrl}/teams/{oldJoinCode}";
                string newPath = $"{baseUrl}/teams/{newJoinCode}";

                // Data nodes to migrate (everything under the team path)
                string[] nodes = { "logs", "stickers", "chat", "dm", "online_status", "helper", "project_stages" };

                foreach (var node in nodes)
                {
                    try
                    {
                        // GET all data from old path
                        var response = await _http.GetAsync($"{oldPath}/{node}.json");
                        if (!response.IsSuccessStatusCode) continue;

                        string json = await response.Content.ReadAsStringAsync();
                        if (string.IsNullOrWhiteSpace(json) || json == "null") continue;

                        // PUT data to new path (preserves all keys and structure)
                        var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                        await _http.PutAsync($"{newPath}/{node}.json", content);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════
        // MERGE TEAMS — PERMANENTLY COMBINE TWO TEAMS INTO ONE
        // Merges all data (users, stickers, logs, chat, etc.) from
        // the other team INTO the current team. Both admins become co-admins.
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Checks if the other team has a pending merge request FOR our team code.
        /// Both teams must press Unite with each other's code for merge to proceed.
        /// </summary>
        public static async Task<bool> CheckMergeRequestAsync(string otherJoinCode, string myJoinCode)
        {
            try
            {
                string baseUrl = DefaultFirebaseBase;
                string url = $"{baseUrl}/teams/{otherJoinCode}/merge_request.json";
                var response = await _http.GetAsync(url);
                if (!response.IsSuccessStatusCode) return false;

                string json = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(json) || json == "null") return false;

                // Check if the other team requested merge with US
                var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                return data != null && data.ContainsKey("targetCode") &&
                       data["targetCode"].Equals(myJoinCode, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        /// <summary>
        /// Posts a merge request on OUR team node so the other team can detect it.
        /// </summary>
        public static async Task SetMergeRequestAsync(string myJoinCode, string targetJoinCode)
        {
            try
            {
                string baseUrl = DefaultFirebaseBase;
                string url = $"{baseUrl}/teams/{myJoinCode}/merge_request.json";
                var data = new { targetCode = targetJoinCode, requestedAt = DateTime.UtcNow.ToString("o") };
                var content = new StringContent(
                    JsonConvert.SerializeObject(data), System.Text.Encoding.UTF8, "application/json");
                await _http.PutAsync(url, content);
            }
            catch { }
        }

        /// <summary>
        /// Clears the merge request from a team node.
        /// </summary>
        public static async Task ClearMergeRequestAsync(string joinCode)
        {
            try
            {
                string baseUrl = DefaultFirebaseBase;
                string url = $"{baseUrl}/teams/{joinCode}/merge_request.json";
                await _http.DeleteAsync(url);
            }
            catch { }
        }

        /// <summary>
        /// Performs the actual merge: copies all data from otherTeam into myTeam.
        /// Merges members, MembersMeta, stickers, logs, chat, dm, helper, project_stages.
        /// The other team's admin becomes AssistantAdmin on the merged team.
        /// Returns the merged TeamInfo, or null on failure.
        /// </summary>
        public static async Task<TeamInfo> MergeTeamsAsync(string myJoinCode, string otherJoinCode)
        {
            try
            {
                var team = LoadTeam();
                string baseUrl = (team != null && !string.IsNullOrWhiteSpace(team.CustomFirebaseUrl))
                    ? team.CustomFirebaseUrl.TrimEnd('/')
                    : DefaultFirebaseBase;

                // Fetch the other team's info
                var otherTeam = await FindTeamByJoinCodeAsync(otherJoinCode);
                if (otherTeam == null) return null;

                // ── MERGE MEMBERS ──
                var mergedMembers = new HashSet<string>(team.Members ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                if (otherTeam.Members != null)
                    foreach (var m in otherTeam.Members)
                        mergedMembers.Add(m);
                team.Members = mergedMembers.ToList();

                // ── MERGE MembersMeta ──
                if (team.MembersMeta == null) team.MembersMeta = new Dictionary<string, MemberMeta>();
                if (otherTeam.MembersMeta != null)
                {
                    foreach (var kvp in otherTeam.MembersMeta)
                    {
                        if (!team.MembersMeta.ContainsKey(kvp.Key))
                            team.MembersMeta[kvp.Key] = kvp.Value;
                    }
                }

                // ── PROMOTE OTHER ADMIN TO ASSISTANT ADMIN ──
                if (team.AssistantAdmins == null) team.AssistantAdmins = new List<string>();
                if (!string.IsNullOrEmpty(otherTeam.AdminName) &&
                    !team.AdminName.Equals(otherTeam.AdminName, StringComparison.OrdinalIgnoreCase) &&
                    !team.AssistantAdmins.Contains(otherTeam.AdminName))
                {
                    team.AssistantAdmins.Add(otherTeam.AdminName);
                }
                // Also carry over the other team's assistant admins
                if (otherTeam.AssistantAdmins != null)
                {
                    foreach (var aa in otherTeam.AssistantAdmins)
                    {
                        if (!team.AssistantAdmins.Contains(aa) &&
                            !team.AdminName.Equals(aa, StringComparison.OrdinalIgnoreCase))
                            team.AssistantAdmins.Add(aa);
                    }
                }

                // ── MIGRATE FIREBASE DATA (stickers, logs, chat, etc.) ──
                string otherPath = $"{baseUrl}/teams/{otherJoinCode}";
                string myPath = $"{baseUrl}/teams/{myJoinCode}";

                string[] mergeNodes = { "logs", "stickers", "chat", "dm", "helper", "project_stages" };

                foreach (var node in mergeNodes)
                {
                    try
                    {
                        // GET data from other team
                        var resp = await _http.GetAsync($"{otherPath}/{node}.json");
                        if (!resp.IsSuccessStatusCode) continue;
                        string json = await resp.Content.ReadAsStringAsync();
                        if (string.IsNullOrWhiteSpace(json) || json == "null") continue;

                        // Deserialize as dictionary to merge key by key (avoid overwriting existing data)
                        var otherData = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (otherData == null || otherData.Count == 0) continue;

                        // PATCH into our team (merges with existing data, does not overwrite)
                        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                        var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"{myPath}/{node}.json")
                        {
                            Content = content
                        };
                        await _http.SendAsync(req);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
                    }
                }

                // ── SAVE MERGED TEAM ──
                SaveTeam(team);
                await SaveTeamToFirebaseAsync(team);

                // ── CLEAR MERGE REQUESTS ON BOTH SIDES ──
                await ClearMergeRequestAsync(myJoinCode);
                await ClearMergeRequestAsync(otherJoinCode);

                return team;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════
        // SAVE TEAM/USERS BY SPECIFIC JOIN CODE (for joining new teams)
        // ══════════════════════════════════════════════════════════

        /// <summary>Save team info for a specific join code (not necessarily the active team).</summary>
        public static void SaveTeamByCode(string joinCode, TeamInfo team)
        {
//             DebugLogger.Log("[UserStorage] >> SaveTeamByCode() called");
            
            try
            {
                string folder = GetTeamFolderPath(joinCode);
                string json = JsonConvert.SerializeObject(team, Formatting.Indented);
                File.WriteAllText(Path.Combine(folder, "team.bit"), json);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ERROR in SaveTeamByCode: {ex.Message}");
            }
        }

        /// <summary>Save user list for a specific join code.</summary>
        public static void SaveUsersByCode(string joinCode, List<UserInfo> users)
        {
               //  DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            try
            {
                users = DeduplicateUsers(users);
                var data = users.ToDictionary(
                    u => u.Name,
                    u => new UserStorageEntry
                    {
                        PasswordHash = u.GetPasswordHash(),
                        IsDefaultPassword = u.IsDefaultPassword,
                        IsAdmin = u.IsAdmin,
                        TeamJoinCode = u.TeamJoinCode,
                        Color = u.Color,
                        Title = u.Title,
                        Role = u.Role,
                        Country = u.Country,
                        WeeklyHourLimit = u.WeeklyHourLimit
                    }
                );
                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                string folder = GetTeamFolderPath(joinCode);
                File.WriteAllText(Path.Combine(folder, "users.bit"), json);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ERROR in SaveUsersByCode: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        // FAVORITES — STORED PER-USER IN ROOT APP FOLDER
        // ══════════════════════════════════════════════════════════

        private static string GetFavoritesFilePath()
        {
           //     DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            return Path.Combine(GetFolderPath(), "favorites.bit");
        }

        /// <summary>Get favorite users for a given user.</summary>
        public static List<string> GetFavoriteUsers(string userName)
        {
//             DebugLogger.Log("[UserStorage] >> GetFavoriteUsers() called");
            try
            {
                string path = GetFavoritesFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
//                         DebugLogger.Log($"[UserStorage] 📖 File read: {path}");
                    var all = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
                    if (all != null && all.ContainsKey(userName))
                        return all[userName];
                }
            }
            catch { }
            return new List<string>();
        }

        /// <summary>Save favorite users for a given user.</summary>
        public static void SaveFavoriteUsers(string userName, List<string> favorites)
        {
//             DebugLogger.Log("[UserStorage] >> SaveFavoriteUsers() called");
            
            try
            {
                string path = GetFavoritesFilePath();
                Dictionary<string, List<string>> all;

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
//                         DebugLogger.Log($"[UserStorage] 📖 File read: {path}");
                    all = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json)
                          ?? new Dictionary<string, List<string>>();
                }
                else
                {
                    all = new Dictionary<string, List<string>>();
                }

                all[userName] = favorites ?? new List<string>();
                string output = JsonConvert.SerializeObject(all, Formatting.Indented);
                File.WriteAllText(path, output);
//                     DebugLogger.Log($"[UserStorage] ✍️ File write: {path}");
//                     DebugLogger.Log($"[UserStorage] ✍️ Writing file: {path}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ERROR in SaveFavoriteUsers: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════
        // CHAT FONT SIZE — USER PREFERENCE (Small / Medium / Big)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the saved chat font size preference for a user.
        /// Returns "Small", "Medium", or "Big". Defaults to "Small" if not set.
        /// </summary>
        public static string GetChatFontSize(string userName)
        {
            //    DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            try
            {
                string path = Path.Combine(GetFolderPath(), "chatfontsize.bit");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
//                         DebugLogger.Log($"[UserStorage] 📖 File read: {path}");
                    var all = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (all != null && all.ContainsKey(userName))
                        return all[userName];
                }
            }
            catch { }
            return "Small";
        }

        /// <summary>
        /// Saves the chat font size preference for a user.
        /// Valid values: "Small", "Medium", "Big".
        /// </summary>
        public static void SaveChatFontSize(string userName, string size)
        {
//             DebugLogger.Log("[UserStorage] >> SaveChatFontSize() called");
            
            try
            {
                string path = Path.Combine(GetFolderPath(), "chatfontsize.bit");
                Dictionary<string, string> all;

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
//                         DebugLogger.Log($"[UserStorage] 📖 File read: {path}");
                    all = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                          ?? new Dictionary<string, string>();
                }
                else
                {
                    all = new Dictionary<string, string>();
                }

                all[userName] = size;
                File.WriteAllText(path, JsonConvert.SerializeObject(all));
//                     DebugLogger.Log($"[UserStorage] ✍️ File write: {path}");
//                     DebugLogger.Log($"[UserStorage] ✍️ Writing file: {path}");
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════
        // DM FONT SIZE — PER-USER PREFERENCE (Small / Medium / Big)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the saved DM (private message) font size preference for a user.
        /// Returns "Small", "Medium", or "Big". Defaults to "Small" if not set.
        /// </summary>
        public static string GetDmFontSize(string userName)
        {
//             DebugLogger.Log("[UserStorage] >> GetDmFontSize() called");
            
            try
            {
                string path = Path.Combine(GetFolderPath(), "dmfontsize.bit");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
//                         DebugLogger.Log($"[UserStorage] 📖 File read: {path}");
                    var all = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (all != null && all.ContainsKey(userName))
                        return all[userName];
                }
            }
            catch { }
            return "Small";
        }

        /// <summary>
        /// Saves the DM (private message) font size preference for a user.
        /// Valid values: "Small", "Medium", "Big".
        /// </summary>
        public static void SaveDmFontSize(string userName, string size)
        {
//             DebugLogger.Log("[UserStorage] >> SaveDmFontSize() called");
            
            try
            {
                string path = Path.Combine(GetFolderPath(), "dmfontsize.bit");
                Dictionary<string, string> all;

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
//                         DebugLogger.Log($"[UserStorage] 📖 File read: {path}");
                    all = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                          ?? new Dictionary<string, string>();
                }
                else
                {
                    all = new Dictionary<string, string>();
                }

                all[userName] = size;
                File.WriteAllText(path, JsonConvert.SerializeObject(all));
//                     DebugLogger.Log($"[UserStorage] ✍️ File write: {path}");
//                     DebugLogger.Log($"[UserStorage] ✍️ Writing file: {path}");
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════════════════
        // AI CHAT FONT SIZE — PER-USER PREFERENCE (Small / Medium / Big)
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the saved AI chat font size preference for a user.
        /// Returns "Small", "Medium", or "Big". Defaults to "Small" if not set.
        /// </summary>
        public static string GetAiChatFontSize(string userName)
        {
            try
            {
                string path = Path.Combine(GetFolderPath(), "aichatfontsize.bit");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var all = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (all != null && all.ContainsKey(userName))
                        return all[userName];
                }
            }
            catch { }
            return "Small";
        }

        /// <summary>
        /// Saves the AI chat font size preference for a user.
        /// Valid values: "Small", "Medium", "Big".
        /// </summary>
        public static void SaveAiChatFontSize(string userName, string size)
        {
            try
            {
                string path = Path.Combine(GetFolderPath(), "aichatfontsize.bit");
                Dictionary<string, string> all;

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    all = JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                          ?? new Dictionary<string, string>();
                }
                else
                {
                    all = new Dictionary<string, string>();
                }

                all[userName] = size;
                File.WriteAllText(path, JsonConvert.SerializeObject(all));
            }
            catch { }
        }

        // ══════════════════════════════════════════════════════════
        // GLOBAL DM — FIREBASE PATH FOR CROSS-PROJECT MESSAGING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Returns the default (shared) Firebase base URL — NOT team-specific.
        /// Used for cross-project features like DMs and favorites.
        /// </summary>
        public static string GetGlobalFirebaseUrl()
        {
//             DebugLogger.Log("[UserStorage] >> GetGlobalFirebaseUrl() called");
            string baseUrl = "https://csharptimelogger-default-rtdb.europe-west1.firebasedatabase.app";
            return baseUrl;
        }

        /// <summary>
        /// Returns Firebase URL for cross-project direct messages.
        /// Path: /global_dm/{conversationId}.json
        /// </summary>
        public static string GetGlobalDmUrl(string conversationId)
        {
//             DebugLogger.Log("[UserStorage] >> GetGlobalDmUrl() called");
            
            return GetGlobalFirebaseUrl() + "/global_dm/" + conversationId + ".json";
        }

        /// <summary>
        /// Returns Firebase base URL for a specific team (by join code).
        /// Used for sending heartbeats to all joined teams.
        /// </summary>
        public static string GetFirebaseBaseUrlForTeam(string joinCode)
        {
//             DebugLogger.Log("[UserStorage] >> GetFirebaseBaseUrlForTeam() called");
            
            string baseUrl = "https://csharptimelogger-default-rtdb.europe-west1.firebasedatabase.app";
            var team = LoadTeamByCode(joinCode);
            if (team != null && !string.IsNullOrEmpty(team.CustomFirebaseUrl))
                baseUrl = team.CustomFirebaseUrl.TrimEnd('/');
            return $"{baseUrl}/teams/{joinCode}";
        }

        // ══════════════════════════════════════════════════════════
        // GLOBAL ACCOUNTS — FIREBASE-BASED USER ACCOUNTS
        // Stored at: /accounts/{username_key}/
        //   passwordHash  — SHA256 Base64 hash
        //   displayName   — original casing of the username
        //   teams         — list of join codes the user belongs to
        //   createdAt     — ISO 8601 UTC timestamp
        //
        // This allows users to log in from ANY device with name+password
        // and automatically sync/rejoin all their teams.
        // ══════════════════════════════════════════════════════════

        /// <summary>Firebase account data model</summary>
        public class FirebaseAccount
        {
            public string PasswordHash { get; set; }
            public string DisplayName { get; set; }
            public List<string> Teams { get; set; } = new List<string>();
            public string CreatedAt { get; set; }
        }

        /// <summary>Returns the Firebase URL for a global account by username.</summary>
        private static string GetAccountUrl(string username)
        {
//             DebugLogger.Log("[UserStorage] >> GetAccountUrl() called");
            
            // Use lowercase key so login is case-insensitive
            string key = username.Trim().ToLower().Replace(" ", "_").Replace(".", "_");
            return $"{DefaultFirebaseBase}/accounts/{key}.json";
        }

        /// <summary>Registers a new global account in Firebase.
        /// Returns true if created, false if username already taken.</summary>
        public static async Task<bool> RegisterAccountAsync(string displayName, string passwordHash)
        {
            try
            {
                // Check if account already exists
                var existing = await GetAccountAsync(displayName);
                if (existing != null)
                    return false; // Username taken

                var account = new FirebaseAccount
                {
                    PasswordHash = passwordHash,
                    DisplayName = displayName.Trim(),
                    Teams = new List<string>(),
                    CreatedAt = DateTime.UtcNow.ToString("o")
                };

                string url = GetAccountUrl(displayName);
                string json = JsonConvert.SerializeObject(account);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PutAsync(url, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
                return false;
            }
        }

        /// <summary>Gets a global account from Firebase. Returns null if not found.</summary>
        public static async Task<FirebaseAccount> GetAccountAsync(string username)
        {
            try
            {
                string url = GetAccountUrl(username);
                var response = await _http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(json) && json != "null")
                        return JsonConvert.DeserializeObject<FirebaseAccount>(json);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
            return null;
        }

        /// <summary>Verifies login credentials against global account in Firebase.
        /// Returns the account if credentials match, null otherwise.</summary>
        public static async Task<FirebaseAccount> LoginAccountAsync(string username, string passwordHash)
        {
            try
            {
                var account = await GetAccountAsync(username);
                if (account != null && account.PasswordHash == passwordHash)
                    return account;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
            return null;
        }

        /// <summary>Adds a team join code to the global account's team list in Firebase.</summary>
        public static async System.Threading.Tasks.Task AddTeamToAccountAsync(string username, string joinCode)
        {
            try
            {
                var account = await GetAccountAsync(username);
                if (account == null) return;

                string code = joinCode.ToUpper();
                if (!account.Teams.Contains(code))
                {
                    account.Teams.Add(code);
                    string url = GetAccountUrl(username);
                    string json = JsonConvert.SerializeObject(account);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    await _http.PutAsync(url, content);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
        }

        /// <summary>Removes a team join code from the global account in Firebase.</summary>
        public static async System.Threading.Tasks.Task RemoveTeamFromAccountAsync(string username, string joinCode)
        {
            try
            {
                var account = await GetAccountAsync(username);
                if (account == null) return;

                account.Teams.Remove(joinCode.ToUpper());
                string url = GetAccountUrl(username);
                string json = JsonConvert.SerializeObject(account);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PutAsync(url, content);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
        }

        /// <summary>Syncs all teams from a global account to this device.
        /// Downloads each team from Firebase, saves locally, and adds to the teams index.
        /// Returns the list of synced teams.</summary>
        public static async Task<List<TeamInfo>> SyncAccountTeamsAsync(string username, string passwordHash)
        {
            var syncedTeams = new List<TeamInfo>();
            try
            {
                var account = await GetAccountAsync(username);
                if (account == null || account.Teams == null) return syncedTeams;

                foreach (string joinCode in account.Teams)
                {
                    var team = await FindTeamByJoinCodeAsync(joinCode);
                    if (team == null) continue;
                    if (team.IsBanned(username)) continue;

                    // Ensure this user is in the team's member list
                    bool nameInTeam = team.Members.Any(m =>
                        m.Equals(username, StringComparison.OrdinalIgnoreCase) ||
                        m.Equals(account.DisplayName, StringComparison.OrdinalIgnoreCase));
                    if (!nameInTeam)
                    {
                        team.Members.Add(account.DisplayName);
                        await SaveTeamToFirebaseAsync(team);
                    }

                    // Save team locally
                    SaveTeamByCode(joinCode, team);

                    // Create local user entries for this team
                    var users = new List<UserInfo>();
                    foreach (var memberName in team.Members)
                    {
                        bool isAdmin = memberName.Equals(team.AdminName, StringComparison.OrdinalIgnoreCase);
                        var user = new UserInfo(memberName, isAdmin, team.JoinCode);

                        // If this is the logging-in user, set their real password
                        if (memberName.Equals(account.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                            memberName.Equals(username, StringComparison.OrdinalIgnoreCase))
                        {
                            // Set the password hash directly via reflection-free approach
                            user.SetPassword("temp");  // dummy
                            // We need to store the real hash — use the constructor for existing users
                            user = new UserInfo(memberName, passwordHash, false, isAdmin, team.JoinCode);
                        }

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
                        users.Add(user);
                    }
                    SaveUsersByCode(joinCode, users);

                    // Add to teams index
                    string displayName = account.DisplayName ?? username;
                    AddTeamToIndex(joinCode, team.TeamName, displayName);

                    syncedTeams.Add(team);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ❌ Exception in method: {ex.Message}");
            }
            return syncedTeams;
        }

        /// <summary>Updates the password hash in the global Firebase account.
        /// Called when user changes their password locally.</summary>
        public static async System.Threading.Tasks.Task UpdateAccountPasswordAsync(string username, string newPasswordHash)
        {
            try
            {
                var account = await GetAccountAsync(username);
                if (account == null) return;

                account.PasswordHash = newPasswordHash;
                string url = GetAccountUrl(username);
                string json = JsonConvert.SerializeObject(account);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PutAsync(url, content);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ERROR in UpdateAccountPasswordAsync: {ex.Message}");
            }
        }

        public static async Task PostWelcomeChatMessageAsync(string joinCode, string userName)
        {
            try
            {
                string baseUrl = GetFirebaseBaseUrlForTeam(joinCode);
                string url = baseUrl + "/chat.json";

                var payload = new
                {
                    user = "WorkFlow",
                    message = $"{userName} joined the team.",
                    timestamp = DateTime.UtcNow.ToString("o")
                };

                string json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _http.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[UserStorage] ERROR in PostWelcomeChatMessageAsync: {ex.Message}");
            }
        }
    }
}
