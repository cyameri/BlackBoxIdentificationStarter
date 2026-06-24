using System.Globalization;
using System.Text;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.IO;

public static class ResultExporter
{
    public static void SaveCsv(string path, SignalData data, IdentificationResult result)
    {
        var culture = CultureInfo.InvariantCulture;
        var builder = new StringBuilder();

        builder.AppendLine("t;x;y;y_model;residual");
        for (int i = 0; i < data.Count; i++)
        {
            SignalPoint point = data.Points[i];
            builder.AppendLine(string.Join(";",
                point.Time.ToString(culture),
                point.Input.ToString(culture),
                point.Output.ToString(culture),
                result.ModelOutput[i].ToString(culture),
                result.Residual[i].ToString(culture)));
        }

        builder.AppendLine();
        builder.AppendLine("A coefficients");
        for (int i = 0; i < result.LinearCoefficients.Length; i++)
            builder.AppendLine($"A[{i}];{result.LinearCoefficients[i].ToString(culture)}");

        builder.AppendLine();
        builder.AppendLine("C coefficients");
        for (int i = 0; i < result.QuadraticCoefficients.GetLength(0); i++)
        {
            for (int j = 0; j < result.QuadraticCoefficients.GetLength(1); j++)
            {
                builder.AppendLine(
                    $"C[{i},{j}];{result.QuadraticCoefficients[i, j].ToString(culture)}");
            }
        }

        builder.AppendLine();
        builder.AppendLine($"Unknown coefficients;{result.ParameterCount}");
        builder.AppendLine($"Equations;{result.EquationCount}");
        builder.AppendLine($"RMSE;{result.Rmse.ToString(culture)}");
        builder.AppendLine($"Relative error, %;{result.RelativeErrorPercent.ToString(culture)}");
        builder.AppendLine($"Max absolute error;{result.MaxAbsoluteError.ToString(culture)}");

        builder.AppendLine();
        builder.AppendLine("Amplitude normalization");
        builder.AppendLine($"Enabled;{result.IsNormalized}");
        builder.AppendLine($"Input mean;{result.InputMean.ToString(culture)}");
        builder.AppendLine($"Input scale;{result.InputScale.ToString(culture)}");
        builder.AppendLine($"Output mean;{result.OutputMean.ToString(culture)}");
        builder.AppendLine($"Output scale;{result.OutputScale.ToString(culture)}");

        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }
}
