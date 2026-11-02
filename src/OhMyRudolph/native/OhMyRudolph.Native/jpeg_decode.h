#ifndef OM_JPEG_DECODE_H
#define OM_JPEG_DECODE_H
#include <stddef.h>
#ifdef __cplusplus
extern "C" {
#endif
int om_decode_rgb(const unsigned char* data, size_t size, int width, int height, unsigned char** result);
#ifdef __cplusplus
}
#endif
#endif
