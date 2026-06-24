using System;

namespace BlackBoxIdentification.MathModel;

public static class ChebyshevBasis
{
    /// <summary>
    /// Многочлен Чебышева первого рода T_n(x).
    /// В Maple-файлах используется orthopoly[T](n, x) непосредственно на x in [0, 1].
    /// </summary>
    public static double T(int n, double x)
    {
        if (n < 0)
            throw new ArgumentOutOfRangeException(nameof(n));

        x = Math.Clamp(x, -1.0, 1.0);

        if (n == 0)
            return 1.0;

        if (n == 1)
            return x;

        double t0 = 1.0;
        double t1 = x;

        for (int k = 2; k <= n; k++)
        {
            double t2 = 2.0 * x * t1 - t0;
            t0 = t1;
            t1 = t2;
        }

        return t1;
    }
}
