using System;

namespace Zfw.GpuCaps
{
    // Mirrors the JSON schema (SRS 8.2). Field names match the JSON keys so JsonUtility can
    // read and write it directly. Bump Schema when the shape changes.
    [Serializable]
    public class GpuCapsReport
    {
        public const int CurrentSchema = 1;

        public int schema = CurrentSchema;
        public string api;
        /// <summary>"native" when the plugin answered, "systeminfo" when we fell back to Unity's SystemInfo.</summary>
        public string source;
        public GpuInfo gpu = new GpuInfo();
        public DriverInfo driver = new DriverInfo();
        public string os;
        public FeatureSet features = new FeatureSet();
        public UnityInfo unity = new UnityInfo();
        public string error;

        public bool Has(Feature f) => f != null && f.value == Feature.Supported;
    }

    [Serializable] public class GpuInfo { public string name; public int vendorId; public long dedicatedMemoryMB; }
    [Serializable] public class DriverInfo { public string version; public long raw; }

    [Serializable]
    public class Feature
    {
        public const string Supported = "supported", Unsupported = "unsupported", Emulated = "emulated";
        public string value;
        public string raw;
        public Feature() { }
        public Feature(string value, string raw = "") { this.value = value; this.raw = raw; }
        public static Feature From(bool b, string raw = "") => new Feature(b ? Supported : Unsupported, raw);
        public override string ToString() => string.IsNullOrEmpty(raw) ? value : $"{value} ({raw})";
    }

    [Serializable]
    public class FeatureSet
    {
        public Feature bindless, raytracing, vrs, meshShader, shaderModel;
        // Project 3 adds: dlss, fsr, metalfx (schema stays 1 while only adding optional fields).
    }

    [Serializable]
    public class UnityInfo
    {
        public string graphicsDeviceType;
        public string graphicsDeviceVersion;
        public int graphicsShaderLevel;
        public int graphicsMemorySize;
        public int maxTextureSize;
        public bool supportsComputeShaders;
        public bool supportsRayTracing;
        public string unityVersion;
    }

    public static class Vendors
    {
        public const int NVIDIA = 0x10DE, AMD = 0x1002, Intel = 0x8086, Apple = 0x106B;
    }
}
