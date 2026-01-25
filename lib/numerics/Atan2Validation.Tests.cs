using System;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validates the behavioral differences between .NET's Math.Atan2 and PineScript's custom atan2 implementation.
/// 
/// PineScript's atan2 uses a numerically stable algorithm:
/// 1. If |x| > |y|: angle = atan(|y|/|x|)
/// 2. If |y| >= |x|: angle = π/2 - atan(|x|/|y|)
/// 3. Then applies quadrant correction based on sign of x and y
/// 
/// Math.Atan2 follows the standard IEEE convention: atan2(y, x) returns the angle
/// in radians between the positive x-axis and the point (x, y).
/// 
/// Both return values in the range [-π, π], but they can differ in edge cases
/// and have different numerical stability characteristics.
/// </summary>
public class Atan2ValidationTests
{
    private readonly ITestOutputHelper _output;

    public Atan2ValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// PineScript-style atan2 implementation for comparison.
    /// This is a direct port of the algorithm from ht_phasor.pine.
    /// </summary>
    private static double PineScriptAtan2(double y, double x)
    {
        if (y == 0.0 && x == 0.0)
            throw new ArgumentException("atan2: Both y and x cannot be zero", nameof(y));

        double ay = Math.Abs(y);
        double ax = Math.Abs(x);
        double angle;

        if (ax > ay)
        {
            angle = Math.Atan(ay / ax);
        }
        else
        {
            angle = (Math.PI / 2.0) - Math.Atan(ax / ay);
        }

        if (x < 0.0)
            angle = Math.PI - angle;
        if (y < 0.0)
            angle = -angle;

        return angle;
    }

    [Fact]
    public void Atan2_StandardQuadrant1_BothMatch()
    {
        // First quadrant: x > 0, y > 0
        double y = 1.0, x = 1.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);

        _output.WriteLine($"Q1: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Q1: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Q1: Difference = {Math.Abs(dotNet - pine):E}");

        Assert.Equal(dotNet, pine, precision: 14);
    }

    [Fact]
    public void Atan2_StandardQuadrant2_BothMatch()
    {
        // Second quadrant: x < 0, y > 0
        double y = 1.0, x = -1.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);

        _output.WriteLine($"Q2: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Q2: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Q2: Difference = {Math.Abs(dotNet - pine):E}");

        Assert.Equal(dotNet, pine, precision: 14);
    }

    [Fact]
    public void Atan2_StandardQuadrant3_BothMatch()
    {
        // Third quadrant: x < 0, y < 0
        double y = -1.0, x = -1.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);

        _output.WriteLine($"Q3: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Q3: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Q3: Difference = {Math.Abs(dotNet - pine):E}");

        Assert.Equal(dotNet, pine, precision: 14);
    }

    [Fact]
    public void Atan2_StandardQuadrant4_BothMatch()
    {
        // Fourth quadrant: x > 0, y < 0
        double y = -1.0, x = 1.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);

        _output.WriteLine($"Q4: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Q4: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Q4: Difference = {Math.Abs(dotNet - pine):E}");

        Assert.Equal(dotNet, pine, precision: 14);
    }

    [Fact]
    public void Atan2_AxisAligned_PositiveY()
    {
        // On positive Y-axis: x = 0, y > 0 → π/2
        double y = 1.0, x = 0.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);
        double expected = Math.PI / 2.0;

        _output.WriteLine($"Positive Y-axis: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Positive Y-axis: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Expected: π/2 = {expected:F15}");

        Assert.Equal(expected, dotNet, precision: 14);
        Assert.Equal(expected, pine, precision: 14);
    }

    [Fact]
    public void Atan2_AxisAligned_NegativeY()
    {
        // On negative Y-axis: x = 0, y < 0 → -π/2
        double y = -1.0, x = 0.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);
        double expected = -Math.PI / 2.0;

        _output.WriteLine($"Negative Y-axis: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Negative Y-axis: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Expected: -π/2 = {expected:F15}");

        Assert.Equal(expected, dotNet, precision: 14);
        Assert.Equal(expected, pine, precision: 14);
    }

    [Fact]
    public void Atan2_AxisAligned_PositiveX()
    {
        // On positive X-axis: x > 0, y = 0 → 0
        double y = 0.0, x = 1.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);
        double expected = 0.0;

        _output.WriteLine($"Positive X-axis: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Positive X-axis: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Expected: 0 = {expected:F15}");

        Assert.Equal(expected, dotNet, precision: 14);
        Assert.Equal(expected, pine, precision: 14);
    }

    [Fact]
    public void Atan2_AxisAligned_NegativeX()
    {
        // On negative X-axis: x < 0, y = 0 → π
        double y = 0.0, x = -1.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);
        double expected = Math.PI;

        _output.WriteLine($"Negative X-axis: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Negative X-axis: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Expected: π = {expected:F15}");

        Assert.Equal(expected, dotNet, precision: 14);
        Assert.Equal(expected, pine, precision: 14);
    }

    [Fact]
    public void Atan2_Origin_DotNetReturnsZero_PineThrows()
    {
        // Origin: x = 0, y = 0
        // Math.Atan2 returns 0 (by convention)
        // PineScript throws an error
        double y = 0.0, x = 0.0;
        double dotNet = Math.Atan2(y, x);

        _output.WriteLine($"Origin: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine("Origin: PineScript throws ArgumentException");

        Assert.Equal(0.0, dotNet);
        Assert.Throws<ArgumentException>(() => PineScriptAtan2(y, x));
    }

    [Fact]
    public void Atan2_VerySmallValues_NumericalStability()
    {
        // Test numerical stability with very small values
        double y = 1e-300, x = 1e-300;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);
        double expected = Math.PI / 4.0; // 45 degrees

        _output.WriteLine($"Small values: Math.Atan2({y:E}, {x:E}) = {dotNet:F15}");
        _output.WriteLine($"Small values: PineAtan2({y:E}, {x:E}) = {pine:F15}");
        _output.WriteLine($"Expected: π/4 = {expected:F15}");
        _output.WriteLine($"Difference = {Math.Abs(dotNet - pine):E}");

        // Both should handle small values well
        Assert.Equal(expected, dotNet, precision: 10);
        Assert.Equal(expected, pine, precision: 10);
    }

    [Fact]
    public void Atan2_VeryLargeValues_NumericalStability()
    {
        // Test numerical stability with very large values
        double y = 1e300, x = 1e300;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);
        double expected = Math.PI / 4.0; // 45 degrees

        _output.WriteLine($"Large values: Math.Atan2({y:E}, {x:E}) = {dotNet:F15}");
        _output.WriteLine($"Large values: PineAtan2({y:E}, {x:E}) = {pine:F15}");
        _output.WriteLine($"Expected: π/4 = {expected:F15}");
        _output.WriteLine($"Difference = {Math.Abs(dotNet - pine):E}");

        Assert.Equal(expected, dotNet, precision: 10);
        Assert.Equal(expected, pine, precision: 10);
    }

    [Fact]
    public void Atan2_AspectRatioExtreme_TallVector()
    {
        // PineScript algorithm switches behavior when |y| > |x|
        // Test with y >> x
        double y = 1000.0, x = 1.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);

        _output.WriteLine($"Tall vector: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Tall vector: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Difference = {Math.Abs(dotNet - pine):E}");

        // Should be very close to π/2
        Assert.True(Math.Abs(dotNet - pine) < 1e-12, $"Difference {Math.Abs(dotNet - pine):E} exceeds tolerance");
    }

    [Fact]
    public void Atan2_AspectRatioExtreme_WideVector()
    {
        // Test with x >> y
        double y = 1.0, x = 1000.0;
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);

        _output.WriteLine($"Wide vector: Math.Atan2({y}, {x}) = {dotNet:F15}");
        _output.WriteLine($"Wide vector: PineAtan2({y}, {x}) = {pine:F15}");
        _output.WriteLine($"Difference = {Math.Abs(dotNet - pine):E}");

        // Should be very close to 0
        Assert.True(Math.Abs(dotNet - pine) < 1e-12, $"Difference {Math.Abs(dotNet - pine):E} exceeds tolerance");
    }

    [Theory]
    [InlineData(0.5, 0.866025403784439)] // 30 degrees
    [InlineData(0.707106781186548, 0.707106781186548)] // 45 degrees
    [InlineData(0.866025403784439, 0.5)] // 60 degrees
    public void Atan2_CommonAngles_Match(double y, double x)
    {
        double dotNet = Math.Atan2(y, x);
        double pine = PineScriptAtan2(y, x);

        _output.WriteLine($"Math.Atan2({y}, {x}) = {dotNet:F15} rad = {dotNet * 180 / Math.PI:F10}°");
        _output.WriteLine($"PineAtan2({y}, {x}) = {pine:F15} rad = {pine * 180 / Math.PI:F10}°");
        _output.WriteLine($"Difference = {Math.Abs(dotNet - pine):E}");

        Assert.Equal(dotNet, pine, precision: 13);
    }

    [Fact]
    public void Atan2_FullCircle_360DegreesSweep()
    {
        // Sweep through 360 degrees and verify both implementations match
        const int steps = 360;
        double maxDiff = 0;
        int maxDiffStep = 0;

        for (int i = 0; i < steps; i++)
        {
            double angle = i * 2.0 * Math.PI / steps;
            double y = Math.Sin(angle);
            double x = Math.Cos(angle);

            // Skip origin (angle = 0 with x=1, y=0 is fine, but need to handle numerical zeros)
            if (Math.Abs(x) < 1e-15 && Math.Abs(y) < 1e-15)
                continue;

            double dotNet = Math.Atan2(y, x);
            double pine = PineScriptAtan2(y, x);
            double diff = Math.Abs(dotNet - pine);

            if (diff > maxDiff)
            {
                maxDiff = diff;
                maxDiffStep = i;
            }
        }

        _output.WriteLine($"Full circle sweep: {steps} steps");
        _output.WriteLine($"Maximum difference: {maxDiff:E} at step {maxDiffStep} ({maxDiffStep}°)");

        // Expect very small differences
        Assert.True(maxDiff < 1e-14, $"Maximum difference {maxDiff:E} exceeds tolerance");
    }

    [Fact]
    public void Summary_ImplementationDifferences()
    {
        _output.WriteLine("=== Math.Atan2 vs PineScript atan2 Summary ===");
        _output.WriteLine("");
        _output.WriteLine("1. Origin handling:");
        _output.WriteLine("   - Math.Atan2(0, 0) returns 0");
        _output.WriteLine("   - PineScript throws an error");
        _output.WriteLine("");
        _output.WriteLine("2. Algorithm:");
        _output.WriteLine("   - Math.Atan2: IEEE standard implementation");
        _output.WriteLine("   - PineScript: Uses |y|/|x| or |x|/|y| based on which is larger");
        _output.WriteLine("                 This avoids division by very small numbers");
        _output.WriteLine("");
        _output.WriteLine("3. Numerical stability:");
        _output.WriteLine("   - Both handle extreme values well");
        _output.WriteLine("   - PineScript's approach may be slightly more stable for extreme aspect ratios");
        _output.WriteLine("");
        _output.WriteLine("4. Recommendation:");
        _output.WriteLine("   - Use Math.Atan2 for general purposes (standard, well-tested)");
        _output.WriteLine("   - PineScript algorithm adds ~zero benefit in .NET");
        _output.WriteLine("   - Only difference is origin handling (error vs 0)");

        Assert.True(true); // This is a documentation test
    }
}