namespace PRN222.Models
{
    public class SystemSetting
    {
        public Guid Id { get; set; }
        public string SettingKey { get; set; } // Unique key for the setting
        public string SettingValue { get; set; }
        public string Description { get; set; }
        public string SettingType { get; set; } // "String", "Integer", "Boolean", "JSON"
        public DateTime CreatedDate { get; set; }
        public DateTime? LastModifiedDate { get; set; }

        // For Live Model Swapping
        // Example: SettingKey = "ActiveModelName", SettingValue = "GPT-4"
    }
}
