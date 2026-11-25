namespace SriSakthiLodge_API.Model
{
    public class GalleryItem
    {
        public int Id { get; set; }
        public string Type { get; set; } = "image";
        public string Url { get; set; } = "";
        public LocalizedString Title { get; set; } = new();
        public LocalizedString Alt { get; set; } = new();
        public string? Category { get; set; }
        public int SortOrder { get; set; }
        public bool IsPublished { get; set; }
    }

    public class CreateGalleryRequest
    {
        public string Type { get; set; } = "image";
        public string Url { get; set; } = "";
        public string? TitleEn { get; set; }
        public string? TitleTa { get; set; }
        public string? AltEn { get; set; }
        public string? AltTa { get; set; }
        public string? Category { get; set; }
        public int SortOrder { get; set; } = 0;
        public bool IsPublished { get; set; } = false;
    }

    public class UpdateGalleryRequest : CreateGalleryRequest { }
}

