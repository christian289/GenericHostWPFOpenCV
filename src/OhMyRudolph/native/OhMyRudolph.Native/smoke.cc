#include "om_rudolph.h"
#include <cstdio>
#include <jpeglib.h>
#include <cstdlib>
#include <fstream>
#include <iterator>
#include <vector>

static void require(bool ok, const char* name) {
  if (!ok) { std::fprintf(stderr, "FAIL: %s\n", name); std::exit(1); }
}

static std::vector<unsigned char> blank_jpeg() {
  jpeg_compress_struct c{}; jpeg_error_mgr e{};
  c.err = jpeg_std_error(&e); jpeg_create_compress(&c);
  unsigned char* bytes = nullptr; unsigned long length = 0;
  jpeg_mem_dest(&c, &bytes, &length);
  c.image_width = 64; c.image_height = 64; c.input_components = 3; c.in_color_space = JCS_RGB;
  jpeg_set_defaults(&c); jpeg_start_compress(&c, TRUE);
  unsigned char row[64 * 3]{};
  while (c.next_scanline < c.image_height) { JSAMPROW p = row; jpeg_write_scanlines(&c, &p, 1); }
  jpeg_finish_compress(&c);
  std::vector<unsigned char> result(bytes, bytes + length);
  std::free(bytes); jpeg_destroy_compress(&c); return result;
}

int main(int argc, char** argv) {
  require(argc == 2 || argc == 5, "resource root and optional fixture arguments");
  require(sizeof(om_point) == 8 && om_abi_version() == 1, "ABI");
  void* bad = reinterpret_cast<void*>(1);
  require(om_create("/nonexistent-om-model-root", &bad) == 2 && bad == nullptr, "missing model");
  auto jpeg = blank_jpeg();
  for (int i = 0; i < 3; ++i) {
    void* h = nullptr;
    require(om_create(argv[1], &h) == 0 && h, "create");
    om_point p[5]{}; int32_t n = -1;
    require(om_detect_jpeg(h, jpeg.data(), static_cast<int32_t>(jpeg.size()), 64, 64, p, 5, &n) == 0 && n == 0, "synthetic no face");
    n = -1;
    require(om_detect_jpeg(h, jpeg.data(), static_cast<int32_t>(jpeg.size()), 63, 64, p, 5, &n) == 1 && n == 0, "dimension mismatch");
    const uint8_t invalid[] = {1, 2, 3}; n = -1;
    require(om_detect_jpeg(h, invalid, sizeof(invalid), 64, 64, p, 5, &n) == 1 && n == 0, "invalid JPEG");
    om_destroy(h);
  }
  if (argc == 5) {
    std::ifstream input(argv[2], std::ios::binary);
    require(input.good(), "fixture open");
    std::vector<unsigned char> fixture((std::istreambuf_iterator<char>(input)),
                                       std::istreambuf_iterator<char>());
    require(!fixture.empty() && fixture.size() <= 8 * 1024 * 1024, "fixture size");
    const int width = std::atoi(argv[3]), height = std::atoi(argv[4]);
    void* h = nullptr;
    require(om_create(argv[1], &h) == 0 && h, "fixture create");
    int32_t n = -1;
    require(om_detect_jpeg(h, fixture.data(), static_cast<int32_t>(fixture.size()), width, height, nullptr, 0, &n) == 4 && n == 0, "fixture insufficient output buffer");
    om_point p[5]{};
    require(om_detect_jpeg(h, fixture.data(), static_cast<int32_t>(fixture.size()), width, height, p, 5, &n) == 0 && n > 0 && n <= 5, "fixture face detection");
    om_destroy(h);
    std::puts("native ABI smoke passed including positive-face fixture");
  } else {
    std::puts("native ABI smoke passed (no positive-face fixture)");
  }
}
