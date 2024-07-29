namespace QuanTAlib;
public class GBM_Feed
{
    private readonly double _mu;
    private readonly double _sigma;
    private readonly Random _random;
    private double _lastClose;
    private double _lastHigh;
    private double _lastLow;

    public GBM_Feed(double initialPrice, double mu, double sigma)
    {
        _lastClose = initialPrice;
        _lastHigh = initialPrice;
        _lastLow = initialPrice;
        _mu = mu;
        _sigma = sigma;
        _random = Random.Shared;
    }

    public TBar Generate(bool IsNew = true)
    {
        DateTime time = DateTime.UtcNow;
        double dt = 1.0 / 252; // Assuming daily steps in a trading year of 252 days
        double drift = (_mu - 0.5 * _sigma * _sigma) * dt;
        double diffusion = _sigma * Math.Sqrt(dt) * NormalRandom();
        double newClose = _lastClose * Math.Exp(drift + diffusion);

        double open = _lastClose;
        double high = Math.Max(open, newClose) * (1 + _random.NextDouble() * 0.01);
        double low = Math.Min(open, newClose) * (1 - _random.NextDouble() * 0.01);
        double volume = 1000 + _random.NextDouble() * 1000; // Random volume between 1000 and 2000

        if (!IsNew)
        {
            high = Math.Max(_lastHigh, high);
            low = Math.Min(_lastLow, low);
        }
        else
        {
            _lastClose = newClose;
        }

        _lastHigh = high;
        _lastLow = low;

        return new TBar(time, open, high, low, newClose, volume, IsNew);
    }

    private double NormalRandom()
    {
        // Box-Muller transform to generate standard normal random variable
        double u1 = 1.0 - _random.NextDouble(); // Uniform(0,1] random doubles
        double u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}