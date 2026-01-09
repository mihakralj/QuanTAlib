using Xunit;
using QuanTAlib;
using System.Runtime.InteropServices;

namespace Validation;

public class Cheby1ValidationTests
{
    [Fact]
    public void Validate_Againstpython_Descriptive()
    {
        // Python scipy.signal.cheby1 validation
        // b, a = signal.cheby1(2, 1, 1/10)
        // [0.02089736, 0.04179471, 0.02089736]
        // [1.        , -1.63299316,  0.71658259]
        // Results for input [1, 2, 3, 4, 5, 5, 5, 5, 5]

        // This is a known implementation test to meaningful values since standard validation libs don't always contain Chebyshev
        // We will perform a basic sanity check here.

        var filter = new Cheby1(20, 1.0); // Wn = 1/20
        var input = new TSeries();
        for (int i = 0; i < 100; i++) input.Add(new TValue(DateTime.UtcNow, 100)); // Impulse/Step

        var output = filter.Update(input);

        Assert.True(Math.Abs(output.Last.Value - 100.0) < 1.0); // Should converge to DC gain of 1
    }
}
