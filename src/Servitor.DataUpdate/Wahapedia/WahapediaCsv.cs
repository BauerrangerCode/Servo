using System.Text.RegularExpressions;

namespace Servitor.DataUpdate.Wahapedia;

/// <summary>One CSV record; keys are the camel-cased header names, in column order.</summary>
public sealed class CsvRow : OrderedDictionary<string, string>
{
    public string Get(string column) => TryGetValue(column, out var value) ? value : "";
}

/// <summary>
/// Reads the pipe-separated Wahapedia CSV exports: the header names become camelCase keys ("datasheet_id" →
/// "datasheetId"), the last column (an artefact of the trailing pipe) is ignored, and a record may span several
/// lines when a cell contains line breaks.
/// </summary>
public static partial class WahapediaCsv
{
    public static List<CsvRow> Parse(string csv)
    {
        var lines = csv.TrimStart('\uFEFF').Split("\r\n");
        var headers = lines[0].Split('|').Select(ToCamelCase).ToArray();
        var rows = new List<CsvRow>();

        foreach (var record in AssembleRecords(lines, headers.Length))
        {
            var columns = record.Split('|');
            var row = new CsvRow();
            for (var i = 0; i < columns.Length - 1 && i < headers.Length; i++)
            {
                row[headers[i]] = WahapediaHtml.Strip(columns[i]);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static IEnumerable<string> AssembleRecords(string[] lines, int expectedColumns)
    {
        var buffer = "";
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (buffer.Length == 0 && string.IsNullOrWhiteSpace(line.Replace("|", "")))
            {
                continue;
            }

            buffer = buffer.Length > 0 ? $"{buffer}\n{line}" : line;
            if (buffer.Count(c => c == '|') + 1 >= expectedColumns)
            {
                yield return buffer;
                buffer = "";
            }
        }
    }

    private static string ToCamelCase(string header) =>
        NonAlphanumericRegex().Replace(header.ToLowerInvariant(), match => match.Groups[1].Value.ToUpperInvariant());

    [GeneratedRegex("[^a-zA-Z0-9]+(.)")]
    private static partial Regex NonAlphanumericRegex();
}
