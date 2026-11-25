namespace SriSakthiLodge_API.Model
{
    public class SiteContent
    {
        public LocalizedString About { get; set; } = new();
        public AnnouncementContent Announcement { get; set; } = new();
        public List<FaqItem> Faq { get; set; } = new();
        public PoliciesContent Policies { get; set; } = new();
    }

    public class AnnouncementContent
    {
        public bool Active { get; set; }
        public LocalizedString Text { get; set; } = new();
    }

    public class FaqItem
    {
        public LocalizedString Question { get; set; } = new();
        public LocalizedString Answer { get; set; } = new();
    }

    public class PoliciesContent
    {
        public LocalizedString Cancellation { get; set; } = new();
        public LocalizedString Terms { get; set; } = new();
        public LocalizedString Privacy { get; set; } = new();
    }

    public class UpdateContentRequest
    {
        public string ContentKey { get; set; } = "";
        public string ValueJson { get; set; } = "";
    }
}

