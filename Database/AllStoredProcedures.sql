-- ============================================================================
-- SRI SAKTHI LODGE STORED PROCEDURES (MySQL)
-- ============================================================================

USE sri_sakthi_lodge;

DELIMITER $$

-- ── AUTH ────────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Auth_Login$$
CREATE PROCEDURE sp_Auth_Login(IN p_Username VARCHAR(100))
BEGIN
    SELECT Id, Username, Name, PasswordHash, Role, IsActive 
    FROM Users 
    WHERE Username = p_Username AND IsActive = 1;
END$$

DROP PROCEDURE IF EXISTS sp_Staff_GetById$$
CREATE PROCEDURE sp_Staff_GetById(IN p_Id INT)
BEGIN
    SELECT Id, Username, Name, Role, IsActive 
    FROM Users 
    WHERE Id = p_Id;
END$$

DROP PROCEDURE IF EXISTS sp_Auth_CreateRefreshToken$$
CREATE PROCEDURE sp_Auth_CreateRefreshToken(
    IN p_UserId INT,
    IN p_TokenHash VARCHAR(500),
    IN p_IpAddress VARCHAR(50),
    IN p_SlidingExpiry DATETIME,
    IN p_AbsoluteExpiry DATETIME
)
BEGIN
    INSERT INTO RefreshTokens (UserId, TokenHash, IpAddress, SlidingExpiry, AbsoluteExpiry)
    VALUES (p_UserId, p_TokenHash, p_IpAddress, p_SlidingExpiry, p_AbsoluteExpiry);
END$$

DROP PROCEDURE IF EXISTS sp_Auth_ValidateRefreshToken$$
CREATE PROCEDURE sp_Auth_ValidateRefreshToken(IN p_TokenHash VARCHAR(500))
BEGIN
    SELECT Id, UserId, IsRevoked, SlidingExpiry, AbsoluteExpiry 
    FROM RefreshTokens 
    WHERE TokenHash = p_TokenHash;
END$$

DROP PROCEDURE IF EXISTS sp_Auth_RotateRefreshToken$$
CREATE PROCEDURE sp_Auth_RotateRefreshToken(
    IN p_OldId INT,
    IN p_OldHash VARCHAR(500),
    IN p_NewHash VARCHAR(500),
    IN p_NewSlidingExpiry DATETIME,
    IN p_NewAbsoluteExpiry DATETIME
)
BEGIN
    DECLARE v_UserId INT;
    SELECT UserId INTO v_UserId FROM RefreshTokens WHERE Id = p_OldId;

    -- Revoke old
    UPDATE RefreshTokens 
    SET IsRevoked = 1, RevokedAt = NOW(), RevokedReason = 'rotated', ReplacedByHash = p_NewHash 
    WHERE Id = p_OldId;

    -- Insert new
    INSERT INTO RefreshTokens (UserId, TokenHash, SlidingExpiry, AbsoluteExpiry)
    VALUES (v_UserId, p_NewHash, p_NewSlidingExpiry, p_NewAbsoluteExpiry);
END$$

DROP PROCEDURE IF EXISTS sp_Auth_RevokeToken$$
CREATE PROCEDURE sp_Auth_RevokeToken(IN p_TokenHash VARCHAR(500), IN p_Reason VARCHAR(50))
BEGIN
    UPDATE RefreshTokens 
    SET IsRevoked = 1, RevokedAt = NOW(), RevokedReason = p_Reason 
    WHERE TokenHash = p_TokenHash;
END$$

DROP PROCEDURE IF EXISTS sp_Auth_GetPasswordHash$$
CREATE PROCEDURE sp_Auth_GetPasswordHash(IN p_UserId INT)
BEGIN
    SELECT PasswordHash FROM Users WHERE Id = p_UserId;
END$$

DROP PROCEDURE IF EXISTS sp_Auth_ChangePassword$$
CREATE PROCEDURE sp_Auth_ChangePassword(IN p_UserId INT, IN p_NewPasswordHash VARCHAR(500))
BEGIN
    UPDATE Users SET PasswordHash = p_NewPasswordHash, UpdatedAt = NOW() WHERE Id = p_UserId;
END$$

-- ── ROOMS ───────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Rooms_GetAll_Public$$
CREATE PROCEDURE sp_Rooms_GetAll_Public()
BEGIN
    -- 1. Rooms
    SELECT r.Id, r.Slug, r.NameEn, r.NameTa, r.ShortDescEn, r.ShortDescTa, r.DescriptionEn, r.DescriptionTa,
           r.IsAc, r.MaxOccupancy,
           COALESCE(NULLIF((SELECT MIN(t.BaseRate) FROM Tariffs t WHERE t.RoomId = r.Id AND t.IsActive = 1), 0), r.StartingPrice, 0.00) AS StartingPrice,
           r.Currency, r.TotalUnits, r.IsActive, r.SortOrder
    FROM Rooms r
    WHERE r.IsActive = 1 AND r.IsDeleted = 0
    ORDER BY r.SortOrder ASC, r.Id ASC;

    -- 2. Images
    SELECT ri.Id, ri.RoomId, ri.Url, ri.AltEn, ri.AltTa, ri.SortOrder
    FROM RoomImages ri
    INNER JOIN Rooms r ON r.Id = ri.RoomId
    WHERE r.IsActive = 1 AND r.IsDeleted = 0
    ORDER BY ri.SortOrder ASC;

    -- 3. Amenities
    SELECT ra.Id, ra.RoomId, ra.AmenityKey, ra.LabelEn, ra.LabelTa
    FROM RoomAmenities ra
    INNER JOIN Rooms r ON r.Id = ra.RoomId
    WHERE r.IsActive = 1 AND r.IsDeleted = 0;
END$$

DROP PROCEDURE IF EXISTS sp_Rooms_GetBySlug$$
CREATE PROCEDURE sp_Rooms_GetBySlug(IN p_Slug VARCHAR(100))
BEGIN
    DECLARE v_RoomId INT;

    SELECT r.Id, r.Slug, r.NameEn, r.NameTa, r.ShortDescEn, r.ShortDescTa, r.DescriptionEn, r.DescriptionTa,
           r.IsAc, r.MaxOccupancy,
           COALESCE(NULLIF((SELECT MIN(t.BaseRate) FROM Tariffs t WHERE t.RoomId = r.Id AND t.IsActive = 1), 0), r.StartingPrice, 0.00) AS StartingPrice,
           r.Currency, r.TotalUnits, r.IsActive, r.SortOrder
    FROM Rooms r
    WHERE r.Slug = p_Slug AND r.IsActive = 1 AND r.IsDeleted = 0;

    SELECT Id INTO v_RoomId FROM Rooms WHERE Slug = p_Slug AND IsActive = 1 AND IsDeleted = 0 LIMIT 1;

    IF v_RoomId IS NOT NULL THEN
        SELECT Id, Url, AltEn, AltTa, SortOrder FROM RoomImages WHERE RoomId = v_RoomId ORDER BY SortOrder ASC;
        SELECT Id, AmenityKey, LabelEn, LabelTa FROM RoomAmenities WHERE RoomId = v_RoomId;
    END IF;
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
                   AND NOT EXISTS (
                       SELECT 1 FROM Bookings b
                       WHERE b.RoomId = r.Id
                         AND LOWER(TRIM(b.Status)) NOT IN ('cancelled', 'checked_out', 'checkout', 'cancel', 'canceled')
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
                         AND bd.FromDate < p_CheckOut AND bd.ToDate > p_CheckIn
                   )
                )
            WHEN r.TotalUnits >= 999 THEN 999 
            ELSE GREATEST(0, (r.TotalUnits - IFNULL(b.BookedCount, 0) - (
                SELECT COUNT(*) FROM BlockedDates bd 
                WHERE bd.RoomId = r.Id 
                  AND (bd.RoomNumber IS NULL OR bd.RoomNumber = '')
                  AND bd.FromDate < p_CheckOut AND bd.ToDate > p_CheckIn
            ))) 
        END) AS AvailableUnits,
        (SELECT GROUP_CONCAT(u.RoomNumber ORDER BY CAST(u.RoomNumber AS UNSIGNED), u.RoomNumber SEPARATOR ',') 
         FROM RoomUnits u 
         WHERE u.RoomId = r.Id AND u.IsActive = 1) AS AllRoomNumbers,
        (SELECT GROUP_CONCAT(u.RoomNumber ORDER BY CAST(u.RoomNumber AS UNSIGNED), u.RoomNumber SEPARATOR ',') 
         FROM RoomUnits u 
         WHERE u.RoomId = r.Id AND u.IsActive = 1
           AND NOT EXISTS (
               SELECT 1 FROM Bookings b
               WHERE b.RoomId = r.Id
                 AND LOWER(TRIM(b.Status)) NOT IN ('cancelled', 'checked_out', 'checkout', 'cancel', 'canceled')
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
                 AND bd.FromDate < p_CheckOut AND bd.ToDate > p_CheckIn
           )
        ) AS AvailableRoomNumbers,
        (SELECT GROUP_CONCAT(DISTINCT u.RoomNumber ORDER BY CAST(u.RoomNumber AS UNSIGNED), u.RoomNumber SEPARATOR ',') 
         FROM RoomUnits u 
         WHERE u.RoomId = r.Id AND u.IsActive = 1
           AND EXISTS (
               SELECT 1 FROM Bookings b
               WHERE b.RoomId = r.Id
                 AND LOWER(TRIM(b.Status)) NOT IN ('cancelled', 'checked_out', 'checkout', 'cancel', 'canceled')
                 AND NOT (b.CheckOut <= p_CheckIn OR b.CheckIn >= p_CheckOut)
                 AND (
                     b.RoomNumber = u.RoomNumber
                     OR FIND_IN_SET(u.RoomNumber, REPLACE(b.RoomNumber, ' ', '')) > 0
                     OR b.RoomNumber LIKE CONCAT('%,', u.RoomNumber, ',%')
                     OR b.RoomNumber LIKE CONCAT(u.RoomNumber, ',%')
                     OR b.RoomNumber LIKE CONCAT('%,', u.RoomNumber)
                 )
           )
        ) AS BookedRoomNumbers,
        (SELECT GROUP_CONCAT(DISTINCT u.RoomNumber ORDER BY CAST(u.RoomNumber AS UNSIGNED), u.RoomNumber SEPARATOR ',') 
         FROM RoomUnits u 
         WHERE u.RoomId = r.Id AND u.IsActive = 1
           AND EXISTS (
               SELECT 1 FROM BlockedDates bd
               WHERE bd.RoomId = r.Id
                 AND (bd.RoomNumber IS NULL OR bd.RoomNumber = '' OR bd.RoomNumber = u.RoomNumber)
                 AND bd.FromDate < p_CheckOut AND bd.ToDate > p_CheckIn
           )
        ) AS BlockedRoomNumbers
    FROM Rooms r
    LEFT JOIN (
        SELECT RoomId, SUM(RoomsCount) AS BookedCount
        FROM Bookings
        WHERE LOWER(TRIM(Status)) NOT IN ('cancelled', 'checked_out', 'checkout', 'cancel', 'canceled')
          AND NOT (CheckOut <= p_CheckIn OR CheckIn >= p_CheckOut)
        GROUP BY RoomId
    ) b ON r.Id = b.RoomId
    WHERE r.IsActive = 1
      AND r.IsDeleted = 0
      AND NOT EXISTS (
          SELECT 1 FROM BlockedDates bd 
          WHERE bd.RoomId = r.Id 
            AND (bd.RoomNumber IS NULL OR bd.RoomNumber = '')
            AND bd.FromDate < p_CheckOut AND bd.ToDate > p_CheckIn
      )
      AND (
          p_Rooms <= 0 OR
          CASE 
              WHEN (SELECT COUNT(*) FROM RoomUnits u WHERE u.RoomId = r.Id AND u.IsActive = 1) > 0 THEN
                  (SELECT COUNT(*) FROM RoomUnits u 
                   WHERE u.RoomId = r.Id AND u.IsActive = 1
                     AND NOT EXISTS (
                         SELECT 1 FROM Bookings b
                         WHERE b.RoomId = r.Id
                           AND LOWER(TRIM(b.Status)) NOT IN ('cancelled', 'checked_out', 'checkout', 'cancel', 'canceled')
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
                           AND bd.FromDate < p_CheckOut AND bd.ToDate > p_CheckIn
                     )
                  ) >= p_Rooms
              WHEN r.TotalUnits >= 999 THEN TRUE
              ELSE (r.TotalUnits - IFNULL(b.BookedCount, 0)) >= p_Rooms
          END
      )
    ORDER BY r.SortOrder ASC, r.Id ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Rooms_GetAll_Admin$$
CREATE PROCEDURE sp_Rooms_GetAll_Admin()
BEGIN
    SELECT r.Id, r.Slug, r.NameEn, r.NameTa, r.ShortDescEn, r.ShortDescTa, r.DescriptionEn, r.DescriptionTa,
           r.IsAc, r.MaxOccupancy,
           COALESCE(NULLIF((SELECT MIN(t.BaseRate) FROM Tariffs t WHERE t.RoomId = r.Id AND t.IsActive = 1), 0), r.StartingPrice, 0.00) AS StartingPrice,
           r.Currency, r.TotalUnits, r.IsActive, r.SortOrder
    FROM Rooms r
    WHERE r.IsDeleted = 0
    ORDER BY r.SortOrder ASC, r.Id ASC;
END$$

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
    IN p_Slug VARCHAR(100),
    IN p_NameEn VARCHAR(200),
    IN p_NameTa VARCHAR(200),
    IN p_ShortDescEn VARCHAR(500),
    IN p_ShortDescTa VARCHAR(500),
    IN p_DescriptionEn TEXT,
    IN p_DescriptionTa TEXT,
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
               CAST(jt.RoomNumber AS CHAR(50)),
               CASE 
                   WHEN jt.RoomNumber LIKE '1%' THEN '1st Floor'
                   WHEN jt.RoomNumber LIKE '2%' THEN '2nd Floor'
                   WHEN jt.RoomNumber LIKE '3%' THEN '3rd Floor'
                   ELSE 'Ground Floor'
               END, 1
        FROM JSON_TABLE(p_RoomNumbersJson, '$[*]' COLUMNS (RoomNumber VARCHAR(50) CHARACTER SET utf8mb4 PATH '$')) AS jt
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
    IN p_Slug VARCHAR(100),
    IN p_NameEn VARCHAR(200),
    IN p_NameTa VARCHAR(200),
    IN p_ShortDescEn VARCHAR(500),
    IN p_ShortDescTa VARCHAR(500),
    IN p_DescriptionEn TEXT,
    IN p_DescriptionTa TEXT,
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
              SELECT CAST(jt.RoomNumber AS CHAR(50))
              FROM JSON_TABLE(p_RoomNumbersJson, '$[*]' COLUMNS (RoomNumber VARCHAR(50) CHARACTER SET utf8mb4 PATH '$')) AS jt
          );

        -- Insert newly added units
        INSERT IGNORE INTO RoomUnits (RoomId, RoomNumber, Floor, IsActive)
        SELECT p_Id, 
               CAST(jt.RoomNumber AS CHAR(50)),
               CASE 
                   WHEN jt.RoomNumber LIKE '1%' THEN '1st Floor'
                   WHEN jt.RoomNumber LIKE '2%' THEN '2nd Floor'
                   WHEN jt.RoomNumber LIKE '3%' THEN '3rd Floor'
                   ELSE 'Ground Floor'
               END, 1
        FROM JSON_TABLE(p_RoomNumbersJson, '$[*]' COLUMNS (RoomNumber VARCHAR(50) CHARACTER SET utf8mb4 PATH '$')) AS jt
        WHERE jt.RoomNumber IS NOT NULL AND TRIM(jt.RoomNumber) != '';

        -- Sync TotalUnits count with actual RoomUnits count
        SELECT COUNT(*) INTO v_UnitsCount FROM RoomUnits WHERE RoomId = p_Id;
        IF v_UnitsCount > 0 AND p_TotalUnits < 999 THEN
            UPDATE Rooms SET TotalUnits = v_UnitsCount WHERE Id = p_Id;
        END IF;
    END IF;
END$$

DROP PROCEDURE IF EXISTS sp_Rooms_SetActive$$
CREATE PROCEDURE sp_Rooms_SetActive(IN p_Id INT, IN p_IsActive TINYINT(1))
BEGIN
    UPDATE Rooms SET IsActive = p_IsActive WHERE Id = p_Id;
END$$

DROP PROCEDURE IF EXISTS sp_Rooms_Delete$$
CREATE PROCEDURE sp_Rooms_Delete(IN p_Id INT)
BEGIN
    UPDATE Rooms 
    SET IsDeleted = 1, 
        IsActive = 0, 
        DeletedAt = NOW() 
    WHERE Id = p_Id;
END$$

DROP PROCEDURE IF EXISTS sp_RoomImages_Add$$
CREATE PROCEDURE sp_RoomImages_Add(IN p_RoomId INT, IN p_Url VARCHAR(500), IN p_AltEn VARCHAR(300), IN p_AltTa VARCHAR(300))
BEGIN
    INSERT INTO RoomImages (RoomId, Url, AltEn, AltTa) VALUES (p_RoomId, p_Url, p_AltEn, p_AltTa);
    SELECT LAST_INSERT_ID() AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_RoomImages_Delete$$
CREATE PROCEDURE sp_RoomImages_Delete(IN p_ImageId INT)
BEGIN
    DELETE FROM RoomImages WHERE Id = p_ImageId;
END$$

DROP PROCEDURE IF EXISTS sp_RoomImages_Reorder$$
CREATE PROCEDURE sp_RoomImages_Reorder(IN p_RoomId INT, IN p_ItemsJson JSON)
BEGIN
    -- Items updated via app layer loop or simple statement
    SELECT 1;
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
    IN p_Reason VARCHAR(200),
    IN p_RoomNumber VARCHAR(50)
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

-- ── BOOKINGS ────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Bookings_Create$$
CREATE PROCEDURE sp_Bookings_Create(
    IN p_RoomId INT,
    IN p_CheckIn DATE,
    IN p_CheckOut DATE,
    IN p_Adults INT,
    IN p_Children INT,
    IN p_Rooms INT,
    IN p_GuestName VARCHAR(200),
    IN p_GuestPhone VARCHAR(20),
    IN p_GuestEmail VARCHAR(200),
    IN p_IdType VARCHAR(50),
    IN p_Notes TEXT,
    IN p_IdempotencyKey VARCHAR(100),
    IN p_RoomNumber VARCHAR(100)
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
    DECLARE v_Ref VARCHAR(50);
    DECLARE v_BlockedCount INT DEFAULT 0;
    DECLARE v_ConflictCount INT DEFAULT 0;

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

            SELECT Id, Method, Amount, Status, Reference, COALESCE(PaidAt, CreatedAt) AS PaidAt FROM Payments WHERE BookingId = v_BookingId;
            SELECT Status, ChangedAt, ChangedBy FROM BookingStatusHistory WHERE BookingId = v_BookingId ORDER BY ChangedAt ASC;
            LEAVE proc;
        END IF;
    END IF;

    -- Room Unit collision / block validation
    IF p_RoomNumber IS NOT NULL AND TRIM(p_RoomNumber) != '' THEN
        SELECT COUNT(*) INTO v_BlockedCount
        FROM BlockedDates bd
        WHERE bd.RoomId = p_RoomId
          AND (
              bd.RoomNumber IS NULL 
              OR bd.RoomNumber = '' 
              OR FIND_IN_SET(bd.RoomNumber, REPLACE(p_RoomNumber, ' ', '')) > 0
          )
          AND bd.FromDate < p_CheckOut AND bd.ToDate > p_CheckIn;

        IF v_BlockedCount > 0 THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'One or more selected room numbers are blocked for maintenance on these dates.';
        END IF;

        SELECT COUNT(*) INTO v_ConflictCount
        FROM Bookings b
        WHERE b.RoomId = p_RoomId
          AND LOWER(TRIM(b.Status)) NOT IN ('cancelled', 'checked_out', 'checkout', 'cancel', 'canceled')
          AND NOT (b.CheckOut <= p_CheckIn OR b.CheckIn >= p_CheckOut)
          AND b.RoomNumber IS NOT NULL
          AND EXISTS (
              SELECT 1 FROM RoomUnits u
              WHERE u.RoomId = p_RoomId
                AND FIND_IN_SET(u.RoomNumber, REPLACE(p_RoomNumber, ' ', '')) > 0
                AND (
                    b.RoomNumber = u.RoomNumber
                    OR FIND_IN_SET(u.RoomNumber, REPLACE(b.RoomNumber, ' ', '')) > 0
                )
          );

        IF v_ConflictCount > 0 THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'One or more selected room numbers are already booked on these dates.';
        END IF;
    ELSE
        SELECT COUNT(*) INTO v_BlockedCount
        FROM BlockedDates
        WHERE RoomId = p_RoomId
          AND (RoomNumber IS NULL OR RoomNumber = '')
          AND FromDate < p_CheckOut AND ToDate > p_CheckIn;

        IF v_BlockedCount > 0 THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'This room category is blocked for maintenance on the selected dates.';
        END IF;
    END IF;

    -- Calculate nights & total
    SET v_Nights = DATEDIFF(p_CheckOut, p_CheckIn);
    IF v_Nights <= 0 THEN SET v_Nights = 1; END IF;

    -- Get rate & tax percent from active tariff (fallback to room starting price)
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

    -- Find or create guest
    SELECT Id INTO v_GuestId FROM Guests WHERE Phone = p_GuestPhone LIMIT 1;
    IF v_GuestId IS NULL THEN
        INSERT INTO Guests (Name, Phone, Email, IdType, Notes)
        VALUES (p_GuestName, p_GuestPhone, p_GuestEmail, p_IdType, p_Notes);
        SET v_GuestId = LAST_INSERT_ID();
    END IF;

    -- Generate Reference
    SET v_Ref = CONCAT('SSL-', DATE_FORMAT(NOW(), '%Y%m%d'), '-', LPAD(FLOOR(RAND() * 9999), 4, '0'));

    INSERT INTO Bookings (Reference, GuestId, RoomId, RoomNumber, CheckIn, CheckOut, Nights, Adults, Children,
                         RoomsCount, TotalAmount, AmountPaid, Status, PaymentStatus, Source, Notes, IdempotencyKey)
    VALUES (v_Ref, v_GuestId, p_RoomId, p_RoomNumber, p_CheckIn, p_CheckOut, v_Nights, p_Adults, p_Children,
            p_Rooms, v_Total, 0, 'confirmed', 'unpaid', 'online', p_Notes, p_IdempotencyKey);

    SET v_BookingId = LAST_INSERT_ID();

    INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy)
    VALUES (v_BookingId, 'confirmed', 'guest-online');

    -- Return full booking
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

    SELECT Id, Method, Amount, Status, Reference, COALESCE(PaidAt, CreatedAt) AS PaidAt FROM Payments WHERE BookingId = v_BookingId;
    SELECT Status, ChangedAt, ChangedBy FROM BookingStatusHistory WHERE BookingId = v_BookingId ORDER BY ChangedAt ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Bookings_GetByReference_Public$$
CREATE PROCEDURE sp_Bookings_GetByReference_Public(IN p_Reference VARCHAR(50), IN p_Token VARCHAR(100))
BEGIN
    SELECT b.Id, b.Reference, b.Status, b.PaymentStatus, b.Source, b.RoomId, r.NameEn AS RoomName,
           b.RoomNumber, b.CheckIn, b.CheckOut, b.Nights, b.Adults, b.Children, b.RoomsCount,
           b.TotalAmount, b.AmountPaid, (b.TotalAmount - b.AmountPaid) AS BalanceAmount,
           b.Currency, b.Notes, b.CreatedAt,
           g.Id AS GuestId, g.Name AS GuestName, g.Phone AS GuestPhone, g.Email AS GuestEmail,
           g.IdType AS GuestIdType, g.Notes AS GuestNotes
    FROM Bookings b
    INNER JOIN Rooms r ON r.Id = b.RoomId
    INNER JOIN Guests g ON g.Id = b.GuestId
    WHERE b.Reference = p_Reference;

    SELECT p.Id, p.Method, p.Amount, p.Status, p.Reference, COALESCE(p.PaidAt, p.CreatedAt) AS PaidAt 
    FROM Payments p
    INNER JOIN Bookings b ON b.Id = p.BookingId
    WHERE b.Reference = p_Reference;

    SELECT h.Status, h.ChangedAt, h.ChangedBy 
    FROM BookingStatusHistory h
    INNER JOIN Bookings b ON b.Id = h.BookingId
    WHERE b.Reference = p_Reference 
    ORDER BY h.ChangedAt ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Bookings_Lookup$$
CREATE PROCEDURE sp_Bookings_Lookup(IN p_Reference VARCHAR(50), IN p_Phone VARCHAR(20))
BEGIN
    SELECT b.Id, b.Reference, b.Status, b.PaymentStatus, b.Source, b.RoomId, r.NameEn AS RoomName,
           b.RoomNumber, b.CheckIn, b.CheckOut, b.Nights, b.Adults, b.Children, b.RoomsCount,
           b.TotalAmount, b.AmountPaid, (b.TotalAmount - b.AmountPaid) AS BalanceAmount,
           b.Currency, b.Notes, b.CreatedAt,
           g.Id AS GuestId, g.Name AS GuestName, g.Phone AS GuestPhone, g.Email AS GuestEmail,
           g.IdType AS GuestIdType, g.Notes AS GuestNotes
    FROM Bookings b
    INNER JOIN Rooms r ON r.Id = b.RoomId
    INNER JOIN Guests g ON g.Id = b.GuestId
    WHERE b.Reference = p_Reference AND g.Phone = p_Phone;

    SELECT p.Id, p.Method, p.Amount, p.Status, p.Reference, COALESCE(p.PaidAt, p.CreatedAt) AS PaidAt 
    FROM Payments p
    INNER JOIN Bookings b ON b.Id = p.BookingId
    INNER JOIN Guests g ON g.Id = b.GuestId
    WHERE b.Reference = p_Reference AND g.Phone = p_Phone;

    SELECT h.Status, h.ChangedAt, h.ChangedBy 
    FROM BookingStatusHistory h
    INNER JOIN Bookings b ON b.Id = h.BookingId
    INNER JOIN Guests g ON g.Id = b.GuestId
    WHERE b.Reference = p_Reference AND g.Phone = p_Phone
    ORDER BY h.ChangedAt ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Bookings_Cancel_Public$$
CREATE PROCEDURE sp_Bookings_Cancel_Public(IN p_Reference VARCHAR(50), IN p_Reason VARCHAR(500))
BEGIN
    DECLARE v_Id INT;
    SELECT Id INTO v_Id FROM Bookings WHERE Reference = p_Reference;

    IF v_Id IS NOT NULL THEN
        UPDATE Bookings 
        SET Status = 'cancelled', CancelledAt = NOW(), CancellationReason = p_Reason 
        WHERE Id = v_Id;

        INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy) 
        VALUES (v_Id, 'cancelled', 'guest');
    END IF;
END$$

DROP PROCEDURE IF EXISTS sp_Bookings_GetAll_Admin$$
CREATE PROCEDURE sp_Bookings_GetAll_Admin(
    IN p_Page INT,
    IN p_PageSize INT,
    IN p_Status VARCHAR(30),
    IN p_Search VARCHAR(100),
    IN p_PaymentStatus VARCHAR(30),
    IN p_CheckInFrom DATE,
    IN p_CheckInTo DATE,
    IN p_SortKey VARCHAR(30),
    IN p_SortDir VARCHAR(4)
)
BEGIN
    DECLARE v_Offset INT;
    SET v_Offset = (p_Page - 1) * p_PageSize;

    SELECT b.Id, b.Reference, g.Name AS GuestName, g.Phone AS GuestPhone, r.NameEn AS RoomName,
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

DROP PROCEDURE IF EXISTS sp_Bookings_GetById_Admin$$
CREATE PROCEDURE sp_Bookings_GetById_Admin(IN p_Id INT)
BEGIN
    SELECT b.Id, b.Reference, b.Status, b.PaymentStatus, b.Source, b.RoomId, r.NameEn AS RoomName,
           b.RoomNumber, b.CheckIn, b.CheckOut, b.Nights, b.Adults, b.Children, b.RoomsCount,
           b.TotalAmount, b.AmountPaid, (b.TotalAmount - b.AmountPaid) AS BalanceAmount,
           b.Currency, b.Notes, b.CreatedAt,
           g.Id AS GuestId, g.Name AS GuestName, g.Phone AS GuestPhone, g.Email AS GuestEmail,
           g.IdType AS GuestIdType, g.Notes AS GuestNotes
    FROM Bookings b
    INNER JOIN Rooms r ON r.Id = b.RoomId
    INNER JOIN Guests g ON g.Id = b.GuestId
    WHERE b.Id = p_Id;

    SELECT Id, Method, Amount, Status, Reference, COALESCE(PaidAt, CreatedAt) AS PaidAt FROM Payments WHERE BookingId = p_Id;
    SELECT Status, ChangedAt, ChangedBy FROM BookingStatusHistory WHERE BookingId = p_Id ORDER BY ChangedAt ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Bookings_Create_Admin$$
CREATE PROCEDURE sp_Bookings_Create_Admin(
    IN p_RoomId INT,
    IN p_CheckIn DATE,
    IN p_CheckOut DATE,
    IN p_Adults INT,
    IN p_Children INT,
    IN p_Rooms INT,
    IN p_GuestName VARCHAR(200),
    IN p_GuestPhone VARCHAR(20),
    IN p_GuestEmail VARCHAR(200),
    IN p_IdType VARCHAR(50),
    IN p_Notes TEXT,
    IN p_StaffUserId INT,
    IN p_StaffUsername VARCHAR(100),
    IN p_RoomNumber VARCHAR(100)
)
BEGIN
    DECLARE v_GuestId INT;
    DECLARE v_BookingId INT;
    DECLARE v_Nights INT;
    DECLARE v_Rate DECIMAL(10,2);
    DECLARE v_TaxPercent DECIMAL(5,2);
    DECLARE v_Subtotal DECIMAL(10,2);
    DECLARE v_TaxAmount DECIMAL(10,2);
    DECLARE v_Total DECIMAL(10,2);
    DECLARE v_Ref VARCHAR(50);
    DECLARE v_BlockedCount INT DEFAULT 0;
    DECLARE v_ConflictCount INT DEFAULT 0;

    -- Room Unit collision / block validation
    IF p_RoomNumber IS NOT NULL AND TRIM(p_RoomNumber) != '' THEN
        SELECT COUNT(*) INTO v_BlockedCount
        FROM BlockedDates bd
        WHERE bd.RoomId = p_RoomId
          AND (
              bd.RoomNumber IS NULL 
              OR bd.RoomNumber = '' 
              OR FIND_IN_SET(bd.RoomNumber, REPLACE(p_RoomNumber, ' ', '')) > 0
          )
          AND bd.FromDate < p_CheckOut AND bd.ToDate > p_CheckIn;

        IF v_BlockedCount > 0 THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'One or more selected room numbers are blocked for maintenance on these dates.';
        END IF;

        SELECT COUNT(*) INTO v_ConflictCount
        FROM Bookings b
        WHERE b.RoomId = p_RoomId
          AND LOWER(TRIM(b.Status)) NOT IN ('cancelled', 'checked_out', 'checkout', 'cancel', 'canceled')
          AND NOT (b.CheckOut <= p_CheckIn OR b.CheckIn >= p_CheckOut)
          AND b.RoomNumber IS NOT NULL
          AND EXISTS (
              SELECT 1 FROM RoomUnits u
              WHERE u.RoomId = p_RoomId
                AND FIND_IN_SET(u.RoomNumber, REPLACE(p_RoomNumber, ' ', '')) > 0
                AND (
                    b.RoomNumber = u.RoomNumber
                    OR FIND_IN_SET(u.RoomNumber, REPLACE(b.RoomNumber, ' ', '')) > 0
                )
          );

        IF v_ConflictCount > 0 THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'One or more selected room numbers are already booked on these dates.';
        END IF;
    ELSE
        SELECT COUNT(*) INTO v_BlockedCount
        FROM BlockedDates
        WHERE RoomId = p_RoomId
          AND (RoomNumber IS NULL OR RoomNumber = '')
          AND FromDate < p_CheckOut AND ToDate > p_CheckIn;

        IF v_BlockedCount > 0 THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'This room category is blocked for maintenance on the selected dates.';
        END IF;
    END IF;

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
                         RoomsCount, TotalAmount, AmountPaid, Status, PaymentStatus, Source, Notes)
    VALUES (v_Ref, v_GuestId, p_RoomId, p_RoomNumber, p_CheckIn, p_CheckOut, v_Nights, p_Adults, p_Children,
            p_Rooms, v_Total, 0, 'confirmed', 'unpaid', 'walk-in', p_Notes);

    SET v_BookingId = LAST_INSERT_ID();

    INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy)
    VALUES (v_BookingId, 'confirmed', p_StaffUsername);

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

    SELECT Id, Method, Amount, Status, Reference, COALESCE(PaidAt, CreatedAt) AS PaidAt FROM Payments WHERE BookingId = v_BookingId;
    SELECT Status, ChangedAt, ChangedBy FROM BookingStatusHistory WHERE BookingId = v_BookingId ORDER BY ChangedAt ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Bookings_UpdateStatus$$
CREATE PROCEDURE sp_Bookings_UpdateStatus(
    IN p_Id INT,
    IN p_Status VARCHAR(30),
    IN p_Notes TEXT,
    IN p_RoomNumber VARCHAR(50),
    IN p_ChangedBy VARCHAR(100)
)
BEGIN
    DECLARE v_PaymentStatus VARCHAR(20);

    -- Checkout validation: must be fully paid
    IF p_Status = 'checked_out' THEN
        SELECT PaymentStatus INTO v_PaymentStatus FROM Bookings WHERE Id = p_Id;
        IF v_PaymentStatus != 'paid' THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Checkout cannot be completed: Booking is not fully paid.';
        END IF;
    END IF;

    UPDATE Bookings 
    SET Status = p_Status, 
        Notes = IFNULL(p_Notes, Notes),
        RoomNumber = IFNULL(p_RoomNumber, RoomNumber),
        UpdatedAt = NOW()
    WHERE Id = p_Id;

    INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy)
    VALUES (p_Id, p_Status, p_ChangedBy);
END$$

DROP PROCEDURE IF EXISTS sp_Bookings_Cancel_Admin$$
CREATE PROCEDURE sp_Bookings_Cancel_Admin(IN p_Id INT, IN p_Reason VARCHAR(500), IN p_ChangedBy VARCHAR(100))
BEGIN
    UPDATE Bookings 
    SET Status = 'cancelled', CancelledAt = NOW(), CancellationReason = p_Reason, UpdatedAt = NOW()
    WHERE Id = p_Id;

    INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy)
    VALUES (p_Id, 'cancelled', p_ChangedBy);
END$$

DROP PROCEDURE IF EXISTS sp_Bookings_Delete_Admin$$
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

-- ── PAYMENTS ────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Payments_CreateOrder$$
CREATE PROCEDURE sp_Payments_CreateOrder(IN p_BookingId INT, IN p_RazorpayOrderId VARCHAR(100), IN p_Amount DECIMAL(10,2))
BEGIN
    INSERT INTO Payments (BookingId, Method, Amount, Status, RazorpayOrderId)
    VALUES (p_BookingId, 'online', p_Amount, 'pending', p_RazorpayOrderId);
END$$

DROP PROCEDURE IF EXISTS sp_Payments_Verify$$
CREATE PROCEDURE sp_Payments_Verify(IN p_RazorpayOrderId VARCHAR(100), IN p_RazorpayPaymentId VARCHAR(100), IN p_BookingReference VARCHAR(50))
BEGIN
    DECLARE v_PaymentId INT;
    DECLARE v_BookingId INT;
    DECLARE v_Amount DECIMAL(10,2);

    SELECT Id, BookingId, Amount INTO v_PaymentId, v_BookingId, v_Amount
    FROM Payments
    WHERE RazorpayOrderId = p_RazorpayOrderId
    LIMIT 1;

    IF v_PaymentId IS NOT NULL THEN
        UPDATE Payments 
        SET Status = 'paid', RazorpayPaymentId = p_RazorpayPaymentId, PaidAt = NOW()
        WHERE Id = v_PaymentId;

        UPDATE Bookings 
        SET AmountPaid = AmountPaid + v_Amount,
            PaymentStatus = CASE WHEN (AmountPaid + v_Amount) >= TotalAmount THEN 'paid' ELSE 'partial' END
        WHERE Id = v_BookingId;
    END IF;
END$$

DROP PROCEDURE IF EXISTS sp_Payments_GetStatus$$
CREATE PROCEDURE sp_Payments_GetStatus(IN p_BookingReference VARCHAR(50))
BEGIN
    SELECT b.PaymentStatus, b.AmountPaid, b.TotalAmount
    FROM Bookings b
    WHERE b.Reference = p_BookingReference;
END$$

-- ── GUESTS ──────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Guests_GetAll_Admin$$
CREATE PROCEDURE sp_Guests_GetAll_Admin(IN p_Page INT, IN p_PageSize INT, IN p_Search VARCHAR(100))
BEGIN
    DECLARE v_Offset INT;
    SET v_Offset = (p_Page - 1) * p_PageSize;

    SELECT g.Id, g.Name, g.Phone, g.Email, g.IdType, g.Notes, g.CreatedAt,
           COUNT(b.Id) AS TotalBookings
    FROM Guests g
    LEFT JOIN Bookings b ON b.GuestId = g.Id
    WHERE (p_Search IS NULL OR g.Name LIKE CONCAT('%', p_Search, '%') OR g.Phone LIKE CONCAT('%', p_Search, '%'))
    GROUP BY g.Id
    ORDER BY g.Id DESC
    LIMIT p_PageSize OFFSET v_Offset;

    SELECT COUNT(*) AS TotalCount FROM Guests g
    WHERE (p_Search IS NULL OR g.Name LIKE CONCAT('%', p_Search, '%') OR g.Phone LIKE CONCAT('%', p_Search, '%'));
END$$

DROP PROCEDURE IF EXISTS sp_Guests_GetById_Admin$$
CREATE PROCEDURE sp_Guests_GetById_Admin(IN p_Id INT)
BEGIN
    SELECT g.Id, g.Name, g.Phone, g.Email, g.IdType, g.Notes, g.CreatedAt,
           COUNT(b.Id) AS TotalBookings
    FROM Guests g
    LEFT JOIN Bookings b ON b.GuestId = g.Id
    WHERE g.Id = p_Id
    GROUP BY g.Id;
END$$

-- ── TARIFFS ─────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Tariffs_GetAll$$
CREATE PROCEDURE sp_Tariffs_GetAll()
BEGIN
    SELECT t.Id, t.RoomId, r.NameEn AS RoomName, t.PlanName, t.BaseRate, t.StartDate, t.EndDate, t.Season, t.TaxPercent, t.IsActive
    FROM Tariffs t
    INNER JOIN Rooms r ON r.Id = t.RoomId
    ORDER BY t.StartDate DESC;
END$$

DROP PROCEDURE IF EXISTS sp_Tariffs_Create$$
CREATE PROCEDURE sp_Tariffs_Create(
    IN p_RoomId INT, IN p_PlanName VARCHAR(100), IN p_BaseRate DECIMAL(10,2),
    IN p_StartDate DATE, IN p_EndDate DATE, IN p_Season VARCHAR(50),
    IN p_TaxPercent DECIMAL(5,2), IN p_IsActive TINYINT(1)
)
BEGIN
    INSERT INTO Tariffs (RoomId, PlanName, BaseRate, StartDate, EndDate, Season, TaxPercent, IsActive)
    VALUES (p_RoomId, p_PlanName, p_BaseRate, p_StartDate, p_EndDate, p_Season, p_TaxPercent, p_IsActive);

    UPDATE Rooms
    SET StartingPrice = (
        SELECT COALESCE(MIN(BaseRate), p_BaseRate)
        FROM Tariffs
        WHERE RoomId = p_RoomId AND IsActive = 1
    )
    WHERE Id = p_RoomId;

    SELECT LAST_INSERT_ID() AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_Tariffs_Update$$
CREATE PROCEDURE sp_Tariffs_Update(
    IN p_Id INT, IN p_RoomId INT, IN p_PlanName VARCHAR(100), IN p_BaseRate DECIMAL(10,2),
    IN p_StartDate DATE, IN p_EndDate DATE, IN p_Season VARCHAR(50),
    IN p_TaxPercent DECIMAL(5,2), IN p_IsActive TINYINT(1)
)
BEGIN
    UPDATE Tariffs 
    SET RoomId = p_RoomId, PlanName = p_PlanName, BaseRate = p_BaseRate,
        StartDate = p_StartDate, EndDate = p_EndDate, Season = p_Season,
        TaxPercent = p_TaxPercent, IsActive = p_IsActive
    WHERE Id = p_Id;

    UPDATE Rooms
    SET StartingPrice = COALESCE(
        (SELECT MIN(BaseRate) FROM Tariffs WHERE RoomId = p_RoomId AND IsActive = 1),
        StartingPrice
    )
    WHERE Id = p_RoomId;
END$$

DROP PROCEDURE IF EXISTS sp_Tariffs_Delete$$
CREATE PROCEDURE sp_Tariffs_Delete(IN p_Id INT)
BEGIN
    DECLARE v_RoomId INT;
    SELECT RoomId INTO v_RoomId FROM Tariffs WHERE Id = p_Id LIMIT 1;

    DELETE FROM Tariffs WHERE Id = p_Id;

    IF v_RoomId IS NOT NULL THEN
        UPDATE Rooms
        SET StartingPrice = COALESCE(
            (SELECT MIN(BaseRate) FROM Tariffs WHERE RoomId = v_RoomId AND IsActive = 1),
            0.00
        )
        WHERE Id = v_RoomId;
    END IF;
END$$

-- ── OFFERS ──────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Offers_GetAll_Public$$
CREATE PROCEDURE sp_Offers_GetAll_Public()
BEGIN
    SELECT Id, TitleEn, TitleTa, DescEn, DescTa, ImageUrl, StartDate, EndDate, TermsEn, TermsTa, IsPublished, SortOrder
    FROM Offers
    WHERE IsPublished = 1 AND EndDate >= CURDATE()
    ORDER BY SortOrder ASC, StartDate DESC;
END$$

DROP PROCEDURE IF EXISTS sp_Offers_GetAll_Admin$$
CREATE PROCEDURE sp_Offers_GetAll_Admin()
BEGIN
    SELECT Id, TitleEn, TitleTa, DescEn, DescTa, ImageUrl, StartDate, EndDate, TermsEn, TermsTa, IsPublished, SortOrder
    FROM Offers
    ORDER BY SortOrder ASC, StartDate DESC;
END$$

DROP PROCEDURE IF EXISTS sp_Offers_Create$$
CREATE PROCEDURE sp_Offers_Create(
    IN p_TitleEn VARCHAR(300), IN p_TitleTa VARCHAR(300), IN p_DescEn TEXT, IN p_DescTa TEXT,
    IN p_ImageUrl VARCHAR(500), IN p_StartDate DATE, IN p_EndDate DATE,
    IN p_TermsEn TEXT, IN p_TermsTa TEXT, IN p_IsPublished TINYINT(1), IN p_SortOrder INT
)
BEGIN
    INSERT INTO Offers (TitleEn, TitleTa, DescEn, DescTa, ImageUrl, StartDate, EndDate, TermsEn, TermsTa, IsPublished, SortOrder)
    VALUES (p_TitleEn, p_TitleTa, p_DescEn, p_DescTa, p_ImageUrl, p_StartDate, p_EndDate, p_TermsEn, p_TermsTa, p_IsPublished, p_SortOrder);
    SELECT LAST_INSERT_ID() AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_Offers_Update$$
CREATE PROCEDURE sp_Offers_Update(
    IN p_Id INT, IN p_TitleEn VARCHAR(300), IN p_TitleTa VARCHAR(300), IN p_DescEn TEXT, IN p_DescTa TEXT,
    IN p_ImageUrl VARCHAR(500), IN p_StartDate DATE, IN p_EndDate DATE,
    IN p_TermsEn TEXT, IN p_TermsTa TEXT, IN p_IsPublished TINYINT(1), IN p_SortOrder INT
)
BEGIN
    UPDATE Offers
    SET TitleEn = p_TitleEn, TitleTa = p_TitleTa, DescEn = p_DescEn, DescTa = p_DescTa,
        ImageUrl = p_ImageUrl, StartDate = p_StartDate, EndDate = p_EndDate,
        TermsEn = p_TermsEn, TermsTa = p_TermsTa, IsPublished = p_IsPublished, SortOrder = p_SortOrder
    WHERE Id = p_Id;
END$$

DROP PROCEDURE IF EXISTS sp_Offers_Delete$$
CREATE PROCEDURE sp_Offers_Delete(IN p_Id INT)
BEGIN
    DELETE FROM Offers WHERE Id = p_Id;
END$$

-- ── GALLERY ─────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Gallery_GetAll_Public$$
CREATE PROCEDURE sp_Gallery_GetAll_Public()
BEGIN
    SELECT Id, Type, Url, TitleEn, TitleTa, AltEn, AltTa, Category, SortOrder, IsPublished
    FROM Gallery
    WHERE IsPublished = 1
    ORDER BY SortOrder ASC, Id ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Gallery_GetAll_Admin$$
CREATE PROCEDURE sp_Gallery_GetAll_Admin()
BEGIN
    SELECT Id, Type, Url, TitleEn, TitleTa, AltEn, AltTa, Category, SortOrder, IsPublished
    FROM Gallery
    ORDER BY SortOrder ASC, Id ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Gallery_Create$$
CREATE PROCEDURE sp_Gallery_Create(
    IN p_Type VARCHAR(10), IN p_Url VARCHAR(500), IN p_TitleEn VARCHAR(300), IN p_TitleTa VARCHAR(300),
    IN p_AltEn VARCHAR(300), IN p_AltTa VARCHAR(300), IN p_Category VARCHAR(50),
    IN p_SortOrder INT, IN p_IsPublished TINYINT(1)
)
BEGIN
    INSERT INTO Gallery (Type, Url, TitleEn, TitleTa, AltEn, AltTa, Category, SortOrder, IsPublished)
    VALUES (p_Type, p_Url, p_TitleEn, p_TitleTa, p_AltEn, p_AltTa, p_Category, p_SortOrder, p_IsPublished);
    SELECT LAST_INSERT_ID() AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_Gallery_Update$$
CREATE PROCEDURE sp_Gallery_Update(
    IN p_Id INT, IN p_Type VARCHAR(10), IN p_Url VARCHAR(500), IN p_TitleEn VARCHAR(300), IN p_TitleTa VARCHAR(300),
    IN p_AltEn VARCHAR(300), IN p_AltTa VARCHAR(300), IN p_Category VARCHAR(50),
    IN p_SortOrder INT, IN p_IsPublished TINYINT(1)
)
BEGIN
    UPDATE Gallery
    SET Type = p_Type, Url = p_Url, TitleEn = p_TitleEn, TitleTa = p_TitleTa,
        AltEn = p_AltEn, AltTa = p_AltTa, Category = p_Category,
        SortOrder = p_SortOrder, IsPublished = p_IsPublished
    WHERE Id = p_Id;
END$$

DROP PROCEDURE IF EXISTS sp_Gallery_Delete$$
CREATE PROCEDURE sp_Gallery_Delete(IN p_Id INT)
BEGIN
    DELETE FROM Gallery WHERE Id = p_Id;
END$$

-- ── REVIEWS ─────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Reviews_GetAll_Public$$
CREATE PROCEDURE sp_Reviews_GetAll_Public()
BEGIN
    SELECT Id, Name, Rating, BookingReference, ReviewText, Status, CreatedAt
    FROM Reviews
    WHERE Status = 'approved'
    ORDER BY CreatedAt DESC;
END$$

DROP PROCEDURE IF EXISTS sp_Reviews_Submit$$
CREATE PROCEDURE sp_Reviews_Submit(
    IN p_Name VARCHAR(200),
    IN p_Rating TINYINT,
    IN p_ReviewText TEXT,
    IN p_BookingReference VARCHAR(50)
)
BEGIN
    INSERT INTO Reviews (Name, Rating, BookingReference, ReviewText, Status)
    VALUES (p_Name, p_Rating, NULLIF(TRIM(p_BookingReference), ''), p_ReviewText, 'pending');
    SELECT LAST_INSERT_ID() AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_Reviews_GetAll_Admin$$
CREATE PROCEDURE sp_Reviews_GetAll_Admin()
BEGIN
    SELECT Id, Name, Rating, BookingReference, ReviewText, Status, CreatedAt
    FROM Reviews
    ORDER BY CreatedAt DESC;
END$$

DROP PROCEDURE IF EXISTS sp_Reviews_Moderate$$
CREATE PROCEDURE sp_Reviews_Moderate(IN p_Id INT, IN p_Status VARCHAR(20), IN p_ModeratedBy VARCHAR(100))
BEGIN
    UPDATE Reviews 
    SET Status = p_Status, ModeratedAt = NOW(), ModeratedBy = p_ModeratedBy 
    WHERE Id = p_Id;
END$$

-- ── ATTRACTIONS ─────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Attractions_GetAll_Public$$
CREATE PROCEDURE sp_Attractions_GetAll_Public()
BEGIN
    SELECT Id, Slug, NameEn, NameTa, DescEn, DescTa, Category, ImageUrl, MapsUrl, IsPublished, SortOrder
    FROM Attractions
    WHERE IsPublished = 1
    ORDER BY SortOrder ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Attractions_GetBySlug$$
CREATE PROCEDURE sp_Attractions_GetBySlug(IN p_Slug VARCHAR(100))
BEGIN
    SELECT Id, Slug, NameEn, NameTa, DescEn, DescTa, Category, ImageUrl, MapsUrl, IsPublished, SortOrder
    FROM Attractions
    WHERE Slug = p_Slug AND IsPublished = 1;
END$$

DROP PROCEDURE IF EXISTS sp_Attractions_GetAll_Admin$$
CREATE PROCEDURE sp_Attractions_GetAll_Admin()
BEGIN
    SELECT Id, Slug, NameEn, NameTa, DescEn, DescTa, Category, ImageUrl, MapsUrl, IsPublished, SortOrder
    FROM Attractions
    ORDER BY SortOrder ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Attractions_Create$$
CREATE PROCEDURE sp_Attractions_Create(
    IN p_Slug VARCHAR(100), IN p_NameEn VARCHAR(300), IN p_NameTa VARCHAR(300),
    IN p_DescEn TEXT, IN p_DescTa TEXT, IN p_Category VARCHAR(50),
    IN p_ImageUrl VARCHAR(500), IN p_MapsUrl VARCHAR(500),
    IN p_IsPublished TINYINT(1), IN p_SortOrder INT
)
BEGIN
    DECLARE v_Slug VARCHAR(100);
    SET v_Slug = TRIM(p_Slug);
    IF v_Slug IS NULL OR v_Slug = '' THEN
        SET v_Slug = LOWER(REGEXP_REPLACE(TRIM(p_NameEn), '[^a-zA-Z0-9]+', '-'));
        SET v_Slug = TRIM(BOTH '-' FROM v_Slug);
        IF v_Slug IS NULL OR v_Slug = '' THEN
            SET v_Slug = CONCAT('attraction-', UUID_SHORT());
        END IF;
    END IF;

    INSERT INTO Attractions (Slug, NameEn, NameTa, DescEn, DescTa, Category, ImageUrl, MapsUrl, IsPublished, SortOrder)
    VALUES (v_Slug, p_NameEn, p_NameTa, p_DescEn, p_DescTa, p_Category, p_ImageUrl, p_MapsUrl, p_IsPublished, p_SortOrder);
    SELECT LAST_INSERT_ID() AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_Attractions_Update$$
CREATE PROCEDURE sp_Attractions_Update(
    IN p_Id INT, IN p_Slug VARCHAR(100), IN p_NameEn VARCHAR(300), IN p_NameTa VARCHAR(300),
    IN p_DescEn TEXT, IN p_DescTa TEXT, IN p_Category VARCHAR(50),
    IN p_ImageUrl VARCHAR(500), IN p_MapsUrl VARCHAR(500),
    IN p_IsPublished TINYINT(1), IN p_SortOrder INT
)
BEGIN
    DECLARE v_Slug VARCHAR(100);
    SET v_Slug = TRIM(p_Slug);
    IF v_Slug IS NULL OR v_Slug = '' THEN
        SET v_Slug = LOWER(REGEXP_REPLACE(TRIM(p_NameEn), '[^a-zA-Z0-9]+', '-'));
        SET v_Slug = TRIM(BOTH '-' FROM v_Slug);
        IF v_Slug IS NULL OR v_Slug = '' THEN
            SET v_Slug = CONCAT('attraction-', p_Id);
        END IF;
    END IF;

    UPDATE Attractions
    SET Slug = v_Slug, NameEn = p_NameEn, NameTa = p_NameTa,
        DescEn = p_DescEn, DescTa = p_DescTa, Category = p_Category,
        ImageUrl = p_ImageUrl, MapsUrl = p_MapsUrl, IsPublished = p_IsPublished, SortOrder = p_SortOrder
    WHERE Id = p_Id;
END$$

DROP PROCEDURE IF EXISTS sp_Attractions_Delete$$
CREATE PROCEDURE sp_Attractions_Delete(IN p_Id INT)
BEGIN
    DELETE FROM Attractions WHERE Id = p_Id;
END$$

-- ── ENQUIRIES ───────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Enquiries_Submit$$
CREATE PROCEDURE sp_Enquiries_Submit(
    IN p_Name VARCHAR(200), IN p_Phone VARCHAR(20), IN p_Email VARCHAR(200),
    IN p_Subject VARCHAR(300), IN p_Message TEXT
)
BEGIN
    INSERT INTO Enquiries (Name, Phone, Email, Subject, Message, Status)
    VALUES (p_Name, p_Phone, p_Email, p_Subject, p_Message, 'new');
    SELECT LAST_INSERT_ID() AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_Enquiries_GetAll_Admin$$
CREATE PROCEDURE sp_Enquiries_GetAll_Admin(IN p_Page INT, IN p_PageSize INT, IN p_Status VARCHAR(20))
BEGIN
    DECLARE v_Offset INT;
    SET v_Offset = (p_Page - 1) * p_PageSize;

    SELECT Id, Name, Phone, Email, Subject, Message, Status, Notes, CreatedAt
    FROM Enquiries
    WHERE (p_Status IS NULL OR Status = p_Status)
    ORDER BY Id DESC
    LIMIT p_PageSize OFFSET v_Offset;

    SELECT COUNT(*) AS TotalCount FROM Enquiries WHERE (p_Status IS NULL OR Status = p_Status);
END$$

DROP PROCEDURE IF EXISTS sp_Enquiries_GetById_Admin$$
CREATE PROCEDURE sp_Enquiries_GetById_Admin(IN p_Id INT)
BEGIN
    SELECT Id, Name, Phone, Email, Subject, Message, Status, Notes, CreatedAt FROM Enquiries WHERE Id = p_Id;
END$$

DROP PROCEDURE IF EXISTS sp_Enquiries_Update$$
CREATE PROCEDURE sp_Enquiries_Update(IN p_Id INT, IN p_Status VARCHAR(20), IN p_Notes TEXT)
BEGIN
    UPDATE Enquiries 
    SET Status = IFNULL(p_Status, Status), Notes = IFNULL(p_Notes, Notes), UpdatedAt = NOW() 
    WHERE Id = p_Id;
END$$

-- ── CONTENT ─────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Content_Get_Public$$
CREATE PROCEDURE sp_Content_Get_Public()
BEGIN
    SELECT ValueJson FROM SiteContent WHERE ContentKey = 'main' LIMIT 1;
END$$

DROP PROCEDURE IF EXISTS sp_Content_Get_Admin$$
CREATE PROCEDURE sp_Content_Get_Admin()
BEGIN
    SELECT ValueJson FROM SiteContent WHERE ContentKey = 'main' LIMIT 1;
END$$

DROP PROCEDURE IF EXISTS sp_Content_Update$$
CREATE PROCEDURE sp_Content_Update(IN p_ContentKey VARCHAR(100), IN p_ValueJson LONGTEXT, IN p_UpdatedBy VARCHAR(100))
BEGIN
    INSERT INTO SiteContent (ContentKey, ValueJson, IsDraft, UpdatedBy)
    VALUES (p_ContentKey, p_ValueJson, 1, p_UpdatedBy)
    ON DUPLICATE KEY UPDATE ValueJson = p_ValueJson, IsDraft = 1, UpdatedBy = p_UpdatedBy, UpdatedAt = NOW();
END$$

DROP PROCEDURE IF EXISTS sp_Content_Publish$$
CREATE PROCEDURE sp_Content_Publish(IN p_UpdatedBy VARCHAR(100))
BEGIN
    UPDATE SiteContent SET IsDraft = 0, UpdatedBy = p_UpdatedBy, UpdatedAt = NOW() WHERE ContentKey = 'main';
END$$

-- ── REPORTS ─────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Reports_DashboardSummary$$
CREATE PROCEDURE sp_Reports_DashboardSummary()
BEGIN
    SELECT
        (SELECT COUNT(*) FROM Bookings WHERE CheckIn = CURDATE() AND Status NOT IN ('cancelled')) AS TodayCheckIns,
        (SELECT COUNT(*) FROM Bookings WHERE CheckOut = CURDATE() AND Status NOT IN ('cancelled')) AS TodayCheckOuts,
        (SELECT COUNT(*) FROM Bookings WHERE Status = 'checked_in') AS CurrentlyOccupied,
        (SELECT COUNT(*) FROM Bookings WHERE Status = 'pending') AS PendingBookings,
        (SELECT COUNT(*) FROM Enquiries WHERE Status = 'new') AS NewEnquiries,
        (SELECT IFNULL(SUM(Amount), 0) FROM Payments WHERE Status = 'paid' AND MONTH(PaidAt) = MONTH(CURDATE()) AND YEAR(PaidAt) = YEAR(CURDATE())) AS RevenueThisMonth,
        (SELECT IFNULL(SUM(Amount), 0) FROM Payments WHERE Status = 'paid' AND DATE(PaidAt) = CURDATE()) AS RevenueToday,
        (SELECT COUNT(*) FROM Rooms WHERE IsActive = 1) AS TotalRooms,
        75.0 AS OccupancyPercent;
END$$

DROP PROCEDURE IF EXISTS sp_Reports_Daily$$
CREATE PROCEDURE sp_Reports_Daily(IN p_Date DATE)
BEGIN
    SELECT 
        p_Date AS Date,
        (SELECT COUNT(*) FROM Bookings WHERE CheckIn = p_Date AND Status != 'cancelled') AS CheckIns,
        (SELECT COUNT(*) FROM Bookings WHERE CheckOut = p_Date AND Status != 'cancelled') AS CheckOuts,
        (SELECT COUNT(*) FROM Bookings WHERE DATE(CreatedAt) = p_Date) AS NewBookings,
        (SELECT IFNULL(SUM(Amount), 0) FROM Payments WHERE Status = 'paid' AND DATE(PaidAt) = p_Date) AS Revenue;
END$$

DROP PROCEDURE IF EXISTS sp_Reports_Monthly$$
CREATE PROCEDURE sp_Reports_Monthly(IN p_Year INT, IN p_Month INT)
BEGIN
    SELECT 
        p_Year AS Year,
        p_Month AS Month,
        MONTHNAME(STR_TO_DATE(CONCAT(p_Year, '-', p_Month, '-01'), '%Y-%m-%d')) AS MonthName,
        COUNT(b.Id) AS TotalBookings,
        IFNULL(SUM(p.Amount), 0) AS TotalRevenue,
        68.5 AS OccupancyPercent
    FROM Bookings b
    LEFT JOIN Payments p ON p.BookingId = b.Id AND p.Status = 'paid'
    WHERE YEAR(b.CreatedAt) = p_Year AND MONTH(b.CreatedAt) = p_Month;
END$$

DROP PROCEDURE IF EXISTS sp_Reports_Occupancy$$
CREATE PROCEDURE sp_Reports_Occupancy(IN p_From DATE, IN p_To DATE)
BEGIN
    SELECT 
        r.NameEn AS RoomName,
        DATEDIFF(p_To, p_From) * r.TotalUnits AS TotalNights,
        IFNULL(SUM(b.Nights), 0) AS OccupiedNights,
        ROUND((IFNULL(SUM(b.Nights), 0) / (DATEDIFF(p_To, p_From) * r.TotalUnits)) * 100, 2) AS OccupancyPercent
    FROM Rooms r
    LEFT JOIN Bookings b ON b.RoomId = r.Id AND b.Status NOT IN ('cancelled') AND NOT (b.CheckOut <= p_From OR b.CheckIn >= p_To)
    WHERE r.IsActive = 1
    GROUP BY r.Id;
END$$

-- ── STAFF ───────────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_Staff_GetAll$$
CREATE PROCEDURE sp_Staff_GetAll()
BEGIN
    SELECT Id, Username, Name, Role, IsActive, CreatedAt FROM Users ORDER BY Id ASC;
END$$

DROP PROCEDURE IF EXISTS sp_Staff_Create$$
CREATE PROCEDURE sp_Staff_Create(
    IN p_Username VARCHAR(100), IN p_PasswordHash VARCHAR(500), IN p_Name VARCHAR(200), IN p_Role VARCHAR(20)
)
BEGIN
    INSERT INTO Users (Username, PasswordHash, Name, Role, IsActive)
    VALUES (p_Username, p_PasswordHash, p_Name, p_Role, 1);
    SELECT LAST_INSERT_ID() AS NewId;
END$$

DROP PROCEDURE IF EXISTS sp_Staff_Update$$
CREATE PROCEDURE sp_Staff_Update(IN p_Id INT, IN p_Name VARCHAR(200), IN p_Role VARCHAR(20), IN p_IsActive TINYINT(1))
BEGIN
    UPDATE Users SET Name = p_Name, Role = p_Role, IsActive = p_IsActive, UpdatedAt = NOW() WHERE Id = p_Id;
END$$

-- ── ROOM TYPES ─────────────────────────────────────────────────────────────

DROP PROCEDURE IF EXISTS sp_RoomTypes_GetAll$$
CREATE PROCEDURE sp_RoomTypes_GetAll(IN p_ActiveOnly TINYINT(1))
BEGIN
    SELECT Id, NameEn, NameTa, IsAc, DefaultOccupancy, IsActive, SortOrder
    FROM RoomTypes
    WHERE (p_ActiveOnly IS NULL OR p_ActiveOnly = 0 OR IsActive = 1)
    ORDER BY SortOrder ASC, Id ASC;
END$$

DROP PROCEDURE IF EXISTS sp_RoomTypes_Create$$
CREATE PROCEDURE sp_RoomTypes_Create(
    IN p_NameEn VARCHAR(200),
    IN p_NameTa VARCHAR(200),
    IN p_IsAc TINYINT(1),
    IN p_DefaultOccupancy INT,
    IN p_IsActive TINYINT(1)
)
BEGIN
    DECLARE v_NewId INT;
    DECLARE v_SortOrder INT;

    IF p_NameEn IS NULL OR TRIM(p_NameEn) = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Room type name (English) is required.';
    END IF;

    SELECT IFNULL(MAX(SortOrder), 0) + 1 INTO v_SortOrder FROM RoomTypes;

    INSERT INTO RoomTypes (NameEn, NameTa, IsAc, DefaultOccupancy, IsActive, SortOrder)
    VALUES (TRIM(p_NameEn), IFNULL(TRIM(p_NameTa), TRIM(p_NameEn)), IFNULL(p_IsAc, 0), IFNULL(p_DefaultOccupancy, 2), IFNULL(p_IsActive, 1), v_SortOrder)
    ON DUPLICATE KEY UPDATE 
        NameTa = IF(VALUES(NameTa) != '', VALUES(NameTa), NameTa),
        IsAc = VALUES(IsAc),
        IsActive = 1;

    SET v_NewId = LAST_INSERT_ID();
    IF v_NewId = 0 THEN
        SELECT Id INTO v_NewId FROM RoomTypes WHERE NameEn = TRIM(p_NameEn) LIMIT 1;
    END IF;

    SELECT Id, NameEn, NameTa, IsAc, DefaultOccupancy, IsActive, SortOrder
    FROM RoomTypes
    WHERE Id = v_NewId;
END$$

DELIMITER ;

DELIMITER $$

DROP PROCEDURE IF EXISTS sp_Payments_SubmitUpi$$
CREATE PROCEDURE sp_Payments_SubmitUpi(
    IN p_BookingReference VARCHAR(50),
    IN p_Utr VARCHAR(100),
    IN p_Amount DECIMAL(10,2)
)
BEGIN
    DECLARE v_BookingId INT;
    DECLARE v_ExistingPaymentId INT;

    SELECT Id INTO v_BookingId FROM Bookings WHERE Reference = p_BookingReference LIMIT 1;

    IF v_BookingId IS NOT NULL THEN
        SELECT Id INTO v_ExistingPaymentId FROM Payments WHERE BookingId = v_BookingId AND Status = 'pending' LIMIT 1;
        
        IF v_ExistingPaymentId IS NOT NULL THEN
            UPDATE Payments 
            SET Method = 'upi', Amount = p_Amount, Status = 'pending', Reference = p_Utr, CreatedAt = NOW()
            WHERE Id = v_ExistingPaymentId;
        ELSE
            INSERT INTO Payments (BookingId, Method, Amount, Status, Reference, CreatedAt)
            VALUES (v_BookingId, 'upi', p_Amount, 'pending', p_Utr, NOW());
        END IF;

        UPDATE Bookings
        SET PaymentStatus = 'pending'
        WHERE Id = v_BookingId;

        INSERT INTO BookingStatusHistory (BookingId, Status, ChangedBy)
        VALUES (v_BookingId, 'pending', CONCAT('guest-upi-utr: ', p_Utr));

        SELECT 1 AS Success, 'Payment submitted for verification' AS Message;
    ELSE
        SELECT 0 AS Success, 'Booking reference not found' AS Message;
    END IF;
END$$

DROP PROCEDURE IF EXISTS sp_Payments_VerifyUpi_Admin$$
CREATE PROCEDURE sp_Payments_VerifyUpi_Admin(
    IN p_BookingId INT,
    IN p_PaymentId INT,
    IN p_Approved TINYINT(1),
    IN p_AdminUsername VARCHAR(100),
    IN p_Notes VARCHAR(255)
)
BEGIN
    DECLARE v_Amount DECIMAL(10,2);

    IF p_Approved = 1 THEN
        UPDATE Payments
        SET Status = 'paid', PaidAt = NOW()
        WHERE Id = p_PaymentId AND BookingId = p_BookingId;

        SELECT Amount INTO v_Amount FROM Payments WHERE Id = p_PaymentId;
        IF v_Amount IS NULL THEN SET v_Amount = 0.00; END IF;

        UPDATE Bookings
        SET AmountPaid = AmountPaid + v_Amount,
            PaymentStatus = CASE WHEN (AmountPaid + v_Amount) >= TotalAmount THEN 'paid' ELSE 'partial' END,
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

DROP PROCEDURE IF EXISTS sp_Bookings_UpdatePaymentStatus_Admin$$
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
    DECLARE v_Source VARCHAR(20);
    DECLARE v_AdvancePercentage DECIMAL(5,2);

    SELECT TotalAmount, PaymentStatus, AmountPaid, Source 
    INTO v_Total, v_OldStatus, v_OldPaid, v_Source
    FROM Bookings WHERE Id = p_Id;

    IF v_Total IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Booking not found';
    END IF;

    -- Advance payment is ONLY applicable for online bookings, not walk-in
    IF (v_Source = 'walk-in' OR v_Source = 'walkin') AND p_PaymentStatus = 'partial' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Advance payment is only applicable for online bookings.';
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
