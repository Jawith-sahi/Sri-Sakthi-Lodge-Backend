namespace SriSakthiLodge_API.Model
{
    public class LocalizedString
    {
        public string En { get; set; } = "";
        public string Ta { get; set; } = "";
    }

    public class RoomImage
    {
        public int Id { get; set; }
        public string Url { get; set; } = "";
        public LocalizedString Alt { get; set; } = new();
        public int SortOrder { get; set; }
    }

    public class RoomAmenity
    {
        public string Key { get; set; } = "";
        public LocalizedString Label { get; set; } = new();
    }

    public class Room
    {
        public int Id { get; set; }
        public string Slug { get; set; } = "";
        public LocalizedString Name { get; set; } = new();
        public LocalizedString ShortDescription { get; set; } = new();
        public LocalizedString Description { get; set; } = new();
        public bool IsAc { get; set; }
        public int MaxOccupancy { get; set; }
        public decimal StartingPrice { get; set; }
        public string Currency { get; set; } = "INR";
        public int TotalUnits { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public List<RoomImage> Images { get; set; } = new();
        public List<RoomAmenity> Amenities { get; set; } = new();
        public List<string> RoomNumbers { get; set; } = new();
    }

    public class RoomAvailabilityRequest
    {
        public string CheckIn { get; set; } = "";
        public string CheckOut { get; set; } = "";
        public int Adults { get; set; } = 1;
        public int Children { get; set; } = 0;
        public int Rooms { get; set; } = 1;
    }

    public class AvailableRoom
    {
        public int Id { get; set; }
        public string Slug { get; set; } = "";
        public LocalizedString Name { get; set; } = new();
        public LocalizedString? ShortDescription { get; set; }
        public bool IsAc { get; set; }
        public int MaxOccupancy { get; set; }
        public string? ImageUrl { get; set; }
        public decimal PricePerNight { get; set; }
        public decimal? TaxPercent { get; set; }
        public int AvailableUnits { get; set; }
        public string Currency { get; set; } = "INR";
        public List<string> AllRoomNumbers { get; set; } = new();
        public List<string> AvailableRoomNumbers { get; set; } = new();
        public List<string> BookedRoomNumbers { get; set; } = new();
        public List<string> BlockedRoomNumbers { get; set; } = new();
    }

    public class RoomUnit
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = "";
        public string? Floor { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class BlockedDate
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string? RoomNumber { get; set; }
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string? Reason { get; set; }
    }

    public class BlockDateRequest
    {
        public string? RoomNumber { get; set; }
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string? Reason { get; set; }
    }

    public class ReorderImagesRequest
    {
        public List<ImageOrderItem> Items { get; set; } = new();
    }

    public class ImageOrderItem
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
    }

    public class CreateRoomRequest
    {
        public string Slug { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string NameTa { get; set; } = "";
        public string? ShortDescEn { get; set; }
        public string? ShortDescTa { get; set; }
        public string? DescriptionEn { get; set; }
        public string? DescriptionTa { get; set; }
        public bool IsAc { get; set; }
        public int MaxOccupancy { get; set; }
        public decimal StartingPrice { get; set; }
        public int TotalUnits { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;
        public List<string> RoomNumbers { get; set; } = new();
        public List<CreateAmenityRequest> Amenities { get; set; } = new();
    }

    public class CreateAmenityRequest
    {
        public string Key { get; set; } = "";
        public string LabelEn { get; set; } = "";
        public string LabelTa { get; set; } = "";
    }

    public class UpdateRoomRequest : CreateRoomRequest { }

    public class RoomType
    {
        public int Id { get; set; }
        public string NameEn { get; set; } = "";
        public string NameTa { get; set; } = "";
        public LocalizedString Name { get; set; } = new();
        public bool IsAc { get; set; }
        public int DefaultOccupancy { get; set; } = 2;
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
    }

    public class CreateRoomTypeRequest
    {
        public string NameEn { get; set; } = "";
        public string? NameTa { get; set; }
        public bool IsAc { get; set; } = false;
        public int DefaultOccupancy { get; set; } = 2;
        public bool IsActive { get; set; } = true;
    }
}

