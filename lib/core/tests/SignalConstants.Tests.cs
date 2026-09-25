namespace QuanTAlib.Tests;

public class SignalConstantsTests
{
    [Fact]
    public void DefaultEpsilon_IsNotMachineEpsilon()
    {
        // The spec is explicit that the default epsilon must not be double.Epsilon, which is a
        // machine-level spacing value unsuitable as a financial comparison tolerance.
        Assert.NotEqual(double.Epsilon, SignalConstants.DefaultEpsilon);
        Assert.Equal(1e-10, SignalConstants.DefaultEpsilon);
    }

    [Fact]
    public void DefaultEpsilon_IsPositiveAndFinite()
    {
        Assert.True(SignalConstants.DefaultEpsilon > 0.0);
        Assert.True(double.IsFinite(SignalConstants.DefaultEpsilon));
    }

    [Fact]
    public void TruthThreshold_IsOneHalf()
    {
        Assert.Equal(0.5, SignalConstants.TruthThreshold);
    }

    [Fact]
    public void TruthThreshold_IsWithinUnitInterval()
    {
        Assert.True(SignalConstants.TruthThreshold > 0.0);
        Assert.True(SignalConstants.TruthThreshold < 1.0);
    }

    [Fact]
    public void OutOfRangePolicy_ColdIsDefault()
    {
        Assert.Equal(default, OutOfRangePolicy.Cold);
    }

    [Fact]
    public void OutOfRangePolicy_HasExactlyTwoValues()
    {
        var values = Enum.GetValues<OutOfRangePolicy>();
        Assert.Equal(2, values.Length);
        Assert.Contains(OutOfRangePolicy.Cold, values);
        Assert.Contains(OutOfRangePolicy.Saturate, values);
    }
}
