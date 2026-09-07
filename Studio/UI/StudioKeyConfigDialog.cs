namespace SimilarProductsWinForms.Studio.UI;

using System;
using System.Drawing;
using System.Windows.Forms;
using SimilarProductsWinForms.Studio.Services;

public sealed class StudioKeyConfigDialog : Form
{
    private readonly TextBox _txtPhotoRoom;
    private readonly TextBox _txtGemini;
    private readonly TextBox _txtOpenAi;
    private readonly TextBox _txtBfl;
    private readonly TextBox _txtIdeogram;

    public StudioKeyConfigDialog(string? focusEngine = null)
    {
        Text = "🔑 AI Görsel Studio - API Anahtarları Yapılandırması";
        Size = new Size(580, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            RowCount = 8,
            ColumnCount = 2
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var cfg = StudioConfigurationManager.Current;

        // Title info
        var header = new Label
        {
            Text = "✨ AI Görsel Motorları API Anahtarları",
            Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(129, 140, 248),
            Dock = DockStyle.Fill,
            Height = 32
        };
        root.Controls.Add(header, 0, 0);
        root.SetColumnSpan(header, 2);

        // Subtitle
        var subHeader = new Label
        {
            Text = "Girilen anahtarlar güvenle yerel olarak saklanır ve tek tıkla tüm motorlara bağlanır.",
            ForeColor = Color.FromArgb(148, 163, 184),
            Dock = DockStyle.Fill,
            Height = 26
        };
        root.Controls.Add(subHeader, 0, 1);
        root.SetColumnSpan(subHeader, 2);

        // 1. PhotoRoom Key
        _txtPhotoRoom = CreateKeyBox(cfg.PhotoRoomApiKey, "sk_pr_etsy_... veya sandbox_sk_...");
        root.Controls.Add(CreateLabel("🪞 PhotoRoom API Key:"), 0, 2);
        root.Controls.Add(_txtPhotoRoom, 1, 2);

        // 2. Gemini Key
        _txtGemini = CreateKeyBox(cfg.GoogleGeminiApiKey, "AIzaSy...");
        root.Controls.Add(CreateLabel("🍌 Gemini API Key:"), 0, 3);
        root.Controls.Add(_txtGemini, 1, 3);

        // 3. OpenAI Key
        _txtOpenAi = CreateKeyBox(cfg.OpenAiApiKey, "sk-proj-...");
        root.Controls.Add(CreateLabel("🤖 OpenAI API Key:"), 0, 4);
        root.Controls.Add(_txtOpenAi, 1, 4);

        // 4. BFL FLUX Key
        _txtBfl = CreateKeyBox(cfg.BflApiKey, "bfl-key-...");
        root.Controls.Add(CreateLabel("⚡ BFL (FLUX) API Key:"), 0, 5);
        root.Controls.Add(_txtBfl, 1, 5);

        // 5. Ideogram Key
        _txtIdeogram = CreateKeyBox(cfg.IdeogramApiKey, "ideogram-key-...");
        root.Controls.Add(CreateLabel("🎨 Ideogram API Key:"), 0, 6);
        root.Controls.Add(_txtIdeogram, 1, 6);

        // Action Buttons
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 16, 0, 0)
        };

        var btnClose = new Button
        {
            Text = "Vazgeç",
            DialogResult = DialogResult.Cancel,
            Size = new Size(100, 38),
            BackColor = Color.FromArgb(51, 65, 85),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnPanel.Controls.Add(btnClose);

        var btnSave = new Button
        {
            Text = "💾 Kaydet",
            DialogResult = DialogResult.OK,
            Size = new Size(120, 38),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold)
        };
        btnSave.Click += (_, _) => SaveSettings();
        btnPanel.Controls.Add(btnSave);

        root.Controls.Add(btnPanel, 0, 7);
        root.SetColumnSpan(btnPanel, 2);

        Controls.Add(root);

        // Auto focus relevant box
        if (focusEngine?.Equals("photoroom", StringComparison.OrdinalIgnoreCase) == true)
        {
            ActiveControl = _txtPhotoRoom;
        }
        else if (focusEngine?.Equals("gemini", StringComparison.OrdinalIgnoreCase) == true)
        {
            ActiveControl = _txtGemini;
        }
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240)
        };
    }

    private static TextBox CreateKeyBox(string initialValue, string placeholder)
    {
        return new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9.5F),
            Text = initialValue,
            PlaceholderText = placeholder,
            UseSystemPasswordChar = true,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private void SaveSettings()
    {
        var cfg = StudioConfigurationManager.Current;
        cfg.PhotoRoomApiKey = _txtPhotoRoom.Text.Trim();
        cfg.GoogleGeminiApiKey = _txtGemini.Text.Trim();
        cfg.OpenAiApiKey = _txtOpenAi.Text.Trim();
        cfg.BflApiKey = _txtBfl.Text.Trim();
        cfg.IdeogramApiKey = _txtIdeogram.Text.Trim();

        StudioConfigurationManager.Save(cfg);
        MessageBox.Show(this, "✅ AI motor anahtarları başarıyla kaydedildi ve senkronize edildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
