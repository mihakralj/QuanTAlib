using System.Drawing;
using System.Runtime.CompilerServices;
using TradingPlatform.BusinessLayer;

namespace QuanTAlib;

[SkipLocalsInit]
public sealed class MarketfiIndicator : Indicator, IWatchlistIndicator
{
    [InputParameter("Show cold values", sortIndex: 21)]
    public bool ShowColdValues { get; set; } = true;

    private Marketfi _marketfi = null!;
    private readonly LineSeries _mfiLine;

    public static int MinHistoryDepths => 0;
    int IWatchlistIndicator.MinHistoryDepths => MinHistoryDepths;

    public override string ShortName => "MARKETFI";
    public override string SourceCodeLink => "https://github.com/mihakralj/QuanTAlib/blob/main/lib/oscillators/marketfi/Marketfi.Quantower.cs";

    public MarketfiIndicator()
    {
        OnBackGround = true;
        SeparateWindow = true;
        Name = "MARKETFI - Market Facilitation Index";
        Description = "Bill Williams' efficiency measure: price movement per unit of volume. High MFI with rising volume signals trend continuation.";

        _mfiLine = new LineSeries("MARKETFI", Color.Cyan, 2, LineStyle.Solid);
        AddLineSeries(_mfiLine);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnInit()
    {
        _marketfi = new Marketfi();
        base.OnInit();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void OnUpdate(UpdateArgs args)
    {
        _ = _marketfi.Update(this.GetInputBar(args), args.IsNewBar());

        _mfiLine.SetValue(_marketfi.Last.Value, _marketfi.IsHot, ShowColdValues);
    }
}
