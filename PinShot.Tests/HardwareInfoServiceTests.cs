using PinShot;

namespace PinShot.Tests;

[TestClass]
public sealed class HardwareInfoServiceTests
{
    [TestMethod]
    public void RecommendsLightProfileForLowResourceSystems()
    {
        var hardware = new HardwareSnapshot(
            "Test OS", "1.0", "X64", "Test CPU", 2,
            4L * 1024 * 1024 * 1024,
            512L * 1024 * 1024,
            null, null, "Not checked");

        ResourceProfile profile = HardwareInfoService.Recommend(hardware);
        ResourceBudget budget = HardwareInfoService.CreateBudget(profile, hardware);

        Assert.AreEqual(ResourceProfile.Light, profile);
        Assert.AreEqual(1, budget.MaxConcurrentOperations);
        Assert.IsLessThanOrEqualTo(hardware.AvailableMemoryBytes!.Value, budget.MaxWorkingMemoryBytes);
    }

    [TestMethod]
    public void PerformanceProfileStillCapsConcurrentWork()
    {
        var hardware = new HardwareSnapshot(
            "Test OS", "1.0", "X64", "Test CPU", 16,
            32L * 1024 * 1024 * 1024,
            16L * 1024 * 1024 * 1024,
            null, true, "Vulkan available");

        ResourceProfile profile = HardwareInfoService.Recommend(hardware);
        ResourceBudget budget = HardwareInfoService.CreateBudget(profile, hardware);

        Assert.AreEqual(ResourceProfile.Performance, profile);
        Assert.IsLessThanOrEqualTo(2, budget.MaxConcurrentOperations);
        Assert.IsLessThanOrEqualTo(hardware.AvailableMemoryBytes!.Value, budget.MaxWorkingMemoryBytes);
    }
}
