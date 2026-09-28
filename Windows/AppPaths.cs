/// <summary>Where the helper keeps its browser profile and cached icon.</summary>
internal static class AppPaths
{
    /// <summary>
    /// Names a folder to use instead of <c>%LOCALAPPDATA%\PrimeVideoSpeedController</c>.
    /// </summary>
    public const string DataDirectoryVariable = "PVSC_DATA_DIR";

    /// <summary>
    /// True when <see cref="DataDirectoryVariable"/> is set. The helper then leaves
    /// the machine alone: no Start Menu entry is written, because that entry would
    /// point a normal installation at this copy of the helper.
    /// </summary>
    public static bool IsCustomDataRoot => CustomRoot() is not null;

    public static string DataRoot =>
        CustomRoot() ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeVideoSpeedController");

    public static string EdgeProfile => Path.Combine(DataRoot, "EdgeProfile");

    private static string? CustomRoot()
    {
        var value = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
    }
}
