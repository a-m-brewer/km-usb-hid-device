using System;
using System.IO;
using System.Text.Json;

namespace RemoteHIDController
{
    public class AppSettings
    {
        public string LastIpAddress { get; set; } = "192.168.1.177";
        
        // Add future settings here as properties
        // public bool AutoConnect { get; set; } = false;
        // public int MouseSensitivity { get; set; } = 1;

        private const string SettingsFileName = "settings.json";

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    var json = File.ReadAllText(SettingsFileName);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    return settings ?? new AppSettings();
                }
            }
            catch
            {
                // If there's an error loading, return defaults
            }
            
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                // Load existing settings first to preserve other values
                var existingSettings = Load();
                
                // Update only the properties that are set on this instance
                // This prevents overwriting other settings
                var json = File.Exists(SettingsFileName) 
                    ? File.ReadAllText(SettingsFileName) 
                    : "{}";
                
                var existingJson = JsonSerializer.Deserialize<JsonDocument>(json);
                var thisJson = JsonSerializer.SerializeToDocument(this);
                
                // Merge settings
                var mergedSettings = new AppSettings();
                if (existingSettings != null)
                {
                    // Copy existing settings
                    mergedSettings.LastIpAddress = existingSettings.LastIpAddress;
                    // Add future settings copy here
                }
                
                // Override with current values
                if (!string.IsNullOrEmpty(this.LastIpAddress))
                {
                    mergedSettings.LastIpAddress = this.LastIpAddress;
                }
                
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                };
                var jsonOutput = JsonSerializer.Serialize(mergedSettings, options);
                File.WriteAllText(SettingsFileName, jsonOutput);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
