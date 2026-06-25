CREATE DATABASE IF NOT EXISTS `rentalai` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE DATABASE IF NOT EXISTS `rentalai_notifications` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

GRANT ALL PRIVILEGES ON `rentalai`.* TO 'rentalai'@'%';
GRANT ALL PRIVILEGES ON `rentalai_notifications`.* TO 'rentalai'@'%';

FLUSH PRIVILEGES;
