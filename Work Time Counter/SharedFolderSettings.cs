using System;
using System.IO;
using Newtonsoft.Json;

namespace Work_Time_Counter
{
    public class SharedFolderSettings
    {
        public bool Enabled { get; set; } = false;
        public string LocalSharedFolderPath { get; set; } = "";
        public bool AutoRefreshEnabled { get; set; } = true;
        public int RefreshIntervalSeconds { get; set; } = 30;
        public bool AutoDownload { get; set; } = false;

        private static string GetSettingsPath()
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WorkFlow");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            return Path.Combine(folder, "shared_folder_settings.json");
        }

        public static SharedFolderSettings LoadSettings()
        {
            try
            {
                string path = GetSettingsPath();
                if (!File.Exists(path))
                    return new SharedFolderSettings();

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return new SharedFolderSettings();

                var settings = JsonConvert.DeserializeObject<SharedFolderSettings>(json);
                return settings ?? new SharedFolderSettings();
            }
            catch
            {
                return new SharedFolderSettings();
            }
        }

        public static void SaveSettings(SharedFolderSettings settings)
        {
            try
            {
                string path = GetSettingsPath();
                string json = JsonConvert.SerializeObject(settings ?? new SharedFolderSettings(), Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch
            {
            }
        }
    }
}
