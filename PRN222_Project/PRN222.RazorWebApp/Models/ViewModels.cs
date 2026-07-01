using System.ComponentModel.DataAnnotations;

namespace PRN222.RazorWebApp.Models
{
    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; } = "";
        [Required]
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; }
    }

    public class RegisterViewModel
    {
        [Required]
        public string Username { get; set; } = "";
        [Required]
        public string Password { get; set; } = "";
        [Required]
        public string ConfirmPassword { get; set; } = "";
    }

    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 50 ký tự.")]
        public string Username { get; set; } = "";
        
        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 đến 50 ký tự.")]
        public string Password { get; set; } = "";
        
        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu.")]
        [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
        public string ConfirmPassword { get; set; } = "";
        
        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        public string Role { get; set; } = "Student";
        public List<Guid> AssignedCourseIds { get; set; } = new();
    }

    public class EditUserViewModel
    {
        public Guid Id { get; set; }
        
        [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 50 ký tự.")]
        public string Username { get; set; } = "";
        
        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        public string Role { get; set; } = "Student";
        public List<Guid> AssignedCourseIds { get; set; } = new();
    }
}
