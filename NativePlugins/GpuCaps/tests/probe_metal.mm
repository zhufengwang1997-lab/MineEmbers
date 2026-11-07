// Standalone check of the Metal backend without Unity: prints the native JSON.
#include "../src/CapsReport.h"
#import <Metal/Metal.h>
int main() {
    id<MTLDevice> d = MTLCreateSystemDefaultDevice();
    gpucaps::Report r;
    gpucaps::CollectMetal((__bridge void*)d, r);
    std::fputs(gpucaps::ToJson(r).c_str(), stdout);
    return r.error.empty() ? 0 : 1;
}
