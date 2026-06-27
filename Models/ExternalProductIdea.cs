namespace SimilarProductsWinForms.Models;

internal sealed class ExternalProductIdea
{
    public string Source { get; init; } = "";
    public string Title { get; init; } = "";
    public string SearchUrl { get; init; } = "";
    public string ProductUrl { get; set; } = "";
    public string Price { get; set; } = "";
    public string Notes { get; init; } = "";
    public int OpportunityScore { get; init; }
    public string Opportunity => $"{OpportunityScore}/100";
}
