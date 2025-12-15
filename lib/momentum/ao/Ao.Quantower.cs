using System.Drawing;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

public class AoIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Fast Period", sortIndex: 1, 1, 1000, 1, 0)]
    public int FastPeriod { get; set; } = 5;

    [InputParameter("Slow Period", sortIndex: 2, 1, 1000, 1, 0)]
    public int SlowPeriod { get; set; } = 34;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Ao? _ao;
    protected LineSeries? UpSeries;
    protected LineSeries? DownSeries;

    public int MinHistoryDepths => SlowPeriod;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"AO {FastPeriod}:{SlowPeriod}";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/momentum/ao/Ao.Quantower.cs";

    public AoIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "AO - Awesome Oscillator";
        Description = "Momentum indicator measuring market momentum";

        UpSeries = new(name: "AO Up", color: Color.Green, width: 2, style: LineStyle.Solid);
        DownSeries = new(name: "AO Down", color: Color.Red, width: 2, style: LineStyle.Solid);

        AddLineSeries(UpSeries);
        AddLineSeries(DownSeries);
    }

    protected override void OnInit()
    {
        _ao = new Ao(FastPeriod, SlowPeriod);
        base.OnInit();
    }

    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar;

        TBar bar = this.GetInputBar(args);
        TValue result = _ao!.Update(bar, isNew);

        if (!_ao.IsHot && !ShowColdValues)
        {
            return;
        }

        // Determine color based on momentum
        // Green if rising, Red if falling
        // We need previous value to compare.
        // Since OnUpdate is called multiple times for the same bar (ticks),
        // we need to be careful about "previous value".
        // Ideally, we compare with the value of the *previous bar*.
        // But AO coloring is usually: Current > Previous Bar's AO => Green.
        // Or Current > Previous Value (intra-bar)?
        // Standard is: "Green bar if the bar is higher than the previous bar. Red bar if the bar is lower than the previous bar."
        // "Previous bar" usually means the AO value of the previous period.

        // We can get the previous value from the indicator history if we stored it,
        // or just use _ao.Last (which is current) and we need the previous one.
        // But _ao doesn't expose history directly unless we use TSeries.
        // However, Quantower stores history in the Series.
        
        // Get previous value from series
        double prevAo = double.NaN;
        if (Count > 1)
        {
            // Try to get from UpSeries
            prevAo = UpSeries!.GetValue(1);
            if (double.IsNaN(prevAo))
            {
                prevAo = DownSeries!.GetValue(1);
            }
        }

        // If first bar, just pick a color (e.g. Green) or NaN
        if (double.IsNaN(prevAo) || result.Value > prevAo)
        {
            UpSeries!.SetValue(result.Value);
            DownSeries!.SetValue(double.NaN);
        }
        else
        {
            UpSeries!.SetValue(double.NaN);
            DownSeries!.SetValue(result.Value);
        }
    }
}
