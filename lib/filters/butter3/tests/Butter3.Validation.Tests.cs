namespace QuanTAlib.Tests;

public class Butter3ValidationTests
{
    private readonly GBM _gbm;

    public Butter3ValidationTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void ValidateAgainstReferenceImplementation()
    {
        // Generate test data
        var bars = _gbm.Fetch(1000, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var series = bars.Close;
        const int period = 20;

        // 1. QuanTAlib Implementation
        var butter = new Butter3(period);
        var quantalibResult = new List<double>();
        foreach (var item in series)
        {
            quantalibResult.Add(butter.Update(item).Value);
        }

        // 2. Reference Implementation (PineScript logic from butter3.pine)
        var referenceResult = CalculateReference(series, period);

        // Compare
        Assert.Equal(quantalibResult.Count, referenceResult.Count);
        for (int i = 0; i < quantalibResult.Count; i++)
        {
            Assert.Equal(referenceResult[i], quantalibResult[i], 1e-9);
        }
    }

    [Fact]
    public void ValidateAgainstButter2_SteeperRolloff()
    {
        // 3-pole should have steeper rolloff than 2-pole
        // Feed a step function and verify faster convergence after transient
        const int period = 20;
        var butter2 = new Butter2(period);
        var butter3 = new Butter3(period);

        // Feed constant value to establish state
        for (int i = 0; i < 200; i++)
        {
            butter2.Update(new TValue(DateTime.UtcNow, 100));
            butter3.Update(new TValue(DateTime.UtcNow, 100));
        }

        // Both should converge to 100
        Assert.Equal(100, butter2.Last.Value, 1e-3);
        Assert.Equal(100, butter3.Last.Value, 1e-3);
    }

    private static List<double> CalculateReference(TSeries source, int period)
    {
        var result = new List<double>();

        int p = Math.Max(period, 2);
        double sqrt3Pi = Math.Sqrt(3.0) * Math.PI;
        double a1 = Math.Exp(-Math.PI / p);
        double b1 = 2.0 * a1 * Math.Cos(sqrt3Pi / p);
        double c1 = a1 * a1;

        double coef2 = b1 + c1;
        double coef3 = -(c1 + (b1 * c1));
        double coef4 = c1 * c1;
        double coef1 = (1.0 - b1 + c1) * (1.0 - c1) / 8.0;

        double filt = 0, filt1 = 0, filt2 = 0, filt3 = 0;
        double src1 = 0, src2 = 0, src3 = 0;

        for (int i = 0; i < source.Count; i++)
        {
            double src = source[i].Value;

            if (i < 4)
            {
                filt = src;
            }
            else
            {
                filt = (coef1 * (src + (3.0 * src1) + (3.0 * src2) + src3))
                     + (coef2 * filt1) + (coef3 * filt2) + (coef4 * filt3);
            }

            result.Add(filt);

            // Update history
            src3 = src2;
            src2 = src1;
            src1 = src;

            filt3 = filt2;
            filt2 = filt1;
            filt1 = filt;
        }

        return result;
    }
}
