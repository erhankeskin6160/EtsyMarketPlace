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
/// Modern custom-drawn CheckedListBox with sleek dark theme, custom vector checkboxes,
/// smooth scrolling via ModernVScrollBar, comfortable row spacing, and full WinForms API compatibility.
/// </summary>
public class ModernCheckedListBox : Control
{
    public class CheckedItemEntry
    {
        public object Value { get; set; }
        public CheckState CheckState { get; set; }
        public bool Checked => CheckState == CheckState.Checked;

        public CheckedItemEntry(object value, CheckState state)
        {
            Value = value;
            CheckState = state;
        }

        public override string ToString() => Value?.ToString() ?? string.Empty;
    }

    private readonly List<CheckedItemEntry> _items = new();
    private readonly ModernVScrollBar _scrollBar;
    private int _hoverIndex = -1;
    private int _selectedIndex = -1;
    private int _itemHeight = 28;
    private bool _checkOnClick = true;
    private bool _isFocused = false;

    public event ItemCheckEventHandler? ItemCheck;
    public event EventHandler? SelectedIndexChanged;

    public int ItemHeight
    {
        get => _itemHeight;
        set { _itemHeight = Math.Max(20, value); UpdateScroll(); Invalidate(); }
    }

    public bool CheckOnClick
    {
        get => _checkOnClick;
        set => _checkOnClick = value;
    }

    public Color BorderColor { get; set; } = UiStyle.BorderColor;
    public Color BorderFocusColor { get; set; } = Color.FromArgb(99, 102, 241);
    public Color HoverColor { get; set; } = UiStyle.CardHoverBackground;

    public ObjectCollection Items { get; }
    public CheckedItemCollection CheckedItems { get; }
    public CheckedIndexCollection CheckedIndices { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            int clamped = (value < 0 || value >= _items.Count) ? -1 : value;
            if (_selectedIndex != clamped)
            {
                _selectedIndex = clamped;
                EnsureVisible(_selectedIndex);
                Invalidate();
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public object? SelectedItem
    {
        get => (_selectedIndex >= 0 && _selectedIndex < _items.Count) ? _items[_selectedIndex].Value : null;
        set
        {
            int idx = -1;
            if (value != null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (Equals(_items[i].Value, value))
                    {
                        idx = i;
                        break;
                    }
                }
            }
            SelectedIndex = idx;
        }
    }

    public ModernCheckedListBox()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        DoubleBuffered = true;
        BackColor = UiStyle.InputBackground;
        ForeColor = UiStyle.TextDark;
        Font = UiStyle.BaseFont;

        Items = new ObjectCollection(this);
        CheckedItems = new CheckedItemCollection(this);
        CheckedIndices = new CheckedIndexCollection(this);

        _scrollBar = new ModernVScrollBar
        {
            Visible = false,
            Width = 8,
            Dock = DockStyle.None
        };
        _scrollBar.ValueChanged += (_, _) => Invalidate();
        Controls.Add(_scrollBar);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        UpdateScroll();
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateScroll();
    }

    private void UpdateScroll()
    {
        if (_scrollBar == null) return;
        int availableH = Math.Max(1, Height - 4);
        int visibleRows = availableH / _itemHeight;
        int maxScroll = Math.Max(0, _items.Count - visibleRows);

        if (maxScroll > 0)
        {
            _scrollBar.Visible = true;
            _scrollBar.Location = new Point(Width - _scrollBar.Width - 2, 2);
            _scrollBar.Height = Math.Max(10, Height - 4);
            _scrollBar.Maximum = maxScroll;
            _scrollBar.LargeChange = Math.Max(1, visibleRows);
            if (_scrollBar.Value > maxScroll) _scrollBar.Value = maxScroll;
        }
        else
        {
            _scrollBar.Visible = false;
            _scrollBar.Value = 0;
        }
    }

    public void EnsureVisible(int index)
    {
        if (index < 0 || index >= _items.Count || !_scrollBar.Visible) return;
        int visibleRows = Math.Max(1, (Height - 4) / _itemHeight);
        if (index < _scrollBar.Value)
        {
            _scrollBar.Value = index;
        }
        else if (index >= _scrollBar.Value + visibleRows)
        {
            _scrollBar.Value = index - visibleRows + 1;
        }
    }

    public bool GetItemChecked(int index)
    {
        if (index < 0 || index >= _items.Count) return false;
        return _items[index].Checked;
    }

    public void SetItemChecked(int index, bool isChecked)
    {
        SetItemCheckState(index, isChecked ? CheckState.Checked : CheckState.Unchecked);
    }

    public CheckState GetItemCheckState(int index)
    {
        if (index < 0 || index >= _items.Count) return CheckState.Unchecked;
        return _items[index].CheckState;
    }

    public void SetItemCheckState(int index, CheckState value)
    {
        if (index < 0 || index >= _items.Count) return;
        var current = _items[index].CheckState;
        if (current != value)
        {
            var e = new ItemCheckEventArgs(index, value, current);
            ItemCheck?.Invoke(this, e);
            _items[index].CheckState = e.NewValue;
            Invalidate();
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _isFocused = true;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _isFocused = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();

        if (e.Button == MouseButtons.Left)
        {
            int scrollVal = _scrollBar.Visible ? _scrollBar.Value : 0;
            int clickedIdx = scrollVal + ((e.Y - 2) / _itemHeight);
            if (clickedIdx >= 0 && clickedIdx < _items.Count)
            {
                SelectedIndex = clickedIdx;
                int rowW = _scrollBar.Visible ? Width - _scrollBar.Width - 6 : Width - 4;
                if (e.X >= 2 && e.X <= rowW)
                {
                    if (_checkOnClick || e.X <= 32)
                    {
                        SetItemChecked(clickedIdx, !GetItemChecked(clickedIdx));
                    }
                }
            }
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int scrollVal = _scrollBar.Visible ? _scrollBar.Value : 0;
        int rowW = _scrollBar.Visible ? Width - _scrollBar.Width - 6 : Width - 4;
        int prevHover = _hoverIndex;

        if (e.X >= 2 && e.X <= rowW && e.Y >= 2 && e.Y < Height - 2)
        {
            int idx = scrollVal + ((e.Y - 2) / _itemHeight);
            _hoverIndex = (idx >= 0 && idx < _items.Count) ? idx : -1;
        }
        else
        {
            _hoverIndex = -1;
        }

        if (prevHover != _hoverIndex) Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverIndex != -1)
        {
            _hoverIndex = -1;
            Invalidate();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_scrollBar.Visible)
        {
            int delta = -Math.Sign(e.Delta);
            _scrollBar.Value += delta;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Down)
        {
            if (SelectedIndex < _items.Count - 1)
            {
                SelectedIndex++;
                EnsureVisible(SelectedIndex);
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            if (SelectedIndex > 0)
            {
                SelectedIndex--;
                EnsureVisible(SelectedIndex);
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Space)
        {
            if (SelectedIndex >= 0 && SelectedIndex < _items.Count)
            {
                SetItemChecked(SelectedIndex, !GetItemChecked(SelectedIndex));
            }
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Background
        using (var bgBrush = new SolidBrush(BackColor))
        {
            g.FillRectangle(bgBrush, ClientRectangle);
        }

        int scrollVal = _scrollBar.Visible ? _scrollBar.Value : 0;
        int rowW = _scrollBar.Visible ? Width - _scrollBar.Width - 6 : Width - 4;
        int visibleCount = (Height / _itemHeight) + 2;

        int startIdx = scrollVal;
        int endIdx = Math.Min(_items.Count, startIdx + visibleCount);

        for (int i = startIdx; i < endIdx; i++)
        {
            int rowY = 2 + (i - startIdx) * _itemHeight;
            var rowRect = new Rectangle(2, rowY, rowW, _itemHeight);

            bool isHovered = (i == _hoverIndex);
            bool isSelected = (i == _selectedIndex);

            if (isHovered || isSelected)
            {
                Color rowBg = isHovered ? HoverColor : Color.FromArgb(40, 52, 75);
                using var rowBrush = new SolidBrush(rowBg);
                using var rowPath = ModernCardPanel.CreateRoundedRectanglePath(new Rectangle(rowRect.X + 2, rowRect.Y + 1, rowRect.Width - 4, rowRect.Height - 2), 4);
                g.FillPath(rowBrush, rowPath);
            }

            var entry = _items[i];
            int boxSize = 18;
            int boxX = rowRect.X + 8;
            int boxY = rowRect.Y + (_itemHeight - boxSize) / 2;
            var boxRect = new Rectangle(boxX, boxY, boxSize, boxSize);

            ModernCheckBox.DrawBox(
                g,
                boxRect,
                entry.Checked,
                entry.CheckState == CheckState.Indeterminate,
                isHovered,
                false,
                Enabled,
                boxSize,
                4);

            int textX = boxRect.Right + 8;
            int textW = Math.Max(0, rowRect.Right - textX - 4);
            var textRect = new Rectangle(textX, rowRect.Y, textW, _itemHeight);

            Color textClr = Enabled ? ForeColor : UiStyle.TextMuted;
            TextRenderer.DrawText(
                g,
                entry.ToString(),
                Font,
                textRect,
                textClr,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }

        // Draw Border
        Color border = _isFocused ? BorderFocusColor : BorderColor;
        using (var borderPen = new Pen(border, 1f))
        {
            g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
        }
    }

    public class ObjectCollection : IList, ICollection, IEnumerable
    {
        private readonly ModernCheckedListBox _owner;

        internal ObjectCollection(ModernCheckedListBox owner) => _owner = owner;

        public int Count => _owner._items.Count;
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[int index]
        {
            get => _owner._items[index].Value;
            set
            {
                _owner._items[index].Value = value ?? string.Empty;
                _owner.Invalidate();
            }
        }

        public int Add(object item)
        {
            return Add(item, CheckState.Unchecked);
        }

        public int Add(object item, bool isChecked)
        {
            return Add(item, isChecked ? CheckState.Checked : CheckState.Unchecked);
        }

        public int Add(object item, CheckState checkState)
        {
            var entry = new CheckedItemEntry(item, checkState);
            _owner._items.Add(entry);
            _owner.UpdateScroll();
            _owner.Invalidate();
            return _owner._items.Count - 1;
        }

        public void AddRange(object[] items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                _owner._items.Add(new CheckedItemEntry(item, CheckState.Unchecked));
            }
            _owner.UpdateScroll();
            _owner.Invalidate();
        }

        public void AddRange(IEnumerable<object> items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                _owner._items.Add(new CheckedItemEntry(item, CheckState.Unchecked));
            }
            _owner.UpdateScroll();
            _owner.Invalidate();
        }

        public void Clear()
        {
            _owner._items.Clear();
            _owner._selectedIndex = -1;
            _owner._hoverIndex = -1;
            _owner.UpdateScroll();
            _owner.Invalidate();
        }

        public bool Contains(object? value)
        {
            if (value == null) return false;
            return _owner._items.Any(e => Equals(e.Value, value));
        }

        public int IndexOf(object? value)
        {
            if (value == null) return -1;
            for (int i = 0; i < _owner._items.Count; i++)
            {
                if (Equals(_owner._items[i].Value, value)) return i;
            }
            return -1;
        }

        public void Insert(int index, object? value)
        {
            _owner._items.Insert(index, new CheckedItemEntry(value ?? string.Empty, CheckState.Unchecked));
            _owner.UpdateScroll();
            _owner.Invalidate();
        }

        int IList.Add(object? value) => Add(value ?? string.Empty);

        public void Remove(object? value)
        {
            int idx = IndexOf(value);
            if (idx >= 0) RemoveAt(idx);
        }

        public void RemoveAt(int index)
        {
            if (index >= 0 && index < _owner._items.Count)
            {
                _owner._items.RemoveAt(index);
                if (_owner._selectedIndex >= _owner._items.Count)
                    _owner._selectedIndex = _owner._items.Count - 1;
                _owner.UpdateScroll();
                _owner.Invalidate();
            }
        }

        public void CopyTo(Array array, int index)
        {
            for (int i = 0; i < _owner._items.Count; i++)
            {
                array.SetValue(_owner._items[i].Value, index + i);
            }
        }

        public IEnumerator GetEnumerator()
        {
            foreach (var item in _owner._items)
            {
                yield return item.Value;
            }
        }
    }

    public class CheckedItemCollection : IList, ICollection, IEnumerable
    {
        private readonly ModernCheckedListBox _owner;

        internal CheckedItemCollection(ModernCheckedListBox owner) => _owner = owner;

        public int Count => _owner._items.Count(e => e.Checked);
        public bool IsReadOnly => true;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[int index]
        {
            get
            {
                int current = 0;
                foreach (var e in _owner._items)
                {
                    if (e.Checked)
                    {
                        if (current == index) return e.Value;
                        current++;
                    }
                }
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            set => throw new NotSupportedException();
        }

        public bool Contains(object? value)
        {
            if (value == null) return false;
            return _owner._items.Any(e => e.Checked && Equals(e.Value, value));
        }

        public int IndexOf(object? value)
        {
            if (value == null) return -1;
            int idx = 0;
            foreach (var e in _owner._items)
            {
                if (e.Checked)
                {
                    if (Equals(e.Value, value)) return idx;
                    idx++;
                }
            }
            return -1;
        }

        public int Add(object? value) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public void Insert(int index, object? value) => throw new NotSupportedException();
        public void Remove(object? value) => throw new NotSupportedException();
        public void RemoveAt(int index) => throw new NotSupportedException();

        public void CopyTo(Array array, int index)
        {
            int i = 0;
            foreach (var e in _owner._items)
            {
                if (e.Checked)
                {
                    array.SetValue(e.Value, index + i);
                    i++;
                }
            }
        }

        public IEnumerator GetEnumerator()
        {
            foreach (var e in _owner._items)
            {
                if (e.Checked) yield return e.Value;
            }
        }
    }

    public class CheckedIndexCollection : IList, ICollection, IEnumerable<int>
    {
        private readonly ModernCheckedListBox _owner;

        internal CheckedIndexCollection(ModernCheckedListBox owner) => _owner = owner;

        public int Count => _owner._items.Count(e => e.Checked);
        public bool IsReadOnly => true;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public int this[int index]
        {
            get
            {
                int current = 0;
                for (int i = 0; i < _owner._items.Count; i++)
                {
                    if (_owner._items[i].Checked)
                    {
                        if (current == index) return i;
                        current++;
                    }
                }
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        object? IList.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        public bool Contains(int index) => _owner.GetItemChecked(index);
        bool IList.Contains(object? value) => value is int i && Contains(i);

        public int IndexOf(int index)
        {
            int current = 0;
            for (int i = 0; i < _owner._items.Count; i++)
            {
                if (_owner._items[i].Checked)
                {
                    if (i == index) return current;
                    current++;
                }
            }
            return -1;
        }
        int IList.IndexOf(object? value) => value is int i ? IndexOf(i) : -1;

        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();

        public void CopyTo(Array array, int index)
        {
            int cur = 0;
            for (int i = 0; i < _owner._items.Count; i++)
            {
                if (_owner._items[i].Checked)
                {
                    array.SetValue(i, index + cur);
                    cur++;
                }
            }
        }

        public IEnumerator<int> GetEnumerator()
        {
            for (int i = 0; i < _owner._items.Count; i++)
            {
                if (_owner._items[i].Checked) yield return i;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
