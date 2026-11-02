#ifndef OM_RUDOLPH_H
#define OM_RUDOLPH_H
#include <stdint.h>
#ifdef __cplusplus
extern "C" {
#endif
typedef struct om_point { int32_t x; int32_t y; } om_point;
uint32_t om_abi_version(void);
int32_t om_create(const char* resource_root_utf8, void** out_handle);
void om_destroy(void* handle);
int32_t om_detect_jpeg(void* handle, const uint8_t* jpeg, int32_t jpeg_size,
                       int32_t expected_width, int32_t expected_height,
                       om_point* points, int32_t capacity, int32_t* written);
#ifdef __cplusplus
}
#endif
#endif
