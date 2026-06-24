using System;
using System.Linq;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public sealed class SignalInterpolator
{
    private readonly double[] _time;
    private readonly double[] _input;
    private readonly double[] _output;

    public SignalInterpolator(SignalData data)
    {
        if (data.Count < 2)
            throw new InvalidOperationException("Для интерполяции требуется не менее двух точек.");

        _time = data.Time.ToArray();
        _input = data.Input.ToArray();
        _output = data.Output.ToArray();
    }

    public double Input(double t) => Interpolate(_input, t);

    public double Output(double t) => Interpolate(_output, t);

    private double Interpolate(double[] values, double t)
    {
        if (t <= _time[0])
            return values[0];

        if (t >= _time[^1])
            return values[^1];

        int right = Array.BinarySearch(_time, t);
        if (right >= 0)
            return values[right];

        right = ~right;
        int left = right - 1;

        double denominator = _time[right] - _time[left];
        if (denominator <= 0.0)
            return values[left];

        double alpha = (t - _time[left]) / denominator;
        return values[left] + alpha * (values[right] - values[left]);
    }
}
