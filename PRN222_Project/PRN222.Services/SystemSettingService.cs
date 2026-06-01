using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class SystemSettingService : ISystemSettingService
    {
        private readonly AppDbContext _dbContext;

        public SystemSettingService(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<string> GetSettingValueAsync(string settingKey)
        {
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == settingKey);

            if (setting == null)
                return string.Empty;

            return setting.SettingValue;
        }

        public async Task<SystemSetting> GetSettingAsync(string settingKey)
        {
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == settingKey);

            if (setting == null)
                throw new InvalidOperationException($"Setting with key '{settingKey}' not found.");

            return setting;
        }

        public async Task<List<SystemSetting>> GetAllSettingsAsync()
        {
            return await _dbContext.SystemSettings
                .OrderBy(s => s.SettingKey)
                .ToListAsync();
        }

        public async Task SetSettingAsync(string settingKey, string settingValue, string description = "", string settingType = "String")
        {
            var existingSetting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == settingKey);

            if (existingSetting != null)
            {
                // Update existing setting
                existingSetting.SettingValue = settingValue;
                existingSetting.Description = description;
                existingSetting.SettingType = settingType;
                existingSetting.LastModifiedDate = DateTime.UtcNow;
            }
            else
            {
                // Create new setting
                var newSetting = new SystemSetting
                {
                    Id = Guid.NewGuid(),
                    SettingKey = settingKey,
                    SettingValue = settingValue,
                    Description = description,
                    SettingType = settingType,
                    CreatedDate = DateTime.UtcNow
                };

                _dbContext.SystemSettings.Add(newSetting);
            }

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteSettingAsync(string settingKey)
        {
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == settingKey);

            if (setting == null)
                throw new InvalidOperationException($"Setting with key '{settingKey}' not found.");

            _dbContext.SystemSettings.Remove(setting);
            await _dbContext.SaveChangesAsync();
        }

        public async Task<bool> SettingExistsAsync(string settingKey)
        {
            return await _dbContext.SystemSettings
                .AnyAsync(s => s.SettingKey == settingKey);
        }
    }
}
