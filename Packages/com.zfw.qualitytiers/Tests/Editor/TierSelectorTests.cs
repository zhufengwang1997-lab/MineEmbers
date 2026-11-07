using NUnit.Framework;
using System.Linq;

namespace Zfw.QualityTiers.Tests
{
    public class TierSelectorTests
    {
        [TestCase(10.9f, Tier.Ultra)]
        [TestCase(11.0f, Tier.Ultra)]
        [TestCase(13.5f, Tier.High)]
        [TestCase(19.9f, Tier.Medium)]
        [TestCase(26.0f, Tier.Low)]
        [TestCase(0f, Tier.Medium)]  // no GPU timing available
        public void Benchmark(float ms, Tier expected) => Assert.AreEqual(expected, TierSelector.FromBenchmark(ms));

        [Test]
        public void HardwareCap()
        {
            Assert.AreEqual(Tier.Ultra, TierSelector.CapFromHardware(8192, true));   // 1070 Ti
            Assert.AreEqual(Tier.Medium, TierSelector.CapFromHardware(3072, true));  // 3 GB card
            Assert.AreEqual(Tier.Low, TierSelector.CapFromHardware(16384, false));   // iGPU
        }

        [Test]
        public void FinalIsMinOfBenchmarkAndCap()
        {
            Assert.AreEqual(Tier.Medium, TierSelector.Min(TierSelector.FromBenchmark(9f), TierSelector.CapFromHardware(3072, true)));
        }

        [Test]
        public void Discrete()
        {
            Assert.IsTrue(TierSelector.IsDiscreteLike(0x10DE, "NVIDIA GeForce GTX 1070 Ti"));
            Assert.IsTrue(TierSelector.IsDiscreteLike(0x106B, "Apple M5 Max"));
            Assert.IsFalse(TierSelector.IsDiscreteLike(0x8086, "Intel(R) UHD Graphics 630"));
            Assert.IsTrue(TierSelector.IsDiscreteLike(0x8086, "Intel(R) Arc(TM) A770"));
            Assert.IsFalse(TierSelector.IsDiscreteLike(0x1002, "AMD Radeon(TM) Graphics"));
        }

        [Test]
        public void Reselect()
        {
            Assert.IsTrue(TierSelector.NeedsReselect("", "", "GPU", "1"));
            Assert.IsFalse(TierSelector.NeedsReselect("GPU", "1", "GPU", "1"));
            Assert.IsTrue(TierSelector.NeedsReselect("GPU", "1", "GPU", "2"));
        }
    }

    public class CompatOverridesTests
    {
        const string Json = @"[
          {""api"":""Vulkan"",""vendor"":""NVIDIA"",""disable"":[""HeatHaze""],""case"":""CASE-07""},
          {""api"":""Direct3D12"",""vendor"":""NVIDIA"",""driverMax"":""560.00"",""maxTier"":""Medium"",""case"":""CASE-03""}
        ]";

        [Test]
        public void MatchesApiAndVendor()
        {
            var t = CompatOverrides.Parse(Json);
            Assert.AreEqual("CASE-07", CompatOverrides.Matching(t, "Vulkan", 0x10DE, "566.36").Single().@case);
            Assert.IsEmpty(CompatOverrides.Matching(t, "Vulkan", 0x1002, "24.1.1"));
        }

        [Test]
        public void DriverRange()
        {
            var t = CompatOverrides.Parse(Json);
            Assert.IsEmpty(CompatOverrides.Matching(t, "Direct3D12", 0x10DE, "566.36"));
            Assert.AreEqual("CASE-03", CompatOverrides.Matching(t, "Direct3D12", 0x10DE, "552.44").Single().@case);
        }

        [Test]
        public void VersionCompare()
        {
            Assert.Less(CompatOverrides.CompareVersion("566.36", "572.16"), 0);
            Assert.AreEqual(0, CompatOverrides.CompareVersion("560", "560.00"));
            Assert.Greater(CompatOverrides.CompareVersion("10.2", "9.9"), 0);
        }
    }
}
