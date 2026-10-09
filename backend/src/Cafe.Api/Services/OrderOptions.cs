namespace Cafe.Api.Services;

public sealed class OrderOptions
{
    public const string SectionName = "Orders";

    public int MinLeadMinutes { get; set; } = 15;
    public int MaxAdvanceDays { get; set; } = 7;
    public int MaxLines { get; set; } = 20;
    public int MaxQuantityPerLine { get; set; } = 20;
}