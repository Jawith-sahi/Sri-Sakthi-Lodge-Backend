namespace SriSakthiLodge_API.Model
{
    public class Offer
    {
        public int Id { get; set; }
        public LocalizedString Title { get; set; } = new();
        public LocalizedString Description { get; set; } = new();
        public string? ImageUrl { get; set; }
        public string StartDate { get; set; } = "";
        public string? EndDate { get; set; }
        public LocalizedString Terms { get; set; } = new();
        public bool IsPublished { get; set; }
        public int SortOrder { get; set; }
    }

    public class CreateOfferRequest
    {
        public string TitleEn { get; set; } = "";
        public string TitleTa { get; set; } = "";
        public string? DescEn { get; set; }
        public string? DescTa { get; set; }
        public string? ImageUrl { get; set; }
        public string StartDate { get; set; } = "";
        public string? EndDate { get; set; }
        public string? TermsEn { get; set; }
        public string? TermsTa { get; set; }
        public bool IsPublished { get; set; } = false;
        public int SortOrder { get; set; } = 0;
    }

    public class UpdateOfferRequest : CreateOfferRequest { }
}

