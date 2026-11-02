#include <stddef.h>
#include <stdio.h>
#include <stdlib.h>
#include <setjmp.h>
#include <jpeglib.h>
#include "jpeg_decode.h"

struct error_state { struct jpeg_error_mgr pub; jmp_buf jump; };
struct decode_state { struct jpeg_decompress_struct decoder; struct error_state error; unsigned char* pixels; int created; };
static void fail_jpeg(j_common_ptr c) { struct error_state* e = (struct error_state*)c->err; longjmp(e->jump, 1); }
static void message_jpeg(j_common_ptr c, int level) { if (level < 0) fail_jpeg(c); }
int om_decode_rgb(const unsigned char* data, size_t size, int width, int height, unsigned char** result) {
  struct decode_state* s = (struct decode_state*)calloc(1, sizeof(*s));
  *result = NULL;
  if (!s) return 0;
  s->decoder.err = jpeg_std_error(&s->error.pub);
  s->error.pub.error_exit = fail_jpeg; s->error.pub.emit_message = message_jpeg;
  if (setjmp(s->error.jump)) {
    if (s->created) jpeg_destroy_decompress(&s->decoder);
    free(s->pixels); free(s); return 0;
  }
  jpeg_create_decompress(&s->decoder); s->created = 1;
  jpeg_mem_src(&s->decoder, data, (unsigned long)size);
  if (jpeg_read_header(&s->decoder, TRUE) != JPEG_HEADER_OK ||
      s->decoder.image_width != (unsigned)width || s->decoder.image_height != (unsigned)height) {
    jpeg_destroy_decompress(&s->decoder); free(s); return 0;
  }
  s->decoder.out_color_space = JCS_RGB;
  jpeg_start_decompress(&s->decoder);
  s->pixels = (unsigned char*)malloc((size_t)width * (size_t)height * 3);
  if (!s->pixels) { jpeg_destroy_decompress(&s->decoder); free(s); return 0; }
  while (s->decoder.output_scanline < s->decoder.output_height) {
    JSAMPROW row = s->pixels + (size_t)s->decoder.output_scanline * width * 3;
    jpeg_read_scanlines(&s->decoder, &row, 1);
  }
  jpeg_finish_decompress(&s->decoder); jpeg_destroy_decompress(&s->decoder);
  *result = s->pixels; free(s); return 1;
}
