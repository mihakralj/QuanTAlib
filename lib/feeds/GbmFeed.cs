using System.Security.Cryptography;

namespace QuanTAlib;

public class GbmFeed : TBarSeries
{
    private readonly double _mu, _sigma;
    private readonly RandomNumberGenerator _rng;
    private double _lastClose, _lastHigh, _lastLow;

    public GbmFeed(double initialPrice = 100.0, double mu = 0.05, double sigma = 0.2)
    {
        _lastClose = _lastHigh = _lastLow = initialPrice;
        _mu = mu;
        _sigma = sigma;
        _rng = RandomNumberGenerator.Create();
        this.Name = $"GBM({_sigma:F2})";
    }

    public void Add(bool isNew = true) => Add(time: DateTime.Now, isNew: isNew);
    public void Add(DateTime time, bool isNew = true) => base.Add(Generate(time, isNew));
    public void Add(int count)
    {
        DateTime startTime = DateTime.UtcNow - TimeSpan.FromHours(count);
        for (int i = 0; i < count; i++)
        {
            Add(startTime, true);
            Add(startTime, false);
            Add(startTime, false);
            startTime = startTime.AddHours(1);
        }
    }

    public TBar Generate(DateTime time, bool isNew = true)
    {
        double dt = 1.0 / 252;
        double drift = (_mu - 0.5 * _sigma * _sigma) * dt;
        double diffusion = _sigma * Math.Sqrt(dt) * GenerateNormalRandom();
        double newClose = _lastClose * Math.Exp(drift + diffusion);

        double open = _lastClose;
        double high = Math.Max(_lastHigh, Math.Max(open, newClose) * (1 + GenerateRandomDouble() * 0.01));
        double low = Math.Min(_lastLow, Math.Min(open, newClose) * (1 - GenerateRandomDouble() * 0.01));
        double volume = 1000 + GenerateRandomDouble() * 1000;

        if (isNew)
        {
            _lastClose = newClose;
        }
        else
        {
            high = Math.Max(_lastHigh, high);
            low = Math.Min(_lastLow, low);
        }
        _lastHigh = high;
        _lastLow = low;

        TBar bar = new(time, open, high, low, newClose, volume, isNew);
        return bar;
    }

    private double GenerateNormalRandom()
    {
        // Box-Muller transform to generate standard normal random variable
        double u1 = 1.0 - GenerateRandomDouble(); // Uniform(0,1] random doubles
        double u2 = 1.0 - GenerateRandomDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    private double GenerateRandomDouble()
    {
        byte[] bytes = new byte[8];
        _rng.GetBytes(bytes);
        return (double)BitConverter.ToUInt64(bytes, 0) / ulong.MaxValue;
    }
}