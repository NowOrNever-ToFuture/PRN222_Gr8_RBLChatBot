using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.Models;
using PRN222.Services.Interfaces;

namespace PRN222.RazorWebApp.Pages.TestQuestions
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ITestQuestionService _testQuestionService;
        private readonly IImportService _importService;
        private readonly ICourseService _courseService;

        public IndexModel(ITestQuestionService testQuestionService, IImportService importService, ICourseService courseService)
        {
            _testQuestionService = testQuestionService;
            _importService = importService;
            _courseService = courseService;
        }

        public List<TestQuestion> Questions { get; set; } = new();
        public List<PRN222.Models.Course> Courses { get; set; } = new();

        public async Task OnGetAsync()
        {
            Questions = await _testQuestionService.GetAllAsync();
            Courses = await _courseService.GetAllCoursesAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync([FromBody] TestQuestion question)
        {
            try
            {
                var created = await _testQuestionService.CreateAsync(question);
                return new JsonResult(new { success = true, data = created });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
        }

        public async Task<IActionResult> OnPostEditAsync([FromBody] TestQuestion question)
        {
            try
            {
                var updated = await _testQuestionService.UpdateAsync(question);
                return new JsonResult(new { success = true, data = updated });
            }
            catch (Exception ex) { return new JsonResult(new { success = false, message = ex.Message }); }
        }

        public async Task<IActionResult> OnPostDeleteAsync([FromBody] DeleteRequest request)
        {
            var result = await _testQuestionService.DeleteAsync(request.Id);
            return new JsonResult(new { success = result });
        }

        public async Task<IActionResult> OnPostImportExcelAsync(IFormFile excelFile, Guid courseId)
        {
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Vui lòng chọn file Excel.";
                return RedirectToPage();
            }
            try
            {
                using var stream = excelFile.OpenReadStream();
                var (inserted, updated, errors) = await _importService.ImportTestQuestionsFromExcelAsync(stream, courseId);
                TempData["SuccessMessage"] = $"Import thành công! Thêm mới: {inserted}, Cập nhật: {updated}, Lỗi: {errors}";
            }
            catch (Exception ex) { TempData["ErrorMessage"] = $"Lỗi import: {ex.Message}"; }
            return RedirectToPage();
        }

        public IActionResult OnGetDownloadTemplate()
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var ws = workbook.Worksheets.Add("TestQuestions");
            ws.Cell(1, 1).Value = "QuestionText"; ws.Cell(1, 2).Value = "AnswerOptions";
            ws.Cell(1, 3).Value = "GroundTruth"; ws.Cell(1, 4).Value = "Explanation"; ws.Cell(1, 5).Value = "Difficulty";
            var headerRange = ws.Range("A1:E1");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.CornflowerBlue;
            headerRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;
            ws.Cell(2, 1).Value = "Triết học Mác-Lênin là gì?";
            ws.Cell(2, 2).Value = "{\"A\": \"Hệ thống triết học\", \"B\": \"Môn khoa học\", \"C\": \"Cả A và B\", \"D\": \"Không phải A và B\"}";
            ws.Cell(2, 3).Value = "Triết học Mác-Lênin là hệ thống triết học do Karl Marx và Friedrich Engels sáng lập.";
            ws.Cell(2, 4).Value = "Đáp án C đúng."; ws.Cell(2, 5).Value = 1;
            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms); ms.Position = 0;
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TestQuestions_Template.xlsx");
        }

        public class DeleteRequest { public Guid Id { get; set; } }
    }
}
