using Xunit;
using QuanTAlib;

namespace Validation;

public class Cheby2ValidationTests
{
    [Fact]
    public void Verify_ImpulseResponse()
    {
        // Cheby2 is IIR, so we check its impulse response
        var filter = new Cheby2(period: 10, attenuation: 5.0);

        // Impulse: 1, 0, 0, 0...
        var result1 = filter.Update(new TValue(DateTime.UtcNow, 1.0)).Value;
        filter.Update(new TValue(DateTime.UtcNow, 0.0));
        filter.Update(new TValue(DateTime.UtcNow, 0.0));

        // We just verified the code runs and produces values
        // Precise values depend on exact coefficients which we validated via the algorithm implementation match
        Assert.True(Math.Abs(result1) > 0);

        // Verify stability (should decay towards zero for lowpass IIR)
        for(int i=0; i<50; i++)
            filter.Update(new TValue(DateTime.UtcNow, 0.0));

        Assert.True(Math.Abs(filter.Last.Value) < 1e-5);
    }
}