using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    public static class TeamLocalCacheStore
    {
        private const string CacheCollectionName = "cache_docs";
        private static readonly object _dbLock = new object();

        private class CacheDocument
        {
            [BsonId]
            public string Id { get; set; }
            public string Kind { get; set; } // "dict" / "list"
            public string PayloadJson { get; set; }
            public DateTime UpdatedUtc { get; set; }
        }

        private static string GetActiveTeamFolder()
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WorkFlow");

            if (!Directory.Exists(root))
                Directory.CreateDirectory(root);

            string activePath = Path.Combine(root, "active_team.bit");
            string joinCode = "DEFAULT";

            if (File.Exists(activePath))
            {
                string code = File.ReadAllText(activePath).Trim();
                if (!string.IsNullOrWhiteSpace(code))
                    joinCode = code.ToUpperInvariant();
            }

            string teamFolder = Path.Combine(root, "teams", joinCode);
            if (!Directory.Exists(teamFolder))
                Directory.CreateDirectory(teamFolder);

            return teamFolder;
        }

        private static string GetPath(string fileName)
        {
            return Path.Combine(GetActiveTeamFolder(), fileName);
        }

        private static string GetDbPath()
        {
            return Path.Combine(GetActiveTeamFolder(), "team_local_cache.db");
        }

        private static string BuildDocId(string fileName, string kind)
        {
            string safeName = (fileName ?? "default").Trim().ToLowerInvariant();
            return $"{kind}:{safeName}";
        }

        private static LiteDatabase OpenDb()
        {
            return new LiteDatabase($"Filename={GetDbPath()};Connection=shared");
        }

        private static string TryLoadPayload(string fileName, string kind)
        {
            string docId = BuildDocId(fileName, kind);
            lock (_dbLock)
            {
                using (var db = OpenDb())
                {
                    var col = db.GetCollection<CacheDocument>(CacheCollectionName);
                    col.EnsureIndex(x => x.Id, true);
                    var doc = col.FindById(docId);
                    return doc?.PayloadJson;
                }
            }
        }

        private static void SavePayload(string fileName, string kind, string payloadJson)
        {
            string docId = BuildDocId(fileName, kind);
            lock (_dbLock)
            {
                using (var db = OpenDb())
                {
                    var col = db.GetCollection<CacheDocument>(CacheCollectionName);
                    col.EnsureIndex(x => x.Id, true);
                    col.Upsert(new CacheDocument
                    {
                        Id = docId,
                        Kind = kind,
                        PayloadJson = payloadJson ?? "",
                        UpdatedUtc = DateTime.UtcNow
                    });
                }
            }
        }

        public static Dictionary<string, T> LoadDictionary<T>(string fileName)
        {
            try
            {
                string json = TryLoadPayload(fileName, "dict");
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    // Legacy migration path: read the old JSON file once, then persist to DB.
                    // Use SafeFileWriter for auto-recovery from .bak
                    string path = GetPath(fileName);
                    json = SafeFileWriter.ReadAllText(path);

                    if (string.IsNullOrWhiteSpace(json) || json == "null")
                    {
                        // Final fallback: raw file read
                        if (!File.Exists(path))
                            return new Dictionary<string, T>();
                        json = File.ReadAllText(path);
                        if (string.IsNullOrWhiteSpace(json) || json == "null")
                            return new Dictionary<string, T>();
                    }
                }

                var data = JsonConvert.DeserializeObject<Dictionary<string, T>>(json)
                    ?? new Dictionary<string, T>();
                SaveDictionary(fileName, data); // ensures DB has canonical copy
                return data;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[TeamLocalCache] LoadDictionary ERROR for {fileName}: {ex.Message}");
                return new Dictionary<string, T>();
            }
        }

        public static void SaveDictionary<T>(string fileName, Dictionary<string, T> data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(
                    data ?? new Dictionary<string, T>(),
                    Formatting.Indented);
                SavePayload(fileName, "dict", json);

                // Mirror the LiteDB file to backup location after save
                try { BackupManager.MirrorSingleFile(GetDbPath()); } catch { }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[TeamLocalCache] SaveDictionary CRITICAL ERROR for {fileName}: {ex.Message}");
            }
        }

        public static List<T> LoadList<T>(string fileName)
        {
            try
            {
                string json = TryLoadPayload(fileName, "list");
                if (string.IsNullOrWhiteSpace(json) || json == "null")
                {
                    // Legacy migration path with SafeFileWriter recovery
                    string path = GetPath(fileName);
                    json = SafeFileWriter.ReadAllText(path);

                    if (string.IsNullOrWhiteSpace(json) || json == "null")
                    {
                        if (!File.Exists(path))
                            return new List<T>();
                        json = File.ReadAllText(path);
                        if (string.IsNullOrWhiteSpace(json) || json == "null")
                            return new List<T>();
                    }
                }

                var data = JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
                SaveList(fileName, data); // ensures DB has canonical copy
                return data;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[TeamLocalCache] LoadList ERROR for {fileName}: {ex.Message}");
                return new List<T>();
            }
        }

        public static void SaveList<T>(string fileName, List<T> data)
        {
            try
            {
                string json = JsonConvert.SerializeObject(
                    data ?? new List<T>(),
                    Formatting.Indented);
                SavePayload(fileName, "list", json);

                // Mirror the LiteDB file to backup location after save
                try { BackupManager.MirrorSingleFile(GetDbPath()); } catch { }
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[TeamLocalCache] SaveList CRITICAL ERROR for {fileName}: {ex.Message}");
            }
        }
    }
}
