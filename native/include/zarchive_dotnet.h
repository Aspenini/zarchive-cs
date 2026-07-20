/* zarchive_dotnet.h
 *
 * Stable C ABI bridge over the ZArchive C++ library, consumed by the
 * ZArchive.NET managed bindings. This header intentionally exposes only
 * plain integer types, opaque pointers, caller-provided buffers and
 * UTF-8 strings. No C++ types or exceptions ever cross this boundary.
 *
 * ABI stability rules:
 *  - Every exported symbol is prefixed with zar_dotnet_.
 *  - Functions use the platform default C calling convention (cdecl).
 *  - Breaking changes require incrementing ZAR_DOTNET_ABI_VERSION.
 */
#ifndef ZARCHIVE_DOTNET_H
#define ZARCHIVE_DOTNET_H

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#define ZAR_DOTNET_API __declspec(dllexport)
#else
#define ZAR_DOTNET_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C"
{
#endif

#define ZAR_DOTNET_ABI_VERSION 1u

/* Matches upstream ZARCHIVE_INVALID_NODE. */
#define ZAR_DOTNET_INVALID_NODE 0xFFFFFFFFu

typedef struct zar_dotnet_reader zar_dotnet_reader;
typedef struct zar_dotnet_writer zar_dotnet_writer;

typedef uint32_t zar_dotnet_node;

typedef enum zar_dotnet_status
{
    ZAR_DOTNET_OK = 0,
    ZAR_DOTNET_INVALID_ARGUMENT = 1,
    ZAR_DOTNET_NOT_FOUND = 2,
    ZAR_DOTNET_INVALID_NODE_STATUS = 3,
    ZAR_DOTNET_NOT_A_FILE = 4,
    ZAR_DOTNET_NOT_A_DIRECTORY = 5,
    ZAR_DOTNET_IO_ERROR = 6,
    ZAR_DOTNET_INVALID_ARCHIVE = 7,
    ZAR_DOTNET_BUFFER_TOO_SMALL = 8,
    ZAR_DOTNET_INVALID_STATE = 9,
    ZAR_DOTNET_OUT_OF_MEMORY = 10,
    ZAR_DOTNET_INTERNAL_ERROR = 255
} zar_dotnet_status;

/* ---- Version and diagnostics ---- */

ZAR_DOTNET_API uint32_t zar_dotnet_abi_version(void);

/* Returns a static, never-freed string describing the bundled upstream
 * ZArchive version and commit. */
ZAR_DOTNET_API const char* zar_dotnet_upstream_version(void);

/* Copies the calling thread's last error message (UTF-8, NUL-terminated)
 * into buffer. Two-call pattern: pass a null/short buffer to learn the
 * required length (including the NUL) via required_length. */
ZAR_DOTNET_API zar_dotnet_status zar_dotnet_get_last_error(
    char* buffer,
    size_t buffer_length,
    size_t* required_length);

/* ---- Reader ---- */

ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_open_file(
    const char* path_utf8,
    zar_dotnet_reader** out_reader);

ZAR_DOTNET_API void zar_dotnet_reader_destroy(
    zar_dotnet_reader* reader);

/* Looks up an archive path ("" resolves to the root directory).
 * Returns ZAR_DOTNET_NOT_FOUND when no matching entry exists. */
ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_lookup(
    zar_dotnet_reader* reader,
    const char* archive_path,
    size_t archive_path_length,
    int32_t allow_file,
    int32_t allow_directory,
    zar_dotnet_node* out_node);

ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_get_entry_type(
    zar_dotnet_reader* reader,
    zar_dotnet_node node,
    int32_t* out_is_file,
    int32_t* out_is_directory);

ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_get_file_size(
    zar_dotnet_reader* reader,
    zar_dotnet_node node,
    uint64_t* out_size);

/* Random-access read. Reads up to buffer_length bytes starting at offset.
 * Reads past end-of-file yield fewer bytes (possibly zero); that is not
 * an error. */
ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_read_file(
    zar_dotnet_reader* reader,
    zar_dotnet_node node,
    uint64_t offset,
    void* buffer,
    size_t buffer_length,
    size_t* out_bytes_read);

ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_get_directory_count(
    zar_dotnet_reader* reader,
    zar_dotnet_node node,
    uint32_t* out_count);

/* Retrieves one child of a directory node. Name uses the two-call buffer
 * pattern: on ZAR_DOTNET_BUFFER_TOO_SMALL, required_name_length holds the
 * byte count required including the terminating NUL. The metadata outputs
 * are populated whenever the node and index are valid, regardless of
 * whether the name fit. */
ZAR_DOTNET_API zar_dotnet_status zar_dotnet_reader_get_directory_entry(
    zar_dotnet_reader* reader,
    zar_dotnet_node directory,
    uint32_t index,
    char* name_buffer,
    size_t name_buffer_length,
    size_t* required_name_length,
    int32_t* out_is_file,
    int32_t* out_is_directory,
    uint64_t* out_size);

/* ---- Writer ---- */

ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_create_file(
    const char* output_path_utf8,
    zar_dotnet_writer** out_writer);

/* Destroys the writer. If the archive was never finalized, the output
 * file created by zar_dotnet_writer_create_file is deleted so that a
 * partial archive is never mistaken for a valid one. */
ZAR_DOTNET_API void zar_dotnet_writer_destroy(
    zar_dotnet_writer* writer);

ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_make_directory(
    zar_dotnet_writer* writer,
    const char* archive_path,
    size_t archive_path_length,
    int32_t recursive);

/* Starts a new file entry and makes it the append target. Parent
 * directories must already exist. Fails with ZAR_DOTNET_INVALID_STATE if
 * an entry with the same name already exists or the parent is missing. */
ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_start_file(
    zar_dotnet_writer* writer,
    const char* archive_path,
    size_t archive_path_length);

ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_append(
    zar_dotnet_writer* writer,
    const void* data,
    size_t data_length);

/* Writes the archive footer and flushes the output file. May only be
 * called once; the writer accepts no further entries afterwards. */
ZAR_DOTNET_API zar_dotnet_status zar_dotnet_writer_finalize(
    zar_dotnet_writer* writer);

#ifdef __cplusplus
}
#endif

#endif /* ZARCHIVE_DOTNET_H */
