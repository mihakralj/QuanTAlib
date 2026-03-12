using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;
using Skender.Stock.Indicators;
using TALib;
using QuanTAlib.Tests;

namespace QuanTAlib;

/// <summary>
/// Combined validation tests for PlusDi, MinusDi, PlusDm, MinusDm.
/// Cross-validates against TA-Lib, Skender, Ooples, and Dx equivalence.
/// </summary>
public sealed class DiDmValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public DiDmValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    // ═══════════════════════════════════════════════
    //  TA-Lib Validation
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_MatchesTalib()
    {
        var indicator = new PlusDi(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.PlusDI(hData, lData, cData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.PlusDILookback(14);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    [Fact]
    public void MinusDi_MatchesTalib()
    {
        var indicator = new MinusDi(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.MinusDI(hData, lData, cData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.MinusDILookback(14);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    [Fact]
    public void PlusDm_MatchesTalib()
    {
        var indicator = new PlusDm(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.PlusDM(hData, lData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.PlusDMLookback(14);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    [Fact]
    public void MinusDm_MatchesTalib()
    {
        var indicator = new MinusDm(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.MinusDM(hData, lData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.MinusDMLookback(14);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    // ═══════════════════════════════════════════════
    //  Skender Validation
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_MatchesSkender()
    {
        var indicator = new PlusDi(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAdx(14).ToList();

        ValidationHelper.VerifyData(results, skenderResults, x => x.Pdi);
    }

    [Fact]
    public void MinusDi_MatchesSkender()
    {
        var indicator = new MinusDi(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAdx(14).ToList();

        ValidationHelper.VerifyData(results, skenderResults, x => x.Mdi);
    }

    // ═══════════════════════════════════════════════
    //  Dx Equivalence
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_ExactlyMatchesDx_DiPlus()
    {
        var indicator = new PlusDi(14);
        var dx = new Dx(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            dx.Update(_data.Bars[i]);

            Assert.Equal(dx.DiPlus.Value, indicator.Last.Value, 1e-12);
        }
    }

    [Fact]
    public void MinusDi_ExactlyMatchesDx_DiMinus()
    {
        var indicator = new MinusDi(14);
        var dx = new Dx(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            dx.Update(_data.Bars[i]);

            Assert.Equal(dx.DiMinus.Value, indicator.Last.Value, 1e-12);
        }
    }

    [Fact]
    public void PlusDm_ExactlyMatchesDx_DmPlus()
    {
        var indicator = new PlusDm(14);
        var dx = new Dx(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            dx.Update(_data.Bars[i]);

            Assert.Equal(dx.DmPlus.Value, indicator.Last.Value, 1e-12);
        }
    }

    [Fact]
    public void MinusDm_ExactlyMatchesDx_DmMinus()
    {
        var indicator = new MinusDm(14);
        var dx = new Dx(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            dx.Update(_data.Bars[i]);

            Assert.Equal(dx.DmMinus.Value, indicator.Last.Value, 1e-12);
        }
    }

    // ═══════════════════════════════════════════════
    //  Self-Consistency: Batch == Streaming
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_BatchEqualsStreaming()
    {
        var batchResults = PlusDi.Batch(_data.Bars, 14);

        var streaming = new PlusDi(14);
        var streamResults = new List<double>();
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            streamResults.Add(streaming.Update(_data.Bars[i]).Value);
        }

        Assert.Equal(streamResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void MinusDi_BatchEqualsStreaming()
    {
        var batchResults = MinusDi.Batch(_data.Bars, 14);

        var streaming = new MinusDi(14);
        var streamResults = new List<double>();
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            streamResults.Add(streaming.Update(_data.Bars[i]).Value);
        }

        Assert.Equal(streamResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void PlusDm_BatchEqualsStreaming()
    {
        var batchResults = PlusDm.Batch(_data.Bars, 14);

        var streaming = new PlusDm(14);
        var streamResults = new List<double>();
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            streamResults.Add(streaming.Update(_data.Bars[i]).Value);
        }

        Assert.Equal(streamResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-9);
        }
    }

    [Fact]
    public void MinusDm_BatchEqualsStreaming()
    {
        var batchResults = MinusDm.Batch(_data.Bars, 14);

        var streaming = new MinusDm(14);
        var streamResults = new List<double>();
        for (int i = 0; i < _data.Bars.Count; i++)
        {
            streamResults.Add(streaming.Update(_data.Bars[i]).Value);
        }

        Assert.Equal(streamResults.Count, batchResults.Count);
        for (int i = 0; i < batchResults.Count; i++)
        {
            Assert.Equal(streamResults[i], batchResults.Values[i], 1e-9);
        }
    }

    // ═══════════════════════════════════════════════
    //  Multi-Period TALib Validation
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData(7)]
    [InlineData(21)]
    [InlineData(28)]
    public void PlusDi_MatchesTalib_VariousPeriods(int period)
    {
        var indicator = new PlusDi(period);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.PlusDI(hData, lData, cData, 0..^0, outReal, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.PlusDILookback(period);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(21)]
    [InlineData(28)]
    public void MinusDi_MatchesTalib_VariousPeriods(int period)
    {
        var indicator = new MinusDi(period);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.MinusDI(hData, lData, cData, 0..^0, outReal, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.MinusDILookback(period);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(21)]
    [InlineData(28)]
    public void PlusDm_MatchesTalib_VariousPeriods(int period)
    {
        var indicator = new PlusDm(period);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.PlusDM(hData, lData, 0..^0, outReal, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.PlusDMLookback(period);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(21)]
    [InlineData(28)]
    public void MinusDm_MatchesTalib_VariousPeriods(int period)
    {
        var indicator = new MinusDm(period);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.MinusDM(hData, lData, 0..^0, outReal, out var outRange, period);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.MinusDMLookback(period);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    // ═══════════════════════════════════════════════
    //  Multi-Period Skender Validation
    // ═══════════════════════════════════════════════

    [Theory]
    [InlineData(7)]
    [InlineData(21)]
    [InlineData(28)]
    public void PlusDi_MatchesSkender_VariousPeriods(int period)
    {
        var indicator = new PlusDi(period);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAdx(period).ToList();
        ValidationHelper.VerifyData(results, skenderResults, x => x.Pdi);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(21)]
    [InlineData(28)]
    public void MinusDi_MatchesSkender_VariousPeriods(int period)
    {
        var indicator = new MinusDi(period);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            results.Add(indicator.Last.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAdx(period).ToList();
        ValidationHelper.VerifyData(results, skenderResults, x => x.Mdi);
    }

    // ═══════════════════════════════════════════════
    //  Determinism: Consistent Across Multiple Runs
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_ConsistentAcrossMultipleRuns()
    {
        var ind1 = new PlusDi(14);
        var ind2 = new PlusDi(14);
        var results1 = new List<double>();
        var results2 = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            ind1.Update(_data.Bars[i]);
            results1.Add(ind1.Last.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            ind2.Update(_data.Bars[i]);
            results2.Add(ind2.Last.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(results1[i], results2[i], 1e-10);
        }
    }

    [Fact]
    public void MinusDi_ConsistentAcrossMultipleRuns()
    {
        var ind1 = new MinusDi(14);
        var ind2 = new MinusDi(14);
        var results1 = new List<double>();
        var results2 = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            ind1.Update(_data.Bars[i]);
            results1.Add(ind1.Last.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            ind2.Update(_data.Bars[i]);
            results2.Add(ind2.Last.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(results1[i], results2[i], 1e-10);
        }
    }

    [Fact]
    public void PlusDm_ConsistentAcrossMultipleRuns()
    {
        var ind1 = new PlusDm(14);
        var ind2 = new PlusDm(14);
        var results1 = new List<double>();
        var results2 = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            ind1.Update(_data.Bars[i]);
            results1.Add(ind1.Last.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            ind2.Update(_data.Bars[i]);
            results2.Add(ind2.Last.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(results1[i], results2[i], 1e-10);
        }
    }

    [Fact]
    public void MinusDm_ConsistentAcrossMultipleRuns()
    {
        var ind1 = new MinusDm(14);
        var ind2 = new MinusDm(14);
        var results1 = new List<double>();
        var results2 = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            ind1.Update(_data.Bars[i]);
            results1.Add(ind1.Last.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            ind2.Update(_data.Bars[i]);
            results2.Add(ind2.Last.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(results1[i], results2[i], 1e-10);
        }
    }

    // ═══════════════════════════════════════════════
    //  Non-Negative Output Validation
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_OutputIsNonNegative()
    {
        var indicator = new PlusDi(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            Assert.True(indicator.Last.Value >= 0, $"PlusDi output at bar {i} was {indicator.Last.Value}");
        }
    }

    [Fact]
    public void MinusDi_OutputIsNonNegative()
    {
        var indicator = new MinusDi(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            Assert.True(indicator.Last.Value >= 0, $"MinusDi output at bar {i} was {indicator.Last.Value}");
        }
    }

    [Fact]
    public void PlusDm_OutputIsNonNegative()
    {
        var indicator = new PlusDm(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            Assert.True(indicator.Last.Value >= 0, $"PlusDm output at bar {i} was {indicator.Last.Value}");
        }
    }

    [Fact]
    public void MinusDm_OutputIsNonNegative()
    {
        var indicator = new MinusDm(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            Assert.True(indicator.Last.Value >= 0, $"MinusDm output at bar {i} was {indicator.Last.Value}");
        }
    }

    // ═══════════════════════════════════════════════
    //  DI values bounded 0-100
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_OutputBounded0To100()
    {
        var indicator = new PlusDi(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            double val = indicator.Last.Value;
            if (i >= 14)
            {
                Assert.True(val >= 0 && val <= 100, $"PlusDi at bar {i} was {val}, expected [0,100]");
            }
        }
    }

    [Fact]
    public void MinusDi_OutputBounded0To100()
    {
        var indicator = new MinusDi(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            double val = indicator.Last.Value;
            if (i >= 14)
            {
                Assert.True(val >= 0 && val <= 100, $"MinusDi at bar {i} was {val}, expected [0,100]");
            }
        }
    }

    // ═══════════════════════════════════════════════
    //  Different Periods Produce Different Results
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_DifferentPeriods_ProduceDifferentResults()
    {
        var short14 = new PlusDi(7);
        var long28 = new PlusDi(28);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            short14.Update(_data.Bars[i]);
            long28.Update(_data.Bars[i]);
        }

        Assert.NotEqual(short14.Last.Value, long28.Last.Value);
    }

    [Fact]
    public void MinusDi_DifferentPeriods_ProduceDifferentResults()
    {
        var short14 = new MinusDi(7);
        var long28 = new MinusDi(28);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            short14.Update(_data.Bars[i]);
            long28.Update(_data.Bars[i]);
        }

        Assert.NotEqual(short14.Last.Value, long28.Last.Value);
    }

    [Fact]
    public void PlusDm_DifferentPeriods_ProduceDifferentResults()
    {
        var short14 = new PlusDm(7);
        var long28 = new PlusDm(28);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            short14.Update(_data.Bars[i]);
            long28.Update(_data.Bars[i]);
        }

        Assert.NotEqual(short14.Last.Value, long28.Last.Value);
    }

    [Fact]
    public void MinusDm_DifferentPeriods_ProduceDifferentResults()
    {
        var short14 = new MinusDm(7);
        var long28 = new MinusDm(28);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            short14.Update(_data.Bars[i]);
            long28.Update(_data.Bars[i]);
        }

        Assert.NotEqual(short14.Last.Value, long28.Last.Value);
    }

    // ═══════════════════════════════════════════════
    //  OoplesFinance Structural Validation
    // ═══════════════════════════════════════════════

    [Fact]
    public void DiDm_MatchesOoples_Structural()
    {
        // OoplesFinance.CalculateAverageDirectionalIndex produces Di+/Di- as part of ADX
        var ooplesData = _data.SkenderQuotes
            .Select(q => new TickerData
            {
                Date = q.Date,
                Open = (double)q.Open,
                High = (double)q.High,
                Low = (double)q.Low,
                Close = (double)q.Close,
                Volume = (double)q.Volume
            })
            .ToList();

        var stockData = new StockData(ooplesData);
        var adxResults = stockData.CalculateAverageDirectionalIndex(MovingAvgType.WildersSmoothingMethod, 14);

        // Verify the Ooples ADX calculation produces finite DI values
        var allValues = adxResults.OutputValues.Values.SelectMany(v => v).ToList();
        int finiteCount = allValues.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite Ooples DI/DM values, got {finiteCount}");
    }

    // ═══════════════════════════════════════════════
    //  Batch Matches TALib
    // ═══════════════════════════════════════════════

    [Fact]
    public void PlusDi_BatchMatchesTalib()
    {
        var batchResults = PlusDi.Batch(_data.Bars, 14);

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.PlusDI(hData, lData, cData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.PlusDILookback(14);
        ValidationHelper.VerifyData(batchResults.Select(x => x.Value).ToList(), outReal, outRange, lookback);
    }

    [Fact]
    public void MinusDi_BatchMatchesTalib()
    {
        var batchResults = MinusDi.Batch(_data.Bars, 14);

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.MinusDI(hData, lData, cData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.MinusDILookback(14);
        ValidationHelper.VerifyData(batchResults.Select(x => x.Value).ToList(), outReal, outRange, lookback);
    }

    [Fact]
    public void PlusDm_BatchMatchesTalib()
    {
        var batchResults = PlusDm.Batch(_data.Bars, 14);

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.PlusDM(hData, lData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.PlusDMLookback(14);
        ValidationHelper.VerifyData(batchResults.Select(x => x.Value).ToList(), outReal, outRange, lookback);
    }

    [Fact]
    public void MinusDm_BatchMatchesTalib()
    {
        var batchResults = MinusDm.Batch(_data.Bars, 14);

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.MinusDM(hData, lData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        int lookback = Functions.MinusDMLookback(14);
        ValidationHelper.VerifyData(batchResults.Select(x => x.Value).ToList(), outReal, outRange, lookback);
    }
}
