// IUnityGraphicsMetal*.h declares id<MTLDevice> in its interface, so it can only be included
// from Objective-C++. This file is the one place that does, and hands the device back as void*.
#import <Metal/Metal.h>
#include "IUnityInterface.h"
#include "IUnityGraphicsMetal.h"

namespace gpucaps {
// Prefer the versioned interfaces (the unversioned one is deprecated); fall back for older players.
void* UnityMetalDevice(IUnityInterfaces* ifs) {
    if (!ifs) return nullptr;
    if (auto* m = ifs->Get<IUnityGraphicsMetalV2>()) return (__bridge void*)m->MetalDevice();
    if (auto* m = ifs->Get<IUnityGraphicsMetalV1>()) return (__bridge void*)m->MetalDevice();
    if (auto* m = ifs->Get<IUnityGraphicsMetal>()) return (__bridge void*)m->MetalDevice();
    return nullptr;
}
}  // namespace gpucaps
