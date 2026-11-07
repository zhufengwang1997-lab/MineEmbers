// CapsReport: the API-neutral result of a capability query, plus JSON output.
// Each backend (D3D12 / Vulkan / Metal) fills one of these; GpuCaps.cpp serializes it.
#pragma once

#include <cstdint>
#include <cstdio>
#include <string>
#include <utility>
#include <vector>

namespace gpucaps {

constexpr int kSchemaVersion = 1;

// "supported" | "unsupported" | "emulated", or a free-form value for shaderModel.
struct Feature {
    std::string value;
    std::string raw;  // the native tier/enum the value was derived from, for the matrix tooltip
};

struct Report {
    std::string api;            // "Direct3D12" | "Vulkan" | "Metal"
    std::string gpuName;
    uint32_t vendorId = 0;      // PCI vendor: 0x10DE NVIDIA, 0x1002 AMD, 0x8086 Intel, 0x106B Apple
    uint64_t dedicatedMemoryMB = 0;
    std::string driverVersion;  // human-readable
    uint64_t driverRaw = 0;     // as reported by the API
    std::string os;
    std::vector<std::pair<std::string, Feature>> features;  // ordered
    std::string error;          // non-empty if the query failed part-way

    void Set(const std::string& key, std::string value, std::string raw = {}) {
        for (auto& f : features)
            if (f.first == key) { f.second = {std::move(value), std::move(raw)}; return; }
        features.push_back({key, {std::move(value), std::move(raw)}});
    }
};

inline const char* Supported(bool b) { return b ? "supported" : "unsupported"; }

inline std::string JsonEscape(const std::string& s) {
    std::string o;
    o.reserve(s.size() + 2);
    for (unsigned char c : s) {
        switch (c) {
            case '"': o += "\\\""; break;
            case '\\': o += "\\\\"; break;
            case '\n': o += "\\n"; break;
            case '\r': o += "\\r"; break;
            case '\t': o += "\\t"; break;
            default:
                if (c < 0x20) { char b[8]; std::snprintf(b, sizeof b, "\\u%04x", c); o += b; }
                else o += static_cast<char>(c);
        }
    }
    return o;
}

// The native part of the report. The C# side adds the "unity" block from SystemInfo.
inline std::string ToJson(const Report& r) {
    auto q = [](const std::string& s) { return "\"" + JsonEscape(s) + "\""; };
    std::string j = "{\n";
    j += "  \"schema\": " + std::to_string(kSchemaVersion) + ",\n";
    j += "  \"api\": " + q(r.api) + ",\n";
    j += "  \"gpu\": { \"name\": " + q(r.gpuName) + ", \"vendorId\": " + std::to_string(r.vendorId) +
         ", \"dedicatedMemoryMB\": " + std::to_string(r.dedicatedMemoryMB) + " },\n";
    j += "  \"driver\": { \"version\": " + q(r.driverVersion) + ", \"raw\": " + std::to_string(r.driverRaw) + " },\n";
    j += "  \"os\": " + q(r.os) + ",\n";
    j += "  \"features\": {";
    for (size_t i = 0; i < r.features.size(); ++i) {
        const auto& f = r.features[i];
        j += (i ? ",\n    " : "\n    ") + q(f.first) + ": { \"value\": " + q(f.second.value) +
             ", \"raw\": " + q(f.second.raw) + " }";
    }
    j += "\n  }";
    if (!r.error.empty()) j += ",\n  \"error\": " + q(r.error);
    j += "\n}\n";
    return j;
}

// Backends. Each takes the native device handle Unity gives us (as void*) and fills `out`.
// Defined in CapsD3D12.cpp / CapsVulkan.cpp / CapsMetal.mm; only the ones for the
// current platform are compiled in.
void CollectD3D12(void* id3d12Device, Report& out);
void CollectVulkan(const void* unityVulkanInstance, Report& out);
void CollectMetal(void* mtlDevice, Report& out);

}  // namespace gpucaps
