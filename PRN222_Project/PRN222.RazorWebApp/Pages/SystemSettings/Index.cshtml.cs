using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.SystemSettings
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ISystemSettingService _systemSettingService;

        public IndexModel(ISystemSettingService systemSettingService)
        {
            _systemSettingService = systemSettingService ?? throw new ArgumentNullException(nameof(systemSettingService));
        }

        public string ActiveModel { get; set; } = "bge-m3";
        public string ActiveStrategy { get; set; } = "markdown_header";
        public int ChunkSize { get; set; } = 500;

        public async Task OnGetAsync()
        {
            var val = await _systemSettingService.GetSettingValueAsync("ActiveEmbeddingModel");
            ActiveModel = string.IsNullOrEmpty(val) ? "bge-m3" : val;

            var strat = await _systemSettingService.GetSettingValueAsync("ActiveChunkingStrategy");
            ActiveStrategy = string.IsNullOrEmpty(strat) ? "markdown_header" : strat;

            var sizeStr = await _systemSettingService.GetSettingValueAsync("ChunkSize");
            ChunkSize = int.TryParse(sizeStr, out int size) ? size : 500;
        }

        public async Task<IActionResult> OnPostUpdateActiveSettingsAsync(string activeModel, string activeStrategy, int chunkSize)
        {
            if (string.IsNullOrWhiteSpace(activeModel) || string.IsNullOrWhiteSpace(activeStrategy) || chunkSize <= 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn cấu hình hợp lệ.";
                return RedirectToPage();
            }

            try
            {
                await _systemSettingService.SetSettingAsync("ActiveEmbeddingModel", activeModel);
                await _systemSettingService.SetSettingAsync("ActiveChunkingStrategy", activeStrategy);
                await _systemSettingService.SetSettingAsync("ChunkSize", chunkSize.ToString());
                TempData["SuccessMessage"] = $"Đã cập nhật cấu hình mặc định thành công: Mô hình = {activeModel} | Chặt chunk = {activeStrategy} | Kích thước chunk = {chunkSize}. Hệ thống sẽ áp dụng ngay lập tức.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi cập nhật cấu hình: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateActiveModelAjaxAsync(string activeModel)
        {
            if (string.IsNullOrWhiteSpace(activeModel))
            {
                return new JsonResult(new { success = false, message = "Vui lòng chọn model hợp lệ." });
            }

            try
            {
                await _systemSettingService.SetSettingAsync("ActiveEmbeddingModel", activeModel);
                return new JsonResult(new { success = true, message = $"Đã cập nhật model mặc định sang {activeModel}." });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, message = ex.Message });
            }
        }
    }
}
