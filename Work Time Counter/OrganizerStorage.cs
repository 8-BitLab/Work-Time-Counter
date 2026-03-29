// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        OrganizerStorage.cs                                          ║
// ║  PURPOSE:     LOCAL PERSISTENCE FOR CALENDAR / ORGANIZER DATA              ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Handles save/load of organizer entries and settings to local JSON files.  ║
// ║  Data is stored under %AppData%\WorkFlow\organizer\                        ║
// ║  Works fully offline — no network required.                                ║
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
    // ═══════════════════════════════════════════════════════════════════════════
    //  ORGANIZER SETTINGS — user preferences for the calendar module
    // ═══════════════════════════════════════════════════════════════════════════
    public class OrganizerSettings
    {
        /// <summary>Show or hide the calendar panel</summary>
        public bool ShowCalendar { get; set; } = true;

        /// <summary>Enable sound alarms</summary>
        public bool SoundAlarmEnabled { get; set; } = true;

        /// <summary>Show popup reminders on app startup</summary>
        public bool PopupOnStartup { get; set; } = true;

        /// <summary>First day of week: 0=Sunday, 1=Monday</summary>
        public int FirstDayOfWeek { get; set; } = 1; // Monday default

        /// <summary>Show ISO week numbers in calendar</summary>
        public bool ShowWeekNumbers { get; set; } = false;

        /// <summary>Compact calendar view (fewer rows)</summary>
        public bool CompactView { get; set; } = false;

        /// <summary>Default snooze duration in minutes</summary>
        public int DefaultSnoozeMins { get; set; } = 10;

        /// <summary>Default popup window width</summary>
        public int PopupWidth { get; set; } = 460;

        /// <summary>Default popup window height</summary>
        public int PopupHeight { get; set; } = 820;

        /// <summary>Show or hide weather gadget in right column.</summary>
        public bool ShowWeatherWidget { get; set; } = true;

        /// <summary>Weather mode cycle: daily / weekly / monthly.</summary>
        public string WeatherWidgetMode { get; set; } = "daily";

        /// <summary>Show or hide floating personal sticker board.</summary>
        public bool ShowPersonalBoard { get; set; } = false;

        /// <summary>Remembered side for floating personal board (right/left).</summary>
        public string PersonalBoardSide { get; set; } = "right";

        /// <summary>Master switch for Ask AI widget and API usage.</summary>
        public bool ShowAiWidget { get; set; } = false;

        /// <summary>Show or hide floating AI chat window.</summary>
        public bool ShowAiChatPanel { get; set; } = false;

        /// <summary>Remembered side for floating AI chat window (right/left).</summary>
        public string AiChatPanelSide { get; set; } = "right";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ORGANIZER STORAGE — static helper for save/load operations
    // ═══════════════════════════════════════════════════════════════════════════
    public static class OrganizerStorage
    {
        private static readonly object _lock = new object();

        // ── FOLDER & FILE PATHS ──

        private static string GetOrganizerFolder()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "WorkFlow", "organizer");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        private static string GetDatabasePath()
            => Path.Combine(GetOrganizerFolder(), "organizer_data.json");

        private static string GetSettingsPath()
            => Path.Combine(GetOrganizerFolder(), "organizer_settings.json");

        // ═══════════════════════════════════════════════════════════════════════
        //  DATABASE — LOAD / SAVE
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Load the full organizer database from disk.
        /// Uses SafeFileWriter for auto-recovery from corrupted JSON via .bak file.</summary>
        public static OrganizerDatabase LoadDatabase()
        {
//             DebugLogger.Log("[OrganizerStorage] LoadDatabase: Loading organizer database from disk");

            lock (_lock)
            {
                try
                {
                    string path = GetDatabasePath();

                    // Use SafeFileWriter — auto-recovers from .bak if main is corrupted
                    string json = SafeFileWriter.ReadAllText(path);

                    if (string.IsNullOrWhiteSpace(json) || json == "null")
                    {
                        // Fallback: check raw file (for first run before SafeFileWriter was added)
                        if (File.Exists(path))
                        {
                            json = File.ReadAllText(path);
                            if (string.IsNullOrWhiteSpace(json) || json == "null")
                                return new OrganizerDatabase();
                        }
                        else
                        {
                            return new OrganizerDatabase();
                        }
                    }

                    var db = JsonConvert.DeserializeObject<OrganizerDatabase>(json);

                    if (db == null)
                    {
                        DebugLogger.Log("[OrganizerStorage] LoadDatabase: Deserialization returned null — possible corruption");
                        return new OrganizerDatabase();
                    }

//                     DebugLogger.Log($"[OrganizerStorage] LoadDatabase: Loaded {db.Entries.Count} entries and {db.DailyNotes.Count} daily notes");
                    return db;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[OrganizerStorage] LoadDatabase: Error loading database - {ex.Message}");
                    return new OrganizerDatabase();
                }
            }
        }

        /// <summary>Save the full organizer database to disk.
        /// Uses SafeFileWriter for atomic writes with .bak backup.
        /// Mirrors to BackupManager secondary location.</summary>
        public static void SaveDatabase(OrganizerDatabase db)
        {
//             DebugLogger.Log($"[OrganizerStorage] SaveDatabase: Saving database with {db.Entries.Count} entries");

            lock (_lock)
            {
                try
                {
                    string path = GetDatabasePath();
                    string json = JsonConvert.SerializeObject(db, Formatting.Indented);

                    // Use SafeFileWriter: .tmp → .bak → rename (atomic)
                    bool success = SafeFileWriter.WriteAllText(path, json);

                    if (success)
                    {
                        // Mirror to backup location
                        BackupManager.MirrorSingleFile(path);
//                         DebugLogger.Log("[OrganizerStorage] SaveDatabase: Database saved + mirrored");
                    }
                    else
                    {
                        // Fallback to direct write
                        DebugLogger.Log("[OrganizerStorage] SaveDatabase: SafeFileWriter failed, trying direct write");
                        File.WriteAllText(path, json);
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[OrganizerStorage] SaveDatabase: CRITICAL ERROR - {ex.Message}");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  ENTRY CRUD HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Get all entries for a specific date (yyyy-MM-dd).</summary>
        public static List<OrganizerEntry> GetEntriesForDate(string dateKey)
        {
//             DebugLogger.Log($"[OrganizerStorage] GetEntriesForDate: Retrieving entries for {dateKey}");

            var db = LoadDatabase();
            var entries = db.Entries.Where(e => e.Date == dateKey).OrderBy(e => e.TimeFrom).ToList();

//             DebugLogger.Log($"[OrganizerStorage] GetEntriesForDate: Found {entries.Count} entries for {dateKey}");
            return entries;
        }

        /// <summary>Get all dates that have at least one entry.</summary>
        public static HashSet<string> GetDatesWithEntries()
        {
//             DebugLogger.Log("[OrganizerStorage] GetDatesWithEntries: Retrieving all dates with entries");

            var db = LoadDatabase();
            var dates = new HashSet<string>(db.Entries.Select(e => e.Date));

//             DebugLogger.Log($"[OrganizerStorage] GetDatesWithEntries: Found {dates.Count} dates with entries");
            return dates;
        }

        /// <summary>Add or update an entry. If Id exists, replaces it.</summary>
        public static void SaveEntry(OrganizerEntry entry)
        {
//             DebugLogger.Log($"[OrganizerStorage] SaveEntry: Saving entry {entry.Id} for {entry.Date} - {entry.Title}");

            var db = LoadDatabase();
            entry.ModifiedAt = DateTime.UtcNow.ToString("o");
            int idx = db.Entries.FindIndex(e => e.Id == entry.Id);
            if (idx >= 0)
            {
//                 DebugLogger.Log($"[OrganizerStorage] SaveEntry: Updating existing entry at index {idx}");
                db.Entries[idx] = entry;
            }
            else
            {
//                 DebugLogger.Log("[OrganizerStorage] SaveEntry: Adding new entry");
                db.Entries.Add(entry);
            }
            SaveDatabase(db);
        }

        /// <summary>Delete an entry by Id.</summary>
        public static void DeleteEntry(string entryId)
        {
//             DebugLogger.Log($"[OrganizerStorage] DeleteEntry: Deleting entry {entryId}");

            var db = LoadDatabase();
            int removed = db.Entries.RemoveAll(e => e.Id == entryId);

//             DebugLogger.Log($"[OrganizerStorage] DeleteEntry: Removed {removed} entry/entries");
            SaveDatabase(db);
        }

        /// <summary>Get entry count for a specific date.</summary>
        public static int GetEntryCount(string dateKey)
        {
//             DebugLogger.Log($"[OrganizerStorage] GetEntryCount: Counting entries for {dateKey}");

            var db = LoadDatabase();
            int count = db.Entries.Count(e => e.Date == dateKey);

//             DebugLogger.Log($"[OrganizerStorage] GetEntryCount: {count} entries on {dateKey}");
            return count;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  DAILY NOTES HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Get daily notes for a specific date.</summary>
        public static string GetDailyNotes(string dateKey)
        {
//             DebugLogger.Log($"[OrganizerStorage] GetDailyNotes: Retrieving notes for {dateKey}");

            var db = LoadDatabase();
            var notes = db.DailyNotes.FirstOrDefault(n => n.Date == dateKey);

            if (notes != null)
            {
//                 DebugLogger.Log($"[OrganizerStorage] GetDailyNotes: Found notes, length: {notes.Notes.Length}");
                return notes.Notes;
            }

//             DebugLogger.Log("[OrganizerStorage] GetDailyNotes: No notes found");
            return "";
        }

        /// <summary>Save daily notes for a specific date.</summary>
        public static void SaveDailyNotes(string dateKey, string text)
        {
//             DebugLogger.Log($"[OrganizerStorage] SaveDailyNotes: Saving notes for {dateKey}, length: {text.Length}");

            var db = LoadDatabase();
            var existing = db.DailyNotes.FirstOrDefault(n => n.Date == dateKey);
            if (existing != null)
            {
//                 DebugLogger.Log("[OrganizerStorage] SaveDailyNotes: Updating existing notes");
                existing.Notes = text;
                existing.ModifiedAt = DateTime.UtcNow.ToString("o");
            }
            else
            {
//                 DebugLogger.Log("[OrganizerStorage] SaveDailyNotes: Creating new notes entry");
                db.DailyNotes.Add(new DailyNotes
                {
                    Date = dateKey,
                    Notes = text,
                    ModifiedAt = DateTime.UtcNow.ToString("o")
                });
            }
            SaveDatabase(db);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  ALARM HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Get all entries with pending alarms (not yet fired, alarm time <= now).</summary>
        public static List<OrganizerEntry> GetPendingAlarms()
        {
//             DebugLogger.Log("[OrganizerStorage] GetPendingAlarms: Checking for pending alarms");

            var db = LoadDatabase();
            var now = DateTime.Now;
            var pending = db.Entries.Where(e =>
                e.AlarmEnabled &&
                !e.AlarmFired &&
                !string.IsNullOrEmpty(e.AlarmDateTime) &&
                DateTime.TryParse(e.AlarmDateTime, out DateTime alarmTime) &&
                alarmTime <= now &&
                (string.IsNullOrEmpty(e.SnoozedUntil) ||
                 (DateTime.TryParse(e.SnoozedUntil, out DateTime snoozeTime) && snoozeTime <= now))
            ).ToList();

//             DebugLogger.Log($"[OrganizerStorage] GetPendingAlarms: Found {pending.Count} pending alarms");
            return pending;
        }

        /// <summary>Mark an alarm as fired.</summary>
        public static void MarkAlarmFired(string entryId)
        {
//             DebugLogger.Log($"[OrganizerStorage] MarkAlarmFired: Marking alarm fired for entry {entryId}");

            var db = LoadDatabase();
            var entry = db.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null)
            {
//                 DebugLogger.Log("[OrganizerStorage] MarkAlarmFired: Entry found, setting AlarmFired=true");
                entry.AlarmFired = true;
                entry.ModifiedAt = DateTime.UtcNow.ToString("o");
                SaveDatabase(db);
            }
            else
            {
//                 DebugLogger.Log("[OrganizerStorage] MarkAlarmFired: Entry not found");
            }
        }

        /// <summary>Snooze an alarm for the given number of minutes.</summary>
        public static void SnoozeAlarm(string entryId, int minutes)
        {
//             DebugLogger.Log($"[OrganizerStorage] SnoozeAlarm: Snoozing entry {entryId} for {minutes} minutes");

            var db = LoadDatabase();
            var entry = db.Entries.FirstOrDefault(e => e.Id == entryId);
            if (entry != null)
            {
//                 DebugLogger.Log("[OrganizerStorage] SnoozeAlarm: Entry found, setting snooze time");
                entry.SnoozedUntil = DateTime.Now.AddMinutes(minutes).ToString("o");
                entry.AlarmFired = false; // Reset so it fires again after snooze
                entry.ModifiedAt = DateTime.UtcNow.ToString("o");
                SaveDatabase(db);
            }
            else
            {
//                 DebugLogger.Log("[OrganizerStorage] SnoozeAlarm: Entry not found");
            }
        }

        /// <summary>Get upcoming alarms (within the next N minutes).</summary>
        public static List<OrganizerEntry> GetUpcomingAlarms(int withinMinutes = 60)
        {
//             DebugLogger.Log($"[OrganizerStorage] GetUpcomingAlarms: Checking for alarms within {withinMinutes} minutes");

            var db = LoadDatabase();
            var now = DateTime.Now;
            var cutoff = now.AddMinutes(withinMinutes);
            var upcoming = db.Entries.Where(e =>
                e.AlarmEnabled &&
                !e.AlarmFired &&
                !string.IsNullOrEmpty(e.AlarmDateTime) &&
                DateTime.TryParse(e.AlarmDateTime, out DateTime alarmTime) &&
                alarmTime > now && alarmTime <= cutoff
            ).OrderBy(e => e.AlarmDateTime).ToList();

//             DebugLogger.Log($"[OrganizerStorage] GetUpcomingAlarms: Found {upcoming.Count} upcoming alarms");
            return upcoming;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SETTINGS — LOAD / SAVE
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Load organizer settings from disk.</summary>
        public static OrganizerSettings LoadSettings()
        {
//             DebugLogger.Log("[OrganizerStorage] LoadSettings: Loading settings from disk");

            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path))
                {
//                     DebugLogger.Log("[OrganizerStorage] LoadSettings: Settings file does not exist, returning defaults");
                    return new OrganizerSettings();
                }

                string json = File.ReadAllText(path);
                var settings = JsonConvert.DeserializeObject<OrganizerSettings>(json);

                if (settings == null)
                {
//                     DebugLogger.Log("[OrganizerStorage] LoadSettings: Deserialization returned null");
                    return new OrganizerSettings();
                }

//                 DebugLogger.Log("[OrganizerStorage] LoadSettings: Settings loaded successfully");
                return settings;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[OrganizerStorage] LoadSettings: Error loading settings - {ex.Message}");
                return new OrganizerSettings();
            }
        }

        /// <summary>Save organizer settings to disk. Uses SafeFileWriter for safety.</summary>
        public static void SaveSettings(OrganizerSettings settings)
        {
//             DebugLogger.Log("[OrganizerStorage] SaveSettings: Saving settings to disk");

            try
            {
                string path = GetSettingsPath();
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);

                if (!SafeFileWriter.WriteAllText(path, json))
                    File.WriteAllText(path, json); // Fallback

                BackupManager.MirrorSingleFile(path);
//                 DebugLogger.Log("[OrganizerStorage] SaveSettings: Settings saved successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[OrganizerStorage] SaveSettings: Error saving settings - {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  SEARCH / FILTER
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Search entries by keyword across title and description.</summary>
        public static List<OrganizerEntry> SearchEntries(string keyword, string dateKey = null)
        {
//             DebugLogger.Log($"[OrganizerStorage] SearchEntries: Searching for '{keyword}' (dateKey: {dateKey})");

            var db = LoadDatabase();
            var query = db.Entries.AsEnumerable();

            if (!string.IsNullOrEmpty(dateKey))
                query = query.Where(e => e.Date == dateKey);

            if (!string.IsNullOrEmpty(keyword))
            {
                string lower = keyword.ToLowerInvariant();
                query = query.Where(e =>
                    (e.Title ?? "").ToLowerInvariant().Contains(lower) ||
                    (e.Description ?? "").ToLowerInvariant().Contains(lower));
            }

            var results = query.OrderBy(e => e.Date).ThenBy(e => e.TimeFrom).ToList();
//             DebugLogger.Log($"[OrganizerStorage] SearchEntries: Found {results.Count} matching entries");

            return results;
        }

        /// <summary>Get today's summary: total entries, completed, pending alarms.</summary>
        public static (int total, int completed, int pendingAlarms) GetTodaySummary()
        {
//             DebugLogger.Log("[OrganizerStorage] GetTodaySummary: Retrieving today's summary");

            string today = DateTime.Today.ToString("yyyy-MM-dd");
            var db = LoadDatabase();
            var todayEntries = db.Entries.Where(e => e.Date == today).ToList();
            int total = todayEntries.Count;
            int completed = todayEntries.Count(e => e.IsCompleted || e.Status == OrganizerStatus.Done);
            int pendingAlarms = todayEntries.Count(e => e.AlarmEnabled && !e.AlarmFired);

//             DebugLogger.Log($"[OrganizerStorage] GetTodaySummary: Total={total}, Completed={completed}, PendingAlarms={pendingAlarms}");

            return (total, completed, pendingAlarms);
        }
    }
}
