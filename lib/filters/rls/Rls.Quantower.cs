using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class RlsIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Filter Order (taps)", sortIndex: 1, 2, 64, 1, 0)]
    public int Order { get; set; } = 16;

    [InputParameter("Forgetting Factor (λ)", sortIndex: 2, 0.9, 1.0, 0.005, 3)]
    public double Lambda { get; set; } = 0.99;

    [IndicatorExtensions.DataSourceInput]
    public SourceType Source { get; set; } = SourceType.Close;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Rls _rls = null!;
    private readonly LineSeries _rlsSeries;
    private string _sourceName = null!;
    private Func<IHistoryItem, double> _priceSelector = null!;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => $"RLS {Order}:{Lambda:F3}:{_sourceName}";

    public RlsIndicator()
    {
        OnBackGround = true;
        SeparateWindow = false;
        Name = "RLS - Recursive Least Squares Adaptive Filter";
        Description = "Adaptive FIR filter with inverse correlation matrix for fast convergence";
        _rlsSeries = new LineSeries(name: $"RLS {Order}", color: Color.Orange, width: 2, style: LineStyle.Solid);
        AddLineSeries(_rlsSeries);
    }

    protected override void OnInit()
    {
        _priceSelector = Source.GetPriceSelector();
        _sourceName = Source.ToString();
        _rls = new Rls(Order, Lambda);
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();
        var item = HistoricalData[Count - 1, SeekOriginHistory.Begin];
        double value = _rls.Update(new TValue(item.TimeLeft.Ticks, _priceSelector(item)), isNew).Value;
        _rlsSeries.SetValue(value, _rls.IsHot, ShowColdValues);
    }
}
