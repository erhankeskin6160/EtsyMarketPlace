namespace SimilarProductsWinForms;

using System.Diagnostics;
using EtsyMarketPlace.Application.Automation;
using SimilarProductsWinForms.Services;

internal sealed class AutomationReportingForm(
    IAutomationSettingsStore settingsStore,
    AutomationScheduler scheduler) : Form
{
    private readonly CheckBox _enabledCheckBox = new() { Text = "Otomatik yenilemeyi etkinlestir", AutoSize = true };
    private readonly NumericUpDown _intervalInput = new() { Minimum = 1, Maximum = 720 };
    private readonly NumericUpDown _lookbackInput = new() { Minimum = 1, Maximum = 365 };
    private readonly NumericUpDown _revenueAlertInput = new() { Minimum = 1, Maximum = 100, DecimalPlaces = 1 };
    private readonly NumericUpDown _orderAlertInput = new() { Minimum = 1, Maximum = 100, DecimalPlaces = 1 };
    private readonly TextBox _outputTextBox = new();
    private readonly TextBox _statusTextBox = new();
    private AutomationSettings _settings = new();

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
        LoadSettings();
        scheduler.StatusChanged += SchedulerOnStatusChanged;
        AppendStatus(scheduler.LastStatus);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        scheduler.StatusChanged -= SchedulerOnStatusChanged;
        base.OnFormClosed(e);
    }

    private void BuildLayout()
    {
        Text = "Otomasyon ve Raporlama";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(920, 650);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(247, 248, 250);
        Padding = new Padding(22);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 9 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        for (var row = 1; row <= 6; row++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Otomasyon ve Raporlama",
            Font = new Font("Segoe UI Semibold", 22F),
            ForeColor = Color.FromArgb(23, 32, 49),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        root.Controls.Add(title, 0, 0);
        root.SetColumnSpan(title, 2);

        root.Controls.Add(LabelFor("Durum"), 0, 1);
        var enabledPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        enabledPanel.Controls.Add(_enabledCheckBox);
        root.Controls.Add(enabledPanel, 1, 1);
        AddNumericRow(root, 2, "Calisma araligi (saat)", _intervalInput);
        AddNumericRow(root, 3, "Rapor donemi (gun)", _lookbackInput);
        AddNumericRow(root, 4, "Ciro dusus uyarisi (%)", _revenueAlertInput);
        AddNumericRow(root, 5, "Siparis dusus uyarisi (%)", _orderAlertInput);

        root.Controls.Add(LabelFor("Rapor klasoru"), 0, 6);
        var folderPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        folderPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        _outputTextBox.Dock = DockStyle.Fill;
        folderPanel.Controls.Add(_outputTextBox, 0, 0);
        var browse = CreateButton("Klasor Sec");
        browse.Click += (_, _) => SelectFolder();
        folderPanel.Controls.Add(browse, 1, 0);
        root.Controls.Add(folderPanel, 1, 6);

        var commands = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        commands.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        var save = CreateButton("Ayarlari Kaydet");
        save.Click += (_, _) => SaveSettings();
        commands.Controls.Add(save, 1, 0);
        var run = CreateButton("Simdi Calistir");
        run.Click += async (_, _) => await RunNowAsync();
        commands.Controls.Add(run, 2, 0);
        var openFolder = CreateButton("Klasoru Ac");
        openFolder.Click += (_, _) => OpenOutputFolder();
        commands.Controls.Add(openFolder, 3, 0);
        root.Controls.Add(commands, 0, 7);
        root.SetColumnSpan(commands, 2);

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        _statusTextBox.BackColor = Color.White;
        root.Controls.Add(_statusTextBox, 0, 8);
        root.SetColumnSpan(_statusTextBox, 2);
    }

    private void LoadSettings()
    {
        _settings = settingsStore.Load();
        _enabledCheckBox.Checked = _settings.Enabled;
        _intervalInput.Value = Math.Clamp(_settings.IntervalHours, 1, 720);
        _lookbackInput.Value = Math.Clamp(_settings.LookbackDays, 1, 365);
        _revenueAlertInput.Value = Math.Clamp(_settings.RevenueDropAlertPercent, 1, 100);
        _orderAlertInput.Value = Math.Clamp(_settings.OrderDropAlertPercent, 1, 100);
        _outputTextBox.Text = _settings.OutputDirectory;
        AppendStatus(_settings.LastRunAt.HasValue
            ? $"Son calisma: {_settings.LastRunAt:dd.MM.yyyy HH:mm}"
            : "Henuz otomasyon calismasi yok.");
    }

    private void SaveSettings()
    {
        ApplyControls();
        settingsStore.Save(_settings);
        AppendStatus("Ayarlar kaydedildi.");
    }

    private async Task RunNowAsync()
    {
        try
        {
            SaveSettings();
            UseWaitCursor = true;
            var result = await scheduler.RunNowAsync();
            AppendStatus($"HTML: {result.Export.HtmlPath}");
            AppendStatus($"CSV: {result.Export.CsvPath}");
            AppendStatus($"Uyari sayisi: {result.Alerts.Count}");
            LoadSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Otomasyon", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ApplyControls()
    {
        _settings.Enabled = _enabledCheckBox.Checked;
        _settings.IntervalHours = (int)_intervalInput.Value;
        _settings.LookbackDays = (int)_lookbackInput.Value;
        _settings.RevenueDropAlertPercent = _revenueAlertInput.Value;
        _settings.OrderDropAlertPercent = _orderAlertInput.Value;
        _settings.OutputDirectory = _outputTextBox.Text.Trim();
    }

    private void SelectFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "HTML ve CSV raporlarinin kaydedilecegi klasoru secin",
            SelectedPath = _outputTextBox.Text,
            ShowNewFolderButton = true,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) _outputTextBox.Text = dialog.SelectedPath;
    }

    private void OpenOutputFolder()
    {
        var path = _outputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private void SchedulerOnStatusChanged(string status)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(() => AppendStatus(status));
        else AppendStatus(status);
    }

    private void AppendStatus(string status) =>
        _statusTextBox.AppendText($"{DateTime.Now:HH:mm:ss} - {status}{Environment.NewLine}");

    private static void AddNumericRow(TableLayoutPanel root, int row, string label, NumericUpDown input)
    {
        root.Controls.Add(LabelFor(label), 0, row);
        input.Dock = DockStyle.Left;
        input.Width = 180;
        root.Controls.Add(input, 1, row);
    }

    private static Label LabelFor(string text) => new()
    {
        Dock = DockStyle.Fill,
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(52, 61, 75),
    };

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = Color.FromArgb(32, 97, 165),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5F),
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false,
            Margin = new Padding(6, 2, 0, 2),
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }
}
