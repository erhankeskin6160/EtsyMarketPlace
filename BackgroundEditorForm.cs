namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

/// <summary>
/// Geriye dönük uyumluluk için AI Arka Plan Stüdyosu pencere sarmalayıcısı.
/// Çekirdek iş mantığı BatchStudioPanelControl içinde yer alır.
/// </summary>
internal sealed class BackgroundEditorForm : Form
{
    private readonly BatchStudioPanelControl _studioControl;

    public BackgroundEditorForm(
        EtsyApiClient? apiClient = null,
        AiOptimizationSettings? aiSettings = null,
        string? initialImagePath = null,
        long? initialListingId = null)
    {
        Text = "🖼️ Etsy AI Arka Plan Stüdyosu & Toplu Düzenleyici";
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 740);
        BackColor = Color.FromArgb(15, 23, 42); // Slate 900
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        _studioControl = new BatchStudioPanelControl(apiClient, aiSettings);
        _studioControl.Dock = DockStyle.Fill;
        Controls.Add(_studioControl);

        if (!string.IsNullOrWhiteSpace(initialImagePath))
        {
            _studioControl.LoadInitialImage(initialImagePath, initialListingId);
        }
    }
}
