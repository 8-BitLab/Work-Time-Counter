// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        AlarmService.cs                                              ║
// ║  PURPOSE:     BACKGROUND ALARM MANAGEMENT SERVICE                          ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Manages background alarm checking on a background timer (30-second        ║
// ║  interval). Monitors organizer entries for pending alarms, fires events    ║
// ║  for UI notification, handles snoozing, dismissal, and recurring alarm     ║
// ║  creation. Extracted from Form1 for better modularity and testability.    ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    /// <summary>
    /// AlarmService manages background alarm checking and notification for the organizer module.
    /// Runs on a 30-second timer, checks for pending alarms, fires events, and handles recurrence.
    /// </summary>
    public class AlarmService
    {
        private Timer _alarmTimer;
        private HashSet<string> _firedAlarmIds = new HashSet<string>();
        private bool _isRunning = false;

        // ═══════════════════════════════════════════════════════════════════════════
        //  EVENTS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Raised when a pending alarm fires.
        /// Subscriber receives the OrganizerEntry for display in AlarmNotificationForm.
        /// </summary>
        public event Action<OrganizerEntry> AlarmTriggered;

        /// <summary>
        /// Raised when the list of upcoming alarms changes.
        /// Used to update a "coming soon" indicator or upcoming alarms panel.
        /// </summary>
        public event Action<List<OrganizerEntry>> UpcomingAlarmsChanged;

        // ═══════════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Create a new AlarmService. Must call Start() to begin monitoring.
        /// </summary>
        public AlarmService()
        {
            _alarmTimer = new Timer();
            _alarmTimer.Interval = 30000; // 30 seconds
            _alarmTimer.Tick += OnAlarmTimerTick;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  LIFECYCLE METHODS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Start the alarm service. Performs an initial check after a 5-second delay.
        /// </summary>
        public void Start()
        {
//             DebugLogger.Log("[AlarmService] Start() called");

            if (_isRunning)
            {
//                 DebugLogger.Log("[AlarmService] Already running, ignoring Start() call");
                return;
            }

            _isRunning = true;
//             DebugLogger.Log("[AlarmService] Service started, scheduling initial check in 5 seconds");

            // Schedule initial check after 5-second delay
            var initialCheckTimer = new Timer { Interval = 5000 };
            initialCheckTimer.Tick += (s, e) =>
            {
//                 DebugLogger.Log("[AlarmService] Initial check timer fired");
                initialCheckTimer.Stop();
                initialCheckTimer.Dispose();
                OnAlarmTimerTick(null, null);
            };
            initialCheckTimer.Start();

            // Start the recurring 30-second timer
            _alarmTimer.Start();
//             DebugLogger.Log("[AlarmService] Recurring 30-second timer started");
        }

        /// <summary>
        /// Stop the alarm service.
        /// </summary>
        public void Stop()
        {
//             DebugLogger.Log("[AlarmService] Stop() called");

            if (!_isRunning)
            {
//                 DebugLogger.Log("[AlarmService] Not running, ignoring Stop() call");
                return;
            }

            _isRunning = false;
            _alarmTimer.Stop();
            _firedAlarmIds.Clear();
//             DebugLogger.Log("[AlarmService] Service stopped, timer halted and fired alarm cache cleared");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  TIMER TICK HANDLER
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Called every 30 seconds. Checks for pending alarms and upcoming alarms.
        /// </summary>
        private void OnAlarmTimerTick(object sender, EventArgs e)
        {
            try
            {
//                 DebugLogger.Log("[AlarmService] Timer tick - checking for pending alarms");

                // Check for pending alarms
                var pendingAlarms = OrganizerStorage.GetPendingAlarms();
//                 DebugLogger.Log($"[AlarmService] Found {pendingAlarms.Count} pending alarm(s)");

                foreach (var entry in pendingAlarms)
                {
                    // Prevent duplicate firings
                    if (_firedAlarmIds.Contains(entry.Id))
                    {
//                         DebugLogger.Log($"[AlarmService] Alarm {entry.Id} ({entry.Title}) already fired, skipping");
                        continue;
                    }

//                     DebugLogger.Log($"[AlarmService] Firing alarm for '{entry.Title}' (ID: {entry.Id})");
                    _firedAlarmIds.Add(entry.Id);

                    // Mark as fired to prevent re-firing
                    OrganizerStorage.MarkAlarmFired(entry.Id);

                    // Fire the AlarmTriggered event
                    AlarmTriggered?.Invoke(entry);

                    // Create next occurrence if recurring
                    if (entry.Recurrence != RecurrenceType.None)
                    {
//                         DebugLogger.Log($"[AlarmService] Alarm '{entry.Title}' is recurring ({entry.Recurrence}), creating next occurrence");
                        CreateNextRecurrence(entry);
                    }
                }

                // Check for upcoming alarms (within 15 minutes)
                var upcomingAlarms = OrganizerStorage.GetUpcomingAlarms(15);
//                 DebugLogger.Log($"[AlarmService] Found {upcomingAlarms.Count} upcoming alarm(s) within 15 minutes");
                UpcomingAlarmsChanged?.Invoke(upcomingAlarms);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AlarmService] ERROR in timer tick: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  RECURRENCE HANDLING
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Create the next occurrence of a recurring alarm.
        /// Shifts the alarm date/time based on recurrence type (Daily/Weekly/Monthly).
        /// </summary>
        private void CreateNextRecurrence(OrganizerEntry entry)
        {
            try
            {
//                 DebugLogger.Log($"[AlarmService] CreateNextRecurrence for '{entry.Title}' (Recurrence: {entry.Recurrence})");

                if (!DateTime.TryParse(entry.AlarmDateTime, out var alarmDt))
                {
                    DebugLogger.Log($"[AlarmService] Failed to parse alarm datetime for '{entry.Title}', aborting");
                    return;
                }

                DateTime nextAlarm;
                DateTime nextDate;

                switch (entry.Recurrence)
                {
                    case RecurrenceType.Daily:
//                         DebugLogger.Log($"[AlarmService] Creating daily recurrence for '{entry.Title}'");
                        nextAlarm = alarmDt.AddDays(1);
                        nextDate = DateTime.TryParse(entry.Date, out var d)
                            ? d.AddDays(1)
                            : nextAlarm.Date;
                        break;

                    case RecurrenceType.Weekly:
//                         DebugLogger.Log($"[AlarmService] Creating weekly recurrence for '{entry.Title}'");
                        nextAlarm = alarmDt.AddDays(7);
                        nextDate = DateTime.TryParse(entry.Date, out var w)
                            ? w.AddDays(7)
                            : nextAlarm.Date;
                        break;

                    case RecurrenceType.Monthly:
//                         DebugLogger.Log($"[AlarmService] Creating monthly recurrence for '{entry.Title}'");
                        nextAlarm = alarmDt.AddMonths(1);
                        nextDate = DateTime.TryParse(entry.Date, out var m)
                            ? m.AddMonths(1)
                            : nextAlarm.Date;
                        break;

                    default:
//                         DebugLogger.Log($"[AlarmService] Unknown recurrence type {entry.Recurrence}, aborting");
                        return;
                }

                // Create new entry for next occurrence
                var nextEntry = new OrganizerEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Date = nextDate.ToString("yyyy-MM-dd"),
                    Title = entry.Title,
                    Description = entry.Description,
                    Category = entry.Category,
                    Status = OrganizerStatus.Planned,
                    Priority = entry.Priority,
                    TimeFrom = entry.TimeFrom,
                    TimeTo = entry.TimeTo,
                    Link = entry.Link,
                    StickyNote = entry.StickyNote,
                    AlarmEnabled = true,
                    AlarmDateTime = nextAlarm.ToString("o"),
                    AlarmFired = false,
                    SnoozedUntil = "",
                    Recurrence = entry.Recurrence,
                    Owner = entry.Owner,
                    CreatedAt = DateTime.UtcNow.ToString("o"),
                    ModifiedAt = DateTime.UtcNow.ToString("o")
                };

                OrganizerStorage.SaveEntry(nextEntry);
//                 DebugLogger.Log($"[AlarmService] Next recurrence created for '{entry.Title}' on {nextDate:yyyy-MM-dd} at {nextAlarm:HH:mm:ss}");

                // Remove from fired set so it can be fired again after creating next occurrence
                _firedAlarmIds.Remove(entry.Id);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AlarmService] ERROR creating next recurrence: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  ALARM CONTROL METHODS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Snooze an alarm for a given number of minutes.
        /// </summary>
        /// <param name="entryId">The organizer entry ID to snooze.</param>
        /// <param name="minutes">Number of minutes to snooze for.</param>
        public void SnoozeAlarm(string entryId, int minutes)
        {
            try
            {
//                 DebugLogger.Log($"[AlarmService] Snoozing alarm ID {entryId} for {minutes} minute(s)");
                OrganizerStorage.SnoozeAlarm(entryId, minutes);
                _firedAlarmIds.Remove(entryId);
//                 DebugLogger.Log($"[AlarmService] Alarm {entryId} snoozed successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AlarmService] ERROR snoozing alarm {entryId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Dismiss an alarm (mark it as fired so it won't trigger again).
        /// </summary>
        /// <param name="entryId">The organizer entry ID to dismiss.</param>
        public void DismissAlarm(string entryId)
        {
            try
            {
//                 DebugLogger.Log($"[AlarmService] Dismissing alarm ID {entryId}");
                OrganizerStorage.MarkAlarmFired(entryId);
                _firedAlarmIds.Add(entryId);
//                 DebugLogger.Log($"[AlarmService] Alarm {entryId} dismissed successfully");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AlarmService] ERROR dismissing alarm {entryId}: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  SUMMARY / STATUS METHODS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Get today's summary: total entries, completed entries, and pending alarms.
        /// </summary>
        /// <returns>
        /// Tuple containing:
        ///   - total: Total number of entries for today
        ///   - completed: Number of completed entries for today
        ///   - pendingAlarms: Number of pending (not yet fired) alarms for today
        /// </returns>
        public (int total, int completed, int pendingAlarms) GetTodaySummary()
        {
            try
            {
//                 DebugLogger.Log("[AlarmService] GetTodaySummary() called");

                string today = DateTime.Today.ToString("yyyy-MM-dd");
                var todayEntries = OrganizerStorage.GetEntriesForDate(today);

                int total = todayEntries.Count;
                int completed = todayEntries.Count(e => e.IsCompleted);
                int pendingAlarms = todayEntries.Count(e =>
                    e.AlarmEnabled &&
                    !e.AlarmFired &&
                    !string.IsNullOrEmpty(e.AlarmDateTime));

//                 DebugLogger.Log($"[AlarmService] Today's summary: Total={total}, Completed={completed}, PendingAlarms={pendingAlarms}");
                return (total, completed, pendingAlarms);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[AlarmService] ERROR getting today's summary: {ex.Message}");
                return (0, 0, 0);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        //  DISPOSAL
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Clean up resources. Call this when the application shuts down.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Stop();
            }
            catch { }
        }
    }
}