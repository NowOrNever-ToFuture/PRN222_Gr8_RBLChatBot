using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;

namespace PRN222.Services
{
    public class SystemSettingsHub : Hub { }

    public class SystemSettingService : ISystemSettingService
    {
        private readonly AppDbContext _dbContext;
        private readonly IHubContext<SystemSettingsHub> _hubContext;

        public SystemSettingService(AppDbContext dbContext, IHubContext<SystemSettingsHub> hubContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
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
            SystemSetting setting;
            var existingSetting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == settingKey);

            if (existingSetting != null)
            {
                existingSetting.SettingValue = settingValue;
                existingSetting.Description = description;
                existingSetting.SettingType = settingType;
                existingSetting.LastModifiedDate = DateTime.UtcNow;
                setting = existingSetting;
            }
            else
            {
                setting = new SystemSetting
                {
                    Id = Guid.NewGuid(),
                    SettingKey = settingKey,
                    SettingValue = settingValue,
                    Description = description,
                    SettingType = settingType,
                    CreatedDate = DateTime.UtcNow
                };

                _dbContext.SystemSettings.Add(setting);
            }

            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveSettingChanged", new
            {
                key = setting.SettingKey,
                value = setting.SettingValue,
                description = setting.Description,
                settingType = setting.SettingType,
                lastModifiedDate = setting.LastModifiedDate,
                createdDate = setting.CreatedDate
            });
        }

        public async Task DeleteSettingAsync(string settingKey)
        {
            var setting = await _dbContext.SystemSettings
                .FirstOrDefaultAsync(s => s.SettingKey == settingKey);

            if (setting == null)
                throw new InvalidOperationException($"Setting with key '{settingKey}' not found.");

            _dbContext.SystemSettings.Remove(setting);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("ReceiveSettingDeleted", settingKey);
        }

        public async Task<bool> SettingExistsAsync(string settingKey)
        {
            return await _dbContext.SystemSettings
                .AnyAsync(s => s.SettingKey == settingKey);
        }
    }
}
