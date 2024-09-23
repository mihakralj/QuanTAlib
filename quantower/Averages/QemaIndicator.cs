using TradingPlatform.BusinessLayer;
using QuanTAlib;

public class QemaIndicator : IndicatorBase
{
    [InputParameter("alpha 1", sortIndex: 1, minimum: 0.01, maximum: 1.0, increment: 0.01, decimalPlaces: 2)]
    public double k1 { get; set; } = 0.2;

    [InputParameter("alpha 2", sortIndex: 2, minimum: 0.01, maximum: 1.0, increment: 0.01, decimalPlaces: 2)]
    public double k2 { get; set; } = 0.3;
        [InputParameter("alpha 3", sortIndex: 3, minimum: 0.01, maximum: 1.0, increment: 0.01, decimalPlaces: 2)]
    public double k3 { get; set; } = 0.4;
        [InputParameter("alpha 4", sortIndex: 4, minimum: 0.01, maximum: 1.0, increment: 0.01, decimalPlaces: 2)]
    public double k4 { get; set; } = 0.5;
    private Qema? ma;
    protected override AbstractBase QuanTAlib => ma!;
    public override string ShortName => $"QEMA {k1:F2} : {k2:F2} : {k3:F2} : {k4:F2} :{SourceName}";

    public QemaIndicator() : base()
    {
        Name = "QEMA - Quad Exponential Moving Average";
        Description = "Quad Exponential Moving Average";
    }

    protected override void InitIndicator()
    {
        base.InitIndicator();
        ma = new Qema(k1, k2, k3, k4);
    }
}
