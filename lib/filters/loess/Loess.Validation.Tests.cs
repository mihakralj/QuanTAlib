using Xunit;
using QuanTAlib.Tests;

namespace QuanTAlib;

public class LoessValidationTests : IDisposable
{
    private readonly ValidationTestData _testData;
    private bool _disposed;

    public LoessValidationTests()
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
    public void Validate_Against_Linear_Trend()
    {
        // Loess is a local linear regression.
        // If we feed it a perfect line, it should produce a perfect line (except maybe at edges if window is partial).
        // Our implementation handles partial windows by doing partial convolution, so it might deviate at start.

        var loess = new Loess(10);

        // Generate a line y = x
        var input = new List<double>();
        var expected = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            input.Add(i * 1.0);
            expected.Add(i * 1.0);
        }

        var actual = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            actual.Add(loess.Update(new TValue(DateTime.UtcNow, input[i])).Value);
        }

        // Check after warmup
        for (int i = 10; i < 50; i++)
        {
             Assert.Equal(expected[i], actual[i], 1e-6);
        }
    }
}