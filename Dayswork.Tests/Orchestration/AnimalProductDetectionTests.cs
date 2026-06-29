using Dayswork.Orchestration;
using FsCheck.Xunit;
using Xunit;

namespace Dayswork.Tests.Orchestration;

/// <summary>
/// Ground animal-product detection is category-based (Egg -5, Animal Goods -18)
/// with a guaranteed-parity legacy-id include, so vanilla + SVE + future products are all collected.
/// </summary>
public sealed class AnimalProductDetectionTests
{
    private const int Egg = -5;          // Object.EggCategory
    private const int AnimalGoods = -18; // Object.sellAtPierresAndMarnies
    private const int Milk = -6;
    private const int Other = -16;       // building resources (e.g., a non-product)

    [Theory]
    // Legacy vanilla ids stay detected (parity), regardless of category argument.
    [InlineData("(O)174", "174", Egg)]   // Large Egg
    [InlineData("(O)430", "430", -17)]   // Truffle — covered by the legacy include even if its category differs
    [InlineData("(O)444", "444", AnimalGoods)] // Duck Feather
    [InlineData("(O)446", "446", AnimalGoods)] // Rabbit's Foot
    public void Legacy_vanilla_products_still_detected(string qid, string id, int category)
    {
        Assert.True(WorkAreaScanner.IsAnimalProductForageObject(qid, id, category, bigCraftable: false));
    }

    [Theory]
    // New products (vanilla Wool, SVE Goose Egg/Camel Wool) detected by category alone.
    [InlineData("(O)440", "440", AnimalGoods)]                                              // vanilla Wool
    [InlineData("FlashShifter.StardewValleyExpandedCP_Goose_Egg", "FlashShifter.StardewValleyExpandedCP_Goose_Egg", Egg)]
    [InlineData("FlashShifter.StardewValleyExpandedCP_Camel_Wool", "FlashShifter.StardewValleyExpandedCP_Camel_Wool", AnimalGoods)]
    public void Category_products_detected(string qid, string id, int category)
    {
        Assert.True(WorkAreaScanner.IsAnimalProductForageObject(qid, id, category, bigCraftable: false));
    }

    [Theory]
    [InlineData("(O)390", "390", Other)]   // Stone — not a product
    [InlineData("(O)999", "999", Milk)]    // Milk category excluded (never a ground object anyway)
    public void Non_products_not_detected(string qid, string id, int category)
    {
        Assert.False(WorkAreaScanner.IsAnimalProductForageObject(qid, id, category, bigCraftable: false));
    }

    [Fact]
    public void Big_craftables_in_a_product_category_are_not_collected()
    {
        // A big-craftable machine should never be grabbed even if its category overlapped.
        Assert.False(WorkAreaScanner.IsAnimalProductForageObject("(BC)16", "16", AnimalGoods, bigCraftable: true));
    }

    [Fact]
    public void Geode_crusher_is_not_collected_as_animal_product()
    {
        // (BC)182 (Geode Crusher) shares itemId "182" with (O)182 (Large Egg). The bare-id
        // check must not match placed machines — regression for the bug where the crusher was
        // removed from the world and deposited into the animal-product output chest.
        Assert.False(WorkAreaScanner.IsAnimalProductForageObject("(BC)182", "182", Other, bigCraftable: true));
    }

    [Property(MaxTest = 300)]
    public void Detection_is_deterministic(string qid, string id, int category, bool bigCraftable)
    {
        Assert.Equal(
            WorkAreaScanner.IsAnimalProductForageObject(qid ?? "", id ?? "", category, bigCraftable),
            WorkAreaScanner.IsAnimalProductForageObject(qid ?? "", id ?? "", category, bigCraftable));
    }
}
