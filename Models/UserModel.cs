using System.Linq;
using System.Text.Json.Serialization;

namespace TuProyecto.Models
{
    public class UserModel
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? MiddleName { get; set; }
        public string? LastName { get; set; }
        public string? DateOfBirth { get; set; } // yyyy-MM-dd

        [JsonIgnore]
        public string? DisplayName
        {
            get
            {
                var parts = new[] { FirstName, MiddleName, LastName }
                            .Where(p => !string.IsNullOrWhiteSpace(p))
                            .Select(p => p!.Trim()).ToArray();
                if (parts.Length == 0) return null;
                return string.Join(' ', parts);
            }
        }
    }
}
