cmake_minimum_required(VERSION 3.20)
project(GpuCaps LANGUAGES CXX)

# Unity's plugin headers ship inside the editor: <Unity>/Contents/PluginAPI (mac) or
# <Unity>/Editor/Data/PluginAPI (Windows). Pass -DUNITY_PLUGIN_API=<that folder>.
set(UNITY_PLUGIN_API "" CACHE PATH "Path to Unity's PluginAPI headers")
if(NOT EXISTS "${UNITY_PLUGIN_API}/IUnityGraphics.h")
  message(FATAL_ERROR "Set UNITY_PLUGIN_API to the editor's PluginAPI folder (got '${UNITY_PLUGIN_API}')")
endif()

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)   # only UNITY_INTERFACE_EXPORT symbols are visible

set(OUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../Packages/com.zfw.gpucaps/Plugins")

if(APPLE)
  enable_language(OBJCXX)
  # A MODULE + BUNDLE is what Unity loads on macOS (GpuCaps.bundle).
  add_library(GpuCaps MODULE src/GpuCaps.cpp src/CapsMetal.mm src/UnityMetalBridge.mm)
  set_target_properties(GpuCaps PROPERTIES
    BUNDLE TRUE
    BUNDLE_EXTENSION bundle
    MACOSX_BUNDLE_BUNDLE_NAME GpuCaps
    MACOSX_BUNDLE_GUI_IDENTIFIER com.zfw.gpucaps
    OSX_ARCHITECTURES "arm64;x86_64"
    LIBRARY_OUTPUT_DIRECTORY "${OUT_DIR}/macOS")
  set_source_files_properties(src/CapsMetal.mm src/UnityMetalBridge.mm PROPERTIES COMPILE_FLAGS "-fobjc-arc")
  target_link_libraries(GpuCaps PRIVATE "-framework Metal" "-framework Foundation")
elseif(WIN32)
  find_package(Vulkan REQUIRED COMPONENTS Headers)  # headers only; entry points come from Unity
  add_library(GpuCaps SHARED src/GpuCaps.cpp src/CapsD3D12.cpp src/CapsVulkan.cpp)
  target_compile_definitions(GpuCaps PRIVATE GPUCAPS_HAS_UNITY_HEADERS NOMINMAX WIN32_LEAN_AND_MEAN)
  target_link_libraries(GpuCaps PRIVATE Vulkan::Headers dxgi d3d12)
  set_target_properties(GpuCaps PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${OUT_DIR}/Windows"
    RUNTIME_OUTPUT_DIRECTORY_RELWITHDEBINFO "${OUT_DIR}/Windows")
  if(MSVC)
    target_compile_options(GpuCaps PRIVATE /W4 /utf-8)
  endif()
endif()

target_include_directories(GpuCaps PRIVATE "${UNITY_PLUGIN_API}" src)
