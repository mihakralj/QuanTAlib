using Skender.Stock.Indicators;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class VortexValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public VortexValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void MatchesSkender()
    {
        var vortex = new Vortex(14);
        var viPlusResults = new List<double>();
        var viMinusResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            vortex.Update(_data.Bars[i]);
            viPlusResults.Add(vortex.ViPlus.Value);
            viMinusResults.Add(vortex.ViMinus.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetVortex(14).ToList();

        // Verify VI+
        ValidationHelper.VerifyData(viPlusResults, skenderResults, x => x.Pvi);

        // Verify VI-
        ValidationHelper.VerifyData(viMinusResults, skenderResults, x => x.Nvi);
    }

    [Fact]
    public void MatchesSkender_Period21()
    {
        var vortex = new Vortex(21);
        var viPlusResults = new List<double>();
        var viMinusResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            vortex.Update(_data.Bars[i]);
            viPlusResults.Add(vortex.ViPlus.Value);
            viMinusResults.Add(vortex.ViMinus.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetVortex(21).ToList();

        // Verify VI+
        ValidationHelper.VerifyData(viPlusResults, skenderResults, x => x.Pvi);

        // Verify VI-
        ValidationHelper.VerifyData(viMinusResults, skenderResults, x => x.Nvi);
    }

    [Fact]
    public void MatchesSkender_ShortPeriod()
    {
        var vortex = new Vortex(7);
        var viPlusResults = new List<double>();
        var viMinusResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            vortex.Update(_data.Bars[i]);
            viPlusResults.Add(vortex.ViPlus.Value);
            viMinusResults.Add(vortex.ViMinus.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetVortex(7).ToList();

        // Verify VI+
        ValidationHelper.VerifyData(viPlusResults, skenderResults, x => x.Pvi);

        // Verify VI-
        ValidationHelper.VerifyData(viMinusResults, skenderResults, x => x.Nvi);
    }

    [Fact]
    public void MatchesSkender_LongPeriod()
    {
        var vortex = new Vortex(28);
        var viPlusResults = new List<double>();
        var viMinusResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            vortex.Update(_data.Bars[i]);
            viPlusResults.Add(vortex.ViPlus.Value);
            viMinusResults.Add(vortex.ViMinus.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetVortex(28).ToList();

        // Verify VI+
        ValidationHelper.VerifyData(viPlusResults, skenderResults, x => x.Pvi);

        // Verify VI-
        ValidationHelper.VerifyData(viMinusResults, skenderResults, x => x.Nvi);
    }

    [Fact]
    public void BatchMatchesSkender()
    {
        // Batch returns VI+ as the primary output (TSeries)
        var batchViPlus = Vortex.Batch(_data.Bars, 14);

        var skenderResults = _data.SkenderQuotes.GetVortex(14).ToList();

        // Verify batch VI+ matches Skender VI+
        var viPlusResults = batchViPlus.Select(x => x.Value).ToList();
        ValidationHelper.VerifyData(viPlusResults, skenderResults, x => x.Pvi);

        // For VI-, use streaming since Batch only returns VI+
        var vortex = new Vortex(14);
        var viMinusResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            vortex.Update(_data.Bars[i]);
            viMinusResults.Add(vortex.ViMinus.Value);
        }

        // Verify VI- from streaming matches Skender
        ValidationHelper.VerifyData(viMinusResults, skenderResults, x => x.Nvi);
    }

    [Fact]
    public void ConsistentAcrossMultipleRuns()
    {
        var vortex1 = new Vortex(14);
        var vortex2 = new Vortex(14);

        var results1Plus = new List<double>();
        var results1Minus = new List<double>();
        var results2Plus = new List<double>();
        var results2Minus = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            vortex1.Update(_data.Bars[i]);
            results1Plus.Add(vortex1.ViPlus.Value);
            results1Minus.Add(vortex1.ViMinus.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            vortex2.Update(_data.Bars[i]);
            results2Plus.Add(vortex2.ViPlus.Value);
            results2Minus.Add(vortex2.ViMinus.Value);
        }

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            Assert.Equal(results1Plus[i], results2Plus[i], 1e-10);
            Assert.Equal(results1Minus[i], results2Minus[i], 1e-10);
        }
    }
}
