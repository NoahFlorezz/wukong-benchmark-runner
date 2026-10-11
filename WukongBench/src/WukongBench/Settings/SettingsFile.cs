using System.Text;

namespace WukongBench.Settings;

/// <summary>Reads and writes GameUserSettings.ini keeping the encoding the game used.</summary>
internal static class SettingsFile
{
    public static (string Text, Encoding Encoding) Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var encoding = DetectEncoding(bytes);
        var preamble = encoding.GetPreamble().Length;
        return (encoding.GetString(bytes, preamble, bytes.Length - preamble), encoding);
    }

    public static void Write(string path, string text, Encoding encoding) => File.WriteAllText(path, text, encoding);

    private static Encoding DetectEncoding(byte[] bytes)
    {
        var isUtf16 = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE;
        if (isUtf16)
        {
            return Encoding.Unicode;
        }

        var hasUtf8Bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: hasUtf8Bom);
    }
}
