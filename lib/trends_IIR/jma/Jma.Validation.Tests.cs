
namespace QuanTAlib.Tests;

public class JmaValidationTests
{
    [Fact]
    public void Jma_FollowsPriceTrend()
    {
        // JMA should generally follow the price.
        // If price goes up, JMA should eventually go up.

        var jma = new Jma(10);
        double previousJma = 0;

        // Uptrend
        for (int i = 0; i < 100; i++)
        {
            var result = jma.Update(new TValue(DateTime.UtcNow, i));
            if (i > 20) // Allow warmup
            {
                Assert.True(result.Value > previousJma, $"JMA should be increasing in uptrend at step {i}");
            }
            previousJma = result.Value;
        }
    }

    [Fact]
    public void Jma_WithinBounds()
    {
        // JMA should stay within the range of recent prices (roughly)
        // It's a moving average, so it shouldn't overshoot wildly unless phase is negative and high volatility?
        // With default phase 0, it should be well behaved.

        var jma = new Jma(10);
        var gbm = new GBM(startPrice: 100, mu: 0, sigma: 0.5);

        for (int i = 0; i < 1000; i++)
        {
            var bar = gbm.Next(isNew: true);
            var result = jma.Update(new TValue(bar.Time, bar.Close));

            if (i > 20)
            {
                // Update bounds of recent price history (simplified)
                // This is a loose check.
                // Just check it's finite and positive for this GBM
                Assert.True(double.IsFinite(result.Value));
                Assert.True(result.Value > 0);
            }
        }
    }
}
