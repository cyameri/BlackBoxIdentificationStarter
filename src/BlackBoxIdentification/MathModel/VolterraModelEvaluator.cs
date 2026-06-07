using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class VolterraModelEvaluator
{
    public static double[] Evaluate(SignalData data, IdentificationParameters p, double h0, double[] a, double[,] c)
    {
        var output = new double[data.Count];

        for (int n = 0; n < data.Count; n++)
        {
            double[] features = DesignMatrixBuilder.BuildFeatureVector(data, p, n);

            double value = h0 * features[0];
            int column = 1;

            for (int i = 0; i < a.Length; i++)
                value += a[i] * features[column++];

            for (int i = 0; i < c.GetLength(0); i++)
            {
                for (int j = 0; j < c.GetLength(1); j++)
                    value += c[i, j] * features[column++];
            }

            output[n] = value;
        }

        return output;
    }
}
