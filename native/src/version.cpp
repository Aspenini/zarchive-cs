#include "bridge_common.h"

#ifndef ZAR_DOTNET_UPSTREAM_VERSION
#define ZAR_DOTNET_UPSTREAM_VERSION "unknown"
#endif

extern "C" ZAR_DOTNET_API uint32_t zar_dotnet_abi_version(void)
{
    return ZAR_DOTNET_ABI_VERSION;
}

extern "C" ZAR_DOTNET_API const char* zar_dotnet_upstream_version(void)
{
    return ZAR_DOTNET_UPSTREAM_VERSION;
}
