using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class MaxIndicator : IndicatorBase
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 50;

    [InputParameter("Decay to mean", sortIndex: 1, minimum: 0.00, maximum: 100.0, increment: 0.01, decimalPlaces: 2)]
    public double Decay { get; set; } = 0.1;

    private Max? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"MAX {Period} : {Decay:F2} : {SourceName}";

    public MaxIndicator() : base()
    {
        Name = "MAX - Maximum value (with decay) ";
    }

    protected override void InitIndicator()
    {
        ma = new Max(Period, Decay);
        MinHistoryDepths = ma.WarmupPeriod;
        Source = 2;
        base.InitIndicator();
    }
}
