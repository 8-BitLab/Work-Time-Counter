// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        FirebaseTrafficTracker.cs                                    ║
// ║  PURPOSE:     TRACKS ALL FIREBASE HTTP TRAFFIC (BYTES SENT & RECEIVED)     ║
// ║               SO USERS CAN MONITOR THEIR MONTHLY USAGE IN SETTINGS         ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝
//
// ══════════════════════════════════════════════════════════════════════════════
// HOW IT WORKS:
// ══════════════════════════════════════════════════════════════════════════════
//
//   INSTEAD OF USING HttpClient DIRECTLY, USE FirebaseTrafficTracker:
//     var response = await FirebaseTrafficTracker.GetAsync(url);
//     var response = await FirebaseTrafficTracker.PostAsync(url, content);
//     var response = await FirebaseTrafficTracker.PutAsync(url, content);
//     var response = await FirebaseTrafficTracker.PatchAsync(url, content);
//     var response = await FirebaseTrafficTracker.DeleteAsync(url);
//
//   EVERY CALL AUTOMATICALLY COUNTS BYTES UPLOADED AND DOWNLOADED
//   THE COUNTERS PERSIST TO A LOCAL FILE SO THEY SURVIVE APP RESTARTS
//   COUNTERS RESET AUTOMATICALLY ON THE 1ST OF EACH MONTH
//
//   USAGE DATA IS STORED AT:
//     %APPDATA%/WorkTimeCounter/firebase_traffic.json
//
// ══════════════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    // ══════════════════════════════════════════════════════════════════════════
    // TRAFFIC DATA MODEL — PERSISTED TO LOCAL JSON FILE
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Data model for tracking Firebase API traffic statistics.
    /// All fields are serialized to JSON and persist across app restarts.
    /// Counters automatically reset on the 1st of each month.
    /// </summary>
    public class TrafficData
    {
        /// <summary>Total bytes uploaded to Firebase (HTTP request bodies)</summary>
        public long BytesSent { get; set; }

        /// <summary>Total bytes downloaded from Firebase (HTTP response bodies)</summary>
        public long BytesReceived { get; set; }

        /// <summary>Total number of HTTP API requests made to Firebase</summary>
        public int RequestCount { get; set; }

        /// <summary>Month key (format "YYYY-MM") — used to detect when to reset counters</summary>
        public string MonthKey { get; set; }

        /// <summary>ISO 8601 timestamp of last update</summary>
        public string LastUpdated { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // FIREBASE TRAFFIC TRACKER — STATIC CLASS FOR EASY ACCESS EVERYWHERE
    // Provides drop-in replacements for HttpClient GET/POST/PUT/PATCH/DELETE
    // that automatically track bytes sent/received and persist to local JSON
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Static class that wraps HttpClient and tracks Firebase API traffic statistics.
    /// All traffic counters persist to disk and automatically reset on the 1st of each month.
    /// Thread-safe for concurrent API calls.
    /// </summary>
    public static class FirebaseTrafficTracker
    {
        // ─── SHARED HTTP CLIENT (REUSED FOR ALL FIREBASE REQUESTS) ───
        private static readonly HttpClient _http = new HttpClient();

        // ─── LOCK OBJECT FOR THREAD-SAFE COUNTER UPDATES ───
        private static readonly object _lock = new object();

        // ─── IN-MEMORY TRAFFIC COUNTERS ───
        private static TrafficData _data;

        // ─── LOCAL FILE PATH FOR PERSISTING TRAFFIC DATA ───
        private static readonly string DATA_FILE = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "WorkTimeCounter", "firebase_traffic.json");

        // ══════════════════════════════════════════════════════════════════
        // STATIC CONSTRUCTOR — LOADS SAVED TRAFFIC DATA FROM DISK AT STARTUP
        // ══════════════════════════════════════════════════════════════════
        static FirebaseTrafficTracker()
        {
//             DebugLogger.Log("[FirebaseTraffic] Static constructor — loading traffic data from disk");
            LoadData();
        }

        // ══════════════════════════════════════════════════════════════════
        // PUBLIC PROPERTIES — READ-ONLY ACCESS TO CURRENT TRAFFIC STATS
        // All getters are thread-safe via locking
        // ══════════════════════════════════════════════════════════════════

        /// <summary>Total bytes uploaded to Firebase in the current month</summary>
        public static long BytesSent { get { lock (_lock) return _data.BytesSent; } }

        /// <summary>Total bytes downloaded from Firebase in the current month</summary>
        public static long BytesReceived { get { lock (_lock) return _data.BytesReceived; } }

        /// <summary>Total bytes (sent + received) in the current month</summary>
        public static long TotalBytes { get { lock (_lock) return _data.BytesSent + _data.BytesReceived; } }

        /// <summary>Total number of API requests made in the current month</summary>
        public static int RequestCount { get { lock (_lock) return _data.RequestCount; } }

        /// <summary>Current month key in "YYYY-MM" format</summary>
        public static string MonthKey { get { lock (_lock) return _data.MonthKey; } }

        // ══════════════════════════════════════════════════════════════════
        // FORMATTED STRINGS — FOR DISPLAY IN UI
        // These convert byte counts to human-readable format (B, KB, MB, GB)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>BytesSent formatted as human-readable string (e.g., "5.2 MB")</summary>
        public static string FormattedSent => FormatBytes(BytesSent);

        /// <summary>BytesReceived formatted as human-readable string (e.g., "12.3 MB")</summary>
        public static string FormattedReceived => FormatBytes(BytesReceived);

        /// <summary>TotalBytes formatted as human-readable string (e.g., "17.5 MB")</summary>
        public static string FormattedTotal => FormatBytes(TotalBytes);

        // ══════════════════════════════════════════════════════════════════
        // HTTP METHODS — DROP-IN REPLACEMENTS FOR HttpClient
        // All methods automatically count bytes and handle response re-wrapping
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sends GET request and counts bytes sent/received.
        /// Response content is re-wrapped to allow caller to read the response.
        /// </summary>
        public static async Task<HttpResponseMessage> GetAsync(string url)
        {
//             DebugLogger.Log("[FirebaseTraffic] GetAsync — url=" + url);
            CountSent(url.Length + 50); // URL + HTTP headers estimate
            var response = await _http.GetAsync(url);
            string body = await response.Content.ReadAsStringAsync();
            CountReceived(body.Length);
            // RE-WRAP THE RESPONSE SO CALLER CAN STILL READ IT
            response.Content = new StringContent(body, Encoding.UTF8, "application/json");
//             DebugLogger.Log("[FirebaseTraffic] GetAsync complete — status=" + response.StatusCode);
            return response;
        }

        /// <summary>
        /// Sends POST request with JSON body and counts bytes sent/received.
        /// Response content is re-wrapped to allow caller to read the response.
        /// </summary>
        public static async Task<HttpResponseMessage> PostAsync(string url, StringContent content)
        {
//             DebugLogger.Log("[FirebaseTraffic] PostAsync — url=" + url);
            string bodyStr = await content.ReadAsStringAsync();
            CountSent(url.Length + bodyStr.Length + 100); // URL + body + headers estimate
            var newContent = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            var response = await _http.PostAsync(url, newContent);
            string respBody = await response.Content.ReadAsStringAsync();
            CountReceived(respBody.Length);
            response.Content = new StringContent(respBody, Encoding.UTF8, "application/json");
//             DebugLogger.Log("[FirebaseTraffic] PostAsync complete — status=" + response.StatusCode);
            return response;
        }

        /// <summary>
        /// Sends PUT request with JSON body and counts bytes sent/received.
        /// Response content is re-wrapped to allow caller to read the response.
        /// </summary>
        public static async Task<HttpResponseMessage> PutAsync(string url, StringContent content)
        {
//             DebugLogger.Log("[FirebaseTraffic] PutAsync — url=" + url);
            string bodyStr = await content.ReadAsStringAsync();
            CountSent(url.Length + bodyStr.Length + 100); // URL + body + headers estimate
            var newContent = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            var response = await _http.PutAsync(url, newContent);
            string respBody = await response.Content.ReadAsStringAsync();
            CountReceived(respBody.Length);
            response.Content = new StringContent(respBody, Encoding.UTF8, "application/json");
//             DebugLogger.Log("[FirebaseTraffic] PutAsync complete — status=" + response.StatusCode);
            return response;
        }

        /// <summary>
        /// Sends PATCH request with JSON body and counts bytes sent/received.
        /// Uses HttpMethod("PATCH") for .NET Framework 4.7.2 compatibility.
        /// Response content is re-wrapped to allow caller to read the response.
        /// </summary>
        public static async Task<HttpResponseMessage> PatchAsync(string url, StringContent content)
        {
//             DebugLogger.Log("[FirebaseTraffic] PatchAsync — url=" + url);
            string bodyStr = await content.ReadAsStringAsync();
            CountSent(url.Length + bodyStr.Length + 100); // URL + body + headers estimate
            var newContent = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = newContent
            };
            var response = await _http.SendAsync(request);
            string respBody = await response.Content.ReadAsStringAsync();
            CountReceived(respBody.Length);
            response.Content = new StringContent(respBody, Encoding.UTF8, "application/json");
//             DebugLogger.Log("[FirebaseTraffic] PatchAsync complete — status=" + response.StatusCode);
            return response;
        }

        /// <summary>
        /// Sends DELETE request and counts bytes sent/received.
        /// Response content is re-wrapped to allow caller to read the response.
        /// </summary>
        public static async Task<HttpResponseMessage> DeleteAsync(string url)
        {
//             DebugLogger.Log("[FirebaseTraffic] DeleteAsync — url=" + url);
            CountSent(url.Length + 50); // URL + HTTP headers estimate
            var response = await _http.DeleteAsync(url);
            string respBody = await response.Content.ReadAsStringAsync();
            CountReceived(respBody.Length);
            response.Content = new StringContent(respBody, Encoding.UTF8, "application/json");
//             DebugLogger.Log("[FirebaseTraffic] DeleteAsync complete — status=" + response.StatusCode);
            return response;
        }

        // ══════════════════════════════════════════════════════════════════
        // RESET — MANUALLY RESET COUNTERS (e.g. FROM SETTINGS PANEL)
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Manually resets all traffic counters to zero and updates month key to current month.
        /// Thread-safe. Updates are persisted to disk.
        /// </summary>
        public static void ResetCounters()
        {
//             DebugLogger.Log("[FirebaseTraffic] ResetCounters called");
            lock (_lock)
            {
                _data.BytesSent = 0;
                _data.BytesReceived = 0;
                _data.RequestCount = 0;
                _data.MonthKey = DateTime.Now.ToString("yyyy-MM");
                _data.LastUpdated = DateTime.UtcNow.ToString("o");
                SaveData();
//                 DebugLogger.Log("[FirebaseTraffic] Counters reset");
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // INTERNAL HELPERS — COUNT BYTES, TRACK REQUESTS, AND PERSIST
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Records bytes sent in an HTTP request.
        /// Increments request counter, checks for month rollover, and persists to disk.
        /// Thread-safe via lock.
        /// </summary>
        private static void CountSent(long bytes)
        {
            lock (_lock)
            {
                CheckMonthReset(); // Check if we crossed into a new month
                _data.BytesSent += bytes;
                _data.RequestCount++;
                _data.LastUpdated = DateTime.UtcNow.ToString("o");
                SaveData();
            }
        }

        /// <summary>
        /// Records bytes received in an HTTP response.
        /// Updates timestamp and persists to disk.
        /// Thread-safe via lock.
        /// </summary>
        private static void CountReceived(long bytes)
        {
            lock (_lock)
            {
                _data.BytesReceived += bytes;
                _data.LastUpdated = DateTime.UtcNow.ToString("o");
                SaveData();
            }
        }

        /// <summary>
        /// Checks if the month has changed since last reset.
        /// If so, automatically resets all counters (called "billing reset").
        /// </summary>
        private static void CheckMonthReset()
        {
            string currentMonth = DateTime.Now.ToString("yyyy-MM");
            if (_data.MonthKey != currentMonth)
            {
//                 DebugLogger.Log("[FirebaseTraffic] Month changed — resetting counters. Old month=" + _data.MonthKey +
//                                ", new month=" + currentMonth);
                _data.BytesSent = 0;
                _data.BytesReceived = 0;
                _data.RequestCount = 0;
                _data.MonthKey = currentMonth;
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // PERSISTENCE: LOAD / SAVE TRAFFIC DATA TO LOCAL JSON FILE
        // File location: %AppData%\WorkTimeCounter\firebase_traffic.json
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads traffic data from disk JSON file.
        /// Creates default TrafficData if file doesn't exist or JSON parsing fails.
        /// Initializes MonthKey if missing.
        /// </summary>
        private static void LoadData()
        {
//             DebugLogger.Log("[FirebaseTraffic] LoadData called");
            try
            {
                string dir = Path.GetDirectoryName(DATA_FILE);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
//                     DebugLogger.Log("[FirebaseTraffic] Created data directory: " + dir);
                }

                if (File.Exists(DATA_FILE))
                {
                    string json = File.ReadAllText(DATA_FILE);
                    _data = JsonConvert.DeserializeObject<TrafficData>(json) ?? new TrafficData();
//                     DebugLogger.Log("[FirebaseTraffic] Loaded traffic data from disk — bytes sent=" + _data.BytesSent +
//                                    ", bytes received=" + _data.BytesReceived + ", month=" + _data.MonthKey);
                }
                else
                {
                    _data = new TrafficData();
//                     DebugLogger.Log("[FirebaseTraffic] No traffic file found — creating new");
                }

                // ─── INITIALIZE MONTH KEY IF MISSING ───
                if (string.IsNullOrEmpty(_data.MonthKey))
                {
                    _data.MonthKey = DateTime.Now.ToString("yyyy-MM");
//                     DebugLogger.Log("[FirebaseTraffic] Set initial MonthKey: " + _data.MonthKey);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[FirebaseTraffic] LoadData exception: " + ex.Message + " — using default");
                _data = new TrafficData { MonthKey = DateTime.Now.ToString("yyyy-MM") };
            }
        }

        /// <summary>
        /// Saves traffic data to disk JSON file (indented for readability).
        /// Creates directory if needed. Silent on failure (logs to Debug).
        /// </summary>
        private static void SaveData()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_data, Formatting.Indented);
                File.WriteAllText(DATA_FILE, json);
//                 DebugLogger.Log("[FirebaseTraffic] Traffic data saved to disk");
            }
            catch (Exception ex)
            {
                DebugLogger.Log("[FirebaseTraffic] SaveData failed: " + ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        // FORMATTING: HUMAN-READABLE BYTE SIZE STRINGS
        // Converts byte counts to B, KB, MB, or GB with appropriate precision
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Formats a byte count as a human-readable string.
        /// Examples: 512 → "512 B", 1048576 → "1.0 MB", 1073741824 → "1.00 GB"
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("F2") + " GB";
        }
    }
}
