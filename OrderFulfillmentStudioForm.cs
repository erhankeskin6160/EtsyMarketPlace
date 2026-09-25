namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;
using SimilarProductsWinForms.Controls;

/// <summary>
/// Etsy Sipariş & Çoklu Kargo Yönetim Stüdyosu Formu.
/// Dashboard içerisine gömülebilir veya bağımsız pencere olarak açılabilir.
/// </summary>
public sealed class OrderFulfillmentStudioForm : Form
{
    private readonly OrderFulfillmentStudioControl _studioControl;

    public OrderFulfillmentStudioForm()
    {
        Text = "Etsy Sipariş & Çoklu Kargo Yönetim Stüdyosu";
        Size = new Size(1280, 800);
        MinimumSize = new Size(1024, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(11, 17, 32);

        _studioControl = new OrderFulfillmentStudioControl
        {
            Dock = DockStyle.Fill
        };

        Controls.Add(_studioControl);
    }
}
