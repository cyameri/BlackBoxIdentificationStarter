using System;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class DesignMatrixBuilder
{
    public static (double[,] Matrix, double[] Target, int[] SampleIndexes) Build(SignalData data, IdentificationParameters p)
    {
        int m1 = p.LinearOrder, m2 = p.QuadraticOrder, parameterCount = m1 + m2 * m2;
        int start = Math.Min(Math.Max(1, p.MemoryLength), data.Count - 1);
        int available = data.Count - start;
        int rows = Math.Min(Math.Max(parameterCount + 1, p.NodeCount), available);
        if (rows <= 0) throw new InvalidOperationException("Недостаточно точек для построения системы.");
        var matrix = new double[rows, parameterCount];
        var target = new double[rows];
        var sampleIndexes = new int[rows];
        var input = data.Input.ToArray(); var output = data.Output.ToArray();
        for (int row = 0; row < rows; row++)
        {
            int n = start + row * Math.Max(1, available - 1) / Math.Max(1, rows - 1);
            sampleIndexes[row] = n; target[row] = output[n];
            for (int i = 0; i < m1; i++)
            {
                double sum = 0;
                for (int s = 0; s <= p.MemoryLength && n - s >= 0; s++)
                    sum += ChebyshevBasis.T(i, ChebyshevBasis.MapToMinusOneOne(s, p.MemoryLength)) * input[n - s];
                matrix[row, i] = sum;
            }
            int column = m1;
            for (int i = 0; i < m2; i++)
            for (int j = 0; j < m2; j++)
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
                matrix[row, column++] = sum;
            }
        }
        return (matrix, target, sampleIndexes);
    }
}
