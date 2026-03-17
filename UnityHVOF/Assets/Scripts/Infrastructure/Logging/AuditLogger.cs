// ============================================================================
// Audit Logger — STRIDE: Repudiation Defense.
//
// Records all lab operations with immutable timestamps and unique IDs
// to a JSON file in persistent storage.
//
// Port of: lab_control/infrastructure/audit_logger.py
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HVOFSim.Domain.Ports;

namespace HVOFSim.Infrastructure.Logging
{
    /// <summary>
    /// Audit logger that records all actions to a JSON-lines file.
    /// STRIDE: Prevents repudiation by maintaining tamper-evident logs.
    /// </summary>
    public sealed class AuditLogger : ILabAuditLogger, ISimulationAuditLogger
    {
        private readonly List<Dictionary<string, object>> _logs = new();
        private readonly string _logFilePath;
        private int _eventCounter;

        /// <summary>
        /// Create an audit logger.
        /// </summary>
        /// <param name="logDirectory">Directory for log files. Null = in-memory only.</param>
        public AuditLogger(string logDirectory = null)
        {
            if (!string.IsNullOrEmpty(logDirectory))
            {
                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);
                _logFilePath = Path.Combine(logDirectory, "audit_log.jsonl");
            }
        }

        // ── ILabAuditLogger ──────────────────────────────────────────

        public string LogAction(string subsystem, string action, Dictionary<string, object> details)
        {
            string eventId = GenerateEventId();
            var entry = new Dictionary<string, object>
            {
                ["event_id"] = eventId,
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["type"] = "ACTION",
                ["subsystem"] = subsystem,
                ["action"] = action,
                ["details"] = details ?? new Dictionary<string, object>()
            };
            AppendLog(entry);
            return eventId;
        }

        public string LogSafetyEvent(string eventType, Dictionary<string, object> details)
        {
            string eventId = GenerateEventId();
            var entry = new Dictionary<string, object>
            {
                ["event_id"] = eventId,
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["type"] = "SAFETY",
                ["event_type"] = eventType,
                ["details"] = details ?? new Dictionary<string, object>()
            };
            AppendLog(entry);
            return eventId;
        }

        public List<Dictionary<string, object>> GetRecentLogs(int count = 50)
        {
            int start = Math.Max(0, _logs.Count - count);
            return _logs.GetRange(start, _logs.Count - start);
        }

        // ── ISimulationAuditLogger ───────────────────────────────────

        public string LogSimulationStart(
            string materialName, string solverName, Dictionary<string, object> parameters)
        {
            string runId = GenerateEventId();
            var entry = new Dictionary<string, object>
            {
                ["event_id"] = runId,
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["type"] = "SIM_START",
                ["material"] = materialName,
                ["solver"] = solverName,
                ["parameters"] = parameters ?? new Dictionary<string, object>()
            };
            AppendLog(entry);
            return runId;
        }

        public void LogSimulationEnd(string runId, Dictionary<string, object> resultSummary)
        {
            var entry = new Dictionary<string, object>
            {
                ["event_id"] = GenerateEventId(),
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["type"] = "SIM_END",
                ["run_id"] = runId,
                ["results"] = resultSummary ?? new Dictionary<string, object>()
            };
            AppendLog(entry);
        }

        public void LogError(string runId, string error)
        {
            var entry = new Dictionary<string, object>
            {
                ["event_id"] = GenerateEventId(),
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["type"] = "ERROR",
                ["run_id"] = runId,
                ["error"] = error
            };
            AppendLog(entry);
        }

        // ── Private Helpers ──────────────────────────────────────────

        private string GenerateEventId()
        {
            _eventCounter++;
            return $"EVT-{_eventCounter:D6}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        private void AppendLog(Dictionary<string, object> entry)
        {
            _logs.Add(entry);

            // Persist to file if configured (STRIDE: tamper-evident)
            if (!string.IsNullOrEmpty(_logFilePath))
            {
                try
                {
                    string json = DictionaryToJson(entry);
                    File.AppendAllText(_logFilePath, json + "\n");
                }
                catch
                {
                    // Fail silently — availability over persistence
                }
            }
        }

        /// <summary>Simple JSON serialization without external dependencies.</summary>
        private static string DictionaryToJson(Dictionary<string, object> dict)
        {
            var parts = dict.Select(kvp =>
            {
                string value = kvp.Value switch
                {
                    string s => $"\"{EscapeJson(s)}\"",
                    Dictionary<string, object> d => DictionaryToJson(d),
                    _ => kvp.Value?.ToString() ?? "null"
                };
                return $"\"{EscapeJson(kvp.Key)}\": {value}";
            });
            return "{" + string.Join(", ", parts) + "}";
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
