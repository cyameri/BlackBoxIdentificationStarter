using System.Globalization;
using System.Text;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.IO;

public static class ResultExporter
{
    public static void SaveCsv(string path, SignalData data, IdentificationResult result)
    {
        var culture = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("t;x;y;y_model;residual");
        for (int i = 0; i < data.Count; i++)
        {
            var p = data.Points[i];
            sb.AppendLine(string.Join(";", p.Time.ToString(culture), p.Input.ToString(culture), p.Output.ToString(culture), result.ModelOutput[i].ToString(culture), result.Residual[i].ToString(culture)));
        }
        sb.AppendLine(); sb.AppendLine("A coefficients");
        for (int i = 0; i < result.LinearCoefficients.Length; i++) sb.AppendLine($"A[{i}];{result.LinearCoefficients[i].ToString(culture)}");
        sb.AppendLine(); sb.AppendLine("C coefficients");
        for (int i = 0; i < result.QuadraticCoefficients.GetLength(0); i++)
        for (int j = 0; j < result.QuadraticCoefficients.GetLength(1); j++) sb.AppendLine($"C[{i},{j}];{result.QuadraticCoefficients[i, j].ToString(culture)}");
        sb.AppendLine(); sb.AppendLine($"RMSE;{result.Rmse.ToString(culture)}");
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}
