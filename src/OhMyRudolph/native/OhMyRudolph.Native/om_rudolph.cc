#include "om_rudolph.h"
#include "jpeg_decode.h"

#include <algorithm>
#include <cmath>
#include <cstdlib>
#include <fstream>
#include <memory>
#include <mutex>
#include <string>
#include <utility>
#include <vector>

#include "mediapipe/framework/calculator_graph.h"
#include "mediapipe/framework/formats/image_frame.h"
#include "mediapipe/framework/formats/landmark.pb.h"
#include "mediapipe/framework/port/parse_text_proto.h"
#include "mediapipe/framework/resources.h"
#include "mediapipe/framework/resources_service.h"

namespace {
constexpr char kDetection[] = "mediapipe/modules/face_detection/face_detection_short_range.tflite";
constexpr char kLandmark[] = "mediapipe/modules/face_landmark/face_landmark.tflite";
constexpr char kGraph[] = R"pb(
  input_stream: "image"
  output_stream: "landmarks"
  input_side_packet: "num_faces"
  input_side_packet: "use_prev_landmarks"
  input_side_packet: "with_attention"
  node {
    calculator: "FaceLandmarkFrontCpu"
    input_stream: "IMAGE:image"
    input_side_packet: "NUM_FACES:num_faces"
    input_side_packet: "USE_PREV_LANDMARKS:use_prev_landmarks"
    input_side_packet: "WITH_ATTENTION:with_attention"
    output_stream: "LANDMARKS:landmarks"
  }
)pb";

struct Handle {
  std::mutex mutex;
  std::vector<std::pair<float, float>> noses;
  int64_t timestamp = 0;
  bool running = false;
  mediapipe::CalculatorGraph graph;
  ~Handle() {
    if (running) {
      if (!graph.CloseInputStream("image").ok()) graph.Cancel();
      graph.WaitUntilDone().IgnoreError();
    }
  }
};
bool readable(const std::string& path) {
  std::ifstream input(path, std::ios::binary);
  return input.good() && input.peek() != std::ifstream::traits_type::eof();
}
}

static_assert(sizeof(om_point) == 8, "ABI point layout");
extern "C" uint32_t om_abi_version(void) { return 1; }
extern "C" int32_t om_create(const char* root, void** out) {
  if (!out) return 1;
  *out = nullptr;
  if (!root || !*root || root[0] != '/') return 1;
  try {
    const std::string prefix = std::string(root) + "/";
    if (!readable(prefix + kDetection) || !readable(prefix + kLandmark)) return 2;
    auto h = std::make_unique<Handle>();
    std::shared_ptr<mediapipe::Resources> resources = mediapipe::CreateDefaultResourcesWithMapping(
        {{kDetection, prefix + kDetection}, {kLandmark, prefix + kLandmark}});
    if (!h->graph.SetServiceObject(mediapipe::kResourcesService, std::move(resources)).ok()) return 3;
    auto config = mediapipe::ParseTextProtoOrDie<mediapipe::CalculatorGraphConfig>(kGraph);
    if (!h->graph.Initialize(std::move(config)).ok()) return 3;
    Handle* ptr = h.get();
    if (!h->graph.ObserveOutputStream("landmarks", [ptr](const mediapipe::Packet& packet) {
      const auto& faces = packet.Get<std::vector<mediapipe::NormalizedLandmarkList>>();
      std::lock_guard<std::mutex> lock(ptr->mutex);
      ptr->noses.clear();
      for (const auto& face : faces) {
        if (face.landmark_size() <= 1) return absl::InternalError("missing landmark");
        ptr->noses.emplace_back(face.landmark(1).x(), face.landmark(1).y());
      }
      return absl::OkStatus();
    }).ok()) return 3;
    if (!h->graph.StartRun({{"num_faces", mediapipe::MakePacket<int>(5)},
                            {"use_prev_landmarks", mediapipe::MakePacket<bool>(false)},
                            {"with_attention", mediapipe::MakePacket<bool>(false)}}).ok()) return 2;
    h->running = true;
    auto probe = std::make_unique<mediapipe::ImageFrame>(mediapipe::ImageFormat::SRGB, 64, 64);
    probe->SetToZero();
    if (!h->graph.AddPacketToInputStream("image",
            mediapipe::Adopt(probe.release()).At(mediapipe::Timestamp(h->timestamp++))).ok() ||
        !h->graph.WaitUntilIdle().ok()) return 2;
    { std::lock_guard<std::mutex> lock(h->mutex); h->noses.clear(); }
    *out = h.release(); return 0;
  } catch (...) { return 3; }
}
extern "C" void om_destroy(void* handle) {
  try { delete static_cast<Handle*>(handle); } catch (...) {}
}
extern "C" int32_t om_detect_jpeg(void* handle, const uint8_t* jpeg, int32_t size,
                                    int32_t width, int32_t height, om_point* points,
                                    int32_t capacity, int32_t* written) {
  if (!written) return 1;
  *written = 0;
  if (!handle || !jpeg || size <= 0 || size > 8 * 1024 * 1024 || width <= 0 || height <= 0 ||
      width > 4096 || height > 4096 || static_cast<int64_t>(width) * height > 16777216 ||
      capacity < 0 || (capacity > 0 && !points)) return 1;
  try {
    unsigned char* pixels = nullptr;
    if (!om_decode_rgb(jpeg, static_cast<size_t>(size), width, height, &pixels)) return 1;
    std::unique_ptr<unsigned char, decltype(&std::free)> data(pixels, &std::free);
    auto frame = std::make_unique<mediapipe::ImageFrame>();
    frame->CopyPixelData(mediapipe::ImageFormat::SRGB, width, height, pixels,
                         mediapipe::ImageFrame::kDefaultAlignmentBoundary);
    auto* h = static_cast<Handle*>(handle);
    { std::lock_guard<std::mutex> lock(h->mutex); h->noses.clear(); }
    if (!h->graph.AddPacketToInputStream("image", mediapipe::Adopt(frame.release()).At(mediapipe::Timestamp(h->timestamp++))).ok() ||
        !h->graph.WaitUntilIdle().ok()) return 3;
    std::lock_guard<std::mutex> lock(h->mutex);
    if (h->noses.size() > 5) return 3;
    if (h->noses.size() > static_cast<size_t>(capacity)) return 4;
    for (const auto& nose : h->noses) {
      if (!std::isfinite(nose.first) || !std::isfinite(nose.second) ||
          std::abs(static_cast<double>(nose.first) * width) > 2147483647.0 ||
          std::abs(static_cast<double>(nose.second) * height) > 2147483647.0) return 3;
    }
    int n = 0;
    for (const auto& nose : h->noses) {
      points[n++] = {static_cast<int32_t>(static_cast<double>(nose.first) * width),
                     static_cast<int32_t>(static_cast<double>(nose.second) * height)};
    }
    *written = n; return 0;
  } catch (...) { return 3; }
}
