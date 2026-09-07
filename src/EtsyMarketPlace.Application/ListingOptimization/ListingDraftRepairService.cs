namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Text;

/// <summary>
/// Analyzes a listing draft validation report and produces a targeted repair prompt
/// that instructs the AI to fix only the specific issues found. Enables the
/// "generate → validate → repair → show" loop.
/// </summary>
public sealed class ListingDraftRepairService
{
    private readonly ListingDraftValidator _validator = new();

    /// <summary>
    /// Minimum overall score below which a repair attempt is recommended.
    /// </summary>
    public const int RepairThreshold = 75;

    /// <summary>
    /// Maximum number of automatic repair iterations to prevent infinite loops.
    /// </summary>
    public const int MaxRepairIterations = 2;

    /// <summary>
    /// Evaluates whether the given draft needs repair based on validation scores.
    /// </summary>
    public RepairDecision Evaluate(ListingDraftValidationReport report)
    {
        if (report.OverallScore >= RepairThreshold)
        {
            return new RepairDecision(false, report, [], "Taslak yeterli kalitede, onarma gerekmiyor.");
        }

        var repairTargets = new List<string>();

        if (report.Title.Score < 70)
            repairTargets.Add("title");
        if (report.Tags.Score < 70)
            repairTargets.Add("tags");
        if (report.Description.Score < 70)
            repairTargets.Add("description");
        if (report.Materials.Score < 50)
            repairTargets.Add("materials");
        if (report.Risk.Score < 70)
            repairTargets.Add("risk");

        // Even if individual fields are OK, overall is low — repair all
        if (repairTargets.Count == 0)
        {
            repairTargets.AddRange(["title", "tags", "description"]);
        }

        var summary = $"Genel puan {report.OverallScore}/100. Onarilmasi gereken alanlar: {string.Join(", ", repairTargets)}.";
        return new RepairDecision(true, report, repairTargets, summary);
    }

    /// <summary>
    /// Builds a repair prompt that tells the AI exactly which fields to fix and what the issues are.
    /// The AI should return the same JSON schema as the original optimization prompt.
    /// </summary>
    public static string BuildRepairPrompt(
        RepairDecision decision,
        string currentTitle,
        string currentDescription,
        IReadOnlyList<string> currentTags,
        IReadOnlyList<string> currentMaterials,
        string targetKeyword)
    {
        var builder = new StringBuilder();

        builder.AppendLine(ListingDraftInstructionBuilder.BuildResponseSchemaInstruction());
        builder.AppendLine();
        builder.AppendLine(ListingDraftInstructionBuilder.BuildSystemInstruction());
        builder.AppendLine();
        builder.AppendLine("REPAIR MODE: The previous AI draft was validated and found issues. Fix ONLY the problems listed below. Keep everything else unchanged.");
        builder.AppendLine();

        // List specific issues to fix
        builder.AppendLine("Issues to fix:");
        foreach (var issue in decision.Report.Issues.Take(8))
        {
            builder.AppendLine($"- {issue}");
        }
        builder.AppendLine();

        // Field-specific repair instructions
        if (decision.RepairTargets.Contains("title"))
        {
            builder.AppendLine("TITLE REPAIR:");
            foreach (var issue in decision.Report.Title.Issues)
            {
                builder.AppendLine($"  - {issue}");
            }
            builder.AppendLine("  Generate 3 new titles that fix these issues while keeping the product identity.");
            builder.AppendLine();
        }

        if (decision.RepairTargets.Contains("tags"))
        {
            builder.AppendLine("TAG REPAIR:");
            foreach (var issue in decision.Report.Tags.Issues)
            {
                builder.AppendLine($"  - {issue}");
            }
            builder.AppendLine("  Generate 13 tags that fix these issues. Each tag must be 20 characters or less, English, multi-word when possible.");
            builder.AppendLine();
        }

        if (decision.RepairTargets.Contains("description"))
        {
            builder.AppendLine("DESCRIPTION REPAIR:");
            foreach (var issue in decision.Report.Description.Issues)
            {
                builder.AppendLine($"  - {issue}");
            }
            builder.AppendLine($"  Rewrite the description in English fixing these issues. Weave '{targetKeyword}' naturally into the first paragraph.");
            builder.AppendLine();
        }

        if (decision.RepairTargets.Contains("materials"))
        {
            builder.AppendLine("MATERIALS REPAIR:");
            foreach (var issue in decision.Report.Materials.Issues)
            {
                builder.AppendLine($"  - {issue}");
            }
            builder.AppendLine("  List only real, specific materials mentioned in the product.");
            builder.AppendLine();
        }

        if (decision.RepairTargets.Contains("risk"))
        {
            builder.AppendLine("RISK REPAIR:");
            foreach (var issue in decision.Report.Risk.Issues)
            {
                builder.AppendLine($"  - {issue}");
            }
            builder.AppendLine("  Remove or replace risky brand/IP terms with generic descriptive alternatives. Add Turkish risk_warnings for any remaining concerns.");
            builder.AppendLine();
        }

        // Current draft context
        builder.AppendLine("Current draft to repair:");
        builder.AppendLine($"Target keyword: {targetKeyword}");
        builder.AppendLine($"Current title: {currentTitle}");
        builder.AppendLine($"Current tags: {string.Join(", ", currentTags)}");
        builder.AppendLine($"Current materials: {string.Join(", ", currentMaterials)}");
        builder.AppendLine($"Current description: {currentDescription}");

        return builder.ToString();
    }

    /// <summary>
    /// Validates a draft and returns the validation report.
    /// Convenience method for the repair loop.
    /// </summary>
    public ListingDraftValidationReport ValidateDraft(
        string title,
        string description,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> materials,
        string category,
        string targetKeyword) =>
        _validator.Validate(new ListingDraftValidationInput(title, description, tags, materials, category, targetKeyword));
}

// --- Models ---

public sealed record RepairDecision(
    bool NeedsRepair,
    ListingDraftValidationReport Report,
    IReadOnlyList<string> RepairTargets,
    string Summary);
