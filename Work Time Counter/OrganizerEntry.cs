// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        OrganizerEntry.cs                                            ║
// ║  PURPOSE:     DATA MODEL FOR CALENDAR / DAILY ORGANIZER ENTRIES            ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Defines the data structures used by the built-in Calendar & Daily         ║
// ║  Organizer module. Each entry can be a meeting, interview, call, task,     ║
// ║  reminder, or personal note — with optional alarm and recurrence.          ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;

namespace Work_Time_Counter
{
    // ═══════════════════════════════════════════════════════════════════════════
    //  ENTRY CATEGORY — determines icon / color tag in the UI
    // ═══════════════════════════════════════════════════════════════════════════
    public enum OrganizerCategory
    {
        Meeting,
        Interview,
        Call,
        Task,
        Reminder,
        Personal
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ENTRY STATUS — tracks lifecycle of each entry
    // ═══════════════════════════════════════════════════════════════════════════
    public enum OrganizerStatus
    {
        Planned,
        Done,
        Postponed,
        Cancelled
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ENTRY PRIORITY — importance marker
    // ═══════════════════════════════════════════════════════════════════════════
    public enum OrganizerPriority
    {
        Low,
        Normal,
        High,
        Urgent
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  RECURRENCE TYPE — for repeating reminders/alarms
    // ═══════════════════════════════════════════════════════════════════════════
    public enum RecurrenceType
    {
        None,
        Daily,
        Weekly,
        Monthly
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ORGANIZER ENTRY — one item on a given day
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Represents a single organizer entry (meeting, task, reminder, etc.)
    /// stored per-day in the local JSON database.
    /// </summary>
    public class OrganizerEntry
    {
        /// <summary>Unique identifier (GUID string)</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Date this entry belongs to (yyyy-MM-dd)</summary>
        public string Date { get; set; }

        /// <summary>Short title shown in calendar and list</summary>
        public string Title { get; set; } = "";

        /// <summary>Detailed description / notes</summary>
        public string Description { get; set; } = "";

        /// <summary>Category: Meeting, Interview, Call, Task, Reminder, Personal</summary>
        public OrganizerCategory Category { get; set; } = OrganizerCategory.Task;

        /// <summary>Status: Planned, Done, Postponed, Cancelled</summary>
        public OrganizerStatus Status { get; set; } = OrganizerStatus.Planned;

        /// <summary>Priority: Low, Normal, High, Urgent</summary>
        public OrganizerPriority Priority { get; set; } = OrganizerPriority.Normal;

        /// <summary>Start time (HH:mm format, nullable)</summary>
        public string TimeFrom { get; set; } = "";

        /// <summary>End time (HH:mm format, nullable)</summary>
        public string TimeTo { get; set; } = "";

        /// <summary>Whether this item is completed (checkbox)</summary>
        public bool IsCompleted { get; set; } = false;

        /// <summary>Optional link (Teams, Zoom, website, etc.)</summary>
        public string Link { get; set; } = "";

        /// <summary>Sticky note text (separate from description)</summary>
        public string StickyNote { get; set; } = "";

        // ── ALARM / REMINDER FIELDS ──

        /// <summary>Whether an alarm is set for this entry</summary>
        public bool AlarmEnabled { get; set; } = false;

        /// <summary>Alarm date+time (ISO 8601 local)</summary>
        public string AlarmDateTime { get; set; } = "";

        /// <summary>Whether the alarm has already fired (to prevent re-firing)</summary>
        public bool AlarmFired { get; set; } = false;

        /// <summary>Snooze until this time (ISO 8601 local, empty = no snooze)</summary>
        public string SnoozedUntil { get; set; } = "";

        /// <summary>Recurrence type for repeating alarms</summary>
        public RecurrenceType Recurrence { get; set; } = RecurrenceType.None;

        // ── METADATA ──

        /// <summary>When this entry was created (ISO 8601 UTC)</summary>
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");

        /// <summary>When this entry was last modified (ISO 8601 UTC)</summary>
        public string ModifiedAt { get; set; } = DateTime.UtcNow.ToString("o");

        /// <summary>Username of the owner (for multi-user support later)</summary>
        public string Owner { get; set; } = "";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  DAILY NOTES — a separate sticky-note blob per day
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Free-form daily notes saved per date. Separate from individual entries.
    /// </summary>
    public class DailyNotes
    {
        /// <summary>Date key (yyyy-MM-dd)</summary>
        public string Date { get; set; }

        /// <summary>Free-form note text for the day</summary>
        public string Notes { get; set; } = "";

        /// <summary>Last saved timestamp (ISO 8601 UTC)</summary>
        public string ModifiedAt { get; set; } = DateTime.UtcNow.ToString("o");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    //  ORGANIZER DATABASE — top-level container saved to JSON
    // ═══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Root container for all organizer data — serialized to a single JSON file.
    /// </summary>
    public class OrganizerDatabase
    {
        /// <summary>All organizer entries across all dates</summary>
        public List<OrganizerEntry> Entries { get; set; } = new List<OrganizerEntry>();

        /// <summary>Daily notes keyed by date string</summary>
        public List<DailyNotes> DailyNotes { get; set; } = new List<DailyNotes>();

        /// <summary>Schema version for future migrations</summary>
        public int SchemaVersion { get; set; } = 1;
    }
}
