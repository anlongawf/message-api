using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.Models
{
    public class GroupChat
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; }

        [Required]
        public int LeaderId { get; set; } // Trưởng nhóm

        [ForeignKey("LeaderId")]
        public User Leader { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

