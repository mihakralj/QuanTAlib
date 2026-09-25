using TALib;
using Xunit;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for ADD against TA-Lib's Add function.
/// </summary>
public class AddValidationTests
{
    private readonly GBM _gbmA = new(sigma: 0.5, mu: 0.0, seed: 100);
    private readonly GBM _gbmB = new(sigma: 0.3, mu: 0.0, seed: 200);
    private const double Tolerance = 1e-9;

    [Fact]
    public void Add_MatchesTalib_Batch()
    {
        var a = _gbmA.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var b = _gbmB.Fetch(200, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        var qResult = Add.Batch(a, b);

        double[] tOutput = new double[a.Count];
        var retCode = TALib.Functions.Add<double>(a.Values, b.Values, 0..^0, tOutput, out var outRange);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);

        var (offset, length) = outRange.GetOffsetAndLength(tOutput.Length);
        for (int i = 0; i < length; i++)
        {
            Assert.Equal(tOutput[offset + i], qResult[offset + i].Value, Tolerance);
        }
    }

    [Fact]
    public void Add_MatchesTalib_Streaming()
    {
        var a = _gbmA.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;
        var b = _gbmB.Fetch(100, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1)).Close;

        double[] tOutput = new double[a.Count];
        var retCode = TALib.Functions.Add<double>(a.Values, b.Values, 0..^0, tOutput, out var outRange);
        Assert.Equal(TALib.Core.RetCode.Success, retCode);
        var (offset, _) = outRange.GetOffsetAndLength(tOutput.Length);

        var add = new Add();
        for (int i = 0; i < a.Count; i++)
        {
            var r = add.Update(a[i], b[i], true);
            Assert.Equal(tOutput[offset + i], r.Value, Tolerance);
        }
    }
}
