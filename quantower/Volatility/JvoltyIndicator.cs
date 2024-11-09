using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class JvoltyIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 1, 1, 2000, 1, 0)]
    public int Period { get; set; } = 14;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    private Jma? jma;
    protected LineSeries? JvoltySeries;
    public static int MinHistoryDepths => 2;


    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public JvoltyIndicator()
    {
        Name = "JVOLTY - Mark Jurik's Volatility";
        Description = "Measures market volatility according to Mark Jurik.";
        SeparateWindow = true;

        JvoltySeries = new("JVOLTY", color: IndicatorExtensions.Volatility, 2, LineStyle.Solid);
        AddLineSeries(JvoltySeries);
    }

    protected override void OnInit()
    {
        jma = new(Period);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TValue input = this.GetInputValue(args, Source);
        jma!.Calc(input);

        JvoltySeries!.SetValue(jma.Volty);
    }

    public override string ShortName => $"JVOLTY ({Period})";
}
