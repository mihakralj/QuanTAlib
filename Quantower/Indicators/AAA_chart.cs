using System.Diagnostics;
using System.Drawing;
using System.Linq;
using TradingPlatform.BusinessLayer;
namespace QuanTAlib;

public class AAA_chart : Indicator {
    #region Parameters

    [InputParameter("Smoothing period", 0, 1, 999, 1, 1)]
    private readonly int Period = 10;

    #endregion Parameters

    private TBars bars;
    private JMA_Series ind_a;
	private DWMA_Series ind_b;

	public override string ShortName => $"AAA ({this.Period})";

	public AAA_chart() : base()
    {
        this.SeparateWindow = false;

        this.Name = "AAA - Test indicator";
        this.Description = "Test indicator";

		this.AddLineSeries("JMA", Color.RoyalBlue, 3, LineStyle.Solid);
		this.AddLineSeries("DWMA", Color.OrangeRed, 3, LineStyle.Solid);

		this.SeparateWindow = false;
	}

    protected override void OnInit()
    {
        this.bars = new();

        this.ind_a = new(source: bars.Close, period: this.Period, useNaN: false);
		this.ind_b = new(source: bars.OHLC4, period: this.Period, useNaN: false);
	}

	protected override void OnUpdate(UpdateArgs args)
    {
		bool update = !(args.Reason == UpdateReason.NewBar || args.Reason == UpdateReason.HistoricalBar);

        this.bars.Add(this.Time(),
            this.GetPrice(PriceType.Open),
            this.GetPrice(PriceType.High),
            this.GetPrice(PriceType.Low),
            this.GetPrice(PriceType.Close),
            this.GetPrice(PriceType.Volume),
            update);

		this.SetValue(this.ind_a.v.Last(), 0);
		this.SetValue(this.ind_b.v.Last(), 1);
	}
}
