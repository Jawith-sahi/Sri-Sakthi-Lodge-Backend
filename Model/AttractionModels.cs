namespace SriSakthiLodge_API.Model
{
    public class Attraction
    {
        public int Id { get; set; }
        public string Slug { get; set; } = "";
        public LocalizedString Name { get; set; } = new();
        public LocalizedString Description { get; set; } = new();
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public string? MapsUrl { get; set; }
        public bool IsPublished { get; set; }
        public int SortOrder { get; set; }
    }

    public class CreateAttractionRequest
    {
        public string Slug { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string NameTa { get; set; } = "";
        public string? DescEn { get; set; }
        public string? DescTa { get; set; }
        public string? Category { get; set; }
        public string? ImageUrl { get; set; }
        public string? MapsUrl { get; set; }
        public bool IsPublished { get; set; } = false;
        public int SortOrder { get; set; } = 0;
    }

    public class UpdateAttractionRequest : CreateAttractionRequest { }
}

