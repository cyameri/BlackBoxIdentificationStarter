using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class VolterraModelEvaluator
{
    public static double[] Evaluate(
        SignalData data,
        IdentificationParameters parameters,
        double[] linearCoefficients,
        double[,] quadraticCoefficients)
    {
        var output = new double[data.Count];
        var interpolator = new SignalInterpolator(data);

        for (int pointIndex = 0; pointIndex < data.Count; pointIndex++)
        {
            double time = data.Points[pointIndex].Time;
            double[] features = DesignMatrixBuilder.BuildFeatureVectorAtTime(
                interpolator,
                parameters,
                time);

            int column = 0;
            double value = 0.0;

            for (int i = 0; i < linearCoefficients.Length; i++)
                value += linearCoefficients[i] * features[column++];

            for (int i = 0; i < quadraticCoefficients.GetLength(0); i++)
            {
                for (int j = 0; j < quadraticCoefficients.GetLength(1); j++)
                    value += quadraticCoefficients[i, j] * features[column++];
            }

            output[pointIndex] = value;
        }

        return output;
    }
}
