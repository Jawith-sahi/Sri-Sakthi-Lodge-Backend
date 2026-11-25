-- ============================================================================
-- SRI SAKTHI LODGE - ALL ALTER SCRIPTS (MIGRATIONS / PATCHES)
-- Track future schema alterations here
-- ============================================================================

USE sri_sakthi_lodge;

-- Example patch template:
-- ALTER TABLE Bookings ADD COLUMN IF NOT EXISTS SpecialRequests TEXT NULL;

-- ── 2026-11-18: ADD ROOM TYPES TABLE & SEED INITIAL TYPES ─────────────────────
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;

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

-- ── 2026-11-18: ADD SOFT DELETE COLUMNS TO ROOMS ──────────────────────────────
ALTER TABLE Rooms ADD COLUMN IF NOT EXISTS IsDeleted TINYINT(1) NOT NULL DEFAULT 0 AFTER IsActive;
ALTER TABLE Rooms ADD COLUMN IF NOT EXISTS DeletedAt DATETIME NULL AFTER IsDeleted;

-- ── 2026-11-18: TARIFF TAX (GST) CALCULATION SUPPORT ─────────────────────────
-- sp_Rooms_CheckAvailability, sp_Bookings_Create, and sp_Bookings_Create_Admin
-- updated to dynamically calculate TaxAmount = ROUND(Subtotal * (TaxPercent / 100), 2)
-- when TaxPercent > 0, or 0.00 when TaxPercent IS NULL (exempt).

-- ── 2026-11-18: UPI SETTINGS & ROOM IMAGES SEED ─────────────────────────────
INSERT INTO SiteContent (ContentKey, ValueJson, IsDraft, UpdatedAt, UpdatedBy)
VALUES (
    'upi_settings',
    '{"enabled":true,"upiId":"srisakthi@ybl","payeeName":"Sri Sakthi Lodge","qrCodeUrl":"","advancePercent":100,"instructions":"Scan with any UPI app (GPay / PhonePe / Paytm / BHIM) and pay the advance. Enter the UTR reference to confirm."}',
    0,
    NOW(),
    'system'
)
ON DUPLICATE KEY UPDATE UpdatedAt = NOW();

-- ── 2026-11-18: UPDATE ROOM INVENTORY & ACTIVATION ───────────────────
UPDATE Rooms SET IsActive = 1 WHERE Slug IN ('single-bed', 'double-bed', 'standard-ac');
UPDATE Rooms SET TotalUnits = 15 WHERE Slug IN ('double-bed', 'standard-ac');
UPDATE Rooms SET TotalUnits = 5 WHERE Slug = 'single-bed';

-- ── 2026-11-19: MAKE TARIFFS.ENDDATE NULLABLE FOR ONGOING RATES ────────
ALTER TABLE Tariffs MODIFY COLUMN EndDate DATE NULL;

-- ── 2026-11-19: FIX SP_PAYMENTS_VERIFYUPI_ADMIN BALANCEAMOUNT COLUMN ERROR ───
DROP PROCEDURE IF EXISTS sp_Payments_VerifyUpi_Admin;
DELIMITER $$
CREATE PROCEDURE sp_Payments_VerifyUpi_Admin(
    IN p_BookingId INT,
    IN p_PaymentId INT,
    IN p_Approved TINYINT(1),
    IN p_AdminUsername VARCHAR(100) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_Notes VARCHAR(255) COLLATE utf8mb4_uca1400_ai_ci
)
BEGIN
    DECLARE v_Amount DECIMAL(10,2);
    DECLARE v_TotalAmount DECIMAL(10,2);
    DECLARE v_CurrentPaid DECIMAL(10,2);
    DECLARE v_NewPaid DECIMAL(10,2);

    IF p_Approved = 1 THEN
        UPDATE Payments
        SET Status = 'paid', PaidAt = NOW()
        WHERE Id = p_PaymentId AND BookingId = p_BookingId;

        SELECT Amount INTO v_Amount FROM Payments WHERE Id = p_PaymentId;
        IF v_Amount IS NULL THEN SET v_Amount = 0.00; END IF;

        SELECT TotalAmount, IFNULL(AmountPaid, 0.00) INTO v_TotalAmount, v_CurrentPaid
        FROM Bookings WHERE Id = p_BookingId;

        SET v_NewPaid = v_CurrentPaid + v_Amount;

        UPDATE Bookings
        SET AmountPaid = v_NewPaid,
            PaymentStatus = CASE 
                WHEN v_NewPaid >= v_TotalAmount THEN 'paid' 
                WHEN v_NewPaid > 0 THEN 'partial' 
                ELSE 'unpaid' 
            END,
            Status = 'confirmed'
        WHERE Id = p_BookingId;

        INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy)
        VALUES (p_BookingId, 'confirmed', CONCAT(p_AdminUsername, ' (UPI Approved: ', COALESCE(p_Notes, 'OK'), ')'));

        SELECT 1 AS Success, 'Payment approved and booking confirmed' AS Message;
    ELSE
        UPDATE Payments
        SET Status = 'failed'
        WHERE Id = p_PaymentId AND BookingId = p_BookingId;

        UPDATE Bookings
        SET PaymentStatus = 'unpaid'
        WHERE Id = p_BookingId;

        INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy)
        VALUES (p_BookingId, 'pending', CONCAT(p_AdminUsername, ' (UPI Rejected: ', COALESCE(p_Notes, 'Rejected'), ')'));

        SELECT 1 AS Success, 'Payment marked as rejected' AS Message;
    END IF;
END$$
DELIMITER ;

-- 2026-11-19: Payment Status Management & Admin Manual Payment Status Update
DROP PROCEDURE IF EXISTS sp_Bookings_UpdatePaymentStatus_Admin;
DELIMITER $$
CREATE PROCEDURE sp_Bookings_UpdatePaymentStatus_Admin(
    IN p_Id INT,
    IN p_PaymentStatus VARCHAR(20),
    IN p_AmountPaid DECIMAL(10,2),
    IN p_ChangedBy VARCHAR(100),
    IN p_Notes VARCHAR(255)
)
BEGIN
    DECLARE v_Total DECIMAL(10,2);
    DECLARE v_OldStatus VARCHAR(20);
    DECLARE v_OldPaid DECIMAL(10,2);
    DECLARE v_AdvancePercentage DECIMAL(5,2);

    SELECT TotalAmount, PaymentStatus, AmountPaid INTO v_Total, v_OldStatus, v_OldPaid
    FROM Bookings WHERE Id = p_Id;

    IF v_Total IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Booking not found';
    END IF;

    -- If paymentStatus is 'paid', AmountPaid should be full total unless explicitly specified
    IF p_PaymentStatus = 'paid' THEN
        IF p_AmountPaid IS NULL OR p_AmountPaid <= 0 THEN
            SET p_AmountPaid = v_Total;
        END IF;
    ELSEIF p_PaymentStatus = 'unpaid' THEN
        SET p_AmountPaid = 0.00;
    ELSEIF p_PaymentStatus = 'partial' THEN
        IF p_AmountPaid IS NULL OR p_AmountPaid <= 0 THEN
            SELECT CAST(Value AS DECIMAL(5,2)) INTO v_AdvancePercentage 
            FROM Settings WHERE `Key` = 'AdvancePaymentPercentage' LIMIT 1;
            
            IF v_AdvancePercentage IS NULL OR v_AdvancePercentage <= 0 THEN
                SET v_AdvancePercentage = 30.00;
            END IF;
            
            SET p_AmountPaid = ROUND(v_Total * (v_AdvancePercentage / 100.0), 2);
        END IF;
    END IF;

    UPDATE Bookings
    SET PaymentStatus = p_PaymentStatus,
        AmountPaid = p_AmountPaid,
        UpdatedAt = NOW()
    WHERE Id = p_Id;

    -- If money was paid (greater than previously recorded), record in Payments
    IF (p_AmountPaid - v_OldPaid) > 0 THEN
        INSERT INTO Payments (BookingId, Method, Amount, Status, Reference, PaidAt, CreatedAt)
        VALUES (p_Id, 'manual', (p_AmountPaid - v_OldPaid), 'paid', 
                CONCAT('Manual Payment (', CASE WHEN p_PaymentStatus = 'paid' THEN 'Full Payment' ELSE 'Advance Payment' END, ')'), 
                NOW(), NOW());
    END IF;

    -- Record in BookingStatusHistory
    INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy)
    VALUES (p_Id, (SELECT Status FROM Bookings WHERE Id = p_Id), 
            CONCAT(p_ChangedBy, ' [Payment: ', p_PaymentStatus, ', Paid: ₹', p_AmountPaid, 
                   IF(p_Notes IS NOT NULL AND TRIM(p_Notes) != '', CONCAT(' - ', p_Notes), ''), ']'));

    SELECT 1 AS Success, 'Payment status updated successfully' AS Message;
END$$
DELIMITER ;

-- 2026-11-19: Support Idempotency Key in sp_Bookings_Create to prevent Duplicate Key 1062 on retries
DROP PROCEDURE IF EXISTS sp_Bookings_Create;
DELIMITER $$
CREATE PROCEDURE sp_Bookings_Create(
    IN p_RoomId INT,
    IN p_CheckIn DATE,
    IN p_CheckOut DATE,
    IN p_Adults INT,
    IN p_Children INT,
    IN p_Rooms INT,
    IN p_GuestName VARCHAR(200) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_GuestPhone VARCHAR(20) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_GuestEmail VARCHAR(200) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_IdType VARCHAR(50) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_Notes TEXT COLLATE utf8mb4_uca1400_ai_ci,
    IN p_IdempotencyKey VARCHAR(100) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_RoomNumber VARCHAR(100) COLLATE utf8mb4_uca1400_ai_ci
)
proc: BEGIN
    DECLARE v_GuestId INT;
    DECLARE v_BookingId INT;
    DECLARE v_Nights INT;
    DECLARE v_Rate DECIMAL(10,2);
    DECLARE v_TaxPercent DECIMAL(5,2);
    DECLARE v_Subtotal DECIMAL(10,2);
    DECLARE v_TaxAmount DECIMAL(10,2);
    DECLARE v_Total DECIMAL(10,2);
    DECLARE v_Ref VARCHAR(50) COLLATE utf8mb4_uca1400_ai_ci;

    -- Idempotency check: if request was already processed with this key, return the existing booking!
    IF p_IdempotencyKey IS NOT NULL AND TRIM(p_IdempotencyKey) != '' THEN
        SELECT Id INTO v_BookingId FROM Bookings WHERE IdempotencyKey = p_IdempotencyKey LIMIT 1;
        IF v_BookingId IS NOT NULL THEN
            SELECT b.Id, b.Reference, b.Status, b.PaymentStatus, b.Source, b.RoomId, r.NameEn AS RoomName,
                   b.RoomNumber, b.CheckIn, b.CheckOut, b.Nights, b.Adults, b.Children, b.RoomsCount,
                   b.TotalAmount, b.AmountPaid, (b.TotalAmount - b.AmountPaid) AS BalanceAmount,
                   b.Currency, b.Notes, b.CreatedAt,
                   g.Id AS GuestId, g.Name AS GuestName, g.Phone AS GuestPhone, g.Email AS GuestEmail,
                   g.IdType AS GuestIdType, g.Notes AS GuestNotes
            FROM Bookings b
            INNER JOIN Rooms r ON r.Id = b.RoomId
            INNER JOIN Guests g ON g.Id = b.GuestId
            WHERE b.Id = v_BookingId;

            SELECT Id, Method, Amount, Status, Reference, PaidAt FROM Payments WHERE BookingId = v_BookingId;
            SELECT Status, ChangedAt, ChangedBy FROM BookingStatusHistory WHERE BookingId = v_BookingId ORDER BY ChangedAt ASC;
            LEAVE proc;
        END IF;
    END IF;

    -- Calculate nights & total
    SET v_Nights = DATEDIFF(p_CheckOut, p_CheckIn);
    IF v_Nights <= 0 THEN SET v_Nights = 1; END IF;

    SELECT 
        COALESCE(NULLIF((SELECT MIN(t.BaseRate) FROM Tariffs t WHERE t.RoomId = p_RoomId AND t.IsActive = 1), 0), r.StartingPrice, 0.00),
        (SELECT t.TaxPercent FROM Tariffs t WHERE t.RoomId = p_RoomId AND t.IsActive = 1 ORDER BY t.StartDate DESC LIMIT 1)
    INTO v_Rate, v_TaxPercent
    FROM Rooms r
    WHERE r.Id = p_RoomId;

    SET v_Subtotal = v_Rate * v_Nights * p_Rooms;

    IF v_TaxPercent IS NOT NULL AND v_TaxPercent > 0 THEN
        SET v_TaxAmount = ROUND(v_Subtotal * (v_TaxPercent / 100.0), 2);
    ELSE
        SET v_TaxAmount = 0.00;
    END IF;

    SET v_Total = v_Subtotal + v_TaxAmount;

    SELECT Id INTO v_GuestId FROM Guests WHERE Phone = p_GuestPhone LIMIT 1;
    IF v_GuestId IS NULL THEN
        INSERT INTO Guests (Name, Phone, Email, IdType, Notes)
        VALUES (p_GuestName, p_GuestPhone, p_GuestEmail, p_IdType, p_Notes);
        SET v_GuestId = LAST_INSERT_ID();
    END IF;

    SET v_Ref = CONCAT('SSL-', DATE_FORMAT(NOW(), '%Y%m%d'), '-', LPAD(FLOOR(RAND() * 9999), 4, '0'));

    INSERT INTO Bookings (Reference, GuestId, RoomId, RoomNumber, CheckIn, CheckOut, Nights, Adults, Children,
                         RoomsCount, TotalAmount, AmountPaid, Status, PaymentStatus, Source, Notes, IdempotencyKey)
    VALUES (v_Ref, v_GuestId, p_RoomId, p_RoomNumber, p_CheckIn, p_CheckOut, v_Nights, p_Adults, p_Children,
            p_Rooms, v_Total, 0, 'confirmed', 'unpaid', 'online', p_Notes, p_IdempotencyKey);

    SET v_BookingId = LAST_INSERT_ID();

    INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy)
    VALUES (v_BookingId, 'confirmed', 'guest-online');

    SELECT b.Id, b.Reference, b.Status, b.PaymentStatus, b.Source, b.RoomId, r.NameEn AS RoomName,
           b.RoomNumber, b.CheckIn, b.CheckOut, b.Nights, b.Adults, b.Children, b.RoomsCount,
           b.TotalAmount, b.AmountPaid, (b.TotalAmount - b.AmountPaid) AS BalanceAmount,
           b.Currency, b.Notes, b.CreatedAt,
           g.Id AS GuestId, g.Name AS GuestName, g.Phone AS GuestPhone, g.Email AS GuestEmail,
           g.IdType AS GuestIdType, g.Notes AS GuestNotes
    FROM Bookings b
    INNER JOIN Rooms r ON r.Id = b.RoomId
    INNER JOIN Guests g ON g.Id = b.GuestId
    WHERE b.Id = v_BookingId;

    SELECT Id, Method, Amount, Status, Reference, PaidAt FROM Payments WHERE BookingId = v_BookingId;
    SELECT Status, ChangedAt, ChangedBy FROM BookingStatusHistory WHERE BookingId = v_BookingId ORDER BY ChangedAt ASC;
END proc$$
DELIMITER ;

-- 2026-11-19: Support AllowPayAtHotel setting for online booking (constant advance vs optional pay at lodge)
UPDATE sitecontent 
SET ValueJson = JSON_SET(ValueJson, '$.AllowPayAtHotel', true) 
WHERE ContentKey = 'upi_settings' AND JSON_EXTRACT(ValueJson, '$.AllowPayAtHotel') IS NULL;

-- 2026-11-19: Merge Offers into Rooms Module (Room-specific offers & promotions)
ALTER TABLE offers 
  ADD COLUMN IF NOT EXISTS RoomId INT NULL,
  ADD COLUMN IF NOT EXISTS DiscountPercent DECIMAL(5,2) DEFAULT 0.00,
  ADD COLUMN IF NOT EXISTS OfferPrice DECIMAL(10,2) NULL;

DELIMITER //
CREATE TRIGGER IF NOT EXISTS trg_offers_before_insert BEFORE INSERT ON offers FOR EACH ROW
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

CREATE TRIGGER IF NOT EXISTS trg_offers_before_update BEFORE UPDATE ON offers FOR EACH ROW
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

-- ── 2026-11-20: REVIEWS BOOKING REFERENCE & SITE CONTENT CLEANUP ─────────────
ALTER TABLE Reviews ADD COLUMN IF NOT EXISTS BookingReference VARCHAR(50) NULL AFTER Rating;

DELETE FROM SiteContent WHERE ContentKey = '' OR ContentKey IS NULL;
DELETE FROM RoomImages WHERE Url LIKE '/images/rooms/%';

-- ── 2026-11-21: ATTRACTIONS EMPTY SLUG CLEANUP ─────────────
UPDATE Attractions SET Slug = LOWER(TRIM(BOTH '-' FROM REGEXP_REPLACE(TRIM(NameEn), '[^a-zA-Z0-9]+', '-'))) WHERE Slug = '' OR Slug IS NULL;

-- ── 2026-11-21: BOOKINGS DELETE PROCEDURE (RESTRICTED TO CANCELLED OR CHECKED_OUT) ──
DROP PROCEDURE IF EXISTS sp_Bookings_Delete_Admin;
DELIMITER $$
CREATE PROCEDURE sp_Bookings_Delete_Admin(IN p_Id INT)
BEGIN
    DECLARE v_Status VARCHAR(30);
    SELECT Status INTO v_Status FROM Bookings WHERE Id = p_Id;

    IF v_Status IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Booking not found.';
    ELSEIF v_Status != 'cancelled' AND v_Status != 'checked_out' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Booking can only be deleted if status is Cancelled or Checked Out.';
    ELSE
        DELETE FROM Bookings WHERE Id = p_Id;
    END IF;
END$$
DELIMITER ;

-- ── 2026-11-21: BACKEND FILTER MANAGEMENT & SERVER-SIDE PAGINATION ENHANCEMENTS ──
-- 1. Bookings search extended to match payment references (e.g. UPI UTR numbers)
DROP PROCEDURE IF EXISTS sp_Bookings_GetAll_Admin;
DELIMITER $$
CREATE PROCEDURE sp_Bookings_GetAll_Admin(
    IN p_Page INT,
    IN p_PageSize INT,
    IN p_Status VARCHAR(30) DEFAULT NULL,
    IN p_Search VARCHAR(100) DEFAULT NULL,
    IN p_PaymentStatus VARCHAR(30) DEFAULT NULL,
    IN p_CheckInFrom DATE DEFAULT NULL,
    IN p_CheckInTo DATE DEFAULT NULL,
    IN p_SortKey VARCHAR(30) DEFAULT NULL,
    IN p_SortDir VARCHAR(4) DEFAULT NULL
)
BEGIN
    DECLARE v_Offset INT;
    SET v_Offset = (p_Page - 1) * p_PageSize;

    SELECT b.Id, b.Reference, b.RoomNumber, g.Name AS GuestName, g.Phone AS GuestPhone, r.NameEn AS RoomName,
           b.CheckIn, b.CheckOut, b.Nights, b.Status, b.PaymentStatus, b.TotalAmount,
           b.AmountPaid,
           (b.TotalAmount - b.AmountPaid) AS BalanceAmount, b.Source, b.CreatedAt,
           (SELECT p.Reference FROM Payments p WHERE p.BookingId = b.Id AND p.Method = 'upi' ORDER BY p.Id DESC LIMIT 1) AS PaymentRef
    FROM Bookings b
    INNER JOIN Rooms r ON r.Id = b.RoomId
    INNER JOIN Guests g ON g.Id = b.GuestId
    WHERE (p_Status IS NULL OR TRIM(p_Status) = '' OR b.Status = p_Status)
      AND (p_PaymentStatus IS NULL OR TRIM(p_PaymentStatus) = '' OR b.PaymentStatus = p_PaymentStatus)
      AND (p_CheckInFrom IS NULL OR b.CheckIn >= p_CheckInFrom)
      AND (p_CheckInTo IS NULL OR b.CheckIn <= p_CheckInTo)
      AND (p_Search IS NULL OR TRIM(p_Search) = ''
           OR b.Reference LIKE CONCAT('%', p_Search, '%')
           OR b.RoomNumber LIKE CONCAT('%', p_Search, '%')
           OR g.Name LIKE CONCAT('%', p_Search, '%')
           OR g.Phone LIKE CONCAT('%', p_Search, '%')
           OR EXISTS (SELECT 1 FROM Payments p WHERE p.BookingId = b.Id AND p.Reference LIKE CONCAT('%', p_Search, '%')))
    ORDER BY
        CASE WHEN (p_SortKey = 'checkIn' AND (p_SortDir IS NULL OR p_SortDir = 'asc'))  THEN b.CheckIn END ASC,
        CASE WHEN (p_SortKey = 'checkIn' AND p_SortDir = 'desc')                        THEN b.CheckIn END DESC,
        CASE WHEN (p_SortKey = 'createdAt' AND p_SortDir = 'asc')                       THEN b.CreatedAt END ASC,
        CASE WHEN (p_SortKey = 'createdAt' AND (p_SortDir IS NULL OR p_SortDir = 'desc')) THEN b.CreatedAt END DESC,
        b.Id DESC
    LIMIT p_PageSize OFFSET v_Offset;

    SELECT COUNT(*) AS TotalCount
    FROM Bookings b
    INNER JOIN Guests g ON g.Id = b.GuestId
    WHERE (p_Status IS NULL OR TRIM(p_Status) = '' OR b.Status = p_Status)
      AND (p_PaymentStatus IS NULL OR TRIM(p_PaymentStatus) = '' OR b.PaymentStatus = p_PaymentStatus)
      AND (p_CheckInFrom IS NULL OR b.CheckIn >= p_CheckInFrom)
      AND (p_CheckInTo IS NULL OR b.CheckIn <= p_CheckInTo)
      AND (p_Search IS NULL OR TRIM(p_Search) = ''
           OR b.Reference LIKE CONCAT('%', p_Search, '%')
           OR g.Name LIKE CONCAT('%', p_Search, '%')
           OR g.Phone LIKE CONCAT('%', p_Search, '%')
           OR EXISTS (SELECT 1 FROM Payments p WHERE p.BookingId = b.Id AND p.Reference LIKE CONCAT('%', p_Search, '%')));
END$$
DELIMITER ;

-- ── 2026-11-22: PHYSICAL ROOM UNITS & MULTI-ROOM MANDATORY SELECTION ──
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
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_uca1400_ai_ci;

INSERT IGNORE INTO RoomUnits (RoomId, RoomNumber, Floor, IsActive) VALUES
(1, '101', '1st Floor', 1), (1, '102', '1st Floor', 1), (1, '103', '1st Floor', 1), (1, '104', '1st Floor', 1), (1, '105', '1st Floor', 1),
(2, '201', '2nd Floor', 1), (2, '202', '2nd Floor', 1), (2, '203', '2nd Floor', 1), (2, '204', '2nd Floor', 1), (2, '205', '2nd Floor', 1),
(2, '206', '2nd Floor', 1), (2, '207', '2nd Floor', 1), (2, '208', '2nd Floor', 1), (2, '209', '2nd Floor', 1), (2, '210', '2nd Floor', 1),
(2, '211', '2nd Floor', 1), (2, '212', '2nd Floor', 1), (2, '213', '2nd Floor', 1), (2, '214', '2nd Floor', 1), (2, '215', '2nd Floor', 1),
(3, '301', '3rd Floor', 1), (3, '302', '3rd Floor', 1), (3, '303', '3rd Floor', 1), (3, '304', '3rd Floor', 1), (3, '305', '3rd Floor', 1),
(3, '306', '3rd Floor', 1), (3, '307', '3rd Floor', 1), (3, '308', '3rd Floor', 1), (3, '309', '3rd Floor', 1), (3, '310', '3rd Floor', 1),
(3, '311', '3rd Floor', 1), (3, '312', '3rd Floor', 1), (3, '313', '3rd Floor', 1), (3, '314', '3rd Floor', 1), (3, '315', '3rd Floor', 1);

-- ── 2026-11-22: ROOM UNITS ADMIN SYNC & ROOM NUMBER BLOCKING ──────────────────
ALTER TABLE BlockedDates ADD COLUMN IF NOT EXISTS RoomNumber VARCHAR(50) NULL AFTER RoomId;

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_Rooms_GetById$$
CREATE PROCEDURE sp_Rooms_GetById(IN p_Id INT)
BEGIN
    SELECT r.Id, r.Slug, r.NameEn, r.NameTa, r.ShortDescEn, r.ShortDescTa, r.DescriptionEn, r.DescriptionTa,
           r.IsAc, r.MaxOccupancy,
           COALESCE(NULLIF((SELECT MIN(t.BaseRate) FROM Tariffs t WHERE t.RoomId = r.Id AND t.IsActive = 1), 0), r.StartingPrice, 0.00) AS StartingPrice,
           r.Currency, r.TotalUnits, r.IsActive, r.SortOrder
    FROM Rooms r
    WHERE r.Id = p_Id;

    SELECT Id, Url, AltEn, AltTa, SortOrder FROM RoomImages WHERE RoomId = p_Id ORDER BY SortOrder ASC;
    SELECT Id, AmenityKey, LabelEn, LabelTa FROM RoomAmenities WHERE RoomId = p_Id;
    SELECT RoomNumber FROM RoomUnits WHERE RoomId = p_Id ORDER BY CAST(RoomNumber AS UNSIGNED), RoomNumber ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Rooms_Create$$
CREATE PROCEDURE sp_Rooms_Create(
    IN p_Slug VARCHAR(100) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_NameEn VARCHAR(200) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_NameTa VARCHAR(200) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_ShortDescEn VARCHAR(500) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_ShortDescTa VARCHAR(500) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_DescriptionEn TEXT COLLATE utf8mb4_uca1400_ai_ci,
    IN p_DescriptionTa TEXT COLLATE utf8mb4_uca1400_ai_ci,
    IN p_IsAc TINYINT(1),
    IN p_MaxOccupancy INT,
    IN p_StartingPrice DECIMAL(10,2),
    IN p_TotalUnits INT,
    IN p_IsActive TINYINT(1),
    IN p_SortOrder INT,
    IN p_AmenitiesJson JSON,
    IN p_RoomNumbersJson JSON
)
BEGIN
    DECLARE v_RoomId INT;
    DECLARE v_UnitsCount INT;

    INSERT INTO Rooms (Slug, NameEn, NameTa, ShortDescEn, ShortDescTa, DescriptionEn, DescriptionTa,
                       IsAc, MaxOccupancy, StartingPrice, TotalUnits, IsActive, SortOrder)
    VALUES (p_Slug, p_NameEn, p_NameTa, p_ShortDescEn, p_ShortDescTa, p_DescriptionEn, p_DescriptionTa,
            p_IsAc, p_MaxOccupancy, p_StartingPrice, p_TotalUnits, p_IsActive, p_SortOrder);

    SET v_RoomId = LAST_INSERT_ID();

    -- Insert RoomUnits if p_RoomNumbersJson is provided
    IF p_RoomNumbersJson IS NOT NULL AND JSON_VALID(p_RoomNumbersJson) AND JSON_LENGTH(p_RoomNumbersJson) > 0 THEN
        INSERT IGNORE INTO RoomUnits (RoomId, RoomNumber, Floor, IsActive)
        SELECT v_RoomId, 
               CAST(jt.RoomNumber AS CHAR(50)) COLLATE utf8mb4_uca1400_ai_ci,
               CASE 
                   WHEN jt.RoomNumber LIKE '1%' THEN '1st Floor'
                   WHEN jt.RoomNumber LIKE '2%' THEN '2nd Floor'
                   WHEN jt.RoomNumber LIKE '3%' THEN '3rd Floor'
                   ELSE 'Ground Floor'
               END, 1
        FROM JSON_TABLE(p_RoomNumbersJson, '$[*]' COLUMNS (RoomNumber VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_ci PATH '$')) AS jt
        WHERE jt.RoomNumber IS NOT NULL AND TRIM(jt.RoomNumber) != '';

        SELECT COUNT(*) INTO v_UnitsCount FROM RoomUnits WHERE RoomId = v_RoomId;
        IF v_UnitsCount > 0 AND p_TotalUnits < 999 THEN
            UPDATE Rooms SET TotalUnits = v_UnitsCount WHERE Id = v_RoomId;
        END IF;
    END IF;

    SELECT v_RoomId AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_Rooms_Update$$
CREATE PROCEDURE sp_Rooms_Update(
    IN p_Id INT,
    IN p_Slug VARCHAR(100) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_NameEn VARCHAR(200) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_NameTa VARCHAR(200) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_ShortDescEn VARCHAR(500) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_ShortDescTa VARCHAR(500) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_DescriptionEn TEXT COLLATE utf8mb4_uca1400_ai_ci,
    IN p_DescriptionTa TEXT COLLATE utf8mb4_uca1400_ai_ci,
    IN p_IsAc TINYINT(1),
    IN p_MaxOccupancy INT,
    IN p_StartingPrice DECIMAL(10,2),
    IN p_TotalUnits INT,
    IN p_IsActive TINYINT(1),
    IN p_SortOrder INT,
    IN p_AmenitiesJson JSON,
    IN p_RoomNumbersJson JSON
)
BEGIN
    DECLARE v_UnitsCount INT;

    UPDATE Rooms
    SET Slug = p_Slug, NameEn = p_NameEn, NameTa = p_NameTa,
        ShortDescEn = p_ShortDescEn, ShortDescTa = p_ShortDescTa,
        DescriptionEn = p_DescriptionEn, DescriptionTa = p_DescriptionTa,
        IsAc = p_IsAc, MaxOccupancy = p_MaxOccupancy, StartingPrice = p_StartingPrice,
        TotalUnits = p_TotalUnits, IsActive = p_IsActive, SortOrder = p_SortOrder
    WHERE Id = p_Id;

    -- Sync RoomUnits if p_RoomNumbersJson is provided
    IF p_RoomNumbersJson IS NOT NULL AND JSON_VALID(p_RoomNumbersJson) AND JSON_LENGTH(p_RoomNumbersJson) > 0 THEN
        -- Remove units not in the updated list
        DELETE FROM RoomUnits 
        WHERE RoomId = p_Id 
          AND RoomNumber NOT IN (
              SELECT CAST(jt.RoomNumber AS CHAR(50)) COLLATE utf8mb4_uca1400_ai_ci
              FROM JSON_TABLE(p_RoomNumbersJson, '$[*]' COLUMNS (RoomNumber VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_ci PATH '$')) AS jt
          );

        -- Insert newly added units
        INSERT IGNORE INTO RoomUnits (RoomId, RoomNumber, Floor, IsActive)
        SELECT p_Id, 
               CAST(jt.RoomNumber AS CHAR(50)) COLLATE utf8mb4_uca1400_ai_ci,
               CASE 
                   WHEN jt.RoomNumber LIKE '1%' THEN '1st Floor'
                   WHEN jt.RoomNumber LIKE '2%' THEN '2nd Floor'
                   WHEN jt.RoomNumber LIKE '3%' THEN '3rd Floor'
                   ELSE 'Ground Floor'
               END, 1
        FROM JSON_TABLE(p_RoomNumbersJson, '$[*]' COLUMNS (RoomNumber VARCHAR(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_uca1400_ai_ci PATH '$')) AS jt
        WHERE jt.RoomNumber IS NOT NULL AND TRIM(jt.RoomNumber) != '';

        -- Sync TotalUnits count with actual RoomUnits count
        SELECT COUNT(*) INTO v_UnitsCount FROM RoomUnits WHERE RoomId = p_Id;
        IF v_UnitsCount > 0 AND p_TotalUnits < 999 THEN
            UPDATE Rooms SET TotalUnits = v_UnitsCount WHERE Id = p_Id;
        END IF;
    END IF;
END$$

DROP PROCEDURE IF EXISTS sp_BlockedDates_Get$$
CREATE PROCEDURE sp_BlockedDates_Get(IN p_RoomId INT)
BEGIN
    SELECT Id, RoomId, RoomNumber, FromDate, ToDate, Reason 
    FROM BlockedDates 
    WHERE RoomId = p_RoomId 
    ORDER BY FromDate ASC;
END$$

DROP PROCEDURE IF EXISTS sp_BlockedDates_Add$$
CREATE PROCEDURE sp_BlockedDates_Add(
    IN p_RoomId INT,
    IN p_FromDate DATE,
    IN p_ToDate DATE,
    IN p_Reason VARCHAR(200) COLLATE utf8mb4_uca1400_ai_ci,
    IN p_RoomNumber VARCHAR(50) COLLATE utf8mb4_uca1400_ai_ci
)
BEGIN
    INSERT INTO BlockedDates (RoomId, RoomNumber, FromDate, ToDate, Reason)
    VALUES (p_RoomId, NULLIF(TRIM(p_RoomNumber), ''), p_FromDate, p_ToDate, p_Reason);

    SELECT LAST_INSERT_ID() AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_BlockedDates_Remove$$
CREATE PROCEDURE sp_BlockedDates_Remove(IN p_BlockId INT)
BEGIN
    DELETE FROM BlockedDates WHERE Id = p_BlockId;
END$$

DROP PROCEDURE IF EXISTS sp_Rooms_CheckAvailability$$
CREATE PROCEDURE sp_Rooms_CheckAvailability(
    IN p_CheckIn DATE,
    IN p_CheckOut DATE,
    IN p_Adults INT,
    IN p_Children INT,
    IN p_Rooms INT
)
BEGIN
    SELECT 
        r.Id,
        r.Slug,
        r.NameEn,
        r.NameTa,
        r.ShortDescEn,
        r.ShortDescTa,
        r.IsAc,
        r.MaxOccupancy,
        (SELECT ri.Url FROM RoomImages ri WHERE ri.RoomId = r.Id ORDER BY ri.SortOrder ASC LIMIT 1) AS ImageUrl,
        COALESCE(NULLIF((SELECT MIN(t.BaseRate) FROM Tariffs t WHERE t.RoomId = r.Id AND t.IsActive = 1), 0), r.StartingPrice, 0.00) AS PricePerNight,
        (SELECT t.TaxPercent FROM Tariffs t WHERE t.RoomId = r.Id AND t.IsActive = 1 ORDER BY t.StartDate DESC LIMIT 1) AS TaxPercent,
        r.Currency,
        (CASE 
            WHEN (SELECT COUNT(*) FROM RoomUnits u WHERE u.RoomId = r.Id AND u.IsActive = 1) > 0 THEN
                (SELECT COUNT(*) FROM RoomUnits u 
                 WHERE u.RoomId = r.Id AND u.IsActive = 1
                   -- Not booked
                   AND NOT EXISTS (
                       SELECT 1 FROM Bookings b
                       WHERE b.RoomId = r.Id
                         AND b.Status NOT IN ('cancelled', 'checked_out')
                         AND NOT (b.CheckOut <= p_CheckIn OR b.CheckIn >= p_CheckOut)
                         AND (
                             b.RoomNumber = u.RoomNumber
                             OR FIND_IN_SET(u.RoomNumber, REPLACE(b.RoomNumber, ' ', '')) > 0
                             OR b.RoomNumber LIKE CONCAT('%,', u.RoomNumber, ',%')
                             OR b.RoomNumber LIKE CONCAT(u.RoomNumber, ',%')
                             OR b.RoomNumber LIKE CONCAT('%,', u.RoomNumber)
                         )
                   )
                   -- Not blocked for this specific room number or category-wide
                   AND NOT EXISTS (
                       SELECT 1 FROM BlockedDates bd
                       WHERE bd.RoomId = r.Id
                         AND (bd.RoomNumber IS NULL OR bd.RoomNumber = '' OR bd.RoomNumber = u.RoomNumber)
                         AND NOT (bd.ToDate <= p_CheckIn OR bd.FromDate >= p_CheckOut)
                   )
                )
            WHEN r.TotalUnits >= 999 THEN 999 
            ELSE GREATEST(0, (r.TotalUnits - IFNULL(b.BookedCount, 0))) 
        END) AS AvailableUnits,
        (SELECT GROUP_CONCAT(u.RoomNumber ORDER BY CAST(u.RoomNumber AS UNSIGNED), u.RoomNumber SEPARATOR ',') 
         FROM RoomUnits u 
         WHERE u.RoomId = r.Id AND u.IsActive = 1) AS AllRoomNumbers,
        (SELECT GROUP_CONCAT(u.RoomNumber ORDER BY CAST(u.RoomNumber AS UNSIGNED), u.RoomNumber SEPARATOR ',') 
         FROM RoomUnits u 
         WHERE u.RoomId = r.Id AND u.IsActive = 1
           -- Not booked
           AND NOT EXISTS (
               SELECT 1 FROM Bookings b
               WHERE b.RoomId = r.Id
                 AND b.Status NOT IN ('cancelled', 'checked_out')
                 AND NOT (b.CheckOut <= p_CheckIn OR b.CheckIn >= p_CheckOut)
                 AND (
                     b.RoomNumber = u.RoomNumber
                     OR FIND_IN_SET(u.RoomNumber, REPLACE(b.RoomNumber, ' ', '')) > 0
                     OR b.RoomNumber LIKE CONCAT('%,', u.RoomNumber, ',%')
                     OR b.RoomNumber LIKE CONCAT(u.RoomNumber, ',%')
                     OR b.RoomNumber LIKE CONCAT('%,', u.RoomNumber)
                 )
           )
           -- Not blocked for this specific room number or category-wide
           AND NOT EXISTS (
               SELECT 1 FROM BlockedDates bd
               WHERE bd.RoomId = r.Id
                 AND (bd.RoomNumber IS NULL OR bd.RoomNumber = '' OR bd.RoomNumber = u.RoomNumber)
                 AND NOT (bd.ToDate <= p_CheckIn OR bd.FromDate >= p_CheckOut)
           )
        ) AS AvailableRoomNumbers
    FROM Rooms r
    LEFT JOIN (
        SELECT RoomId, SUM(RoomsCount) AS BookedCount
        FROM Bookings
        WHERE Status NOT IN ('cancelled', 'checked_out')
          AND NOT (CheckOut <= p_CheckIn OR CheckIn >= p_CheckOut)
        GROUP BY RoomId
    ) b ON r.Id = b.RoomId
    WHERE r.IsActive = 1
      AND r.IsDeleted = 0
      -- Do not return if entire category is blocked
      AND NOT EXISTS (
          SELECT 1 FROM BlockedDates bd 
          WHERE bd.RoomId = r.Id 
            AND (bd.RoomNumber IS NULL OR bd.RoomNumber = '')
            AND NOT (bd.ToDate <= p_CheckIn OR bd.FromDate >= p_CheckOut)
      )
      AND (
          r.TotalUnits >= 999 
          OR (r.TotalUnits - IFNULL(b.BookedCount, 0)) >= p_Rooms
          OR (
              SELECT COUNT(*) FROM RoomUnits u 
              WHERE u.RoomId = r.Id AND u.IsActive = 1
                AND NOT EXISTS (
                    SELECT 1 FROM Bookings b
                    WHERE b.RoomId = r.Id
                      AND b.Status NOT IN ('cancelled', 'checked_out')
                      AND NOT (b.CheckOut <= p_CheckIn OR b.CheckIn >= p_CheckOut)
                      AND (
                          b.RoomNumber = u.RoomNumber
                          OR FIND_IN_SET(u.RoomNumber, REPLACE(b.RoomNumber, ' ', '')) > 0
                          OR b.RoomNumber LIKE CONCAT('%,', u.RoomNumber, ',%')
                          OR b.RoomNumber LIKE CONCAT(u.RoomNumber, ',%')
                          OR b.RoomNumber LIKE CONCAT('%,', u.RoomNumber)
                      )
                )
                AND NOT EXISTS (
                    SELECT 1 FROM BlockedDates bd
                    WHERE bd.RoomId = r.Id
                      AND (bd.RoomNumber IS NULL OR bd.RoomNumber = '' OR bd.RoomNumber = u.RoomNumber)
                      AND NOT (bd.ToDate <= p_CheckIn OR bd.FromDate >= p_CheckOut)
                )
          ) >= p_Rooms
      )
    ORDER BY r.SortOrder ASC, r.Id ASC;
END$$

DELIMITER ;




