
namespace QuanTAlib.Tests;

public class MgdiTests
{
    [Fact]
    public void NaN_FirstValue_DoesNotInitializeToZero()
    {
        var mgdi = new Mgdi(14, 0.6);

        // First value is NaN
        var result = mgdi.Update(new TValue(DateTime.UtcNow, double.NaN));

        // Should be NaN, not 0.0
        Assert.True(double.IsNaN(result.Value), $"Expected NaN but got {result.Value}");
    }

    [Fact]
    public void NaN_Sequence_InitializesOnFirstValid()
    {
        var mgdi = new Mgdi(14, 0.6);

        // Sequence of NaNs
        mgdi.Update(new TValue(DateTime.UtcNow, double.NaN));
        mgdi.Update(new TValue(DateTime.UtcNow, double.NaN));

        // First valid value
        double firstValid = 100.0;
        var result = mgdi.Update(new TValue(DateTime.UtcNow, firstValid));

        Assert.Equal(firstValid, result.Value);
    }

    [Fact]
    public void Standard_Calculation()
    {
        var mgdi = new Mgdi(14, 0.6);
        mgdi.Update(new TValue(DateTime.UtcNow, 100.0));
        var result = mgdi.Update(new TValue(DateTime.UtcNow, 101.0));

        Assert.True(result.Value > 100.0);
        Assert.True(result.Value < 101.0);
    }

    [Fact]
    public void Calculate_InvalidK_ThrowsArgumentOutOfRangeException()
    {
        var source = new double[10];
        var output = new double[10];

        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Calculate(source, output, 14, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Calculate(source, output, 14, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Calculate(source, output, 14, double.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Calculate(source, output, 14, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mgdi.Calculate(source, output, 14, -1));
    }
}
