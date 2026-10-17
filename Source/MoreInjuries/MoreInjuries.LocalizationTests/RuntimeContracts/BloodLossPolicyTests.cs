using MoreInjuries.HealthConditions.HeavyBleeding;

namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class BloodLossPolicyTests
{
    [TestMethod]
    public void PreventDirectDeathProtectsTrackedPawnsOnly()
    {
        Assert.IsFalse(BloodLossPolicy.AllowDirectDeath(tracked: true, preventDirectDeath: true));
        Assert.IsTrue(BloodLossPolicy.AllowDirectDeath(tracked: true, preventDirectDeath: false));
        Assert.IsTrue(BloodLossPolicy.AllowDirectDeath(tracked: false, preventDirectDeath: true));
        Assert.IsTrue(BloodLossPolicy.AllowDirectDeath(tracked: false, preventDirectDeath: false));
    }

    [TestMethod]
    public void CappedBloodLossMustNotBeIntrinsicallyLethal()
    {
        Assert.AreEqual(1.001f, BloodLossPolicy.NonlethalCapThreshold(1f, 1f));
        Assert.AreEqual(1.001f, BloodLossPolicy.NonlethalCapThreshold(1f, 0.9f));
        Assert.AreEqual(1.001f, BloodLossPolicy.NonlethalCapThreshold(1f, 1.001f));
        Assert.AreEqual(2f, BloodLossPolicy.NonlethalCapThreshold(1f, 2f));
        Assert.AreEqual(-1f, BloodLossPolicy.NonlethalCapThreshold(1f, -1f));
        Assert.AreEqual(0f, BloodLossPolicy.NonlethalCapThreshold(1f, 0f));
    }
}
