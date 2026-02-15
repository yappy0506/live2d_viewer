using System.IO;
using UnityEngine;

namespace Live2DViewer
{
    public sealed class SettingsService
    {
        private readonly string _path;
        private readonly AppLogger _logger;

        public SettingsService(AppLogger logger)
        {
            _path = Path.Combine(Application.persistentDataPath, "config.json");
            _logger = logger;
        }

        public string ConfigPath => _path;

        public AppConfig LoadOrDefault()
        {
            if (!File.Exists(_path))
            {
                return new AppConfig();
            }

            try
            {
                var json = File.ReadAllText(_path);
                var cfg = JsonUtility.FromJson<AppConfig>(json);
                return cfg ?? new AppConfig();
            }
            catch
            {
                _logger.Warn("config.json parse failed, fallback default");
                return new AppConfig();
            }
        }

        public void Save(AppConfig config)
        {
            var json = JsonUtility.ToJson(config, true);
            File.WriteAllText(_path, json);
            _logger.Info($"settings saved: {_path}");
        }
    }
}
