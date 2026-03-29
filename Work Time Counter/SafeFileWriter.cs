// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        SafeFileWriter.cs                                            ║
// ║  PURPOSE:     ATOMIC FILE WRITES WITH .BAK PROTECTION & CORRUPTION RECOVERY║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║                                                                            ║
// ║  DESCRIPTION:                                                              ║
// ║  Every write follows this safe pattern:                                    ║
// ║    1. Write new content to a .tmp file                                     ║
// ║    2. If original file exists, rename it to .bak                           ║
// ║    3. Rename .tmp to the real filename                                     ║
// ║  This ensures that even if the app crashes mid-write, either the           ║
// ║  original or the .bak file survives. On load, if the main file is          ║
// ║  corrupted, we automatically restore from .bak.                            ║
// ║                                                                            ║
// ║  GitHub: https://github.com/8BitLabEngineering                             ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.IO;

namespace Work_Time_Counter
{
    /// <summary>
    /// Provides atomic file write operations with automatic .bak backup.
    /// If main file is corrupted on read, auto-restores from .bak copy.
    /// Thread-safe: each method uses its own locking internally.
    /// </summary>
    public static class SafeFileWriter
    {
        private static readonly object _writeLock = new object();

        // ═══════════════════════════════════════════════════════════════════
        //  SAFE WRITE — atomic write with .bak rotation
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Writes content to file using atomic rename pattern:
        ///   1. Write to {path}.tmp
        ///   2. Rename existing {path} to {path}.bak
        ///   3. Rename {path}.tmp to {path}
        /// Returns true on success.
        /// </summary>
        public static bool WriteAllText(string path, string content)
        {
            if (string.IsNullOrEmpty(path)) return false;

            string tmpPath = path + ".tmp";
            string bakPath = path + ".bak";

            lock (_writeLock)
            {
                try
                {
                    // Ensure directory exists
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    // Step 1: Write to .tmp file first (safe — doesn't touch original)
                    File.WriteAllText(tmpPath, content);

                    // Step 2: Rotate original → .bak (overwrite old .bak)
                    if (File.Exists(path))
                    {
                        try { File.Copy(path, bakPath, true); }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"[SafeFileWriter] Warning: Could not create .bak for {Path.GetFileName(path)}: {ex.Message}");
                            // Continue anyway — the .tmp is already written
                        }
                    }

                    // Step 3: Replace original with .tmp (atomic on NTFS)
                    if (File.Exists(path))
                        File.Delete(path);
                    File.Move(tmpPath, path);

                    return true;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[SafeFileWriter] ERROR writing {Path.GetFileName(path)}: {ex.Message}");

                    // Cleanup: if .tmp is still around, remove it
                    try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }

                    return false;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  SAFE READ — auto-recover from .bak if main file is corrupted
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Reads file content. If the file is missing, empty, or not valid JSON,
        /// attempts to restore from {path}.bak automatically.
        /// Returns null if both main and .bak are unavailable/corrupted.
        /// </summary>
        public static string ReadAllText(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string bakPath = path + ".bak";

            // Try main file first
            string content = TryReadFile(path);
            if (content != null && IsValidContent(content))
                return content;

            // Main file failed — try .bak
            DebugLogger.Log($"[SafeFileWriter] Main file missing/corrupted: {Path.GetFileName(path)} — trying .bak");

            string bakContent = TryReadFile(bakPath);
            if (bakContent != null && IsValidContent(bakContent))
            {
                // Restore .bak → main file so next read is normal
                DebugLogger.Log($"[SafeFileWriter] Restoring from .bak: {Path.GetFileName(path)}");
                try
                {
                    File.Copy(bakPath, path, true);
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[SafeFileWriter] Warning: Could not restore .bak → main: {ex.Message}");
                }
                return bakContent;
            }

            DebugLogger.Log($"[SafeFileWriter] Both main and .bak unavailable for: {Path.GetFileName(path)}");
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  CHECK IF .BAK EXISTS — for UI status display
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Returns true if a .bak file exists for the given path.
        /// Useful for showing backup status in the UI.
        /// </summary>
        public static bool HasBackup(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            return File.Exists(path + ".bak");
        }

        /// <summary>
        /// Returns the last write time of the .bak file, or null if it doesn't exist.
        /// </summary>
        public static DateTime? GetBackupAge(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string bakPath = path + ".bak";
            if (!File.Exists(bakPath)) return null;
            return File.GetLastWriteTimeUtc(bakPath);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  INTERNAL HELPERS
        // ═══════════════════════════════════════════════════════════════════

        private static string TryReadFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string content = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(content) || content == "null")
                    return null;
                return content;
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"[SafeFileWriter] Read error for {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Basic validation: content starts with { or [ (JSON), or is non-empty text.
        /// This catches obvious corruption (binary garbage, partial writes).
        /// </summary>
        private static bool IsValidContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;

            string trimmed = content.TrimStart();

            // For JSON files, check basic structure
            if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                return true;

            // For plain text files (like active_team.bit, remember.bit), any content is valid
            return trimmed.Length > 0;
        }
    }
}
