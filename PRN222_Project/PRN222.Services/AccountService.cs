using Microsoft.EntityFrameworkCore;
using PRN222.Models;
using PRN222.Repositories;
using PRN222.Services.Interfaces;
using System.Security.Cryptography;

namespace PRN222.Services
{
    public class AccountService : IAccountService
    {
        private readonly AppDbContext _dbContext;

        public AccountService(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<User?> LoginAsync(string username, string password)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null || !VerifyPassword(password, user.PasswordHash))
            {
                return null;
            }
            return user;
        }

        public async Task<(bool Success, string ErrorMessage)> RegisterAsync(string username, string password)
        {
            if (await _dbContext.Users.AnyAsync(u => u.Username == username))
            {
                return (false, "Tên đăng nhập này đã được sử dụng.");
            }

            try
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Username = username,
                    FullName = username,
                    PasswordHash = HashPassword(password),
                    Role = "Student"
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi đăng ký: {ex.Message}");
            }
        }

        public string HashPassword(string password)
        {
            byte[] salt = new byte[16] { 80, 82, 78, 50, 50, 50, 95, 83, 65, 76, 84, 95, 75, 69, 89, 33 };

            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
            {
                byte[] hash = pbkdf2.GetBytes(32);
                byte[] hashWithSalt = new byte[48];
                System.Buffer.BlockCopy(salt, 0, hashWithSalt, 0, 16);
                System.Buffer.BlockCopy(hash, 0, hashWithSalt, 16, 32);
                return Convert.ToBase64String(hashWithSalt);
            }
        }

        public bool VerifyPassword(string passwordPlain, string passwordHash)
        {
            try
            {
                if (string.IsNullOrEmpty(passwordHash)) return false;

                byte[] salt = new byte[16] { 80, 82, 78, 50, 50, 50, 95, 83, 65, 76, 84, 95, 75, 69, 89, 33 };
                byte[] hashWithSalt = Convert.FromBase64String(passwordHash);

                if (hashWithSalt.Length != 48) return false;

                using (var pbkdf2 = new Rfc2898DeriveBytes(passwordPlain, salt, 10000, HashAlgorithmName.SHA256))
                {
                    byte[] hash = pbkdf2.GetBytes(32);
                    return CompareBytes(hashWithSalt, 16, hash, 0, 32);
                }
            }
            catch
            {
                return false;
            }
        }

        private bool CompareBytes(byte[] array1, int offset1, byte[] array2, int offset2, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (array1[offset1 + i] != array2[offset2 + i]) return false;
            }
            return true;
        }
    }
}
