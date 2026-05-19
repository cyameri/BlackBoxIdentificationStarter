using System;
using System.Linq;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class ResultFactory
{
    public static IdentificationResult Create(SignalData data, IdentificationParameters p, double[] solution)
    {
        var a = solution.Take(p.LinearOrder).ToArray();
        var c = new double[p.QuadraticOrder, p.QuadraticOrder];
        int k = p.LinearOrder;
        for (int i = 0; i < p.QuadraticOrder; i++)
        for (int j = 0; j < p.QuadraticOrder; j++) c[i, j] = solution[k++];
        var modelOutput = VolterraModelEvaluator.Evaluate(data, p, a, c);
        var residual = new double[data.Count]; double sumSquares = 0;
        for (int i = 0; i < data.Count; i++) { residual[i] = data.Points[i].Output - modelOutput[i]; sumSquares += residual[i] * residual[i]; }
        return new IdentificationResult { LinearCoefficients = a, QuadraticCoefficients = c, ModelOutput = modelOutput, Residual = residual, Rmse = Math.Sqrt(sumSquares / data.Count) };
    }
}
