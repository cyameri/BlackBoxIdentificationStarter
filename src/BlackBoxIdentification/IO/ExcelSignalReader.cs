using System;
using System.Collections.Generic;
using BlackBoxIdentification.Core;
using ClosedXML.Excel;

namespace BlackBoxIdentification.IO;

public static class ExcelSignalReader
{
    public static SignalData ReadTwoColumnFile(string path)
    {
        using var workbook = new XLWorkbook(path);
        var sheet = workbook.Worksheet(1);
        var points = new List<SignalPoint>();
        foreach (var row in sheet.RowsUsed())
        {
            var values = new List<double>();
            foreach (var cell in row.CellsUsed())
                if (TryGetDouble(cell, out var value)) values.Add(value);
            if (values.Count >= 3) points.Add(new SignalPoint(values[0], values[1], values[2]));
            else if (values.Count == 2) points.Add(new SignalPoint(points.Count, values[0], values[1]));
        }
        if (points.Count < 5) throw new InvalidOperationException("В файле слишком мало числовых точек. Ожидаются колонки x,y или t,x,y.");
        return new SignalData(points);
    }
    private static bool TryGetDouble(IXLCell cell, out double value)
    {
        value = 0;
        if (cell.DataType == XLDataType.Number) { value = cell.GetDouble(); return true; }
        return double.TryParse(cell.GetString().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value);
    }
}
