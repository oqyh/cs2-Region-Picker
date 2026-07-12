using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace CS2RegionPicker;

public class WorldMap : Canvas
{
    public event Action<MainWindow.PopItem>? PopClicked;

    private Brush _sea    = Brushes.Black;
    private Brush _land   = Brushes.Gray;
    private Brush _grid   = Brushes.DimGray;
    private Brush _accent = Brushes.Cyan;
    private Brush _pin    = Brushes.Orange;
    private Brush _good   = Brushes.LimeGreen;
    private Brush _medium = Brushes.Gold;
    private Brush _bad    = Brushes.OrangeRed;
    private Brush _dim    = Brushes.DimGray;
    private Brush _pendingBrush = Freeze("#5E81AC");
    private Brush _blockedBrush = Freeze("#C74E7B");

    private static Brush Freeze(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private (double Lat, double Lon)? _home;
    private List<MainWindow.PopItem> _pops = new();
    private Geometry? _cachedLand;

    private Border? _infoBox;
    private Image? _infoFlag;
    private TextBlock? _infoTitle;
    private TextBlock? _infoSub;
    private Brush _infoBg   = Brushes.Black;
    private Brush _infoFg   = Brushes.White;
    private Brush _infoSubFg = Brushes.Gray;
    private readonly ScaleTransform _infoScale = new();

    private double _zoom = 1.0;
    private double _panX = 0, _panY = 0;
    private bool _dragging;
    private Point _dragStart;
    private double _panStartX, _panStartY;
    private readonly TranslateTransform _pan = new();
    private readonly ScaleTransform _scale = new();

    public WorldMap()
    {
        ClipToBounds = true;
        SizeChanged += (_, _) => { _cachedLand = null; ClampPan(); ApplyTransform(); Redraw(); };

        var tg = new TransformGroup();
        tg.Children.Add(_scale);
        tg.Children.Add(_pan);
        RenderTransform = tg;

        MouseWheel += OnWheel;
        MouseLeftButtonDown += OnDragStart;
        MouseMove += OnDrag;
        MouseLeftButtonUp += OnDragEnd;
        MouseLeave += (_, _) => { if (_dragging) EndDrag(); };
        Cursor = System.Windows.Input.Cursors.Hand;
    }

    private void OnWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        Point m = e.GetPosition(this);
        double old = _zoom;
        double factor = e.Delta > 0 ? 1.15 : 1 / 1.15;
        _zoom = Math.Max(1.0, Math.Min(8.0, _zoom * factor));
        if (Math.Abs(_zoom - old) < 0.0001) return;

        double scaleChange = _zoom / old;
        _panX = m.X - (m.X - _panX) * scaleChange;
        _panY = m.Y - (m.Y - _panY) * scaleChange;

        ClampPan();
        ApplyTransform();
        ScheduleMarkerRedraw();
        e.Handled = true;
    }

    private bool _redrawQueued;
    private void ScheduleMarkerRedraw()
    {
        if (_redrawQueued) return;
        _redrawQueued = true;
        Dispatcher.BeginInvoke(new Action(() => { _redrawQueued = false; Redraw(); }),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnDragStart(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {

        if (e.Handled) return;
        _dragging = true;
        _dragStart = e.GetPosition(this);
        _panStartX = _panX; _panStartY = _panY;
        Cursor = System.Windows.Input.Cursors.ScrollAll;
        CaptureMouse();
    }

    private void OnDrag(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging) return;
        Point p = e.GetPosition(this);
        _panX = _panStartX + (p.X - _dragStart.X);
        _panY = _panStartY + (p.Y - _dragStart.Y);
        ClampPan();
        ApplyTransform();
    }

    private void OnDragEnd(object sender, System.Windows.Input.MouseButtonEventArgs e) => EndDrag();

    private void EndDrag()
    {
        if (!_dragging) return;
        _dragging = false;
        Cursor = System.Windows.Input.Cursors.Hand;
        ReleaseMouseCapture();
    }

    private void ClampPan()
    {
        double w = ActualWidth, h = ActualHeight;
        double minX = w - w * _zoom, minY = h - h * _zoom;
        _panX = Math.Max(minX, Math.Min(0, _panX));
        _panY = Math.Max(minY, Math.Min(0, _panY));
    }

    private void ApplyTransform()
    {
        _scale.ScaleX = _scale.ScaleY = _zoom;
        _pan.X = _panX;
        _pan.Y = _panY;
    }

    public void SetTheme(Brush sea, Brush land, Brush grid, Brush accent, Brush pin,
                         Brush good, Brush medium, Brush bad, Brush dim)
    {
        _sea = sea; _land = land; _grid = grid; _accent = accent; _pin = pin;
        _good = good; _medium = medium; _bad = bad; _dim = dim;
        _cachedLand = null;
        Redraw();
    }

    public void SetInfoColors(Brush bg, Brush fg, Brush subFg)
    {
        _infoBg = bg; _infoFg = fg; _infoSubFg = subFg;
    }

    public void SetHome(double lat, double lon)
    {
        _home = (lat, lon);
        Redraw();
    }

    public void SetPops(List<MainWindow.PopItem> pops)
    {
        _pops = pops;
        Redraw();
    }

    private Point Project(double lat, double lon)
    {
        double w = ActualWidth, h = ActualHeight;
        double x = (lon + 180.0) / 360.0 * w;
        double y = (90.0 - lat) / 180.0 * h;
        return new Point(x, y);
    }

    private void Redraw()
    {
        Children.Clear();
        _infoBox = null;
        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10)
        {
            return;
        }

        var seaBase = (_sea as SolidColorBrush)?.Color ?? Colors.Black;
        Background = new LinearGradientBrush(
            Darken(seaBase, 0.85), Lighten(seaBase, 1.08), 90);

        for (int lon = -180; lon <= 180; lon += 30)
        {
            Point a = Project(-85, lon), b = Project(85, lon);
            AddLine(a, b, _grid, 0.4 / _zoom);
        }
        for (int lat = -60; lat <= 60; lat += 30)
        {
            Point a = Project(lat, -180), b = Project(lat, 180);
            AddLine(a, b, _grid, 0.4 / _zoom);
        }

        if (_cachedLand == null)
        {
            var group = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (string path in WorldPaths.Continents)
            {
                try
                {
                    group.Children.Add(Geometry.Parse(ScalePath(path, w, h)));
                }
                catch
                {

                }
            }
            if (group.CanFreeze)
            {
                group.Freeze();
            }
            _cachedLand = group;
        }

        var landColor = (_land as SolidColorBrush)?.Color ?? Colors.Gray;
        Children.Add(new Path
        {
            Data = _cachedLand,
            Fill = new LinearGradientBrush(Lighten(landColor, 1.12), landColor, 90),
            Stroke = _accent,
            StrokeThickness = 0.4 / _zoom,
            Opacity = 0.96,
            IsHitTestVisible = false
        });

        if (_home.HasValue)
        {
            double linv = 1.0 / _zoom;
            Point hp = Project(_home.Value.Lat, _home.Value.Lon);
            foreach (MainWindow.PopItem pop in _pops)
            {
                var coord = pop.Coord;
                if (coord == null || !pop.IsAllowed)
                {
                    continue;
                }
                Point pp = Project(coord.Value.Lat, coord.Value.Lon);
                var line = new Line
                {
                    X1 = hp.X, Y1 = hp.Y, X2 = pp.X, Y2 = pp.Y,
                    Stroke = _accent, StrokeThickness = 1 * linv, Opacity = 0.35,
                    StrokeDashArray = new DoubleCollection { 3, 3 }
                };
                Children.Add(line);
            }
        }

        foreach (MainWindow.PopItem pop in _pops)
        {
            var coord = pop.Coord;
            if (coord == null)
            {
                continue;
            }
            Point p = Project(coord.Value.Lat, coord.Value.Lon);
            AddPopMarker(pop, p);
        }

        if (_home.HasValue)
        {
            AddHomePin(Project(_home.Value.Lat, _home.Value.Lon));
        }
    }

    private void ShowInfo(MainWindow.PopItem pop, Point mouse)
    {
        if (_infoBox == null)
        {
            _infoFlag = new Image
            {
                Width = 20, Height = 15,
                Stretch = Stretch.UniformToFill,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0)
            };
            RenderOptions.SetBitmapScalingMode(_infoFlag, BitmapScalingMode.HighQuality);

            _infoTitle = new TextBlock
            {
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var titleRow = new StackPanel { Orientation = Orientation.Horizontal };
            titleRow.Children.Add(_infoFlag);
            titleRow.Children.Add(_infoTitle);

            _infoSub = new TextBlock { FontSize = 11 };

            var stack = new StackPanel();
            stack.Children.Add(titleRow);
            stack.Children.Add(_infoSub);

            _infoBox = new Border
            {
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 6, 10, 7),
                Child = stack,
                IsHitTestVisible = false,
                RenderTransform = _infoScale,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 12, ShadowDepth = 2, Opacity = 0.35, Color = Colors.Black
                }
            };
            Children.Add(_infoBox);
        }

        _infoBox.Background = _infoBg;
        _infoBox.BorderBrush = _accent;
        _infoBox.BorderThickness = new Thickness(1);

        string country = Flags.Country(pop.PrimaryCode) ?? "";
        if (country.Length > 0)
        {
            try
            {
                _infoFlag!.Source = new BitmapImage(
                    new Uri($"pack://application:,,,/flags/{country.ToLowerInvariant()}.png"));
                _infoFlag.Visibility = Visibility.Visible;
            }
            catch { _infoFlag!.Visibility = Visibility.Collapsed; }
        }
        else
        {
            _infoFlag!.Visibility = Visibility.Collapsed;
        }

        _infoTitle!.Foreground = _infoFg;
        _infoTitle.Text = pop.Desc;

        _infoSub!.Inlines.Clear();
        _infoSub.Inlines.Add(new System.Windows.Documents.Run(pop.CodeText + "   ·   ") { Foreground = _infoSubFg });

        var pingRun = new System.Windows.Documents.Run(pop.PingText) { Foreground = PingColor(pop), FontWeight = FontWeights.Bold };
        _infoSub.Inlines.Add(pingRun);

        _infoSub.Inlines.Add(new System.Windows.Documents.Run("   +   ") { Foreground = _infoSubFg });

        var stateRun = new System.Windows.Documents.Run(
            pop.IsPending ? "◆ " + Loc.T("state_pending")
                          : (pop.IsAllowed ? "● " + Loc.T("state_allowed")
                                           : "✕ " + Loc.T("state_blocked")))
        {
            Foreground = pop.IsPending ? _pendingBrush : (pop.IsAllowed ? _good : _blockedBrush),
            FontWeight = FontWeights.Bold
        };
        _infoSub.Inlines.Add(stateRun);

        _infoBox.Visibility = Visibility.Visible;
        MoveInfo(mouse);
    }

    private void MoveInfo(Point mouse)
    {
        if (_infoBox == null || _infoBox.Visibility != Visibility.Visible) return;

        double inv = 1.0 / _zoom;
        _infoScale.ScaleX = _infoScale.ScaleY = inv;

        _infoBox.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = _infoBox.DesiredSize.Width * inv, h = _infoBox.DesiredSize.Height * inv;
        double off = 14 * inv;

        double viewLeft = -_panX * inv, viewTop = -_panY * inv;
        double viewRight = viewLeft + ActualWidth * inv, viewBottom = viewTop + ActualHeight * inv;

        double x = mouse.X + off, y = mouse.Y + off;
        if (x + w > viewRight) x = mouse.X - w - off;
        if (y + h > viewBottom) y = mouse.Y - h - off;

        SetLeft(_infoBox, Math.Max(viewLeft + 2 * inv, x));
        SetTop(_infoBox, Math.Max(viewTop + 2 * inv, y));
    }

    private void HideInfo()
    {
        if (_infoBox != null) _infoBox.Visibility = Visibility.Collapsed;
    }

    private void AddPopMarker(MainWindow.PopItem pop, Point p)
    {

        Brush fill = pop.IsPending ? _pendingBrush
                   : pop.IsAllowed ? PingColor(pop)
                   : _blockedBrush;

        double inv = 1.0 / _zoom;
        double r = (pop.IsAllowed ? 5 : 4) * inv;
        double stroke = (pop.IsAllowed ? 1.5 : 1) * inv;

        var dot = new Ellipse
        {
            Width = r * 2, Height = r * 2,
            Fill = fill,
            Stroke = pop.IsPending ? _pendingBrush : (pop.IsAllowed ? _accent : _blockedBrush),
            StrokeThickness = stroke,
            IsHitTestVisible = false
        };
        SetLeft(dot, p.X - r);
        SetTop(dot, p.Y - r);

        double hitR = 10 * inv;
        var hit = new Ellipse
        {
            Width = hitR * 2, Height = hitR * 2,
            Fill = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = pop
        };
        SetLeft(hit, p.X - hitR);
        SetTop(hit, p.Y - hitR);
        hit.MouseLeftButtonDown += (s, e) =>
        {
            if (((FrameworkElement)s).Tag is MainWindow.PopItem clicked) PopClicked?.Invoke(clicked);
            e.Handled = true;
        };
        hit.MouseEnter += (s, e) => ShowInfo(pop, e.GetPosition(this));
        hit.MouseMove  += (s, e) => MoveInfo(e.GetPosition(this));
        hit.MouseLeave += (s, _) => HideInfo();

        if (!pop.IsAllowed && !pop.IsPending)
        {
            double x = 4 * inv;
            AddLine(new Point(p.X - x, p.Y - x), new Point(p.X + x, p.Y + x), _blockedBrush, 1.2 * inv);
            AddLine(new Point(p.X - x, p.Y + x), new Point(p.X + x, p.Y - x), _blockedBrush, 1.2 * inv);
        }
        else if (pop.IsAllowed && !pop.IsPending)
        {

            var halo = new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Stroke = fill, StrokeThickness = 1.5 * inv, Opacity = 0.6,
                IsHitTestVisible = false
            };
            SetLeft(halo, p.X - r);
            SetTop(halo, p.Y - r);
            Children.Add(halo);

            var grow = new DoubleAnimation(1, 2.6, TimeSpan.FromSeconds(1.8)) { RepeatBehavior = RepeatBehavior.Forever };
            var fade = new DoubleAnimation(0.6, 0, TimeSpan.FromSeconds(1.8)) { RepeatBehavior = RepeatBehavior.Forever };
            var st = new ScaleTransform();
            halo.RenderTransform = st;
            halo.RenderTransformOrigin = new Point(0.5, 0.5);
            st.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            halo.BeginAnimation(OpacityProperty, fade);
        }

        Children.Add(dot);
        Children.Add(hit);
    }

    private void AddHomePin(Point p)
    {

        double inv = 1.0 / _zoom;

        double ringR = 5 * inv;
        var ring = new Ellipse
        {
            Width = ringR * 2, Height = ringR * 2,
            Stroke = _pin, StrokeThickness = 2 * inv, Opacity = 0.7,
            IsHitTestVisible = false
        };
        SetLeft(ring, p.X - ringR);
        SetTop(ring, p.Y - ringR);
        Children.Add(ring);
        var st = new ScaleTransform();
        ring.RenderTransform = st;
        ring.RenderTransformOrigin = new Point(0.5, 0.5);
        ring.BeginAnimation(OpacityProperty, new DoubleAnimation(0.7, 0, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever });
        st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 5, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever });
        st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 5, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever });

        double dh = 8 * inv, dw = 6 * inv;
        var diamond = new Polygon
        {
            Points = new PointCollection
            {
                new(p.X, p.Y - dh), new(p.X + dw, p.Y), new(p.X, p.Y + dh), new(p.X - dw, p.Y)
            },
            Fill = _pin, Stroke = Brushes.White, StrokeThickness = 1.5 * inv,
            IsHitTestVisible = false
        };
        Children.Add(diamond);

        var label = new TextBlock
        {
            Text = Loc.T("you"), Foreground = _pin, FontSize = 10, FontWeight = FontWeights.Bold,

            FontFamily = new FontFamily(Loc.IsRtl ? "Segoe UI" : "Consolas"),
            IsHitTestVisible = false,
            RenderTransform = new ScaleTransform(inv, inv)
        };
        SetLeft(label, p.X + 8 * inv);
        SetTop(label, p.Y - 6 * inv);
        Children.Add(label);
    }

    private Brush PingColor(MainWindow.PopItem pop)
    {

        if (pop.PingText.EndsWith("ms") && int.TryParse(pop.PingText.Replace(" ms", ""), out int ms))
        {
            return ms < 70 ? _good : ms < 140 ? _medium : _bad;
        }
        return _accent;
    }

    private static Color Lighten(Color c, double factor)
    {
        return Color.FromRgb(
            (byte)Math.Min(255, c.R * factor),
            (byte)Math.Min(255, c.G * factor),
            (byte)Math.Min(255, c.B * factor));
    }

    private static Color Darken(Color c, double factor)
    {
        return Color.FromRgb(
            (byte)(c.R * factor),
            (byte)(c.G * factor),
            (byte)(c.B * factor));
    }

    private void AddLine(Point a, Point b, Brush stroke, double thickness)
    {
        Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = stroke, StrokeThickness = thickness, IsHitTestVisible = false });
    }

    private string ScalePath(string path, double w, double h)
    {
        double sx = w / 360.0, sy = h / 180.0;
        var sb = new System.Text.StringBuilder(path.Length + 32);
        var inv = System.Globalization.CultureInfo.InvariantCulture;

        foreach (string token in path.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length == 1 && char.IsLetter(token[0]))
            {
                sb.Append(token).Append(' ');
                continue;
            }

            int comma = token.IndexOf(',');
            if (comma <= 0)
            {
                continue;
            }

            if (double.TryParse(token.AsSpan(0, comma), System.Globalization.NumberStyles.Float, inv, out double x) &&
                double.TryParse(token.AsSpan(comma + 1), System.Globalization.NumberStyles.Float, inv, out double y))
            {
                sb.Append((x * sx).ToString("F1", inv)).Append(',')
                  .Append((y * sy).ToString("F1", inv)).Append(' ');
            }
        }
        return sb.ToString();
    }
}
