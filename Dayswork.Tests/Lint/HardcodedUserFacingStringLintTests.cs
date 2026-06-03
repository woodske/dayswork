namespace Dayswork.Tests.Lint;

using System.Text.RegularExpressions;
using Xunit;

public class HardcodedUserFacingStringLintTests
{
    private static readonly Regex StringLiteralRegex =
        new("(?<!@)\"(?:[^\"\\\\]|\\\\.)*\"", RegexOptions.Compiled);
    private static readonly Regex InterpolatedHoleRegex =
        new(@"\{[^}]+\}", RegexOptions.Compiled);

    private static readonly Regex TechnicalLiteralRegex =
        new(
            "^(?:[A-Za-z0-9_./\\\\:-]+|\\(O\\)\\d+|\\(BC\\)\\d+|[A-Za-z]+(?:[A-Z][A-Za-z0-9]+)+)$",
            RegexOptions.Compiled);

    private static readonly Regex[] ApprovedLinePatterns =
    {
        new(@"I18nHelper\.Get\(", RegexOptions.Compiled),
        new(@"_helper\.Translation\.Get\(", RegexOptions.Compiled),
        new(@"\.Log\(", RegexOptions.Compiled),
        new(@"ConsoleCommands\.Add\(", RegexOptions.Compiled),
        new(@"playSound\(", RegexOptions.Compiled),
        new(@"GetApi\(", RegexOptions.Compiled),
        new(@"ItemRegistry\.Create", RegexOptions.Compiled),
        new(@"QualifiedItemId", RegexOptions.Compiled),
        new(@"ItemId", RegexOptions.Compiled),
        new(@"new ClickableComponent\(", RegexOptions.Compiled),
        new(@"new ClickableTextureComponent\(", RegexOptions.Compiled),
        new(@"doesTileHaveProperty\(", RegexOptions.Compiled),
        new(@"Map\.Properties", RegexOptions.Compiled),
        new(@"GetField\(", RegexOptions.Compiled),
        new(@"GetMethod\(", RegexOptions.Compiled),
        new(@"GetType\(", RegexOptions.Compiled),
        new(@"Expression\.Parameter\(", RegexOptions.Compiled),
        new(@"NameWithoutLocale\.IsEquivalentTo", RegexOptions.Compiled),
        new(@"LocationName", RegexOptions.Compiled),
        new(@"new Zone\(", RegexOptions.Compiled),
        new(@"Game1\.content\.Load", RegexOptions.Compiled),
        new(@"\bContains\(", RegexOptions.Compiled),
        new(@"\bEquals\(", RegexOptions.Compiled),
        new(@"string\.Equals\(", RegexOptions.Compiled),
        new(@"Guid\.NewGuid", RegexOptions.Compiled),
        new(@"TaskKind\.", RegexOptions.Compiled),
        new(@"BUILDINGS_CONSTRUCTED", RegexOptions.Compiled),
        new(@"build=", RegexOptions.Compiled),
        new(@"\.Append\(", RegexOptions.Compiled),
    };

    private static readonly Regex[] UserVisibleCallsitePatterns =
    {
        new(@"HUDMessage\(", RegexOptions.Compiled),
        new(@"drawTextWithShadow\(", RegexOptions.Compiled),
        new(@"SpriteBatch\.DrawString\(", RegexOptions.Compiled),
        new(@"AddSectionTitle\(", RegexOptions.Compiled),
        new(@"AddNumberOption\(", RegexOptions.Compiled),
    };

    [Fact]
    public void Dayswork_source_has_no_non_i18n_user_facing_string_literals()
    {
        var sourceRoot = Path.Combine(FindWorkspaceRoot(), "Dayswork");
        var violations = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsIgnoredPath(path))
            .SelectMany(FindViolations)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Found hardcoded user-facing string literals:\n" + string.Join("\n", violations));
    }

    private static IEnumerable<string> FindViolations(string path)
    {
        var relativePath = Path.GetRelativePath(FindWorkspaceRoot(), path).Replace('\\', '/');
        var lines = File.ReadAllLines(path);

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (IsCommentLine(line) || HasApprovedContext(lines, index))
                continue;

            var isUserVisibleCallsite = UserVisibleCallsitePatterns.Any(pattern => pattern.IsMatch(line));
            foreach (Match match in StringLiteralRegex.Matches(line))
            {
                var literal = match.Value[1..^1];
                var visibleText = StripInterpolatedHoles(literal);
                if (!ContainsAlphabeticContent(visibleText))
                    continue;

                if (!LooksUserVisible(visibleText, isUserVisibleCallsite))
                    continue;

                yield return $"{relativePath}:{index + 1} => \"{literal}\"";
            }
        }
    }

    private static bool HasApprovedContext(IReadOnlyList<string> lines, int index)
    {
        for (var candidate = Math.Max(0, index - 5); candidate <= Math.Min(lines.Count - 1, index + 5); candidate++)
        {
            if (ApprovedLinePatterns.Any(pattern => pattern.IsMatch(lines[candidate])))
                return true;
        }

        return false;
    }

    private static string StripInterpolatedHoles(string literal) =>
        InterpolatedHoleRegex.Replace(literal, string.Empty);

    private static bool ContainsAlphabeticContent(string literal) =>
        literal.Any(char.IsLetter);

    private static bool LooksUserVisible(string literal, bool isUserVisibleCallsite)
    {
        if (TechnicalLiteralRegex.IsMatch(literal))
            return isUserVisibleCallsite;

        if (isUserVisibleCallsite)
            return true;

        return literal.StartsWith("[Dayswork]", StringComparison.Ordinal) ||
               literal.Any(char.IsWhiteSpace) ||
               literal.Contains('!') ||
               literal.Contains('?') ||
               literal.Contains('.') ||
               literal.Contains(':');
    }

    private static bool IsCommentLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal) ||
               trimmed.StartsWith("/*", StringComparison.Ordinal) ||
               trimmed.StartsWith("*", StringComparison.Ordinal) ||
               trimmed.StartsWith("*/", StringComparison.Ordinal);
    }

    private static bool IsIgnoredPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
               normalized.Contains("/obj/", StringComparison.Ordinal);
    }

    private static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Dayswork.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the Dayswork workspace root.");
    }
}
