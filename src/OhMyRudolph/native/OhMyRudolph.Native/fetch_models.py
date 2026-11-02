#!/usr/bin/env python3
"""Build-only: extract exact v0.10.21 assets from the official PyPI wheel."""
import hashlib
import pathlib
import sys
import urllib.request
import zipfile

URL = "https://files.pythonhosted.org/packages/9f/99/5da7ae7f7e25847383bc2fe5a9adc7ce150dd371682f486c0666b407cad7/mediapipe-0.10.21-cp312-cp312-manylinux_2_28_x86_64.whl"
WHEEL_SHA256 = "956eb1ebc275c629e61b085b2cab89c3a5b9e93bad1bb107348d98dafb5a4bb5"
ASSETS = {
    "mediapipe/modules/face_detection/face_detection_short_range.tflite": "bbff11cebd1eb27a1e004cae0b0e63ec8c551cbf34a4451148b4908b8db3eca8",
    "mediapipe/modules/face_landmark/face_landmark.tflite": "1055cb9d4a9ca8b8c688902a3a5194311138ba256bcc94e336d8373a5f30c814",
}

def verify(data, expected):
    if hashlib.sha256(data).hexdigest() != expected:
        raise ValueError("SHA-256 mismatch")

root = pathlib.Path(sys.argv[1])
with urllib.request.urlopen(URL, timeout=120) as response:
    wheel = response.read()
verify(wheel, WHEEL_SHA256)
import io
with zipfile.ZipFile(io.BytesIO(wheel)) as archive:
    for name, digest in ASSETS.items():
        data = archive.read(name)
        verify(data, digest)
        target = root / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    license_file = root / "licenses" / "mediapipe-LICENSE"
    license_file.parent.mkdir(parents=True, exist_ok=True)
    license_file.write_bytes(archive.read("mediapipe-0.10.21.dist-info/LICENSE"))
