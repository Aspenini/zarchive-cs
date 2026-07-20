#include "bridge_common.h"

#include <cstring>

namespace zar_bridge
{

namespace
{
thread_local std::string t_lastError;
}

void set_error(std::string message)
{
    t_lastError = std::move(message);
}

void clear_error()
{
    t_lastError.clear();
}

const std::string& last_error()
{
    return t_lastError;
}

} // namespace zar_bridge

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_get_last_error(
    char* buffer,
    size_t buffer_length,
    size_t* required_length)
{
    const std::string& message = zar_bridge::last_error();
    const size_t required = message.size() + 1; // include NUL
    if (required_length != nullptr)
        *required_length = required;
    if (buffer == nullptr || buffer_length < required)
        return ZAR_DOTNET_BUFFER_TOO_SMALL;
    std::memcpy(buffer, message.c_str(), required);
    return ZAR_DOTNET_OK;
}
