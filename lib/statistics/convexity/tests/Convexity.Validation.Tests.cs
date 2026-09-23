using Skender.Stock.Indicators;

namespace QuanTAlib.Tests;

/// <summary>
/// Validation tests for Convexity (Beta Convexity) against Skender's GetBeta(BetaType.All).
/// </summary>
public sealed class ConvexityValidationTests : IDisposable
{
    private readonly ValidationTestData _data;

    public ConvexityValidationTests()
    {
        _data = new ValidationTestData();
    }

    public void Dispose()
    {
        _data.Dispose();
    }

    [Fact]
    public void Validate_Against_Skender()
    {
        var marketQuotes = _data.Data;

        // Asset correlated to market with asymmetric up/down beta so convexity is non-trivial.
        var assetQuotes = new List<TBar>();
        double assetPrice = 100;
        const double betaUpTarget = 1.8;
        const double betaDownTarget = 1.2;

        var noiseGbm = new GBM(startPrice: 100, mu: 0, sigma: 0.2, seed: 999);

        assetQuotes.Add(new TBar(marketQuotes[0].Time, assetPrice, assetPrice, assetPrice, assetPrice, 1000));

        for (int i = 1; i < marketQuotes.Count; i++)
        {
            double marketReturn = (marketQuotes[i].Value - marketQuotes[i - 1].Value) / marketQuotes[i - 1].Value;

            var noiseBar = noiseGbm.Next();
            double noise = (noiseBar.Close - noiseBar.Open) / noiseBar.Open;

            double targetBeta = marketReturn > 0 ? betaUpTarget : betaDownTarget;
            double assetReturn = (targetBeta * marketReturn) + noise;

            assetPrice *= 1 + assetReturn;
            assetQuotes.Add(new TBar(marketQuotes[i].Time, assetPrice, assetPrice, assetPrice, assetPrice, 1000));
        }

        var skenderMarket = marketQuotes.Select(x => new Quote { Date = x.AsDateTime, Close = (decimal)x.Value }).ToList();
        var skenderAsset = assetQuotes.Select(x => new Quote { Date = x.AsDateTime, Close = (decimal)x.Close }).ToList();

        const int period = 20;
        var skenderBeta = skenderAsset.GetBeta(skenderMarket, period, BetaType.All).ToList();

        var convexity = new Convexity(period);
        var qlBetaUp = new List<double>();
        var qlBetaDown = new List<double>();
        var qlRatio = new List<double>();
        var qlConvexity = new List<double>();

        for (int i = 0; i < marketQuotes.Count; i++)
        {
            convexity.Update(assetQuotes[i].Close, marketQuotes[i].Value);
            qlBetaUp.Add(convexity.BetaUp);
            qlBetaDown.Add(convexity.BetaDown);
            qlRatio.Add(convexity.Ratio);
            qlConvexity.Add(convexity.ConvexityValue);
        }

        int count = qlConvexity.Count;
        int skip = period + 5;
        int compared = 0;

        for (int i = skip; i < count; i++)
        {
            var sk = skenderBeta[i];
            if (sk.BetaUp.HasValue && sk.BetaDown.HasValue && sk.Convexity.HasValue)
            {
                Assert.Equal(sk.BetaUp.Value, qlBetaUp[i], ValidationHelper.DefaultTolerance);
                Assert.Equal(sk.BetaDown.Value, qlBetaDown[i], ValidationHelper.DefaultTolerance);
                Assert.Equal(sk.Convexity.Value, qlConvexity[i], ValidationHelper.DefaultTolerance);

                if (sk.Ratio.HasValue && double.IsFinite(sk.Ratio.Value) && double.IsFinite(qlRatio[i]))
                {
                    Assert.Equal(sk.Ratio.Value, qlRatio[i], ValidationHelper.DefaultTolerance);
                }

                compared++;
            }
        }

        Assert.True(compared > 100, $"Only {compared} values compared against Skender");
    }

    [Fact]
    public void ConvexityValue_MatchesBetaUpMinusBetaDownSquared()
    {
        var marketQuotes = _data.Data;
        var convexity = new Convexity(20);

        for (int i = 0; i < marketQuotes.Count; i++)
        {
            double asset = marketQuotes[i].Value * 1.01; // simple correlated proxy
            convexity.Update(asset, marketQuotes[i].Value);
        }

        double expected = (convexity.BetaUp - convexity.BetaDown) * (convexity.BetaUp - convexity.BetaDown);
        Assert.Equal(expected, convexity.ConvexityValue, ValidationHelper.DefaultTolerance);
    }
}
