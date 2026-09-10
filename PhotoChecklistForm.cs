namespace SimilarProductsWinForms;

using System.Text;
using SimilarProductsWinForms.Controls;

internal sealed class PhotoChecklistForm : Form
{
    private readonly ProductCandidate? _product;
    private readonly List<CheckBox> _checks = [];
    private readonly Label _scoreLabel = new();
    private readonly ModernMultilineTextBox _planTextBox = new();

    public PhotoChecklistForm(ProductCandidate? product)
    {
        _product = product;
        BuildLayout();
        UpdatePlan();
    }

    private void BuildLayout()
    {
        Text = "Gorsel Kontrol Listesi";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(820, 640);
        Font = new Font("Segoe UI", 10F);
        Padding = new Padding(18);
        UiStyle.ApplyResponsiveTheme(this, new Size(820, 640));

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 260));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = _product is null ? "Secili urun yok" : _product.Name,
            Font = new Font("Segoe UI Semibold", 14F),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        var checkPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 8,
        };
        AddCheck(checkPanel, "Ana foto: urunu temiz zeminde net ve tam gosteriyor");
        AddCheck(checkPanel, "Olcek foto: elde, masada veya referans objeyle boyut hissi veriyor");
        AddCheck(checkPanel, "Kullanim foto: masa, raf, duvar, cosplay veya oda dekoru sahnesi var");
        AddCheck(checkPanel, "Detay foto: boya, doku, isik, parca birlesimi veya yuzey kalitesi gorunuyor");
        AddCheck(checkPanel, "Varyasyon foto: renk, boyut veya aksesuar secenekleri ayri gorunuyor");
        AddCheck(checkPanel, "Paket icerigi foto: alicinin kutuda ne alacagi belli");
        AddCheck(checkPanel, "Olcu foto: cetvel, olcu yazisi veya boyut bilgisi var");
        _scoreLabel.Dock = DockStyle.Fill;
        _scoreLabel.Font = new Font("Segoe UI Semibold", 11F);
        _scoreLabel.TextAlign = ContentAlignment.MiddleLeft;
        checkPanel.Controls.Add(_scoreLabel, 0, 7);
        root.Controls.Add(checkPanel, 0, 1);

        _planTextBox.Dock = DockStyle.Fill;
        _planTextBox.ReadOnly = true;
        root.Controls.Add(_planTextBox, 0, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
        };
        var copyButton = CreateButton("Plani kopyala");
        copyButton.Click += (_, _) =>
        {
            Clipboard.SetText(_planTextBox.Text);
            copyButton.Text = "Kopyalandi";
        };
        buttonPanel.Controls.Add(copyButton);
        root.Controls.Add(buttonPanel, 0, 3);

        Controls.Add(root);
    }

    private void AddCheck(TableLayoutPanel panel, string text)
    {
        var check = new CheckBox
        {
            Dock = DockStyle.Fill,
            Text = text,
            AutoSize = false,
        };
        check.CheckedChanged += (_, _) => UpdatePlan();
        _checks.Add(check);
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        panel.Controls.Add(check, 0, panel.Controls.Count);
    }

    private void UpdatePlan()
    {
        var completed = _checks.Count(check => check.Checked);
        var total = _checks.Count;
        var percent = total == 0 ? 0 : (int)Math.Round(completed * 100m / total);
        _scoreLabel.Text = $"Gorsel hazirlik skoru: {percent}/100 ({completed}/{total})";

        var builder = new StringBuilder();
        builder.AppendLine($"Urun: {(_product is null ? "Secili urun yok" : _product.Name)}");
        builder.AppendLine($"Gorsel hazirlik skoru: {percent}/100");
        builder.AppendLine();
        builder.AppendLine("Eksik cekimler:");
        foreach (var check in _checks.Where(check => !check.Checked))
        {
            builder.AppendLine($"- {check.Text}");
        }

        builder.AppendLine();
        builder.AppendLine("Not:");
        builder.AppendLine("Ilk foto urunu tek bakista anlatmali. Bulanik, karanlik, cok kirpilmis veya sadece atmosfer veren gorseller satis kararini zayiflatir.");
        _planTextBox.Text = builder.ToString();
    }

    private static Button CreateButton(string text) => new()
    {
        Text = text,
        Width = 145,
        Height = 34,
        BackColor = Color.FromArgb(32, 97, 165),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
    };
}
