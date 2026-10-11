using System.Collections.Immutable;

namespace WukongBench.Core.Settings;

/// <summary>
/// Line-preserving view of an Unreal Engine ini file: only the keys that are
/// explicitly set change, everything else is written back byte for byte.
/// </summary>
public sealed class IniDocument
{
    private readonly ImmutableArray<string> _lines;
    private readonly string _newLine;

    private IniDocument(ImmutableArray<string> lines, string newLine)
    {
        _lines = lines;
        _newLine = newLine;
    }

    public static IniDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToImmutableArray();
        return new IniDocument(lines, newLine);
    }

    public string? Get(string section, string key)
    {
        var (start, end) = FindSection(section);
        var index = FindKey(start, end, key);
        return index < 0 ? null : _lines[index][(_lines[index].IndexOf('=') + 1)..].Trim();
    }

    public IniDocument Set(string section, string key, string value)
    {
        var entry = $"{key}={value}";
        var (start, end) = FindSection(section);
        if (start < 0)
        {
            return new IniDocument(AppendSection(section, entry), _newLine);
        }

        var index = FindKey(start, end, key);
        return index >= 0
            ? new IniDocument(_lines.SetItem(index, entry), _newLine)
            : new IniDocument(_lines.Insert(LastContentLine(start, end) + 1, entry), _newLine);
    }

    public override string ToString() => string.Join(_newLine, _lines);

    private ImmutableArray<string> AppendSection(string section, string entry)
    {
        var content = _lines.Reverse().SkipWhile(string.IsNullOrWhiteSpace).Reverse().ToImmutableArray();
        var separator = content.IsEmpty ? ImmutableArray<string>.Empty : ImmutableArray.Create(string.Empty);
        return content.AddRange(separator).AddRange(new[] { $"[{section}]", entry, string.Empty });
    }

    /// <returns>Index of the first line after the header and the exclusive end, or (-1, -1).</returns>
    private (int Start, int End) FindSection(string section)
    {
        var header = $"[{section}]";
        for (var i = 0; i < _lines.Length; i++)
        {
            if (!string.Equals(_lines[i].Trim(), header, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var end = i + 1;
            while (end < _lines.Length && !IsSectionHeader(_lines[end]))
            {
                end++;
            }

            return (i + 1, end);
        }

        return (-1, -1);
    }

    private int FindKey(int start, int end, string key)
    {
        for (var i = Math.Max(start, 0); i < end; i++)
        {
            var line = _lines[i];
            var separator = line.IndexOf('=');
            if (separator > 0
                && !line.TrimStart().StartsWith(';')
                && string.Equals(line[..separator].Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private int LastContentLine(int start, int end)
    {
        var last = end - 1;
        while (last >= start && string.IsNullOrWhiteSpace(_lines[last]))
        {
            last--;
        }

        return last;
    }

    private static bool IsSectionHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith('[') && trimmed.EndsWith(']');
    }
}
