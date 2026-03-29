using System;
using System.Diagnostics;

namespace Work_Time_Counter
{
    public static class DebugLogger
    {
        private static DebugForm _debugForm;
        private static bool _initialized;

        public static void Initialize(DebugForm debugForm)
        {
            _debugForm = debugForm;
            _initialized = true;
        }

        private static bool IsImportantMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("[error]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("fatal", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void Log(string message)
        {
            if (!_initialized)
                return;

            if (!IsImportantMessage(message))
                return;

            try
            {
                string timestamped = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
                Debug.WriteLine(timestamped);
            }
            catch
            {
            }
        }

        public static bool IsInitialized => _initialized;
    }
}
