namespace Messenger.DTO
{
    public class CreateGroupDto
    {
        public string Name { get; set; }
        public int CreatorId { get; set; } // Người tạo nhóm sẽ là trưởng nhóm
    }
}

