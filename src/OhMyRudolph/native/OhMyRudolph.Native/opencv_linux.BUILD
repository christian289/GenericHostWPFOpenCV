# OpenCV 4 system packages on Ubuntu 24.04 amd64.
licenses(["notice"])
exports_files(["LICENSE"])
cc_library(
    name = "opencv",
    hdrs = glob([
        "include/x86_64-linux-gnu/opencv4/opencv2/cvconfig.h",
        "include/opencv4/opencv2/**/*.h*",
    ]),
    includes = ["include/x86_64-linux-gnu/opencv4/", "include/opencv4/"],
    linkopts = [
        "-l:libopencv_core.so", "-l:libopencv_calib3d.so",
        "-l:libopencv_features2d.so", "-l:libopencv_highgui.so",
        "-l:libopencv_imgcodecs.so", "-l:libopencv_imgproc.so",
        "-l:libopencv_video.so", "-l:libopencv_videoio.so",
    ],
    visibility = ["//visibility:public"],
)
