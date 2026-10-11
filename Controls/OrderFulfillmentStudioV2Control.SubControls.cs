namespace SimilarProductsWinForms.Controls;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;
using EtsyMarketPlace.Domain.Shipping;

public sealed partial class OrderFulfillmentStudioV2Control
{
    /// <summary>Yakalama sürerken gösterilen basit durum penceresi.</summary>
    private sealed class CaptureProgressForm : Form
    {
        private readonly Label _status;
        private readonly CancellationTokenSource _cts;

        public CaptureProgressForm(CancellationTokenSource cts)
        {
            _cts = cts;
            Text = "Aras Şablonu Yakalanıyor";
            Size = new Size(580, 210);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            BackColor = UiStyle.BackgroundColor;
            ForeColor = UiStyle.TextDark;
            Font = UiStyle.BaseFont;

            _status = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Hazırlanıyor...",
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(16),
                ForeColor = UiStyle.TextDark
            };

            var cancel = UiStyle.CreateButton("İptal", isSecondary: true);
            cancel.Dock = DockStyle.Bottom;
            cancel.Height = 36;
            cancel.Click += (s, e) =>
            {
                _cts.Cancel();
                SetStatus("İptal ediliyor...");
            };

            Controls.Add(_status);
            Controls.Add(cancel);
        }

        public void SetStatus(string message)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => SetStatus(message)));
                }
                catch (Exception caught) { AppLog.Swallowed(caught); }

                return;
            }

            _status.Text = message;
        }
    }

    #region iç modeller ve satır kontrolleri

    /// <summary>Tek bir taşıyıcı teklifi.</summary>
    public sealed class OrderQuote
    {
        public string ProviderName { get; set; } = string.Empty;
        public string CarrierName { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string SubCarrier { get; set; } = string.Empty;
        public string ServiceCode { get; set; } = string.Empty;
        public string ServiceType { get; set; } = string.Empty;
        public decimal PriceUsd { get; set; }
        public decimal PriceTry { get; set; }
        public string DeliveryText { get; set; } = string.Empty;
        public bool IsCheapest { get; set; }
        public bool IsCreationSupported { get; set; }
        public QuoteSource Source { get; set; } = QuoteSource.Estimated;
        public string Note { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 48.855m;
    }

    /// <summary>Yuvarlatılmış köşeli, tema token'larına bağlı kart paneli.</summary>
    internal sealed class ThemedCard : Panel
    {
        private int _cornerRadius = 12;

        public ThemedCard()
        {
            UiStyle.SetDoubleBuffered(this);
            BackColor = UiStyle.CardBackground;
            Padding = new Padding(0);
        }

        public int CornerRadius
        {
            get => _cornerRadius;
            set
            {
                _cornerRadius = Math.Max(0, value);
                UpdateRegion();
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            if (_cornerRadius <= 0)
            {
                Region = null;
                return;
            }

            Region?.Dispose();
            Region = new Region(UiStyle.CreateRoundedRectanglePath(new Rectangle(0, 0, Width, Height), _cornerRadius));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_cornerRadius <= 0 || Width <= 2 || Height <= 2)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = UiStyle.CreateRoundedRectanglePath(new Rectangle(0, 0, Width - 1, Height - 1), _cornerRadius);
            using var pen = new Pen(UiStyle.BorderColor, 1f);
            e.Graphics.DrawPath(pen, path);
        }
    }

    /// <summary>Sipariş kuyruğundaki tek satır.</summary>
    internal sealed class OrderRowControl : Control
    {
        private static readonly Font IdFont = new("Segoe UI Semibold", 10f);
        private static readonly Font MetaFont = new("Segoe UI", 8.5f);
        private static readonly Font PillFont = new("Segoe UI", 8f, FontStyle.Bold);

        public EtsyOrderFulfillmentItem Order { get; }
        public bool IsSelected { get; set; }
        public event EventHandler? OrderSelected;

        public OrderRowControl(EtsyOrderFulfillmentItem order)
        {
            Order = order;
            UiStyle.SetDoubleBuffered(this);
            Height = 96;
            Cursor = Cursors.Hand;
            BackColor = UiStyle.CardBackground;
            MouseClick += (s, e) => OrderSelected?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = IsSelected
                ? Blend(UiStyle.BackgroundColor, UiStyle.AccentColor, 0.14)
                : UiStyle.BackgroundColor;

            using (var path = UiStyle.CreateRoundedRectanglePath(bounds, 10))
            {
                using var brush = new SolidBrush(fill);
                g.FillPath(brush, path);
                using var pen = new Pen(IsSelected ? UiStyle.AccentColor : UiStyle.BorderColor, IsSelected ? 1.6f : 1f);
                g.DrawPath(pen, path);
            }

            bool shipped = string.Equals(Order.Status, "Shipped", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(Order.Status, "Fulfilled", StringComparison.OrdinalIgnoreCase);

            using (var bar = new SolidBrush(shipped ? UiStyle.SuccessColor : UiStyle.WarningColor))
            {
                using var path = UiStyle.CreateRoundedRectanglePath(new Rectangle(0, 8, 4, Height - 16), 2);
                g.FillPath(bar, path);
            }

            int x = 16;
            int y = 12;

            TextRenderer.DrawText(g, Order.OrderNumber, IdFont, new Point(x, y), UiStyle.TextDark, TextFormatFlags.NoPadding);
            y += 22;

            string meta1 = string.Join("  ·  ", new[] { Order.BuyerName, CountryLabel() }.Where(s => !string.IsNullOrWhiteSpace(s)));
            TextRenderer.DrawText(g, meta1, MetaFont, new Point(x, y), UiStyle.TextMuted, TextFormatFlags.NoPadding);
            y += 18;

            string shippingType = string.IsNullOrWhiteSpace(Order.IossNumber) ? "Standart ihracat" : "IOSS";
            string meta2 = $"{Order.Items.Count} kalem  ·  {Order.TotalWeightKg:N2} kg  ·  {shippingType}";
            TextRenderer.DrawText(g, meta2, MetaFont, new Point(x, y), UiStyle.TextMuted, TextFormatFlags.NoPadding);

            // durum rozeti (sağ üst)
            string badge = shipped ? "Gönderildi" : "Bekleyen";
            Color badgeBg = shipped ? UiStyle.SuccessColor : UiStyle.WarningColor;
            Size badgeSize = TextRenderer.MeasureText(badge, PillFont);
            var badgeRect = new Rectangle(Width - badgeSize.Width - 26, 12, badgeSize.Width + 14, 20);
            using (var path = UiStyle.CreateRoundedRectanglePath(badgeRect, 10))
            {
                using var brush = new SolidBrush(Color.FromArgb(46, badgeBg));
                g.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(140, badgeBg), 1f);
                g.DrawPath(pen, path);
            }

            TextRenderer.DrawText(g, badge, PillFont, badgeRect, badgeBg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private string CountryLabel()
        {
            if (!string.IsNullOrWhiteSpace(Order.CountryName))
            {
                return $"{Order.CountryName} ({Order.CountryCode})";
            }

            return Order.CountryCode;
        }

        internal static Color Blend(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(
                (int)(a.R + ((b.R - a.R) * t)),
                (int)(a.G + ((b.G - a.G) * t)),
                (int)(a.B + ((b.B - a.B) * t)));
        }
    }

    /// <summary>Taşıyıcı karşılaştırma listesindeki tek satır.</summary>
    internal sealed class QuoteRowControl : Control
    {
        private static readonly Font NameFont = new("Segoe UI Semibold", 8.8f);
        private static readonly Font MetaFont = new("Segoe UI", 8.5f);
        private static readonly Font PriceFont = new("Segoe UI Semibold", 12f, FontStyle.Bold);
        private static readonly Font PillFont = new("Segoe UI", 8f, FontStyle.Bold);

        private static readonly Color SelectedAccent = Color.FromArgb(167, 139, 250); // seçili teklif moru

        private Image? _logo;
        private bool _isSelected;

        public OrderQuote Quote { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                Invalidate();
            }
        }

        public event EventHandler? Selected;

        public QuoteRowControl(OrderQuote quote)
        {
            Quote = quote;
            UiStyle.SetDoubleBuffered(this);
            Height = 88;
            Cursor = Cursors.Hand;
            BackColor = UiStyle.CardBackground;

            try
            {
                // Sağlayıcı markası: Aras satırında Aras, ShipEntegra satırında ShipEntegra,
                // Ship to More satırında Ship to More logosu görünür (alt taşıyıcı logosu değil).
                _logo = ShippingLogoHelper.GetProviderLogo(quote.ProviderName);
            }
            catch
            {
                _logo = null;
            }

            MouseClick += (s, e) => Selected?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color baseFill = UiStyle.BackgroundColor;
            if (IsSelected)
            {
                baseFill = OrderRowControl.Blend(baseFill, SelectedAccent, 0.16);
            }
            else if (Quote.IsCheapest)
            {
                baseFill = OrderRowControl.Blend(baseFill, UiStyle.SuccessColor, 0.12);
            }

            Color stroke = IsSelected
                ? SelectedAccent
                : (Quote.IsCheapest ? UiStyle.SuccessColor : UiStyle.BorderColor);
            float strokeWidth = IsSelected || Quote.IsCheapest ? 1.5f : 1f;

            using (var path = UiStyle.CreateRoundedRectanglePath(bounds, 10))
            {
                using var brush = new SolidBrush(baseFill);
                g.FillPath(brush, path);
                using var pen = new Pen(stroke, strokeWidth);
                g.DrawPath(pen, path);
            }

            int x = 12;
            var logoRect = new Rectangle(x, 12, 38, 26);
            if (_logo != null)
            {
                ShippingLogoHelper.DrawImagePreserveAspect(g, _logo, logoRect);
            }

            string usd = $"${Quote.PriceUsd:N2}";
            string tryTextPreview = $"{Quote.PriceTry:N2} ₺";
            Size usdSizePreview = TextRenderer.MeasureText(usd, PriceFont);
            Size trySizePreview = TextRenderer.MeasureText(tryTextPreview, MetaFont);
            int priceBlockWidth = Math.Max(usdSizePreview.Width, trySizePreview.Width) + 14;

            int textX = x + 46;
            int textWidth = Math.Max(80, Width - textX - priceBlockWidth - 10);

            // Saglayici adi ikinci satirda zaten yaziyor; baslikta tekrar etmesin.
            string displayName = Quote.ServiceName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(Quote.ProviderName))
            {
                string prefix = Quote.ProviderName.Trim() + " ";
                if (displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    displayName = displayName.Substring(prefix.Length).TrimStart();
                }
            }

            TextRenderer.DrawText(g, displayName, NameFont,
                new Rectangle(textX, 10, textWidth, 18), UiStyle.TextDark,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

            string metaLine = string.IsNullOrWhiteSpace(Quote.DeliveryText)
                ? Quote.ProviderName
                : $"{Quote.ProviderName}  ·  {Quote.DeliveryText}";
            TextRenderer.DrawText(g, metaLine, MetaFont,
                new Rectangle(textX, 28, textWidth, 16), UiStyle.TextMuted,
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter);

            Size usdSize = TextRenderer.MeasureText(usd, PriceFont);
            TextRenderer.DrawText(g, usd, PriceFont, new Point(Width - usdSize.Width - 14, 10), UiStyle.TextDark, TextFormatFlags.NoPadding);

            string tryText = $"{Quote.PriceTry:N2} ₺";
            Size trySize = TextRenderer.MeasureText(tryText, MetaFont);
            TextRenderer.DrawText(g, tryText, MetaFont, new Point(Width - trySize.Width - 14, 32), UiStyle.TextMuted, TextFormatFlags.NoPadding);

            int pillX = textX;
            int pillY = 54;

            pillX = DrawPill(g, CarrierQuoteTrust.ToBadgeText(Quote.Source), PillFont, pillX, pillY,
                Quote.Source == QuoteSource.Live ? UiStyle.SuccessColor : UiStyle.WarningColor);

            if (IsSelected)
            {
                pillX = DrawPill(g, "SEÇİLİ", PillFont, pillX, pillY, SelectedAccent);
            }

            if (Quote.IsCheapest)
            {
                pillX = DrawPill(g, "EN UCUZ", PillFont, pillX, pillY, UiStyle.SuccessColor);
            }

            if (!Quote.IsCreationSupported)
            {
                DrawPill(g, "Gönderi kapalı", PillFont, pillX, pillY, UiStyle.DangerColor);
            }
        }

        private static int DrawPill(Graphics g, string text, Font font, int x, int y, Color accent)
        {
            Size size = TextRenderer.MeasureText(text, font);
            var rect = new Rectangle(x, y, size.Width + 14, 19);

            using (var path = UiStyle.CreateRoundedRectanglePath(rect, 9))
            {
                using var brush = new SolidBrush(Color.FromArgb(46, accent));
                g.FillPath(brush, path);
                using var pen = new Pen(Color.FromArgb(140, accent), 1f);
                g.DrawPath(pen, path);
            }

            var textRect = new Rectangle(rect.X + 7, rect.Y + 1, rect.Width - 14, rect.Height - 2);
            TextRenderer.DrawText(g, text, font, textRect, accent, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return x + rect.Width + 6;
        }
    }

    /// <summary>
    /// Ship to More alıcı telefon ister; Etsy siparişlerinde telefon alanı çoğu
    /// zaman boş geldiği için gönderi oluşturmadan önce küçük bir giriş istenir.
    /// </summary>
    internal sealed class PhoneNumberPromptForm : Form
    {
        private readonly TextBox _txtPhone = new();

        public static string? Ask(IWin32Window owner, string buyerName)
        {
            using var form = new PhoneNumberPromptForm(buyerName);
            return form.ShowDialog(owner) == DialogResult.OK ? form._txtPhone.Text.Trim() : null;
        }

        private PhoneNumberPromptForm(string buyerName)
        {
            Text = "Alıcı Telefonu Gerekli";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(400, 158);
            BackColor = UiStyle.BackgroundColor;
            ForeColor = UiStyle.TextDark;

            var lblInfo = new Label
            {
                Text = $"{buyerName} için alıcı telefonu gerekli.\nTelefon numarasını girin:",
                Location = new Point(16, 14),
                Size = new Size(368, 42),
                ForeColor = UiStyle.TextDark,
                Font = new Font("Segoe UI", 9.5f)
            };

            _txtPhone.Location = new Point(16, 60);
            _txtPhone.Width = 368;
            _txtPhone.Font = new Font("Segoe UI", 10.5f);
            _txtPhone.BorderStyle = BorderStyle.FixedSingle;

            var btnOk = UiStyle.CreateButton("Kaydet", isSecondary: false);
            btnOk.Dock = DockStyle.None;
            btnOk.Size = new Size(120, 32);
            btnOk.Location = new Point(160, 112);
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_txtPhone.Text))
                {
                    MessageBox.Show(this, "Telefon numarası boş olamaz.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DialogResult = DialogResult.OK;
                Close();
            };

            var btnCancel = UiStyle.CreateButton("Vazgeç", isSecondary: true);
            btnCancel.Dock = DockStyle.None;
            btnCancel.Size = new Size(110, 32);
            btnCancel.Location = new Point(288, 112);
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.Add(lblInfo);
            Controls.Add(_txtPhone);
            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }
    }

    #endregion
}
