#pragma once

#include "zarchive_dotnet.h"

#include <filesystem>
#include <new>
#include <stdexcept>
#include <string>
#include <string_view>

namespace zar_bridge
{

void set_error(std::string message);
void clear_error();
const std::string& last_error();

inline std::filesystem::path utf8_to_path(const char* utf8)
{
    return std::filesystem::path(
        std::u8string(reinterpret_cast<const char8_t*>(utf8)));
}

} // namespace zar_bridge

/* Every exported function body runs inside this guard so no C++ exception
 * can cross the ABI boundary. */
#define ZAR_BRIDGE_CATCH_ALL                                                   \
    catch (const std::bad_alloc&)                                              \
    {                                                                          \
        zar_bridge::set_error("Native allocation failed");                     \
        return ZAR_DOTNET_OUT_OF_MEMORY;                                       \
    }                                                                          \
    catch (const std::exception& ex)                                           \
    {                                                                          \
        zar_bridge::set_error(ex.what());                                      \
        return ZAR_DOTNET_INTERNAL_ERROR;                                      \
    }                                                                          \
    catch (...)                                                                \
    {                                                                          \
        zar_bridge::set_error("Unknown native exception");                     \
        return ZAR_DOTNET_INTERNAL_ERROR;                                      \
    }
