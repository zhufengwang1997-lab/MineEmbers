// Standalone check of the Vulkan backend: creates its own instance (Unity does this in-game)
// and runs the same collection code. On macOS this goes through MoltenVK.
#define VK_NO_PROTOTYPES
#include <vulkan/vulkan.h>
#include "../src/CapsReport.h"
#include <dlfcn.h>
#include <vector>

namespace gpucaps {
struct VulkanHandles { VkInstance instance; VkPhysicalDevice physicalDevice; PFN_vkGetInstanceProcAddr getInstanceProcAddr; };
void CollectVulkanHandles(const VulkanHandles&, Report&);
std::string DecodeDriverVersion(uint32_t vendor, uint32_t v);
}

int main() {
    // Self-check of the vendor decoding, using the bit patterns the drivers report.
    uint32_t nv = (566u << 22) | (36u << 14);
    if (gpucaps::DecodeDriverVersion(0x10DE, nv) != "566.36") { std::puts("NVIDIA decode FAILED"); return 2; }
    if (gpucaps::DecodeDriverVersion(0x8086, (101u << 14) | 6130u) != "101.6130") { std::puts("Intel decode FAILED"); return 2; }

    void* lib = dlopen("libvulkan.1.dylib", RTLD_NOW);
    if (!lib) lib = dlopen("/opt/homebrew/lib/libvulkan.1.dylib", RTLD_NOW);
    if (!lib) { std::puts("no Vulkan loader"); return 1; }
    auto gipa = reinterpret_cast<PFN_vkGetInstanceProcAddr>(dlsym(lib, "vkGetInstanceProcAddr"));
    auto create = reinterpret_cast<PFN_vkCreateInstance>(gipa(nullptr, "vkCreateInstance"));

    const char* ext[] = {VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME, "VK_KHR_get_physical_device_properties2"};
    VkApplicationInfo app{};
    app.sType = VK_STRUCTURE_TYPE_APPLICATION_INFO;
    app.apiVersion = VK_API_VERSION_1_3;
    VkInstanceCreateInfo ci{};
    ci.sType = VK_STRUCTURE_TYPE_INSTANCE_CREATE_INFO;
    ci.flags = VK_INSTANCE_CREATE_ENUMERATE_PORTABILITY_BIT_KHR;
    ci.pApplicationInfo = &app;
    ci.enabledExtensionCount = 2;
    ci.ppEnabledExtensionNames = ext;
    VkInstance inst{};
    if (create(&ci, nullptr, &inst) != VK_SUCCESS) { std::puts("vkCreateInstance failed"); return 1; }
    auto enumerate = reinterpret_cast<PFN_vkEnumeratePhysicalDevices>(gipa(inst, "vkEnumeratePhysicalDevices"));
    uint32_t n = 0; enumerate(inst, &n, nullptr);
    std::vector<VkPhysicalDevice> pds(n); enumerate(inst, &n, pds.data());
    if (!n) { std::puts("no physical device"); return 1; }

    gpucaps::Report r;
    gpucaps::CollectVulkanHandles({inst, pds[0], gipa}, r);
    std::fputs(gpucaps::ToJson(r).c_str(), stdout);
    reinterpret_cast<PFN_vkDestroyInstance>(gipa(inst, "vkDestroyInstance"))(inst, nullptr);
    return r.error.empty() ? 0 : 1;
}
