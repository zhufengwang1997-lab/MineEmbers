// IUnityGraphicsMetal.h declares id<MTLDevice> in its interface, so it can only be included
// from Objective-C++. This file is the one place that does, and hands the device back as void*.
#import <Metal/Metal.h>
#include "IUnityInterface.h"
#include "IUnityGraphicsMetal.h"

namespace gpucaps {
void* UnityMetalDevice(IUnityInterfaces* ifs) {
    IUnityGraphicsMetal* m = ifs ? ifs->Get<IUnityGraphicsMetal>() : nullptr;
    return m ? (__bridge void*)m->MetalDevice() : nullptr;
}
}  // namespace gpucaps
