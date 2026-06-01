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

        public async Task OnGetAsync()
        {
            var val = await _systemSettingService.GetSettingValueAsync("ActiveEmbeddingModel");
            ActiveModel = string.IsNullOrEmpty(val) ? "bge-m3" : val;
        }

        public async Task<IActionResult> OnPostUpdateActiveModelAsync(string activeModel)
        {
            if (string.IsNullOrWhiteSpace(activeModel))
            {
                TempData["ErrorMessage"] = "Vui lòng chọn một mô hình hợp lệ.";
                return RedirectToPage();
            }
            try
            {
                await _systemSettingService.SetSettingAsync("ActiveEmbeddingModel", activeModel);
                TempData["SuccessMessage"] = $"Đã cập nhật mô hình mặc định thành công: {activeModel}. Hệ thống Chatbot RAG cho sinh viên sẽ áp dụng ngay lập tức.";
            }
            catch (Exception ex) { TempData["ErrorMessage"] = $"Lỗi cập nhật cấu hình: {ex.Message}"; }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateActiveModelAjaxAsync(string activeModel)
        {
            if (string.IsNullOrWhiteSpace(activeModel))
                return new JsonResult(new { success = false, message = "Model không hợp lệ." });
            try
            {
                await _systemSettingService.SetSettingAsync("ActiveEmbeddingModel", activeModel);
                return new JsonResult(new { success = true, model = activeModel });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
        }
    }
}
