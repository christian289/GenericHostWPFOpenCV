package(default_visibility = ["//visibility:public"])

cc_library(
    name = "jpeg_decode",
    srcs = ["jpeg_decode.c"],
    hdrs = ["jpeg_decode.h"],
    linkopts = ["-ljpeg"],
)

cc_binary(
    name = "libohmyrudolph_native.so",
    srcs = ["om_rudolph.cc", "om_rudolph.h"],
    linkshared = True,
    linkstatic = True,
    additional_linker_inputs = ["exports.map"],
    linkopts = ["-Wl,--version-script=$(location :exports.map)"],
    deps = [
        ":jpeg_decode",
        "//mediapipe/framework:calculator_framework",
        "//mediapipe/framework:resources",
        "//mediapipe/framework:resources_service",
        "//mediapipe/framework/formats:image_frame",
        "//mediapipe/framework/formats:landmark_cc_proto",
        "//mediapipe/framework/port:parse_text_proto",
        "//mediapipe/modules/face_landmark:face_landmark_front_cpu",
        "@com_google_absl//absl/status",
    ],
)

cc_import(
    name = "native_import",
    shared_library = ":libohmyrudolph_native.so",
)

cc_binary(
    name = "native_smoke",
    srcs = ["smoke.cc", "om_rudolph.h"],
    linkopts = ["-ljpeg"],
    deps = [":native_import"],
)
