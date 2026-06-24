using System;
using System.Collections.Generic;
using System.Linq;
using BlackBoxIdentification.Core;

namespace BlackBoxIdentification.MathModel;

public static class SignalPreprocessor
{
    /// <summary>
    /// Сортирует точки по времени, объединяет повторяющиеся отметки времени и
    /// приводит временную ось к интервалу [0, 1]. Амплитуды x и y не изменяются.
    /// </summary>
    public static SignalData NormalizeTimeToUnitInterval(SignalData source)
    {
        if (source.Count < 2)
            throw new InvalidOperationException("Для нормирования времени требуется не менее двух точек.");

        var grouped = source.Points
            .OrderBy(point => point.Time)
            .GroupBy(point => point.Time)
            .Select(group => new SignalPoint(
                group.Key,
                group.Average(point => point.Input),
                group.Average(point => point.Output)))
            .ToList();

        if (grouped.Count < 2)
            throw new InvalidOperationException("В файле должно быть не менее двух различных отметок времени.");

        double first = grouped[0].Time;
        double last = grouped[^1].Time;
        double span = last - first;

        var normalized = new List<SignalPoint>(grouped.Count);

        if (span <= 0.0 || double.IsNaN(span) || double.IsInfinity(span))
        {
            for (int i = 0; i < grouped.Count; i++)
            {
                double t = i / (double)(grouped.Count - 1);
                normalized.Add(new SignalPoint(t, grouped[i].Input, grouped[i].Output));
            }
        }
        else
        {
            foreach (var point in grouped)
            {
                double t = (point.Time - first) / span;
                normalized.Add(new SignalPoint(t, point.Input, point.Output));
            }
        }

        return new SignalData(normalized);
    }
}
