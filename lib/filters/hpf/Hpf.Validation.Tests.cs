using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public class HpfValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public HpfValidationTests()
    {
        _testData = new ValidationTestData();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _testData.Dispose();
        }

        _disposed = true;
    }

    [Fact]
    public void Validate_Against_PineScript_Logic()
    {
        // Since we don't have an external library for this specific filter,
        // we validate against a local implementation of the Pine Script logic.

        var lengths = new[] { 10, 20, 40, 100 };
        var source = _testData.Data.Select(x => x.Value).ToArray();

        foreach (var length in lengths)
        {
            var hpf = new Hpf(length);
            var actual = new List<double>();
            foreach (var val in source)
            {
                actual.Add(hpf.Update(new TValue(DateTime.UtcNow.Ticks, val)).Value);
            }

            var expected = PineHpf(source, length);

            // Verify
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.Equal(expected[i], actual[i], 1e-9);
            }
        }
    }

    private static List<double> PineHpf(double[] src, int length)
    {
        // hpf(series float src, simple int length) =>
        // int safe_length = math.max(length, 1)
        // float pi = math.pi
        // float omega = 2.0 * pi / safe_length
        // float alpha = (math.cos(omega) + math.sin(omega) - 1.0) / math.cos(omega)
        // var float hp_val_internal = 0.0
        // float hp1 = nz(hp_val_internal[1], 0.0)
        // float hp2 = nz(hp_val_internal[2], 0.0)
        // float ssrc = nz(src, src[1])
        // float src1 = nz(src[1], ssrc)
        // float src2 = nz(src[2], src1)
        // float alpha_div_2 = alpha / 2.0
        // float one_minus_alpha = 1.0 - alpha
        // hp_val_internal := (1.0 - alpha_div_2) * (1.0 - alpha_div_2) * (ssrc - 2.0 * src1 + src2) + 2.0 * one_minus_alpha * hp1 - one_minus_alpha * one_minus_alpha * hp2

        int safe_length = Math.Max(length, 1);
        double omegaFactor = 0.70710678118654752440084436210485;
        double omega = omegaFactor * 2.0 * Math.PI / safe_length;
        double alpha = (Math.Cos(omega) + Math.Sin(omega) - 1.0) / Math.Cos(omega);

        double alphaDiv2 = alpha / 2.0;
        double oneMinusAlpha = 1.0 - alpha;

        double coeff1 = (1.0 - alphaDiv2) * (1.0 - alphaDiv2);
        double coeff2 = 2.0 * oneMinusAlpha;
        double coeff3 = oneMinusAlpha * oneMinusAlpha;

        var result = new List<double>();

        double hp1 = 0.0;
        double hp2 = 0.0;
        double src1 = 0.0;
        double src2 = 0.0;

        if (src.Length > 0)
        {
            // Sample 0: initialize basic history
            result.Add(0.0);
            src1 = src[0];
            src2 = src[0];
        }

        if (src.Length > 1)
        {
            // Sample 1: shift history, output 0 (start recursion at 3rd bar)
            result.Add(0.0);
            src2 = src1;
            src1 = src[1];
        }

        for (int i = 2; i < src.Length; i++)
        {
            double ssrc = src[i];

            double term1 = coeff1 * (ssrc - 2.0 * src1 + src2);
            double term2 = coeff2 * hp1;
            double term3 = coeff3 * hp2;

            double hp = term1 + term2 - term3;
            result.Add(hp);

            hp2 = hp1;
            hp1 = hp;
            src2 = src1;
            src1 = ssrc;
        }

        return result;
    }
}
