using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zfw.GpuCaps.Tests
{
    public class GpuCapsTests
    {
        static bool NativeApi => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal
                              || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12
                              || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan;

        [Test]
        public void ReportIsComplete()
        {
            var r = GpuCaps.Refresh();
            Debug.Log(JsonUtility.ToJson(r, true));
            Assert.AreEqual(GpuCapsReport.CurrentSchema, r.schema);
            Assert.IsNotEmpty(r.api);
            Assert.IsNotEmpty(r.gpu.name);
            Assert.IsNotNull(r.features.bindless);
            Assert.IsNotNull(r.features.raytracing);
            Assert.IsNotNull(r.features.shaderModel);
            Assert.AreEqual(Application.unityVersion, r.unity.unityVersion);
        }

        [Test]
        public void NativePluginAnswersOnSupportedApis()
        {
            if (!NativeApi) Assert.Ignore($"graphics device {SystemInfo.graphicsDeviceType}: plugin not expected (run without -nographics)");
            var r = GpuCaps.Refresh();
            Assert.AreEqual("native", r.source, r.error);
            Assert.AreEqual(SystemInfo.graphicsDeviceType.ToString(), r.api);
            // The plugin and Unity look at the same device: names must agree.
            Assert.AreEqual(SystemInfo.graphicsDeviceName, r.gpu.name);
            Assert.That(r.error, Is.Null.Or.Empty);
        }

        [TestCase("NVIDIA GeForce GTX 1070 Ti", "GTX1070Ti")]
        [TestCase("Apple M5 Max", "AppleM5Max")]
        [TestCase("AMD Radeon(TM) RX 7900 XTX", "RX7900XTX")]
        [TestCase("Intel(R) Arc(TM) A770 Graphics", "ArcA770")]
        public void ShortName(string name, string expected) => Assert.AreEqual(expected, GpuCaps.ShortDeviceName(name));

        [Test]
        public void JsonRoundTrip()
        {
            var r = GpuCaps.Current;
            var back = JsonUtility.FromJson<GpuCapsReport>(JsonUtility.ToJson(r));
            Assert.AreEqual(r.gpu.name, back.gpu.name);
            Assert.AreEqual(r.features.raytracing.value, back.features.raytracing.value);
        }
    }
}
