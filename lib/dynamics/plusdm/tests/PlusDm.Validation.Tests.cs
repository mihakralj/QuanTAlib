using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Enums;
using Skender.Stock.Indicators;
using TALib;
using QuanTAlib.Tests;

namespace QuanTAlib;

/// <summary>
/// Validation tests for PlusDm (+DM). Cross-validates against TA-Lib,
/// OoplesFinance, and internal Dx equivalence with multiple periods.
/// Note: Skender does not expose DM values directly; only DI values via GetAdx().
/// </summary>
public sealed class PlusDmValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public PlusDmValidationTests()
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
    public void MatchesTalib()
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

    [Theory]
    [InlineData(7)]
    [InlineData(21)]
    [InlineData(28)]
    public void MatchesTalib_VariousPeriods(int period)
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

    // ═══════════════════════════════════════════════
    //  Dx Equivalence
    // ═══════════════════════════════════════════════

    [Fact]
    public void ExactlyMatchesDx_DmPlus()
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

    // ═══════════════════════════════════════════════
    //  OoplesFinance Structural Validation
    // ═══════════════════════════════════════════════

    [Fact]
    public void MatchesOoples_Structural()
    {
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

        var allValues = adxResults.OutputValues.Values.SelectMany(v => v).ToList();
        int finiteCount = allValues.Count(v => double.IsFinite(v));
        Assert.True(finiteCount > 100, $"Expected >100 finite Ooples ADX/DI values, got {finiteCount}");
    }

    // ═══════════════════════════════════════════════
    //  Self-Consistency: Batch == Streaming
    // ═══════════════════════════════════════════════

    [Fact]
    public void BatchEqualsStreaming()
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
    public void BatchMatchesTalib()
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

    // ═══════════════════════════════════════════════
    //  Determinism
    // ═══════════════════════════════════════════════

    [Fact]
    public void ConsistentAcrossMultipleRuns()
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

    // ═══════════════════════════════════════════════
    //  Output Range Validation
    // ═══════════════════════════════════════════════

    [Fact]
    public void OutputIsNonNegative()
    {
        var indicator = new PlusDm(14);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            indicator.Update(_data.Bars[i]);
            Assert.True(indicator.Last.Value >= 0, $"+DM output at bar {i} was {indicator.Last.Value}");
        }
    }

    // ═══════════════════════════════════════════════
    //  Different Periods Produce Different Results
    // ═══════════════════════════════════════════════

    [Fact]
    public void DifferentPeriods_ProduceDifferentResults()
    {
        var short7 = new PlusDm(7);
        var long28 = new PlusDm(28);

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            short7.Update(_data.Bars[i]);
            long28.Update(_data.Bars[i]);
        }

        Assert.NotEqual(short7.Last.Value, long28.Last.Value);
    }
}
