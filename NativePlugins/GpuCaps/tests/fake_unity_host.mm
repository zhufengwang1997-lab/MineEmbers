// Loads GpuCaps.bundle exactly like Unity does (dlopen + UnityPluginLoad with an IUnityInterfaces
// registry), pretending to be a Metal player. Exercises the full lifecycle without the editor.
#import <Metal/Metal.h>
#include <dlfcn.h>
#include <cstdio>
#include <cstring>
#include "IUnityInterface.h"
#include "IUnityGraphics.h"
#include "IUnityGraphicsMetal.h"

static IUnityGraphicsDeviceEventCallback g_cb = nullptr;
static UnityGfxRenderer g_renderer = kUnityGfxRendererNull;
static id<MTLDevice> g_dev;

static UnityGfxRenderer GetRenderer() { return g_renderer; }
static void Reg(IUnityGraphicsDeviceEventCallback cb) { g_cb = cb; }
static void Unreg(IUnityGraphicsDeviceEventCallback) { g_cb = nullptr; }
static int ReserveEvt(int) { return 0; }
static IUnityGraphics gfx{};

static id<MTLDevice> MetalDevice() { return g_dev; }
static IUnityGraphicsMetalV2 metal{};  // what current players expose

static IUnityInterface* GetIf(UnityInterfaceGUID g) {
    if (g == UNITY_GET_INTERFACE_GUID(IUnityGraphics)) return reinterpret_cast<IUnityInterface*>(&gfx);
    if (g == UNITY_GET_INTERFACE_GUID(IUnityGraphicsMetalV2)) return reinterpret_cast<IUnityInterface*>(&metal);
    return nullptr;
}
static IUnityInterface* GetIfSplit(unsigned long long h, unsigned long long l) { return GetIf(UnityInterfaceGUID(h, l)); }
static void RegIf(UnityInterfaceGUID, IUnityInterface*) {}
static void RegIfSplit(unsigned long long, unsigned long long, IUnityInterface*) {}

int main(int argc, char** argv) {
    gfx.GetRenderer = GetRenderer; gfx.RegisterDeviceEventCallback = Reg;
    gfx.UnregisterDeviceEventCallback = Unreg; gfx.ReserveEventIDRange = ReserveEvt;
    metal.MetalDevice = MetalDevice;
    IUnityInterfaces ifs{};
    ifs.GetInterface = GetIf; ifs.RegisterInterface = RegIf;
    ifs.GetInterfaceSplit = GetIfSplit; ifs.RegisterInterfaceSplit = RegIfSplit;

    void* h = dlopen(argv[1], RTLD_NOW);
    if (!h) { std::printf("dlopen failed: %s\n", dlerror()); return 1; }
    auto load = (void (*)(IUnityInterfaces*))dlsym(h, "UnityPluginLoad");
    auto unload = (void (*)())dlsym(h, "UnityPluginUnload");
    auto json = (const char* (*)())dlsym(h, "GpuCaps_GetReportJson");
    int fail = 0;

    // 1) Plugin loaded before the device exists (Null renderer): must not crash, must not report Metal.
    load(&ifs);
    const char* j = json();
    std::printf("[before device] %s\n", j && std::strstr(j, "not handled") ? "ok (error report, C# falls back)" : "UNEXPECTED");
    fail += !(j && std::strstr(j, "not handled"));

    // 2) Device created -> Initialize event.
    g_dev = MTLCreateSystemDefaultDevice(); g_renderer = kUnityGfxRendererMetal;
    g_cb(kUnityGfxDeviceEventInitialize);
    j = json();
    bool ok = j && std::strstr(j, "\"api\": \"Metal\"") && !std::strstr(j, "\"error\"");
    std::printf("[initialize]    %s\n", ok ? "ok" : "FAILED"); fail += !ok;
    if (argc > 2) std::fputs(j, stdout);

    // 3) Shutdown clears the cache; unload unregisters.
    g_cb(kUnityGfxDeviceEventShutdown);
    std::printf("[shutdown]      %s\n", json() == nullptr ? "ok" : "FAILED"); fail += json() != nullptr;
    unload();
    std::printf("[unload]        %s\n", g_cb == nullptr ? "ok" : "FAILED"); fail += g_cb != nullptr;
    return fail;
}
