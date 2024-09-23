using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class MinIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 50;

    [InputParameter("Decay to mean", sortIndex: 1, minimum: 0.00, maximum: 100.0, increment: 0.01, decimalPlaces: 2)]
    public double Decay { get; set; } = 0.1;

    private Min? mi;
    protected override AbstractBase QuanTAlib => mi!;
    public override string ShortName => $"MIN {Period} : {Decay:F2} : {SourceName}";
    public MinIndicator() : base()
    {
        Name = "MIN - Minimum value (with decay)";
    }

    protected override void InitIndicator()
    {
        mi = new Min(Period, Decay);
        MinHistoryDepths = mi.WarmupPeriod;
        Source = 3;
        base.InitIndicator();
    }
}