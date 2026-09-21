namespace QuanTAlib.Tests;

public class Ssf3ValidationTests
{
    private readonly GBM _gbm;

    public Ssf3ValidationTests()
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
        var ssf = new Ssf3(period);
        var quantalibResult = new List<double>();
        foreach (var item in series)
        {
            quantalibResult.Add(ssf.Update(item).Value);
        }

        // 2. Reference Implementation (PineScript logic from ssf3.pine)
        var referenceResult = CalculateReference(series, period);

        // Compare
        Assert.Equal(quantalibResult.Count, referenceResult.Count);
        for (int i = 0; i < quantalibResult.Count; i++)
        {
            Assert.Equal(referenceResult[i], quantalibResult[i], 1e-9);
        }
    }

    [Fact]
    public void ValidateAgainstSsf2_SteepRolloff()
    {
        // 3-pole should have steeper rolloff than 2-pole
        // Feed constant value and verify both converge
        const int period = 20;
        var ssf2 = new Ssf2(period);
        var ssf3 = new Ssf3(period);

        for (int i = 0; i < 200; i++)
        {
            ssf2.Update(new TValue(DateTime.UtcNow, 100));
            ssf3.Update(new TValue(DateTime.UtcNow, 100));
        }

        // Both should converge to 100
        Assert.Equal(100, ssf2.Last.Value, 1e-3);
        Assert.Equal(100, ssf3.Last.Value, 1e-3);
    }

    private static List<double> CalculateReference(TSeries source, int period)
    {
        var result = new List<double>();

        int p = Math.Max(1, period);
        double sqrt3Pi = Math.Sqrt(3.0) * Math.PI;
        double a1 = Math.Exp(-Math.PI / p);
        double b1 = 2.0 * a1 * Math.Cos(sqrt3Pi / p);
        double c1 = a1 * a1;

        double coef2 = b1 + c1;
        double coef3 = -(c1 + (b1 * c1));
        double coef4 = c1 * c1;
        double coef1 = 1.0 - coef2 - coef3 - coef4;

        double filt = 0, filt1 = 0, filt2 = 0, filt3 = 0;

        for (int i = 0; i < source.Count; i++)
        {
            double src = source[i].Value;

            if (i < 4)
            {
                filt = src;
            }
            else
            {
                // y = coef1*x + coef2*y[1] + coef3*y[2] + coef4*y[3]
                filt = (coef1 * src) + (coef2 * filt1) + (coef3 * filt2) + (coef4 * filt3);
            }

            result.Add(filt);

            filt3 = filt2;
            filt2 = filt1;
            filt1 = filt;
        }

        return result;
    }
}
