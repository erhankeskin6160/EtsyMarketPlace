namespace SimilarProductsWinForms;

using SimilarProductsWinForms.Services;
using SimilarProductsWinForms.Controls;

internal sealed class OpportunityScoreForm : Form
{
    private readonly TextBox _productTextBox = new();
    private readonly ModernNumericUpDown _demandInput = CreateScoreInput();
    private readonly ModernNumericUpDown _shopFitInput = CreateScoreInput();
    private readonly ModernNumericUpDown _productionEaseInput = CreateScoreInput();
    private readonly ModernNumericUpDown _profitInput = CreateScoreInput();
    private readonly ModernNumericUpDown _visualInput = CreateScoreInput();
    private readonly ModernNumericUpDown _competitionRiskInput = CreateScoreInput();
    private readonly ModernNumericUpDown _shippingRiskInput = CreateScoreInput();
    private readonly ModernNumericUpDown _ipRiskInput = CreateScoreInput();
    private readonly ModernMultilineTextBox _resultTextBox = new();

    public OpportunityScoreForm(ProductCandidate? product)
    {
        BuildLayout();
        LoadProduct(product);
        Calculate();
    }

    private void BuildLayout()
    {
        Text = "Urun Firsat Puani";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(850, 700);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);
        UiStyle.ApplyResponsiveTheme(this, new Size(850, 700));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 11,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddTextRow(root, 0, "Urun", _productTextBox);
        AddInputRow(root, 1, "Talep potansiyeli", _demandInput);
        AddInputRow(root, 2, "Magaza uyumu", _shopFitInput);
        AddInputRow(root, 3, "Uretim kolayligi", _productionEaseInput);
        AddInputRow(root, 4, "Kar potansiyeli", _profitInput);
        AddInputRow(root, 5, "Gorsel vitrin etkisi", _visualInput);
        AddInputRow(root, 6, "Rekabet riski", _competitionRiskInput);
        AddInputRow(root, 7, "Kargo/hasar riski", _shippingRiskInput);
        AddInputRow(root, 8, "Marka/IP riski", _ipRiskInput);

        var calculateButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Firsat puanini hesapla",
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        calculateButton.FlatAppearance.BorderSize = 0;
        calculateButton.Click += (_, _) => Calculate();
        root.Controls.Add(new Label(), 0, 9);
        root.Controls.Add(calculateButton, 1, 9);

        _resultTextBox.Dock = DockStyle.Fill;
        _resultTextBox.ReadOnly = true;
        root.Controls.Add(CreateLabel("Sonuc"), 0, 10);
        root.Controls.Add(_resultTextBox, 1, 10);

        for (var i = 0; i < 10; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        }
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Controls.Add(root);
    }

    private void LoadProduct(ProductCandidate? product)
    {
        if (product is null)
        {
            _productTextBox.Text = "Secili urun yok";
            return;
        }

        _productTextBox.Text = product.Name;
        var baseScore = Math.Clamp(product.Priority, 35, 95);
        _demandInput.Value = baseScore;
        _shopFitInput.Value = Math.Clamp(baseScore + 5, 0, 100);
        _productionEaseInput.Value = product.MaxPrice <= 55 ? 80 : 62;
        _profitInput.Value = product.MaxPrice >= 80 ? 78 : 62;
        _visualInput.Value = product.Category is "LOTR" or "Marvel" or "Ben10" or "Video Game" ? 82 : 68;
        _competitionRiskInput.Value = 55;
        _shippingRiskInput.Value = product.MaxPrice >= 120 ? 70 : 45;
        _ipRiskInput.Value = product.Category is "LOTR" or "Marvel" or "Ben10" or "Valorant" or "Minecraft" ? 70 : 55;
    }

    private void Calculate()
    {
        var result = OpportunityScoreCalculator.Calculate(
            (int)_demandInput.Value,
            (int)_shopFitInput.Value,
            (int)_productionEaseInput.Value,
            (int)_profitInput.Value,
            (int)_visualInput.Value,
            (int)_competitionRiskInput.Value,
            (int)_shippingRiskInput.Value,
            (int)_ipRiskInput.Value);

        _resultTextBox.Text =
            $"Firsat puani: {result.Score}/100{Environment.NewLine}" +
            $"Karar: {result.Decision}{Environment.NewLine}" +
            $"Pozitif agirlikli skor: {result.WeightedPositiveScore:0.##}{Environment.NewLine}" +
            $"Risk agirlikli skor: {result.WeightedRiskScore:0.##}{Environment.NewLine}{Environment.NewLine}" +
            $"Guclu yanlar:{Environment.NewLine}{FormatList(result.Strengths)}{Environment.NewLine}{Environment.NewLine}" +
            $"Riskler:{Environment.NewLine}{FormatList(result.Risks)}{Environment.NewLine}{Environment.NewLine}" +
            $"Sonraki adimlar:{Environment.NewLine}{FormatList(result.NextActions)}";
    }

    private static void AddTextRow(TableLayoutPanel root, int row, string label, TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.ReadOnly = true;
        root.Controls.Add(CreateLabel(label), 0, row);
        root.Controls.Add(textBox, 1, row);
    }

    private static void AddInputRow(TableLayoutPanel root, int row, string label, ModernNumericUpDown input)
    {
        input.Dock = DockStyle.Fill;
        input.ValueChanged += (_, _) =>
        {
            if (input.FindForm() is OpportunityScoreForm form)
            {
                form.Calculate();
            }
        };
        root.Controls.Add(CreateLabel(label), 0, row);
        root.Controls.Add(input, 1, row);
    }

    private static Label CreateLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private static ModernNumericUpDown CreateScoreInput() => new()
    {
        Minimum = 0,
        Maximum = 100,
        Increment = 5,
    };

    private void InitializeComponent()
    {

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
