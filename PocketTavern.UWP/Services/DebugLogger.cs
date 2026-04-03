using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PocketTavern.UWP.Services
{
    /// <summary>
    /// In-memory circular log + optional file persistence for API requests, responses and errors.
    /// Mirrors Android DebugLogger singleton.
    /// </summary>
    public static class DebugLogger
    {
        private const int MaxEntries = 200;
        private static readonly List<LogEntry> _entries = new List<LogEntry>();
        private static readonly object _lock = new object();
        private static string _logFilePath;
        private static bool _enabled = true;

        public static event EventHandler EntryAdded;

        /// <summary>
        /// Initialize file-based logging. Call once at app startup.
        /// logFolder should be a writable path (e.g., ApplicationData.Current.LocalFolder.Path).
        /// </summary>
        public static void Init(string logFolder)
        {
            try
            {
                _logFilePath = Path.Combine(logFolder, "debug_log.txt");
                File.WriteAllText(_logFilePath,
                    $"=== PocketTavern Debug Log Started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n");
                Log("DebugLogger initialized");
                Log($"Log file: {_logFilePath}");
            }
            catch { _logFilePath = null; }
        }

        public static void SetEnabled(bool enabled) => _enabled = enabled;

        public static void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (!_enabled) return;

            var entry = new LogEntry
            {
                Timestamp = DateTimeOffset.Now,
                Level     = level,
                Message   = message
            };
            lock (_lock)
            {
                _entries.Add(entry);
                if (_entries.Count > MaxEntries)
                    _entries.RemoveAt(0);
            }
            EntryAdded?.Invoke(null, EventArgs.Empty);
            System.Diagnostics.Debug.WriteLine($"[{level}] {message}");

            // Append to file if initialized
            if (_logFilePath != null)
            {
                try
                {
                    File.AppendAllText(_logFilePath,
                        $"[{entry.Timestamp:HH:mm:ss.fff}] [{level}] {message}\n");
                }
                catch { }
            }
        }

        public static void LogSection(string title)
            => Log($"===== {title} =====", LogLevel.Info);

        public static void LogKeyValue(string key, object value)
            => Log($"  {key}: {value ?? "(null)"}", LogLevel.Info);

        public static void LogRequest(string method, string url, string body = null)
        {
            var sb = new StringBuilder();
            sb.Append($"\u2192 {method} {url}");
            if (!string.IsNullOrEmpty(body))
            {
                var truncated = body.Length > 2000 ? body.Substring(0, 2000) + "\u2026" : body;
                sb.AppendLine().Append(truncated);
            }
            Log(sb.ToString(), LogLevel.Request);
        }

        public static void LogResponse(int statusCode, string body = null)
        {
            var sb = new StringBuilder();
            sb.Append($"\u2190 {statusCode}");
            if (!string.IsNullOrEmpty(body))
            {
                var truncated = body.Length > 2000 ? body.Substring(0, 2000) + "\u2026" : body;
                sb.AppendLine().Append(truncated);
            }
            Log(sb.ToString(), statusCode >= 400 ? LogLevel.Error : LogLevel.Response);
        }

        public static void LogError(string message) => Log(message, LogLevel.Error);

        public static void LogError(string tag, string message, Exception ex = null)
        {
            var msg = ex != null ? $"[{tag}] {message}\n{ex}" : $"[{tag}] {message}";
            Log(msg, LogLevel.Error);
        }

        public static void LogPrompt(string label, string prompt)
        {
            LogSection(label);
            Log("--- BEGIN PROMPT ---");
            foreach (var line in prompt.Split('\n'))
                Log(line);
            Log("--- END PROMPT ---");
        }

        public static void LogApiRequest(string endpoint, string requestBody)
        {
            LogSection($"API Request to {endpoint}");
            Log("--- REQUEST BODY ---");
            var lines = requestBody.Split('\n');
            for (int i = 0; i < Math.Min(100, lines.Length); i++)
                Log(lines[i]);
            if (lines.Length > 100)
                Log($"... (truncated, {lines.Length - 100} more lines)");
            Log("--- END REQUEST ---");
        }

        public static string GetLogContents()
        {
            if (_logFilePath != null)
            {
                try { return File.ReadAllText(_logFilePath); } catch { }
            }
            return ExportText();
        }

        public static void ClearLog()
        {
            Clear();
            if (_logFilePath != null)
            {
                try
                {
                    File.WriteAllText(_logFilePath,
                        $"=== Log Cleared {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n\n");
                }
                catch { }
            }
        }

        public static IReadOnlyList<LogEntry> GetEntries()
        {
            lock (_lock) return _entries.ToArray();
        }

        public static void Clear()
        {
            lock (_lock) _entries.Clear();
            EntryAdded?.Invoke(null, EventArgs.Empty);
        }

        public static string ExportText()
        {
            var sb = new StringBuilder();
            lock (_lock)
            {
                foreach (var e in _entries)
                    sb.AppendLine($"[{e.Timestamp:HH:mm:ss.fff}] [{e.Level}] {e.Message}");
            }
            return sb.ToString();
        }
    }

    public class LogEntry
    {
        public DateTimeOffset Timestamp { get; set; }
        public LogLevel       Level     { get; set; }
        public string         Message   { get; set; }
    }

    public enum LogLevel { Info, Request, Response, Error }
}
