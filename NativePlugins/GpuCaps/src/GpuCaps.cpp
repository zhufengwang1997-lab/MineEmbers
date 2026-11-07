// Unity native plugin shell (FR-CAP-01).
//
// Lifecycle:
//   UnityPluginLoad      -> grab IUnityGraphics, register the device-event callback.
//                           Do NOT touch the device here: on a cold start it may not exist yet.
//   kUnityGfxDeviceEventInitialize -> GetRenderer() tells us the API; fetch the native device
//                           through the matching IUnityGraphicsXxx interface and collect.
//   kUnityGfxDeviceEventShutdown   -> drop the cached report.
//
// C# reads the cached JSON with GpuCaps_GetReportJson().
#include "CapsReport.h"

#include "IUnityGraphics.h"
#include "IUnityInterface.h"

#if defined(_WIN32)
#include <d3d12.h>
#include "IUnityGraphicsD3D12.h"
#define VK_NO_PROTOTYPES
#include <vulkan/vulkan.h>
#include "IUnityGraphicsVulkan.h"
#endif

#include <mutex>
#include <string>

namespace gpucaps {
#if defined(__APPLE__)
void* UnityMetalDevice(IUnityInterfaces* ifs);  // UnityMetalBridge.mm (needs Objective-C)
#endif
}  // namespace gpucaps

namespace {

IUnityInterfaces* g_ifs = nullptr;
IUnityGraphics* g_gfx = nullptr;
std::mutex g_mu;
std::string g_json;    // empty until the device is initialized
int g_renderer = -1;   // UnityGfxRenderer, for diagnostics

void Collect() {
    gpucaps::Report r;
    const UnityGfxRenderer api = g_gfx->GetRenderer();
    switch (api) {
#if defined(_WIN32)
        case kUnityGfxRendererD3D12:
            if (auto* d3d = g_ifs->Get<IUnityGraphicsD3D12v7>()) gpucaps::CollectD3D12(d3d->GetDevice(), r);
            else r.error = "IUnityGraphicsD3D12v7 unavailable";
            break;
        case kUnityGfxRendererVulkan:
            if (auto* vk = g_ifs->Get<IUnityGraphicsVulkan>()) {
                UnityVulkanInstance inst = vk->Instance();
                gpucaps::CollectVulkan(&inst, r);
            } else {
                r.error = "IUnityGraphicsVulkan unavailable";
            }
            break;
#endif
#if defined(__APPLE__)
        case kUnityGfxRendererMetal:
            gpucaps::CollectMetal(gpucaps::UnityMetalDevice(g_ifs), r);
            break;
#endif
        default:
            // D3D11 / OpenGL / Null: leave api empty, C# falls back to SystemInfo.
            r.error = "renderer " + std::to_string(static_cast<int>(api)) + " not handled by GpuCaps";
            break;
    }
    std::lock_guard<std::mutex> lk(g_mu);
    g_renderer = static_cast<int>(api);
    g_json = gpucaps::ToJson(r);
}

void UNITY_INTERFACE_API OnDeviceEvent(UnityGfxDeviceEventType e) {
    if (e == kUnityGfxDeviceEventInitialize) {
        Collect();
    } else if (e == kUnityGfxDeviceEventShutdown) {
        std::lock_guard<std::mutex> lk(g_mu);
        g_json.clear();
        g_renderer = -1;
    }
}

}  // namespace

extern "C" {

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* ifs) {
    g_ifs = ifs;
    g_gfx = ifs->Get<IUnityGraphics>();
    g_gfx->RegisterDeviceEventCallback(OnDeviceEvent);
    // If the plugin is loaded after the device already exists (the usual case for a plugin
    // first touched by a [DllImport] call), no Initialize event will come, so run it now.
    OnDeviceEvent(kUnityGfxDeviceEventInitialize);
}

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginUnload() {
    if (g_gfx) g_gfx->UnregisterDeviceEventCallback(OnDeviceEvent);
    g_gfx = nullptr;
    g_ifs = nullptr;
}

// Returned pointer stays valid until the next device event; C# copies it immediately.
UNITY_INTERFACE_EXPORT const char* UNITY_INTERFACE_API GpuCaps_GetReportJson() {
    std::lock_guard<std::mutex> lk(g_mu);
    return g_json.empty() ? nullptr : g_json.c_str();
}

UNITY_INTERFACE_EXPORT int UNITY_INTERFACE_API GpuCaps_GetRendererId() {
    std::lock_guard<std::mutex> lk(g_mu);
    return g_renderer;
}

}  // extern "C"
