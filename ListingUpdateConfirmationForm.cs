namespace SimilarProductsWinForms;

using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class ListingUpdateConfirmationForm(
    MarketListingResult listing,
    ListingTextUpdate update) : Form
{
    private readonly CheckBox _confirmCheckBox = new()
    {
        Dock = DockStyle.Fill,
        Text = "Bu listing Etsy'de canli olarak guncellenecek. Eski metni kontrol ettim ve onayliyorum.",
    };

    public bool Confirmed { get; private set; }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
    }

    private void BuildLayout()
    {
        Text = "Etsy Listing Guncelleme Onayi";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 720);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(16);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        Controls.Add(root);

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"Listing #{listing.ListingId} canli guncelleme onayi",
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        split.Controls.Add(BuildTextPanel("Mevcut Etsy metni", CurrentText()), 0, 0);
        split.Controls.Add(BuildTextPanel("Yeni uygulanacak metin", NewText()), 1, 0);
        root.Controls.Add(split, 0, 1);

        root.Controls.Add(_confirmCheckBox, 0, 2);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };
        var updateButton = CreateButton("Etsy'de Guncelle", Color.FromArgb(20, 126, 76));
        updateButton.Click += (_, _) =>
        {
            if (!_confirmCheckBox.Checked)
            {
                MessageBox.Show(this, "Canli guncelleme icin onay kutusunu isaretleyin.", "Onay gerekli", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Confirmed = true;
            DialogResult = DialogResult.OK;
            Close();
        };
        buttons.Controls.Add(updateButton);

        var cancelButton = CreateButton("Vazgec", Color.FromArgb(82, 93, 110));
        cancelButton.Click += (_, _) => Close();
        buttons.Controls.Add(cancelButton);
        root.Controls.Add(buttons, 0, 3);
    }

    private string CurrentText() =>
        $"📌 MEVCUT BAŞLIK{Environment.NewLine}{listing.Title}{Environment.NewLine}{Environment.NewLine}" +
        $"🏷️ MEVCUT TAGLER{Environment.NewLine}{string.Join(", ", listing.Tags)}{Environment.NewLine}{Environment.NewLine}" +
        $"🧱 MEVCUT MATERYALLER{Environment.NewLine}Mevcut materyal bilgisi Etsy listing detayından kontrol edilmeli.{Environment.NewLine}{Environment.NewLine}" +
        $"📄 MEVCUT AÇIKLAMA{Environment.NewLine}{listing.Description}";

    private string NewText() =>
        $"✨ YENİ OPTİMİZE BAŞLIK ({update.Title.Length}/140 Karakter){Environment.NewLine}{update.Title}{Environment.NewLine}{Environment.NewLine}" +
        $"🏷️ YENİ 13 LONG-TAIL TAG ({update.Tags.Count} Tag){Environment.NewLine}{string.Join(", ", update.Tags)}{Environment.NewLine}{Environment.NewLine}" +
        $"🧱 YENİ MATERYALLER{Environment.NewLine}{string.Join(", ", update.Materials ?? [])}{Environment.NewLine}{Environment.NewLine}" +
        $"📄 YENİ PARAGRAFLI AÇIKLAMA (ETSY FORMATI){Environment.NewLine}{EtsyMarketPlace.Application.ListingOptimization.EtsyDescriptionFormatter.NormalizeForEtsy(update.Description)}";

    private static Control BuildTextPanel(string title, string text)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(4) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI Semibold", 10.5F),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        panel.Controls.Add(new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Text = text,
        }, 0, 1);
        return panel;
    }

    private static Button CreateButton(string text, Color backColor)
    {
        var button = new Button
        {
            Text = text,
            Width = 165,
            Height = 36,
            Margin = new Padding(8, 6, 0, 6),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
