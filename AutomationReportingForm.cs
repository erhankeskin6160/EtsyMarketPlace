namespace SimilarProductsWinForms;

using System.Diagnostics;
using System.Text;
using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Domain.ProductOpportunity;
using EtsyMarketPlace.Infrastructure.Automation;
using EtsyMarketPlace.Infrastructure.Http;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class AutomationReportingForm(
    IAutomationSettingsStore settingsStore,
    AutomationScheduler scheduler,
    WindowsTaskSchedulerService taskScheduler) : Form
{
    private readonly CheckBox _enabledCheckBox = new() { Text = "Otomatik yenilemeyi etkinlestir", AutoSize = true };
    private readonly NumericUpDown _intervalInput = new() { Minimum = 1, Maximum = 720 };
    private readonly NumericUpDown _lookbackInput = new() { Minimum = 1, Maximum = 365 };
    private readonly NumericUpDown _revenueAlertInput = new() { Minimum = 1, Maximum = 100, DecimalPlaces = 1 };
    private readonly NumericUpDown _orderAlertInput = new() { Minimum = 1, Maximum = 100, DecimalPlaces = 1 };
    private readonly TextBox _outputTextBox = new();
    private readonly TextBox _statusTextBox = new();
    private readonly TextBox _queueShopTypeTextBox = new();
    private readonly TextBox _queueKeywordTextBox = new();
    private readonly NumericUpDown _queueLimitInput = new() { Minimum = 5, Maximum = 100, Value = 30 };
    private readonly Label _queueSummaryLabel = new();
    private readonly BindingSource _queueBindingSource = new();
    private readonly DataGridView _queueGrid = new();
    private readonly ExternalMarketplaceSearchService _externalSearchService = new();
    private readonly OpportunityBatchActionService _batchActionService = new();
    private List<ExternalProductIdea> _queueRows = [];
    private AutomationSettings _settings = new();

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        BuildLayout();
        LoadSettings();
        scheduler.StatusChanged += SchedulerOnStatusChanged;
        ApiResilienceTelemetry.EventPublished += ApiTelemetryOnEventPublished;
        AppendStatus(scheduler.LastStatus);
        if (ApiResilienceTelemetry.LastEvent is { } lastEvent) AppendApiEvent(lastEvent);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        scheduler.StatusChanged -= SchedulerOnStatusChanged;
        ApiResilienceTelemetry.EventPublished -= ApiTelemetryOnEventPublished;
        base.OnFormClosed(e);
    }

    private void BuildLayout()
    {
        Text = "Otomasyon ve Raporlama";
        StartPosition = FormStartPosition.CenterParent;
        UiStyle.ApplyResponsiveTheme(this, new Size(1024, 680));
        Padding = new Padding(22);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 13 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        for (var row = 1; row <= 6; row++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var title = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Otomasyon ve Raporlama",
            Font = UiStyle.TitleFont,
            ForeColor = UiStyle.TextDark,
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

        root.Controls.Add(LabelFor("Windows gorevi"), 0, 7);
        var taskCommands = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
        for (var column = 0; column < 4; column++) taskCommands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        var createTask = CreateButton("Gorev Olustur");
        createTask.Click += async (_, _) => await CreateWindowsTaskAsync();
        taskCommands.Controls.Add(createTask, 0, 0);
        var queryTask = CreateButton("Durum");
        queryTask.Click += async (_, _) => await QueryWindowsTaskAsync();
        taskCommands.Controls.Add(queryTask, 1, 0);
        var runTask = CreateButton("Hemen Calistir");
        runTask.Click += async (_, _) => await RunWindowsTaskAsync();
        taskCommands.Controls.Add(runTask, 2, 0);
        var deleteTask = CreateButton("Gorevi Kaldir", isDanger: true);
        deleteTask.Click += async (_, _) => await DeleteWindowsTaskAsync();
        taskCommands.Controls.Add(deleteTask, 3, 0);
        root.Controls.Add(taskCommands, 1, 7);

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
        root.Controls.Add(commands, 0, 8);
        root.SetColumnSpan(commands, 2);

        var queueTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Firsat kuyrugu raporu",
            Font = new Font("Segoe UI Semibold", 13F),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(23, 32, 49),
        };
        root.Controls.Add(queueTitle, 0, 9);
        root.SetColumnSpan(queueTitle, 2);
        root.Controls.Add(BuildOpportunityQueueToolbar(), 0, 10);
        root.SetColumnSpan(root.GetControlFromPosition(0, 10)!, 2);
        ConfigureOpportunityQueueGrid();
        root.Controls.Add(_queueGrid, 0, 11);
        root.SetColumnSpan(_queueGrid, 2);

        _statusTextBox.Dock = DockStyle.Fill;
        _statusTextBox.Multiline = true;
        _statusTextBox.ReadOnly = true;
        _statusTextBox.ScrollBars = ScrollBars.Vertical;
        _statusTextBox.BackColor = Color.White;
        root.Controls.Add(_statusTextBox, 0, 12);
        root.SetColumnSpan(_statusTextBox, 2);

        UiStyle.AttachSidebarNav(this, "automation");
    }

    private Control BuildOpportunityQueueToolbar()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 2 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        panel.Controls.Add(LabelFor("Magaza turu"), 0, 0);
        _queueShopTypeTextBox.Dock = DockStyle.Fill;
        _queueShopTypeTextBox.PlaceholderText = "Orn: 3D cosplay prop";
        panel.Controls.Add(_queueShopTypeTextBox, 1, 0);
        panel.Controls.Add(LabelFor("Anahtar kelime"), 2, 0);
        _queueKeywordTextBox.Dock = DockStyle.Fill;
        _queueKeywordTextBox.PlaceholderText = "Orn: fantasy bust";
        panel.Controls.Add(_queueKeywordTextBox, 3, 0);
        panel.Controls.Add(LabelFor("Limit"), 4, 0);
        _queueLimitInput.Dock = DockStyle.Fill;
        panel.Controls.Add(_queueLimitInput, 5, 0);
        var run = CreateButton("Rapor Uret");
        run.Click += (_, _) => RunOpportunityQueueReport();
        panel.Controls.Add(run, 6, 0);
        var export = CreateButton("CSV Aktar");
        export.Click += (_, _) => ExportOpportunityQueueCsv();
        panel.Controls.Add(export, 7, 0);

        panel.Controls.Add(LabelFor("Toplu islem"), 0, 1);
        var testList = CreateButton("Teste Al");
        testList.Click += (_, _) => ApplyQueueBatchAction(OpportunityBatchActionType.MoveToTestList);
        panel.Controls.Add(testList, 1, 1);
        var aiDraft = CreateButton("AI Taslak");
        aiDraft.Click += (_, _) => ApplyQueueBatchAction(OpportunityBatchActionType.GenerateAiDraft);
        panel.Controls.Add(aiDraft, 2, 1);
        var manual = CreateButton("Manuel");
        manual.Click += (_, _) => ApplyQueueBatchAction(OpportunityBatchActionType.MarkManualReview);
        panel.Controls.Add(manual, 3, 1);
        var reject = CreateButton("Reddet");
        reject.BackColor = Color.FromArgb(180, 58, 58);
        reject.Click += (_, _) => ApplyQueueBatchAction(OpportunityBatchActionType.Reject);
        panel.Controls.Add(reject, 4, 1);

        _queueSummaryLabel.Dock = DockStyle.Fill;
        _queueSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;
        _queueSummaryLabel.ForeColor = Color.FromArgb(82, 93, 110);
        _queueSummaryLabel.Text = "Urun arayip otomasyon aksiyon raporu uret.";
        panel.Controls.Add(_queueSummaryLabel, 8, 0);
        panel.SetColumnSpan(_queueSummaryLabel, 2);
        return panel;
    }

    private void ConfigureOpportunityQueueGrid()
    {
        _queueGrid.Dock = DockStyle.Fill;
        _queueGrid.AutoGenerateColumns = false;
        _queueGrid.AllowUserToAddRows = false;
        _queueGrid.AllowUserToDeleteRows = false;
        _queueGrid.ReadOnly = false;
        _queueGrid.RowHeadersVisible = false;
        _queueGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _queueGrid.BackgroundColor = Color.White;
        _queueGrid.DataSource = _queueBindingSource;
        _queueGrid.CellDoubleClick += (_, _) => OpenSelectedQueueUrl();
        _queueGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "Sec",
            DataPropertyName = nameof(ExternalProductIdea.IsSelected),
            Width = 48,
            ReadOnly = false,
        });
        AddQueueColumn("Aksiyon", nameof(ExternalProductIdea.RecommendedAction), 130);
        AddQueueColumn("Oncelik", nameof(ExternalProductIdea.ActionPriority), 85);
        AddQueueColumn("Firsat", nameof(ExternalProductIdea.Opportunity), 70);
        AddQueueColumn("Karar", nameof(ExternalProductIdea.DecisionGroup), 120);
        AddQueueColumn("Kuyruk", nameof(ExternalProductIdea.UserStatus), 110);
        AddQueueColumn("Urun", nameof(ExternalProductIdea.Title), 360, true);
        AddQueueColumn("Kaynak", nameof(ExternalProductIdea.Source), 120);
        AddQueueColumn("Fiyat", nameof(ExternalProductIdea.Price), 110);
        AddQueueColumn("Risk", nameof(ExternalProductIdea.Risk), 70);
        AddQueueColumn("Neden", nameof(ExternalProductIdea.ActionReason), 460);
        AddQueueColumn("Link", nameof(ExternalProductIdea.ProductUrl), 320);
    }

    private void RunOpportunityQueueReport()
    {
        try
        {
            UseWaitCursor = true;
            var rows = _externalSearchService
                .BuildSearchIdeas(_queueShopTypeTextBox.Text, _queueKeywordTextBox.Text, [])
                .Take((int)_queueLimitInput.Value)
                .ToList();
            var report = _externalSearchService.BuildAutomationReport(rows);
            _queueRows = rows
                .OrderBy(row => PriorityRank(row.ActionPriority))
                .ThenByDescending(row => row.OpportunityScore)
                .ToList();
            _queueBindingSource.DataSource = _queueRows;
            RefreshOpportunityQueueSummary(report);
            AppendStatus($"Firsat kuyrugu raporu uretildi: {_queueRows.Count} satir.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Firsat kuyrugu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            AppendStatus("Firsat kuyrugu raporu uretilemedi.");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ExportOpportunityQueueCsv()
    {
        if (_queueRows.Count == 0)
        {
            MessageBox.Show(this, "Aktarilacak firsat raporu yok.", "CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Firsat kuyrugu CSV kaydet",
            Filter = "CSV dosyasi|*.csv",
            FileName = $"firsat-kuyrugu-{DateTime.Now:yyyyMMdd-HHmm}.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var selectedRows = GetSelectedQueueRows();
        var rowsToExport = selectedRows.Count == 0 ? _queueRows : selectedRows;
        File.WriteAllText(dialog.FileName, BuildOpportunityQueueCsv(rowsToExport), Encoding.UTF8);
        AppendStatus($"Firsat kuyrugu CSV aktarildi: {dialog.FileName}");
    }

    private string BuildOpportunityQueueCsv(IReadOnlyList<ExternalProductIdea> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Aksiyon,Oncelik,Firsat,Karar,Kuyruk,Urun,Kaynak,Fiyat,Risk,Neden,Link");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", [
                Csv(row.RecommendedAction),
                Csv(row.ActionPriority),
                Csv(row.Opportunity),
                Csv(row.DecisionGroup),
                Csv(row.UserStatus),
                Csv(row.Title),
                Csv(row.Source),
                Csv(row.Price),
                Csv(row.Risk),
                Csv(row.ActionReason),
                Csv(row.ProductUrl),
            ]));
        }

        return builder.ToString();
    }

    private void ApplyQueueBatchAction(OpportunityBatchActionType action)
    {
        var selectedRows = GetSelectedQueueRows();
        if (selectedRows.Count == 0)
        {
            MessageBox.Show(this, "Once kuyrukta islem yapilacak satirlari sec.", "Toplu islem", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var riskyCount = selectedRows.Count(IsRisky);
        if (action == OpportunityBatchActionType.GenerateAiDraft && riskyCount > 0)
        {
            var riskyDecision = MessageBox.Show(
                this,
                $"{riskyCount} riskli urun secili. Bu urunleri AI taslak kuyruguna almak istiyor musun?",
                "Riskli AI taslak onayi",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (riskyDecision != DialogResult.Yes) return;
        }

        var decision = MessageBox.Show(
            this,
            $"{selectedRows.Count} secili urun icin '{BatchActionLabel(action)}' islemi uygulanacak. Devam edilsin mi?",
            "Toplu islem onayi",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);
        if (decision != DialogResult.Yes) return;

        var request = new OpportunityBatchActionRequest(
            action,
            selectedRows.Select(ToAutomationInput).ToList(),
            AllowRiskyAiDraft: action == OpportunityBatchActionType.GenerateAiDraft && riskyCount > 0);
        var result = _batchActionService.Apply(request);

        for (var index = 0; index < selectedRows.Count; index++)
        {
            var itemResult = result.Items[index];
            if (!itemResult.Success) continue;
            selectedRows[index].UserStatus = ExternalMarketplaceSearchService.StatusLabel(itemResult.NewStatus);
            RefreshQueueRecommendation(selectedRows[index]);
        }

        _queueGrid.Refresh();
        RefreshOpportunityQueueSummary();
        AppendStatus($"{BatchActionLabel(action)}: {result.SuccessCount} basarili, {result.BlockedCount} engellendi.");
    }

    private List<ExternalProductIdea> GetSelectedQueueRows()
    {
        _queueGrid.EndEdit();
        return _queueRows.Where(row => row.IsSelected).ToList();
    }

    private void RefreshQueueRecommendation(ExternalProductIdea row)
    {
        var recommendation = _externalSearchService.Recommend(row);
        row.RecommendedAction = recommendation.Action;
        row.ActionPriority = recommendation.Priority;
        row.ActionReason = recommendation.Reason;
    }

    private void RefreshOpportunityQueueSummary()
    {
        if (_queueRows.Count == 0)
        {
            _queueSummaryLabel.Text = "Urun arayip otomasyon aksiyon raporu uret.";
            return;
        }

        RefreshOpportunityQueueSummary(_externalSearchService.BuildAutomationReport(_queueRows));
    }

    private void RefreshOpportunityQueueSummary(OpportunityAutomationReport report)
    {
        _queueSummaryLabel.Text =
            $"Toplam {report.Summary.Total} | Guclu {report.Summary.Strong} | Test {report.Summary.WorthTesting} | Riskli {report.Summary.Risky} | AI taslak {report.Summary.DraftReady} | Ort. {report.Summary.AverageOpportunity:0.#}";
    }

    private static OpportunityAutomationInput ToAutomationInput(ExternalProductIdea row) =>
        new(
            row.Title,
            row.OpportunityScore,
            row.DemandScore,
            row.RiskScore,
            row.EtsyFitScore,
            ExternalMarketplaceSearchService.ParseDecisionGroup(row.DecisionGroup),
            ExternalMarketplaceSearchService.ParseStatus(row.UserStatus));

    private static bool IsRisky(ExternalProductIdea row) =>
        row.RiskScore >= 70 || ExternalMarketplaceSearchService.ParseDecisionGroup(row.DecisionGroup) == OpportunityDecisionGroup.Risky;

    private static string BatchActionLabel(OpportunityBatchActionType action) => action switch
    {
        OpportunityBatchActionType.MoveToTestList => "Test listesine al",
        OpportunityBatchActionType.GenerateAiDraft => "AI taslak uret",
        OpportunityBatchActionType.MarkManualReview => "Manuel inceleme",
        OpportunityBatchActionType.Reject => "Reddet",
        _ => "Toplu islem",
    };

    private void OpenSelectedQueueUrl()
    {
        if (_queueBindingSource.Current is not ExternalProductIdea idea || string.IsNullOrWhiteSpace(idea.ProductUrl)) return;
        Process.Start(new ProcessStartInfo(idea.ProductUrl) { UseShellExecute = true });
    }

    private void AddQueueColumn(string header, string property, int width, bool fill = false)
    {
        _queueGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            AutoSizeMode = fill ? DataGridViewAutoSizeColumnMode.Fill : DataGridViewAutoSizeColumnMode.None,
            ReadOnly = true,
        });
    }

    private static int PriorityRank(string priority) => priority switch
    {
        "Yuksek" => 0,
        "Orta" => 1,
        _ => 2,
    };

    private static string Csv(string value)
    {
        var clean = value.Replace("\"", "\"\"");
        return $"\"{clean}\"";
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
        AppendStatus($"Arka plan log dosyasi: {AutomationHeadlessRunner.LogPath}");
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

    private async Task CreateWindowsTaskAsync()
    {
        try
        {
            SaveSettings();
            UseWaitCursor = true;
            var result = await taskScheduler.CreateAsync(Application.ExecutablePath, _settings.IntervalHours);
            AppendTaskResult(result, "Windows gorevi olusturuldu.", "Windows gorevi olusturulamadi.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Windows gorevi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task QueryWindowsTaskAsync()
    {
        try
        {
            UseWaitCursor = true;
            var result = await taskScheduler.QueryAsync();
            AppendTaskResult(result, "Windows gorev durumu alindi.", "Windows gorevi bulunamadi veya okunamadi.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Windows gorevi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task RunWindowsTaskAsync()
    {
        try
        {
            UseWaitCursor = true;
            var result = await taskScheduler.RunAsync();
            AppendTaskResult(result, "Windows gorevi baslatildi.", "Windows gorevi baslatilamadi.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Windows gorevi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task DeleteWindowsTaskAsync()
    {
        try
        {
            UseWaitCursor = true;
            var result = await taskScheduler.DeleteAsync();
            AppendTaskResult(result, "Windows gorevi kaldirildi.", "Windows gorevi kaldirilamadi.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Windows gorevi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void AppendTaskResult(WindowsTaskResult result, string successMessage, string failureMessage)
    {
        AppendStatus(result.Success ? successMessage : failureMessage);
        if (!string.IsNullOrWhiteSpace(result.Output)) AppendStatus(result.Output);
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

    private void ApiTelemetryOnEventPublished(ApiRequestEvent item)
    {
        if (IsDisposed) return;
        if (InvokeRequired) BeginInvoke(() => AppendApiEvent(item));
        else AppendApiEvent(item);
    }

    private void AppendApiEvent(ApiRequestEvent item)
    {
        var status = item.StatusCode.HasValue ? $"HTTP {item.StatusCode}" : "HTTP -";
        var wait = item.Delay > TimeSpan.Zero ? $" | bekleme {item.Delay.TotalSeconds:0.##} sn" : "";
        AppendStatus($"API | {status} | deneme {item.Attempt} | kuyruk {item.QueueDepth}{wait} | {item.Message}");
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
        ForeColor = UiStyle.TextDark,
    };

    private static Button CreateButton(string text, bool isSecondary = false, bool isDanger = false)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = isDanger ? UiStyle.DangerColor : (isSecondary ? UiStyle.SecondaryColor : UiStyle.PrimaryColor),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = UiStyle.SemiboldBaseFont,
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false,
            Margin = new Padding(6, 2, 0, 2),
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isDanger ? Color.FromArgb(220, 38, 38) : (isSecondary ? UiStyle.SecondaryHover : UiStyle.PrimaryHover);
        return button;
    }
}
