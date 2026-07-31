using Skua.Core.Interfaces.Services;
using Skua.Core.Models;
using Skua.Core.Models.Scripts;

namespace Skua.Core.Scripts;

public sealed class ScriptPathResolver : IScriptPathResolver
{
    private readonly ICustomScriptService _customScripts;
    private readonly string _repositoryRoot = Canonicalize(ClientFileSources.SkuaScriptsDIR);

    public ScriptPathResolver(ICustomScriptService customScripts)
    {
        _customScripts = customScripts;
    }

    public ScriptPathResolution Resolve(string? ownerFile, string specifier)
    {
        if (string.IsNullOrWhiteSpace(specifier))
            return new(null, Array.Empty<string>());

        string normalizedSpecifier = specifier.Trim().Replace('/', Path.DirectorySeparatorChar);
        List<string> candidates = new();

        if (Path.IsPathRooted(normalizedSpecifier))
        {
            candidates.Add(normalizedSpecifier);
        }
        else if (normalizedSpecifier.StartsWith($".{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                 normalizedSpecifier.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            AddOwnerRelativeCandidate(candidates, ownerFile, normalizedSpecifier);
        }
        else if (normalizedSpecifier.StartsWith($"Scripts{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(Path.Combine(_repositoryRoot, normalizedSpecifier[("Scripts".Length + 1)..]));
        }
        else if (_customScripts.IsCustomPath(ownerFile ?? string.Empty))
        {
            AddOwnerRelativeCandidate(candidates, ownerFile, normalizedSpecifier);
            candidates.Add(Path.Combine(_repositoryRoot, normalizedSpecifier));
        }
        else
        {
            candidates.Add(Path.Combine(_repositoryRoot, normalizedSpecifier));
            AddOwnerRelativeCandidate(candidates, ownerFile, normalizedSpecifier);
        }

        List<string> existing = candidates
            .Select(Canonicalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToList();

        return new(existing.FirstOrDefault(), existing);
    }

    private static void AddOwnerRelativeCandidate(List<string> candidates, string? ownerFile, string specifier)
    {
        if (!string.IsNullOrWhiteSpace(ownerFile))
            candidates.Add(Path.Combine(Path.GetDirectoryName(Canonicalize(ownerFile))!, specifier));
    }

    private static string Canonicalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
