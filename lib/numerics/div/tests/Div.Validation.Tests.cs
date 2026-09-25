using TALib;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for DIV against TA-Lib's Div function.
/// GBM close prices are strictly positive, so the zero-denominator policy is not exercised here;
/// see Div.Tests.cs for that behavior.
/// </summary>
public class DivValidationTests
{
    private readonly GBM _gbmA = new(sigma: 0.5, mu: 0.0, seed: 103);
    private readonly GBM _gbmB = new(sigma: 0.3, mu: 0.0, seed: 203);
    private const double Tolerance = 1e-9;

    [Fact]
    public void Div_MatchesTalib_Batch()
    {
        var a = _gbmA.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var b = _gbmB.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        var qResult = Div.Batch(a, b);

        double[] tOutput = new double[a.Count];
        var retCode = TALib.Functions.Div<double>(a.Values, b.Values, 0..^0, tOutput, out var outRange);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);
        for (int i = 0; i < length; i++)
        {
            Assert.Equal(tOutput[offset + i], qResult[offset + i].Value, Tolerance);
        }
    }

    [Fact]
    public void Div_MatchesTalib_Streaming()
    {
        var a = _gbmA.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var b = _gbmB.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        double[] tOutput = new double[a.Count];
        var retCode = TALib.Functions.Div<double>(a.Values, b.Values, 0..^0, tOutput, out var outRange);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);
        var (offset, _) = outRange.GetOffsetAndLength(tOutput.Length);

        var div = new Div();
        for (int i = 0; i < a.Count; i++)
        {
            var r = div.Update(a[i], b[i], true);
            Assert.Equal(tOutput[offset + i], r.Value, Tolerance);
        }
    }
}
