using System;
using System.Collections.Generic;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using Skender.Stock.Indicators;
using Xunit;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public sealed class IchimokuValidationTests : IDisposable
{
    private const double Precision = 1e-10;
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;
    private bool _disposed;

    public IchimokuValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (disposing)
        {
            _testData?.Dispose();
        }
    }

    #region Tenkan-sen Validation Tests

    [Fact]
    public void Tenkan_ManualCalculation_MatchesDonchianMidpoint()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Bar sequence with known highs and lows:
        // Bar 1: H=110, L=90
        // Bar 2: H=115, L=85
        // Bar 3: H=108, L=92
        // 3-period high = max(110, 115, 108) = 115
        // 3-period low = min(90, 85, 92) = 85
        // Tenkan = (115 + 85) / 2 = 100

        ichimoku.Update(new TBar(baseTime, 100, 110, 90, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, 100, 115, 85, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 120000, 100, 108, 92, 100, 1000));

        double expected = (115.0 + 85.0) / 2.0;
        Assert.Equal(expected, ichimoku.Tenkan.Value, Precision);
    }

    [Fact]
    public void Tenkan_SlidingWindow_DropsOldValues()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Initial 3 bars: H range 100-120, L range 80-90
        ichimoku.Update(new TBar(baseTime, 90, 100, 80, 90, 1000));       // H=100, L=80
        ichimoku.Update(new TBar(baseTime + 60000, 100, 110, 85, 100, 1000));  // H=110, L=85
        ichimoku.Update(new TBar(baseTime + 120000, 110, 120, 90, 110, 1000)); // H=120, L=90

        // Tenkan with bars 1-3: max(100,110,120)=120, min(80,85,90)=80
        // Tenkan = (120 + 80) / 2 = 100
        Assert.Equal(100.0, ichimoku.Tenkan.Value, Precision);

        // Add 4th bar: H=105, L=95
        // Window now includes bars 2,3,4: H=110,120,105, L=85,90,95
        // max(110,120,105)=120, min(85,90,95)=85
        // Tenkan = (120 + 85) / 2 = 102.5
        ichimoku.Update(new TBar(baseTime + 180000, 100, 105, 95, 100, 1000));
        Assert.Equal(102.5, ichimoku.Tenkan.Value, Precision);
    }

    #endregion

    #region Kijun-sen Validation Tests

    [Fact]
    public void Kijun_ManualCalculation_MatchesDonchianMidpoint()
    {
        var ichimoku = new Ichimoku(2, 4, 8, 4);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 4 bars for Kijun calculation
        // Bar 1: H=105, L=95
        // Bar 2: H=110, L=90
        // Bar 3: H=115, L=85
        // Bar 4: H=108, L=92
        // 4-period high = max(105,110,115,108) = 115
        // 4-period low = min(95,90,85,92) = 85
        // Kijun = (115 + 85) / 2 = 100

        ichimoku.Update(new TBar(baseTime, 100, 105, 95, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, 100, 110, 90, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 120000, 100, 115, 85, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 180000, 100, 108, 92, 100, 1000));

        double expected = (115.0 + 85.0) / 2.0;
        Assert.Equal(expected, ichimoku.Kijun.Value, Precision);
    }

    [Fact]
    public void Kijun_LongerPeriodThanTenkan_SmoothsMoreData()
    {
        var ichimoku = new Ichimoku(2, 4, 8, 4);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Add 4 bars with increasing trend
        for (int i = 0; i < 4; i++)
        {
            double basePrice = 100 + i * 5;
            ichimoku.Update(new TBar(baseTime + i * 60000, basePrice, basePrice + 5, basePrice - 5, basePrice, 1000));
        }

        // Tenkan (2-period) uses last 2 bars: bars 3,4
        // H range: 110+5, 115+5 = 115, 120 -> max=120
        // L range: 110-5, 115-5 = 105, 110 -> min=105
        // Tenkan = (120 + 105) / 2 = 112.5

        // Kijun (4-period) uses all 4 bars
        // H range: 100+5, 105+5, 110+5, 115+5 = 105, 110, 115, 120 -> max=120
        // L range: 100-5, 105-5, 110-5, 115-5 = 95, 100, 105, 110 -> min=95
        // Kijun = (120 + 95) / 2 = 107.5

        Assert.Equal(112.5, ichimoku.Tenkan.Value, Precision);
        Assert.Equal(107.5, ichimoku.Kijun.Value, Precision);
    }

    #endregion

    #region Senkou Span A Validation Tests

    [Fact]
    public void SenkouA_ManualCalculation_AverageOfTenkanKijun()
    {
        var ichimoku = new Ichimoku(2, 3, 5, 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Create scenario where we can calculate Tenkan and Kijun independently
        // Bar 1: H=100, L=80
        // Bar 2: H=120, L=70
        // Bar 3: H=110, L=90

        ichimoku.Update(new TBar(baseTime, 90, 100, 80, 90, 1000));
        ichimoku.Update(new TBar(baseTime + 60000, 95, 120, 70, 95, 1000));
        ichimoku.Update(new TBar(baseTime + 120000, 100, 110, 90, 100, 1000));

        // Tenkan (2-period): bars 2,3 -> H=120,110 max=120, L=70,90 min=70
        // Tenkan = (120 + 70) / 2 = 95

        // Kijun (3-period): bars 1,2,3 -> H=100,120,110 max=120, L=80,70,90 min=70
        // Kijun = (120 + 70) / 2 = 95

        // SenkouA = (Tenkan + Kijun) / 2 = (95 + 95) / 2 = 95

        double expectedTenkan = (120.0 + 70.0) / 2.0;
        double expectedKijun = (120.0 + 70.0) / 2.0;
        double expectedSenkouA = (expectedTenkan + expectedKijun) / 2.0;

        Assert.Equal(expectedTenkan, ichimoku.Tenkan.Value, Precision);
        Assert.Equal(expectedKijun, ichimoku.Kijun.Value, Precision);
        Assert.Equal(expectedSenkouA, ichimoku.SenkouA.Value, Precision);
    }

    [Fact]
    public void SenkouA_DifferentTenkanKijun_CorrectAverage()
    {
        var ichimoku = new Ichimoku(2, 4, 8, 4);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Bars designed to give different Tenkan and Kijun
        ichimoku.Update(new TBar(baseTime, 100, 100, 60, 80, 1000));       // Very low bar
        ichimoku.Update(new TBar(baseTime + 60000, 100, 110, 90, 100, 1000));
        ichimoku.Update(new TBar(baseTime + 120000, 100, 120, 100, 110, 1000));
        ichimoku.Update(new TBar(baseTime + 180000, 110, 130, 110, 120, 1000));

        // Tenkan (2-period): bars 3,4 -> H=120,130 max=130, L=100,110 min=100
        // Tenkan = (130 + 100) / 2 = 115

        // Kijun (4-period): all bars -> H=100,110,120,130 max=130, L=60,90,100,110 min=60
        // Kijun = (130 + 60) / 2 = 95

        // SenkouA = (115 + 95) / 2 = 105

        double expectedTenkan = (130.0 + 100.0) / 2.0;  // 115
        double expectedKijun = (130.0 + 60.0) / 2.0;    // 95
        double expectedSenkouA = (expectedTenkan + expectedKijun) / 2.0;  // 105

        Assert.Equal(expectedTenkan, ichimoku.Tenkan.Value, Precision);
        Assert.Equal(expectedKijun, ichimoku.Kijun.Value, Precision);
        Assert.Equal(expectedSenkouA, ichimoku.SenkouA.Value, Precision);
    }

    #endregion

    #region Senkou Span B Validation Tests

    [Fact]
    public void SenkouB_ManualCalculation_LongestPeriodMidpoint()
    {
        var ichimoku = new Ichimoku(2, 3, 5, 3);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 5 bars for Senkou B calculation
        double[] highs = { 100, 110, 120, 115, 105 };
        double[] lows = { 90, 85, 80, 88, 92 };

        for (int i = 0; i < 5; i++)
        {
            ichimoku.Update(new TBar(baseTime + i * 60000, (highs[i] + lows[i]) / 2, highs[i], lows[i], (highs[i] + lows[i]) / 2, 1000));
        }

        // 5-period: max(100,110,120,115,105) = 120, min(90,85,80,88,92) = 80
        // SenkouB = (120 + 80) / 2 = 100

        double expectedSenkouB = (120.0 + 80.0) / 2.0;
        Assert.Equal(expectedSenkouB, ichimoku.SenkouB.Value, Precision);
    }

    [Fact]
    public void SenkouB_LongestPeriod_IncorporatesAllData()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Add 10 bars with extreme at bar 1
        ichimoku.Update(new TBar(baseTime, 50, 200, 50, 125, 1000));  // Extreme high=200, low=50

        for (int i = 1; i < 10; i++)
        {
            ichimoku.Update(new TBar(baseTime + i * 60000, 100, 110, 90, 100, 1000));
        }

        // 10-period includes the extreme bar
        // max(200,110,110,...) = 200, min(50,90,90,...) = 50
        // SenkouB = (200 + 50) / 2 = 125

        Assert.Equal(125.0, ichimoku.SenkouB.Value, Precision);

        // Add another bar to drop the extreme
        ichimoku.Update(new TBar(baseTime + 10 * 60000, 100, 110, 90, 100, 1000));

        // Now 10-period window doesn't include extreme bar
        // max(110,110,...) = 110, min(90,90,...) = 90
        // SenkouB = (110 + 90) / 2 = 100

        Assert.Equal(100.0, ichimoku.SenkouB.Value, Precision);
    }

    #endregion

    #region Chikou Span Validation Tests

    [Fact]
    public void Chikou_EqualsCurrentClosePrice()
    {
        var ichimoku = new Ichimoku();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var testPrices = new double[] { 100.5, 102.3, 99.8, 105.0, 98.2 };

        foreach (double closePrice in testPrices)
        {
            ichimoku.Update(new TBar(baseTime, 100, 110, 90, closePrice, 1000));
            Assert.Equal(closePrice, ichimoku.Chikou.Value, Precision);
            baseTime += 60000;
        }
    }

    [Fact]
    public void Chikou_FollowsCloseExactly()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 15; i++)
        {
            double expectedClose = 100 + i * 1.5;
            ichimoku.Update(new TBar(baseTime + i * 60000, expectedClose, expectedClose + 5, expectedClose - 5, expectedClose, 1000));
            Assert.Equal(expectedClose, ichimoku.Chikou.Value, Precision);
        }
    }

    #endregion

    #region Cloud Formation Tests

    [Fact]
    public void Cloud_BullishConfiguration_SenkouAAboveB()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Strong uptrend with recently higher prices
        // Short-term (Tenkan) and medium-term (Kijun) should be higher than long-term (SenkouB)
        // This creates bullish cloud where SenkouA > SenkouB

        // Start with low prices
        for (int i = 0; i < 10; i++)
        {
            double price = 50 + i; // 50 to 59
            ichimoku.Update(new TBar(baseTime + i * 60000, price, price + 5, price - 5, price, 1000));
        }

        // Then jump to much higher prices - affects Tenkan and Kijun more than SenkouB
        for (int i = 10; i < 15; i++)
        {
            double price = 100 + (i - 10) * 2;
            ichimoku.Update(new TBar(baseTime + i * 60000, price, price + 5, price - 5, price, 1000));
        }

        // In this scenario, SenkouA should be above SenkouB (bullish cloud)
        // because Tenkan and Kijun are averaging recent higher prices
        // while SenkouB still includes older lower prices
        Assert.True(ichimoku.SenkouA.Value >= ichimoku.SenkouB.Value);
    }

    [Fact]
    public void Cloud_BearishConfiguration_SenkouBAboveA()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Downtrend scenario: start high, end low
        // SenkouB will remember old highs while Tenkan/Kijun fall

        // Start with high prices
        for (int i = 0; i < 10; i++)
        {
            double price = 150 - i; // 150 down to 141
            ichimoku.Update(new TBar(baseTime + i * 60000, price, price + 5, price - 5, price, 1000));
        }

        // Then drop to much lower prices
        for (int i = 10; i < 15; i++)
        {
            double price = 100 - (i - 10) * 3;
            ichimoku.Update(new TBar(baseTime + i * 60000, price, price + 5, price - 5, price, 1000));
        }

        // In downtrend, SenkouB (longer term) should be above SenkouA (bearish cloud)
        Assert.True(ichimoku.SenkouB.Value >= ichimoku.SenkouA.Value);
    }

    #endregion

    #region Standard Ichimoku Parameters Tests

    [Fact]
    public void StandardParameters_9_26_52_26_WorksCorrectly()
    {
        var ichimoku = new Ichimoku();  // Uses default 9, 26, 52, 26
        var barSeries = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Generate 100 bars of simulated price data
        double price = 100;
        for (int i = 0; i < 100; i++)
        {
            // Random walk-ish price movement
            double change = Math.Sin(i * 0.1) * 2 + Math.Cos(i * 0.05);
            price += change;
            barSeries.Add(new TBar(baseTime + i * 60000, price, price + 2, price - 2, price, 1000));
        }

        // Process all bars
        foreach (var bar in barSeries)
        {
            ichimoku.Update(bar);
        }

        // After 52 bars, should be warmed up
        Assert.True(ichimoku.IsHot);

        // All outputs should be finite
        Assert.True(double.IsFinite(ichimoku.Tenkan.Value));
        Assert.True(double.IsFinite(ichimoku.Kijun.Value));
        Assert.True(double.IsFinite(ichimoku.SenkouA.Value));
        Assert.True(double.IsFinite(ichimoku.SenkouB.Value));
        Assert.True(double.IsFinite(ichimoku.Chikou.Value));
    }

    [Fact]
    public void CryptoParameters_10_30_60_30_WorksCorrectly()
    {
        // Common crypto market settings (doubled because 24/7 markets)
        var ichimoku = new Ichimoku(10, 30, 60, 30);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Process enough bars to warmup
        for (int i = 0; i < 70; i++)
        {
            double price = 40000 + Math.Sin(i * 0.05) * 1000;
            ichimoku.Update(new TBar(baseTime + i * 60000, price, price + 50, price - 50, price, 10));
        }

        Assert.True(ichimoku.IsHot);
        Assert.Equal(60, ichimoku.WarmupPeriod);  // Based on SenkouB period
    }

    #endregion

    #region Batch Processing Validation Tests

    [Fact]
    public void Batch_MatchesSequentialProcessing()
    {
        var barSeries = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 60; i++)
        {
            double price = 100 + i;
            barSeries.Add(new TBar(baseTime + i * 60000, price, price + 5, price - 5, price, 1000));
        }

        // Batch processing
        var (batchTenkan, batchKijun, batchSenkouA, batchSenkouB, batchChikou) = Ichimoku.Batch(barSeries);

        // Sequential processing
        var sequential = new Ichimoku();
        var seqTenkan = new List<double>();
        var seqKijun = new List<double>();
        var seqSenkouA = new List<double>();
        var seqSenkouB = new List<double>();
        var seqChikou = new List<double>();

        foreach (var bar in barSeries)
        {
            sequential.Update(bar);
            seqTenkan.Add(sequential.Tenkan.Value);
            seqKijun.Add(sequential.Kijun.Value);
            seqSenkouA.Add(sequential.SenkouA.Value);
            seqSenkouB.Add(sequential.SenkouB.Value);
            seqChikou.Add(sequential.Chikou.Value);
        }

        // Compare results
        Assert.Equal(seqTenkan.Count, batchTenkan.Count);
        for (int i = 0; i < seqTenkan.Count; i++)
        {
            Assert.Equal(seqTenkan[i], batchTenkan[i].Value, Precision);
            Assert.Equal(seqKijun[i], batchKijun[i].Value, Precision);
            Assert.Equal(seqSenkouA[i], batchSenkouA[i].Value, Precision);
            Assert.Equal(seqSenkouB[i], batchSenkouB[i].Value, Precision);
            Assert.Equal(seqChikou[i], batchChikou[i].Value, Precision);
        }
    }

    [Fact]
    public void Calculate_ReturnsWarmIndicator()
    {
        var barSeries = new TBarSeries();
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        for (int i = 0; i < 60; i++)
        {
            double price = 100 + i;
            barSeries.Add(new TBar(baseTime + i * 60000, price, price + 5, price - 5, price, 1000));
        }

        var (results, indicator) = Ichimoku.Calculate(barSeries);

        Assert.True(indicator.IsHot);
        Assert.Equal(52, indicator.WarmupPeriod);

        // Last values in results should match indicator state
        Assert.Equal(indicator.Tenkan.Value, results.Tenkan.Last.Value, Precision);
        Assert.Equal(indicator.Kijun.Value, results.Kijun.Last.Value, Precision);
    }

    #endregion

    #region Cross Validation Tests

    [Fact]
    public void TenkanKijunCross_BullishSignal()
    {
        var ichimoku = new Ichimoku(3, 5, 10, 5);
        long baseTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // Create scenario where Tenkan starts below Kijun, then crosses above

        // Phase 1: Ranging market - Tenkan ≈ Kijun
        for (int i = 0; i < 5; i++)
        {
            ichimoku.Update(new TBar(baseTime + i * 60000, 100, 105, 95, 100, 1000));
        }

        // Capture initial state (using discards since we're testing the response to change)
        _ = ichimoku.Tenkan.Value;
        _ = ichimoku.Kijun.Value;

        // Phase 2: Sharp upward move - Tenkan should rise faster
        for (int i = 5; i < 10; i++)
        {
            double price = 100 + (i - 5) * 5;
            ichimoku.Update(new TBar(baseTime + i * 60000, price, price + 3, price - 3, price, 1000));
        }

        // Tenkan (short-term) should react faster to the uptrend
        // In uptrend, Tenkan >= Kijun
        Assert.True(ichimoku.Tenkan.Value >= ichimoku.Kijun.Value);
    }

    #endregion

    #region Skender Cross-Validation Tests

    [Fact]
    public void Validate_Skender_TenkanSen()
    {
        // Skender GetIchimoku returns IchimokuResult with TenkanSen (decimal?)
        // Both use Donchian midpoint: (highest-high + lowest-low) / 2 over tenkanPeriod
        var (qTenkan, _, _, _, _) = Ichimoku.Batch(_testData.Bars);
        var sResult = _testData.SkenderQuotes.GetIchimoku(9, 26, 52).ToList();

        int count = Math.Min(qTenkan.Count, sResult.Count);
        int start = Math.Max(9, count - 100);
        int matched = 0;

        for (int i = start; i < count; i++)
        {
            double qValue = qTenkan[i].Value;
            decimal? sValue = sResult[i].TenkanSen;
            if (!sValue.HasValue || !double.IsFinite(qValue))
            {
                continue;
            }

            double diff = Math.Abs(qValue - (double)sValue.Value);
            Assert.True(diff <= ValidationHelper.SkenderTolerance,
                $"Tenkan mismatch at [{i}]: QuanTAlib={qValue:G17}, Skender={(double)sValue.Value:G17}, diff={diff:E3}");
            matched++;
        }

        Assert.True(matched > 50, $"Only matched {matched} Tenkan values");
        _output.WriteLine($"Ichimoku Tenkan validated against Skender ({matched} values matched)");
    }

    [Fact]
    public void Validate_Skender_KijunSen()
    {
        var (_, qKijun, _, _, _) = Ichimoku.Batch(_testData.Bars);
        var sResult = _testData.SkenderQuotes.GetIchimoku(9, 26, 52).ToList();

        int count = Math.Min(qKijun.Count, sResult.Count);
        int start = Math.Max(26, count - 100);
        int matched = 0;

        for (int i = start; i < count; i++)
        {
            double qValue = qKijun[i].Value;
            decimal? sValue = sResult[i].KijunSen;
            if (!sValue.HasValue || !double.IsFinite(qValue))
            {
                continue;
            }

            double diff = Math.Abs(qValue - (double)sValue.Value);
            Assert.True(diff <= ValidationHelper.SkenderTolerance,
                $"Kijun mismatch at [{i}]: QuanTAlib={qValue:G17}, Skender={(double)sValue.Value:G17}, diff={diff:E3}");
            matched++;
        }

        Assert.True(matched > 50, $"Only matched {matched} Kijun values");
        _output.WriteLine($"Ichimoku Kijun validated against Skender ({matched} values matched)");
    }

    [Fact]
    public void Validate_Skender_SenkouSpanB()
    {
        // SenkouSpanB is the Donchian midpoint over the longest period (52)
        // Note: Skender shifts SenkouB forward by displacement periods in its output array,
        // so sResult[i].SenkouSpanB at index i is the value computed for bar (i - displacement).
        // QuanTAlib does NOT apply displacement in its batch output.
        // Therefore: QuanTAlib SenkouB[i] should match Skender SenkouSpanB[i + displacement].
        var (_, _, _, qSenkouB, _) = Ichimoku.Batch(_testData.Bars);
        var sResult = _testData.SkenderQuotes.GetIchimoku(9, 26, 52).ToList();

        int displacement = 26;
        int count = Math.Min(qSenkouB.Count, sResult.Count - displacement);
        int start = Math.Max(52, count - 100);
        int matched = 0;

        for (int i = start; i < count; i++)
        {
            double qValue = qSenkouB[i].Value;
            int sIdx = i + displacement;
            if (sIdx >= sResult.Count)
            {
                break;
            }
            decimal? sValue = sResult[sIdx].SenkouSpanB;
            if (!sValue.HasValue || !double.IsFinite(qValue))
            {
                continue;
            }

            double diff = Math.Abs(qValue - (double)sValue.Value);
            Assert.True(diff <= ValidationHelper.SkenderTolerance,
                $"SenkouB mismatch at q[{i}] vs s[{sIdx}]: QuanTAlib={qValue:G17}, Skender={(double)sValue.Value:G17}, diff={diff:E3}");
            matched++;
        }

        Assert.True(matched > 30, $"Only matched {matched} SenkouB values");
        _output.WriteLine($"Ichimoku SenkouB validated against Skender ({matched} values, offset +{displacement})");
    }

    [Fact]
    public void Validate_Skender_ChikouSpan()
    {
        // Chikou Span = current close price (plotted backward by displacement)
        // Both should agree that Chikou = Close at each bar
        var (_, _, _, _, qChikou) = Ichimoku.Batch(_testData.Bars);
        var sResult = _testData.SkenderQuotes.GetIchimoku(9, 26, 52).ToList();

        int displacement = 26;
        int count = Math.Min(qChikou.Count, sResult.Count);
        int matched = 0;

        // Skender stores ChikouSpan at index (i - displacement), i.e. sResult[i].ChikouSpan
        // is the close of bar (i + displacement). QuanTAlib Chikou[i] = Close[i].
        // So QuanTAlib Chikou[i] == Skender ChikouSpan[i - displacement] when i >= displacement.
        for (int i = displacement; i < count; i++)
        {
            double qValue = qChikou[i].Value;
            int sIdx = i - displacement;
            decimal? sValue = sResult[sIdx].ChikouSpan;
            if (!sValue.HasValue || !double.IsFinite(qValue))
            {
                continue;
            }

            double diff = Math.Abs(qValue - (double)sValue.Value);
            Assert.True(diff <= ValidationHelper.SkenderTolerance,
                $"Chikou mismatch at q[{i}] vs s[{sIdx}]: QuanTAlib={qValue:G17}, Skender={(double)sValue.Value:G17}, diff={diff:E3}");
            matched++;
        }

        Assert.True(matched > 50, $"Only matched {matched} Chikou values");
        _output.WriteLine($"Ichimoku Chikou validated against Skender ({matched} values matched)");
    }

    #endregion

    #region Ooples Cross-Validation

    [Fact]
    public void Ichimoku_MatchesOoples_Structural()
    {
        // CalculateIchimokuCloud — structural test; outputs stored in OutputValues (Tenkan/Kijun/etc.)
        var ooplesData = _testData.SkenderQuotes
            .Select(q => new TickerData { Date = q.Date, Open = (double)q.Open, High = (double)q.High, Low = (double)q.Low, Close = (double)q.Close, Volume = (double)q.Volume })
            .ToList();

        var result = new StockData(ooplesData).CalculateIchimokuCloud();
        // Ooples multi-output indicators store results in OutputValues, not CustomValuesList
        var allValues = result.OutputValues.Values.SelectMany(v => v).ToList();

        int finiteCount = allValues.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite Ooples Ichimoku values, got {finiteCount}");
    }

    #endregion

    [Fact]
    public void Ichimoku_Correction_Recomputes()
    {
        var ind = new Ichimoku();
        var t0 = new DateTime(946_684_800_000_000_0L, DateTimeKind.Utc);

        // Build state well past warmup
        for (int i = 0; i < 100; i++)
        {
            double p = 100.0 + 10.0 * Math.Sin(2.0 * Math.PI * i / 20.0);
            ind.Update(new TBar(t0.AddMinutes(i), p, p + 2, p - 2, p, 1000), isNew: true);
        }

        // Anchor bar
        var anchorTime = t0.AddMinutes(100);
        const double anchorClose = 105.5;
        ind.Update(new TBar(anchorTime, anchorClose, anchorClose + 2, anchorClose - 2, anchorClose, 1000), isNew: true);
        double anchorTenkan = ind.Tenkan.Value;

        // Correction with a dramatically different price — Tenkan must change
        ind.Update(new TBar(anchorTime, anchorClose * 10, (anchorClose + 2) * 10, (anchorClose - 2) * 10, anchorClose * 10, 1000), isNew: false);
        Assert.NotEqual(anchorTenkan, ind.Tenkan.Value);

        // Correction back to original price — must exactly restore original Tenkan
        ind.Update(new TBar(anchorTime, anchorClose, anchorClose + 2, anchorClose - 2, anchorClose, 1000), isNew: false);
        Assert.Equal(anchorTenkan, ind.Tenkan.Value, 1e-9);
    }
}
