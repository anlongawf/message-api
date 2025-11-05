using System.ComponentModel.DataAnnotations;

namespace Messenger.Models
{
    public class User
    {
        [Key]
        public int IdUser { get; set; }

        [Required, MaxLength(100)]
        public string Email { get; set; }

        [Required, MaxLength(50)]
        public string UserName { get; set; }

        [Required, MaxLength(100)]
        public string Password { get; set; }

        [MaxLength(500)]
        public string? AvatarUrl { get; set; } // URL của avatar
    }
}