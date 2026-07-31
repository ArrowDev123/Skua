using System.Reflection;

namespace Skua.Core.Models;

public static class ClientFileSources
{
    public static string AssemblyVersion { get; } = GetInformationalVersion();
    public static bool IsNightly { get; } = AssemblyVersion.Contains('-', StringComparison.Ordinal);
    public static string DisplayVersion { get; } = IsNightly ? AssemblyVersion + "-N" : AssemblyVersion;
    public static string SkuaDIR { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Skua");
    public static string SkuaSettingsDIR { get; } = Path.Combine(SkuaDIR, "Skua.settings.json");
    public static string SkuaScriptsDIR { get; } = Path.Combine(SkuaDIR, "Scripts");
    public static string SkuaThemesDIR { get; } = Path.Combine(SkuaDIR, "themes");
    public static string SkuaOptionsDIR { get; } = Path.Combine(SkuaDIR, "options");
    public static string SkuaPluginsDIR { get; } = Path.Combine(SkuaDIR, "plugins");
    public static string SkuaAdvancedSkillsFile { get; } = Path.Combine(SkuaDIR, "AdvancedSkills.json");
    public static string SkuaQuestsFile { get; } = Path.Combine(SkuaDIR, "QuestData.json");
    public static string SkuaScriptsCommitFile { get; } = Path.Combine(SkuaDIR, "scripts-commit.txt");
    public static string SkuaJunkItemsFile { get; } = Path.Combine(SkuaScriptsDIR, "JunkItems.json");

    private static string GetInformationalVersion()
    {
        string? informational = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
            return informational.Split('+', 2)[0];

        return Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0.0";
    }
}
