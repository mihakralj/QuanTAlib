
namespace QuanTAlib.Tests;

public class CovarianceSimdTests
{
    [Fact]
    public void Covariance_Simd_Matches_Scalar_LargeDataset()
    {
        // Arrange
        const int count = 1000; // > 256 to trigger SIMD
        int period = 20;
        var gbmX = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 42);
        var gbmY = new GBM(startPrice: 100, mu: 0.05, sigma: 0.2, seed: 123);
        var dataX = new double[count];
        var dataY = new double[count];
        for (int i = 0; i < count; i++)
        {
            dataX[i] = gbmX.Next().Close;
            dataY[i] = gbmY.Next().Close;
        }

        var sourceX = new TSeries();
        sourceX.Add(dataX);
        var sourceY = new TSeries();
        sourceY.Add(dataY);

        // Act
        // This will use SIMD if available and length >= 256
        var simdResult = Covariance.Batch(sourceX, sourceY, period);

        // Calculate expected using scalar loop (simulating by using small chunks or manual calc,
        // but easier to just use the streaming update which is scalar)
        var scalarCov = new Covariance(period);
        var expectedValues = new double[count];
        for (int i = 0; i < count; i++)
        {
            var res = scalarCov.Update(dataX[i], dataY[i]);
            expectedValues[i] = res.Value;
        }

        // Assert
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expectedValues[i], simdResult.Values[i], precision: 7);
        }
    }

    [Fact]
    public void Covariance_Simd_Handles_NaN_Correctly()
    {
        // Arrange
        int count = 500;
        int period = 50;
        var dataX = Enumerable.Range(0, count).Select(x => (double)x).ToArray();
        var dataY = Enumerable.Range(0, count).Select(x => (double)x * 2).ToArray();

        // Inject NaN
        dataX[300] = double.NaN;
        dataY[350] = double.NaN;

        var sourceX = new TSeries();
        sourceX.Add(dataX);
        var sourceY = new TSeries();
        sourceY.Add(dataY);

        // Act
        // The implementation checks for ContainsNonFinite() before using SIMD.
        // If NaN is present, it should fall back to Scalar.
        // We want to verify that the result is correct regardless of the path taken.
        var result = Covariance.Batch(sourceX, sourceY, period);

        // Assert
        // Verify around the NaN values
        // Index 300 has NaN in X. Covariance should handle it (likely treat as 0 or propagate last valid if logic dictates,
        // but current implementation replaces non-finite with 0 in scalar core).

        // Let's verify against streaming which we know uses scalar logic
        // BUT: Batch implementation replaces NaN with 0, while Streaming propagates NaN.
        // To compare, we must feed 0 instead of NaN to streaming.
        var scalarCov = new Covariance(period);
        for (int i = 0; i < count; i++)
        {
            double x = dataX[i];
            double y = dataY[i];
            if (!double.IsFinite(x))
            {
                x = 0;
            }

            if (!double.IsFinite(y))
            {
                y = 0;
            }

            var res = scalarCov.Update(x, y);
            Assert.Equal(res.Value, result.Values[i], precision: 9);
        }
    }

    [Fact]
    public void Covariance_Simd_Resync_Check()
    {
        // Arrange
        // Create a dataset large enough to trigger resync in SIMD loop (ResyncInterval = 1000)
        // We need > 1000 elements processed in the SIMD loop.
        // The SIMD loop starts at 'period' and goes up to 'simdEnd'.
        // So we need length > period + 1000.
        int period = 10;
        int count = 2000;

        // Use simple linear data to make verification easy
        // y = 2x
        var dataX = Enumerable.Range(0, count).Select(x => (double)x).ToArray();
        var dataY = Enumerable.Range(0, count).Select(x => (double)x * 2).ToArray();

        var sourceX = new TSeries();
        sourceX.Add(dataX);
        var sourceY = new TSeries();
        sourceY.Add(dataY);

        // Act
        var result = Covariance.Batch(sourceX, sourceY, period);

        // Assert
        // For y=2x, Cov(X,Y) = 2*Var(X)
        // Var(X) of sequence 0,1,2... is constant for fixed period?
        // For period 10: 0..9. Variance is constant.
        // Var(0..9) = 9.16666... (Population) or 10.185... (Sample)?
        // Let's just compare with scalar truth.

        var scalarCov = new Covariance(period);
        for (int i = 0; i < count; i++)
        {
            var res = scalarCov.Update(dataX[i], dataY[i]);
            Assert.Equal(res.Value, result.Values[i], precision: 9);
        }
    }
}
