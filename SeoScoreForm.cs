namespace SimilarProductsWinForms;

using SimilarProductsWinForms.Services;

internal sealed class SeoScoreForm : Form
{
    private readonly TextBox _titleTextBox = new();
    private readonly TextBox _descriptionTextBox = new();
    private readonly TextBox _tagsTextBox = new();
    private readonly TextBox _primaryKeywordTextBox = new();
    private readonly TextBox _resultTextBox = new();

    public SeoScoreForm(ProductCandidate? product)
    {
        BuildLayout();
        LoadProduct(product);
        Calculate();
    }

    private void BuildLayout()
    {
        Text = "SEO Kalite Skoru";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(920, 720);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

        AddTextBoxRow(root, 0, "Ana keyword", _primaryKeywordTextBox, multiline: false);
        AddTextBoxRow(root, 1, "Baslik", _titleTextBox, multiline: true);
        AddTextBoxRow(root, 2, "Aciklama", _descriptionTextBox, multiline: true);
        AddTextBoxRow(root, 3, "13 tag", _tagsTextBox, multiline: true);

        var calculateButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "SEO skorunu hesapla",
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        calculateButton.FlatAppearance.BorderSize = 0;
        calculateButton.Click += (_, _) => Calculate();
        root.Controls.Add(new Label(), 0, 4);
        root.Controls.Add(calculateButton, 1, 4);

        _resultTextBox.Dock = DockStyle.Fill;
        _resultTextBox.Multiline = true;
        _resultTextBox.ReadOnly = true;
        _resultTextBox.ScrollBars = ScrollBars.Vertical;
        _resultTextBox.BackColor = Color.White;
        root.Controls.Add(CreateLabel("Sonuc"), 0, 5);
        root.Controls.Add(_resultTextBox, 1, 5);

        Controls.Add(root);
    }

    private void LoadProduct(ProductCandidate? product)
    {
        if (product is null)
        {
            _primaryKeywordTextBox.Text = "";
            _titleTextBox.Text = "";
            _descriptionTextBox.Text = "";
            _tagsTextBox.Text = "";
            return;
        }

        var primaryKeyword = BuildPrimaryKeyword(product);
        _primaryKeywordTextBox.Text = primaryKeyword;
        _titleTextBox.Text = BuildTitle(product, primaryKeyword);
        _descriptionTextBox.Text = BuildDescription(product, primaryKeyword);
        _tagsTextBox.Text = string.Join(", ", BuildTags(product));
    }

    private void Calculate()
    {
        var tags = _tagsTextBox.Text
            .Split([',', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = SeoScoreCalculator.Calculate(
            _titleTextBox.Text,
            _descriptionTextBox.Text,
            tags,
            _primaryKeywordTextBox.Text);

        _resultTextBox.Text =
            $"SEO skoru: {result.Score}/100{Environment.NewLine}" +
            $"Kullanilan tag: {result.UsedTagCount}/13{Environment.NewLine}" +
            $"Tekrar eden tag: {result.DuplicateTagCount}{Environment.NewLine}" +
            $"Long-tail tag: {result.LongTailTagCount}{Environment.NewLine}{Environment.NewLine}" +
            $"Guclu yanlar:{Environment.NewLine}{FormatList(result.Strengths)}{Environment.NewLine}" +
            $"Uyarilar:{Environment.NewLine}{FormatList(result.Warnings)}{Environment.NewLine}" +
            $"Oneriler:{Environment.NewLine}{FormatList(result.Suggestions)}";
    }

    private static void AddTextBoxRow(TableLayoutPanel root, int row, string label, TextBox textBox, bool multiline)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.Multiline = multiline;
        textBox.ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None;
        root.Controls.Add(CreateLabel(label), 0, row);
        root.Controls.Add(textBox, 1, row);
    }

    private static Label CreateLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static string BuildPrimaryKeyword(ProductCandidate product)
    {
        var words = product.Keywords
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(4);
        return string.Join(' ', words);
    }

    private static string BuildTitle(ProductCandidate product, string primaryKeyword)
    {
        return $"{primaryKeyword} - 3D Printed Collectible, Cosplay Prop, Desk Decor";
    }

    private static string BuildDescription(ProductCandidate product, string primaryKeyword)
    {
        return $"{primaryKeyword} is a 3D printed collectible designed for fans, cosplay displays, desks, shelves, and themed rooms.{Environment.NewLine}{Environment.NewLine}" +
            $"This product idea is related to: {product.RelatedStoreProduct}.{Environment.NewLine}" +
            $"Category path: {product.EtsyCategoryDisplay}.{Environment.NewLine}" +
            $"Opportunity note: {product.Reason}{Environment.NewLine}{Environment.NewLine}" +
            "Add exact dimensions, material, color options, processing time, package contents, and care instructions before publishing.";
    }

    private static List<string> BuildTags(ProductCandidate product)
    {
        var baseTags = new List<string>
        {
            "3d printed gift",
            "cosplay prop",
            "desk decor",
            "fan gift",
            "collector gift",
            "display prop",
            "game room decor",
            "movie room decor",
            "geek gift",
            "custom prop",
            "shelf decor",
            "printed figure",
            "handmade prop",
        };

        foreach (var word in product.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = word.Trim('-', ':');
            if (candidate.Length is >= 4 and <= 20 && !baseTags.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                baseTags.Insert(0, candidate);
            }
        }

        return baseTags.Take(13).ToList();
    }

    private static string FormatList(IReadOnlyCollection<string> values)
    {
        if (values.Count == 0)
        {
            return "- Yok";
        }

        return string.Join(Environment.NewLine, values.Select(value => $"- {value}"));
    }
}
