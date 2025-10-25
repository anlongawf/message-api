using System.ComponentModel.DataAnnotations;

namespace Messenger.DTO
{
    public class LoginDTO
    {
        [Required]
        [MaxLength(50)]
        public string Email { get; set; }

        [Required]
        [MaxLength(100)]
        public string Password { get; set; }
    }
}