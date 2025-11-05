-- Tạo database
CREATE DATABASE IF NOT EXISTS `s1_messenger-api` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE `s1_messenger-api`;

-- Bảng Users
CREATE TABLE IF NOT EXISTS `Users` (
    `IdUser` INT AUTO_INCREMENT PRIMARY KEY,
    `Email` VARCHAR(100) NOT NULL,
    `UserName` VARCHAR(50) NOT NULL,
    `Password` VARCHAR(100) NOT NULL
);

-- Bảng Messages (tin nhắn cá nhân)
CREATE TABLE IF NOT EXISTS `Messages` (
    `IdMessage` INT AUTO_INCREMENT PRIMARY KEY,
    `SenderId` INT NOT NULL,
    `ReceiverId` INT NOT NULL,
    `Message` VARCHAR(1000) NULL,
    `FileUrl` VARCHAR(500) NULL,
    `FileType` VARCHAR(50) NULL,
    `FileName` VARCHAR(255) NULL,
    `SentAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    FOREIGN KEY (`SenderId`) REFERENCES `Users`(`IdUser`) ON DELETE CASCADE,
    FOREIGN KEY (`ReceiverId`) REFERENCES `Users`(`IdUser`) ON DELETE CASCADE
);

-- Bảng Friends
CREATE TABLE IF NOT EXISTS `Friends` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `UserId` INT NOT NULL,
    `FriendId` INT NOT NULL,
    `Accepted` BOOLEAN NOT NULL DEFAULT FALSE,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    FOREIGN KEY (`UserId`) REFERENCES `Users`(`IdUser`) ON DELETE CASCADE,
    FOREIGN KEY (`FriendId`) REFERENCES `Users`(`IdUser`) ON DELETE CASCADE
);

-- Bảng GroupChats
CREATE TABLE IF NOT EXISTS `GroupChats` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `Name` VARCHAR(100) NOT NULL,
    `LeaderId` INT NOT NULL,
    `CreatedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    FOREIGN KEY (`LeaderId`) REFERENCES `Users`(`IdUser`) ON DELETE RESTRICT
);

-- Bảng GroupMembers
CREATE TABLE IF NOT EXISTS `GroupMembers` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `GroupChatId` INT NOT NULL,
    `UserId` INT NOT NULL,
    `Role` VARCHAR(20) NOT NULL DEFAULT 'Member',
    `JoinedAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    FOREIGN KEY (`GroupChatId`) REFERENCES `GroupChats`(`Id`) ON DELETE CASCADE,
    FOREIGN KEY (`UserId`) REFERENCES `Users`(`IdUser`) ON DELETE CASCADE
);

-- Bảng GroupMessages
CREATE TABLE IF NOT EXISTS `GroupMessages` (
    `Id` INT AUTO_INCREMENT PRIMARY KEY,
    `GroupChatId` INT NOT NULL,
    `SenderId` INT NOT NULL,
    `Message` VARCHAR(1000) NULL,
    `FileUrl` VARCHAR(500) NULL,
    `FileType` VARCHAR(50) NULL,
    `FileName` VARCHAR(255) NULL,
    `SentAt` DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    FOREIGN KEY (`GroupChatId`) REFERENCES `GroupChats`(`Id`) ON DELETE CASCADE,
    FOREIGN KEY (`SenderId`) REFERENCES `Users`(`IdUser`) ON DELETE RESTRICT
);

-- Tạo indexes (MySQL không hỗ trợ IF NOT EXISTS cho CREATE INDEX)
-- Chỉ tạo nếu chưa tồn tại bằng cách kiểm tra thủ công
-- Hoặc có thể bỏ qua nếu đã có sẵn trong CREATE TABLE

