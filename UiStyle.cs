namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

internal static class UiStyle
{
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
        if (ctrl is Panel pnl && pnl.GetType() == typeof(Panel))
        {
            if (pnl.BackColor == SystemColors.Control || pnl.BackColor == Color.Empty)
            {
                pnl.BackColor = Color.Transparent;
            }
        }
        else if (ctrl is SimilarProductsWinForms.Controls.ModernCardPanel card)
        {
            card.CardColor = CardBackground;
            card.BorderColor = BorderColor;
        }
        else if (ctrl is GroupBox gb)
        {
            gb.Font = SemiboldBaseFont;
            gb.ForeColor = TextDark;
            if (gb.BackColor == SystemColors.Control || gb.BackColor == Color.Empty)
            {
                gb.BackColor = Color.Transparent;
            }
            ApplyToControls(gb.Controls);
        }
        else if (ctrl is Label lbl)
        {
            // Dark hardcoded colors should be fixed to TextDark or TextMuted
            if (lbl.ForeColor == SystemColors.ControlText ||
                lbl.ForeColor == Color.Black ||
                lbl.ForeColor == Color.FromArgb(15, 23, 42) ||
                lbl.ForeColor == Color.FromArgb(23, 32, 49) ||
                lbl.ForeColor == Color.FromArgb(24, 31, 42) ||
                lbl.ForeColor == Color.FromArgb(49, 59, 73) ||
                lbl.ForeColor == Color.FromArgb(82, 93, 110))
            {
                lbl.ForeColor = lbl.Font.Size < 9.2F ? TextMuted : TextDark;
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
            cb.Font = BaseFont;
            cb.FlatStyle = FlatStyle.Flat;
            cb.BackColor = InputBackground;
            cb.ForeColor = TextDark;
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
}


