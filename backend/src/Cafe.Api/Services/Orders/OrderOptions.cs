namespace Cafe.Api.Services.Orders;

public sealed class OrderOptions
{
    public const string SectionName = "Orders";

    public int MinLeadMinutes { get; set; } = 15;
    public int MaxAdvanceDays { get; set; } = 7;
    public int MaxLines { get; set; } = 20;
    public int MaxQuantityPerLine { get; set; } = 20;

    public decimal MaxCounterPaymentAmount { get; set; } = 1000m;   // PHP (1000 pesos)
    public int UnpaidExpiryMinutes { get; set; } = 30;
    public int NoShowMinutes { get; set; } = 20;
    public int CleanupIntervalSeconds { get; set; } = 60;
}