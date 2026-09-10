namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

/// <summary>
/// Modern visual chip/card selector control for quick scene presets.
/// </summary>
public class PresetChipSelector : FlowLayoutPanel
{
    public event EventHandler<string>? SelectedPresetChanged;

    private readonly List<PresetChipButton> _chipButtons = [];
    private string _selectedPreset = "";

    public string SelectedPreset
    {
        get => _selectedPreset;
        set
        {
            _selectedPreset = value ?? "";
            UpdateSelectionStates();
        }
    }

    public PresetChipSelector()
    {
        DoubleBuffered = true;
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Transparent;
        Margin = new Padding(0);
        Padding = new Padding(0);
    }

    public void SetPresets(IEnumerable<(string Id, string Label, string Icon)> presets)
    {
        SuspendLayout();
        Controls.Clear();
        _chipButtons.Clear();

        foreach (var (id, label, icon) in presets)
        {
            var btn = new PresetChipButton
            {
                PresetId = id,
                DisplayText = $"{icon} {label}",
                Margin = new Padding(0, 0, 6, 6)
            };

            btn.Click += (_, _) =>
            {
                SelectedPreset = btn.PresetId;
                SelectedPresetChanged?.Invoke(this, btn.PresetId);
            };

            _chipButtons.Add(btn);
            Controls.Add(btn);
        }

        if (_chipButtons.Count > 0 && string.IsNullOrWhiteSpace(_selectedPreset))
        {
            _selectedPreset = _chipButtons[0].PresetId;
        }

        UpdateSelectionStates();
        ResumeLayout(true);
    }

    private void UpdateSelectionStates()
    {
        foreach (var btn in _chipButtons)
        {
            btn.IsSelected = string.Equals(btn.PresetId, _selectedPreset, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class PresetChipButton : Control
    {
        public string PresetId { get; set; } = "";
        public string DisplayText { get; set; } = "";

        private bool _isSelected;
        private bool _isHovered;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                Invalidate();
            }
        }

        public PresetChipButton()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            DoubleBuffered = true;
            Height = 32;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI Semibold", 8.8F);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var textSize = g.MeasureString(DisplayText, Font);
            Width = (int)textSize.Width + 24;

            Color parentBg = Parent?.BackColor ?? Color.FromArgb(30, 41, 59);
            if (parentBg == Color.Transparent && Parent?.Parent != null) parentBg = Parent.Parent.BackColor;
            using (var clearBrush = new SolidBrush(parentBg != Color.Transparent ? parentBg : Color.FromArgb(30, 41, 59)))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, 14);

            Color bg = _isSelected
                ? Color.FromArgb(99, 102, 241) // Active Indigo
                : (_isHovered ? Color.FromArgb(45, 55, 75) : Color.FromArgb(30, 41, 59));

            Color border = _isSelected
                ? Color.FromArgb(129, 140, 248)
                : (_isHovered ? Color.FromArgb(71, 85, 105) : Color.FromArgb(51, 65, 85));

            Color fg = _isSelected
                ? Color.White
                : (_isHovered ? Color.FromArgb(241, 245, 249) : Color.FromArgb(203, 213, 225));

            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }

            using (var pen = new Pen(border, 1.2f))
            {
                g.DrawPath(pen, path);
            }

            using (var textBrush = new SolidBrush(fg))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(DisplayText, Font, textBrush, rect, sf);
            }
        }
    }
}
