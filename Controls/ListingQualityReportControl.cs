namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Windows.Forms;
using EtsyMarketPlace.Application.ListingOptimization;

internal sealed class ListingQualityReportControl : UserControl
{
    private readonly Label _titleLabel = new();
    private readonly Label _overallScoreBadge = new();
    private readonly Label _riskBadge = new();
    private readonly Label _titleScoreLabel = new();
    private readonly Label _tagsScoreLabel = new();
    private readonly Label _descriptionScoreLabel = new();
    private readonly Label _materialsScoreLabel = new();
    private readonly Label _riskScoreLabel = new();
    private readonly ProgressBar _titleProgressBar = new();
    private readonly ProgressBar _tagsProgressBar = new();
    private readonly ProgressBar _descriptionProgressBar = new();
    private readonly ProgressBar _materialsProgressBar = new();
    private readonly ProgressBar _riskProgressBar = new();
    private readonly ListView _issuesListView = new();
    private readonly Button _repairButton = UiStyle.CreateButton("AI ile Otomatik Onar");
    private Action? _onRepairRequested;

    public ListingQualityReportControl()
    {
        BuildControlLayout();
    }

    private void BuildControlLayout()
    {
        Dock = DockStyle.Fill;
        BackColor = UiStyle.CardBackground;
        Padding = new Padding(12);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); // Header
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Score breakdown
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Issues list
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Repair button
        Controls.Add(root);

        // Header Panel
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));

        _titleLabel.Dock = DockStyle.Fill;
        _titleLabel.Text = "Etsy Taslak Kalite Karnesi";
        _titleLabel.Font = new Font("Segoe UI Semibold", 11F);
        _titleLabel.ForeColor = UiStyle.TextDark;
        _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
        header.Controls.Add(_titleLabel, 0, 0);

        ConfigureBadge(_overallScoreBadge, "Puan: -", UiStyle.SecondaryColor);
        header.Controls.Add(_overallScoreBadge, 1, 0);

        ConfigureBadge(_riskBadge, "Risk: -", UiStyle.SecondaryColor);
        header.Controls.Add(_riskBadge, 2, 0);

        root.Controls.Add(header, 0, 0);

        // Score Breakdown Table
        var breakdown = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5, Padding = new Padding(0, 4, 0, 4) };
        breakdown.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        breakdown.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        breakdown.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        breakdown.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        for (var r = 0; r < 5; r++) breakdown.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));

        AddScoreRow(breakdown, 0, 0, "Baslik", _titleScoreLabel, _titleProgressBar);
        AddScoreRow(breakdown, 0, 2, "Tagler", _tagsScoreLabel, _tagsProgressBar);
        AddScoreRow(breakdown, 1, 0, "Aciklama", _descriptionScoreLabel, _descriptionProgressBar);
        AddScoreRow(breakdown, 1, 2, "Materyal", _materialsScoreLabel, _materialsProgressBar);
        AddScoreRow(breakdown, 2, 0, "Risk Puani", _riskScoreLabel, _riskProgressBar);

        root.Controls.Add(breakdown, 0, 1);

        // Issues ListView
        _issuesListView.Dock = DockStyle.Fill;
        _issuesListView.View = View.Details;
        _issuesListView.HeaderStyle = ColumnHeaderStyle.None;
        _issuesListView.FullRowSelect = true;
        _issuesListView.Columns.Add("Detay", -2);
        _issuesListView.BorderStyle = BorderStyle.FixedSingle;
        _issuesListView.Font = new Font("Segoe UI", 9F);
        root.Controls.Add(_issuesListView, 0, 2);

        // Repair Button
        _repairButton.Click += (_, _) => _onRepairRequested?.Invoke();
        _repairButton.Visible = false;
        root.Controls.Add(_repairButton, 0, 3);
    }

    public void SetReport(
        ListingDraftValidationReport? report,
        Action? onRepairRequested = null)
    {
        _onRepairRequested = onRepairRequested;
        _repairButton.Visible = onRepairRequested is not null;

        if (report is null)
        {
            ResetDisplay();
            return;
        }

        // Overall Score Badge
        _overallScoreBadge.Text = $"{report.OverallScore}/100";
        _overallScoreBadge.BackColor = GetScoreColor(report.OverallScore);

        // Risk Badge
        _riskBadge.Text = $"Risk: {report.Risk.RiskLevel}";
        _riskBadge.BackColor = report.Risk.RiskLevel switch
        {
            "Dusuk" => UiStyle.SuccessColor,
            "Orta" => UiStyle.AccentColor,
            _ => UiStyle.DangerColor,
        };

        // Field Scores
        UpdateScoreRow("Baslik", report.Title.Score, _titleScoreLabel, _titleProgressBar);
        UpdateScoreRow("Tagler", report.Tags.Score, _tagsScoreLabel, _tagsProgressBar);
        UpdateScoreRow("Aciklama", report.Description.Score, _descriptionScoreLabel, _descriptionProgressBar);
        UpdateScoreRow("Materyal", report.Materials.Score, _materialsScoreLabel, _materialsProgressBar);
        UpdateScoreRow("Risk", report.Risk.Score, _riskScoreLabel, _riskProgressBar);

        // Populate ListView with Issues and Strengths
        _issuesListView.Items.Clear();

        foreach (var issue in report.Issues)
        {
            var item = new ListViewItem($"  ❌ {issue}")
            {
                ForeColor = UiStyle.DangerColor,
            };
            _issuesListView.Items.Add(item);
        }

        foreach (var strength in report.Strengths)
        {
            var item = new ListViewItem($"  ✅ {strength}")
            {
                ForeColor = UiStyle.SuccessColor,
            };
            _issuesListView.Items.Add(item);
        }

        if (_issuesListView.Columns.Count > 0)
        {
            _issuesListView.Columns[0].Width = _issuesListView.Width - 25;
        }
    }

    private static void ConfigureBadge(Label badge, string text, Color color)
    {
        badge.Dock = DockStyle.Fill;
        badge.Text = text;
        badge.BackColor = color;
        badge.ForeColor = Color.White;
        badge.Font = new Font("Segoe UI Semibold", 9F);
        badge.TextAlign = ContentAlignment.MiddleCenter;
        badge.Margin = new Padding(2);
    }

    private static void AddScoreRow(
        TableLayoutPanel parent,
        int row,
        int col,
        string labelText,
        Label scoreLabel,
        ProgressBar progressBar)
    {
        scoreLabel.Dock = DockStyle.Fill;
        scoreLabel.Text = $"{labelText}: -";
        scoreLabel.Font = new Font("Segoe UI", 8.5F);
        scoreLabel.TextAlign = ContentAlignment.MiddleLeft;
        parent.Controls.Add(scoreLabel, col, row);

        progressBar.Dock = DockStyle.Fill;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        progressBar.Margin = new Padding(2, 3, 6, 3);
        parent.Controls.Add(progressBar, col + 1, row);
    }

    private static void UpdateScoreRow(
        string labelText,
        int score,
        Label scoreLabel,
        ProgressBar progressBar)
    {
        scoreLabel.Text = $"{labelText}: {score}/100";
        progressBar.Value = Math.Clamp(score, 0, 100);
    }

    private static Color GetScoreColor(int score) => score switch
    {
        >= 80 => UiStyle.SuccessColor,
        >= 65 => UiStyle.AccentColor,
        _ => UiStyle.DangerColor,
    };

    private void ResetDisplay()
    {
        _overallScoreBadge.Text = "Puan: -";
        _overallScoreBadge.BackColor = UiStyle.SecondaryColor;
        _riskBadge.Text = "Risk: -";
        _riskBadge.BackColor = UiStyle.SecondaryColor;
        _titleScoreLabel.Text = "Baslik: -";
        _tagsScoreLabel.Text = "Tagler: -";
        _descriptionScoreLabel.Text = "Aciklama: -";
        _materialsScoreLabel.Text = "Materyal: -";
        _riskScoreLabel.Text = "Risk Puani: -";
        _titleProgressBar.Value = 0;
        _tagsProgressBar.Value = 0;
        _descriptionProgressBar.Value = 0;
        _materialsProgressBar.Value = 0;
        _riskProgressBar.Value = 0;
        _issuesListView.Items.Clear();
    }
}
