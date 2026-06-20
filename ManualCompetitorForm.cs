namespace SimilarProductsWinForms;

internal sealed class ManualCompetitorForm : Form
{
    private readonly TextBox _shopNameTextBox = new();
    private readonly TextBox _shopUrlTextBox = new();
    private readonly TextBox _priceTextBox = new();
    private readonly TextBox _unitsSoldTextBox = new();

    public ProductCandidate Result { get; private set; }

    public ManualCompetitorForm(ProductCandidate product)
    {
        Result = product;
        BuildLayout(product);
    }

    private void BuildLayout(ProductCandidate product)
    {
        Text = "Manuel Rakip Verisi";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 420);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.Controls.Add(CreateLabel("Urun"), 0, 0);
        root.Controls.Add(new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            Text = product.Name,
        }, 1, 0);

        AddTextRow(root, 1, "Klon magaza", _shopNameTextBox, product.CloneShopName);
        AddTextRow(root, 2, "Klon/listing linki", _shopUrlTextBox, product.CloneShopUrlDisplay);
        AddTextRow(root, 3, "Rakip fiyat", _priceTextBox, product.CompetitorPriceDisplay);
        AddTextRow(root, 4, "Satis adedi/not", _unitsSoldTextBox, product.UnitsSoldDisplay);

        var info = new Label
        {
            Dock = DockStyle.Fill,
            Text = "API yasakli veya onaysiz oldugunda rakip verisini Etsy sayfasindan manuel kopyalayip buraya girin.",
            ForeColor = Color.FromArgb(75, 85, 99),
        };
        root.Controls.Add(new Label(), 0, 5);
        root.Controls.Add(info, 1, 5);

        var saveButton = new Button
        {
            Dock = DockStyle.Right,
            Width = 150,
            Text = "Kaydet",
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += (_, _) =>
        {
            Result = product with
            {
                CloneShopName = _shopNameTextBox.Text.Trim(),
                CloneShopUrl = _shopUrlTextBox.Text.Trim(),
                CompetitorPrice = _priceTextBox.Text.Trim(),
                UnitsSold = _unitsSoldTextBox.Text.Trim(),
            };
            DialogResult = DialogResult.OK;
            Close();
        };
        root.Controls.Add(new Label(), 0, 6);
        root.Controls.Add(saveButton, 1, 6);

        for (var i = 0; i < 6; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        }
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

        Controls.Add(root);
    }

    private static void AddTextRow(TableLayoutPanel root, int row, string label, TextBox textBox, string value)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Text = value;
        root.Controls.Add(CreateLabel(label), 0, row);
        root.Controls.Add(textBox, 1, row);
    }

    private static Label CreateLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
    };
}
