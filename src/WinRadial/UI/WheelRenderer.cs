namespace WinRadial.UI;

/// <summary>
/// Pure geometry/math for the radial wheel. NO WPF dependencies — fully unit-testable.
/// All angles are in degrees, measured clockwise from 12 o'clock (north = 0°).
/// </summary>
public static class WheelRenderer
{
    public const int SliceCount = 8;
    public const double SliceAngle = 360.0 / SliceCount; // 45°

    /// <summary>
    /// Determines which slice (0–7) a point falls in, given its offset from the wheel center.
    /// Slice 0 is the top (north) slice, increasing clockwise.
    /// </summary>
    /// <param name="dx">Horizontal offset from center (positive = right)</param>
    /// <param name="dy">Vertical offset from center (positive = down, screen coords)</param>
    /// <returns>Slice index 0–7, or -1 if at exact center</returns>
    public static int GetSliceIndex(double dx, double dy)
    {
        if (dx == 0 && dy == 0) return -1;

        // atan2 gives angle from positive X axis, counter-clockwise
        // We need angle from north (top), clockwise
        var radians = Math.Atan2(dx, -dy); // Note: (dx, -dy) maps screen coords to "north = 0"
        var degrees = radians * (180.0 / Math.PI);

        // Normalize to [0, 360)
        if (degrees < 0) degrees += 360.0;

        // Offset by half a slice so slice boundaries are at multiples of 45°
        // centered around 0° (i.e., slice 0 spans -22.5° to 22.5° from north)
        degrees += SliceAngle / 2.0;
        if (degrees >= 360.0) degrees -= 360.0;

        var index = (int)(degrees / SliceAngle);
        return Math.Clamp(index, 0, SliceCount - 1);
    }

    /// <summary>
    /// Gets the start and end angles (in degrees, clockwise from north) for a slice.
    /// </summary>
    public static (double StartAngle, double EndAngle) GetSliceAngles(int sliceIndex)
    {
        var startAngle = sliceIndex * SliceAngle - SliceAngle / 2.0;
        var endAngle = startAngle + SliceAngle;
        return (startAngle, endAngle);
    }

    /// <summary>
    /// Gets the start and end angles with a gap applied (inset on each side).
    /// </summary>
    public static (double StartAngle, double EndAngle) GetSliceAnglesWithGap(int sliceIndex, double gapDegrees)
    {
        var (start, end) = GetSliceAngles(sliceIndex);
        var halfGap = gapDegrees / 2.0;
        return (start + halfGap, end - halfGap);
    }

    /// <summary>
    /// Returns the center point of a slice arc (for label/icon positioning).
    /// Coordinates are offsets from the wheel center.
    /// </summary>
    public static (double X, double Y) GetSliceCenter(int sliceIndex, double innerRadius, double outerRadius)
    {
        var midAngle = sliceIndex * SliceAngle; // Center of slice in our north-clockwise system
        var midRadius = (innerRadius + outerRadius) / 2.0;
        return AngleToPoint(midAngle, midRadius);
    }

    /// <summary>
    /// Returns a point on the outer edge of a slice (for number badge positioning).
    /// </summary>
    public static (double X, double Y) GetSliceOuterEdgeCenter(int sliceIndex, double outerRadius, double inset = 14)
    {
        var midAngle = sliceIndex * SliceAngle;
        return AngleToPoint(midAngle, outerRadius - inset);
    }

    /// <summary>
    /// Converts a "clockwise from north" angle and radius to (X, Y) offset from center.
    /// </summary>
    public static (double X, double Y) AngleToPoint(double angleDegrees, double radius)
    {
        var radians = angleDegrees * (Math.PI / 180.0);
        // North-clockwise: x = sin(angle), y = -cos(angle) [screen coords: y-down]
        var x = radius * Math.Sin(radians);
        var y = -radius * Math.Cos(radians);
        return (x, y);
    }

    /// <summary>
    /// Checks if a point (offset from center) is inside the center hub.
    /// </summary>
    public static bool IsInCenterHub(double dx, double dy, double hubRadius)
    {
        return (dx * dx + dy * dy) <= (hubRadius * hubRadius);
    }

    /// <summary>
    /// Checks if a point (offset from center) is inside the main wheel ring.
    /// </summary>
    public static bool IsInMainRing(double dx, double dy, double innerRadius, double outerRadius)
    {
        var distSq = dx * dx + dy * dy;
        return distSq > (innerRadius * innerRadius) && distSq <= (outerRadius * outerRadius);
    }

    /// <summary>
    /// Checks if a point (offset from center) is inside the submenu outer ring.
    /// </summary>
    public static bool IsInSubRing(double dx, double dy, double outerRadius, double subRadius)
    {
        var distSq = dx * dx + dy * dy;
        return distSq > (outerRadius * outerRadius) && distSq <= (subRadius * subRadius);
    }

    /// <summary>
    /// Gets the 4 corner points of a slice (inner-start, outer-start, outer-end, inner-end)
    /// for constructing the arc path geometry. No gap applied.
    /// </summary>
    public static SliceGeometry GetSliceGeometry(int sliceIndex, double innerRadius, double outerRadius)
    {
        return GetSliceGeometry(sliceIndex, innerRadius, outerRadius, 0);
    }

    /// <summary>
    /// Gets the 4 corner points of a slice with an angular gap applied.
    /// </summary>
    public static SliceGeometry GetSliceGeometry(int sliceIndex, double innerRadius, double outerRadius, double gapDegrees)
    {
        var (startAngle, endAngle) = gapDegrees > 0
            ? GetSliceAnglesWithGap(sliceIndex, gapDegrees)
            : GetSliceAngles(sliceIndex);

        var innerStart = AngleToPoint(startAngle, innerRadius);
        var outerStart = AngleToPoint(startAngle, outerRadius);
        var innerEnd = AngleToPoint(endAngle, innerRadius);
        var outerEnd = AngleToPoint(endAngle, outerRadius);

        var sweepAngle = endAngle - startAngle;

        return new SliceGeometry
        {
            InnerStart = innerStart,
            OuterStart = outerStart,
            InnerEnd = innerEnd,
            OuterEnd = outerEnd,
            InnerRadius = innerRadius,
            OuterRadius = outerRadius,
            StartAngle = startAngle,
            EndAngle = endAngle,
            IsLargeArc = sweepAngle > 180.0
        };
    }
}

/// <summary>
/// Geometric data for rendering a single wheel slice.
/// </summary>
public struct SliceGeometry
{
    public (double X, double Y) InnerStart;
    public (double X, double Y) OuterStart;
    public (double X, double Y) InnerEnd;
    public (double X, double Y) OuterEnd;
    public double InnerRadius;
    public double OuterRadius;
    public double StartAngle;
    public double EndAngle;
    public bool IsLargeArc;
}
