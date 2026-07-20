#include "bridge_common.h"

#include <zarchive/zarchivewriter.h>

#include <fstream>
#include <memory>
#include <string>

struct zar_dotnet_writer
{
    std::filesystem::path outputPath;
    std::ofstream output;
    std::unique_ptr<ZArchiveWriter> impl;
    bool fileCreated = false;
    bool ioError = false;
    bool finalized = false;
    bool hasActiveFile = false;
};

namespace
{

/* ZArchiveWriter only produces multipart output for special use cases; the
 * file-backed bridge always writes a single output file. */
void writer_new_output_file(const int32_t /*partIndex*/, void* ctx)
{
    auto* writer = static_cast<zar_dotnet_writer*>(ctx);
    writer->output.open(writer->outputPath,
                        std::ios::binary | std::ios::trunc);
    if (!writer->output.is_open())
        writer->ioError = true;
    else
        writer->fileCreated = true;
}

void writer_write_output_data(const void* data, size_t length, void* ctx)
{
    auto* writer = static_cast<zar_dotnet_writer*>(ctx);
    writer->output.write(static_cast<const char*>(data),
                         static_cast<std::streamsize>(length));
    if (!writer->output.good())
        writer->ioError = true;
}

zar_dotnet_status validate_writer(zar_dotnet_writer* writer)
{
    if (writer == nullptr)
    {
        zar_bridge::set_error("Writer handle must not be null");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    if (writer->finalized)
    {
        zar_bridge::set_error("Writer has already been finalized");
        return ZAR_DOTNET_INVALID_STATE;
    }
    if (writer->ioError)
    {
        zar_bridge::set_error("A previous write to the output file failed");
        return ZAR_DOTNET_IO_ERROR;
    }
    return ZAR_DOTNET_OK;
}

} // namespace

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_create_file(
    const char* output_path_utf8,
    zar_dotnet_writer** out_writer)
{
    zar_bridge::clear_error();
    if (out_writer != nullptr)
        *out_writer = nullptr;
    if (output_path_utf8 == nullptr || out_writer == nullptr)
    {
        zar_bridge::set_error("path and out_writer must not be null");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        auto writer = std::make_unique<zar_dotnet_writer>();
        writer->outputPath = zar_bridge::utf8_to_path(output_path_utf8);
        // The ZArchiveWriter constructor invokes the new-output-file callback.
        writer->impl = std::make_unique<ZArchiveWriter>(
            writer_new_output_file, writer_write_output_data, writer.get());
        if (writer->ioError)
        {
            zar_bridge::set_error("Could not create the output file");
            return ZAR_DOTNET_IO_ERROR;
        }
        *out_writer = writer.release();
        return ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API void zar_dotnet_writer_destroy(
    zar_dotnet_writer* writer)
{
    if (writer == nullptr)
        return;
    writer->impl.reset();
    if (writer->output.is_open())
        writer->output.close();
    if (!writer->finalized && writer->fileCreated)
    {
        std::error_code ec;
        std::filesystem::remove(writer->outputPath, ec);
    }
    delete writer;
}

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_make_directory(
    zar_dotnet_writer* writer,
    const char* archive_path,
    size_t archive_path_length,
    int32_t recursive)
{
    zar_bridge::clear_error();
    if (const zar_dotnet_status status = validate_writer(writer);
        status != ZAR_DOTNET_OK)
        return status;
    if (archive_path == nullptr)
    {
        zar_bridge::set_error("archive_path must not be null");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        const std::string path(archive_path, archive_path_length);
        if (!writer->impl->MakeDir(path.c_str(), recursive != 0))
        {
            zar_bridge::set_error(
                "Directory could not be created (an entry with that name "
                "already exists, or a path segment is a file)");
            return ZAR_DOTNET_INVALID_STATE;
        }
        return writer->ioError ? ZAR_DOTNET_IO_ERROR : ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_start_file(
    zar_dotnet_writer* writer,
    const char* archive_path,
    size_t archive_path_length)
{
    zar_bridge::clear_error();
    if (const zar_dotnet_status status = validate_writer(writer);
        status != ZAR_DOTNET_OK)
        return status;
    if (archive_path == nullptr || archive_path_length == 0)
    {
        zar_bridge::set_error("archive_path must not be null or empty");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    try
    {
        const std::string path(archive_path, archive_path_length);
        if (!writer->impl->StartNewFile(path.c_str()))
        {
            writer->hasActiveFile = false;
            zar_bridge::set_error(
                "File entry could not be started (an entry with that name "
                "already exists, or the parent directory is missing)");
            return ZAR_DOTNET_INVALID_STATE;
        }
        writer->hasActiveFile = true;
        return writer->ioError ? ZAR_DOTNET_IO_ERROR : ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_append(
    zar_dotnet_writer* writer,
    const void* data,
    size_t data_length)
{
    zar_bridge::clear_error();
    if (const zar_dotnet_status status = validate_writer(writer);
        status != ZAR_DOTNET_OK)
        return status;
    if (data == nullptr && data_length != 0)
    {
        zar_bridge::set_error("data must not be null");
        return ZAR_DOTNET_INVALID_ARGUMENT;
    }
    if (!writer->hasActiveFile)
    {
        zar_bridge::set_error("No file entry is currently active");
        return ZAR_DOTNET_INVALID_STATE;
    }
    try
    {
        if (data_length != 0)
            writer->impl->AppendData(data, data_length);
        return writer->ioError ? ZAR_DOTNET_IO_ERROR : ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}

extern "C" ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_finalize(
    zar_dotnet_writer* writer)
{
    zar_bridge::clear_error();
    if (const zar_dotnet_status status = validate_writer(writer);
        status != ZAR_DOTNET_OK)
        return status;
    try
    {
        writer->impl->Finalize();
        writer->hasActiveFile = false;
        writer->output.flush();
        if (!writer->output.good())
            writer->ioError = true;
        writer->output.close();
        if (writer->ioError)
        {
            zar_bridge::set_error("Writing the archive footer failed");
            return ZAR_DOTNET_IO_ERROR;
        }
        writer->finalized = true;
        return ZAR_DOTNET_OK;
    }
    ZAR_BRIDGE_CATCH_ALL
}
