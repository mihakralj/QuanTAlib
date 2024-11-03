using System.Security.Cryptography;

namespace QuanTAlib;

public class GbmFeed : TBarSeries
{
    private readonly double _mu, _sigma;
    private readonly RandomNumberGenerator _rng;
    private double _lastClose;

    public GbmFeed(double initialPrice = 100.0, double mu = 0.05, double sigma = 0.2)
    {
        _lastClose = initialPrice;
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
            Add(startTime, isNew: true);
            startTime = startTime.AddHours(1);
        }
    }

    public TBar Generate(DateTime time, bool isNew = true)
    {
        double dt = 1.0 / 252;
        double drift = (_mu - (0.5 * _sigma * _sigma)) * dt;
        double diffusion = _sigma * Math.Sqrt(dt) * GenerateNormalRandom();

        double open = _lastClose;
        double close = open * Math.Exp(drift + diffusion);

        // Generate intra-bar price movements
        double maxMove = Math.Abs(close - open) * 1.5; // Allow for some extra movement within the bar
        double high = Math.Max(open, close) + (maxMove * GenerateRandomDouble());
        double low = Math.Min(open, close) - (maxMove * GenerateRandomDouble());

        // Ensure high is always greater than or equal to both open and close
        high = Math.Max(high, Math.Max(open, close));

        // Ensure low is always less than or equal to both open and close
        low = Math.Min(low, Math.Min(open, close));

        double volume = 1000 + (GenerateRandomDouble() * 1000);

        if (isNew)
        {
            _lastClose = close;
        }

        return new TBar(time, open, high, low, close, volume, isNew);
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
