using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.Models
{
    public class GroupMember
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int GroupChatId { get; set; }

        [ForeignKey("GroupChatId")]
        public GroupChat GroupChat { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required, MaxLength(20)]
        public string Role { get; set; } = "Member"; // "Admin" hoặc "Member"

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}

