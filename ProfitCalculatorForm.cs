namespace SimilarProductsWinForms;

using System.Globalization;
using SimilarProductsWinForms.Services;

internal sealed class ProfitCalculatorForm : Form
{
    private readonly TextBox _productNameTextBox = new();
    private readonly NumericUpDown _salePriceInput = CreateMoneyInput(500);
    private readonly NumericUpDown _materialCostInput = CreateMoneyInput(200);
    private readonly NumericUpDown _laborHoursInput = CreateNumberInput(24, 0.25m);
    private readonly NumericUpDown _hourlyRateInput = CreateMoneyInput(100);
    private readonly NumericUpDown _packagingCostInput = CreateMoneyInput(50);
    private readonly NumericUpDown _shippingCostInput = CreateMoneyInput(150);
    private readonly NumericUpDown _adCostInput = CreateMoneyInput(100);
    private readonly NumericUpDown _targetMarginInput = CreatePercentInput();
    private readonly TextBox _resultTextBox = new();

    public ProfitCalculatorForm(ProductCandidate? product)
    {
        BuildLayout();
        LoadProduct(product);
        Calculate();
    }

    private void BuildLayout()
    {
        Text = "Fiyat ve Kar Hesaplayici";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 640);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 11,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddTextRow(root, 0, "Urun", _productNameTextBox);
        AddInputRow(root, 1, "Satis fiyati ($)", _salePriceInput);
        AddInputRow(root, 2, "Malzeme maliyeti ($)", _materialCostInput);
        AddInputRow(root, 3, "Iscilik saati", _laborHoursInput);
        AddInputRow(root, 4, "Saatlik iscilik ($)", _hourlyRateInput);
        AddInputRow(root, 5, "Paketleme ($)", _packagingCostInput);
        AddInputRow(root, 6, "Kargo ($)", _shippingCostInput);
        AddInputRow(root, 7, "Reklam payi ($)", _adCostInput);
        AddInputRow(root, 8, "Hedef kar marji (%)", _targetMarginInput);

        var calculateButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "Hesapla",
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        calculateButton.FlatAppearance.BorderSize = 0;
        calculateButton.Click += (_, _) => Calculate();
        root.Controls.Add(new Label(), 0, 9);
        root.Controls.Add(calculateButton, 1, 9);

        _resultTextBox.Dock = DockStyle.Fill;
        _resultTextBox.Multiline = true;
        _resultTextBox.ReadOnly = true;
        _resultTextBox.ScrollBars = ScrollBars.Vertical;
        _resultTextBox.BackColor = Color.White;
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
            _productNameTextBox.Text = "Secili urun yok";
            _salePriceInput.Value = 49.90m;
            return;
        }

        _productNameTextBox.Text = product.Name;
        _salePriceInput.Value = Math.Clamp(product.MaxPrice, _salePriceInput.Minimum, _salePriceInput.Maximum);

        if (product.MaxPrice >= 100)
        {
            _materialCostInput.Value = 18;
            _laborHoursInput.Value = 2.5m;
            _packagingCostInput.Value = 6;
            _shippingCostInput.Value = 18;
        }
        else if (product.MaxPrice >= 50)
        {
            _materialCostInput.Value = 8;
            _laborHoursInput.Value = 1.25m;
            _packagingCostInput.Value = 4;
            _shippingCostInput.Value = 12;
        }
        else
        {
            _materialCostInput.Value = 3;
            _laborHoursInput.Value = 0.5m;
            _packagingCostInput.Value = 2;
            _shippingCostInput.Value = 7;
        }

        _hourlyRateInput.Value = 8;
        _adCostInput.Value = 3;
        _targetMarginInput.Value = 30;
    }

    private void Calculate()
    {
        var result = ProfitCalculator.Calculate(
            _salePriceInput.Value,
            _materialCostInput.Value,
            _laborHoursInput.Value,
            _hourlyRateInput.Value,
            _packagingCostInput.Value,
            _shippingCostInput.Value,
            _adCostInput.Value,
            _targetMarginInput.Value);

        var warning = result.NetProfit < 0
            ? "UYARI: Bu fiyat zararda."
            : result.SalePrice < result.TargetPrice
                ? "UYARI: Hedef kar marjinin altinda."
                : "Durum: Hedef kar marji karsilaniyor.";

        _resultTextBox.Text =
            $"{warning}{Environment.NewLine}{Environment.NewLine}" +
            $"Satis fiyati: {Money(result.SalePrice)}{Environment.NewLine}" +
            $"Toplam maliyet: {Money(result.TotalCost)}{Environment.NewLine}" +
            $"Net kar: {Money(result.NetProfit)}{Environment.NewLine}" +
            $"Kar marji: {(result.ProfitMargin * 100):0.##}%{Environment.NewLine}{Environment.NewLine}" +
            $"Etsy listing fee: {Money(result.ListingFee)}{Environment.NewLine}" +
            $"Etsy transaction fee (%6.5): {Money(result.TransactionFee)}{Environment.NewLine}" +
            $"Iscilik maliyeti: {Money(result.LaborCost)}{Environment.NewLine}{Environment.NewLine}" +
            $"Basa bas fiyat: {Money(result.BreakEvenPrice)}{Environment.NewLine}" +
            $"Hedef marj icin onerilen fiyat: {Money(result.TargetPrice)}";
    }

    private static void AddTextRow(TableLayoutPanel root, int row, string label, TextBox textBox)
    {
        textBox.Dock = DockStyle.Fill;
        textBox.ReadOnly = true;
        root.Controls.Add(CreateLabel(label), 0, row);
        root.Controls.Add(textBox, 1, row);
    }

    private static void AddInputRow(TableLayoutPanel root, int row, string label, NumericUpDown input)
    {
        input.Dock = DockStyle.Fill;
        input.ValueChanged += (_, _) =>
        {
            if (input.FindForm() is ProfitCalculatorForm form)
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

    private static NumericUpDown CreateMoneyInput(decimal maximum) => new()
    {
        DecimalPlaces = 2,
        Minimum = 0,
        Maximum = maximum,
        Increment = 1,
        ThousandsSeparator = true,
    };

    private static NumericUpDown CreateNumberInput(decimal maximum, decimal increment) => new()
    {
        DecimalPlaces = 2,
        Minimum = 0,
        Maximum = maximum,
        Increment = increment,
        ThousandsSeparator = true,
    };

    private static NumericUpDown CreatePercentInput() => new()
    {
        DecimalPlaces = 0,
        Minimum = 0,
        Maximum = 90,
        Increment = 5,
    };

    private static string Money(decimal value) => value.ToString("$0.00", CultureInfo.InvariantCulture);
}
