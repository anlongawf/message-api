using System.ComponentModel.DataAnnotations;

namespace Messenger.DTO
{
    public class ChangePasswordDTO
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string OldPassword { get; set; }

        [Required]
        [MaxLength(100)]
        public string NewPassword { get; set; }
    }
}

