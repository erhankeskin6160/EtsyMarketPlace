namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Collections.Generic;

internal static class UiStyle
{
    static UiStyle()
    {
        try
        {
            ToolStripManager.Renderer = new ModernDarkMenuRenderer();
        }
        catch { }
    }

    public enum AppTheme
    {
        Light,
        Dark
    }

    public static AppTheme CurrentTheme { get; set; } = AppTheme.Dark;

    // Palette Tokens (Palet 1: Modern Slate & Indigo Navy Design System - Linear / Vercel SaaS Style)
    public static Color PrimaryColor => Color.FromArgb(99, 102, 241);       // #6366F1 - Indigo 500
    public static Color PrimaryHover => Color.FromArgb(79, 70, 229);       // #4F46E5 - Indigo 600
    public static Color SecondaryColor => CurrentTheme == AppTheme.Dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);    // Slate 700 / Slate 200
    public static Color SecondaryHover => CurrentTheme == AppTheme.Dark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225);   // Slate 600 / Slate 300
    public static Color BackgroundColor => CurrentTheme == AppTheme.Dark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252); // Slate 950 #0F172A / Slate 50 #F8FAFC
    public static Color CardBackground => CurrentTheme == AppTheme.Dark ? Color.FromArgb(30, 41, 59) : Color.White;                    // Slate 900 #1E293B / White
    public static Color InputBackground => CurrentTheme == AppTheme.Dark ? Color.FromArgb(30, 41, 59) : Color.White;                   // Slate 900 #1E293B / White
    public static Color TextDark => CurrentTheme == AppTheme.Dark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);          // Slate 50 #F8FAFC (Ultra Crisp White) / Slate 900
    public static Color TextMuted => CurrentTheme == AppTheme.Dark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(71, 85, 105);        // Slate 400 #94A3B8 (Clear Label Gray) / Slate 600
    public static Color BorderColor => CurrentTheme == AppTheme.Dark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);      // Slate 700 #334155 / Slate 200
    public static Color SuccessColor => Color.FromArgb(16, 185, 129);     // #10B981 - Emerald Green
    public static Color DangerColor => Color.FromArgb(239, 68, 68);        // #EF4444 - Rose Red
    public static Color WarningColor => Color.FromArgb(245, 158, 11);       // #F59E0B - Amber Yellow
    public static Color AccentColor => Color.FromArgb(6, 182, 212);        // #06B6D4 - Cyan Glow
    public static Color EtsyColor => Color.FromArgb(249, 115, 22);         // #F97316 - Etsy Warm Terracotta
    public static Color EtsyHover => Color.FromArgb(234, 88, 12);          // #EA580C
    public static Color AiColor => Color.FromArgb(139, 92, 246);           // #8B5CF6 - AI Violet Accent
    public static Color AiHover => Color.FromArgb(124, 58, 237);           // #7C3AED

    // Standard Typography
    public static readonly Font BaseFont = new Font("Segoe UI", 9.5F);
    public static readonly Font SemiboldBaseFont = new Font("Segoe UI Semibold", 9.5F);
    public static readonly Font TitleFont = new Font("Segoe UI Semibold", 18F, FontStyle.Bold);
    public static readonly Font SubtitleFont = new Font("Segoe UI", 9.5F);
    public static readonly Font KpiValueFont = new Font("Segoe UI Semibold", 16F, FontStyle.Bold);

    public static void ApplyTheme(Form form)
    {
        ApplyResponsiveTheme(form);
    }

    public static void ApplyResponsiveTheme(Form form, Size? minSize = null)
    {
        try
        {
            if (System.IO.File.Exists("app.ico"))
            {
                form.Icon = new Icon("app.ico");
            }
        }
        catch { }

        try
        {
            ToolStripManager.Renderer = new ModernDarkMenuRenderer();
        }
        catch { }

        form.BackColor = BackgroundColor;
        form.Font = BaseFont;
        form.MinimumSize = minSize ?? new Size(1024, 680);
        form.AutoScroll = true;
        SetDoubleBuffered(form);
        
        ApplyToControls(form.Controls);

        form.ControlAdded += (sender, e) =>
        {
            if (e.Control != null)
            {
                ApplyToSingleControl(e.Control);
            }
        };

        form.Shown += (sender, e) =>
        {
            ApplyToControls(form.Controls);
        };
    }

    public static void MakeResponsive(Form form, SimilarProductsWinForms.Controls.ModernSidebarNav? sidebar = null)
    {
        ApplyResponsiveTheme(form);
        form.Resize += (sender, e) =>
        {
            if (form.WindowState == FormWindowState.Minimized) return;

            if (sidebar != null)
            {
                bool shouldCollapse = form.ClientSize.Width < 1120;
                if (sidebar.IsCollapsed != shouldCollapse)
                {
                    sidebar.IsCollapsed = shouldCollapse;
                }
            }
        };

        if (sidebar != null && form.ClientSize.Width > 0)
        {
            sidebar.IsCollapsed = form.ClientSize.Width < 1120;
        }
    }

    private static void SetDoubleBuffered(Control control)
    {
        try
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(control, true, null);
        }
        catch { }
    }

    public static void ApplyToControls(Control.ControlCollection controls)
    {
        foreach (Control ctrl in controls)
        {
            ApplyToSingleControl(ctrl);
        }
    }

    public static void ApplyToSingleControl(Control ctrl)
    {
        if (ctrl is Panel pnl)
        {
            if (pnl.BackColor == SystemColors.Control || pnl.BackColor == Color.Empty)
                pnl.BackColor = BackgroundColor;
        }
        else if (ctrl is SimilarProductsWinForms.Controls.ModernCardPanel card)
        {
            card.CardColor = CardBackground;
            card.BorderColor = BorderColor;
        }
        else if (ctrl is GroupBox gb)
        {
            gb.BackColor = BackgroundColor;
            gb.ForeColor = PrimaryColor;
            gb.Font = SemiboldBaseFont;
            ApplyToControls(gb.Controls);
        }
        else if (ctrl is Label lbl)
        {
            if (lbl.ForeColor == SystemColors.ControlText || lbl.ForeColor == Color.Black || lbl.ForeColor == Color.Empty)
            {
                lbl.ForeColor = TextDark;
            }
            if (lbl.Font.Size <= 9F && lbl.Font.Style == FontStyle.Regular)
            {
                lbl.Font = BaseFont;
            }
        }
        else if (ctrl is TextBox txt)
        {
            txt.BorderStyle = BorderStyle.FixedSingle;
            txt.Font = BaseFont;
            txt.BackColor = InputBackground;
            txt.ForeColor = TextDark;
        }
        else if (ctrl is SimilarProductsWinForms.Controls.ModernNumericUpDown mnum)
        {
            mnum.BackColor = InputBackground;
            mnum.ForeColor = TextDark;
        }
        else if (ctrl is NumericUpDown num)
        {
            num.Font = BaseFont;
            num.BackColor = InputBackground;
            num.ForeColor = TextDark;
        }
        else if (ctrl is ListBox lb)
        {
            lb.BorderStyle = BorderStyle.FixedSingle;
            lb.Font = BaseFont;
            lb.BackColor = InputBackground;
            lb.ForeColor = TextDark;
        }
        else if (ctrl is CheckedListBox clb)
        {
            clb.BorderStyle = BorderStyle.FixedSingle;
            clb.Font = BaseFont;
            clb.BackColor = InputBackground;
            clb.ForeColor = TextDark;
        }
        else if (ctrl is ComboBox cb)
        {
            ConfigureComboBox(cb);
        }
        else if (ctrl is SimilarProductsWinForms.Controls.ModernCheckBox mchk)
        {
            mchk.ForeColor = TextDark;
        }
        else if (ctrl is CheckBox chk)
        {
            chk.Font = BaseFont;
            chk.ForeColor = TextDark;
        }
        else if (ctrl is RadioButton rb)
        {
            rb.Font = BaseFont;
            rb.ForeColor = TextDark;
        }
        else if (ctrl is TabControl tc)
        {
            tc.Font = SemiboldBaseFont;
            foreach (TabPage tp in tc.TabPages)
            {
                tp.BackColor = CardBackground;
                ApplyToControls(tp.Controls);
            }
        }
        else if (ctrl is DataGridView grid)
        {
            ConfigureBaseGrid(grid);
        }

        if (ctrl.ContextMenuStrip != null)
        {
            ApplyContextMenuTheme(ctrl.ContextMenuStrip);
        }
        ctrl.ContextMenuStripChanged -= OnControlContextMenuStripChanged;
        ctrl.ContextMenuStripChanged += OnControlContextMenuStripChanged;

        if (ctrl.HasChildren && !(ctrl is GroupBox))
        {
            ApplyToControls(ctrl.Controls);
        }
    }

    public static Button CreateButton(string text, bool isSecondary = false)
    {
        var button = new SimilarProductsWinForms.Controls.ModernButtonControl
        {
            Dock = DockStyle.Fill,
            Text = text,
            NormalColor = isSecondary ? SecondaryColor : PrimaryColor,
            HoverColor = isSecondary ? SecondaryHover : PrimaryHover,
            ForeColor = isSecondary ? TextDark : Color.White,
            Margin = new Padding(4, 2, 4, 2),
        };
        return button;
    }

    public static void AddKpiCard(TableLayoutPanel parent, int column, int row, string title, string key, Dictionary<string, Label> kpis)
    {
        var card = new SimilarProductsWinForms.Controls.ModernCardPanel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 4, 6, 4),
            Padding = new Padding(14, 10, 14, 10),
            CornerRadius = 12,
            CardColor = CardBackground,
            BorderColor = BorderColor,
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = Color.Transparent,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title.ToUpperInvariant(),
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(titleLabel, 0, 0);

        var valueLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "0",
            Font = KpiValueFont,
            ForeColor = TextDark,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(valueLabel, 0, 1);

        card.Controls.Add(layout);
        kpis[key] = valueLabel;
        parent.Controls.Add(card, column, row);
    }

    public static void ConfigureBaseGrid(DataGridView grid)
    {
        SetDoubleBuffered(grid);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AutoGenerateColumns = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.BackgroundColor = CardBackground;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = BorderColor;
        grid.EnableHeadersVisualStyles = false;

        // Header Styling
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersDefaultCellStyle.BackColor = BackgroundColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextMuted;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = BackgroundColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextMuted;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

        // Row Styling
        grid.RowTemplate.Height = 36;
        grid.DefaultCellStyle.BackColor = CardBackground;
        grid.DefaultCellStyle.ForeColor = TextDark;
        grid.DefaultCellStyle.SelectionBackColor = CurrentTheme == AppTheme.Dark ? Color.FromArgb(49, 46, 129) : Color.FromArgb(238, 242, 255); // Indigo 900/100 tint
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Font = BaseFont;

        try
        {
            var property = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            property?.SetValue(grid, true, null);
        }
        catch { }

        // Attach modern scrollbars (hides native win32 scrollbars, adds ModernVScrollBar & ModernHScrollBar)
        SimilarProductsWinForms.Controls.ModernGridScrollAdapter.Attach(grid);

        // Modern custom painting for DataGridViewCheckBoxColumn
        grid.CellPainting += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
            {
                e.PaintBackground(e.CellBounds, true);
                bool isChecked = false;
                if (e.FormattedValue is bool b) isChecked = b;
                else if (e.Value is bool b2) isChecked = b2;

                int boxSize = 16;
                int bx = e.CellBounds.X + (e.CellBounds.Width - boxSize) / 2;
                int by = e.CellBounds.Y + (e.CellBounds.Height - boxSize) / 2;
                var boxRect = new Rectangle(bx, by, boxSize, boxSize);

                if (e.Graphics != null)
                {
                    SimilarProductsWinForms.Controls.ModernCheckBox.DrawBox(
                        e.Graphics,
                        boxRect,
                        isChecked,
                        false,
                        false,
                        false,
                        true,
                        boxSize,
                        4);
                }
                e.Handled = true;
            }
        };

        if (grid.ContextMenuStrip != null)
        {
            ApplyContextMenuTheme(grid.ContextMenuStrip);
        }
        grid.ContextMenuStripChanged -= OnControlContextMenuStripChanged;
        grid.ContextMenuStripChanged += OnControlContextMenuStripChanged;
    }

    public static SimilarProductsWinForms.Controls.ModernSidebarNav? AttachSidebarNav(Form form, string activeItemId, Action<string>? onNavigate = null)
    {
        // Form is embedded inside single-window container (DashboardForm), sidebar is on the parent window.
        return null;
    }

    public static void PopulateSidebarNavItems(SimilarProductsWinForms.Controls.ModernSidebarNav sidebarNav, string activeItemId)
    {
        sidebarNav.ClearItems();
        sidebarNav.AddItem("dashboard", "Kontrol Paneli", "📊", "Genel");
        sidebarNav.AddItem("fast_creator", "Hızlı Ürün Ekle (AI)", "⚡", "Genel", "YENİ");
        sidebarNav.AddItem("creator", "Ürün Bul & Taslak", "🛍️", "Genel");
        sidebarNav.AddItem("ai_image", "AI Görsel Studio", "🖼️", "Genel", "YENİ");
        sidebarNav.AddItem("shop", "Mağazam Performansı", "🏬", "Genel");

        sidebarNav.AddItem("research", "Pazar Araştırması", "🔍", "Araştırma & Analiz");
        sidebarNav.AddItem("viral_3d", "Viral 3D Model Avcısı", "🚀", "Araştırma & Analiz", "YENİ");
        sidebarNav.AddItem("health_score", "Listing Sağlık Skoru", "🩺", "Araştırma & Analiz", "YENİ");
        sidebarNav.AddItem("external", "Dış Pazar Yeri Bulucu", "🌐", "Araştırma & Analiz");
        sidebarNav.AddItem("ai_audit", "Mağaza AI Analizi", "🤖", "Araştırma & Analiz", "YENİ");
        sidebarNav.AddItem("ab_test", "A/B Test Paneli", "📈", "Araştırma & Analiz");

        sidebarNav.AddItem("automation", "Otomasyon Raporu", "⚡", "Otomasyon & Araçlar");
        sidebarNav.AddItem("batch", "Toplu İşlem Kuyruğu", "📦", "Otomasyon & Araçlar");
        sidebarNav.AddItem("profit", "Kâr Simülatörü", "💰", "Otomasyon & Araçlar");
        sidebarNav.AddItem("tracking", "Takip Geçmişi", "🎯", "Otomasyon & Araçlar");
        sidebarNav.AddItem("financial", "Finansal Raporlama", "💳", "Otomasyon & Araçlar", "YENİ");

        sidebarNav.AddItem("notifications", "Bildirim & Bot Ayarları", "🔔", "Sistem");
        sidebarNav.AddItem("theme", CurrentTheme == AppTheme.Dark ? "Açık Moda Geç" : "Karanlık Moda Geç", CurrentTheme == AppTheme.Dark ? "☀️" : "🌙", "Sistem");
        sidebarNav.AddItem("api", "Etsy API Ayarları", "⚙️", "Sistem");

        sidebarNav.SelectedItemId = activeItemId;
    }

    private static void OnControlContextMenuStripChanged(object? sender, EventArgs e)
    {
        if (sender is Control c && c.ContextMenuStrip != null)
        {
            ApplyContextMenuTheme(c.ContextMenuStrip);
        }
    }

    public static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0) return path;

        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        // Top left
        path.AddArc(arc, 180, 90);

        // Top right
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);

        // Bottom right
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);

        // Bottom left
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    public static void ApplyContextMenuTheme(ContextMenuStrip? cms)
    {
        if (cms == null) return;

        cms.Renderer = new ModernDarkMenuRenderer();
        cms.BackColor = CardBackground;
        cms.ForeColor = TextDark;
        cms.Font = BaseFont;

        bool hasImages = false;
        foreach (ToolStripItem item in cms.Items)
        {
            if (item.Image != null)
            {
                hasImages = true;
                break;
            }
        }
        cms.ShowImageMargin = hasImages;

        foreach (ToolStripItem item in cms.Items)
        {
            item.Font = BaseFont;
            item.ForeColor = TextDark;
            if (item is ToolStripMenuItem menuItem)
            {
                menuItem.Padding = new Padding(4, 5, 4, 5);
                foreach (ToolStripItem dropItem in menuItem.DropDownItems)
                {
                    dropItem.Font = BaseFont;
                    dropItem.ForeColor = TextDark;
                    if (dropItem is ToolStripMenuItem subItem)
                    {
                        subItem.Padding = new Padding(4, 5, 4, 5);
                    }
                }
            }
        }
    }

    public static void ConfigureComboBox(ComboBox cb)
    {
        if (cb == null || cb.IsDisposed) return;

        cb.Font = BaseFont;
        cb.FlatStyle = FlatStyle.Flat;
        cb.BackColor = InputBackground;
        cb.ForeColor = TextDark;

        if (cb.DrawMode != DrawMode.OwnerDrawFixed && cb.DrawMode != DrawMode.OwnerDrawVariable)
        {
            cb.DrawMode = DrawMode.OwnerDrawFixed;
        }
        cb.ItemHeight = Math.Max(cb.ItemHeight, 26);

        cb.DrawItem -= ComboBox_DrawItem;
        cb.DrawItem += ComboBox_DrawItem;

        if (cb is not SimilarProductsWinForms.Controls.ModernComboBox)
        {
            ModernComboBoxPainter.Attach(cb);
        }
    }

    private static void ComboBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is ComboBox cb)
        {
            DrawComboBoxItem(cb, e);
        }
    }

    public static void DrawComboBoxItem(ComboBox cb, DrawItemEventArgs e)
    {
        if (e.Index < 0)
        {
            using var emptyBrush = new SolidBrush(InputBackground);
            e.Graphics.FillRectangle(emptyBrush, e.Bounds);
            return;
        }

        bool isClosedArea = (e.State & DrawItemState.ComboBoxEdit) == DrawItemState.ComboBoxEdit;
        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        if (isClosedArea)
        {
            using (var bgBrush = new SolidBrush(InputBackground))
            {
                e.Graphics.FillRectangle(bgBrush, e.Bounds);
            }

            string text = cb.GetItemText(cb.Items[e.Index]) ?? string.Empty;
            int btnWidth = 26;
            var textRect = new Rectangle(
                e.Bounds.X + 8,
                e.Bounds.Y,
                Math.Max(0, e.Bounds.Width - (btnWidth + 10)),
                e.Bounds.Height);

            Color fg = cb.Enabled ? TextDark : TextMuted;
            TextRenderer.DrawText(
                e.Graphics,
                text,
                cb.Font,
                textRect,
                fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            return;
        }

        using (var bgBrush = new SolidBrush(CardBackground))
        {
            e.Graphics.FillRectangle(bgBrush, e.Bounds);
        }

        if (isSelected)
        {
            var itemRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y + 2, e.Bounds.Width - 8, e.Bounds.Height - 4);
            using var selPath = CreateRoundedRectanglePath(itemRect, 4);
            using var selBrush = new SolidBrush(PrimaryColor);
            var oldSmoothing = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(selBrush, selPath);
            e.Graphics.SmoothingMode = oldSmoothing;
        }

        string itemText = cb.GetItemText(cb.Items[e.Index]) ?? string.Empty;
        var itemTextRect = new Rectangle(
            e.Bounds.X + 12,
            e.Bounds.Y,
            Math.Max(0, e.Bounds.Width - 24),
            e.Bounds.Height);

        Color itemFg = isSelected ? Color.White : (cb.Enabled ? TextDark : TextMuted);
        TextRenderer.DrawText(
            e.Graphics,
            itemText,
            cb.Font,
            itemTextRect,
            itemFg,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    private sealed class ModernComboBoxPainter : NativeWindow
    {
        private static readonly ConditionalWeakTable<ComboBox, ModernComboBoxPainter> _painters = new();
        private readonly ComboBox _cb;
        private bool _isHovered;

        public static void Attach(ComboBox cb)
        {
            if (cb == null || cb.IsDisposed) return;
            if (_painters.TryGetValue(cb, out _)) return;

            var painter = new ModernComboBoxPainter(cb);
            _painters.Add(cb, painter);
        }

        private ModernComboBoxPainter(ComboBox cb)
        {
            _cb = cb;
            _cb.HandleCreated += (_, _) => AssignHandle(_cb.Handle);
            _cb.HandleDestroyed += (_, _) => ReleaseHandle();
            _cb.Disposed += (_, _) => ReleaseHandle();

            _cb.MouseEnter += (_, _) => { _isHovered = true; _cb.Invalidate(); };
            _cb.MouseLeave += (_, _) => { _isHovered = false; _cb.Invalidate(); };
            _cb.GotFocus += (_, _) => _cb.Invalidate();
            _cb.LostFocus += (_, _) => _cb.Invalidate();
            _cb.DropDown += (_, _) => _cb.Invalidate();
            _cb.DropDownClosed += (_, _) => _cb.Invalidate();
            _cb.Resize += (_, _) => _cb.Invalidate();
            _cb.SelectedIndexChanged += (_, _) => _cb.Invalidate();

            if (_cb.IsHandleCreated)
            {
                AssignHandle(_cb.Handle);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0014) // WM_ERASEBKGND
            {
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);

            if (m.Msg == 0x000F && _cb.DropDownStyle != ComboBoxStyle.Simple) // WM_PAINT
            {
                PaintOverlay();
            }
        }

        private void PaintOverlay()
        {
            if (!_cb.IsHandleCreated || _cb.Width <= 0 || _cb.Height <= 0) return;

            using var g = Graphics.FromHwnd(_cb.Handle);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int btnWidth = 26;
            var btnRect = new Rectangle(_cb.Width - btnWidth, 1, btnWidth - 1, _cb.Height - 2);
            bool isActive = _isHovered || _cb.DroppedDown;
            Color btnBg = !_cb.Enabled
                ? InputBackground
                : (isActive ? SecondaryHover : SecondaryColor);

            using (var brush = new SolidBrush(btnBg))
            {
                g.FillRectangle(brush, btnRect);
            }

            using (var sepPen = new Pen(BorderColor, 1f))
            {
                g.DrawLine(sepPen, btnRect.X, 2, btnRect.X, _cb.Height - 3);
            }

            int centerX = btnRect.X + (btnRect.Width / 2);
            int centerY = btnRect.Y + (btnRect.Height / 2);
            Color arrowColor = !_cb.Enabled
                ? TextMuted
                : (isActive ? Color.White : TextMuted);

            using (var arrowPen = new Pen(arrowColor, 1.8f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            })
            {
                using var path = new GraphicsPath();
                if (_cb.DroppedDown)
                {
                    path.AddLine(centerX - 4, centerY + 2, centerX, centerY - 2);
                    path.AddLine(centerX, centerY - 2, centerX + 4, centerY + 2);
                }
                else
                {
                    path.AddLine(centerX - 4, centerY - 2, centerX, centerY + 2);
                    path.AddLine(centerX, centerY + 2, centerX + 4, centerY - 2);
                }
                g.DrawPath(arrowPen, path);
            }

            Color borderColor = !_cb.Enabled
                ? BorderColor
                : ((_cb.Focused || _isHovered || _cb.DroppedDown) ? PrimaryColor : BorderColor);

            using (var borderPen = new Pen(borderColor, 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, _cb.Width - 1, _cb.Height - 1);
            }
        }
    }
}

public class ModernDarkMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => UiStyle.CardBackground;
    public override Color MenuBorder => UiStyle.BorderColor;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => UiStyle.PrimaryColor;
    public override Color MenuItemSelectedGradientBegin => UiStyle.PrimaryColor;
    public override Color MenuItemSelectedGradientEnd => UiStyle.PrimaryColor;
    public override Color MenuItemPressedGradientBegin => UiStyle.PrimaryHover;
    public override Color MenuItemPressedGradientMiddle => UiStyle.PrimaryHover;
    public override Color MenuItemPressedGradientEnd => UiStyle.PrimaryHover;
    public override Color MenuStripGradientBegin => UiStyle.CardBackground;
    public override Color MenuStripGradientEnd => UiStyle.CardBackground;
    public override Color CheckBackground => UiStyle.PrimaryColor;
    public override Color CheckSelectedBackground => UiStyle.PrimaryHover;
    public override Color CheckPressedBackground => UiStyle.PrimaryHover;
    public override Color ImageMarginGradientBegin => UiStyle.CardBackground;
    public override Color ImageMarginGradientMiddle => UiStyle.CardBackground;
    public override Color ImageMarginGradientEnd => UiStyle.CardBackground;
    public override Color ImageMarginRevealedGradientBegin => UiStyle.CardBackground;
    public override Color ImageMarginRevealedGradientMiddle => UiStyle.CardBackground;
    public override Color ImageMarginRevealedGradientEnd => UiStyle.CardBackground;
    public override Color SeparatorDark => UiStyle.BorderColor;
    public override Color SeparatorLight => Color.Transparent;
    public override Color ButtonSelectedHighlight => UiStyle.PrimaryColor;
    public override Color ButtonSelectedHighlightBorder => UiStyle.PrimaryColor;
    public override Color ButtonPressedHighlight => UiStyle.PrimaryHover;
    public override Color ButtonPressedHighlightBorder => UiStyle.PrimaryHover;
    public override Color ButtonCheckedHighlight => UiStyle.PrimaryColor;
    public override Color ButtonCheckedHighlightBorder => UiStyle.PrimaryColor;
    public override Color GripDark => UiStyle.BorderColor;
    public override Color GripLight => Color.Transparent;
}

public class ModernDarkMenuRenderer : ToolStripProfessionalRenderer
{
    public ModernDarkMenuRenderer() : base(new ModernDarkMenuColorTable())
    {
        RoundedEdges = false;
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(UiStyle.CardBackground);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(UiStyle.BorderColor, 1f);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(UiStyle.CardBackground);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item == null) return;
        if (e.Item.Selected || e.Item.Pressed)
        {
            var rect = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
            using var path = UiStyle.CreateRoundedRectanglePath(rect, 4);
            using var brush = new SolidBrush(e.Item.Pressed ? UiStyle.PrimaryHover : UiStyle.PrimaryColor);
            var oldMode = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
            e.Graphics.SmoothingMode = oldMode;
        }
        else
        {
            using var brush = new SolidBrush(UiStyle.CardBackground);
            e.Graphics.FillRectangle(brush, 0, 0, e.Item.Width, e.Item.Height);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.Item != null)
        {
            e.TextColor = (e.Item.Selected || e.Item.Pressed)
                ? Color.White
                : (e.Item.Enabled ? UiStyle.TextDark : UiStyle.TextMuted);
            e.TextFont = UiStyle.BaseFont;
        }
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        if (e.Item == null) return;
        int y = e.Item.Height / 2;
        using var pen = new Pen(UiStyle.BorderColor, 1f);
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        int cx = e.ArrowRectangle.X + e.ArrowRectangle.Width / 2;
        int cy = e.ArrowRectangle.Y + e.ArrowRectangle.Height / 2;
        Color arrowCol = (e.Item?.Selected ?? false) ? Color.White : UiStyle.TextMuted;
        using var arrowPen = new Pen(arrowCol, 1.8f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var path = new GraphicsPath();
        path.AddLine(cx - 2, cy - 4, cx + 2, cy);
        path.AddLine(cx + 2, cy, cx - 2, cy + 4);
        var oldMode = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(arrowPen, path);
        e.Graphics.SmoothingMode = oldMode;
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var checkRect = new Rectangle(e.ImageRectangle.X + 1, e.ImageRectangle.Y + 1, 14, 14);
        using var path = UiStyle.CreateRoundedRectanglePath(checkRect, 3);
        using var brush = new SolidBrush(UiStyle.PrimaryColor);
        var oldMode = e.Graphics.SmoothingMode;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);

        using var pen = new Pen(Color.White, 1.6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using var checkPath = new GraphicsPath();
        checkPath.AddLine(checkRect.X + 3, checkRect.Y + 7, checkRect.X + 6, checkRect.Y + 10);
        checkPath.AddLine(checkRect.X + 6, checkRect.Y + 10, checkRect.X + 11, checkRect.Y + 4);
        e.Graphics.DrawPath(pen, checkPath);
        e.Graphics.SmoothingMode = oldMode;
    }
}



