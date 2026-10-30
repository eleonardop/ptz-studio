using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PtzStudio.App.Ui;

internal static class Ux
{
    public static readonly Brush Accent = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
    public static readonly Brush Danger = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
    public static readonly Brush Ok = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
    public static readonly Brush Warn = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));
    public static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

    public static TextBlock Text(string t, double size = 15, bool bold = false, Brush? fg = null) => new()
    {
        Text = t, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        Margin = new Thickness(0, 4, 0, 4), TextWrapping = TextWrapping.Wrap, Foreground = fg ?? Brushes.Black
    };

    public static Button Btn(string text, Action click, double minWidth = 150, Brush? bg = null, Brush? fg = null)
    {
        var b = new Button
        {
            Content = text, MinWidth = minWidth, MinHeight = 42, Margin = new Thickness(4), Padding = new Thickness(14, 6, 14, 6),
            FontSize = 15
        };
        if (bg != null) { b.Background = bg; b.Foreground = fg ?? Brushes.White; b.FontWeight = FontWeights.SemiBold; }
        b.Click += (_, _) => click();
        return b;
    }

    public static StackPanel H(params UIElement[] c)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var e in c) p.Children.Add(e);
        return p;
    }

    public static StackPanel V(params UIElement[] c)
    {
        var p = new StackPanel { Orientation = Orientation.Vertical };
        foreach (var e in c) p.Children.Add(e);
        return p;
    }

    public static WrapPanel Wrap(params UIElement[] c)
    {
        var p = new WrapPanel();
        foreach (var e in c) p.Children.Add(e);
        return p;
    }

    public static GroupBox Group(string header, UIElement content) => new()
    {
        Header = header, Content = content, Margin = new Thickness(0, 6, 0, 10), Padding = new Thickness(10),
        FontSize = 15, FontWeight = FontWeights.SemiBold
    };

    public static ScrollViewer Scroll(UIElement c) => new()
    {
        Content = c, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        Padding = new Thickness(16)
    };

    public static UIElement SliderRow(string label, double min, double max, double value, Action<double> set, string fmt = "0.00")
    {
        var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 160 });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
        var l = Text(label, 14); Grid.SetColumn(l, 0);
        var val = Text(value.ToString(fmt), 14); Grid.SetColumn(val, 2); val.HorizontalAlignment = HorizontalAlignment.Right;
        var s = new Slider { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), VerticalAlignment = VerticalAlignment.Center, Height = 28 };
        Grid.SetColumn(s, 1);
        s.ValueChanged += (_, e) => { set(e.NewValue); val.Text = e.NewValue.ToString(fmt); };
        g.Children.Add(l); g.Children.Add(s); g.Children.Add(val);
        return g;
    }

    public static CheckBox Check(string text, bool value, Action<bool> set)
    {
        var c = new CheckBox { Content = text, IsChecked = value, FontSize = 15, Margin = new Thickness(0, 5, 18, 5) };
        c.Checked += (_, _) => set(true);
        c.Unchecked += (_, _) => set(false);
        return c;
    }

    public static ComboBox Combo(IList<string> items, int selected, Action<int> set, double width = 240)
    {
        var c = new ComboBox { Width = width, FontSize = 15, MinHeight = 34, Margin = new Thickness(4) };
        foreach (var i in items) c.Items.Add(i);
        c.SelectedIndex = Math.Clamp(selected, -1, items.Count - 1);
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) set(c.SelectedIndex); };
        return c;
    }

    public static TextBox Input(string text, Action<string> set, double width = 220)
    {
        var t = new TextBox { Text = text, Width = width, FontSize = 15, MinHeight = 34, Margin = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Center };
        t.LostFocus += (_, _) => set(t.Text);
        return t;
    }

    public static UIElement Labeled(string label, UIElement control, double labelWidth = 230)
    {
        var l = Text(label, 14); l.Width = labelWidth; l.VerticalAlignment = VerticalAlignment.Center;
        return H(l, control);
    }

    public static string? Prompt(Window owner, string title, string label, string def = "")
    {
        var w = new Window
        {
            Title = title, Owner = owner, WindowStartupLocation = WindowStartupLocation.CenterOwner, SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize, MinWidth = 380
        };
        var tb = new TextBox { Text = def, FontSize = 15, MinHeight = 34, Margin = new Thickness(0, 6, 0, 12) };
        string? result = null;
        var ok = new Button { Content = "OK", MinWidth = 90, MinHeight = 36, IsDefault = true, Margin = new Thickness(4) };
        var cancel = new Button { Content = "Cancelar", MinWidth = 90, MinHeight = 36, IsCancel = true, Margin = new Thickness(4) };
        ok.Click += (_, _) => { result = tb.Text; w.DialogResult = true; };
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = label, FontSize = 15 });
        panel.Children.Add(tb);
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(ok); row.Children.Add(cancel);
        panel.Children.Add(row);
        w.Content = panel;
        tb.Loaded += (_, _) => { tb.Focus(); tb.SelectAll(); };
        return w.ShowDialog() == true ? result : null;
    }
}

/// <summary>Base das abas: reconstrói o conteúdo quando perfil/configurações mudam e liga um timer apenas enquanto visível.</summary>
internal abstract class TabBase : UserControl
{
    protected readonly Engine.AppEngine E;
    private readonly System.Windows.Threading.DispatcherTimer _t = new() { Interval = TimeSpan.FromMilliseconds(80) };

    protected TabBase(Engine.AppEngine e)
    {
        E = e;
        _t.Tick += (_, _) => Refresh();
        Loaded += (_, _) => { _t.Start(); };
        Unloaded += (_, _) => _t.Stop();
    }

    /// <summary>Reconstrói a aba. Antes, desanexa a árvore antiga: vários elementos (TextBlocks, TextBoxes) são reaproveitados
    /// entre reconstruções e o WPF não permite que um elemento tenha dois pais.</summary>
    public void Rebuild()
    {
        var old = Content;
        Content = null;
        Detach(old);
        Build();
    }

    private static void Detach(object? o)
    {
        switch (o)
        {
            case Panel p:
                foreach (var c in p.Children.Cast<object>().ToList()) Detach(c);
                p.Children.Clear();
                break;
            case ContentControl cc:
                var inner = cc.Content; cc.Content = null; Detach(inner);
                break;
            case Decorator d:
                var ch = d.Child; d.Child = null; Detach(ch);
                break;
        }
    }
    protected abstract void Build();
    protected virtual void Refresh() { }
    protected void Init() => Build();
}
