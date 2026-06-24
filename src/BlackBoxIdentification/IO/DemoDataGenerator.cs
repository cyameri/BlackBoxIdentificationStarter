using System;
using System.Collections.Generic;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.IO;

public static class DemoDataGenerator
{
    /// <summary>
    /// Демонстрационный пример соответствует третьей модели из Maple-файла:
    /// x(t)=exp(-3t)sin(10t), K1(s)=cos(s/2), K2(s1,s2)=sin(s1+2s2), t in [0,1].
    /// </summary>
    public static SignalData Generate(int count = 201)
    {
        var points = new List<SignalPoint>(count);

        for (int index = 0; index < count; index++)
        {
            double t = index / (double)(count - 1);
            double input = InputFunction(t);
            double output = BuildOutput(t);
            points.Add(new SignalPoint(t, input, output));
        }

        return new SignalData(points);
    }

    private static double InputFunction(double t) => Math.Exp(-3.0 * t) * Math.Sin(10.0 * t);

    private static double FirstKernel(double s) => Math.Cos(0.5 * s);

    private static double SecondKernel(double s1, double s2) => Math.Sin(s1 + 2.0 * s2);

    private static double BuildOutput(double t)
    {
        if (t <= 0.0)
            return 0.0;

        double firstTerm = Integrate(
            s => FirstKernel(s) * InputFunction(t - s),
            0.0,
            t,
            120);

        double secondTerm = Integrate(
            s1 => Integrate(
                s2 => SecondKernel(s1, s2) * InputFunction(t - s1) * InputFunction(t - s2),
                0.0,
                t,
                60),
            0.0,
            t,
            60);

        return firstTerm + secondTerm;
    }

    private static double Integrate(Func<double, double> function, double left, double right, int intervals)
    {
        if (right <= left)
            return 0.0;

        intervals = Math.Max(2, intervals);
        if ((intervals & 1) != 0)
            intervals++;

        double h = (right - left) / intervals;
        double sum = function(left) + function(right);

        for (int i = 1; i < intervals; i++)
        {
            double x = left + i * h;
            sum += ((i & 1) == 0 ? 2.0 : 4.0) * function(x);
        }

        return sum * h / 3.0;
    }
}
