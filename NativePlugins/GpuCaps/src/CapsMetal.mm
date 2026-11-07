// Metal backend. Compiled only on Apple platforms (Objective-C++).
#include "CapsReport.h"

#import <Foundation/Foundation.h>
#import <Metal/Metal.h>

namespace gpucaps {

static std::string HighestFamily(id<MTLDevice> d) {
    // Ordered newest -> oldest; the first one the device supports wins.
    struct { MTLGPUFamily f; const char* name; } apple[] = {
        {MTLGPUFamilyApple10, "Apple10"}, {MTLGPUFamilyApple9, "Apple9"}, {MTLGPUFamilyApple8, "Apple8"}, {MTLGPUFamilyApple7, "Apple7"},
        {MTLGPUFamilyApple6, "Apple6"}, {MTLGPUFamilyMac2, "Mac2"},
    };
    std::string fam = "unknown";
    for (auto& a : apple)
        if ([d supportsFamily:a.f]) { fam = a.name; break; }
    std::string metal = "Metal";
    if ([d supportsFamily:MTLGPUFamilyMetal3]) metal = "Metal 3";
    if (@available(macOS 26.0, iOS 26.0, *)) {
        if ([d supportsFamily:MTLGPUFamilyMetal4]) metal = "Metal 4";
    }
    return metal + " / " + fam;
}

void CollectMetal(void* mtlDevice, Report& r) {
    @autoreleasepool {
        id<MTLDevice> d = (__bridge id<MTLDevice>)mtlDevice;
        r.api = "Metal";
        if (!d) { r.error = "MTLDevice is null"; return; }

        r.gpuName = [d.name UTF8String];
        // Apple GPUs report no PCI vendor; AMD/Intel GPUs on Intel Macs do via the registry,
        // but Unity 6 on Apple silicon is what we target, so treat everything as Apple unless named.
        NSString* n = d.name;
        r.vendorId = [n containsString:@"AMD"] ? 0x1002 : [n containsString:@"Intel"] ? 0x8086 : 0x106B;
        // Unified memory: there is no "dedicated" VRAM. recommendedMaxWorkingSetSize is the
        // budget Metal suggests for GPU-resident resources, which is the closest analogue.
        r.dedicatedMemoryMB = d.recommendedMaxWorkingSetSize / (1024ull * 1024ull);

        NSOperatingSystemVersion v = [NSProcessInfo processInfo].operatingSystemVersion;
        char buf[64];
        std::snprintf(buf, sizeof buf, "%ld.%ld.%ld", (long)v.majorVersion, (long)v.minorVersion, (long)v.patchVersion);
        r.driverVersion = buf;  // Metal driver ships with the OS
        r.driverRaw = (uint64_t)v.majorVersion * 10000 + v.minorVersion * 100 + v.patchVersion;
        r.os = std::string("macOS ") + [[NSProcessInfo processInfo].operatingSystemVersionString UTF8String];

        const bool apple9 = [d supportsFamily:MTLGPUFamilyApple9];  // M3 and newer

        // Bindless: Argument Buffers Tier 2 allows large, GPU-writable resource tables.
        MTLArgumentBuffersTier abt = d.argumentBuffersSupport;
        r.Set("bindless", Supported(abt == MTLArgumentBuffersTier2), abt == MTLArgumentBuffersTier2 ? "ArgumentBuffersTier2" : "ArgumentBuffersTier1");

        // Ray tracing: the API is available from Apple7 (M1), but hardware RT units arrived
        // with Apple9 (M3). Before that Metal runs intersection in compute -> "emulated".
        if (!d.supportsRaytracing) r.Set("raytracing", "unsupported", "supportsRaytracing=NO");
        else r.Set("raytracing", apple9 ? "supported" : "emulated", apple9 ? "hardware (Apple9)" : "software (pre-Apple9)");

        // VRS: Metal's equivalent is the Rasterization Rate Map (screen-space variable rate).
        r.Set("vrs", Supported([d supportsRasterizationRateMapWithLayerCount:1]), "RasterizationRateMap");

        // Mesh shaders: Metal 3 object/mesh pipelines.
        r.Set("meshShader", Supported([d supportsFamily:MTLGPUFamilyMetal3]), "MTLGPUFamilyMetal3");

        r.Set("shaderModel", HighestFamily(d), "");
    }
}

}  // namespace gpucaps
