using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WinRadial.Actions;
using WinRadial.Core;

namespace WinRadial.UI;

/// <summary>
/// Custom WPF FrameworkElement that renders the radial pie menu with a premium,
/// glassmorphic dark look. Uses WheelRenderer for all geometry calculations.
/// Disposes per-render brushes/pens/geometries to avoid handle leaks.
/// </summary>
public sealed class WheelCanvas : FrameworkElement
{
    private readonly AppearanceConfig _appearance;
    private List<IWheelAction> _actions = [];
    private List<IWheelAction> _subActions = [];
    private int _hoveredSlice = -1;
    private int _hoveredSubSlice = -1;
    private bool _submenuOpen;
    private int _submenuParentSlice = -1;
    private string _categoryName = "";
    private int _currentPage;
    private int _totalPages;
    private int _hoveredHubArea = -1; // -1=none, 0=left arrow, 1=right arrow

    // Center of the wheel in local coordinates
    public double CenterX => ActualWidth / 2.0;
    public double CenterY => ActualHeight / 2.0;

    public int HoveredSlice => _hoveredSlice;
    public int HoveredSubSlice => _hoveredSubSlice;
    public bool IsSubmenuOpen => _submenuOpen;
    public int SubmenuParentSlice => _submenuParentSlice;
    public int HoveredHubArea => _hoveredHubArea;

    public WheelCanvas(AppearanceConfig appearance)
    {
        _appearance = appearance;
        IsHitTestVisible = false; // Parent window handles input
    }

    public void UpdateState(
        List<IWheelAction> actions,
        int hoveredSlice,
        int hoveredSubSlice,
        bool submenuOpen,
        int submenuParentSlice,
        List<IWheelAction> subActions,
        string categoryName,
        int currentPage,
        int totalPages,
        int hoveredHubArea)
    {
        _actions = actions;
        _hoveredSlice = hoveredSlice;
        _hoveredSubSlice = hoveredSubSlice;
        _submenuOpen = submenuOpen;
        _submenuParentSlice = submenuParentSlice;
        _subActions = subActions;
        _categoryName = categoryName;
        _currentPage = currentPage;
        _totalPages = totalPages;
        _hoveredHubArea = hoveredHubArea;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var cx = CenterX;
        var cy = CenterY;
        var innerR = _appearance.InnerRadius;
        var outerR = _appearance.OuterRadius;
        var subR = _appearance.SubMenuRadius;
        var gap = _appearance.SliceGapDegrees;

        // Parse colors
        var bgColor = ParseColor(_appearance.BackgroundColor);
        var bgEndColor = ParseColor(_appearance.BackgroundColorEnd);
        var hoverColor = ParseColor(_appearance.HoverColor);
        var hoverEndColor = ParseColor(_appearance.HoverColorEnd);
        var accentColor = ParseColor(_appearance.AccentColor);
        var glowColor = ParseColor(_appearance.GlowColor);
        var textColor = ParseColor(_appearance.TextColor);
        var subTextColor = ParseColor(_appearance.SubTextColor);
        var hoveredTextColor = ParseColor(_appearance.HoveredTextColor);
        var hubColor = ParseColor(_appearance.HubColor);
        var hubBorderColor = ParseColor(_appearance.HubBorderColor);
        var separatorColor = ParseColor(_appearance.SeparatorColor);
        var outerRingColor = ParseColor(_appearance.OuterRingColor);

        // ── Outer glow ring (drawn first, behind everything) ──
        DrawGlowRing(dc, cx, cy, outerR, outerRingColor, glowColor);

        // ── Draw 8 main wedges ──
        for (int i = 0; i < WheelRenderer.SliceCount; i++)
        {
            var geom = WheelRenderer.GetSliceGeometry(i, innerR, outerR, gap);
            var path = BuildSlicePath(geom, cx, cy);

            var isHovered = (i == _hoveredSlice && !_submenuOpen) ||
                           (i == _submenuParentSlice && _submenuOpen);

            // Gradient fill
            Brush fillBrush;
            if (isHovered)
            {
                fillBrush = CreateRadialGradient(cx, cy, outerR, hoverColor, hoverEndColor);
            }
            else
            {
                fillBrush = CreateRadialGradient(cx, cy, outerR, bgColor, bgEndColor);
            }

            var borderPen = new Pen(new SolidColorBrush(separatorColor), 0.5);
            borderPen.Freeze();

            dc.DrawGeometry(fillBrush, borderPen, path);

            // Draw icon + label
            if (i < _actions.Count)
            {
                var txtColor = isHovered ? hoveredTextColor : textColor;
                var subTxtColor = isHovered ? hoveredTextColor : subTextColor;
                var iconColor = isHovered ? hoveredTextColor : textColor;

                DrawSliceContent(dc, i, _actions[i], innerR, outerR, cx, cy,
                    txtColor, subTxtColor, iconColor, isHovered, accentColor);
            }

            // Number badge
            if (_appearance.ShowSliceNumbers)
            {
                DrawSliceNumber(dc, i, outerR, cx, cy,
                    isHovered ? hoveredTextColor : Color.FromArgb(80, 255, 255, 255));
            }
        }

        // ── Draw submenu ring ──
        if (_submenuOpen && _subActions.Count > 0)
        {
            // Submenu glow ring
            DrawGlowRing(dc, cx, cy, subR, outerRingColor, glowColor);

            for (int i = 0; i < WheelRenderer.SliceCount; i++)
            {
                var geom = WheelRenderer.GetSliceGeometry(i, outerR + 4, subR, gap);
                var path = BuildSlicePath(geom, cx, cy);

                var isHovered = i == _hoveredSubSlice;

                Brush fillBrush;
                if (isHovered)
                {
                    fillBrush = CreateRadialGradient(cx, cy, subR, hoverColor, hoverEndColor);
                }
                else
                {
                    fillBrush = CreateRadialGradient(cx, cy, subR, bgColor, bgEndColor);
                }

                var borderPen = new Pen(new SolidColorBrush(separatorColor), 0.5);
                borderPen.Freeze();

                dc.DrawGeometry(fillBrush, borderPen, path);

                if (i < _subActions.Count)
                {
                    var txtColor = isHovered ? hoveredTextColor : textColor;
                    var subTxtColor = isHovered ? hoveredTextColor : subTextColor;
                    var iconColor = isHovered ? hoveredTextColor : textColor;

                    DrawSliceContent(dc, i, _subActions[i], outerR + 4, subR, cx, cy,
                        txtColor, subTxtColor, iconColor, isHovered, accentColor);
                }
            }
        }

        // ── Draw center hub ──
        DrawCenterHub(dc, cx, cy, innerR, hubColor, hubBorderColor, accentColor, glowColor, textColor, subTextColor);
    }

    // ─── Drawing helpers ─────────────────────────────────

    /// <summary>
    /// Draws a subtle glowing ring at the given radius.
    /// </summary>
    private static void DrawGlowRing(DrawingContext dc, double cx, double cy, double radius,
        Color ringColor, Color glowColor)
    {
        // Outer glow (wide, faint)
        var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(30, glowColor.R, glowColor.G, glowColor.B)), 4.0);
        glowPen.Freeze();
        dc.DrawEllipse(null, glowPen, new Point(cx, cy), radius + 2, radius + 2);

        // Crisp ring
        var ringPen = new Pen(new SolidColorBrush(ringColor), 1.0);
        ringPen.Freeze();
        dc.DrawEllipse(null, ringPen, new Point(cx, cy), radius, radius);
    }

    /// <summary>
    /// Creates a radial gradient brush centered on the wheel.
    /// </summary>
    private static RadialGradientBrush CreateRadialGradient(double cx, double cy, double radius,
        Color centerColor, Color edgeColor)
    {
        var brush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.5),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.5,
            RadiusY = 0.5,
        };
        brush.GradientStops.Add(new GradientStop(centerColor, 0.0));
        brush.GradientStops.Add(new GradientStop(edgeColor, 1.0));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Draws the number badge (1–8) near the outer edge of a slice.
    /// </summary>
    private void DrawSliceNumber(DrawingContext dc, int index, double outerR,
        double cx, double cy, Color color)
    {
        var (bx, by) = WheelRenderer.GetSliceOuterEdgeCenter(index, outerR, 16);
        var numText = CreateFormattedText(
            (index + 1).ToString(), color, 9,
            "Segoe UI Variable, Segoe UI", FontWeights.Medium);
        dc.DrawText(numText, new Point(
            cx + bx - numText.Width / 2,
            cy + by - numText.Height / 2));
    }

    private void DrawSliceContent(DrawingContext dc, int index, IWheelAction action,
        double innerR, double outerR, double cx, double cy,
        Color textColor, Color subTextColor, Color iconColor,
        bool isHovered, Color accentColor)
    {
        var (sx, sy) = WheelRenderer.GetSliceCenter(index, innerR, outerR);

        // Try getting a real image icon first
        var imageSource = IconProvider.GetIconImageSource(action, InvalidateVisual);

        if (imageSource != null)
        {
            var imgSize = 28.0;
            var rect = new Rect(cx + sx - imgSize / 2, cy + sy - imgSize / 2 - 10, imgSize, imgSize);
            
            if (isHovered)
            {
                // Simple glow behind the image
                var glowPen = new Pen(new SolidColorBrush(Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B)), 4.0);
                dc.DrawEllipse(null, glowPen, new Point(cx + sx, cy + sy - 10), imgSize / 2, imgSize / 2);
            }

            dc.DrawImage(imageSource, rect);
        }
        else
        {
            // Fallback to Segoe MDL2 Assets glyph
            var glyph = IconProvider.ResolveGlyph(action.IconKey);

            if (isHovered)
            {
                var glowIcon = CreateFormattedText(glyph,
                    Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B),
                    26, "Segoe MDL2 Assets");
                dc.DrawText(glowIcon, new Point(
                    cx + sx - glowIcon.Width / 2,
                    cy + sy - glowIcon.Height / 2 - 10));
            }

            var iconText = CreateFormattedText(glyph, iconColor, 24, "Segoe MDL2 Assets");
            dc.DrawText(iconText, new Point(
                cx + sx - iconText.Width / 2,
                cy + sy - iconText.Height / 2 - 10));
        }

        // Label
        var label = action.Label;
        if (label.Length > 12) label = label[..11] + "…";
        var labelText = CreateFormattedText(label, subTextColor, 11.5,
            "Segoe UI Variable Display, Segoe UI Variable, Segoe UI", FontWeights.Normal);
        dc.DrawText(labelText, new Point(
            cx + sx - labelText.Width / 2,
            cy + sy + 12));

        // Submenu indicator (three dots)
        if (action.HasSubmenu)
        {
            var indicator = CreateFormattedText("···", iconColor, 10, "Segoe UI Variable, Segoe UI", FontWeights.Bold);
            var (indX, indY) = WheelRenderer.GetSliceCenter(index, outerR - 20, outerR - 6);
            dc.DrawText(indicator, new Point(
                cx + indX - indicator.Width / 2,
                cy + indY - indicator.Height / 2));
        }
    }

    private void DrawCenterHub(DrawingContext dc, double cx, double cy, double innerR,
        Color hubColor, Color hubBorderColor, Color accentColor, Color glowColor,
        Color textColor, Color subTextColor)
    {
        // Hub glow ring (behind)
        var hubGlowPen = new Pen(new SolidColorBrush(
            Color.FromArgb(40, glowColor.R, glowColor.G, glowColor.B)), 5.0);
        hubGlowPen.Freeze();
        dc.DrawEllipse(null, hubGlowPen, new Point(cx, cy), innerR + 2, innerR + 2);

        // Hub fill — radial gradient for depth
        var hubBrush = new RadialGradientBrush();
        hubBrush.GradientStops.Add(new GradientStop(
            Color.FromArgb(hubColor.A, (byte)Math.Min(255, hubColor.R + 15),
                (byte)Math.Min(255, hubColor.G + 15), (byte)Math.Min(255, hubColor.B + 20)), 0.0));
        hubBrush.GradientStops.Add(new GradientStop(hubColor, 1.0));
        hubBrush.Freeze();

        // Hub border
        var hubBorderPen = new Pen(new SolidColorBrush(hubBorderColor), 1.5);
        hubBorderPen.Freeze();

        dc.DrawEllipse(hubBrush, hubBorderPen, new Point(cx, cy), innerR, innerR);

        // Category name
        var catText = CreateFormattedText(_categoryName, textColor, 14,
            "Segoe UI Variable Display, Segoe UI Variable, Segoe UI", FontWeights.SemiBold);
        dc.DrawText(catText, new Point(cx - catText.Width / 2, cy - 18));

        // Page indicator
        if (_totalPages > 1)
        {
            // Styled pagination: ‹ 3/4 ›
            var pageStr = $"‹  {_currentPage + 1} / {_totalPages}  ›";
            var pageText = CreateFormattedText(pageStr, subTextColor, 11,
                "Segoe UI Variable, Segoe UI", FontWeights.Normal);
            dc.DrawText(pageText, new Point(cx - pageText.Width / 2, cy + 4));

            // Interactive left arrow
            var leftColor = _hoveredHubArea == 0 ? accentColor : subTextColor;
            var leftArrow = CreateFormattedText("‹", leftColor, 18,
                "Segoe UI Variable, Segoe UI", FontWeights.Bold);
            dc.DrawText(leftArrow, new Point(cx - innerR + 10, cy + 14));

            // Interactive right arrow
            var rightColor = _hoveredHubArea == 1 ? accentColor : subTextColor;
            var rightArrow = CreateFormattedText("›", rightColor, 18,
                "Segoe UI Variable, Segoe UI", FontWeights.Bold);
            dc.DrawText(rightArrow, new Point(cx + innerR - 20, cy + 14));
        }
    }

    // ─── Geometry & text helpers ─────────────────────────

    /// <summary>
    /// Builds a WPF PathGeometry for a wheel slice (annular sector).
    /// </summary>
    private static PathGeometry BuildSlicePath(SliceGeometry geom, double cx, double cy)
    {
        var fig = new PathFigure
        {
            StartPoint = new Point(cx + geom.InnerStart.X, cy + geom.InnerStart.Y),
            IsClosed = true,
            IsFilled = true,
        };

        // Line from inner-start to outer-start
        fig.Segments.Add(new LineSegment(
            new Point(cx + geom.OuterStart.X, cy + geom.OuterStart.Y), true));

        // Outer arc from outer-start to outer-end
        fig.Segments.Add(new ArcSegment(
            new Point(cx + geom.OuterEnd.X, cy + geom.OuterEnd.Y),
            new Size(geom.OuterRadius, geom.OuterRadius),
            0, geom.IsLargeArc, SweepDirection.Clockwise, true));

        // Line from outer-end to inner-end
        fig.Segments.Add(new LineSegment(
            new Point(cx + geom.InnerEnd.X, cy + geom.InnerEnd.Y), true));

        // Inner arc from inner-end back to inner-start (counter-clockwise)
        fig.Segments.Add(new ArcSegment(
            new Point(cx + geom.InnerStart.X, cy + geom.InnerStart.Y),
            new Size(geom.InnerRadius, geom.InnerRadius),
            0, geom.IsLargeArc, SweepDirection.Counterclockwise, true));

        var pathGeom = new PathGeometry();
        pathGeom.Figures.Add(fig);
        return pathGeom;
    }

    private static FormattedText CreateFormattedText(string text, Color color, double size,
        string fontFamily, FontWeight? weight = null)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily(fontFamily), FontStyles.Normal,
                weight ?? FontWeights.Normal, FontStretches.Normal),
            size,
            new SolidColorBrush(color),
            VisualTreeHelper.GetDpi(new DrawingVisual()).PixelsPerDip);
        return ft;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.White;
        }
    }
}
