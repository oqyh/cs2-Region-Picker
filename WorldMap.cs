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

    private readonly Canvas _staticLayer = new();
    private readonly Canvas _lineLayer   = new();
    private readonly Canvas _markerLayer = new();
    private readonly Canvas _topLayer    = new();

    private readonly List<ScaleTransform> _markerScales = new();
    private readonly List<Line> _gridLines = new();
    private readonly List<Line> _connLines = new();
    private Path? _landPath;

    private Border? _infoBox;
    private Image? _infoFlag;
    private TextBlock? _infoTitle;
    private TextBlock? _infoSub;
    private Brush _infoBg    = Brushes.Black;
    private Brush _infoFg    = Brushes.White;
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

        SizeChanged += (_, _) => { _cachedLand = null; ClampPan(); ApplyTransform(); ScheduleFullRebuild(); };

        var tg = new TransformGroup();
        tg.Children.Add(_scale);
        tg.Children.Add(_pan);
        RenderTransform = tg;

        Children.Add(_staticLayer);
        Children.Add(_lineLayer);
        Children.Add(_markerLayer);
        Children.Add(_topLayer);

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
        UpdateZoomDependent();
        e.Handled = true;
    }

    private void UpdateZoomDependent()
    {
        double inv = 1.0 / _zoom;

        foreach (ScaleTransform st in _markerScales)
        {
            st.ScaleX = inv;
            st.ScaleY = inv;
        }

        foreach (Line l in _gridLines) l.StrokeThickness = 0.4 * inv;
        foreach (Line l in _connLines) l.StrokeThickness = 1.4 * inv;
        if (_landPath != null) _landPath.StrokeThickness = 0.4 * inv;

        MoveInfoToLast();
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
                         Brush good, Brush medium, Brush bad)
    {
        _sea = sea; _land = land; _grid = grid; _accent = accent; _pin = pin;
        _good = good; _medium = medium; _bad = bad;
        _cachedLand = null;
        RebuildAll();
    }

    public void SetInfoColors(Brush bg, Brush fg, Brush subFg)
    {
        _infoBg = bg; _infoFg = fg; _infoSubFg = subFg;
    }

    public void SetHome(double lat, double lon)
    {
        _home = (lat, lon);
        ScheduleOverlayRebuild();
    }

    public void SetPops(List<MainWindow.PopItem> pops)
    {
        _pops = pops;
        ScheduleOverlayRebuild();
    }

    private bool _overlayQueued;
    private void ScheduleOverlayRebuild()
    {
        if (_overlayQueued) return;
        _overlayQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _overlayQueued = false;
            RebuildOverlay();
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    private bool _fullQueued;
    private void ScheduleFullRebuild()
    {
        if (_fullQueued) return;
        _fullQueued = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _fullQueued = false;
            RebuildAll();
        }), System.Windows.Threading.DispatcherPriority.Render);
    }

    private Point Project(double lat, double lon)
    {
        double w = ActualWidth, h = ActualHeight;
        double x = (lon + 180.0) / 360.0 * w;
        double y = (90.0 - lat) / 180.0 * h;
        return new Point(x, y);
    }

    private void RebuildAll()
    {
        RebuildStatic();
        RebuildOverlay();
    }

    private void RebuildStatic()
    {
        _staticLayer.Children.Clear();
        _gridLines.Clear();
        _landPath = null;

        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10) return;

        var seaBase = (_sea as SolidColorBrush)?.Color ?? Colors.Black;
        var bg = new LinearGradientBrush(Darken(seaBase, 0.85), Lighten(seaBase, 1.08), 90);
        bg.Freeze();
        Background = bg;

        double inv = 1.0 / _zoom;
        for (int lon = -180; lon <= 180; lon += 30)
        {
            Point a = Project(-85, lon), b = Project(85, lon);
            _gridLines.Add(AddLine(_staticLayer, a, b, _grid, 0.4 * inv));
        }
        for (int lat = -60; lat <= 60; lat += 30)
        {
            Point a = Project(lat, -180), b = Project(lat, 180);
            _gridLines.Add(AddLine(_staticLayer, a, b, _grid, 0.4 * inv));
        }

        if (_cachedLand == null)
        {
            var group = new GeometryGroup { FillRule = FillRule.Nonzero };
            foreach (string path in WorldPaths.Continents)
            {
                try { group.Children.Add(Geometry.Parse(ScalePath(path, w, h))); }
                catch { }
            }
            if (group.CanFreeze) group.Freeze();
            _cachedLand = group;
        }

        var landColor = (_land as SolidColorBrush)?.Color ?? Colors.Gray;
        var landFill = new LinearGradientBrush(Lighten(landColor, 1.12), landColor, 90);
        landFill.Freeze();
        _landPath = new Path
        {
            Data = _cachedLand,
            Fill = landFill,
            Stroke = _accent,
            StrokeThickness = 0.4 * inv,
            Opacity = 0.96,
            IsHitTestVisible = false
        };
        _staticLayer.Children.Add(_landPath);
    }

    private void RebuildOverlay()
    {
        HideInfo();
        _lineLayer.Children.Clear();
        _markerLayer.Children.Clear();
        _markerScales.Clear();
        _connLines.Clear();

        double w = ActualWidth, h = ActualHeight;
        if (w < 10 || h < 10) return;

        if (_home.HasValue)
        {
            Point hp = Project(_home.Value.Lat, _home.Value.Lon);
            foreach (MainWindow.PopItem pop in _pops)
            {
                var coord = pop.Coord;
                if (coord == null || !pop.IsAllowed) continue;

                Point pp = Project(coord.Value.Lat, coord.Value.Lon);
                bool applied = !pop.IsPending;
                AddFlowLine(hp, pp, applied ? _good : _pendingBrush, applied ? 0.55 : 0.38, applied);
            }
        }

        var placed = new List<(MainWindow.PopItem Pop, Point P)>();
        foreach (MainWindow.PopItem pop in _pops)
        {
            var coord = pop.Coord;
            if (coord == null) continue;
            placed.Add((pop, Project(coord.Value.Lat, coord.Value.Lon)));
        }

        foreach ((MainWindow.PopItem pop, Point p, Point off) in SpreadOverlaps(placed))
            AddPopMarker(pop, p, off);

        if (_home.HasValue)
            AddHomePin(Project(_home.Value.Lat, _home.Value.Lon));
    }

    private void AddFlowLine(Point from, Point to, Brush stroke, double opacity, bool fast)
    {
        var line = new Line
        {
            X1 = from.X, Y1 = from.Y, X2 = to.X, Y2 = to.Y,
            Stroke = stroke,
            StrokeThickness = 1.4 / _zoom,
            Opacity = opacity,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            StrokeDashCap = PenLineCap.Round,
            IsHitTestVisible = false
        };

        var flow = new DoubleAnimation(7, 0, TimeSpan.FromSeconds(fast ? 0.9 : 1.6))
        {
            RepeatBehavior = RepeatBehavior.Forever
        };
        line.BeginAnimation(Shape.StrokeDashOffsetProperty, flow);

        _lineLayer.Children.Add(line);
        _connLines.Add(line);
    }

    private Canvas NewMarkerRoot(Point p)
    {
        var root = new Canvas();
        var st = new ScaleTransform(1.0 / _zoom, 1.0 / _zoom);
        root.RenderTransform = st;
        SetLeft(root, p.X);
        SetTop(root, p.Y);
        _markerScales.Add(st);
        _markerLayer.Children.Add(root);
        return root;
    }

    private static Ellipse CenteredCircle(double r, Brush? fill, Brush? stroke, double strokeTh)
    {
        var e = new Ellipse
        {
            Width = r * 2, Height = r * 2,
            Fill = fill, Stroke = stroke, StrokeThickness = strokeTh,
            IsHitTestVisible = false
        };
        SetLeft(e, -r);
        SetTop(e, -r);
        return e;
    }

    private IEnumerable<(MainWindow.PopItem Pop, Point P, Point Offset)> SpreadOverlaps(
        List<(MainWindow.PopItem Pop, Point P)> items)
    {
        double threshold = Math.Max(6.0, ActualWidth * 0.009);
        double t2 = threshold * threshold;

        var clusters  = new List<List<int>>();
        var centroids = new List<Point>();

        for (int i = 0; i < items.Count; i++)
        {
            int found = -1;
            for (int c = 0; c < centroids.Count; c++)
            {
                double dx = items[i].P.X - centroids[c].X;
                double dy = items[i].P.Y - centroids[c].Y;
                if (dx * dx + dy * dy <= t2) { found = c; break; }
            }

            if (found < 0)
            {
                clusters.Add(new List<int> { i });
                centroids.Add(items[i].P);
            }
            else
            {
                clusters[found].Add(i);
                double sx = 0, sy = 0;
                foreach (int k in clusters[found]) { sx += items[k].P.X; sy += items[k].P.Y; }
                centroids[found] = new Point(sx / clusters[found].Count, sy / clusters[found].Count);
            }
        }

        for (int c = 0; c < clusters.Count; c++)
        {
            List<int> cl = clusters[c];

            if (cl.Count == 1)
            {
                yield return (items[cl[0]].Pop, items[cl[0]].P, new Point(0, 0));
                continue;
            }

            cl.Sort((a, b) => string.Compare(items[a].Pop.Desc, items[b].Pop.Desc, StringComparison.OrdinalIgnoreCase));

            double radius = Math.Max(13.0, 11.5 / Math.Sin(Math.PI / cl.Count));

            for (int n = 0; n < cl.Count; n++)
            {
                double angle = -Math.PI / 2 + n * (2 * Math.PI / cl.Count);
                var off = new Point(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
                yield return (items[cl[n]].Pop, centroids[c], off);
            }
        }
    }

    private void AddPopMarker(MainWindow.PopItem pop, Point p, Point offset)
    {
        Canvas root = NewMarkerRoot(p);

        bool spread = offset.X != 0 || offset.Y != 0;

        if (spread)
        {
            root.Children.Add(new Line
            {
                X1 = 0, Y1 = 0, X2 = offset.X, Y2 = offset.Y,
                Stroke = _accent, StrokeThickness = 1, Opacity = 0.45,
                IsHitTestVisible = false
            });
        }

        var inner = new Canvas();
        SetLeft(inner, offset.X);
        SetTop(inner, offset.Y);
        root.Children.Add(inner);

        bool pending = pop.IsPending;
        bool allowed = pop.IsAllowed;

        var hover = CenteredCircle(9, null, _accent, 1.4);
        hover.Opacity = 0;
        inner.Children.Add(hover);

        if (allowed && !pending)
        {

            Brush fill = PingColor(pop);

            var halo = CenteredCircle(5.5, null, _good, 1.5);
            halo.Opacity = 0.6;
            var hst = new ScaleTransform();
            halo.RenderTransform = hst;
            halo.RenderTransformOrigin = new Point(0.5, 0.5);
            inner.Children.Add(halo);

            hst.BeginAnimation(ScaleTransform.ScaleXProperty,
                new DoubleAnimation(1, 2.6, TimeSpan.FromSeconds(1.8)) { RepeatBehavior = RepeatBehavior.Forever });
            hst.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1, 2.6, TimeSpan.FromSeconds(1.8)) { RepeatBehavior = RepeatBehavior.Forever });
            halo.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0.6, 0, TimeSpan.FromSeconds(1.8)) { RepeatBehavior = RepeatBehavior.Forever });

            inner.Children.Add(CenteredCircle(5.5, fill, _accent, 1.5));
        }
        else if (!allowed && !pending)
        {

            AddCross(inner, _blockedBrush, 5, 1.8);
        }
        else
        {

            var ring = CenteredCircle(7, null, _pendingBrush, 1.6);
            ring.StrokeDashArray = new DoubleCollection { 2, 2 };
            inner.Children.Add(ring);

            ring.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1.0, 0.45, TimeSpan.FromSeconds(0.9))
                { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true });

            if (allowed)
                inner.Children.Add(CenteredCircle(4, _pendingBrush, null, 0));
            else
                AddCross(inner, _pendingBrush, 4, 1.6);
        }

        var hit = new Ellipse
        {
            Width = 22, Height = 22,
            Fill = Brushes.Transparent,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = pop
        };
        SetLeft(hit, -11);
        SetTop(hit, -11);
        inner.Children.Add(hit);

        hit.MouseLeftButtonDown += (s, e) =>
        {
            if (((FrameworkElement)s).Tag is MainWindow.PopItem clicked)
            {
                PulseMarker(root);
                PopClicked?.Invoke(clicked);
            }
            e.Handled = true;
        };
        hit.MouseEnter += (s, e) => { hover.Opacity = 0.9; ShowInfo(pop, e.GetPosition(this)); };
        hit.MouseMove  += (s, e) => MoveInfo(e.GetPosition(this));
        hit.MouseLeave += (s, _) => { hover.Opacity = 0; HideInfo(); };
    }

    private static void AddCross(Canvas root, Brush brush, double x, double th)
    {
        root.Children.Add(new Line
        {
            X1 = -x, Y1 = -x, X2 = x, Y2 = x,
            Stroke = brush, StrokeThickness = th,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        });
        root.Children.Add(new Line
        {
            X1 = -x, Y1 = x, X2 = x, Y2 = -x,
            Stroke = brush, StrokeThickness = th,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            IsHitTestVisible = false
        });
    }

    private void PulseMarker(Canvas root)
    {
        if (root.RenderTransform is not ScaleTransform st) return;
        double inv = 1.0 / _zoom;
        st.ScaleX = inv;
        st.ScaleY = inv;
        var anim = new DoubleAnimation(inv * 1.35, inv, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase(),
            FillBehavior = FillBehavior.Stop
        };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
    }

    private void AddHomePin(Point p)
    {
        Canvas root = NewMarkerRoot(p);

        var ring = CenteredCircle(5, null, _pin, 2);
        ring.Opacity = 0.7;
        root.Children.Add(ring);
        var st = new ScaleTransform();
        ring.RenderTransform = st;
        ring.RenderTransformOrigin = new Point(0.5, 0.5);
        ring.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.7, 0, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever });
        st.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(1, 5, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever });
        st.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(1, 5, TimeSpan.FromSeconds(2.2)) { RepeatBehavior = RepeatBehavior.Forever });

        var diamond = new Polygon
        {
            Points = new PointCollection { new(0, -8), new(6, 0), new(0, 8), new(-6, 0) },
            Fill = _pin, Stroke = Brushes.White, StrokeThickness = 1.5,
            IsHitTestVisible = false
        };
        root.Children.Add(diamond);

        var label = new TextBlock
        {
            Text = Loc.T("you"), Foreground = _pin, FontSize = 10, FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily(Loc.IsRtl ? "Segoe UI" : "Consolas"),
            IsHitTestVisible = false
        };
        SetLeft(label, 8);
        SetTop(label, -6);
        root.Children.Add(label);
    }

    private Point _lastMouse;

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
            _topLayer.Children.Add(_infoBox);
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
        _lastMouse = mouse;
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

    private void MoveInfoToLast() => MoveInfo(_lastMouse);

    private void HideInfo()
    {
        if (_infoBox != null) _infoBox.Visibility = Visibility.Collapsed;
    }

    private Brush PingColor(MainWindow.PopItem pop)
    {
        return pop.PingMs is int ms
            ? (ms < 70 ? _good : ms < 140 ? _medium : _bad)
            : _accent;
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

    private static Line AddLine(Canvas layer, Point a, Point b, Brush stroke, double thickness)
    {
        var l = new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = stroke, StrokeThickness = thickness, IsHitTestVisible = false };
        layer.Children.Add(l);
        return l;
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
