"""Instance-local, length-framed face mesh server. Protocol helpers use stdlib only."""
import argparse
import json
import struct
import time

MAX_REQUEST = 8 * 1024 * 1024
MAX_RESPONSE = 64 * 1024
IO_TIMEOUT = 3.0


def decode_request(payload):
    if len(payload) < 5:
        raise ValueError("short request")
    size = struct.unpack_from("<I", payload)[0]
    if size == 0 or size > len(payload) - 5:
        raise ValueError("invalid metadata length")
    metadata = json.loads(payload[4:4 + size].decode("utf-8"))
    required = {"sessionId", "cameraId", "frameId", "width", "height", "mirrored", "rotationDegrees"}
    if not isinstance(metadata, dict) or set(metadata) != required:
        raise ValueError("invalid metadata")
    if (not isinstance(metadata["sessionId"], str) or not metadata["sessionId"].strip()
            or not isinstance(metadata["cameraId"], str) or not metadata["cameraId"].strip()
            or type(metadata["frameId"]) is not int
            or type(metadata["width"]) is not int or metadata["width"] <= 0
            or type(metadata["height"]) is not int or metadata["height"] <= 0
            or type(metadata["mirrored"]) is not bool
            or type(metadata["rotationDegrees"]) is not int
            or metadata["rotationDegrees"] not in (0, 90, 180, 270)):
        raise ValueError("invalid metadata")
    return metadata, payload[4 + size:]


def encode_response(metadata, status, landmarks):
    body = json.dumps({"metadata": metadata, "status": status, "landmarks": landmarks},
                      ensure_ascii=True, separators=(",", ":")).encode("utf-8")
    if not 0 < len(body) <= MAX_RESPONSE:
        raise ValueError("response too large")
    return struct.pack("<I", len(body)) + body


def process_request(payload, detector):
    """Detector receives encoded bytes and metadata; None/empty means no face."""
    metadata, image = decode_request(payload)
    try:
        landmarks = detector(image, metadata)
        return encode_response(metadata, "ok" if landmarks else "no_face", landmarks or [])
    except Exception:
        # Never forward exception text, input data, or image contents to the client.
        return encode_response(metadata, "error", [])


def _overlapped(pipe, operation, value, timeout):
    import pywintypes
    import win32event
    import win32file

    overlapped = pywintypes.OVERLAPPED()
    event = win32event.CreateEvent(None, True, False, None)
    overlapped.hEvent = event
    try:
        if operation == "read":
            code, buffer = win32file.ReadFile(pipe, value, overlapped)
        else:
            code, buffer = win32file.WriteFile(pipe, value, overlapped)
        if code not in (0, 997):
            raise OSError("pipe I/O failed")
        if code == 997:
            milliseconds = win32event.INFINITE if timeout is None else max(0, int(timeout * 1000))
            if win32event.WaitForSingleObject(event, milliseconds) != win32event.WAIT_OBJECT_0:
                win32file.CancelIo(pipe)
                # Complete the cancelled operation before its OVERLAPPED/event goes away.
                try:
                    win32file.GetOverlappedResult(pipe, overlapped, True)
                except pywintypes.error:
                    pass
                raise TimeoutError("pipe I/O timed out")
        count = win32file.GetOverlappedResult(pipe, overlapped, True)
        return bytes(buffer[:count]) if operation == "read" else count
    finally:
        event.Close()


def read_exact(pipe, size, idle=False):
    result = bytearray()
    deadline = None if idle else time.monotonic() + IO_TIMEOUT
    while len(result) < size:
        timeout = None if deadline is None else max(0, deadline - time.monotonic())
        chunk = _overlapped(pipe, "read", size - len(result), timeout)
        if not chunk:
            raise EOFError("pipe closed")
        result.extend(chunk)
        if deadline is None:
            deadline = time.monotonic() + IO_TIMEOUT
    return bytes(result)


def write_exact(pipe, data):
    offset = 0
    deadline = time.monotonic() + IO_TIMEOUT
    while offset < len(data):
        written = _overlapped(pipe, "write", data[offset:], max(0, deadline - time.monotonic()))
        if written <= 0:
            raise EOFError("pipe closed")
        offset += written


def handle_client(pipe, detector):
    while True:
        length = struct.unpack("<I", read_exact(pipe, 4, idle=True))[0]
        if length <= 0 or length > MAX_REQUEST:
            raise ValueError("invalid request length")
        payload = read_exact(pipe, length)
        write_exact(pipe, process_request(payload, detector))


def make_detector():
    import cv2
    import mediapipe as mp
    import numpy as np

    mesh = mp.solutions.face_mesh.FaceMesh(static_image_mode=True, max_num_faces=5)

    def detect(image, metadata):
        frame = cv2.imdecode(np.frombuffer(image, np.uint8), cv2.IMREAD_COLOR)
        if frame is None or frame.shape[1] != metadata["width"] or frame.shape[0] != metadata["height"]:
            raise ValueError("invalid image")
        result = mesh.process(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
        if not result.multi_face_landmarks:
            return []
        return [{"x": int(face.landmark[1].x * frame.shape[1]),
                 "y": int(face.landmark[1].y * frame.shape[0])}
                for face in result.multi_face_landmarks]
    return mesh, detect


def main():
    import pywintypes
    import win32event
    import win32file
    import win32pipe

    parser = argparse.ArgumentParser()
    parser.add_argument("--pipe-name", required=True)
    args = parser.parse_args()
    if not args.pipe_name or "\\" in args.pipe_name or "/" in args.pipe_name:
        parser.error("invalid pipe name")
    mesh, detector = make_detector()
    try:
        while True:
            pipe = win32pipe.CreateNamedPipe(
                "\\\\.\\pipe\\" + args.pipe_name,
                win32pipe.PIPE_ACCESS_DUPLEX | win32file.FILE_FLAG_OVERLAPPED,
                win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_WAIT,
                1, 65536, 65536, 0, None)
            try:
                event = win32event.CreateEvent(None, True, False, None)
                try:
                    overlapped = pywintypes.OVERLAPPED()
                    overlapped.hEvent = event
                    try:
                        code = win32pipe.ConnectNamedPipe(pipe, overlapped)
                        if code == 997:
                            win32event.WaitForSingleObject(event, win32event.INFINITE)
                            win32file.GetOverlappedResult(pipe, overlapped, True)
                    except pywintypes.error as error:
                        if error.winerror != 535:  # Client connected before ConnectNamedPipe.
                            raise
                finally:
                    event.Close()
                try:
                    handle_client(pipe, detector)
                except (EOFError, OSError, ValueError, UnicodeError, json.JSONDecodeError, pywintypes.error):
                    pass
            finally:
                pipe.Close()
    finally:
        mesh.close()


if __name__ == "__main__":
    main()
