// D3D12 backend. Windows only.
#include "CapsReport.h"

#include <d3d12.h>
#include <dxgi1_6.h>
#include <windows.h>
#include <wrl/client.h>

#pragma comment(lib, "dxgi.lib")

using Microsoft::WRL::ComPtr;

namespace gpucaps {

static std::string Narrow(const wchar_t* w) {
    int n = WideCharToMultiByte(CP_UTF8, 0, w, -1, nullptr, 0, nullptr, nullptr);
    std::string s(n > 0 ? n - 1 : 0, '\0');
    if (n > 1) WideCharToMultiByte(CP_UTF8, 0, w, -1, s.data(), n, nullptr, nullptr);
    return s;
}

// The UMD (user-mode driver) version DXGI reports, e.g. 32.0.15.6636.
// NVIDIA's marketing version is the last 5 digits: (15 % 10) * 10000 + 6636 = 56636 -> "566.36".
static void DriverVersion(IDXGIAdapter* adapter, UINT vendor, Report& r) {
    LARGE_INTEGER umd{};
    if (FAILED(adapter->CheckInterfaceSupport(__uuidof(IDXGIDevice), &umd))) return;
    const uint64_t v = static_cast<uint64_t>(umd.QuadPart);
    const unsigned a = (v >> 48) & 0xFFFF, b = (v >> 32) & 0xFFFF, c = (v >> 16) & 0xFFFF, d = v & 0xFFFF;
    char buf[64];
    if (vendor == 0x10DE) {
        unsigned nv = (c % 10) * 10000 + d;
        std::snprintf(buf, sizeof buf, "%u.%02u", nv / 100, nv % 100);
    } else {
        std::snprintf(buf, sizeof buf, "%u.%u.%u.%u", a, b, c, d);
    }
    r.driverVersion = buf;
    r.driverRaw = v;
}

static std::string ShaderModelName(D3D_SHADER_MODEL sm) {
    char buf[16];
    std::snprintf(buf, sizeof buf, "SM %d.%d", (sm >> 4) & 0xF, sm & 0xF);
    return buf;
}

void CollectD3D12(void* devicePtr, Report& r) {
    r.api = "Direct3D12";
    auto* device = static_cast<ID3D12Device*>(devicePtr);
    if (!device) { r.error = "ID3D12Device is null"; return; }

    // Device -> adapter: the device remembers which adapter (by LUID) created it.
    ComPtr<IDXGIFactory4> factory;
    ComPtr<IDXGIAdapter1> adapter;
    if (SUCCEEDED(CreateDXGIFactory1(IID_PPV_ARGS(&factory))) &&
        SUCCEEDED(factory->EnumAdapterByLuid(device->GetAdapterLuid(), IID_PPV_ARGS(&adapter)))) {
        DXGI_ADAPTER_DESC1 desc{};
        adapter->GetDesc1(&desc);
        r.gpuName = Narrow(desc.Description);
        r.vendorId = desc.VendorId;
        r.dedicatedMemoryMB = desc.DedicatedVideoMemory / (1024ull * 1024ull);
        DriverVersion(adapter.Get(), desc.VendorId, r);
    } else {
        r.error = "cannot resolve DXGI adapter";
    }

    OSVERSIONINFOW os{};  // GetVersionEx lies without a manifest; RtlGetVersion does not.
    os.dwOSVersionInfoSize = sizeof os;
    using RtlGetVersionFn = LONG(WINAPI*)(OSVERSIONINFOW*);
    if (auto fn = reinterpret_cast<RtlGetVersionFn>(GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "RtlGetVersion"))) fn(&os);
    char osbuf[64];
    std::snprintf(osbuf, sizeof osbuf, "Windows %s (%lu.%lu.%lu)", os.dwBuildNumber >= 22000 ? "11" : "10",
                  os.dwMajorVersion, os.dwMinorVersion, os.dwBuildNumber);
    r.os = osbuf;

    // Each CheckFeatureSupport struct is a newer "options" block; a zeroed struct on failure
    // means the runtime is too old to know about the feature -> unsupported.
    D3D12_FEATURE_DATA_D3D12_OPTIONS o0{};
    device->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS, &o0, sizeof o0);
    // Tier 3 = fully bindless (unbounded descriptor tables of every type).
    r.Set("bindless", Supported(o0.ResourceBindingTier >= D3D12_RESOURCE_BINDING_TIER_3),
          "ResourceBindingTier" + std::to_string(o0.ResourceBindingTier));

    D3D12_FEATURE_DATA_D3D12_OPTIONS5 o5{};
    device->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS5, &o5, sizeof o5);
    // Pascal (GTX 10xx) exposes DXR tier 1.0 through a driver compute path, so a DXR tier on a
    // GPU without RT cores is "emulated". We can only infer that from the vendor + name.
    if (o5.RaytracingTier == D3D12_RAYTRACING_TIER_NOT_SUPPORTED) {
        r.Set("raytracing", "unsupported", "RaytracingTier0");
    } else {
        bool pascal = r.vendorId == 0x10DE && r.gpuName.find("GTX 10") != std::string::npos;
        r.Set("raytracing", pascal ? "emulated" : "supported", "RaytracingTier" + std::to_string(o5.RaytracingTier));
    }

    D3D12_FEATURE_DATA_D3D12_OPTIONS6 o6{};
    device->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS6, &o6, sizeof o6);
    r.Set("vrs", Supported(o6.VariableShadingRateTier != D3D12_VARIABLE_SHADING_RATE_TIER_NOT_SUPPORTED),
          "VariableShadingRateTier" + std::to_string(o6.VariableShadingRateTier));

    D3D12_FEATURE_DATA_D3D12_OPTIONS7 o7{};
    device->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS7, &o7, sizeof o7);
    r.Set("meshShader", Supported(o7.MeshShaderTier != D3D12_MESH_SHADER_TIER_NOT_SUPPORTED),
          "MeshShaderTier" + std::to_string(o7.MeshShaderTier));

    // Shader model: ask for the highest we know; the runtime lowers it to what it supports.
    // If the runtime does not know the value at all it returns E_INVALIDARG, so step down.
    const D3D_SHADER_MODEL candidates[] = {D3D_SHADER_MODEL_6_8, D3D_SHADER_MODEL_6_7, D3D_SHADER_MODEL_6_6,
                                           D3D_SHADER_MODEL_6_5, D3D_SHADER_MODEL_6_0, D3D_SHADER_MODEL_5_1};
    for (D3D_SHADER_MODEL c : candidates) {
        D3D12_FEATURE_DATA_SHADER_MODEL sm{c};
        if (SUCCEEDED(device->CheckFeatureSupport(D3D12_FEATURE_SHADER_MODEL, &sm, sizeof sm))) {
            r.Set("shaderModel", ShaderModelName(sm.HighestShaderModel), "0x" + [&] {
                char b[8]; std::snprintf(b, sizeof b, "%X", sm.HighestShaderModel); return std::string(b); }());
            break;
        }
    }
}

}  // namespace gpucaps
