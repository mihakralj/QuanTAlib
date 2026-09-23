using Skender.Stock.Indicators;
using OoplesFinance.StockIndicators;
using OoplesFinance.StockIndicators.Models;
using TALib;
using Xunit.Abstractions;

namespace QuanTAlib.Tests;

public class MamaValidationTests
{
    private readonly ValidationTestData _testData;
    private readonly ITestOutputHelper _output;

    // QuanTAlib implements Ehlers' original atan(Q1/I1) phase discriminator (matching
    // Skender/TA-Lib), instead of atan2 + angle wrapping. The remaining ~1e-2 residual below
    // comes from warmup/priming differences (WMA-based unstable period vs simple averaging
    // over the first 6 bars), not from an algorithmic mismatch.
    private const double CrossLibraryTolerance = 0.01;

    public MamaValidationTests(ITestOutputHelper output)
    {
        _output = output;
        _testData = new ValidationTestData();
    }

    [Fact]
    public void Validate_Skender_Batch()
    {
        const double fastLimit = 0.5;
        double slowLimit = 0.05;

        // Skender uses HL2 by default. We need to feed (H+L)/2 to our Mama to match.
        var hl2Values = new List<double>();
        var hl2Times = new List<long>();
        foreach (var q in _testData.SkenderQuotes)
        {
            hl2Values.Add(((double)q.High + (double)q.Low) / 2.0);
            hl2Times.Add(q.Date.Ticks);
        }
        var hl2Series = new TSeries(hl2Times, hl2Values);

        // 1. Calculate QuanTAlib MAMA
        var mama = new Mama(fastLimit, slowLimit);
        var qResult = mama.Update(hl2Series);

        // 2. Calculate Skender MAMA
        var sResult = _testData.SkenderQuotes.GetMama(fastLimit, slowLimit).ToList();

        // 3. Verify MAMA
        ValidationHelper.VerifyData(qResult, sResult, x => x.Mama, skip: 100, tolerance: CrossLibraryTolerance);

        _output.WriteLine("MAMA Batch validated successfully against Skender");
    }

    [Fact]
    public void Validate_Skender_Streaming()
    {
        double fastLimit = 0.5;
        double slowLimit = 0.05;

        // 1. Calculate QuanTAlib MAMA (streaming)
        var mama = new Mama(fastLimit, slowLimit);
        var qMamaResults = new List<double>();
        var qFamaResults = new List<double>();

        for (int i = 0; i < _testData.SkenderQuotes.Count; i++)
        {
            double hl2 = ((double)_testData.SkenderQuotes[i].High + (double)_testData.SkenderQuotes[i].Low) / 2.0;
            var result = mama.Update(new TValue(_testData.Data.Times[i], hl2));
            qMamaResults.Add(result.Value);
            qFamaResults.Add(mama.Fama.Value);
        }

        // 2. Calculate Skender MAMA
        var sResult = _testData.SkenderQuotes.GetMama(fastLimit, slowLimit).ToList();

        // 3. Verify MAMA
        ValidationHelper.VerifyData(qMamaResults, sResult, x => x.Mama, skip: 100, tolerance: CrossLibraryTolerance);

        // 4. Verify FAMA
        ValidationHelper.VerifyData(qFamaResults, sResult, x => x.Fama, skip: 100, tolerance: CrossLibraryTolerance);

        _output.WriteLine("MAMA/FAMA Streaming validated successfully against Skender");
    }

    [Fact]
    public void Validate_Ooples_Batch()
    {
        double fastLimit = 0.5;
        double slowLimit = 0.05;

        // Prepare data for Ooples
        var ooplesData = _testData.SkenderQuotes.Select(q => new TickerData
        {
            Date = q.Date,
            Open = (double)q.Open,
            High = (double)q.High,
            Low = (double)q.Low,
            Close = (double)q.Close,
            Volume = (double)q.Volume
        }).ToList();

        // 1. Calculate Ooples MAMA
        var stockData = new StockData(ooplesData);
        var oResult = stockData.CalculateEhlersMotherOfAdaptiveMovingAverages(fastLimit, slowLimit);
        var oMama = oResult.OutputValues["Mama"];

        // 2. Calculate QuanTAlib MAMA (using Close price to match Ooples default)
        var mama = new Mama(fastLimit, slowLimit);
        var qResult = mama.Update(_testData.Data); // _testData.Data is Close prices

        // 3. Verify MAMA
        ValidationHelper.VerifyData(qResult, oMama, x => x, skip: 100, tolerance: CrossLibraryTolerance);

        // 4. Verify FAMA
        // QuanTAlib stores Fama in a separate property, not in the main TSeries result,
        // and batch Update(TSeries) only returns the Mama series, so FAMA batch comparison
        // is not exercised here (covered by the Skender streaming test instead).

        _output.WriteLine("MAMA Batch validated successfully against Ooples");
    }

    [Fact]
    public void Validate_Talib_Mama()
    {
        // TA-Lib and QuanTAlib both implement Ehlers' atan(Q1/I1) phase discriminator.
        const double fastLimit = 0.5;
        const double slowLimit = 0.05;

        // Use HL2 prices to match both libraries' optional default
        var hl2 = new double[_testData.Count];
        var highPrices = _testData.HighPrices.Span;
        var lowPrices = _testData.LowPrices.Span;
        for (int i = 0; i < _testData.Count; i++)
        {
            hl2[i] = (highPrices[i] + lowPrices[i]) * 0.5;
        }

        double[] taMama = new double[_testData.Count];
        double[] taFama = new double[_testData.Count];

        var retCode = Functions.Mama<double>(
            hl2, 0..^0,
            taMama, taFama,
            out var outRange,
            fastLimit, slowLimit);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        (int offset, int length) = outRange.GetOffsetAndLength(taMama.Length);
        Assert.True(length > 50, $"TALib MAMA produced only {length} values");

        // QuanTAlib MAMA (using HL2)
        var hl2Times = new List<long>();
        var hl2Vals = new List<double>(hl2);
        var timestamps = _testData.Timestamps.Span;
        for (int i = 0; i < _testData.Count; i++) { hl2Times.Add(timestamps[i]); }
        var hl2Series = new TSeries(hl2Times, hl2Vals);

        var mama = new Mama(fastLimit, slowLimit);
        var qResult = mama.Update(hl2Series);

        // Verify MAMA against TA-Lib over the last 100 converged bars.
        int start = Math.Max(0, length - 100);
        for (int j = start; j < length; j++)
        {
            int qi = j + offset;
            Assert.True(
                Math.Abs(qResult[qi].Value - taMama[j]) <= CrossLibraryTolerance,
                $"Mismatch at index {qi}: QuanTAlib={qResult[qi].Value:G17}, TALib={taMama[j]:G17}");
        }

        _output.WriteLine($"MAMA validated successfully against TALib ({length} values compared)");
    }
}
