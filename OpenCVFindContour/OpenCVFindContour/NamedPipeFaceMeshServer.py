import cv2
import mediapipe as mp
import numpy as np
import json
import win32pipe, win32file, pywintypes, struct

PIPE_NAME = r'\\.\pipe\FaceMeshPipe'

# MediaPipe 초기화
mp_face_mesh = mp.solutions.face_mesh
face_mesh = mp_face_mesh.FaceMesh(static_image_mode=True, max_num_faces=5) # 최대 5명(값이 더 커질 경우 처리속도 감소. 혹시 필요하다면 서버를 늘릴 것)

def find_noses(img):
    img_rgb = cv2.cvtColor(img, cv2.COLOR_BGR2RGB)
    result = face_mesh.process(img_rgb)
    if not result.multi_face_landmarks:
        return []

    h, w, _ = img.shape
    noses = []
    for face_landmarks in result.multi_face_landmarks:
        nose = face_landmarks.landmark[1]  # 1번은 코끝
        noses.append({
            "x": int(nose.x * w),
            "y": int(nose.y * h)
        })

    return noses

def read_exact(pipe, size, timeout_sec=5.0):
    data = b''
    while len(data) < size:
        try:
            chunk = win32file.ReadFile(pipe, size - len(data))[1]
        except pywintypes.error as e:
            print(f"[❌ Read 실패] {e}")
            return None
        if not chunk:
            break
        data += chunk
    return data

def handle_client(pipe):
    print("✅ 클라이언트 연결됨!")
    try:
        while True:
            len_buf = read_exact(pipe, 4)
            if not len_buf or len(len_buf) < 4:
                print("[⚠️ 경고] 길이 수신 실패 또는 파이프 종료됨")
                break

            length = struct.unpack('<I', len_buf)[0]
            img_buf = read_exact(pipe, length)
            if img_buf is None or len(img_buf) != length:
                print("[⚠️ 경고] 이미지 데이터 수신 실패")
                break

            nparr = np.frombuffer(img_buf, np.uint8)
            img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

            if img is None:
                print("[⚠️ 경고] 이미지 디코딩 실패")
                result_json = json.dumps({}).encode('utf-8')
            else:
                noses = find_noses(img)
                result_json = json.dumps(noses).encode('utf-8')

            result_len = struct.pack('<I', len(result_json))

            try:
                win32file.WriteFile(pipe, result_len + result_json)
            except pywintypes.error as e:
                print(f"[❌ Write 실패] {e}")
                break
    finally:
        try:
            win32file.CloseHandle(pipe)
        except:
            pass
        print("🛑 클라이언트 연결 종료됨.")

def main():
    print("📡 FaceMesh Pipe 서버 시작됨.")
    while True:
        try:
            pipe = win32pipe.CreateNamedPipe(
                PIPE_NAME,
                win32pipe.PIPE_ACCESS_DUPLEX,
                win32pipe.PIPE_TYPE_BYTE | win32pipe.PIPE_WAIT,
                1, 65536, 65536,
                0, None
            )
            win32pipe.ConnectNamedPipe(pipe, None)
            handle_client(pipe)
        except Exception as e:
            print(f"[❌ 예외] {e}")
            try:
                win32file.CloseHandle(pipe)
            except:
                pass

if __name__ == "__main__":
    main()
