using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnrylnroBannerlord.Utils
{
    class ConfigManager
    {
        public static ConfigManager Instance { get; private set; }

        public Dictionary<string, string> config = new();

        public ConfigManager()
        {
            Instance = this;
        }

        public void LoadConfig(string filePath)
        {
            if (filePath == null)
            {
                ModLogger.Warn("Can not to find config file.");
                return;
            }
            ModLogger.Log($"Loading config file: {filePath}");

            try
            {
                string[] array = File.ReadAllLines(filePath);

                List<string> list = new List<string>();
                for (int i = 0; i < array.Length; i++)
                {
                    string text = array[i];

                    if (text.IndexOf(' ') > 0)
                    {
                        string key = text.Substring(0, text.IndexOf(' '));
                        string value = text.Substring(text.IndexOf(' ') + 1);
                        // 如果配置项已经存在，则覆盖原有值，并记录日志
                        if (config.ContainsKey(key))
                        {
                            ModLogger.Warn($"Configuration item already exists: {key}, overwrite value: {value}");
                            config[key] = value;
                        }
                        else
                        {
                            config.Add(key, value);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLogger.Error($"Failed to load config file: {ex.Message}");
            }
        }

        public static string GetConfig(string key, string defaultIfAbsent = null)
        {
            if (Instance.config.ContainsKey(key))
            {
                ModLogger.Log($"Read configuration item: {key} = {Instance.config[key]}");
                return Instance.config[key];
            }
            else
            {
                // 构建日志消息：若默认值为 null，则不显示“使用默认值”部分
                string logMsg = defaultIfAbsent == null
                    ? $"Configuration item not found: {key}"
                    : $"Configuration item not found: {key}, use default value: {defaultIfAbsent}";
                ModLogger.Warn(logMsg);
                return defaultIfAbsent;
            }
        }
    }
}
