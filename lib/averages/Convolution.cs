using System.Runtime.CompilerServices;
namespace QuanTAlib;

/// <summary>
/// Convolution: A fundamental signal processing operation that combines two signals to form a third signal
/// Applies a custom kernel (weight array) to the input data through convolution, allowing for flexible
/// filtering operations. The kernel is automatically normalized to ensure consistent output scaling.
/// </summary>
/// <remarks>
/// Implementation:
///     Based on standard discrete convolution principles from signal processing
/// </remarks>
public class Convolution : AbstractBase
{
    private readonly double[] _kernel;
    private readonly int _kernelSize;
    private readonly CircularBuffer _buffer;
    private readonly double[] _normalizedKernel;
    private int _activeLength;

    /// <param name="kernel">Array of weights defining the convolution operation. The length of this array determines the filter's window size.</param>
    /// <exception cref="ArgumentException">Thrown when kernel is null or empty.</exception>
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

    /// <param name="source">The data source object that publishes updates.</param>
    /// <param name="kernel">Array of weights defining the convolution operation.</param>
    public Convolution(object source, double[] kernel) : this(kernel)
    {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new ValueSignal(Sub));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private new void Init()
    {
        base.Init();
        _buffer.Clear();
        System.Array.Copy(_kernel, _normalizedKernel, _kernelSize);
        _activeLength = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override void ManageState(bool isNew)
    {
        if (isNew)
        {
            _lastValidValue = Input.Value;
            _index++;
            _activeLength = System.Math.Min(_index, _kernelSize);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NormalizeKernel()
    {
        double sum = 0;

        // Calculate the sum of the active kernel elements
        for (int i = 0; i < _activeLength; i++)
        {
            sum += _kernel[i];
        }

        // Normalize the kernel or set equal weights if the sum is zero
        double normalizationFactor = (sum != 0) ? sum : _activeLength;
        double invNormFactor = 1.0 / normalizationFactor;

        for (int i = 0; i < _activeLength; i++)
        {
            _normalizedKernel[i] = _kernel[i] * invNormFactor;
        }

        // Set the rest of the normalized kernel to zero
        if (_activeLength < _kernelSize)
        {
            System.Array.Clear(_normalizedKernel, _activeLength, _kernelSize - _activeLength);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private double ConvolveBuffer()
    {
        double sum = 0;
        var bufferSpan = _buffer.GetSpan();
        int offset = _activeLength - 1;

        // Unroll the loop for better performance when possible
        int i = 0;
        while (i <= offset - 3)
        {
            sum += (bufferSpan[offset - i] * _normalizedKernel[i]) +
                  (bufferSpan[offset - (i + 1)] * _normalizedKernel[i + 1]) +
                  (bufferSpan[offset - (i + 2)] * _normalizedKernel[i + 2]) +
                  (bufferSpan[offset - (i + 3)] * _normalizedKernel[i + 3]);
            i += 4;
        }

        // Handle remaining elements
        while (i < _activeLength)
        {
            sum += bufferSpan[offset - i] * _normalizedKernel[i];
            i++;
        }

        return sum;
    }
}
