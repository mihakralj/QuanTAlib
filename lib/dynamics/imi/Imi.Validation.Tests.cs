using System;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for IMI (Intraday Momentum Index) implementation.
/// These tests validate the calculation against the published formula by Tushar Chande:
/// IMI = 100 × Sum(Gains) / (Sum(Gains) + Sum(Losses))
/// where Gain = Close - Open if Close > Open, else 0
/// and Loss = Open - Close if Close < Open, else 0
/// </summary>
public sealed class ImiValidationTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public ImiValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public void Dispose()
    {
        // Cleanup if needed
    }

    #region Manual Calculation Verification

    [Fact]
    public void ManualCalculation_SimpleUpBars()
    {
        // Given 3 up bars with known gains
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Bar 1: Open=100, Close=105 → Gain=5
        imi.Update(new TBar(baseTime, 100, 108, 98, 105, 1000));

        // Bar 2: Open=105, Close=108 → Gain=3
        imi.Update(new TBar(baseTime + 60000, 105, 110, 104, 108, 1000));

        // Bar 3: Open=108, Close=110 → Gain=2
        imi.Update(new TBar(baseTime + 120000, 108, 112, 107, 110, 1000));

        // Total gains = 5 + 3 + 2 = 10
        // Total losses = 0
        // IMI = 100 × 10 / (10 + 0) = 100

        Assert.Equal(100.0, imi.Last.Value, 1e-10);

        _output.WriteLine($"Gains: 5 + 3 + 2 = 10");
        _output.WriteLine($"Losses: 0");
        _output.WriteLine($"IMI = 100 × 10 / 10 = {imi.Last.Value}");
    }

    [Fact]
    public void ManualCalculation_SimpleDownBars()
    {
        // Given 3 down bars with known losses
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Bar 1: Open=105, Close=100 → Loss=5
        imi.Update(new TBar(baseTime, 105, 108, 98, 100, 1000));

        // Bar 2: Open=100, Close=97 → Loss=3
        imi.Update(new TBar(baseTime + 60000, 100, 102, 95, 97, 1000));

        // Bar 3: Open=97, Close=95 → Loss=2
        imi.Update(new TBar(baseTime + 120000, 97, 99, 93, 95, 1000));

        // Total gains = 0
        // Total losses = 5 + 3 + 2 = 10
        // IMI = 100 × 0 / (0 + 10) = 0

        Assert.Equal(0.0, imi.Last.Value, 1e-10);

        _output.WriteLine($"Gains: 0");
        _output.WriteLine($"Losses: 5 + 3 + 2 = 10");
        _output.WriteLine($"IMI = 100 × 0 / 10 = {imi.Last.Value}");
    }

    [Fact]
    public void ManualCalculation_MixedBars()
    {
        // Given a mix of up and down bars
        var imi = new Imi(5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Bar 1: Open=100, Close=106 → Gain=6
        imi.Update(new TBar(baseTime, 100, 108, 98, 106, 1000));

        // Bar 2: Open=106, Close=102 → Loss=4
        imi.Update(new TBar(baseTime + 60000, 106, 108, 100, 102, 1000));

        // Bar 3: Open=102, Close=105 → Gain=3
        imi.Update(new TBar(baseTime + 120000, 102, 107, 101, 105, 1000));

        // Bar 4: Open=105, Close=105 → Doji (Gain=0, Loss=0)
        imi.Update(new TBar(baseTime + 180000, 105, 108, 102, 105, 1000));

        // Bar 5: Open=105, Close=103 → Loss=2
        imi.Update(new TBar(baseTime + 240000, 105, 107, 101, 103, 1000));

        // Total gains = 6 + 3 = 9
        // Total losses = 4 + 2 = 6
        // IMI = 100 × 9 / (9 + 6) = 100 × 9 / 15 = 60

        double expected = 100.0 * 9.0 / 15.0;
        Assert.Equal(expected, imi.Last.Value, 1e-10);

        _output.WriteLine($"Gains: 6 + 0 + 3 + 0 + 0 = 9");
        _output.WriteLine($"Losses: 0 + 4 + 0 + 0 + 2 = 6");
        _output.WriteLine($"IMI = 100 × 9 / 15 = {expected}");
        _output.WriteLine($"Actual: {imi.Last.Value}");
    }

    #endregion

    #region Rolling Window Validation

    [Fact]
    public void RollingWindow_DropsOldestValue()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Fill with 3 up bars (gains: 5, 5, 5)
        imi.Update(new TBar(baseTime, 100, 108, 98, 105, 1000));         // +5
        imi.Update(new TBar(baseTime + 60000, 100, 108, 98, 105, 1000)); // +5
        imi.Update(new TBar(baseTime + 120000, 100, 108, 98, 105, 1000)); // +5

        Assert.Equal(100.0, imi.Last.Value, 1e-10);

        // Add a down bar (loss: 5) - oldest gain (5) drops off
        imi.Update(new TBar(baseTime + 180000, 105, 108, 98, 100, 1000)); // -5

        // Now: gains = 5 + 5 = 10, losses = 5
        // IMI = 100 × 10 / 15 = 66.666...
        double expected = 100.0 * 10.0 / 15.0;
        Assert.Equal(expected, imi.Last.Value, 1e-10);

        _output.WriteLine($"After 4th bar:");
        _output.WriteLine($"  Window: [+5, +5, -5]");
        _output.WriteLine($"  Gains: 5 + 5 = 10");
        _output.WriteLine($"  Losses: 5");
        _output.WriteLine($"  IMI = {expected}");
    }

    #endregion

    #region Edge Case Validation

    [Fact]
    public void EdgeCase_AllDojiBars_Returns50()
    {
        // When all bars are doji (Open == Close), IMI should be 50 (neutral)
        var imi = new Imi(5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 5; i++)
        {
            // Doji: Open == Close
            imi.Update(new TBar(baseTime + i * 60000, 100, 105, 95, 100, 1000));
        }

        Assert.Equal(50.0, imi.Last.Value, 1e-10);
        _output.WriteLine("All doji bars (O==C) → IMI = 50 (neutral)");
    }

    [Fact]
    public void EdgeCase_VerySmallMovements()
    {
        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Very small gains
        imi.Update(new TBar(baseTime, 100.0, 100.1, 99.9, 100.0001, 1000));
        imi.Update(new TBar(baseTime + 60000, 100.0, 100.1, 99.9, 100.0002, 1000));
        imi.Update(new TBar(baseTime + 120000, 100.0, 100.1, 99.9, 100.0003, 1000));

        // All are tiny up bars, should still be 100
        Assert.Equal(100.0, imi.Last.Value, 1e-10);
        _output.WriteLine($"Very small gains still → IMI = {imi.Last.Value}");
    }

    [Fact]
    public void EdgeCase_Period1()
    {
        // With period 1, each bar is its own calculation
        var imi = new Imi(1);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Up bar
        imi.Update(new TBar(baseTime, 100, 110, 95, 108, 1000));
        Assert.Equal(100.0, imi.Last.Value, 1e-10);

        // Down bar
        imi.Update(new TBar(baseTime + 60000, 108, 110, 95, 100, 1000));
        Assert.Equal(0.0, imi.Last.Value, 1e-10);

        // Doji
        imi.Update(new TBar(baseTime + 120000, 100, 105, 95, 100, 1000));
        Assert.Equal(50.0, imi.Last.Value, 1e-10);

        _output.WriteLine("Period=1: Each bar → immediate IMI response");
    }

    #endregion

    #region Investopedia Example Validation

    [Fact]
    public void InvestopediaFormula_MatchesDefinition()
    {
        // Validate against Investopedia formula:
        // IMI = (Sum of Up Closes / (Sum of Up Closes + Sum of Down Closes)) × 100
        // Where Up Close = Close - Open when Close > Open

        var imi = new Imi(4);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Day 1: Close > Open (Up day: +3)
        imi.Update(new TBar(baseTime, 50, 54, 49, 53, 1000));

        // Day 2: Close < Open (Down day: -2)
        imi.Update(new TBar(baseTime + 86400000, 53, 54, 50, 51, 1000));

        // Day 3: Close > Open (Up day: +4)
        imi.Update(new TBar(baseTime + 172800000, 51, 56, 50, 55, 1000));

        // Day 4: Close > Open (Up day: +1)
        imi.Update(new TBar(baseTime + 259200000, 55, 57, 54, 56, 1000));

        // Sum of Up Closes = 3 + 4 + 1 = 8
        // Sum of Down Closes = 2
        // IMI = 100 × 8 / (8 + 2) = 80

        double expected = 100.0 * 8.0 / 10.0;
        Assert.Equal(expected, imi.Last.Value, 1e-10);

        _output.WriteLine("Investopedia formula validation:");
        _output.WriteLine($"  Up gains: 3 + 4 + 1 = 8");
        _output.WriteLine($"  Down losses: 2");
        _output.WriteLine($"  IMI = 100 × 8 / 10 = {expected}");
    }

    #endregion

    #region Comparison with RSI Concept

    [Fact]
    public void ImiVsRsiConcept_UsesIntradayNotInterday()
    {
        // IMI differs from RSI in that it uses Open-to-Close (intraday)
        // rather than Close-to-Close (interday)

        var imi = new Imi(3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Bar 1: Open=100, Close=105 (up bar, +5)
        // Bar 2: Open=110, Close=108 (down bar, -2) 
        //        Note: This is up from prev close (105→108) but down intraday!
        // Bar 3: Open=105, Close=110 (up bar, +5)

        imi.Update(new TBar(baseTime, 100, 108, 98, 105, 1000));
        imi.Update(new TBar(baseTime + 60000, 110, 112, 106, 108, 1000)); // Intraday down
        imi.Update(new TBar(baseTime + 120000, 105, 112, 104, 110, 1000));

        // Gains = 5 + 5 = 10
        // Losses = 2
        // IMI = 100 × 10 / 12 = 83.333...

        double expected = 100.0 * 10.0 / 12.0;
        Assert.Equal(expected, imi.Last.Value, 1e-10);

        _output.WriteLine("IMI uses Open-to-Close (intraday), not Close-to-Close (interday)");
        _output.WriteLine($"Bar 2: Opens at 110, closes at 108 → DOWN day for IMI");
        _output.WriteLine($"IMI = {imi.Last.Value:F4}");
    }

    #endregion

    #region Overbought/Oversold Levels

    [Fact]
    public void OverboughtLevel_Above70()
    {
        var imi = new Imi(5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Create scenario with IMI > 70 (overbought)
        // Need gains > 2.33 × losses for IMI > 70
        // 4 up bars (+5 each), 1 down bar (-3)
        // Gains = 20, Losses = 3
        // IMI = 100 × 20/23 = 86.96

        imi.Update(new TBar(baseTime, 100, 108, 98, 105, 1000));         // +5
        imi.Update(new TBar(baseTime + 60000, 100, 108, 98, 105, 1000)); // +5
        imi.Update(new TBar(baseTime + 120000, 100, 108, 98, 105, 1000)); // +5
        imi.Update(new TBar(baseTime + 180000, 100, 108, 98, 105, 1000)); // +5
        imi.Update(new TBar(baseTime + 240000, 100, 102, 95, 97, 1000)); // -3

        Assert.True(imi.Last.Value > 70);
        _output.WriteLine($"Overbought (>70): IMI = {imi.Last.Value:F2}");
    }

    [Fact]
    public void OversoldLevel_Below30()
    {
        var imi = new Imi(5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Create scenario with IMI < 30 (oversold)
        // Need losses > 2.33 × gains for IMI < 30
        // 4 down bars (-5 each), 1 up bar (+3)
        // Gains = 3, Losses = 20
        // IMI = 100 × 3/23 = 13.04

        imi.Update(new TBar(baseTime, 105, 108, 98, 100, 1000));         // -5
        imi.Update(new TBar(baseTime + 60000, 105, 108, 98, 100, 1000)); // -5
        imi.Update(new TBar(baseTime + 120000, 105, 108, 98, 100, 1000)); // -5
        imi.Update(new TBar(baseTime + 180000, 105, 108, 98, 100, 1000)); // -5
        imi.Update(new TBar(baseTime + 240000, 100, 108, 98, 103, 1000)); // +3

        Assert.True(imi.Last.Value < 30);
        _output.WriteLine($"Oversold (<30): IMI = {imi.Last.Value:F2}");
    }

    #endregion
}
