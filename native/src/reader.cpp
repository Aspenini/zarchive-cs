#include "bridge_common.h"

#include <zarchive/zarchivereader.h>

#include <cstring>
#include <memory>

struct zar_dotnet_reader
{
    std::unique_ptr<ZArchiveReader> impl;
};

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_open_file(
    const char* path_utf8,
    zar_dotnet_reader** out_reader)
{
    zar_bridge::clear_error();
    if (out_reader != nullptr)
        *out_reader = nullptr;
    if (path_utf8 == nullptr || out_reader == nullptr)
    {
        zar_bridge::set_error("path and out_reader must not be null");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        const std::filesystem::path path = zar_bridge::utf8_to_path(path_utf8);
        std::error_code ec;
        if (!std::filesystem::exists(path, ec) || ec)
        {
            zar_bridge::set_error("Archive file does not exist");
            return ZAR_DOTNET_NOT_FOUND;
        }
        ZArchiveReader* impl = ZArchiveReader::OpenFromFile(path);
        if (impl == nullptr)
        {
            zar_bridge::set_error(
                "File is not a valid ZArchive or could not be opened");
            return ZAR_DOTNET_INVALID_ARCHIVE;
        }
        auto reader = std::make_unique<zar_dotnet_reader>();
        reader->impl.reset(impl);
        *out_reader = reader.release();
        return ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API void zar_dotnet_reader_destroy(
    zar_dotnet_reader* reader)
{
    delete reader;
}

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_lookup(
    zar_dotnet_reader* reader,
    const char* archive_path,
    size_t archive_path_length,
    int32_t allow_file,
    int32_t allow_directory,
    zar_dotnet_node* out_node)
{
    zar_bridge::clear_error();
    if (out_node != nullptr)
        *out_node = ZAR_DOTNET_INVALID_NODE;
    if (reader == nullptr || out_node == nullptr ||
        (archive_path == nullptr && archive_path_length != 0))
    {
        zar_bridge::set_error("Invalid argument passed to reader_lookup");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        const std::string_view path(
            archive_path_length != 0 ? archive_path : "", archive_path_length);
        const ZArchiveNodeHandle node = reader->impl->LookUp(
            path, allow_file != 0, allow_directory != 0);
        if (node == ZARCHIVE_INVALID_NODE)
        {
            zar_bridge::set_error("No matching entry in archive");
            return ZAR_DOTNET_NOT_FOUND;
        }
        *out_node = node;
        return ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_get_entry_type(
    zar_dotnet_reader* reader,
    zar_dotnet_node node,
    int32_t* out_is_file,
    int32_t* out_is_directory)
{
    zar_bridge::clear_error();
    if (reader == nullptr || out_is_file == nullptr ||
        out_is_directory == nullptr)
    {
        zar_bridge::set_error("Invalid argument passed to get_entry_type");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        const bool isFile = reader->impl->IsFile(node);
        const bool isDirectory = reader->impl->IsDirectory(node);
        if (!isFile && !isDirectory)
        {
            zar_bridge::set_error("Node handle is not valid for this archive");
            return ZAR_DOTNET_INVALID_NODE_STATUS;
        }
        *out_is_file = isFile ? 1 : 0;
        *out_is_directory = isDirectory ? 1 : 0;
        return ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_get_file_size(
    zar_dotnet_reader* reader,
    zar_dotnet_node node,
    uint64_t* out_size)
{
    zar_bridge::clear_error();
    if (reader == nullptr || out_size == nullptr)
    {
        zar_bridge::set_error("Invalid argument passed to get_file_size");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        if (reader->impl->IsDirectory(node))
        {
            zar_bridge::set_error("Node is a directory, not a file");
            return ZAR_DOTNET_NOT_A_FILE;
        }
        if (!reader->impl->IsFile(node))
        {
            zar_bridge::set_error("Node handle is not valid for this archive");
            return ZAR_DOTNET_INVALID_NODE_STATUS;
        }
        *out_size = reader->impl->GetFileSize(node);
        return ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_read_file(
    zar_dotnet_reader* reader,
    zar_dotnet_node node,
    uint64_t offset,
    void* buffer,
    size_t buffer_length,
    size_t* out_bytes_read)
{
    zar_bridge::clear_error();
    if (out_bytes_read != nullptr)
        *out_bytes_read = 0;
    if (reader == nullptr || out_bytes_read == nullptr ||
        (buffer == nullptr && buffer_length != 0))
    {
        zar_bridge::set_error("Invalid argument passed to read_file");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        if (!reader->impl->IsFile(node))
        {
            zar_bridge::set_error(
                reader->impl->IsDirectory(node)
                    ? "Node is a directory, not a file"
                    : "Node handle is not valid for this archive");
            return reader->impl->IsDirectory(node) ? ZAR_DOTNET_NOT_A_FILE
                                                   : ZAR_DOTNET_INVALID_NODE_STATUS;
        }
        if (buffer_length == 0)
            return ZAR_DOTNET_OK;
        const uint64_t bytesRead = reader->impl->ReadFromFile(
            node, offset, static_cast<uint64_t>(buffer_length), buffer);
        *out_bytes_read = static_cast<size_t>(bytesRead);
        return ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API zar_dotnet_status
zar_dotnet_reader_get_directory_count(
    zar_dotnet_reader* reader,
    zar_dotnet_node node,
    uint32_t* out_count)
{
    zar_bridge::clear_error();
    if (reader == nullptr || out_count == nullptr)
    {
        zar_bridge::set_error("Invalid argument passed to get_directory_count");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        if (reader->impl->IsFile(node))
        {
            zar_bridge::set_error("Node is a file, not a directory");
            return ZAR_DOTNET_NOT_A_DIRECTORY;
        }
        if (!reader->impl->IsDirectory(node))
        {
            zar_bridge::set_error("Node handle is not valid for this archive");
            return ZAR_DOTNET_INVALID_NODE_STATUS;
        }
        *out_count = reader->impl->GetDirEntryCount(node);
        return ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API zar_dotnet_status
zar_dotnet_reader_get_directory_entry(
    zar_dotnet_reader* reader,
    zar_dotnet_node directory,
    uint32_t index,
    char* name_buffer,
    size_t name_buffer_length,
    size_t* required_name_length,
    int32_t* out_is_file,
    int32_t* out_is_directory,
    uint64_t* out_size)
{
    zar_bridge::clear_error();
    if (reader == nullptr || required_name_length == nullptr ||
        out_is_file == nullptr || out_is_directory == nullptr ||
        out_size == nullptr)
    {
        zar_bridge::set_error("Invalid argument passed to get_directory_entry");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        ZArchiveReader::DirEntry entry{};
        if (!reader->impl->GetDirEntry(directory, index, entry))
        {
            if (!reader->impl->IsDirectory(directory))
            {
                zar_bridge::set_error("Node is not a valid directory");
                return reader->impl->IsFile(directory)
                           ? ZAR_DOTNET_NOT_A_DIRECTORY
                           : ZAR_DOTNET_INVALID_NODE_STATUS;
            }
            zar_bridge::set_error("Directory entry index is out of range");
            return ZAR_DOTNET_NOT_FOUND;
        }
        *out_is_file = entry.isFile ? 1 : 0;
        *out_is_directory = entry.isDirectory ? 1 : 0;
        *out_size = entry.isFile ? entry.size : 0;
        const size_t required = entry.name.size() + 1; // include NUL
        *required_name_length = required;
        if (name_buffer == nullptr || name_buffer_length < required)
            return ZAR_DOTNET_BUFFER_TOO_SMALL;
        std::memcpy(name_buffer, entry.name.data(), entry.name.size());
        name_buffer[entry.name.size()] = '\0';
        return ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}
