namespace SimilarProductsWinForms.Models;

internal sealed record WeeklyReport(
    string Title,
    string Summary,
    string TopProducts,
    string StatusBreakdown,
    string CategoryBreakdown,
    string ActionPlan,
    string ApiStatus,
    string Markdown);
