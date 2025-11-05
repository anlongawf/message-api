using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.Models
{
    public class ChatMessage
    {
        [Key]
        [Column("IdMessage")]
        public int Id { get; set; }


        [Required]
        public int SenderId { get; set; }  // Người gửi

        [ForeignKey("SenderId")]
        public User Sender { get; set; }

        [Required]
        public int ReceiverId { get; set; }  // Người nhận

        [ForeignKey("ReceiverId")]
        public User Receiver { get; set; }

        [MaxLength(1000)]
        public string? Message { get; set; }

        // Thông tin file (ảnh, video)
        [MaxLength(500)]
        public string? FileUrl { get; set; } // Đường dẫn file

        [MaxLength(50)]
        public string? FileType { get; set; } // "image", "video", hoặc null nếu là text

        [MaxLength(255)]
        public string? FileName { get; set; } // Tên file gốc

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}