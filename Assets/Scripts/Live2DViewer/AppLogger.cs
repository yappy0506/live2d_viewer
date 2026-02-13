using System;
using System.IO;
using UnityEngine;

namespace Live2DViewer
{
    public sealed class AppLogger
    {
        private readonly string _logPath;
        private readonly object _lock = new object();

        public AppLogger()
        {
            var logsDir = Path.Combine(Application.persistentDataPath, "logs");
            Directory.CreateDirectory(logsDir);
            _logPath = Path.Combine(logsDir, "app.log");
        }

        public string LogPath => _logPath;

        public void Info(string message) => Write("INFO", message);
        public void Warn(string message) => Write("WARN", message);
        public void Error(string message) => Write("ERROR", message);

        private void Write(string level, string message)
        {
            var line = $"{DateTimeOffset.Now:O}\t{level}\t{message}";
            lock (_lock)
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }

            if (level == "ERROR") Debug.LogError(message);
            else if (level == "WARN") Debug.LogWarning(message);
            else Debug.Log(message);
        }
    }
}
