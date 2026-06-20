namespace SimilarProductsWinForms.Models;

internal sealed record ListingDraft(
    string Title,
    string ShortDescription,
    string LongDescription,
    string Tags,
    string Materials,
    string PackageContents,
    string PhotoChecklist,
    string ProductionNotes,
    string SafetyNotes);
