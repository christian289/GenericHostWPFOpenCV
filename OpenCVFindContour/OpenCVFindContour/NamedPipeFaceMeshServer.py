import cv2
import mediapipe as mp
import numpy as np
import json
import win32pipe, win32file, pywintypes, struct

PIPE_NAME = r'\\.\pipe\FaceMeshPipe'

# MediaPipe 초기화
mp_face_mesh = mp.solutions.face_mesh
face_mesh = mp_face_mesh.FaceMesh(static_image_mode=True, max_num_faces=1)

def find_nose(img):
    img_rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    result = face_mesh.process(img_rgb)
    if not result.multi_face_landmarks:
        return None

    h, w, _ = img.shape
    nose_landmark = result.multi_face_landmarks[0].landmark[1]  # 1번: 코끝
    return {"x": int(nose_landmark.x * w), "y": int(nose_landmark.y * h)}

def read_exact(pipe, size):
    data = b''
    while len(data) < size:
        chunk = win32file.ReadFile(pipe, size - len(data))[1]
        if not chunk:
            break
        data += chunk
    return data

def main():
    print("📡 Waiting for connection on pipe...")

    while True:
        try:
            pipe = win32pipe.CreateNamedPipe(
                PIPE_NAME,
                win32pipe.PIPE_ACCESS_DUPLEX,
                win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_WAIT,
                1, 65536, 65536,
                0,
                None
            )

            win32pipe.ConnectNamedPipe(pipe, None)
            print("✅ 연결됨!")

            while True:
                # 4바이트 길이 수신
                len_buf = read_exact(pipe, 4)
                if len(len_buf) < 4:
                    break

                length = struct.unpack('<I', len_buf)[0]
                img_buf = read_exact(pipe, length)

                # JPEG 디코드 후 얼굴 인식
                nparr = np.frombuffer(img_buf, np.uint8)
                img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
                if img is None:
                    print("[⚠️ 경고] 이미지 디코딩 실패")
                    break

                nose = find_nose(img)
                result_json = json.dumps(nose or {}).encode('utf-8')
                result_len = struct.pack('<I', len(result_json))
                win32file.WriteFile(pipe, result_len + result_json)

        except pywintypes.error as e:
            print(f"[❌ PIPE ERROR] {e}")
        finally:
            try:
                win32file.CloseHandle(pipe)
            except:
                pass

if __name__ == "__main__":
    main()
