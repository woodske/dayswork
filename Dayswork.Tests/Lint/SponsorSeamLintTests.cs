namespace Dayswork.Tests.Lint;

using System.Text.RegularExpressions;
using Xunit;

/// <summary>
/// The 2.0 sponsor model routes money, shipping, and tools through <c>Sponsor</c> so a worker
/// always spends and ships for the office's OWNER rather than whoever is at the keyboard. Those
/// seams cannot be unit-tested against real netcode types, so they are enforced here: nothing
/// outside <c>Sponsor</c> may reach for a <c>Farmer</c>'s money or shipping bin directly.
/// </summary>
public class SponsorSeamLintTests
{
    private const string SponsorFileName = "Sponsor.cs";

    // Farmer.Money is a wrapper over the LOCAL player's wallet; the sponsor's is team.GetMoney(owner).
    // Sponsor.Money(ownerId) is the sanctioned read, so only a receiver-qualified .Money is a hit.
    private static readonly Regex DirectMoneyRegex =
        new(@"(?<!\bSponsor)\.Money\b", RegexOptions.Compiled);

    // Farm.getShippingBin / shipItem take the farmer whose bin receives the item.
    private static readonly Regex DirectShippingRegex =
        new(@"\b(?:getShippingBin|shipItem)\s*\(", RegexOptions.Compiled);

    [Fact]
    public void NoDirectFarmerMoneyOutsideSponsor()
    {
        var offenders = FindOffenders(DirectMoneyRegex);

        Assert.True(
            offenders.Count == 0,
            "Money must go through Sponsor.Wallet/Money/Charge/Credit so the contract owner pays:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void NoDirectShippingBinOutsideSponsor()
    {
        var offenders = FindOffenders(DirectShippingRegex);

        Assert.True(
            offenders.Count == 0,
            "Shipping must go through Sponsor.ShippingBin/ShipItem so output reaches the owner's bin:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static List<string> FindOffenders(Regex pattern)
    {
        var sourceRoot = Path.Combine(FindWorkspaceRoot(), "Dayswork");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGeneratedOrExempt(file))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (IsCommentLine(lines[i]) || !pattern.IsMatch(lines[i]))
                    continue;

                offenders.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }
        }

        return offenders;
    }

    private static bool IsGeneratedOrExempt(string file)
    {
        var normalized = file.Replace(Path.DirectorySeparatorChar, '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal)
            || normalized.Contains("/obj/", StringComparison.Ordinal)
            || Path.GetFileName(file) == SponsorFileName;
    }

    private static bool IsCommentLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
            || trimmed.StartsWith("*", StringComparison.Ordinal)
            || trimmed.StartsWith("/*", StringComparison.Ordinal);
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
