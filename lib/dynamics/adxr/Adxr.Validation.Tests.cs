using TALib;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class AdxrValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public AdxrValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void MatchesTalib()
    {
        var adxr = new Adxr(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = adxr.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[] outReal = new double[_data.Bars.Count];

        var retCode = Functions.Adxr(hData, lData, cData, 0..^0, outReal, out var outRange, 14);
        Assert.Equal(Core.RetCode.Success, retCode);

        int lookback = Functions.AdxrLookback(14);
        ValidationHelper.VerifyData(results, outReal, outRange, lookback);
    }

    [Fact]
    public void MatchesTulip()
    {
        var adxr = new Adxr(14);
        var results = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = adxr.Update(_data.Bars[i]);
            results.Add(res.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] cData = _data.Bars.Close.Select(x => x.Value).ToArray();
        double[][] inputs = { hData, lData, cData };
        double[] options = { 14 };

        var adxrInd = Tulip.Indicators.adxr;
        double[][] outputs = { new double[hData.Length - adxrInd.Start(options)] };
        adxrInd.Run(inputs, options, outputs);
        double[] tulipResults = outputs[0];

        int lookback = adxrInd.Start(options);
        ValidationHelper.VerifyData(results, tulipResults, lookback);
    }
}
