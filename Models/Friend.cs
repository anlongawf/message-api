using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Messenger.Models
{
    public class Friend
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public int FriendId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool Accepted { get; set; } = false;

        public User User { get; set; }
        public User FriendUser { get; set; }
    }

}