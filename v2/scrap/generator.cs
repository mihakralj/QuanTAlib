/*
public class Generator {
    public List<double> Values { get; private set; }
    public TValue Tick { get; private set; }
    public event Signal Pub;

    public Generator() {
    }

    public Generator(double[] input) : this() {
        Tick = default;
        Values = new List<double>();
        foreach (var value in input) {
            Values.Add(value);
        }
    }

    public void Generate() {
        Random random = new Random();

        double randomValue1 = random.NextDouble();
        double randomValue2 = random.NextDouble();
        foreach (var value in Values) {
            this.Update(randomValue1, true);
            this.Update(randomValue2, false);
            this.Update(value, false);
        }
    }

    public TValue Update(double input, bool isNew) {
        Tick = new TValue(DateTime.Now, input, isNew, false);
        Pub?.Invoke(this, new ValueEventArgs(Tick));
        return Tick;
    }
}

public class Receiver {
    public List<double> Values { get; private set; }

    public Receiver (object source) : this() {
        var pubEvent = source.GetType().GetEvent("Pub");
        pubEvent?.AddEventHandler(source, new Signal(Sub));
    }

    public Receiver() {
        Values = new List<double>();
    }

    public void Sub(object source, ValueEventArgs args) {
        if (args.Tick.IsNew) {
            Values.Add(args.Tick.v);
        } else {
            Values[^1] = args.Tick.v;
        }
        //Console.WriteLine($"{tick.v:F2}");
    }
}
*/