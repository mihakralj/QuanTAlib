using Skender.Stock.Indicators;
using TALib;
using Tulip;
using QuanTAlib.Tests;

namespace QuanTAlib;

public sealed class AroonValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public AroonValidationTests()
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
        var aroon = new Aroon(14);
        var results = new List<double>();
        var upResults = new List<double>();
        var downResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = aroon.Update(_data.Bars[i]);
            results.Add(res.Value);
            upResults.Add(aroon.Up.Value);
            downResults.Add(aroon.Down.Value);
        }

        var skenderResults = _data.SkenderQuotes.GetAroon(14).ToList();

        // Verify Oscillator
        ValidationHelper.VerifyData(results, skenderResults, x => x.Oscillator);

        // Verify Up
        ValidationHelper.VerifyData(upResults, skenderResults, x => x.AroonUp);

        // Verify Down
        ValidationHelper.VerifyData(downResults, skenderResults, x => x.AroonDown);
    }

    [Fact]
    public void MatchesTalib()
    {
        var aroon = new Aroon(14);
        var results = new List<double>();
        var upResults = new List<double>();
        var downResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = aroon.Update(_data.Bars[i]);
            results.Add(res.Value);
            upResults.Add(aroon.Up.Value);
            downResults.Add(aroon.Down.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[] outAroonUp = new double[_data.Bars.Count];
        double[] outAroonDown = new double[_data.Bars.Count];
        double[] outAroonOsc = new double[_data.Bars.Count];

        // TA-Lib Aroon (Up/Down)
        var retCode = TALib.Functions.Aroon(hData, lData, 0..^0, outAroonDown, outAroonUp, out var outRange, 14);
        Assert.Equal(Core.RetCode.Success, retCode);

        // TA-Lib AroonOsc
        var retCodeOsc = TALib.Functions.AroonOsc(hData, lData, 0..^0, outAroonOsc, out var outRangeOsc, 14);
        Assert.Equal(Core.RetCode.Success, retCodeOsc);

        int lookback = TALib.Functions.AroonLookback(14);

        // Verify Up
        ValidationHelper.VerifyData(upResults, outAroonUp, outRange, lookback);

        // Verify Down
        ValidationHelper.VerifyData(downResults, outAroonDown, outRange, lookback);

        // Verify Oscillator
        ValidationHelper.VerifyData(results, outAroonOsc, outRangeOsc, lookback);
    }

    [Fact]
    public void MatchesTulip()
    {
        var aroon = new Aroon(14);
        var results = new List<double>();
        var upResults = new List<double>();
        var downResults = new List<double>();

        for (int i = 0; i < _data.Bars.Count; i++)
        {
            var res = aroon.Update(_data.Bars[i]);
            results.Add(res.Value);
            upResults.Add(aroon.Up.Value);
            downResults.Add(aroon.Down.Value);
        }

        double[] hData = _data.Bars.High.Select(x => x.Value).ToArray();
        double[] lData = _data.Bars.Low.Select(x => x.Value).ToArray();
        double[][] inputs = { hData, lData };
        double[] options = { 14 };

        // Tulip Aroon (Down, Up) - Note: Tulip returns Down then Up
        var aroonInd = Tulip.Indicators.aroon;
        double[][] outputs = { new double[hData.Length - 14], new double[hData.Length - 14] };
        aroonInd.Run(inputs, options, outputs);
        double[] tulipDown = outputs[0];
        double[] tulipUp = outputs[1];

        // Tulip AroonOsc
        var aroonOscInd = Tulip.Indicators.aroonosc;
        double[][] outputsOsc = { new double[hData.Length - 14] };
        aroonOscInd.Run(inputs, options, outputsOsc);
        double[] tulipOsc = outputsOsc[0];

        // Verify Up
        ValidationHelper.VerifyData(upResults, tulipUp, lookback: 14);

        // Verify Down
        ValidationHelper.VerifyData(downResults, tulipDown, lookback: 14);

        // Verify Oscillator
        ValidationHelper.VerifyData(results, tulipOsc, lookback: 14);
    }
}
