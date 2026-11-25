namespace SriSakthiLodge_API.Model
{
    public class Tariff
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public string RoomName { get; set; } = "";
        public string PlanName { get; set; } = "";
        public decimal BaseRate { get; set; }
        public string StartDate { get; set; } = "";
        public string? EndDate { get; set; }
        public string? Season { get; set; }
        public decimal? TaxPercent { get; set; }
        public bool IsActive { get; set; }
    }

    public class CreateTariffRequest
    {
        public int RoomId { get; set; }
        public string PlanName { get; set; } = "";
        public decimal BaseRate { get; set; }
        public string StartDate { get; set; } = "";
        public string? EndDate { get; set; }
        public string? Season { get; set; }
        public decimal? TaxPercent { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateTariffRequest : CreateTariffRequest { }
}

