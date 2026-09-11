namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Windows.Forms;

/// <summary>
/// Modern studio lighting, camera angle, and negative prompt modifier control.
/// </summary>
public class StudioLightingSelectorControl : UserControl
{
    public event EventHandler? SettingsChanged;

    private readonly ComboBox _cboLighting = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _cboCamera = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ModernCheckBox _chkNegativeFilter = new()
    {
        Text = "🛡️ AI Bozulma Önleyici (Anti-Distortion & Clean)",
        Checked = true,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
        ForeColor = Color.FromArgb(129, 140, 248)
    };

    public string SelectedLighting => _cboLighting.SelectedItem?.ToString() ?? "☀️ Doğal Sabah Işığı";
    public string SelectedCamera => _cboCamera.SelectedItem?.ToString() ?? "📐 Göz Hizası (50mm)";
    public bool IsNegativeFilterActive => _chkNegativeFilter.Checked;

    public StudioLightingSelectorControl()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Font = new Font("Segoe UI", 9F);
        AutoSize = true;
        BuildUi();
    }

    private void BuildUi()
    {
        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        // 1. Lighting Style
        layout.Controls.Add(new Label
        {
            Text = "💡 Stüdyo Işık Atmosferi:",
            AutoSize = true,
            ForeColor = Color.FromArgb(226, 232, 240),
            Font = new Font("Segoe UI Semibold", 8.8F),
            Margin = new Padding(0, 2, 0, 2)
        });

        _cboLighting.Width = 330;
        _cboLighting.Font = new Font("Segoe UI", 9F);
        _cboLighting.Items.AddRange([
            "☀️ Doğal Sabah Işığı (Natural Morning Light)",
            "💡 Stüdyo Softbox (Diffuse Catalog Strobe)",
            "🌙 Sinematik Dramatik (Moody Chiaroscuro)",
            "🟣 Neon / Cyber Glow (Cyan & Purple LEDs)",
            "🔥 Altın Saat / Gün Batımı (Warm Golden Hour)",
            "⚪ Nötr Katalog Katalizörü (Zero Shadow Infinite)"
        ]);
        _cboLighting.SelectedIndex = 0;
        _cboLighting.SelectedIndexChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        layout.Controls.Add(_cboLighting);

        // 2. Camera Angle & Lens
        layout.Controls.Add(new Label
        {
            Text = "📷 Kamera Açısı & Çekim Ölçeği:",
            AutoSize = true,
            ForeColor = Color.FromArgb(226, 232, 240),
            Font = new Font("Segoe UI Semibold", 8.8F),
            Margin = new Padding(0, 6, 0, 2)
        });

        _cboCamera.Width = 330;
        _cboCamera.Font = new Font("Segoe UI", 9F);
        _cboCamera.Items.AddRange([
            "📐 Göz Hizası (50mm Commercial Eye-Level)",
            "🔍 Detay / Makro (85mm Shallow Bokeh)",
            "📦 Düz Üstten (Flat Lay 90° Top-Down)",
            "🏠 Geniş Yaşam Alanı (Wide Lifestyle Room)"
        ]);
        _cboCamera.SelectedIndex = 0;
        _cboCamera.SelectedIndexChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        layout.Controls.Add(_cboCamera);

        // 3. Negative Filter Checkbox
        _chkNegativeFilter.Margin = new Padding(0, 6, 0, 4);
        _chkNegativeFilter.CheckedChanged += (_, _) => SettingsChanged?.Invoke(this, EventArgs.Empty);
        layout.Controls.Add(_chkNegativeFilter);

        Controls.Add(layout);
    }

    /// <summary>
    /// Enriches a raw prompt with the selected lighting, lens parameters, and quality modifiers.
    /// </summary>
    public string EnrichPrompt(string basePrompt)
    {
        if (string.IsNullOrWhiteSpace(basePrompt)) return basePrompt;

        string lightingModifier = SelectedLighting switch
        {
            var s when s.Contains("Doğal Sabah") => "bathed in soft warm morning window light, subtle realistic sun flare, gentle ambient morning glow",
            var s when s.Contains("Stüdyo Softbox") => "illuminated by dual professional commercial photography softbox diffusers, crisp balanced highlights, zero harsh glares",
            var s when s.Contains("Sinematik") => "dramatic cinematic chiaroscuro lighting, strong rim light defining silhouettes, deep rich contact shadows",
            var s when s.Contains("Neon") => "ambient cyan and magenta neon LED backlighting, subtle glossy surface reflections, sleek modern streamer vibe",
            var s when s.Contains("Altın Saat") => "gorgeous golden hour sunset sunlight streaming, warm amber atmospheric haze, luminous backlight glow",
            _ => "crisp neutral clean commercial catalog lighting, even exposure"
        };

        string cameraModifier = SelectedCamera switch
        {
            var s when s.Contains("Detay / Makro") => "shot on 85mm f/1.8 macro lens, exquisite crisp textures, creamy blurred background bokeh, ultra-detailed",
            var s when s.Contains("Düz Üstten") => "overhead flat-lay tabletop 90-degree angle, neatly organized composition, sharp edge-to-edge focus",
            var s when s.Contains("Geniş Yaşam") => "wide lifestyle interior shot, environmental context, beautifully styled room, depth of field",
            _ => "shot at eye-level commercial 50mm perspective, natural proportions, 8k resolution"
        };

        string cleanPrompt = basePrompt.Trim().TrimEnd('.');
        string enriched = $"{cleanPrompt}, {lightingModifier}, {cameraModifier}";

        if (IsNegativeFilterActive)
        {
            enriched += ", commercial advertising quality, ultra-sharp focus, photorealistic textures";
        }

        return enriched;
    }
}
