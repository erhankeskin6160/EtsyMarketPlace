namespace SimilarProductsWinForms;

using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;

internal sealed class WeeklyReportForm : Form
{
    private readonly WeeklyReport _report;

    public WeeklyReportForm(IEnumerable<ProductCandidate> products)
    {
        _report = WeeklyReportGenerator.Generate(products);
        BuildLayout();
    }

    private void BuildLayout()
    {
        Text = "Haftalik Rapor";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 760);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);
        UiStyle.ApplyResponsiveTheme(this, new Size(980, 760));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = _report.Title,
            Font = new Font("Segoe UI Semibold", 14F),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreatePage("Ozet", _report.Summary));
        tabs.TabPages.Add(CreatePage("Top urunler", _report.TopProducts));
        tabs.TabPages.Add(CreatePage("Durum", _report.StatusBreakdown));
        tabs.TabPages.Add(CreatePage("Kategori", _report.CategoryBreakdown));
        tabs.TabPages.Add(CreatePage("Aksiyon", _report.ActionPlan));
        tabs.TabPages.Add(CreatePage("API", _report.ApiStatus));
        tabs.TabPages.Add(CreatePage("Markdown", _report.Markdown));
        root.Controls.Add(tabs, 0, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };

        var copyButton = CreateButton("Raporu kopyala");
        copyButton.Click += (_, _) =>
        {
            Clipboard.SetText(_report.Markdown);
            copyButton.Text = "Kopyalandi";
        };
        buttonPanel.Controls.Add(copyButton);

        var saveButton = CreateButton("Markdown kaydet");
        saveButton.Click += (_, _) => SaveMarkdown();
        buttonPanel.Controls.Add(saveButton);

        var saveHtmlButton = CreateButton("HTML kaydet");
        saveHtmlButton.Click += (_, _) => SaveHtml();
        buttonPanel.Controls.Add(saveHtmlButton);

        root.Controls.Add(buttonPanel, 0, 2);
        Controls.Add(root);
    }

    private static TabPage CreatePage(string title, string text)
    {
        var page = new TabPage(title);
        page.Controls.Add(new ModernMultilineTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Text = text,
        });
        return page;
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Width = 150,
        Height = 34,
        Margin = new Padding(0, 0, 8, 0),
        BackColor = Color.FromArgb(32, 97, 165),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
    };

    private void SaveMarkdown()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Markdown dosyasi (*.md)|*.md",
            FileName = $"haftalik-rapor-{DateTime.Now:yyyy-MM-dd}.md",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, _report.Markdown);
    }

    private void SaveHtml()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "HTML dosyasi (*.html)|*.html",
            FileName = $"haftalik-rapor-{DateTime.Now:yyyy-MM-dd}.html",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, WeeklyReportHtmlExporter.BuildHtml(_report));
    }
}
