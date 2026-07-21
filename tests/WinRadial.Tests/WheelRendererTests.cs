using FluentAssertions;
using WinRadial.UI;
using Xunit;

namespace WinRadial.Tests;

/// <summary>
/// Unit tests for WheelRenderer — pure angle/geometry math.
/// Tests all 8 cardinal/ordinal directions, boundary angles, center hub detection,
/// and ring zone detection.
/// </summary>
public class WheelRendererTests
{
    // ─── Slice Index Detection ─────────────────────────

    [Theory]
    [InlineData(0, -100, 0)]   // North → slice 0
    [InlineData(100, 0, 2)]    // East  → slice 2
    [InlineData(0, 100, 4)]    // South → slice 4
    [InlineData(-100, 0, 6)]   // West  → slice 6
    public void GetSliceIndex_CardinalDirections_ReturnsCorrectSlice(double dx, double dy, int expected)
    {
        WheelRenderer.GetSliceIndex(dx, dy).Should().Be(expected);
    }

    [Theory]
    [InlineData(100, -100, 1)]   // Northeast → slice 1
    [InlineData(100, 100, 3)]    // Southeast → slice 3
    [InlineData(-100, 100, 5)]   // Southwest → slice 5
    [InlineData(-100, -100, 7)]  // Northwest → slice 7
    public void GetSliceIndex_OrdinalDirections_ReturnsCorrectSlice(double dx, double dy, int expected)
    {
        WheelRenderer.GetSliceIndex(dx, dy).Should().Be(expected);
    }

    [Fact]
    public void GetSliceIndex_ExactCenter_ReturnsNegativeOne()
    {
        WheelRenderer.GetSliceIndex(0, 0).Should().Be(-1);
    }

    [Fact]
    public void GetSliceIndex_AllSlicesCovered()
    {
        // Verify all 8 slices are reachable by testing angles around the full circle
        var hitSlices = new HashSet<int>();
        for (int degrees = 0; degrees < 360; degrees += 5)
        {
            var radians = degrees * Math.PI / 180.0;
            var dx = 100 * Math.Sin(radians);
            var dy = -100 * Math.Cos(radians);
            hitSlices.Add(WheelRenderer.GetSliceIndex(dx, dy));
        }
        hitSlices.Should().BeEquivalentTo([0, 1, 2, 3, 4, 5, 6, 7]);
    }

    [Fact]
    public void GetSliceIndex_VerySmallOffset_StillWorks()
    {
        // Extremely small but non-zero offset should still return a valid slice
        var result = WheelRenderer.GetSliceIndex(0.001, -0.001);
        result.Should().BeInRange(0, 7);
    }

    [Theory]
    [InlineData(0.001, -100)]   // Almost exactly north
    [InlineData(-0.001, -100)]  // Almost exactly north (other side)
    public void GetSliceIndex_NearBoundary_HandlesWraparound(double dx, double dy)
    {
        // Both should be in slice 0 (north) or adjacent — the point is no crash
        var result = WheelRenderer.GetSliceIndex(dx, dy);
        result.Should().BeInRange(0, 7);
    }

    // ─── Center Hub Detection ──────────────────────────

    [Theory]
    [InlineData(0, 0, 60, true)]       // Exact center
    [InlineData(30, 30, 60, true)]     // Inside hub
    [InlineData(60, 0, 60, true)]      // On boundary (<=)
    [InlineData(61, 0, 60, false)]     // Just outside
    [InlineData(100, 100, 60, false)]  // Way outside
    public void IsInCenterHub_ReturnsCorrectResult(double dx, double dy, double radius, bool expected)
    {
        WheelRenderer.IsInCenterHub(dx, dy, radius).Should().Be(expected);
    }

    // ─── Main Ring Detection ───────────────────────────

    [Theory]
    [InlineData(100, 0, 60, 200, true)]    // In main ring
    [InlineData(30, 0, 60, 200, false)]    // In hub (inside inner)
    [InlineData(250, 0, 60, 200, false)]   // Outside outer
    [InlineData(60.1, 0, 60, 200, true)]   // Just past inner boundary
    [InlineData(200, 0, 60, 200, true)]    // On outer boundary (<=)
    public void IsInMainRing_ReturnsCorrectResult(double dx, double dy, double innerR, double outerR, bool expected)
    {
        WheelRenderer.IsInMainRing(dx, dy, innerR, outerR).Should().Be(expected);
    }

    // ─── Sub Ring Detection ────────────────────────────

    [Theory]
    [InlineData(220, 0, 200, 280, true)]   // In sub ring
    [InlineData(150, 0, 200, 280, false)]  // Inside main ring
    [InlineData(300, 0, 200, 280, false)]  // Outside sub ring
    public void IsInSubRing_ReturnsCorrectResult(double dx, double dy, double outerR, double subR, bool expected)
    {
        WheelRenderer.IsInSubRing(dx, dy, outerR, subR).Should().Be(expected);
    }

    // ─── Slice Geometry ────────────────────────────────

    [Fact]
    public void GetSliceGeometry_AllSlices_HaveCorrectAngles()
    {
        for (int i = 0; i < 8; i++)
        {
            var geom = WheelRenderer.GetSliceGeometry(i, 60, 200);
            var angleSpan = geom.EndAngle - geom.StartAngle;
            angleSpan.Should().BeApproximately(45.0, 0.001, $"Slice {i} should span 45 degrees");
        }
    }

    [Fact]
    public void GetSliceGeometry_IsLargeArc_AlwaysFalseForEightSlices()
    {
        // 45° < 180° so IsLargeArc should always be false for 8-slice layout
        for (int i = 0; i < 8; i++)
        {
            var geom = WheelRenderer.GetSliceGeometry(i, 60, 200);
            geom.IsLargeArc.Should().BeFalse($"Slice {i} with 45° should not be a large arc");
        }
    }

    // ─── Slice Center Positioning ──────────────────────

    [Fact]
    public void GetSliceCenter_Slice0_IsAboveCenter()
    {
        var (x, y) = WheelRenderer.GetSliceCenter(0, 60, 200);
        // Slice 0 (north) should have y < 0 (above center in screen coords)
        y.Should().BeLessThan(0, "Slice 0 (north) center should be above wheel center");
        Math.Abs(x).Should().BeLessThan(1, "Slice 0 (north) center should be near x=0");
    }

    [Fact]
    public void GetSliceCenter_Slice4_IsBelowCenter()
    {
        var (x, y) = WheelRenderer.GetSliceCenter(4, 60, 200);
        // Slice 4 (south) should have y > 0 (below center)
        y.Should().BeGreaterThan(0, "Slice 4 (south) center should be below wheel center");
        Math.Abs(x).Should().BeLessThan(1, "Slice 4 (south) center should be near x=0");
    }

    [Fact]
    public void GetSliceCenter_AllSlices_AreAtCorrectRadius()
    {
        double innerR = 60, outerR = 200;
        var expectedR = (innerR + outerR) / 2.0;

        for (int i = 0; i < 8; i++)
        {
            var (x, y) = WheelRenderer.GetSliceCenter(i, innerR, outerR);
            var actualR = Math.Sqrt(x * x + y * y);
            actualR.Should().BeApproximately(expectedR, 0.001, $"Slice {i} center should be at mid-radius");
        }
    }

    // ─── AngleToPoint ──────────────────────────────────

    [Theory]
    [InlineData(0, 100, 0, -100)]      // North: x=0, y=-100
    [InlineData(90, 100, 100, 0)]      // East:  x=100, y≈0
    [InlineData(180, 100, 0, 100)]     // South: x≈0, y=100
    [InlineData(270, 100, -100, 0)]    // West:  x=-100, y≈0
    public void AngleToPoint_CardinalAngles_ReturnsCorrectCoordinates(
        double angle, double radius, double expectedX, double expectedY)
    {
        var (x, y) = WheelRenderer.AngleToPoint(angle, radius);
        x.Should().BeApproximately(expectedX, 0.01);
        y.Should().BeApproximately(expectedY, 0.01);
    }
}
