using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;
using System.Text.Json;

namespace SriSakthiLodge_API.Services
{
    public class RoomService
    {
        private readonly DbConnection _dbFactory;
        private readonly ILogger<RoomService> _logger;

        public RoomService(DbConnection dbFactory, ILogger<RoomService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task<List<Room>> GetAllPublicAsync()
        {
            var rooms = new List<Room>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Rooms_GetAll_Public", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                rooms.Add(ReadRoom(r));
            }

            if (await r.NextResultAsync())
            {
                while (await r.ReadAsync())
                {
                    var roomId = r.GetInt32("RoomId");
                    var room = rooms.FirstOrDefault(x => x.Id == roomId);
                    if (room != null)
                    {
                        room.Images.Add(new RoomImage
                        {
                            Id = r.GetInt32("Id"),
                            Url = r.GetString("Url"),
                            Alt = new LocalizedString
                            {
                                En = r.IsDBNull("AltEn") ? "" : r.GetString("AltEn"),
                                Ta = r.IsDBNull("AltTa") ? "" : r.GetString("AltTa")
                            },
                            SortOrder = r.GetInt32("SortOrder")
                        });
                    }
                }
            }

            if (await r.NextResultAsync())
            {
                while (await r.ReadAsync())
                {
                    var roomId = r.GetInt32("RoomId");
                    var room = rooms.FirstOrDefault(x => x.Id == roomId);
                    if (room != null)
                    {
                        room.Amenities.Add(new RoomAmenity
                        {
                            Key = r.GetString("AmenityKey"),
                            Label = new LocalizedString
                            {
                                En = r.GetString("LabelEn"),
                                Ta = r.GetString("LabelTa")
                            }
                        });
                    }
                }
            }

            return rooms;
        }

        public async Task<Room?> GetBySlugAsync(string slug)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Rooms_GetBySlug", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Slug", slug);

            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            var room = ReadRoom(r);

            if (await r.NextResultAsync())
            {
                while (await r.ReadAsync())
                {
                    room.Images.Add(new RoomImage
                    {
                        Id = r.GetInt32("Id"),
                        Url = r.GetString("Url"),
                        Alt = new LocalizedString
                        {
                            En = r.IsDBNull("AltEn") ? "" : r.GetString("AltEn"),
                            Ta = r.IsDBNull("AltTa") ? "" : r.GetString("AltTa")
                        },
                        SortOrder = r.GetInt32("SortOrder")
                    });
                }
            }

            if (await r.NextResultAsync())
            {
                while (await r.ReadAsync())
                {
                    room.Amenities.Add(new RoomAmenity
                    {
                        Key = r.GetString("AmenityKey"),
                        Label = new LocalizedString
                        {
                            En = r.GetString("LabelEn"),
                            Ta = r.GetString("LabelTa")
                        }
                    });
                }
            }

            return room;
        }

        public async Task<List<AvailableRoom>> CheckAvailabilityAsync(RoomAvailabilityRequest req)
        {
            var list = new List<AvailableRoom>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Rooms_CheckAvailability", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_CheckIn", req.CheckIn);
            cmd.Parameters.AddWithValue("p_CheckOut", req.CheckOut);
            cmd.Parameters.AddWithValue("p_Adults", req.Adults);
            cmd.Parameters.AddWithValue("p_Children", req.Children);
            cmd.Parameters.AddWithValue("p_Rooms", req.Rooms);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var allUnitsRaw = "";
                var availUnitsRaw = "";
                var bookedUnitsRaw = "";
                var blockedUnitsRaw = "";
                try
                {
                    var ordAll = r.GetOrdinal("AllRoomNumbers");
                    if (ordAll >= 0 && !r.IsDBNull(ordAll)) allUnitsRaw = r.GetString(ordAll);
                }
                catch { }

                try
                {
                    var ordAvail = r.GetOrdinal("AvailableRoomNumbers");
                    if (ordAvail >= 0 && !r.IsDBNull(ordAvail)) availUnitsRaw = r.GetString(ordAvail);
                }
                catch { }

                try
                {
                    var ordBooked = r.GetOrdinal("BookedRoomNumbers");
                    if (ordBooked >= 0 && !r.IsDBNull(ordBooked)) bookedUnitsRaw = r.GetString(ordBooked);
                }
                catch { }

                try
                {
                    var ordBlocked = r.GetOrdinal("BlockedRoomNumbers");
                    if (ordBlocked >= 0 && !r.IsDBNull(ordBlocked)) blockedUnitsRaw = r.GetString(ordBlocked);
                }
                catch { }

                list.Add(new AvailableRoom
                {
                    Id = r.GetInt32("Id"),
                    Slug = r.GetString("Slug"),
                    Name = new LocalizedString { En = r.GetString("NameEn"), Ta = r.GetString("NameTa") },
                    ShortDescription = new LocalizedString
                    {
                        En = r.IsDBNull(r.GetOrdinal("ShortDescEn")) ? "" : r.GetString("ShortDescEn"),
                        Ta = r.IsDBNull(r.GetOrdinal("ShortDescTa")) ? "" : r.GetString("ShortDescTa")
                    },
                    IsAc = r.GetBoolean("IsAc"),
                    MaxOccupancy = r.GetInt32("MaxOccupancy"),
                    ImageUrl = r.IsDBNull(r.GetOrdinal("ImageUrl")) ? null : r.GetString("ImageUrl"),
                    PricePerNight = r.GetDecimal("PricePerNight"),
                    TaxPercent = r.IsDBNull(r.GetOrdinal("TaxPercent")) ? null : r.GetDecimal("TaxPercent"),
                    AvailableUnits = r.GetInt32("AvailableUnits"),
                    Currency = r.GetString("Currency"),
                    AllRoomNumbers = !string.IsNullOrWhiteSpace(allUnitsRaw)
                        ? allUnitsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                        : new List<string>(),
                    AvailableRoomNumbers = !string.IsNullOrWhiteSpace(availUnitsRaw)
                        ? availUnitsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                        : new List<string>(),
                    BookedRoomNumbers = !string.IsNullOrWhiteSpace(bookedUnitsRaw)
                        ? bookedUnitsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                        : new List<string>(),
                    BlockedRoomNumbers = !string.IsNullOrWhiteSpace(blockedUnitsRaw)
                        ? blockedUnitsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                        : new List<string>()
                });
            }
            return list;
        }

        public async Task<List<Room>> GetAllAdminAsync()
        {
            var rooms = new List<Room>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Rooms_GetAll_Admin", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                rooms.Add(ReadRoom(r));
            }
            return rooms;
        }

        public async Task<PagedResult<Room>> GetAllAdminPagedAsync(int page = 1, int pageSize = 10, string? search = null, string? status = null, string? sort = null, string? dir = null)
        {
            var paged = new PagedResult<Room> { Page = page, PageSize = pageSize };
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            var where = "WHERE r.IsDeleted = 0";
            if (!string.IsNullOrWhiteSpace(search))
            {
                where += " AND (r.NameEn LIKE @search OR r.NameTa LIKE @search OR r.Slug LIKE @search OR r.ShortDescEn LIKE @search)";
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim().ToLowerInvariant();
                if (s == "active") where += " AND r.IsActive = 1";
                else if (s == "inactive") where += " AND r.IsActive = 0";
            }

            using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM Rooms r {where}", conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) countCmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                paged.TotalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                paged.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)paged.TotalCount / pageSize) : 0;
            }

            var offset = Math.Max(0, (page - 1) * pageSize);
            var orderBy = "ORDER BY r.SortOrder ASC, r.Id ASC";
            if (!string.IsNullOrWhiteSpace(sort))
            {
                var direction = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                if (sort.Equals("name", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY r.NameEn {direction}";
                else if (sort.Equals("startingPrice", StringComparison.OrdinalIgnoreCase) || sort.Equals("price", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY r.StartingPrice {direction}";
                else if (sort.Equals("totalUnits", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY r.TotalUnits {direction}";
                else if (sort.Equals("maxOccupancy", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY r.MaxOccupancy {direction}";
            }

            var sql = $@"
                SELECT r.Id, r.Slug, r.NameEn, r.NameTa, r.ShortDescEn, r.ShortDescTa, r.DescriptionEn, r.DescriptionTa,
                       r.IsAc, r.MaxOccupancy,
                       COALESCE(NULLIF((SELECT MIN(t.BaseRate) FROM Tariffs t WHERE t.RoomId = r.Id AND t.IsActive = 1), 0), r.StartingPrice, 0.00) AS StartingPrice,
                       r.Currency, r.TotalUnits, r.IsActive, r.SortOrder
                FROM Rooms r
                {where}
                {orderBy}
                LIMIT @offset, @pageSize";

            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    paged.Items.Add(ReadRoom(r));
                }
            }

            if (paged.Items.Count > 0)
            {
                var roomIds = string.Join(",", paged.Items.Select(x => x.Id));
                using var unitsCmd = new MySqlCommand(
                    $"SELECT RoomId, RoomNumber FROM RoomUnits WHERE RoomId IN ({roomIds}) ORDER BY CAST(RoomNumber AS UNSIGNED), RoomNumber ASC",
                    conn);
                using var unitsReader = await unitsCmd.ExecuteReaderAsync();
                var dict = paged.Items.ToDictionary(x => x.Id, x => x.RoomNumbers);
                while (await unitsReader.ReadAsync())
                {
                    var roomId = unitsReader.GetInt32("RoomId");
                    var num = unitsReader.GetString("RoomNumber");
                    if (dict.TryGetValue(roomId, out var list))
                    {
                        list.Add(num);
                    }
                }
            }

            return paged;
        }

        public async Task<Room?> GetByIdAdminAsync(int id)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Rooms_GetById", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);

            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            var room = ReadRoom(r);

            if (await r.NextResultAsync())
            {
                while (await r.ReadAsync())
                {
                    room.Images.Add(new RoomImage
                    {
                        Id = r.GetInt32("Id"),
                        Url = r.GetString("Url"),
                        Alt = new LocalizedString { En = r.IsDBNull("AltEn") ? "" : r.GetString("AltEn"), Ta = r.IsDBNull("AltTa") ? "" : r.GetString("AltTa") },
                        SortOrder = r.GetInt32("SortOrder")
                    });
                }
            }

            if (await r.NextResultAsync())
            {
                while (await r.ReadAsync())
                {
                    room.Amenities.Add(new RoomAmenity
                    {
                        Key = r.GetString("AmenityKey"),
                        Label = new LocalizedString { En = r.GetString("LabelEn"), Ta = r.GetString("LabelTa") }
                    });
                }
            }

            if (await r.NextResultAsync())
            {
                while (await r.ReadAsync())
                {
                    room.RoomNumbers.Add(r.GetString("RoomNumber"));
                }
            }

            return room;
        }

        public async Task<int?> CreateAsync(CreateRoomRequest req, int userId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Rooms_Create", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Slug", req.Slug);
            cmd.Parameters.AddWithValue("p_NameEn", req.NameEn);
            cmd.Parameters.AddWithValue("p_NameTa", req.NameTa);
            cmd.Parameters.AddWithValue("p_ShortDescEn", (object?)req.ShortDescEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_ShortDescTa", (object?)req.ShortDescTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_DescriptionEn", (object?)req.DescriptionEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_DescriptionTa", (object?)req.DescriptionTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IsAc", req.IsAc);
            cmd.Parameters.AddWithValue("p_MaxOccupancy", req.MaxOccupancy);
            cmd.Parameters.AddWithValue("p_StartingPrice", req.StartingPrice);
            cmd.Parameters.AddWithValue("p_TotalUnits", req.TotalUnits);
            cmd.Parameters.AddWithValue("p_IsActive", req.IsActive);
            cmd.Parameters.AddWithValue("p_SortOrder", req.SortOrder);
            cmd.Parameters.AddWithValue("p_AmenitiesJson", JsonSerializer.Serialize(req.Amenities));
            cmd.Parameters.AddWithValue("p_RoomNumbersJson", JsonSerializer.Serialize(req.RoomNumbers));

            var result = await cmd.ExecuteScalarAsync();
            return result != null && int.TryParse(result.ToString(), out int id) ? id : null;
        }

        public async Task<bool> UpdateAsync(int id, UpdateRoomRequest req, int userId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Rooms_Update", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_Slug", req.Slug);
            cmd.Parameters.AddWithValue("p_NameEn", req.NameEn);
            cmd.Parameters.AddWithValue("p_NameTa", req.NameTa);
            cmd.Parameters.AddWithValue("p_ShortDescEn", (object?)req.ShortDescEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_ShortDescTa", (object?)req.ShortDescTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_DescriptionEn", (object?)req.DescriptionEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_DescriptionTa", (object?)req.DescriptionTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IsAc", req.IsAc);
            cmd.Parameters.AddWithValue("p_MaxOccupancy", req.MaxOccupancy);
            cmd.Parameters.AddWithValue("p_StartingPrice", req.StartingPrice);
            cmd.Parameters.AddWithValue("p_TotalUnits", req.TotalUnits);
            cmd.Parameters.AddWithValue("p_IsActive", req.IsActive);
            cmd.Parameters.AddWithValue("p_SortOrder", req.SortOrder);
            cmd.Parameters.AddWithValue("p_AmenitiesJson", JsonSerializer.Serialize(req.Amenities));
            cmd.Parameters.AddWithValue("p_RoomNumbersJson", JsonSerializer.Serialize(req.RoomNumbers));

            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<bool> SetActiveAsync(int id, bool isActive, int userId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Rooms_SetActive", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_IsActive", isActive);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Rooms_Delete", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<int?> AddImageAsync(int roomId, string url, string? altEn, string? altTa)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_RoomImages_Add", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_RoomId", roomId);
            cmd.Parameters.AddWithValue("p_Url", url);
            cmd.Parameters.AddWithValue("p_AltEn", (object?)altEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_AltTa", (object?)altTa ?? DBNull.Value);

            var res = await cmd.ExecuteScalarAsync();
            return res != null && int.TryParse(res.ToString(), out int id) ? id : null;
        }

        public async Task<bool> DeleteImageAsync(int imageId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_RoomImages_Delete", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_ImageId", imageId);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<bool> ReorderImagesAsync(int roomId, List<ImageOrderItem> items)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_RoomImages_Reorder", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_RoomId", roomId);
            cmd.Parameters.AddWithValue("p_ItemsJson", JsonSerializer.Serialize(items));
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<List<BlockedDate>> GetBlockedDatesAsync(int roomId)
        {
            var list = new List<BlockedDate>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_BlockedDates_Get", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_RoomId", roomId);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new BlockedDate
                {
                    Id = r.GetInt32("Id"),
                    RoomId = r.GetInt32("RoomId"),
                    RoomNumber = r.IsDBNull("RoomNumber") ? null : r.GetString("RoomNumber"),
                    From = r.GetDateTime("FromDate").ToString("yyyy-MM-dd"),
                    To = r.GetDateTime("ToDate").ToString("yyyy-MM-dd"),
                    Reason = r.IsDBNull("Reason") ? null : r.GetString("Reason")
                });
            }
            return list;
        }

        public async Task<int?> AddBlockedDateAsync(int roomId, BlockDateRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_BlockedDates_Add", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_RoomId", roomId);
            cmd.Parameters.AddWithValue("p_FromDate", req.From);
            cmd.Parameters.AddWithValue("p_ToDate", req.To);
            cmd.Parameters.AddWithValue("p_Reason", (object?)req.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_RoomNumber", (object?)req.RoomNumber ?? DBNull.Value);

            var res = await cmd.ExecuteScalarAsync();
            return res != null && int.TryParse(res.ToString(), out int id) ? id : null;
        }

        public async Task<bool> RemoveBlockedDateAsync(int blockId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_BlockedDates_Remove", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_BlockId", blockId);
            await cmd.ExecuteNonQueryAsync();
            return true;
        }

        public async Task<List<RoomType>> GetRoomTypesAsync(bool activeOnly = true)
        {
            var list = new List<RoomType>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_RoomTypes_GetAll", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_ActiveOnly", activeOnly ? 1 : 0);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new RoomType
                {
                    Id = r.GetInt32("Id"),
                    NameEn = r.GetString("NameEn"),
                    NameTa = r.GetString("NameTa"),
                    Name = new LocalizedString { En = r.GetString("NameEn"), Ta = r.GetString("NameTa") },
                    IsAc = r.GetBoolean("IsAc"),
                    DefaultOccupancy = r.GetInt32("DefaultOccupancy"),
                    IsActive = r.GetBoolean("IsActive"),
                    SortOrder = r.GetInt32("SortOrder")
                });
            }
            return list;
        }

        public async Task<RoomType?> CreateRoomTypeAsync(CreateRoomTypeRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_RoomTypes_Create", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_NameEn", req.NameEn);
            cmd.Parameters.AddWithValue("p_NameTa", (object?)req.NameTa ?? req.NameEn);
            cmd.Parameters.AddWithValue("p_IsAc", req.IsAc ? 1 : 0);
            cmd.Parameters.AddWithValue("p_DefaultOccupancy", req.DefaultOccupancy);
            cmd.Parameters.AddWithValue("p_IsActive", req.IsActive ? 1 : 0);

            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new RoomType
                {
                    Id = r.GetInt32("Id"),
                    NameEn = r.GetString("NameEn"),
                    NameTa = r.GetString("NameTa"),
                    Name = new LocalizedString { En = r.GetString("NameEn"), Ta = r.GetString("NameTa") },
                    IsAc = r.GetBoolean("IsAc"),
                    DefaultOccupancy = r.GetInt32("DefaultOccupancy"),
                    IsActive = r.GetBoolean("IsActive"),
                    SortOrder = r.GetInt32("SortOrder")
                };
            }
            return null;
        }

        private static Room ReadRoom(MySqlDataReader r)
        {
            return new Room
            {
                Id = r.GetInt32("Id"),
                Slug = r.GetString("Slug"),
                Name = new LocalizedString { En = r.GetString("NameEn"), Ta = r.GetString("NameTa") },
                ShortDescription = new LocalizedString
                {
                    En = r.IsDBNull("ShortDescEn") ? "" : r.GetString("ShortDescEn"),
                    Ta = r.IsDBNull("ShortDescTa") ? "" : r.GetString("ShortDescTa")
                },
                Description = new LocalizedString
                {
                    En = r.IsDBNull("DescriptionEn") ? "" : r.GetString("DescriptionEn"),
                    Ta = r.IsDBNull("DescriptionTa") ? "" : r.GetString("DescriptionTa")
                },
                IsAc = r.GetBoolean("IsAc"),
                MaxOccupancy = r.GetInt32("MaxOccupancy"),
                StartingPrice = r.GetDecimal("StartingPrice"),
                Currency = r.GetString("Currency"),
                TotalUnits = r.GetInt32("TotalUnits"),
                IsActive = r.GetBoolean("IsActive"),
                SortOrder = r.GetInt32("SortOrder")
            };
        }
    }
}

