using System.Collections.Generic;
using System.Threading.Tasks;
using PRN222.Models;

namespace PRN222.Services.Interfaces
{
    public interface ISystemSettingService
    {
        Task<string> GetSettingValueAsync(string settingKey);
        Task<SystemSetting> GetSettingAsync(string settingKey);
        Task<List<SystemSetting>> GetAllSettingsAsync();
        Task SetSettingAsync(string settingKey, string settingValue, string description = "", string settingType = "String");
        Task DeleteSettingAsync(string settingKey);
        Task<bool> SettingExistsAsync(string settingKey);
    }
}
