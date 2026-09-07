namespace SimilarProductsWinForms.Models;

internal sealed class ListingHealthReport
{
    public int TotalScore { get; init; }
    public string Grade { get; init; } = "C";
    public string SummaryText { get; init; } = "";

    // Kategori Kırılımları (Max Puanlar)
    public int ImageScore { get; init; }       // Max 20
    public int TitleScore { get; init; }       // Max 20
    public int TagScore { get; init; }         // Max 20
    public int DescriptionScore { get; init; } // Max 15
    public int InventoryScore { get; init; }   // Max 15
    public int SeoScore { get; init; }         // Max 10

    public List<HealthCategoryBreakdown> BreakdownList { get; init; } = [];
    public List<string> Strengths { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public List<string> ActionItems { get; init; } = [];
}

internal sealed record HealthCategoryBreakdown(
    string CategoryName,
    int Score,
    int MaxScore,
    string StatusText,
    System.Drawing.Color StatusColor);
