-- ============================================================================
-- SRI SAKTHI LODGE DATABASE SCHEMA (MySQL)
-- Matches adatc-admin-webapp-backend SQL architecture & standard naming
-- ============================================================================

CREATE DATABASE IF NOT EXISTS sri_sakthi_lodge CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE sri_sakthi_lodge;

-- 1. USERS (Admin / Staff)
CREATE TABLE IF NOT EXISTS Users (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    Username      VARCHAR(100) NOT NULL UNIQUE,
    PasswordHash  VARCHAR(500) NOT NULL,
    Name          VARCHAR(200) NOT NULL,
    Role          VARCHAR(20)  NOT NULL DEFAULT 'staff', -- 'owner', 'staff'
    IsActive      TINYINT(1)   NOT NULL DEFAULT 1,
    CreatedAt     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 2. REFRESH TOKENS (Rotating Token Store with Sliding & Absolute Expiry)
CREATE TABLE IF NOT EXISTS RefreshTokens (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    UserId          INT           NOT NULL,
    TokenHash       VARCHAR(500)  NOT NULL UNIQUE,
    IpAddress       VARCHAR(50)   NULL,
    IsRevoked       TINYINT(1)    NOT NULL DEFAULT 0,
    RevokedAt       DATETIME      NULL,
    RevokedReason   VARCHAR(50)   NULL, -- 'logout', 'rotated', 'security'
    SlidingExpiry   DATETIME      NOT NULL,
    AbsoluteExpiry  DATETIME      NOT NULL,
    CreatedAt       DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ReplacedByHash  VARCHAR(500)  NULL,
    CONSTRAINT fk_refreshtokens_user FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    INDEX idx_refreshtokens_tokenhash (TokenHash),
    INDEX idx_refreshtokens_userid (UserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3. ROOMS
CREATE TABLE IF NOT EXISTS Rooms (
    Id            INT AUTO_INCREMENT PRIMARY KEY,
    Slug          VARCHAR(100) NOT NULL UNIQUE,
    NameEn        VARCHAR(200) NOT NULL,
    NameTa        VARCHAR(200) NOT NULL,
    ShortDescEn   VARCHAR(500) NULL,
    ShortDescTa   VARCHAR(500) NULL,
    DescriptionEn TEXT         NULL,
    DescriptionTa TEXT         NULL,
    IsAc          TINYINT(1)   NOT NULL DEFAULT 0,
    MaxOccupancy  INT          NOT NULL DEFAULT 2,
    StartingPrice DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    Currency      VARCHAR(10)  NOT NULL DEFAULT 'INR',
    TotalUnits    INT          NOT NULL DEFAULT 1,
    IsActive      TINYINT(1)   NOT NULL DEFAULT 1,
    IsDeleted     TINYINT(1)   NOT NULL DEFAULT 0,
    DeletedAt     DATETIME     NULL,
    SortOrder     INT          NOT NULL DEFAULT 0,
    CreatedAt     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3a. ROOM TYPES (Dynamic Room Types with Tamil translation & IsActive toggle)
CREATE TABLE IF NOT EXISTS RoomTypes (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    NameEn           VARCHAR(200) NOT NULL UNIQUE,
    NameTa           VARCHAR(200) NOT NULL,
    IsAc             TINYINT(1)   NOT NULL DEFAULT 0,
    DefaultOccupancy INT          NOT NULL DEFAULT 2,
    IsActive         TINYINT(1)   NOT NULL DEFAULT 1,
    SortOrder        INT          NOT NULL DEFAULT 0,
    CreatedAt        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 3b. ROOM UNITS (Physical Room Numbers per Room Type)
CREATE TABLE IF NOT EXISTS RoomUnits (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    RoomId     INT          NOT NULL,
    RoomNumber VARCHAR(20)  NOT NULL UNIQUE,
    Floor      VARCHAR(20)  NULL,
    IsActive   TINYINT(1)   NOT NULL DEFAULT 1,
    CreatedAt  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_roomunits_room FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE,
    INDEX idx_roomunits_roomid (RoomId),
    INDEX idx_roomunits_active (IsActive)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 4. ROOM IMAGES
CREATE TABLE IF NOT EXISTS RoomImages (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    RoomId    INT          NOT NULL,
    Url       VARCHAR(500) NOT NULL,
    AltEn     VARCHAR(300) NULL,
    AltTa     VARCHAR(300) NULL,
    SortOrder INT          NOT NULL DEFAULT 0,
    CONSTRAINT fk_roomimages_room FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE,
    INDEX idx_roomimages_roomid (RoomId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 5. ROOM AMENITIES
CREATE TABLE IF NOT EXISTS RoomAmenities (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    RoomId     INT          NOT NULL,
    AmenityKey VARCHAR(50)  NOT NULL,
    LabelEn    VARCHAR(100) NOT NULL,
    LabelTa    VARCHAR(100) NOT NULL,
    CONSTRAINT fk_roomamenities_room FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE,
    INDEX idx_roomamenities_roomid (RoomId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 6. ROOM NUMBERS
CREATE TABLE IF NOT EXISTS RoomNumbers (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    RoomId     INT         NOT NULL,
    RoomNumber VARCHAR(50) NOT NULL UNIQUE,
    CONSTRAINT fk_roomnumbers_room FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 7. BLOCKED DATES
CREATE TABLE IF NOT EXISTS BlockedDates (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    RoomId     INT          NOT NULL,
    RoomNumber VARCHAR(50)  NULL,
    FromDate   DATE         NOT NULL,
    ToDate     DATE         NOT NULL,
    Reason     VARCHAR(200) NULL,
    CreatedAt  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_blockeddates_room FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE,
    INDEX idx_blockeddates_room_dates (RoomId, FromDate, ToDate)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 8. GUESTS
CREATE TABLE IF NOT EXISTS Guests (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    Name      VARCHAR(200) NOT NULL,
    Phone     VARCHAR(20)  NOT NULL,
    Email     VARCHAR(200) NULL,
    IdType    VARCHAR(50)  NULL,
    Notes     TEXT         NULL,
    CreatedAt DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_guests_phone (Phone)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 9. BOOKINGS
CREATE TABLE IF NOT EXISTS Bookings (
    Id                 INT AUTO_INCREMENT PRIMARY KEY,
    Reference          VARCHAR(50)   NOT NULL UNIQUE,
    GuestId            INT           NOT NULL,
    RoomId             INT           NOT NULL,
    RoomNumber         VARCHAR(50)   NULL,
    CheckIn            DATE          NOT NULL,
    CheckOut           DATE          NOT NULL,
    Nights             INT           NOT NULL DEFAULT 1,
    Adults             INT           NOT NULL DEFAULT 1,
    Children           INT           NOT NULL DEFAULT 0,
    RoomsCount         INT           NOT NULL DEFAULT 1,
    TotalAmount        DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    AmountPaid         DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    Currency           VARCHAR(10)   NOT NULL DEFAULT 'INR',
    Status             VARCHAR(30)   NOT NULL DEFAULT 'pending',
    PaymentStatus      VARCHAR(20)   NOT NULL DEFAULT 'unpaid',
    Source             VARCHAR(20)   NOT NULL DEFAULT 'online',
    Notes              TEXT          NULL,
    CancellationReason VARCHAR(500)  NULL,
    CancelledAt        DATETIME      NULL,
    IdempotencyKey     VARCHAR(100)  NULL UNIQUE,
    CreatedAt          DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt          DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    CONSTRAINT fk_bookings_guest FOREIGN KEY (GuestId) REFERENCES Guests(Id),
    CONSTRAINT fk_bookings_room FOREIGN KEY (RoomId) REFERENCES Rooms(Id),
    INDEX idx_bookings_dates (CheckIn, CheckOut),
    INDEX idx_bookings_status (Status)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 10. BOOKING STATUS HISTORY
CREATE TABLE IF NOT EXISTS BookingStatusHistory (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    BookingId INT         NOT NULL,
    Status    VARCHAR(30) NOT NULL,
    ChangedAt DATETIME    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ChangedBy VARCHAR(100) NOT NULL,
    CONSTRAINT fk_history_booking FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 11. PAYMENTS
CREATE TABLE IF NOT EXISTS Payments (
    Id                INT AUTO_INCREMENT PRIMARY KEY,
    BookingId         INT           NOT NULL,
    Method            VARCHAR(20)   NOT NULL DEFAULT 'online',
    Amount            DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    Status            VARCHAR(20)   NOT NULL DEFAULT 'pending',
    RazorpayOrderId   VARCHAR(100)  NULL,
    RazorpayPaymentId VARCHAR(100)  NULL,
    Reference         VARCHAR(100)  NULL,
    PaidAt            DATETIME      NULL,
    CreatedAt         DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_payments_booking FOREIGN KEY (BookingId) REFERENCES Bookings(Id) ON DELETE CASCADE,
    INDEX idx_payments_order (RazorpayOrderId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 12. TARIFFS
CREATE TABLE IF NOT EXISTS Tariffs (
    Id         INT AUTO_INCREMENT PRIMARY KEY,
    RoomId     INT           NOT NULL,
    PlanName   VARCHAR(100)  NOT NULL,
    BaseRate   DECIMAL(10,2) NOT NULL,
    StartDate  DATE          NOT NULL,
    EndDate    DATE          NULL,
    Season     VARCHAR(50)   NULL,
    TaxPercent DECIMAL(5,2)  NULL,
    IsActive   TINYINT(1)    NOT NULL DEFAULT 1,
    CreatedAt  DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_tariffs_room FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 13. OFFERS
CREATE TABLE IF NOT EXISTS Offers (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    RoomId          INT          NULL,
    DiscountPercent DECIMAL(5,2) DEFAULT 0.00,
    OfferPrice      DECIMAL(10,2) NULL,
    TitleEn         VARCHAR(300) NOT NULL,
    TitleTa         VARCHAR(300) NOT NULL,
    DescEn          TEXT         NULL,
    DescTa          TEXT         NULL,
    ImageUrl        VARCHAR(500) NULL,
    StartDate       DATE         NOT NULL,
    EndDate         DATE         NOT NULL,
    TermsEn         TEXT         NULL,
    TermsTa         TEXT         NULL,
    IsPublished     TINYINT(1)   NOT NULL DEFAULT 0,
    SortOrder       INT          NOT NULL DEFAULT 0,
    CreatedAt       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt       DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 14. GALLERY
CREATE TABLE IF NOT EXISTS Gallery (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    Type        VARCHAR(10)  NOT NULL DEFAULT 'image',
    Url         VARCHAR(500) NOT NULL,
    TitleEn     VARCHAR(300) NULL,
    TitleTa     VARCHAR(300) NULL,
    AltEn       VARCHAR(300) NULL,
    AltTa       VARCHAR(300) NULL,
    Category    VARCHAR(50)  NULL,
    SortOrder   INT          NOT NULL DEFAULT 0,
    IsPublished TINYINT(1)   NOT NULL DEFAULT 0,
    CreatedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 15. REVIEWS
CREATE TABLE IF NOT EXISTS Reviews (
    Id               INT AUTO_INCREMENT PRIMARY KEY,
    Name             VARCHAR(200) NOT NULL,
    Rating           TINYINT      NOT NULL DEFAULT 5,
    BookingReference VARCHAR(50)  NULL,
    ReviewText       TEXT         NOT NULL,
    Status           VARCHAR(20)  NOT NULL DEFAULT 'pending',
    CreatedAt        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ModeratedAt      DATETIME     NULL,
    ModeratedBy      VARCHAR(100) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 16. ATTRACTIONS
CREATE TABLE IF NOT EXISTS Attractions (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    Slug        VARCHAR(100) NOT NULL UNIQUE,
    NameEn      VARCHAR(300) NOT NULL,
    NameTa      VARCHAR(300) NOT NULL,
    DescEn      TEXT         NULL,
    DescTa      TEXT         NULL,
    Category    VARCHAR(50)  NULL,
    ImageUrl    VARCHAR(500) NULL,
    MapsUrl     VARCHAR(500) NULL,
    IsPublished TINYINT(1)   NOT NULL DEFAULT 0,
    SortOrder   INT          NOT NULL DEFAULT 0,
    CreatedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 17. ENQUIRIES
CREATE TABLE IF NOT EXISTS Enquiries (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    Name      VARCHAR(200) NOT NULL,
    Phone     VARCHAR(20)  NOT NULL,
    Email     VARCHAR(200) NULL,
    Subject   VARCHAR(300) NULL,
    Message   TEXT         NOT NULL,
    Status    VARCHAR(20)  NOT NULL DEFAULT 'new',
    Notes     TEXT         NULL,
    CreatedAt DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- 18. SITE CONTENT
CREATE TABLE IF NOT EXISTS SiteContent (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    ContentKey  VARCHAR(100) NOT NULL UNIQUE,
    ValueJson   LONGTEXT     NOT NULL,
    IsDraft     TINYINT(1)   NOT NULL DEFAULT 0,
    UpdatedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UpdatedBy   VARCHAR(100) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- SEED INITIAL ADMIN USER
-- Password is 'admin' (hashed via ASP.NET Core Identity PasswordHasher<object>)
INSERT IGNORE INTO Users (Id, Username, PasswordHash, Name, Role, IsActive)
VALUES (1, 'admin', 'AQAAAAIAAYagAAAAEHhEjp63iH8vq48axmJFFwM7JTCPobM7+cBPuWOKmmARwUHlNnZxD3o35Yx02kIlRQ==', 'Lodge Administrator', 'owner', 1);

-- SEED SITE CONTENT
INSERT IGNORE INTO SiteContent (ContentKey, ValueJson, IsDraft)
VALUES ('main', '{"about":{"en":"Welcome to Sri Sakthi Lodge","ta":"ஸ்ரீ சக்தி லாட்ஜ் வரவேற்கிறது"},"announcement":{"active":false,"text":{"en":"","ta":""}},"faq":[],"policies":{"cancellation":{"en":"24 hours prior cancellation allows full refund.\\n\\n• Bookings can be cancelled through the website.\\n• The applicable refund amount is stated to be credited within 7 days.\\n• Guests must provide bank details for the refund transfer.\\n• The seven-day refund period applies to all cancellations.","ta":"24 மணிநேரத்திற்கு முன்பான ரத்து முழு பணத்தைத் திரும்பப் பெறும்.\\n\\n• முன்பதிவுகளை இணையதளம் மூலம் ரத்து செய்யலாம்.\\n• பொருந்தக்கூடிய பணத்தைத் திரும்பப் பெறும் தொகை 7 நாட்களுக்குள் வரவு வைக்கப்படும்.\\n• பணத்தைத் திரும்பப் பெறுவதற்கு விருந்தினர்கள் வங்கி விவரங்களை வழங்க வேண்டும்.\\n• ஏழு நாள் பணத்தைத் திரும்பப் பெறும் காலம் அனைத்து ரத்துகளுக்கும் பொருந்தும்."},"terms":{"en":"Standard lodge terms apply.","ta":"வழக்கமான விடுதி விதிமுறைகள் பொருந்தும்."},"privacy":{"en":"Guest data is strictly protected.","ta":"விருந்தினர் தரவு பாதுகாக்கப்படுகிறது."}}}', 0);

-- SEED INITIAL ROOMS
INSERT IGNORE INTO Rooms (Id, Slug, NameEn, NameTa, ShortDescEn, ShortDescTa, DescriptionEn, DescriptionTa, IsAc, MaxOccupancy, StartingPrice, Currency, TotalUnits, IsActive, SortOrder)
VALUES 
(1, 'single-bed', 'Single Bed', 'ஒற்றை படுக்கை', 'Clean and compact room for solo travelers', 'தனிப்பயணியருக்கான சுத்தமான அறை', 'Comfortable single bed room with essential amenities.', 'அனைத்து அடிப்படை வசதிகளுடன் கூடிய அறை.', 0, 2, 900.00, 'INR', 5, 1, 1),
(2, 'double-bed', 'Double Bed', 'இரட்டை படுக்கை', 'Spacious double bed room', 'விசாலமான இரட்டை படுக்கை அறை', 'Comfortable double bed room for couples or friends.', 'தம்பதிகள் அல்லது நண்பர்களுக்கான அறை.', 0, 3, 1300.00, 'INR', 15, 1, 2),
(3, 'standard-ac', 'Standard A/C', 'ஸ்டாண்டர்டு ஏசி', 'Air-conditioned comfort', 'குளிர்சாதன வசதியுடன் கூடிய அறை', 'Well furnished AC room for a pleasant stay.', 'சௌகரியமான தங்குதலுக்கு ஏற்ற ஏசி அறை.', 1, 3, 1800.00, 'INR', 15, 1, 3),
(4, 'five-bed-room', 'Five Bed Room', 'ஐந்து படுக்கை அறை', 'Clean and compact room for Bunch of travelers', 'பயணிகளுக்கான சுத்தமான அறை', '', '', 0, 6, 2000.00, 'INR', 8, 1, 4),
(6, 'three-bed-room', 'Three Bed Room', 'மூன்று படுக்கை அறை', 'Comfortable three bed room', 'மூன்று படுக்கை அறை', '', '', 0, 4, 1450.00, 'INR', 7, 1, 5);

-- SEED ROOM AMENITIES
INSERT IGNORE INTO RoomAmenities (Id, RoomId, AmenityKey, LabelEn, LabelTa)
VALUES 
(1, 1, 'wifi', 'Free Wi-Fi', 'இலவச வை-பை'),
(2, 2, 'wifi', 'Free Wi-Fi', 'இலவச வை-பை'),
(3, 3, 'wifi', 'Free Wi-Fi', 'இலவச வை-பை'),
(4, 3, 'ac', 'Air Conditioning', 'குளிர்சாதன வசதி');

-- SEED DEFAULT TARIFFS
INSERT IGNORE INTO Tariffs (Id, RoomId, PlanName, BaseRate, StartDate, EndDate, Season, TaxPercent, IsActive)
VALUES 
(1, 1, 'Standard Daily Rate', 900.00, '2024-01-01', NULL, 'Regular', NULL, 1),
(2, 2, 'Standard Daily Rate', 1300.00, '2024-01-01', NULL, 'Regular', NULL, 1),
(3, 3, 'Standard Daily Rate', 1800.00, '2024-01-01', NULL, 'Regular', NULL, 1),
(4, 4, 'Standard Rate', 2000.00, '2024-01-01', NULL, 'Regular', NULL, 1),
(5, 6, 'Standard Rate', 1450.00, '2024-01-01', NULL, 'Regular', NULL, 1);

-- SEED ROOM IMAGES
INSERT IGNORE INTO RoomImages (Id, RoomId, Url, AltEn, AltTa, SortOrder)
VALUES 
(1, 1, '/uploads/ab5a7435-760d-4761-ba29-c2cdedfbdbfc_1 bed.jpeg', NULL, NULL, 0),
(3, 2, '/uploads/6ae7c1bf-7d54-47f4-ae33-b35525730497_2 bed wit AC.PNG', NULL, NULL, 0),
(8, 3, '/uploads/868fea5a-83e0-4619-afc0-1dda31642242_1 bed wit ac.PNG', NULL, NULL, 0),
(9, 4, '/uploads/40ec0134-e5d6-450b-9fbd-b8ed2abb0256_5 bed.PNG', NULL, NULL, 0);

-- SEED INITIAL ROOM UNITS (Physical room numbers)
INSERT IGNORE INTO RoomUnits (RoomId, RoomNumber, Floor, IsActive) VALUES
(1, '101', '1st Floor', 1), (1, '102', '1st Floor', 1), (1, '103', '1st Floor', 1), (1, '104', '1st Floor', 1), (1, '105', '1st Floor', 1),
(2, '201', '2nd Floor', 1), (2, '202', '2nd Floor', 1), (2, '203', '2nd Floor', 1), (2, '204', '2nd Floor', 1), (2, '205', '2nd Floor', 1),
(2, '206', '2nd Floor', 1), (2, '207', '2nd Floor', 1), (2, '208', '2nd Floor', 1), (2, '209', '2nd Floor', 1), (2, '210', '2nd Floor', 1),
(2, '211', '2nd Floor', 1), (2, '212', '2nd Floor', 1), (2, '213', '2nd Floor', 1), (2, '214', '2nd Floor', 1), (2, '215', '2nd Floor', 1),
(3, '301', '3rd Floor', 1), (3, '302', '3rd Floor', 1), (3, '303', '3rd Floor', 1), (3, '304', '3rd Floor', 1), (3, '305', '3rd Floor', 1),
(3, '306', '3rd Floor', 1), (3, '307', '3rd Floor', 1), (3, '308', '3rd Floor', 1), (3, '309', '3rd Floor', 1), (3, '310', '3rd Floor', 1),
(3, '311', '3rd Floor', 1), (3, '312', '3rd Floor', 1), (3, '313', '3rd Floor', 1), (3, '314', '3rd Floor', 1), (3, '315', '3rd Floor', 1),
(4, '401', '4th Floor', 1), (4, '402', '4th Floor', 1), (4, '403', '4th Floor', 1), (4, '404', '4th Floor', 1), (4, '405', '4th Floor', 1),
(4, '406', '4th Floor', 1), (4, '407', '4th Floor', 1), (4, '408', '4th Floor', 1),
(6, '501', '5th Floor', 1), (6, '502', '5th Floor', 1), (6, '503', '5th Floor', 1), (6, '504', '5th Floor', 1), (6, '505', '5th Floor', 1),
(6, '506', '5th Floor', 1), (6, '507', '5th Floor', 1);

-- SEED INITIAL ROOM TYPES
INSERT INTO RoomTypes (Id, NameEn, NameTa, IsAc, DefaultOccupancy, IsActive, SortOrder)
VALUES 
(1, 'Single Bed Room', 'ஒற்றை படுக்கை அறை', 0, 2, 1, 1),
(2, 'Double Bed Room', 'இரட்டை படுக்கை அறை', 0, 3, 1, 2),
(3, 'Three Bed Room', 'மூன்று படுக்கை அறை', 0, 4, 1, 3),
(4, 'Five Bed Room', 'ஐந்து படுக்கை அறை', 0, 6, 1, 4),
(5, 'Standard A/C Room', 'ஸ்டாண்டர்டு ஏசி அறை', 1, 3, 1, 5),
(6, 'Deluxe A/C Room', 'டீலக்ஸ் ஏசி அறை', 1, 3, 1, 6),
(7, 'A/C Suite Room', 'ஏசி சூட் அறை', 1, 4, 1, 7)
ON DUPLICATE KEY UPDATE 
    NameTa = VALUES(NameTa),
    IsAc = VALUES(IsAc),
    DefaultOccupancy = VALUES(DefaultOccupancy),
    IsActive = VALUES(IsActive),
    SortOrder = VALUES(SortOrder);

-- SEED UPI SETTINGS INTO SITECONTENT
INSERT INTO SiteContent (ContentKey, ValueJson, IsDraft, UpdatedAt, UpdatedBy)
VALUES (
    'upi_settings',
    '{"Enabled":true,"AllowPayAtHotel":false,"UpiId":"jawithtb786-1@oksbi","PayeeName":"Sri Sakthi Lodge","QrCodeUrl":"","AdvancePercent":60,"Instructions":"Scan with any UPI app (GPay / PhonePe / Paytm / BHIM) and pay the advance. Enter the UTR reference to confirm."}',
    0,
    NOW(),
    'system'
)
ON DUPLICATE KEY UPDATE ValueJson = VALUES(ValueJson), UpdatedAt = NOW();

-- OFFERS TRIGGERS (Auto-sync RoomId, DiscountPercent and OfferPrice)
DELIMITER //
CREATE TRIGGER IF NOT EXISTS trg_offers_before_insert BEFORE INSERT ON Offers FOR EACH ROW
BEGIN
    IF NEW.DescEn LIKE '%"roomId":%' THEN
        SET NEW.RoomId = CAST(JSON_UNQUOTE(JSON_EXTRACT(NEW.DescEn, '$.roomId')) AS SIGNED);
        IF NEW.DescEn LIKE '%"discountPercent":%' THEN
            SET NEW.DiscountPercent = CAST(JSON_UNQUOTE(JSON_EXTRACT(NEW.DescEn, '$.discountPercent')) AS DECIMAL(5,2));
        END IF;
        IF NEW.DescEn LIKE '%"offerPrice":%' THEN
            SET NEW.OfferPrice = CAST(JSON_UNQUOTE(JSON_EXTRACT(NEW.DescEn, '$.offerPrice')) AS DECIMAL(10,2));
        END IF;
    END IF;
END //

CREATE TRIGGER IF NOT EXISTS trg_offers_before_update BEFORE UPDATE ON Offers FOR EACH ROW
BEGIN
    IF NEW.DescEn LIKE '%"roomId":%' THEN
        SET NEW.RoomId = CAST(JSON_UNQUOTE(JSON_EXTRACT(NEW.DescEn, '$.roomId')) AS SIGNED);
        IF NEW.DescEn LIKE '%"discountPercent":%' THEN
            SET NEW.DiscountPercent = CAST(JSON_UNQUOTE(JSON_EXTRACT(NEW.DescEn, '$.discountPercent')) AS DECIMAL(5,2));
        END IF;
        IF NEW.DescEn LIKE '%"offerPrice":%' THEN
            SET NEW.OfferPrice = CAST(JSON_UNQUOTE(JSON_EXTRACT(NEW.DescEn, '$.offerPrice')) AS DECIMAL(10,2));
        END IF;
    END IF;
END //
DELIMITER ;



