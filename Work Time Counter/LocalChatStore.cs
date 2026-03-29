// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        LocalChatStore.cs                                            ║
// ║  PURPOSE:     LOCAL-FIRST CHAT STORAGE WITH FIREBASE SYNC                  ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Stores all chat messages in a local JSON file on disk. This ensures:      ║
// ║    - Chat works even if Firebase is down or empty                          ║
// ║    - Messages load instantly from local (no network delay)                 ║
// ║    - Less Firebase bandwidth used (sync only changes)                      ║
// ║                                                                            ║
// ║  SYNC STRATEGY:                                                            ║
// ║    1. On send: save locally FIRST, then push to Firebase                   ║
// ║    2. On refresh: load local, merge with Firebase, save merged result      ║
// ║    3. Messages are identified by FirebaseKey or a local UUID              ║
// ║    4. DMs also stored locally with same pattern                            ║
// ║                                                                            ║
// ║  FILE FORMAT:                                                              ║
// ║    %AppData%\WorkFlow\teams\{joinCode}\chat_local.json                     ║
// ║    %AppData%\WorkFlow\teams\{joinCode}\dm_local.json                       ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    /// <summary>
    /// Local-first chat storage.
    /// All messages are saved to disk immediately. Firebase is used for sync only.
    /// Messages are keyed by FirebaseKey (or a local UUID if not yet synced).
    ///
    /// SAFETY FEATURES (added 2026-03):
    ///   - Uses SafeFileWriter for atomic writes with .bak backup
    ///   - Auto-recovers from corrupted JSON via .bak restore
    ///   - Timestamp-based merge prevents Firebase from overwriting newer local edits
    ///   - Mirrors to BackupManager secondary location after every save
    ///   - Thread-safe via _chatLock
    /// </summary>
    public static class LocalChatStore
    {
        // Keep a very large local history so team chat is effectively permanent on this PC.
        // This still prevents unbounded file growth in extreme long-term usage.
        private const int MaxLocalChatMessages = 50000;

        // Thread safety lock for all chat file operations
        private static readonly object _chatLock = new object();

        // ═══ CHAT MESSAGES — LOCAL FILE ═══

        /// <summary>
        /// Returns the file path for local chat storage for the active team.
        /// Path: %AppData%\WorkFlow\teams\{joinCode}\chat_local.json
        /// </summary>
        private static string GetChatFilePath()
        {
            string teamFolder = GetActiveTeamFolder();
            return Path.Combine(teamFolder, "chat_local.json");
        }

        /// <summary>
        /// Returns the file path for local DM storage.
        /// Path: %AppData%\WorkFlow\dm_local.json (global, not per-team)
        /// </summary>
        private static string GetDmFilePath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WorkFlow");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return Path.Combine(folder, "dm_local.json");
        }

        /// <summary>
        /// Gets the active team's folder from UserStorage pattern.
        /// </summary>
        private static string GetActiveTeamFolder()
        {
            string baseFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WorkFlow");
            if (!Directory.Exists(baseFolder))
                Directory.CreateDirectory(baseFolder);

            // Read active team code
            string activeFile = Path.Combine(baseFolder, "active_team.bit");
            string joinCode = "DEFAULT";
            if (File.Exists(activeFile))
            {
                string code = File.ReadAllText(activeFile).Trim();
                if (!string.IsNullOrEmpty(code))
                    joinCode = code.ToUpper();
            }

            string teamFolder = Path.Combine(baseFolder, "teams", joinCode);
            if (!Directory.Exists(teamFolder))
                Directory.CreateDirectory(teamFolder);
            return teamFolder;
        }

        // ═══ LOAD / SAVE CHAT ═══

        /// <summary>
        /// Loads all locally stored chat messages.
        /// Returns a dictionary keyed by message ID (FirebaseKey or local UUID).
        /// </summary>
        public static Dictionary<string, ChatMessage> LoadLocalChat()
        {
//             DebugLogger.Log("[LocalChat] LoadLocalChat: Loading local chat messages");

            lock (_chatLock)
            {
                try
                {
                    string path = GetChatFilePath();

                    // Use SafeFileWriter — auto-recovers from .bak if main file is corrupted
                    string json = SafeFileWriter.ReadAllText(path);

                    if (string.IsNullOrWhiteSpace(json) || json == "null")
                    {
                        // Fallback: check if raw file exists (for files not yet using SafeFileWriter)
                        if (File.Exists(path))
                        {
                            json = File.ReadAllText(path);
                            if (string.IsNullOrWhiteSpace(json) || json == "null")
                                return new Dictionary<string, ChatMessage>();
                        }
                        else
                        {
                            return new Dictionary<string, ChatMessage>();
                        }
                    }

                    var dict = JsonConvert.DeserializeObject<Dictionary<string, ChatMessage>>(json);
                    if (dict == null)
                    {
                        DebugLogger.Log("[LocalChat] LoadLocalChat: Deserialization returned null — possible corruption");
                        return new Dictionary<string, ChatMessage>();
                    }

                    // Restore FirebaseKey from dictionary key
                    foreach (var kvp in dict)
                        kvp.Value.FirebaseKey = kvp.Key;

//                     DebugLogger.Log($"[LocalChat] LoadLocalChat: Loaded {dict.Count} messages");
                    return dict;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[LocalChat] LoadLocalChat: Error loading chat - {ex.Message}");
                    return new Dictionary<string, ChatMessage>();
                }
            }
        }

        /// <summary>
        /// Saves the full chat dictionary to local storage.
        /// Keeps a very large rolling history to avoid losing chat while still bounding file growth.
        /// </summary>
        public static void SaveLocalChat(Dictionary<string, ChatMessage> messages)
        {
            if (messages == null)
                messages = new Dictionary<string, ChatMessage>();

//             DebugLogger.Log($"[LocalChat] SaveLocalChat: Saving {messages.Count} messages");

            lock (_chatLock)
            {
                try
                {
                    // Keep only the newest N messages by timestamp when the cache gets very large.
                    var trimmed = messages
                        .OrderBy(kvp => kvp.Value.timestamp ?? "")
                        .Skip(Math.Max(0, messages.Count - MaxLocalChatMessages))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    if (messages.Count > MaxLocalChatMessages)
                        DebugLogger.Log($"[LocalChat] SaveLocalChat: Trimmed from {messages.Count} to {trimmed.Count} messages");

                    string json = JsonConvert.SerializeObject(trimmed, Formatting.Indented);
                    string path = GetChatFilePath();

                    // Use SafeFileWriter: writes to .tmp first, rotates .bak, then renames
                    bool success = SafeFileWriter.WriteAllText(path, json);

                    if (success)
                    {
                        // Mirror to backup location
                        BackupManager.MirrorSingleFile(path);
//                         DebugLogger.Log("[LocalChat] SaveLocalChat: Chat file saved + mirrored");
                    }
                    else
                    {
                        // SafeFileWriter failed — try direct write as last resort
                        DebugLogger.Log("[LocalChat] SaveLocalChat: SafeFileWriter failed, trying direct write");
                        File.WriteAllText(path, json);
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[LocalChat] SaveLocalChat: CRITICAL ERROR saving chat - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Adds a single message to local storage.
        /// If the message has no FirebaseKey yet, assigns a local UUID.
        /// Returns the key used.
        /// </summary>
        public static string AddMessage(ChatMessage msg)
        {
//             DebugLogger.Log("[LocalChat] AddMessage: Adding message to local storage");

            var dict = LoadLocalChat();

            string key = msg.FirebaseKey;
            if (string.IsNullOrEmpty(key))
            {
                key = "local_" + Guid.NewGuid().ToString("N").Substring(0, 12);
                msg.FirebaseKey = key;
//                 DebugLogger.Log($"[LocalChat] AddMessage: Generated local key: {key}");
            }
            else
            {
//                 DebugLogger.Log($"[LocalChat] AddMessage: Using existing Firebase key: {key}");
            }

            dict[key] = msg;
            SaveLocalChat(dict);

//             DebugLogger.Log($"[LocalChat] AddMessage: Message saved with key {key}");
            return key;
        }

        /// <summary>
        /// Removes a message from local storage by its key.
        /// </summary>
        public static void RemoveMessage(string key)
        {
//             DebugLogger.Log($"[LocalChat] RemoveMessage: Removing message with key {key}");

            if (string.IsNullOrEmpty(key))
            {
//                 DebugLogger.Log("[LocalChat] RemoveMessage: Key is null or empty, skipping");
                return;
            }

            var dict = LoadLocalChat();
            if (dict.Remove(key))
            {
//                 DebugLogger.Log($"[LocalChat] RemoveMessage: Message removed, saving to disk");
                SaveLocalChat(dict);
            }
            else
            {
//                 DebugLogger.Log($"[LocalChat] RemoveMessage: Key not found in dictionary");
            }
        }

        /// <summary>
        /// Updates the message text for an existing message in local storage.
        /// Used by edit feature to update locally without full refresh.
        /// </summary>
        public static void UpdateMessage(string key, string newMessageText)
        {
//             DebugLogger.Log($"[LocalChat] UpdateMessage: Updating message with key {key}");

            if (string.IsNullOrEmpty(key))
            {
//                 DebugLogger.Log("[LocalChat] UpdateMessage: Key is null or empty, skipping");
                return;
            }

            var dict = LoadLocalChat();
            if (dict.ContainsKey(key))
            {
//                 DebugLogger.Log($"[LocalChat] UpdateMessage: Found message, updating text");
                dict[key].message = newMessageText;
                SaveLocalChat(dict);
            }
            else
            {
//                 DebugLogger.Log($"[LocalChat] UpdateMessage: Key not found in dictionary");
            }
        }

        /// <summary>
        /// Merges Firebase data with local data while preserving local history.
        /// Local cache is the durable archive on this PC; Firebase updates overwrite matching keys.
        /// Returns the merged dictionary.
        /// </summary>
        public static Dictionary<string, ChatMessage> MergeWithFirebase(
            Dictionary<string, ChatMessage> firebaseData)
        {
//             DebugLogger.Log("[LocalChat] MergeWithFirebase: Starting merge operation");

            var local = LoadLocalChat();

            if (firebaseData == null || firebaseData.Count == 0)
            {
                // Firebase is empty — return local data as-is (NEVER wipe local)
//                 DebugLogger.Log("[LocalChat] MergeWithFirebase: Firebase data is empty, returning local data");
                return local;
            }

//             DebugLogger.Log($"[LocalChat] MergeWithFirebase: Firebase has {firebaseData.Count} messages, local has {local.Count}");

            // Start from local archive to guarantee no local chat loss.
            var merged = new Dictionary<string, ChatMessage>(local);

            // TIMESTAMP-BASED MERGE: only accept Firebase version if it's newer than local.
            // This prevents stale Firebase copies from overwriting local edits.
            foreach (var kvp in firebaseData)
            {
                kvp.Value.FirebaseKey = kvp.Key;

                if (!merged.ContainsKey(kvp.Key))
                {
                    // New message from Firebase — always accept
                    merged[kvp.Key] = kvp.Value;
                }
                else
                {
                    // Key exists locally — compare timestamps, keep the newer one
                    var localMsg = merged[kvp.Key];
                    var firebaseMsg = kvp.Value;

                    DateTime localTime = DateTime.MinValue;
                    DateTime firebaseTime = DateTime.MinValue;
                    DateTime.TryParse(localMsg.timestamp, out localTime);
                    DateTime.TryParse(firebaseMsg.timestamp, out firebaseTime);

                    // If Firebase version has same or newer timestamp, accept it.
                    // If local is newer (edited locally while offline), keep local.
                    if (firebaseTime >= localTime)
                    {
                        // Also check if message text differs (edited message scenario)
                        if (localMsg.edited && !firebaseMsg.edited && localMsg.message != firebaseMsg.message)
                        {
                            // Local was edited but Firebase has unedited version — keep local
                            DebugLogger.Log($"[LocalChat] MergeWithFirebase: Keeping local edited message for key {kvp.Key}");
                        }
                        else
                        {
                            merged[kvp.Key] = kvp.Value;
                        }
                    }
                    // else: local is newer, keep local version
                }
            }

            // Save merged result back to local
            SaveLocalChat(merged);

//             DebugLogger.Log($"[LocalChat] MergeWithFirebase: Merge complete - {merged.Count} total");

            return merged;
        }

        // ═══ DIRECT MESSAGES — LOCAL STORAGE ═══

        /// <summary>
        /// Loads all locally stored DMs for a conversation.
        /// conversationKey: alphabetically sorted "Alice_Bob" format.
        /// </summary>
        public static Dictionary<string, DirectMessage> LoadLocalDMs(string conversationKey)
        {
//             DebugLogger.Log($"[LocalChat] LoadLocalDMs: Loading DMs for conversation '{conversationKey}'");

            try
            {
                string path = GetDmFilePath();
                if (!File.Exists(path))
                {
//                     DebugLogger.Log("[LocalChat] LoadLocalDMs: DM file does not exist");
                    return new Dictionary<string, DirectMessage>();
                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
//                     DebugLogger.Log("[LocalChat] LoadLocalDMs: DM file is empty or null");
                    return new Dictionary<string, DirectMessage>();
                }

                var all = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, DirectMessage>>>(json);
                if (all != null && all.ContainsKey(conversationKey))
                {
//                     DebugLogger.Log($"[LocalChat] LoadLocalDMs: Loaded {all[conversationKey].Count} DMs");
                    return all[conversationKey];
                }

//                 DebugLogger.Log($"[LocalChat] LoadLocalDMs: Conversation key not found");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LocalChat] LoadLocalDMs: Error loading DMs - {ex.Message}");
            }
            return new Dictionary<string, DirectMessage>();
        }

        /// <summary>
        /// Saves DMs for a conversation to local storage.
        /// </summary>
        public static void SaveLocalDMs(string conversationKey, Dictionary<string, DirectMessage> messages)
        {
//             DebugLogger.Log($"[LocalChat] SaveLocalDMs: Saving {messages.Count} DMs for '{conversationKey}'");

            try
            {
                string path = GetDmFilePath();
                Dictionary<string, Dictionary<string, DirectMessage>> all;

                if (File.Exists(path))
                {
                    string existing = File.ReadAllText(path);
                    all = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, DirectMessage>>>(existing)
                          ?? new Dictionary<string, Dictionary<string, DirectMessage>>();
                }
                else
                {
                    all = new Dictionary<string, Dictionary<string, DirectMessage>>();
                }

                // Keep only last 100 messages per conversation
                var trimmed = messages
                    .OrderBy(kvp => kvp.Value.timestamp ?? "")
                    .Skip(Math.Max(0, messages.Count - 100))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

//                 DebugLogger.Log($"[LocalChat] SaveLocalDMs: Trimmed to {trimmed.Count} messages (kept last 100)");

                all[conversationKey] = trimmed;
                string json = JsonConvert.SerializeObject(all, Formatting.Indented);

                // Use SafeFileWriter for atomic write with .bak backup
                if (!SafeFileWriter.WriteAllText(path, json))
                {
                    // Fallback to direct write
                    File.WriteAllText(path, json);
                }
                BackupManager.MirrorSingleFile(path);

//                 DebugLogger.Log("[LocalChat] SaveLocalDMs: DM file saved successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[LocalChat] SaveLocalDMs: Error saving DMs - {ex.Message}");
            }
        }
    }
}
