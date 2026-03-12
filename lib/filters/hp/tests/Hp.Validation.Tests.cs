using Xunit;

namespace QuanTAlib.Tests;

public class HpValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;

    public HpValidationTests()
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
        if (disposing)
        {
            _testData.Dispose();
        }
    }

    [Fact]
    public void Validate_Against_PineScript_Logic()
    {
        // Since this is a Causal HP approximation from Pine Script,
        // we validate against a local C# implementation of the exact Pine logic
        // to ensure our optimized production code matches the reference algorithm.

        var hp = new Hp(1600);
        var source = _testData.Data;
        var qResult = hp.Update(source);

        var pineResult = PineScriptHp(source, 1600);

        Assert.Equal(qResult.Count, pineResult.Count);
        for (int i = 0; i < qResult.Count; i++)
        {
            Assert.Equal(pineResult[i].Value, qResult[i].Value, 1e-9);
        }
    }

    private static List<TValue> PineScriptHp(TSeries src, double lambda)
    {
        // Reference implementation of:
        // float alpha = (math.sqrt(lambda) * 0.5 - 1.0) / (math.sqrt(lambda) * 0.5 + 1.0)
        // hp_trend := bar_index >= 1 ? (1.0 - alpha) * price + alpha * prev_trend + 0.5 * alpha * (prev_trend - nz(hp_trend[2], prev_trend)) : price

        var result = new List<TValue>();

        double s = Math.Sqrt(lambda);
        double alpha = (s * 0.5 - 1.0) / (s * 0.5 + 1.0);
        alpha = Math.Max(alpha, 0.0001);
        alpha = Math.Min(alpha, 0.9999);

        double prev_trend = 0; // hp_trend[1]
        double prev_prev_trend = 0; // hp_trend[2]

        for (int i = 0; i < src.Count; i++)
        {
            double price = src[i].Value;
            double hp_trend;

            if (i == 0)
            {
                hp_trend = price;
                // for i=0, prev_trend and prev_prev_trend are not used or init to price effectively
                prev_trend = price;
                prev_prev_trend = price;
            }
            else
            {
                hp_trend = (1.0 - alpha) * price +
                           alpha * prev_trend +
                           0.5 * alpha * (prev_trend - prev_prev_trend);

                prev_prev_trend = prev_trend;
                prev_trend = hp_trend;
            }

            result.Add(new TValue(src[i].Time, hp_trend));
        }

        return result;
    }
}
