using MatchBuddy.Entities.Entity;

namespace MatchBuddy.Api.Model
{
    public class LoginModel
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
    }
}
