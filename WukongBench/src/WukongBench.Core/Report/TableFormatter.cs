namespace WukongBench.Core.Report;

/// <summary>Renders a Markdown table with padded columns so it also reads well in a console.</summary>
public static class TableFormatter
{
    public static string Render(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var widths = headers
            .Select((header, column) => rows
                .Select(row => Cell(row, column).Length)
                .Append(header.Length)
                .Max())
            .ToArray();

        var separator = "|" + string.Join("|", widths.Select(width => new string('-', width + 2))) + "|";
        var lines = rows
            .Select(row => Line(headers.Select((_, column) => Cell(row, column)), widths))
            .Prepend(separator)
            .Prepend(Line(headers, widths));
        return string.Join(Environment.NewLine, lines);
    }

    private static string Cell(IReadOnlyList<string> row, int column) => column < row.Count ? row[column] : string.Empty;

    private static string Line(IEnumerable<string> cells, int[] widths) =>
        "| " + string.Join(" | ", cells.Select((cell, column) => cell.PadRight(widths[column]))) + " |";
}
