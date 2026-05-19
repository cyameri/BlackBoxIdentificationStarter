using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class VolterraModelEvaluator
{
    public static double[] Evaluate(SignalData data, IdentificationParameters p, double[] a, double[,] c)
    {
        var output = new double[data.Count]; var input = data.Input.ToArray();
        for (int n = 0; n < data.Count; n++)
        {
            double value = 0;
            for (int i = 0; i < a.Length; i++)
            {
                double sum = 0;
                for (int s = 0; s <= p.MemoryLength && n - s >= 0; s++)
                    sum += ChebyshevBasis.T(i, ChebyshevBasis.MapToMinusOneOne(s, p.MemoryLength)) * input[n - s];
                value += a[i] * sum;
            }
            for (int i = 0; i < c.GetLength(0); i++)
            for (int j = 0; j < c.GetLength(1); j++)
            {
                double sum = 0;
                for (int s1 = 0; s1 <= p.MemoryLength && n - s1 >= 0; s1++)
                {
                    double b1 = ChebyshevBasis.T(i, ChebyshevBasis.MapToMinusOneOne(s1, p.MemoryLength));
                    for (int s2 = 0; s2 <= p.MemoryLength && n - s2 >= 0; s2++)
                    {
                        double b2 = ChebyshevBasis.T(j, ChebyshevBasis.MapToMinusOneOne(s2, p.MemoryLength));
                        sum += b1 * b2 * input[n - s1] * input[n - s2];
                    }
                }
                value += c[i, j] * sum;
            }
            output[n] = value;
        }
        return output;
    }
}
