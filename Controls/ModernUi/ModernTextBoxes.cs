namespace SimilarProductsWinForms.Controls;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

/// <summary>
/// Modern multiline text box with integrated soft ModernVScrollBar and dark styled focus border.
/// </summary>
public class ModernMultilineTextBox : Panel
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private const int EM_GETLINECOUNT = 0x00BA;
    private const int EM_GETFIRSTVISIBLELINE = 0x00CE;
    private const int EM_LINESCROLL = 0x00B6;

    private readonly TextBox _innerBox;
    private readonly ModernVScrollBar _scrollBar;
    private bool _isFocused = false;
    private bool _isSyncing = false;

    public TextBox InnerTextBox => _innerBox;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _innerBox.Text ?? string.Empty;
        set
        {
            _innerBox.Text = value ?? string.Empty;
            SyncScrollBar();
        }
    }

    public string[] Lines
    {
        get => _innerBox.Lines;
        set
        {
            _innerBox.Lines = value ?? [];
            SyncScrollBar();
        }
    }

    public new event EventHandler? TextChanged
    {
        add => _innerBox.TextChanged += value;
        remove => _innerBox.TextChanged -= value;
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override Font Font
    {
        get => _innerBox.Font;
        set
        {
            base.Font = value!;
            if (value != null) _innerBox.Font = value;
            SyncScrollBar();
        }
    }

    public bool ReadOnly
    {
        get => _innerBox.ReadOnly;
        set => _innerBox.ReadOnly = value;
    }

    public int MaxLength
    {
        get => _innerBox.MaxLength;
        set => _innerBox.MaxLength = value;
    }

    public int SelectionStart
    {
        get => _innerBox.SelectionStart;
        set => _innerBox.SelectionStart = value;
    }

    public int SelectionLength
    {
        get => _innerBox.SelectionLength;
        set => _innerBox.SelectionLength = value;
    }

    public new event KeyEventHandler? KeyDown
    {
        add => _innerBox.KeyDown += value;
        remove => _innerBox.KeyDown -= value;
    }

    public new event KeyEventHandler? KeyUp
    {
        add => _innerBox.KeyUp += value;
        remove => _innerBox.KeyUp -= value;
    }

    public new event KeyPressEventHandler? KeyPress
    {
        add => _innerBox.KeyPress += value;
        remove => _innerBox.KeyPress -= value;
    }

    public void Clear()
    {
        _innerBox.Clear();
        SyncScrollBar();
    }

    public void AppendText(string text)
    {
        _innerBox.AppendText(text);
        SyncScrollBar();
    }

    public void Select(int start, int length)
    {
        _innerBox.Select(start, length);
    }

    public ModernMultilineTextBox()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, false);
        DoubleBuffered = true;
        Padding = new Padding(6, 6, 2, 6);
        BackColor = UiStyle.InputBackground;

        _innerBox = new TextBox
        {
            Multiline = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.None,
            Dock = DockStyle.Fill,
            BackColor = UiStyle.InputBackground,
            ForeColor = UiStyle.TextDark,
            Font = new Font("Segoe UI", 9F)
        };

        _scrollBar = new ModernVScrollBar
        {
            Dock = DockStyle.Right,
            Width = 8,
            Visible = false
        };

        _innerBox.Enter += (_, _) => { _isFocused = true; Invalidate(); };
        _innerBox.Leave += (_, _) => { _isFocused = false; Invalidate(); };
        _innerBox.TextChanged += (_, _) => SyncScrollBar();
        _innerBox.SizeChanged += (_, _) => SyncScrollBar();
        _innerBox.KeyUp += (_, _) => SyncFromBox();
        _innerBox.MouseUp += (_, _) => SyncFromBox();
        _innerBox.MouseWheel += (_, _) => SyncFromBox();

        _scrollBar.ValueChanged += (_, _) =>
        {
            if (_isSyncing || !_innerBox.IsHandleCreated) return;
            _isSyncing = true;
            try
            {
                int currentTop = SendMessage(_innerBox.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
                int delta = _scrollBar.Value - currentTop;
                if (delta != 0)
                {
                    SendMessage(_innerBox.Handle, EM_LINESCROLL, 0, delta);
                }
            }
            finally
            {
                _isSyncing = false;
            }
        };

        Controls.Add(_innerBox);
        Controls.Add(_scrollBar);
    }

    private void SyncFromBox()
    {
        if (_isSyncing || !_innerBox.IsHandleCreated) return;
        _isSyncing = true;
        try
        {
            int currentTop = SendMessage(_innerBox.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            _scrollBar.Value = currentTop;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void SyncScrollBar()
    {
        if (_isSyncing || !_innerBox.IsHandleCreated) return;
        _isSyncing = true;
        try
        {
            int lineCount = SendMessage(_innerBox.Handle, EM_GETLINECOUNT, 0, 0);
            int fontH = Math.Max(1, _innerBox.Font.Height);
            int visibleLines = Math.Max(1, _innerBox.ClientSize.Height / fontH);

            int max = Math.Max(0, lineCount - visibleLines);
            bool shouldBeVisible = max > 0;
            if (_scrollBar.Visible != shouldBeVisible) _scrollBar.Visible = shouldBeVisible;
            if (_scrollBar.Maximum != max) _scrollBar.Maximum = max;
            if (_scrollBar.LargeChange != visibleLines) _scrollBar.LargeChange = visibleLines;

            int currentTop = SendMessage(_innerBox.Handle, EM_GETFIRSTVISIBLELINE, 0, 0);
            if (currentTop <= max && _scrollBar.Value != currentTop) _scrollBar.Value = currentTop;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        if (_innerBox != null) _innerBox.BackColor = BackColor;
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        if (_innerBox != null) _innerBox.ForeColor = ForeColor;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        int w = proposedSize.Width > 0 ? proposedSize.Width : Width;
        return new Size(w, Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using (var bgBrush = new SolidBrush(BackColor))
        {
            using var bgPath = ModernCardPanel.CreateRoundedRectanglePath(rect, 6);
            g.FillPath(bgBrush, bgPath);
        }

        Color borderColor = _isFocused ? UiStyle.PrimaryColor : UiStyle.BorderColor;
        using var pen = new Pen(borderColor, _isFocused ? 1.5f : 1f);
        using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, 6);
        g.DrawPath(pen, path);
    }
}

/// <summary>
/// Modern dark-theme single-line TextBox with rounded corners, placeholder support,
/// sleek slate border, glowing focus outline, and inner padding.
/// </summary>
public class ModernTextBox : Panel
{
    private readonly TextBox _inner;
    private bool _isHovered;
    private bool _isFocused;
    private int _cornerRadius = 7;
    private Color _borderColor = Color.FromArgb(51, 65, 85);       // Slate 700
    private Color _borderHoverColor = Color.FromArgb(100, 116, 139); // Slate 500
    private Color _borderFocusColor = Color.FromArgb(99, 102, 241);  // Indigo 500

    public TextBox InnerTextBox => _inner;

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _inner.Text;
        set => _inner.Text = value ?? string.Empty;
    }

    public string PlaceholderText
    {
        get => _inner.PlaceholderText;
        set => _inner.PlaceholderText = value;
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = value; Invalidate(); }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set { _borderColor = value; Invalidate(); }
    }

    public Color BorderHoverColor
    {
        get => _borderHoverColor;
        set { _borderHoverColor = value; Invalidate(); }
    }

    public Color BorderFocusColor
    {
        get => _borderFocusColor;
        set { _borderFocusColor = value; Invalidate(); }
    }

    public bool ReadOnly
    {
        get => _inner.ReadOnly;
        set => _inner.ReadOnly = value;
    }

    public int MaxLength
    {
        get => _inner.MaxLength;
        set => _inner.MaxLength = value;
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override Font Font
    {
        get => _inner.Font;
        set
        {
            base.Font = value!;
            if (value != null) _inner.Font = value;
            PositionInner();
        }
    }

    public new event EventHandler? TextChanged
    {
        add => _inner.TextChanged += value;
        remove => _inner.TextChanged -= value;
    }

    public new event KeyEventHandler? KeyDown
    {
        add => _inner.KeyDown += value;
        remove => _inner.KeyDown -= value;
    }

    public new event KeyPressEventHandler? KeyPress
    {
        add => _inner.KeyPress += value;
        remove => _inner.KeyPress -= value;
    }

    public new event KeyEventHandler? KeyUp
    {
        add => _inner.KeyUp += value;
        remove => _inner.KeyUp -= value;
    }

    public void Select(int start, int length) => _inner.Select(start, length);
    public void SelectAll() => _inner.SelectAll();
    public void Clear() => _inner.Clear();
    public new void Focus() => _inner.Focus();


    public ModernTextBox()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.SupportsTransparentBackColor, false);
        DoubleBuffered = true;
        BackColor = Color.FromArgb(30, 41, 59); // Slate 900
        ForeColor = Color.FromArgb(248, 250, 252); // Slate 50
        Height = 32;

        _inner = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(30, 41, 59),
            ForeColor = Color.FromArgb(248, 250, 252),
            Font = new Font("Segoe UI", 9F)
        };

        _inner.GotFocus += (_, _) => { _isFocused = true; Invalidate(); };
        _inner.LostFocus += (_, _) => { _isFocused = false; Invalidate(); };
        _inner.MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
        _inner.MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };

        MouseEnter += (_, _) => { _isHovered = true; Invalidate(); };
        MouseLeave += (_, _) => { _isHovered = false; Invalidate(); };
        Click += (_, _) => _inner.Focus();

        Controls.Add(_inner);
        PositionInner();
    }

    protected override void OnBackColorChanged(EventArgs e)
    {
        base.OnBackColorChanged(e);
        if (_inner != null) _inner.BackColor = BackColor;
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);
        if (_inner != null) _inner.ForeColor = ForeColor;
    }

    protected override void OnResize(EventArgs eventargs)
    {
        base.OnResize(eventargs);
        PositionInner();
    }

    private void PositionInner()
    {
        if (_inner == null) return;
        int innerH = _inner.PreferredHeight;
        int innerY = Math.Max(2, (Height - innerH) / 2);
        int innerX = 10;
        int innerW = Math.Max(10, Width - (innerX * 2));
        _inner.SetBounds(innerX, innerY, innerW, innerH);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var rect = new Rectangle(0, 0, Width - 1, Height - 1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        using (var bgBrush = new SolidBrush(BackColor))
        {
            using var bgPath = ModernCardPanel.CreateRoundedRectanglePath(rect, _cornerRadius);
            g.FillPath(bgBrush, bgPath);
        }

        Color borderClr = _isFocused ? _borderFocusColor : (_isHovered ? _borderHoverColor : _borderColor);
        float borderWidth = _isFocused ? 1.5f : 1f;
        using (var pen = new Pen(borderClr, borderWidth))
        {
            using var path = ModernCardPanel.CreateRoundedRectanglePath(rect, _cornerRadius);
            g.DrawPath(pen, path);
        }
    }
}
