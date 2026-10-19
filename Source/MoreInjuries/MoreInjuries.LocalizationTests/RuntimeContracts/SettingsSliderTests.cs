namespace MoreInjuries.LocalizationTests.RuntimeContracts;

[TestClass]
public sealed class SettingsSliderTests
{
    [TestMethod]
    public void ViewingSettingsPreservesPreciseAndOutOfRangeValues()
    {
        Assert.AreEqual(0.0625f, SettingsSliderValue.AfterEdit(0.0625f, 0.0625f, 0.0625f));
        Assert.AreEqual(3.75f, SettingsSliderValue.AfterEdit(3.75f, 1f, 1f));
        Assert.AreEqual(-2f, SettingsSliderValue.AfterEdit(-2f, 0f, 0f));
        Assert.AreEqual(0.015f, SettingsSliderValue.AfterEdit(0.015f, 1.5f, 1.5f, displayFactor: 100f));
        Assert.AreEqual(12345f, SettingsSliderValue.AfterEdit(12345f, 4.938f, 4.938f, displayFactor: 1f / 2500f, floor: true));
    }

    [TestMethod]
    public void EditingUsesTheControlsUnitsAndPrecision()
    {
        Assert.AreEqual(0.35f, SettingsSliderValue.AfterEdit(0.0625f, 0.0625f, 0.347f));
        Assert.AreEqual(12f, SettingsSliderValue.AfterEdit(10f, 10f, 11.2f, ceiling: true));
        Assert.AreEqual(11f, SettingsSliderValue.AfterEdit(10f, 10f, 11.2f, decimals: 0));
        Assert.AreEqual(0.025f, SettingsSliderValue.AfterEdit(0.015f, 1.5f, 2.5f, displayFactor: 100f, decimals: 4));
        Assert.AreEqual(12500f, SettingsSliderValue.AfterEdit(10000f, 4f, 5f, displayFactor: 1f / 2500f, floor: true));
        Assert.AreEqual(300000f, SettingsSliderValue.AfterEdit(240000f, 4f, 5f, displayFactor: 1f / 60000f, floor: true));
    }
}
