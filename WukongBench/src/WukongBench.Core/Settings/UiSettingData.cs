using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace WukongBench.Core.Settings;

/// <summary>
/// The benchmark keeps its menu choices in one ini value:
/// <c>UISettingData=(("ScreenMode", "1"),("Vsync", "0"),...)</c>.
/// </summary>
public sealed partial class UiSettingData
{
    private readonly ImmutableArray<KeyValuePair<string, string>> _entries;

    private UiSettingData(ImmutableArray<KeyValuePair<string, string>> entries) => _entries = entries;

    public static UiSettingData Parse(string? raw)
    {
        var entries = EntryPattern().Matches(raw ?? string.Empty)
            .Select(match => KeyValuePair.Create(match.Groups[1].Value, match.Groups[2].Value))
            .ToImmutableArray();
        return new UiSettingData(entries);
    }

    public bool IsEmpty => _entries.IsEmpty;

    public string? Get(string key)
    {
        var index = IndexOf(key);
        return index < 0 ? null : _entries[index].Value;
    }

    public UiSettingData With(string key, string value)
    {
        var entry = KeyValuePair.Create(key, value);
        var index = IndexOf(key);
        return new UiSettingData(index < 0 ? _entries.Add(entry) : _entries.SetItem(index, entry));
    }

    public override string ToString() =>
        "(" + string.Join(",", _entries.Select(entry => $"(\"{entry.Key}\", \"{entry.Value}\")")) + ")";

    private int IndexOf(string key)
    {
        for (var i = 0; i < _entries.Length; i++)
        {
            if (string.Equals(_entries[i].Key, key, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    [GeneratedRegex("""\(\s*"([^"]+)"\s*,\s*"([^"]*)"\s*\)""")]
    private static partial Regex EntryPattern();
}
