namespace SimilarProductsWinForms;

using System.Drawing;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;

/// <summary>
/// Etsy Sipariş &amp; Çoklu Kargo Yönetim Stüdyosu Formu.
/// Dashboard içerisine gömülebilir veya bağımsız pencere olarak açılabilir.
///
/// Görünüm anahtarı:
///   - <see cref="UseV2"/> = true  -> yeni üç bölgeli kokpit (OrderFulfillmentStudioV2Control)
///   - <see cref="UseV2"/> = false -> mevcut dört kolonlu ekran (OrderFulfillmentStudioControl)
/// Eski ekran korunmuştur; yeni ekranda bir sorun görülürse bu anahtarı kapatmak yeterlidir.
/// </summary>
public sealed class OrderFulfillmentStudioForm : Form
{
    /// <summary>
    /// Yeni (V2) arayüzü etkinleştirir. Üretim akışını etkilememek için şimdilik kapalı;
    /// yeni ekranı görmek için true yapın.
    /// </summary>
    public static bool UseV2 { get; set; } = false;

    private readonly Control _studioControl;

    public OrderFulfillmentStudioForm()
    {
        Text = "Etsy Sipariş & Çoklu Kargo Yönetim Stüdyosu";
        Size = new Size(1440, 900);
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = UiStyle.BackgroundColor;

        _studioControl = UseV2
            ? new OrderFulfillmentStudioV2Control { Dock = DockStyle.Fill }
            : new OrderFulfillmentStudioControl { Dock = DockStyle.Fill };

        Controls.Add(_studioControl);
    }
}
