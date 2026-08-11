namespace SimilarProductsWinForms;

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

internal static class UiStyle
{
    // Color Palette (Slate/Indigo Premium Light Theme)
    public static readonly Color PrimaryColor = Color.FromArgb(37, 99, 235);       // #2563EB - Blue/Indigo
    public static readonly Color PrimaryHover = Color.FromArgb(29, 78, 216);       // #1D4ED8 - Darker Indigo
    public static readonly Color SecondaryColor = Color.FromArgb(71, 85, 105);    // #475569 - Slate Gray
    public static readonly Color SecondaryHover = Color.FromArgb(51, 65, 85);    // #334155 - Darker Slate
    public static readonly Color BackgroundColor = Color.FromArgb(248, 250, 252); // #F8FAFC - Ice White/Light Slate
    public static readonly Color CardBackground = Color.White;
    public static readonly Color TextDark = Color.FromArgb(15, 23, 42);          // #0F172A - Slate 900
    public static readonly Color TextMuted = Color.FromArgb(100, 116, 139);      // #64748B - Slate 500
    public static readonly Color BorderColor = Color.FromArgb(226, 232, 240);      // #E2E8F0 - Slate 200
    public static readonly Color SuccessColor = Color.FromArgb(16, 185, 129);     // #10B981 - Emerald Green
    public static readonly Color DangerColor = Color.FromArgb(239, 68, 68);        // #EF4444 - Rose Red
    public static readonly Color AccentColor = Color.FromArgb(249, 115, 22);       // #F97316 - Etsy Orange/Amber

    // Standard Fonts
    public static readonly Font BaseFont = new Font("Segoe UI", 10F);
    public static readonly Font SemiboldBaseFont = new Font("Segoe UI Semibold", 9.5F);
    public static readonly Font TitleFont = new Font("Segoe UI Semibold", 22F);
    public static readonly Font SubtitleFont = new Font("Segoe UI", 10F);
    public static readonly Font KpiValueFont = new Font("Segoe UI Semibold", 16F);

    public static void ApplyTheme(Form form)
    {
        ApplyResponsiveTheme(form);
    }

    public static void ApplyResponsiveTheme(Form form, Size? minSize = null)
    {
        form.BackColor = BackgroundColor;
        form.Font = BaseFont;
        form.MinimumSize = minSize ?? new Size(1280, 760);
        SetDoubleBuffered(form);
        ApplyToControls(form.Controls);
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

    private static void ApplyToControls(Control.ControlCollection controls)
    {
        foreach (Control ctrl in controls)
        {
            if (ctrl is Panel pnl && pnl.GetType() == typeof(Panel))
            {
                pnl.BackColor = Color.Transparent;
            }
            else if (ctrl is Label lbl)
            {
                if (lbl.ForeColor == SystemColors.ControlText)
                {
                    lbl.ForeColor = TextDark;
                }
            }
            else if (ctrl is TextBox txt)
            {
                txt.BorderStyle = BorderStyle.FixedSingle;
                txt.Font = BaseFont;
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

            if (ctrl.HasChildren)
            {
                ApplyToControls(ctrl.Controls);
            }
        }
    }

    public static Button CreateButton(string text, bool isSecondary = false)
    {
        var button = new Button
        {
            Dock = DockStyle.Fill,
            Text = text,
            BackColor = isSecondary ? SecondaryColor : PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = SemiboldBaseFont,
            TextAlign = ContentAlignment.MiddleCenter,
            UseVisualStyleBackColor = false,
            AutoEllipsis = true,
            Padding = new Padding(0),
            Margin = new Padding(6, 2, 0, 2),
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = isSecondary ? SecondaryHover : PrimaryHover;
        return button;
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
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = BorderColor;
        grid.EnableHeadersVisualStyles = false;

        // Header Styling
        grid.ColumnHeadersDefaultCellStyle.BackColor = BackgroundColor;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextDark;
        grid.ColumnHeadersDefaultCellStyle.Font = SemiboldBaseFont;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = BackgroundColor;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextDark;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

        // Row Styling
        grid.DefaultCellStyle.BackColor = CardBackground;
        grid.DefaultCellStyle.ForeColor = TextDark;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(239, 246, 255); // Light blue tint
        grid.DefaultCellStyle.SelectionForeColor = PrimaryColor;
        grid.DefaultCellStyle.Font = BaseFont;

        // Double Buffering using Reflection to prevent WinForms grid flickering
        try
        {
            var property = typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            property?.SetValue(grid, true, null);
        }
        catch { }
    }

    public static void AddKpiCard(TableLayoutPanel parent, int column, int row, string title, string key, Dictionary<string, Label> kpis)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = CardBackground,
            Margin = new Padding(5, 2, 5, 4),
            Padding = new Padding(12, 8, 12, 8),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            ForeColor = TextMuted,
            Font = BaseFont,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(titleLabel, 0, 0);

        var valueLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "-",
            Font = KpiValueFont,
            ForeColor = TextDark,
            TextAlign = ContentAlignment.MiddleLeft
        };
        panel.Controls.Add(valueLabel, 0, 1);

        kpis[key] = valueLabel;
        parent.Controls.Add(panel, column, row);
    }
}
