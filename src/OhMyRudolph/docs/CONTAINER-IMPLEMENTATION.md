# 로컬 컨테이너 처리 런타임 구현 계획

목표: 기존 WPF 포토부스의 촬영·검토·저장 흐름을 유지하고 Python 두 서버를 Linux MediaPipe C++ + .NET 10 호스트로 교체한다. Windows WPF는 Podman의 WSL2 기반 컨테이너와 루프백 HTTP로 통신한다. Velopack에 OCI 이미지 아카이브를 동봉한다.

## 경계

- 모든 소스 변경은 이 앱 폴더 안이며 전용 기능 브랜치에서 검증 단위로 커밋한다. 원격 push·이미지 registry push·공개는 별도 승인이다.
- 카메라는 Windows WPF가 소유한다. Linux 컨테이너에 카메라 장치를 넘기지 않는다. 컨테이너는 전달받은 JPEG만 처리한다.
- 실제 UI·실카메라·사용자 사진 저장은 별도 실행 직전 승인. 기본 검증은 합성 입력과 권리 확인된 시험 자료만 사용한다.
- WSL2/Podman 설치·관리자 권한·재부팅은 자동 수행하지 않는다. 기존 다른 컨테이너·머신 종료, 전역 prune, 전체 Python 종료 금지.
- CPU와 Windows x64/Linux amd64를 우선 대상으로 한다. 다른 아키텍처의 통과를 주장하지 않는다.
- Python은 빌드 도구로 사용될 수 있지만 제품 런타임에는 필요하지 않아야 한다. 전환 완료 시 기존 .py/FastAPI/NamedPipe 클라이언트·호출·의존성을 제거한다.
- 초기 Ubuntu 22.04/GCC11 빌드에서 고정 XNNPACK의 `-mavxvnniint8` 옵션 미지원이 실제로 확인되어, 이미지 내부 빌드·런타임을 Ubuntu 24.04/Noble·GCC13·OpenCV4.6으로 맞춘다. 선택 ISA 커널을 임의로 제거하거나 Windows/WSL 전역 설정을 바꾸지 않는다.
- WPF 소유자 손실은 인증된 health/프레임 요청으로 갱신하는 호스트 lease(기본30초)와 클라이언트 heartbeat(10초)로 감지한다. 만료 시 해당 처리 호스트만 종료하고 컨테이너 `--rm`으로 정리한다. 실제 만료·정리는 통합 검증 전에는 통과로 표시하지 않는다.

## 구현 순서

1. 현재 작업 체크포인트를 커밋하고 WSL/Podman 준비 상태를 확인한다.
2. 공식 Linux 가이드에 맞춰 고정 MediaPipe v0.10.21의 C++ Hello World와 실제 FaceMesh CPU 그래프를 빌드·실행한다. 모델 파일과 라이선스·해시를 확인한다. Hello World만 통과한 상태를 얼굴 검출 성공으로 세지 않는다.
3. 아래 C ABI를 구현하고 .NET Interop/HTTP 호스트를 결합한다. native 오류/모델 누락/손상 입력·얼굴 없음·동시 요청·호스트 종료를 확인한다.
4. 검증된 이미지에 한해 WPF 클라이언트를 전환한다. 기존 프레임 최신성·소유권·세션 거절 정책과 촬영 확정본 보존을 유지한다.
5. 이미지 export/manifest/Velopack payload를 생성하고 실제 load→기동→요청→종료→재기동을 확인한다. 의존성 미준비를 빈 성공으로 대체하지 않는다.
6. 실행 문서와 실제 검증·미실행 범위를 갱신하고 각 완결 단위를 커밋한다.

## 소스 소유권

- native/OhMyRudolph.Native: MediaPipe/Bazel C++ ABI·공식 예제 실행용 빌드·모델 준비. 처리 입력은 JPEG, 반환은 얼굴마다 코 좌표 한 점(최대5).
- OhMyRudolph.Interop: Linux shared library 호출과 SafeHandle. WPF/OpenCVSharp/Python 참조 없음.
- OhMyRudolph.ProcessingHost: ASP.NET Core .NET10 Linux 호스트. Abstractions와 Interop만 참조. 전체 HTTP 요청의 한정·인증·직렬화와 native 수명 담당.
- OhMyRudolph.Abstractions/FaceMesh: BCL 메타데이터·좌표·HTTP 계약.
- OhMyRudolph.Core: Windows Podman 수명/이미지 import/HTTP 클라이언트와 기존 카메라/합성.
- scripts·컨테이너 구성·CPM·솔루션·Velopack·README는 통합 담당자가 관리한다. 컨테이너 전환 전 이전 Python 경로를 조기에 삭제하지 않는다.

## C ABI v1 (고정)

```c
#include <stdint.h>
typedef struct om_point { int32_t x; int32_t y; } om_point;
uint32_t om_abi_version(void); /* 1 */
int32_t om_create(const char* resource_root_utf8, void** out_handle);
void om_destroy(void* handle);
int32_t om_detect_jpeg(void* handle, const uint8_t* jpeg, int32_t jpeg_size,
    int32_t expected_width, int32_t expected_height,
    om_point* points, int32_t capacity, int32_t* written);
```

반환값 0=성공, 1=잘못된 입력, 2=모델/리소스 불가, 3=추론 실패, 4=출력 버퍼 부족. 얼굴 없음은 성공+written=0이다. om_point는 8바이트이며 C++ 객체/STL/예외를 경계 밖으로 넘기지 않는다. 입력 포인터는 호출 동안만 빌리고 출력은 호출자가 소유한다. 실패 시 written=0, create 실패 시 handle=null. 같은 handle에는 한 호출만 진행하며 destroy는 진행 호출이 끝난 뒤 수행한다. 네이티브 멈춤은 취소 토큰으로 강제 중단할 수 없으므로 컨테이너 종료로 격리한다.

## HTTP v1 (루프백 전용)

- GET /health/ready: 엔진/모델 초기화 완료 후만 200. 앱/계약 버전과 ABI=1을 반환한다.
- POST /v1/frames/detect: application/octet-stream. 본문은 little-endian uint32 메타 JSON 길이 + UTF-8 camelCase FaceMeshFrameMetadata JSON + JPEG. 전체 상한8MiB, 메타4096바이트, 원본 최대4096×4096. 이미지 실제 크기와 메타가 일치해야 한다.
- Authorization: Bearer에 실행별 무작위64자리 hex 토큰. 토큰/사진/좌표/원문 경로는 로그에 남기지 않는다. 컨테이너 포트는 Windows 127.0.0.1에만 게시하며 외부 LAN에 공개하지 않는다.
- Abstractions에 FaceMeshPoint(int X,int Y)를 추가하고 FaceMeshResponse.Landmarks를 그 목록으로 통일한다. metadata/status/landmarks를 camelCase JSON으로 직렬화하며 status는 0=Ok,1=NoFace다. 오류는 HTTP 상태와 짧은 code 필드로 별도 반환한다.
- 400 잘못된 메타/이미지, 401 인증 실패, 413 과대 요청, 429 단일 처리 슬롯 사용 중, 500 native 추론 실패. 요청 본문·예외 원문을 응답하지 않는다.
- 호스트의 처리 슬롯은1이며 대기열을 만들지 않는다. WPF도 처리 중1+최신 대기1 정책을 유지한다. 확정 사진은 이 드롭 정책과 분리한다.
- 카메라/세션/프레임/폭/높이/미러/회전 응답을 요청과 대조한다. 취소/시간초과/이전 세션 응답을 성공으로 게시하지 않는다.

## 설치·수명

- 이미지 아카이브와 앱 버전·아카이브 SHA256·expected image ID·모델 출처/해시 manifest를 버전별 payload에 둔다. source .venv/bin/obj나 개인 설정을 넣지 않는다.
- 실행 시 WSL/Podman 사전 점검 → archive 해시 확인 → 해당 image ID가 없으면 podman load → 앱 세션 label/고유 이름으로 create/start → localhost readiness 확인 순서다.
- 명시된 처리 엔진 외의 자동 대체는 없다. 업데이트 시 앱이 소유한 컨테이너만 종료하고 새 이미지/앱의 버전이 맞는지 확인한다. 다른 컨테이너·Podman 머신은 정리하지 않는다.
- 사진 저장 위치·사용자 자료는 Velopack 버전 디렉터리 밖이며 컨테이너 이미지/레이어에 포함하지 않는다.
- WSL/Podman의 OS 실행 기반까지 설치 파일에 포함하는 것은 현재 범위가 아니다. 미준비 기기는 안내하며, 숨은 권한 상승이나 재부팅을 하지 않는다.

## 검증 명령의 의미

현재 실행 가능한 개발 하네스는 README를 따른다. 새 native/호스트/이미지/pack 명령은 실제 구현 후 문서화하고 실행 결과를 기록한다. 본 계획은 빌드·설치·컨테이너 검증 성공의 증거가 아니다.
