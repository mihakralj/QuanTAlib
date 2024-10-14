using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class JvoltyIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Periods", sortIndex: 1, 1, 2000, 1, 0)]
    public int Periods { get; set; } = 20;

    private Jvolty? jvolty;
    protected LineSeries? JvoltySeries;
    public static int MinHistoryDepths => 2;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public JvoltyIndicator()
    {
        Name = "JVOLTY - Mark Jurik's Volatility";
        Description = "Measures market volatility according to Mark Jurik.";
        SeparateWindow = true;

        JvoltySeries = new("JVOLTY", Color.Blue, 2, LineStyle.Solid);
        AddLineSeries(JvoltySeries);
    }

    protected override void OnInit()
    {
        jvolty = new (Periods);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        TBar input = IndicatorExtensions.GetInputBar(this, args);
        TValue result = jvolty!.Calc(input);

        JvoltySeries!.SetValue(result.Value);
    }

    public override string ShortName => $"JVOLTY ({Periods})";
}
