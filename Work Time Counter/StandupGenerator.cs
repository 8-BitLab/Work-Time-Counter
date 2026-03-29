// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        StandupGenerator.cs                                          ║
// ║  PURPOSE:     DAILY STANDUP SUMMARY GENERATOR                              ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Work_Time_Counter
{
    /// <summary>
    /// Static helper class that generates daily standup summaries from work log data.
    /// </summary>
    public static class StandupGenerator
    {
        /// <summary>
        /// Generates a formatted daily standup summary based on yesterday's and today's logged work.
        /// </summary>
        /// <param name="logs">List of work log entries.</param>
        /// <param name="userName">The name of the user.</param>
        /// <param name="date">The date for which to generate the standup (typically today).</param>
        /// <returns>A formatted standup summary string.</returns>
        public static string GenerateStandup(List<LogEntryWithIndex> logs, string userName, DateTime date)
        {
            // [Standup] Generate daily standup summary for user
//             DebugLogger.Log($"[Standup] GenerateStandup({userName}, {date:yyyy-MM-dd})");
            if (logs == null)
            {
//                 DebugLogger.Log("[Standup] Logs were null - initializing empty list");
                logs = new List<LogEntryWithIndex>();
            }

            DateTime yesterday = date.AddDays(-1);
            DateTime today = date;

            var yesterdayLogs = logs
                .Where(l => l.userName == userName && ParseDate(l.timestamp) == yesterday.Date)
                .ToList();

            var todayLogs = logs
                .Where(l => l.userName == userName && ParseDate(l.timestamp) == today.Date)
                .ToList();

//             DebugLogger.Log($"[Standup] Found {yesterdayLogs.Count} logs for yesterday, {todayLogs.Count} for today");

            var yesterdayTotal = CalculateTotalTime(yesterdayLogs);
            var todayTotal = CalculateTotalTime(todayLogs);

            var sb = new StringBuilder();
            sb.AppendLine($"📋 Daily Standup — {userName} ({date:dd MMM yyyy})");
            sb.AppendLine();

            // Yesterday section
            sb.AppendLine("✅ Yesterday:");
            if (yesterdayLogs.Count == 0)
            {
                sb.AppendLine("• No work logged");
            }
            else
            {
                foreach (var log in yesterdayLogs)
                {
                    var timeStr = FormatWorkingTime(log.workingTime);
                    sb.AppendLine($"• Worked on \"{log.description}\" ({log.project}) — {timeStr}");
                }
            }
            sb.AppendLine();

            // Today's plan section
            sb.AppendLine("📌 Today's Plan:");
            if (todayLogs.Count > 0)
            {
                var projects = new HashSet<string>();
                foreach (var log in todayLogs)
                {
                    if (!string.IsNullOrWhiteSpace(log.description) && projects.Add(log.description))
                    {
                        sb.AppendLine($"• Continue: {log.description}");
                    }
                }
            }
            sb.AppendLine("• (Add your tasks here)");
            sb.AppendLine();

            // Yesterday total
            sb.AppendLine($"⏱ Yesterday Total: {yesterdayTotal}");

            var result = sb.ToString();
//             DebugLogger.Log($"[Standup] Standup generated successfully ({result.Length} chars)");
            return result;
        }

        /// <summary>
        /// Generates a weekly summary grouped by day and project category.
        /// </summary>
        /// <param name="logs">List of work log entries.</param>
        /// <param name="userName">The name of the user.</param>
        /// <param name="weekStart">The start date of the week (Monday).</param>
        /// <returns>A formatted weekly summary string.</returns>
        public static string GenerateWeeklySummary(List<LogEntryWithIndex> logs, string userName, DateTime weekStart)
        {
            // [Standup] Generate weekly work summary for user
//             DebugLogger.Log($"[Standup] GenerateWeeklySummary({userName}, {weekStart:yyyy-MM-dd})");
            if (logs == null)
            {
//                 DebugLogger.Log("[Standup] Logs were null - initializing empty list");
                logs = new List<LogEntryWithIndex>();
            }

            DateTime weekEnd = weekStart.AddDays(6);

            var weekLogs = logs
                .Where(l => l.userName == userName &&
                       ParseDate(l.timestamp) >= weekStart.Date &&
                       ParseDate(l.timestamp) <= weekEnd.Date)
                .OrderBy(l => l.timestamp)
                .ToList();

//             DebugLogger.Log($"[Standup] Found {weekLogs.Count} logs for the week");

            var sb = new StringBuilder();
            sb.AppendLine($"📊 Weekly Summary — {userName} ({weekStart:dd MMM} - {weekEnd:dd MMM yyyy})");
            sb.AppendLine();

            if (weekLogs.Count == 0)
            {
//                 DebugLogger.Log("[Standup] No work logged this week");
                sb.AppendLine("No work logged this week.");
                return sb.ToString();
            }

            // Group by day
            var groupedByDay = weekLogs.GroupBy(l => ParseDate(l.timestamp))
                .OrderBy(g => g.Key)
                .ToList();

            var totalWeekTime = new TimeSpan();

            foreach (var dayGroup in groupedByDay)
            {
                var dayDate = dayGroup.Key;
                var dayName = dayDate.ToString("dddd, dd MMM");
                var dayLogs = dayGroup.ToList();

                sb.AppendLine($"📅 {dayName}");

                // Group by project category
                var groupedByProject = dayLogs.GroupBy(l => l.project)
                    .OrderBy(g => g.Key)
                    .ToList();

                TimeSpan dayTotal = new TimeSpan();

                foreach (var projectGroup in groupedByProject)
                {
                    var projectName = projectGroup.Key;
                    var projectLogs = projectGroup.ToList();
                    var projectTime = CalculateTotalTimeSpan(projectLogs);
                    dayTotal = dayTotal.Add(projectTime);

                    sb.AppendLine($"  • {projectName}: {FormatTimeSpan(projectTime)}");

                    foreach (var log in projectLogs)
                    {
                        sb.AppendLine($"    - {log.description}");
                    }
                }

                sb.AppendLine($"  ⏱ Day Total: {FormatTimeSpan(dayTotal)}");
                sb.AppendLine();

                totalWeekTime = totalWeekTime.Add(dayTotal);
            }

            sb.AppendLine($"📊 Weekly Total: {FormatTimeSpan(totalWeekTime)}");

            var result = sb.ToString();
//             DebugLogger.Log($"[Standup] Weekly summary generated successfully ({result.Length} chars)");
            return result;
        }

        /// <summary>
        /// Parses a timestamp string to a DateTime. Handles various formats.
        /// </summary>
        private static DateTime ParseDate(string timestamp)
        {
            // [Standup] Parse timestamp string to DateTime (handles multiple formats)
            if (string.IsNullOrWhiteSpace(timestamp))
            {
//                 DebugLogger.Log("[Standup] ParseDate: empty timestamp - returning today");
                return DateTime.Now.Date;
            }

            if (DateTime.TryParse(timestamp, out var result))
            {
//                 DebugLogger.Log($"[Standup] ParseDate: {timestamp} -> {result:yyyy-MM-dd}");
                return result.Date;
            }

            DebugLogger.Log($"[Standup] ParseDate: failed to parse '{timestamp}' - returning today");
            return DateTime.Now.Date;
        }

        /// <summary>
        /// Calculates total working time from a list of log entries.
        /// </summary>
        private static string CalculateTotalTime(List<LogEntryWithIndex> logs)
        {
            // [Standup] Calculate and format total working time
            var total = CalculateTotalTimeSpan(logs);
            var result = FormatTimeSpan(total);
//             DebugLogger.Log($"[Standup] CalculateTotalTime: {logs.Count} logs -> {result}");
            return result;
        }

        /// <summary>
        /// Calculates total working time as a TimeSpan.
        /// </summary>
        private static TimeSpan CalculateTotalTimeSpan(List<LogEntryWithIndex> logs)
        {
            // [Standup] Sum all working times in log entries
            var total = new TimeSpan();

            foreach (var log in logs)
            {
                var time = ParseWorkingTime(log.workingTime);
                total = total.Add(time);
            }

//             DebugLogger.Log($"[Standup] CalculateTotalTimeSpan: {logs.Count} logs -> {total:hh\\:mm}");
            return total;
        }

        /// <summary>
        /// Parses workingTime string (format: "2h 30m", "1h 15m", etc.) to TimeSpan.
        /// </summary>
        private static TimeSpan ParseWorkingTime(string workingTime)
        {
            // [Standup] Parse working time format (e.g., "2h 30m") to TimeSpan
            if (string.IsNullOrWhiteSpace(workingTime))
            {
//                 DebugLogger.Log("[Standup] ParseWorkingTime: empty - returning zero");
                return TimeSpan.Zero;
            }

            var hours = 0;
            var minutes = 0;

            var parts = workingTime.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                if (part.EndsWith("h"))
                {
                    if (int.TryParse(part.TrimEnd('h'), out var h))
                        hours = h;
                }
                else if (part.EndsWith("m"))
                {
                    if (int.TryParse(part.TrimEnd('m'), out var m))
                        minutes = m;
                }
            }

            var result = new TimeSpan(hours, minutes, 0);
//             DebugLogger.Log($"[Standup] ParseWorkingTime: '{workingTime}' -> {result:hh\\:mm}");
            return result;
        }

        /// <summary>
        /// Formats a TimeSpan as a human-readable string (e.g., "2h 30m").
        /// </summary>
        private static string FormatWorkingTime(string workingTime)
        {
            if (string.IsNullOrWhiteSpace(workingTime))
                return "0h 0m";

            return workingTime;
        }

        /// <summary>
        /// Formats a TimeSpan as a human-readable string (e.g., "2h 30m").
        /// </summary>
        private static string FormatTimeSpan(TimeSpan time)
        {
            // [Standup] Format TimeSpan as human-readable string (e.g., "2h 30m")
            var hours = (int)time.TotalHours;
            var minutes = time.Minutes;

            if (hours == 0 && minutes == 0)
                return "0h 0m";

            if (hours == 0)
                return $"{minutes}m";

            if (minutes == 0)
                return $"{hours}h";

            return $"{hours}h {minutes}m";
        }
    }
}
