namespace SimilarProductsWinForms;

using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class ListingDraftForm : Form
{
    private readonly ListingDraft _draft;
    private readonly TextBox _allDraftTextBox = new();

    public ListingDraftForm(ProductCandidate? product)
    {
        _draft = ListingDraftGenerator.Generate(product ?? ProductCandidate.Seed()[0]);
        BuildLayout(product);
    }

    private void BuildLayout(ProductCandidate? product)
    {
        Text = "Listing Taslak Uretici";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 760);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        var header = new Label
        {
            Dock = DockStyle.Fill,
            Text = product is null ? "Secili urun yok - ornek taslak" : product.Name,
            Font = new Font("Segoe UI Semibold", 14F),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        root.Controls.Add(header, 0, 0);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(CreatePage("Baslik", _draft.Title));
        tabs.TabPages.Add(CreatePage("Kisa aciklama", _draft.ShortDescription));
        tabs.TabPages.Add(CreatePage("Uzun aciklama", _draft.LongDescription));
        tabs.TabPages.Add(CreatePage("13 tag", _draft.Tags));
        tabs.TabPages.Add(CreatePage("Malzeme", _draft.Materials));
        tabs.TabPages.Add(CreatePage("Paket", _draft.PackageContents));
        tabs.TabPages.Add(CreatePage("Fotograf listesi", _draft.PhotoChecklist));
        tabs.TabPages.Add(CreatePage("Uretim notu", _draft.ProductionNotes));
        tabs.TabPages.Add(CreatePage("Guvenlik", _draft.SafetyNotes));
        tabs.TabPages.Add(CreateAllPage());
        root.Controls.Add(tabs, 0, 1);

        var copyButton = new Button
        {
            Dock = DockStyle.Right,
            Width = 180,
            Text = "Tum taslagi kopyala",
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        copyButton.FlatAppearance.BorderSize = 0;
        copyButton.Click += (_, _) =>
        {
            Clipboard.SetText(BuildAllDraftText());
            copyButton.Text = "Kopyalandi";
        };
        root.Controls.Add(copyButton, 0, 2);

        Controls.Add(root);
    }

    private static TabPage CreatePage(string title, string text)
    {
        var page = new TabPage(title);
        var box = CreateReadOnlyBox(text);
        page.Controls.Add(box);
        return page;
    }

    private TabPage CreateAllPage()
    {
        var page = new TabPage("Tum taslak");
        _allDraftTextBox.Dock = DockStyle.Fill;
        _allDraftTextBox.Multiline = true;
        _allDraftTextBox.ReadOnly = true;
        _allDraftTextBox.ScrollBars = ScrollBars.Vertical;
        _allDraftTextBox.Text = BuildAllDraftText();
        page.Controls.Add(_allDraftTextBox);
        return page;
    }

    private static TextBox CreateReadOnlyBox(string text) => new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Text = text,
        BackColor = Color.White,
    };

    private string BuildAllDraftText()
    {
        return
            $"TITLE{Environment.NewLine}{_draft.Title}{Environment.NewLine}{Environment.NewLine}" +
            $"SHORT DESCRIPTION{Environment.NewLine}{_draft.ShortDescription}{Environment.NewLine}{Environment.NewLine}" +
            $"LONG DESCRIPTION{Environment.NewLine}{_draft.LongDescription}{Environment.NewLine}{Environment.NewLine}" +
            $"TAGS{Environment.NewLine}{_draft.Tags}{Environment.NewLine}{Environment.NewLine}" +
            $"MATERIALS{Environment.NewLine}{_draft.Materials}{Environment.NewLine}{Environment.NewLine}" +
            $"PACKAGE CONTENTS{Environment.NewLine}{_draft.PackageContents}{Environment.NewLine}{Environment.NewLine}" +
            $"PHOTO CHECKLIST{Environment.NewLine}{_draft.PhotoChecklist}{Environment.NewLine}{Environment.NewLine}" +
            $"PRODUCTION NOTES{Environment.NewLine}{_draft.ProductionNotes}{Environment.NewLine}{Environment.NewLine}" +
            $"SAFETY NOTES{Environment.NewLine}{_draft.SafetyNotes}";
    }
}
