using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class FiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Period", sortIndex: 10, 1, 500, 1, 0)]
    public int Period { get; set; } = 13;

    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Fi _fi = null!;
    private double _prevClose = double.NaN;
    private double _pPrevClose = double.NaN;
    private readonly LineSeries _series;

    public int MinHistoryDepths => Period;
    int IWatchlistIndicator.MinHistoryDepths => Period;

    public override string ShortName => $"FI({Period})";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/fi/Fi.Quantower.cs";

    public FiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "FI - Force Index";
        Description = "Force Index measures buying and selling pressure as EMA-smoothed price change × volume";

        _series = new LineSeries(name: "FI", color: Color.Yellow, width: 2, style: LineStyle.Solid);
        AddLineSeries(_series);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _fi = new Fi(Period);
        _prevClose = double.NaN;
        _pPrevClose = double.NaN;
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        bool isNew = args.IsNewBar();

        if (isNew)
        {
            _pPrevClose = _prevClose;
        }
        else
        {
            _prevClose = _pPrevClose;
        }

        TBar bar = this.GetInputBar(args);
        double close = bar.Close;
        double volume = bar.Volume;

        double rawForce;
        if (double.IsNaN(_prevClose))
        {
            rawForce = 0;
        }
        else
        {
            rawForce = (close - _prevClose) * volume;
        }

        if (isNew)
        {
            _prevClose = close;
        }

        TValue input = new(bar.Time, rawForce);
        TValue result = _fi.Update(input, isNew);

        _series.SetValue(result.Value, _fi.IsHot, ShowColdValues);
    }
}
