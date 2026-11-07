// Vulkan backend. Windows (and Linux) only; Unity uses Metal on macOS.
//
// We do not link vulkan-1.lib: Unity hands us vkGetInstanceProcAddr, and every other entry
// point is fetched through it. That keeps the plugin loadable when Unity runs on D3D12.
#define VK_NO_PROTOTYPES
#include <vulkan/vulkan.h>

#include "CapsReport.h"

#include <cstring>
#include <vector>

#ifdef GPUCAPS_HAS_UNITY_HEADERS
#include "IUnityGraphicsVulkan.h"
#endif

namespace gpucaps {

// Mirrors the fields of UnityVulkanInstance we need, so this file can also be compiled and
// tested without the Unity headers (tests pass a VulkanHandles directly).
struct VulkanHandles {
    VkInstance instance;
    VkPhysicalDevice physicalDevice;
    PFN_vkGetInstanceProcAddr getInstanceProcAddr;
};

// VkPhysicalDeviceProperties::driverVersion is vendor-encoded.
std::string DecodeDriverVersion(uint32_t vendor, uint32_t v) {
    char buf[48];
    if (vendor == 0x10DE) {  // NVIDIA: 10.8.8.6 bits
        std::snprintf(buf, sizeof buf, "%u.%02u", (v >> 22) & 0x3FF, (v >> 14) & 0xFF);
    } else if (vendor == 0x8086) {  // Intel on Windows: 18.14
        std::snprintf(buf, sizeof buf, "%u.%u", v >> 14, v & 0x3FFF);
    } else {  // everyone else follows VK_MAKE_API_VERSION
        std::snprintf(buf, sizeof buf, "%u.%u.%u", VK_API_VERSION_MAJOR(v), VK_API_VERSION_MINOR(v), VK_API_VERSION_PATCH(v));
    }
    return buf;
}

// Vulkan API version -> highest SPIR-V version it guarantees.
static const char* SpirvFor(uint32_t api) {
    uint32_t mi = VK_API_VERSION_MINOR(api);
    return mi >= 3 ? "1.6" : mi == 2 ? "1.5" : mi == 1 ? "1.3" : "1.0";
}

void CollectVulkanHandles(const VulkanHandles& h, Report& r) {
    r.api = "Vulkan";
    if (!h.instance || !h.physicalDevice || !h.getInstanceProcAddr) { r.error = "Vulkan handles are null"; return; }

#define LOAD(name) auto name = reinterpret_cast<PFN_##name>(h.getInstanceProcAddr(h.instance, #name))
    LOAD(vkGetPhysicalDeviceProperties);
    LOAD(vkGetPhysicalDeviceMemoryProperties);
    LOAD(vkEnumerateDeviceExtensionProperties);
    LOAD(vkGetPhysicalDeviceFeatures2);
#undef LOAD
    if (!vkGetPhysicalDeviceProperties || !vkGetPhysicalDeviceMemoryProperties || !vkEnumerateDeviceExtensionProperties) {
        r.error = "cannot load Vulkan entry points";
        return;
    }

    VkPhysicalDeviceProperties p{};
    vkGetPhysicalDeviceProperties(h.physicalDevice, &p);
    r.gpuName = p.deviceName;
    r.vendorId = p.vendorID;
    r.driverRaw = p.driverVersion;
    r.driverVersion = DecodeDriverVersion(p.vendorID, p.driverVersion);

    // "Dedicated" memory = sum of DEVICE_LOCAL heaps. On integrated GPUs that heap is system RAM.
    VkPhysicalDeviceMemoryProperties mp{};
    vkGetPhysicalDeviceMemoryProperties(h.physicalDevice, &mp);
    uint64_t local = 0;
    for (uint32_t i = 0; i < mp.memoryHeapCount; ++i)
        if (mp.memoryHeaps[i].flags & VK_MEMORY_HEAP_DEVICE_LOCAL_BIT) local += mp.memoryHeaps[i].size;
    r.dedicatedMemoryMB = local / (1024ull * 1024ull);

    uint32_t n = 0;
    vkEnumerateDeviceExtensionProperties(h.physicalDevice, nullptr, &n, nullptr);
    std::vector<VkExtensionProperties> exts(n);
    vkEnumerateDeviceExtensionProperties(h.physicalDevice, nullptr, &n, exts.data());
    auto has = [&](const char* e) {
        for (auto& x : exts) if (std::strcmp(x.extensionName, e) == 0) return true;
        return false;
    };

    // Bindless: descriptor indexing (core in 1.2) with runtime arrays + partially bound + update-after-bind.
    VkPhysicalDeviceDescriptorIndexingFeatures di{};
    di.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_DESCRIPTOR_INDEXING_FEATURES;
    VkPhysicalDeviceFeatures2 f2{};
    f2.sType = VK_STRUCTURE_TYPE_PHYSICAL_DEVICE_FEATURES_2;
    f2.pNext = &di;  // chain: the driver fills every struct linked through pNext
    if (vkGetPhysicalDeviceFeatures2) vkGetPhysicalDeviceFeatures2(h.physicalDevice, &f2);
    bool bindless = di.runtimeDescriptorArray && di.descriptorBindingPartiallyBound &&
                    di.shaderSampledImageArrayNonUniformIndexing && di.descriptorBindingSampledImageUpdateAfterBind;
    r.Set("bindless", Supported(bindless), bindless ? "descriptorIndexing" : (has("VK_EXT_descriptor_indexing") ? "descriptorIndexing(partial)" : ""));

    // NVIDIA does not expose VK_KHR_ray_tracing_pipeline on Pascal (unlike DXR) -> plain unsupported.
    r.Set("raytracing", Supported(has("VK_KHR_ray_tracing_pipeline")), has("VK_KHR_ray_tracing_pipeline") ? "VK_KHR_ray_tracing_pipeline" : "");
    r.Set("vrs", Supported(has("VK_KHR_fragment_shading_rate")), has("VK_KHR_fragment_shading_rate") ? "VK_KHR_fragment_shading_rate" : "");
    r.Set("meshShader", Supported(has("VK_EXT_mesh_shader")),
          has("VK_EXT_mesh_shader") ? "VK_EXT_mesh_shader" : (has("VK_NV_mesh_shader") ? "VK_NV_mesh_shader only" : ""));

    char sm[48];
    std::snprintf(sm, sizeof sm, "Vulkan %u.%u / SPIR-V %s", VK_API_VERSION_MAJOR(p.apiVersion), VK_API_VERSION_MINOR(p.apiVersion), SpirvFor(p.apiVersion));
    char raw[24];
    std::snprintf(raw, sizeof raw, "apiVersion=%u", p.apiVersion);
    r.Set("shaderModel", sm, raw);
}

#ifdef GPUCAPS_HAS_UNITY_HEADERS
void CollectVulkan(const void* unityVulkanInstance, Report& r) {
    const auto* u = static_cast<const UnityVulkanInstance*>(unityVulkanInstance);
    if (!u) { r.api = "Vulkan"; r.error = "UnityVulkanInstance is null"; return; }
    CollectVulkanHandles({u->instance, u->physicalDevice, u->getInstanceProcAddr}, r);
}
#endif

}  // namespace gpucaps
