using MoreInjuries.AI.NpcCombatMedicine;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class NpcMedicalLoadoutTests
{
    private static NpcMedicalLoadout<string> Kit(int existingBandages = 0) => new([
        new("bandage", 9, existingBandages, .05f, .05f, true, 0),
        new("medicine", 9, 0, .5f, 1f, true, 0),
        new("saline", 8, 0, .25f, .25f, true, 1),
        new("defibrillator", 1, 0, .5f, 2f, true, 2),
        new("suction", 1, 0, .4f, 1.5f, true, 2),
        new("ketamine", 2, 0, .05f, .05f, false, 2)]);

    [TestMethod]
    public void ReusableDevicesSurviveConsumableQuantityReduction()
    {
        NpcMedicalSpace space = new(2f, 6f);
        Assert.IsTrue(Kit().FitsCore(space));
        Dictionary<string, int> kit = Kit().Allocate(space);
        Assert.AreEqual(1, kit["defibrillator"]);
        Assert.AreEqual(1, kit["suction"]);
        Assert.IsTrue(kit["medicine"] < 9);
        Assert.IsTrue(kit["saline"] < 8);
        Assert.IsFalse(kit.ContainsKey("ketamine"), "Carried-only sedatives must not displace the clinical core.");
        Assert.IsTrue(kit["bandage"] * .05f + kit["medicine"] * .5f + kit["saline"] * .25f + .9f <= 2.00001f);
        Assert.IsTrue(kit["bandage"] * .05f + kit["medicine"] + kit["saline"] * .25f + 3.5f <= 6.00001f);
    }

    [TestMethod]
    public void MedicSelectionUsesMedicalSkillAmongCarriersWhoCanFitTheCore()
    {
        (int id, int skill, NpcMedicalSpace space)[] pawns = [
            (1, 12, new(.38f, 20f)), (2, 6, new(10f, 20f)), (3, 9, new(10f, 20f)), (4, 18, new(10f, .1f))];
        (int id, int skill, NpcMedicalSpace space) selected = NpcMedicalLoadout<string>.SelectMedic(pawns,
            pawn => Kit().CoreCoverage(pawn.space), pawn => pawn.skill, pawn => pawn.id);
        Assert.AreEqual(3, selected.id);
        Assert.IsTrue(Kit().Allocate(selected.space).ContainsKey("defibrillator"));
        Assert.AreEqual(1, NpcMedicalLoadout<string>.SelectMedic(pawns.Take(1), _ => 0f, pawn => pawn.skill, pawn => pawn.id).id,
            "No room anywhere must not cause generation failure or remove equipment.");
        Assert.AreEqual(1, NpcMedicalLoadout<string>.SelectMedic(pawns.Where(pawn => pawn.id is 1 or 4),
            pawn => Kit().CoreCoverage(pawn.space), pawn => pawn.skill, pawn => pawn.id).id,
            "If nobody fits the complete core, a partly useful kit beats a higher skill with virtually no bulk space.");
    }

    [TestMethod]
    public void ExistingSuppliesCountTowardCoreAndAreNeverGrantedAgain()
    {
        NpcMedicalLoadout<string> existing = new([new("medicine", 6, 6, .5f, 1f, true, 0),
            new("defibrillator", 1, 1, .5f, 2f, true, 2)]);
        Assert.IsTrue(existing.FitsCore(new(0f, 0f)));
        Assert.HasCount(0, existing.Allocate(new(0f, 0f)));
        NpcMedicalLoadout<string> partiallyStocked = new([new("bandage", 9, 3, .05f, .05f, true, 0)]);
        Assert.IsTrue(partiallyStocked.FitsCore(new(0f, 0f)));
        Assert.AreEqual(6, partiallyStocked.Allocate(new(1f, 1f))["bandage"]);
    }

    [TestMethod]
    public void ResolvedBloodItemChangesBothCapacityCostsWithoutRerolling()
    {
        NpcMedicalLoadout<string> blood = new([new("blood", 1, 0, .75f, .5f, true, 1)]);
        NpcMedicalLoadout<string> hemogen = new([new("hemogen", 1, 0, .5f, .25f, true, 1)]);
        Assert.HasCount(0, blood.Allocate(new(.6f, .4f)));
        Assert.AreEqual(1, hemogen.Allocate(new(.6f, .4f))["hemogen"]);
        Assert.HasCount(0, hemogen.Allocate(new(1f, .2f)), "Mass alone cannot authorize a CE item.");
        Assert.HasCount(0, blood.Allocate(new(.6f, float.PositiveInfinity)), "Vanilla still enforces actual mass.");
    }
}
