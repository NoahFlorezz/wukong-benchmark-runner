namespace WukongBench.Settings;

/// <summary>
/// Keeps the user's own GameUserSettings.ini safe while the tool overwrites it.
/// The copy lives outside the game folder so that it survives a crash of this tool.
/// </summary>
internal sealed class SettingsBackup
{
    private readonly string _settingsPath;
    private readonly string _backupPath;

    public SettingsBackup(string settingsPath)
    {
        _settingsPath = settingsPath;
        _backupPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WukongBench",
            "GameUserSettings.backup.ini");
    }

    /// <summary>A leftover copy means the previous run died before restoring; put it back first.</summary>
    public bool RestoreIfStale()
    {
        if (!File.Exists(_backupPath))
        {
            return false;
        }

        Restore();
        return true;
    }

    public void Create()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_backupPath)!);
        File.Copy(_settingsPath, _backupPath, overwrite: true);
    }

    public void Restore()
    {
        if (!File.Exists(_backupPath))
        {
            return;
        }

        File.Copy(_backupPath, _settingsPath, overwrite: true);
        File.Delete(_backupPath);
    }
}
