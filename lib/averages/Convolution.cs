namespace QuanTAlib;

public class Convolution : AbstractBase
{
    private readonly double[] _kernel;
    private readonly int _kernelSize;
    private CircularBuffer _buffer;
    private double[] _normalizedKernel;

    public Convolution(double[] kernel)
    {
        if (kernel == null || kernel.Length == 0)
        {
            throw new ArgumentException("Kernel must not be null or empty.", nameof(kernel));
        }
        _kernel = kernel;
        _kernelSize = kernel.Length;
        _buffer = new CircularBuffer(_kernelSize);
        _normalizedKernel = new double[_kernelSize];
        Init();
    }

    public Convolution(object source, double[] kernel) : this(kernel)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    private new void Init()
    {
        base.Init();
        _buffer.Clear();
        Array.Copy(_kernel, _normalizedKernel, _kernelSize);
    }

    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
        }
    }

    protected override double GetLastValid()
    {
        return _lastValidValue;
    }

    protected override double Calculation()
    {
        ManageState(Input.IsNew);

        _buffer.Add(Input.Value, Input.IsNew);

        // Normalize kernel on each calculation until buffer is full
        if (_index <= _kernelSize)
        {
            NormalizeKernel();
        }

        double result = ConvolveBuffer();
        IsHot = _index >= _kernelSize;

        return result;
    }

    private void NormalizeKernel()
    {
        int activeLength = Math.Min(_index, _kernelSize);
        double sum = 0;

        // Calculate the sum of the active kernel elements
        for (int i = 0; i < activeLength; i++)
        {
            sum += _kernel[i];
        }

        // Normalize the kernel or set equal weights if the sum is zero
        double normalizationFactor = (sum != 0) ? sum : activeLength;
        for (int i = 0; i < activeLength; i++)
        {
            _normalizedKernel[i] = _kernel[i] / normalizationFactor;
        }

        // Set the rest of the normalized kernel to zero
        Array.Clear(_normalizedKernel, activeLength, _kernelSize - activeLength);
    }

    private double ConvolveBuffer()
    {
        double sum = 0;
        var bufferSpan = _buffer.GetSpan();
        int activeLength = Math.Min(_index, _kernelSize);

        for (int i = 0; i < activeLength; i++)
        {
            sum += bufferSpan[activeLength - 1 - i] * _normalizedKernel[i];
        }

        return sum;
    }
}