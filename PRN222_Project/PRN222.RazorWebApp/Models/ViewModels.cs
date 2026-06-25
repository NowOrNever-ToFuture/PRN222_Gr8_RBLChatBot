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
        [Required]
        public string Username { get; set; } = "";
        [Required]
        public string Password { get; set; } = "";
        [Required]
        public string ConfirmPassword { get; set; } = "";
        [Required]
        public string Role { get; set; } = "Student";
        public Guid? CourseId { get; set; }
    }

    public class EditUserViewModel
    {
        public Guid Id { get; set; }
        [Required]
        public string Username { get; set; } = "";
        [Required]
        public string Role { get; set; } = "Student";
        public Guid? CourseId { get; set; }
    }
}
