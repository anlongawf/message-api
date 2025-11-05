-- Thêm cột AvatarUrl vào bảng Users
ALTER TABLE `Users` 
ADD COLUMN `AvatarUrl` VARCHAR(500) NULL AFTER `Password`;


