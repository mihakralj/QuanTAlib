using System.Runtime.CompilerServices;

namespace QuanTAlib;

/// <summary>
/// Beta Coefficient: Measures the volatility of an asset in relation to the overall market.
/// </summary>
/// <remarks>
/// Beta is calculated as the covariance of the asset's returns and the market's returns,
/// divided by the variance of the market's returns.
///
/// Formula:
/// Beta = Cov(Ra, Rm) / Var(Rm)
///
/// Where:
/// Ra = Return of Asset
/// Rm = Return of Market
///
/// This implementation uses the O(1) slope formula for linear regression of Ra vs Rm:
/// Beta = (N * Sum(Ra*Rm) - Sum(Ra) * Sum(Rm)) / (N * Sum(Rm^2) - Sum(Rm)^2)
/// </remarks>
[SkipLocalsInit]
public sealed class Beta : AbstractBase
{
    private readonly RingBuffer _returnsAsset;
    private readonly RingBuffer _returnsMarket;

    private double _prevAsset;
    private double _prevMarket;
    private double _p_prevAsset;
    private double _p_prevMarket;
    private bool _isInitialized;

    private double _sumRa;
    private double _sumRm;
    private double _sumRaRm;
    private double _sumRm2;

    private const double Epsilon = 1e-10;
    private int _updateCount;
    private const int ResyncInterval = 1000;

    public override bool IsHot => _returnsAsset.IsFull;

    public Beta(int period)
    {
        if (period < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be greater than or equal to 1.");
        }
        _returnsAsset = new RingBuffer(period);
        _returnsMarket = new RingBuffer(period);
        Name = $"Beta({period})";
        WarmupPeriod = period + 1; // Need 1 extra for first return
        _isInitialized = false;
    }

    /// <summary>
    /// Updates the Beta indicator with new asset and market prices.
    /// </summary>
    /// <param name="asset">The asset price (TValue).</param>
    /// <param name="market">The market price (TValue).</param>
    /// <param name="isNew">Whether this is a new bar.</param>
    /// <returns>The calculated Beta value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TValue Update(TValue asset, TValue market, bool isNew = true)
    {
        if (isNew)
        {
            if (!_isInitialized)
            {
                _prevAsset = asset.Value;
                _prevMarket = market.Value;
                _isInitialized = true;
                return new TValue(asset.Time, 0);
            }

            _p_prevAsset = _prevAsset;
            _p_prevMarket = _prevMarket;

            // Calculate returns
            double ra = (asset.Value - _prevAsset) / _prevAsset;
            double rm = (market.Value - _prevMarket) / _prevMarket;

            _prevAsset = asset.Value;
            _prevMarket = market.Value;

            // Update buffers and sums
            if (_returnsAsset.IsFull)
            {
                double oldRa = _returnsAsset.Oldest;
                double oldRm = _returnsMarket.Oldest;

                _sumRa -= oldRa;
                _sumRm -= oldRm;
                _sumRaRm -= oldRa * oldRm;
                _sumRm2 -= oldRm * oldRm;
            }

            _returnsAsset.Add(ra);
            _returnsMarket.Add(rm);

            _sumRa += ra;
            _sumRm += rm;
            _sumRaRm += ra * rm;
            _sumRm2 += rm * rm;

            _updateCount++;
            if (_updateCount % ResyncInterval == 0)
            {
                Resync();
            }
        }
        else
        {
            if (!_isInitialized)
            {
                _prevAsset = asset.Value;
                _prevMarket = market.Value;
                _isInitialized = true;
                return new TValue(asset.Time, 0);
            }

            if (_returnsAsset.Count == 0)
            {
                _prevAsset = asset.Value;
                _prevMarket = market.Value;
                return new TValue(asset.Time, 0);
            }

            double oldRa = _returnsAsset.Newest;
            double oldRm = _returnsMarket.Newest;

            double newRa = (asset.Value - _p_prevAsset) / _p_prevAsset;
            double newRm = (market.Value - _p_prevMarket) / _p_prevMarket;

            _prevAsset = asset.Value;
            _prevMarket = market.Value;

            _returnsAsset.UpdateNewest(newRa);
            _returnsMarket.UpdateNewest(newRm);

            _sumRa = _sumRa - oldRa + newRa;
            _sumRm = _sumRm - oldRm + newRm;
            _sumRaRm = _sumRaRm - (oldRa * oldRm) + (newRa * newRm);
            _sumRm2 = _sumRm2 - (oldRm * oldRm) + (newRm * newRm);
        }

        double beta = 0;
        int n = _returnsAsset.Count;
        if (n > 0)
        {
            double denominator = n * _sumRm2 - _sumRm * _sumRm;
            if (Math.Abs(denominator) > Epsilon)
            {
                beta = (n * _sumRaRm - _sumRa * _sumRm) / denominator;
            }
        }

        Last = new TValue(asset.Time, beta);
        PubEvent(Last);
        return Last;
    }

    public TValue Update(double asset, double market, bool isNew = true)
    {
        return Update(new TValue(DateTime.UtcNow, asset), new TValue(DateTime.UtcNow, market), isNew);
    }

    public override TValue Update(TValue input, bool isNew = true)
    {
        throw new NotSupportedException("Beta requires two inputs (asset and market). Use Update(asset, market).");
    }

    public override TSeries Update(TSeries source)
    {
        throw new NotSupportedException("Beta requires two inputs (asset and market). Use Update(asset, market).");
    }

    public override void Prime(ReadOnlySpan<double> source, TimeSpan? step = null)
    {
        throw new NotSupportedException("Beta requires two inputs (asset and market). Use Update(asset, market).");
    }

    public override void Reset()
    {
        _returnsAsset.Clear();
        _returnsMarket.Clear();
        _sumRa = 0;
        _sumRm = 0;
        _sumRaRm = 0;
        _sumRm2 = 0;
        _isInitialized = false;
        _prevAsset = 0;
        _prevMarket = 0;
        _p_prevAsset = 0;
        _p_prevMarket = 0;
        _updateCount = 0;
    }

    private void Resync()
    {
        _sumRa = 0;
        _sumRm = 0;
        _sumRaRm = 0;
        _sumRm2 = 0;

        for (int i = 0; i < _returnsAsset.Count; i++)
        {
            double ra = _returnsAsset[i];
            double rm = _returnsMarket[i];

            _sumRa += ra;
            _sumRm += rm;
            _sumRaRm += ra * rm;
            _sumRm2 += rm * rm;
        }
    }
}
