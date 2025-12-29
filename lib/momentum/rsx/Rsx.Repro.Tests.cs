
namespace QuanTAlib.Tests;

public class RsxReproTests
{
        [Fact]
        public void LastValidValue_ShouldNotUpdate_WhenIsNewIsFalse()
        {
            // Arrange
            var rsx = new Rsx(2);

            // Warmup to ensure initialization
            rsx.Update(new TValue(DateTime.UtcNow, 100), true);
            rsx.Update(new TValue(DateTime.UtcNow, 100), true);
            rsx.Update(new TValue(DateTime.UtcNow, 100), true);

            // Update: Valid value, isNew=false (Transient update)
            // This should NOT persist 200 as LastValidValue for the next bar.
            rsx.Update(new TValue(DateTime.UtcNow, 200), false);

            // Update: NaN value, isNew=true
            // Should use LastValidValue.
            // If bug exists: uses 200. Momentum = 200 - 100 = 100.
            // If fixed: uses 100. Momentum = 100 - 100 = 0.
            var res = rsx.Update(new TValue(DateTime.UtcNow, double.NaN), true);

            // If momentum was 0, RSX should be 50.
            // If momentum was 100, RSX should be > 50.

            Assert.Equal(50.0, res.Value, 1e-6);
    }
}
