using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace MinRectangle;

public partial class MainWindow : Window
{
    // ── стан ────────────────────────────────────────────────────────────────
    private readonly List<Point> _points = new();
    private readonly Random      _rng    = new();

    private readonly List<Control> _gridControls  = new();
    private readonly List<Control> _pointControls = new();
    private readonly List<Control> _hullControls  = new();
    private readonly List<Control> _rectControls  = new();
    private readonly List<Control> _labelControls = new();

    private static readonly IBrush PointBrush = new SolidColorBrush(Color.Parse("#2c3e50"));
    private static readonly IBrush HullBrush  = new SolidColorBrush(Color.Parse("#2980b9"));
    private static readonly IBrush RectBrush  = new SolidColorBrush(Color.Parse("#e74c3c"));
    private static readonly IBrush LabelBrush = new SolidColorBrush(Color.Parse("#c0392b"));

    private static readonly char[] Separators = { ' ', '\t', ',', ';' };

    public MainWindow()
    {
        InitializeComponent();

        RbMouse.IsCheckedChanged += OnModeChanged;
        RbAuto.IsCheckedChanged  += OnModeChanged;
        BtnGenerate.Click        += OnGenerate;
        BtnLoadFile.Click        += OnLoadFile;
        BtnRun.Click             += OnRun;
        BtnClear.Click           += OnClear;
        DrawCanvas.PointerPressed += OnCanvasClick;

        this.Opened += (_, _) =>
        {
            DrawGrid();
            DrawCanvas.SizeChanged += (_, _) => { RemoveLayer(_gridControls); DrawGrid(); };
        };
    }

    // ── режим ────────────────────────────────────────────────────────────────
    private void OnModeChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        bool auto = RbAuto.IsChecked == true;
        TbCount.IsEnabled     = auto;
        BtnGenerate.IsEnabled = auto;
        DrawCanvas.Cursor = auto
            ? new Cursor(StandardCursorType.Arrow)
            : new Cursor(StandardCursorType.Cross);
    }

    // ── ввід мишою ───────────────────────────────────────────────────────────
    private void OnCanvasClick(object? sender, PointerPressedEventArgs e)
    {
        if (RbMouse.IsChecked != true) return;
        if (_points.Count >= 100) { ShowMsg("Ліміт", "Максимум 100 точок у режимі миші."); return; }
        var pos = e.GetPosition(DrawCanvas);
        _points.Add(pos);
        AddDot(pos, _pointControls);
        LblPts.Text = $"Точок: {_points.Count}";
    }

    // ── автогенерація ────────────────────────────────────────────────────────
    private void OnGenerate(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!int.TryParse(TbCount.Text, out int n) || n < 1 || n > 20000)
        {
            ShowMsg("Помилка", "Введіть ціле число від 1 до 20 000."); return;
        }
        double w = DrawCanvas.Bounds.Width  > 10 ? DrawCanvas.Bounds.Width  : 820;
        double h = DrawCanvas.Bounds.Height > 10 ? DrawCanvas.Bounds.Height : 600;
        const double m = 20;
        _points.Clear();
        for (int i = 0; i < n; i++)
            _points.Add(new Point(m + _rng.NextDouble() * (w - 2 * m),
                                  m + _rng.NextDouble() * (h - 2 * m)));
        LblFile.Text = "";
        RedrawPoints();
    }

    // ── завантаження з файлу ─────────────────────────────────────────────────
    // Формат: по одній точці на рядок, X та Y розділені пробілом/табом/комою/крапкою з комою.
    // Рядки з '#' ігноруються як коментарі.
    private async void OnLoadFile(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Відкрити файл з точками",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Текстові файли") { Patterns = new[] { "*.txt", "*.csv" } },
                new FilePickerFileType("Всі файли")      { Patterns = new[] { "*" } }
            }
        });

        if (files.Count == 0) return;

        var raw = new List<(double X, double Y)>();
        using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#')) continue;
            var parts = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 &&
                double.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double x) &&
                double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out double y))
                raw.Add((x, y));
        }

        if (raw.Count == 0) { ShowMsg("Помилка", "Файл не містить коректних точок."); return; }

        // масштабуємо координати під розмір полотна
        double cw = DrawCanvas.Bounds.Width  > 10 ? DrawCanvas.Bounds.Width  : 820;
        double ch = DrawCanvas.Bounds.Height > 10 ? DrawCanvas.Bounds.Height : 600;
        const double margin = 20;

        double minX = raw.Min(p => p.X), maxX = raw.Max(p => p.X);
        double minY = raw.Min(p => p.Y), maxY = raw.Max(p => p.Y);
        double rx = maxX - minX, ry = maxY - minY;

        double sx = rx > 1e-9 ? (cw - 2 * margin) / rx : 1;
        double sy = ry > 1e-9 ? (ch - 2 * margin) / ry : 1;
        double s  = Math.Min(sx, sy);

        _points.Clear();
        foreach (var (px, py) in raw)
            _points.Add(new Point(margin + (px - minX) * s,
                                  margin + (py - minY) * s));

        LblFile.Text = $"📄 {files[0].Name}  ({_points.Count} точок)";
        RedrawPoints();
    }

    // ── алгоритм ─────────────────────────────────────────────────────────────
    private void OnRun(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_points.Count < 2) { ShowMsg("Мало точок", "Додайте хоча б 2 точки."); return; }

        RemoveLayer(_hullControls);
        RemoveLayer(_rectControls);
        RemoveLayer(_labelControls);

        var sw     = Stopwatch.StartNew();
        var hull   = Geometry.GrahamScan(_points);
        var result = Geometry.MinBoundingRect(hull);
        sw.Stop();

        if (hull.Count >= 2)
            AddPolyline(hull.Append(hull[0]), HullBrush, 1.5, _hullControls);

        if (result.HasValue)
        {
            var (area, corners) = result.Value;
            AddPolyline(corners.Append(corners[0]), RectBrush, 2.5, _rectControls, dash: true);
            double cx = corners.Average(p => p.X);
            double cy = corners.Average(p => p.Y);
            AddLabel($"S = {area:F0} px²", cx, cy);
            LblHull.Text = $"Оболонка: {hull.Count} вершин";
            LblArea.Text = $"Площа: {area:F1} px²";
        }
        LblTime.Text = $"Час: {sw.Elapsed.TotalMilliseconds:F2} мс";
    }

    // ── очистити ─────────────────────────────────────────────────────────────
    private void OnClear(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _points.Clear();
        DrawCanvas.Children.Clear();
        _gridControls.Clear();  _pointControls.Clear();
        _hullControls.Clear();  _rectControls.Clear();  _labelControls.Clear();
        DrawGrid();
        LblPts.Text  = "Точок: 0";
        LblHull.Text = "Оболонка: —";
        LblArea.Text = "Площа: —";
        LblTime.Text = "Час: —";
        LblFile.Text = "";
    }

    // ══ Drawing ══════════════════════════════════════════════════════════════

    private void DrawGrid()
    {
        double w = DrawCanvas.Bounds.Width  > 10 ? DrawCanvas.Bounds.Width  : 820;
        double h = DrawCanvas.Bounds.Height > 10 ? DrawCanvas.Bounds.Height : 600;
        var brush = new SolidColorBrush(Color.Parse("#e0e0e0"));
        for (double x = 0; x <= w; x += 50) AddLine(new Point(x, 0), new Point(x, h), brush, _gridControls);
        for (double y = 0; y <= h; y += 50) AddLine(new Point(0, y), new Point(w, y), brush, _gridControls);
    }

    private void AddLine(Point a, Point b, IBrush brush, List<Control> layer)
    {
        var l = new Line { StartPoint = a, EndPoint = b, Stroke = brush, StrokeThickness = 1 };
        DrawCanvas.Children.Add(l);
        layer.Add(l);
    }

    private void AddDot(Point p, List<Control> layer, double r = 3.5)
    {
        var el = new Ellipse { Width = r * 2, Height = r * 2, Fill = PointBrush };
        Canvas.SetLeft(el, p.X - r);
        Canvas.SetTop(el,  p.Y - r);
        DrawCanvas.Children.Add(el);
        layer.Add(el);
    }

    private void AddPolyline(IEnumerable<Point> pts, IBrush brush,
                              double thickness, List<Control> layer, bool dash = false)
    {
        var pl = new Polyline
        {
            Points          = new AvaloniaList<Point>(pts),
            Stroke          = brush,
            StrokeThickness = thickness
        };
        if (dash) pl.StrokeDashArray = new AvaloniaList<double> { 8, 4 };
        DrawCanvas.Children.Add(pl);
        layer.Add(pl);
    }

    private void AddLabel(string text, double cx, double cy)
    {
        var tb = new TextBlock
        {
            Text       = text,
            Foreground = LabelBrush,
            FontSize   = 12,
            FontWeight = FontWeight.Bold
        };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, cx - tb.DesiredSize.Width  / 2);
        Canvas.SetTop(tb,  cy - tb.DesiredSize.Height / 2);
        DrawCanvas.Children.Add(tb);
        _labelControls.Add(tb);
    }

    private void RemoveLayer(List<Control> layer)
    {
        foreach (var c in layer) DrawCanvas.Children.Remove(c);
        layer.Clear();
    }

    private void RedrawPoints()
    {
        DrawCanvas.Children.Clear();
        _gridControls.Clear();  _pointControls.Clear();
        _hullControls.Clear();  _rectControls.Clear();  _labelControls.Clear();
        DrawGrid();
        double r = _points.Count > 500 ? 1.5 : 3;
        foreach (var p in _points) AddDot(p, _pointControls, r);
        LblPts.Text  = $"Точок: {_points.Count}";
        LblHull.Text = "Оболонка: —";
        LblArea.Text = "Площа: —";
        LblTime.Text = "Час: —";
    }

    // ── діалог ───────────────────────────────────────────────────────────────
    private void ShowMsg(string title, string msg)
    {
        var dlg = new Window
        {
            Title  = title,
            Width  = 320,
            Height = 130,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize  = false,
            Background = new SolidColorBrush(Color.Parse("#2b2b2b"))
        };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock
        {
            Text           = msg,
            Foreground     = Brushes.White,
            TextWrapping   = Avalonia.Media.TextWrapping.Wrap
        });
        var ok = new Button
        {
            Content              = "OK",
            HorizontalAlignment  = Avalonia.Layout.HorizontalAlignment.Center,
            Background           = new SolidColorBrush(Color.Parse("#3a6ea8")),
            Foreground           = Brushes.White,
            Padding              = new Thickness(20, 6)
        };
        ok.Click += (_, _) => dlg.Close();
        panel.Children.Add(ok);
        dlg.Content = panel;
        dlg.ShowDialog(this);
    }
}
