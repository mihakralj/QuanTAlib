using QuanTAlib.Tests;

namespace QuanTAlib;

public class RsxValidationTests
{
    private readonly GBM _gbm;

    public RsxValidationTests()
    {
        _gbm = new GBM();
    }

    [Fact]
    public void Validate_Against_Reference_Implementation()
    {
        // Generate data
        int count = 1000;
        int period = 14;
        var bars = _gbm.Fetch(count, DateTime.UtcNow.Ticks, TimeSpan.FromMinutes(1));
        var prices = bars.Close.Values;

        // QuanTAlib implementation
        var rsx = new Rsx(period);
        var quantalibResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            quantalibResults[i] = rsx.Update(new TValue(DateTime.UtcNow, prices[i])).Value;
        }

        // Reference implementation (from user prompt)
        var refRsx = new ReferenceRsx(period);
        var refResults = new double[count];
        for (int i = 0; i < count; i++)
        {
            refResults[i] = refRsx.Add(prices[i]);
        }

        // Compare
        for (int i = 0; i < count; i++)
        {
            // Allow small difference due to floating point arithmetic order
            Assert.Equal(refResults[i], quantalibResults[i], ValidationHelper.DefaultTolerance);
        }
    }

    // Reference implementation provided in the task description
    private class ReferenceRsx
    {
        private readonly double alpha, ialpha;
        // Internal state variables for filter registers:
        private double f28, f30, f38, f40, f48, f50;
        private double f58, f60, f68, f70, f78, f80;

        // Added state for f10 logic
        private double lastF8;
        private bool initialized;

        public double Current { get; private set; }

        public ReferenceRsx(int length)
        {
            // Initialize constants:
            this.alpha = 3.0 / (length + 2.0);
            this.ialpha = 1.0 - this.alpha;
            // Initialize filters to 0:
            f28 = f30 = f38 = f40 = f48 = f50 = 0.0;
            f58 = f60 = f68 = f70 = f78 = f80 = 0.0;
            this.Current = 50.0;  // neutral start
            this.initialized = false;
        }

        public double Add(double price)
        {
            // Core RSX calculations (assuming price input as closing price):
            double f8 = 100 * price;


            if (!initialized)
            {
                lastF8 = f8;
                initialized = true;
            }

            double v8 = f8 - lastF8;
            lastF8 = f8;

            // First smoothing stage:
            f28 = ialpha * f28 + alpha * v8;
            f30 = alpha * f28 + ialpha * f30;
            double vC = 1.5 * f28 - 0.5 * f30;
            // Second smoothing stage:
            f38 = ialpha * f38 + alpha * vC;
            f40 = alpha * f38 + ialpha * f40;
            double v10 = 1.5 * f38 - 0.5 * f40;
            // Third smoothing stage:
            f48 = ialpha * f48 + alpha * v10;
            f50 = alpha * f48 + ialpha * f50;
            double v14 = 1.5 * f48 - 0.5 * f50;
            // Repeat stages for absolute value (momentum magnitude):
            f58 = ialpha * f58 + alpha * Math.Abs(v8);
            f60 = alpha * f58 + ialpha * f60;
            double v18 = 1.5 * f58 - 0.5 * f60;
            f68 = ialpha * f68 + alpha * v18;
            f70 = alpha * f68 + ialpha * f70;
            double v1C = 1.5 * f68 - 0.5 * f70;
            f78 = ialpha * f78 + alpha * v1C;
            f80 = alpha * f78 + ialpha * f80;
            double v20 = 1.5 * f78 - 0.5 * f80;
            // Final RSX value:
            double rsx;
            if (v20 > 1e-10) // Avoid division by zero
            {
                double v4 = (v14 / v20 + 1.0) * 50.0;
                rsx = Math.Clamp(v4, 0.0, 100.0);
            }
            else
            {
                rsx = 50.0;
            }
            this.Current = rsx;
            return rsx;
        }
    }
}
