# OhMyRudolph

무인 사진 촬영 기기의 촬영 흐름을 구현하는 Windows WPF 포토부스 앱입니다.

**프레임 선택 → 라이브 프리뷰 → 4컷 촬영 → 검토·선택 재촬영 → 합성 사진 로컬 저장 → 다음 세션**을 제공합니다. 카메라·촬영·사진 저장은 Windows 앱이 맡고, 얼굴 효과 검출은 로컬 Podman 컨테이너의 .NET 10 호스트와 MediaPipe C++가 맡습니다.

저장소: https://github.com/christian289/GenericHostWPFOpenCV/tree/feat/ohmyrudolph-podman-velopack/src/OhMyRudolph

## 설치 파일로 실행

Velopack 설치 파일에는 Windows x64 자체 포함 앱과 **처리 서버 OCI 이미지·버전/해시 manifest**가 함께 들어갑니다. 이미지 다운로드 없이 첫 사용 시 `podman load`로 가져오고, 이후 같은 image ID를 재사용합니다. 앱 시작만으로 카메라를 켜지 않습니다.

### 대상 기기에 먼저 준비할 것

- Windows x64. 현재 Windows x64/Linux amd64 조합만 검증했습니다. ARM/SQ3 모델은 별도 검증이 필요합니다.
- WSL2와 Podman 실행 환경, 생성·시작된 `podman-machine-default` 머신 및 같은 이름의 rootless 연결. 앱은 전역 기본 연결을 바꾸지 않습니다.
- OpenCvSharp의 Windows 네이티브 라이브러리에 필요한 VC++ x64 런타임. DLL 로드 오류가 발생하면 설치 상태를 확인해야 합니다.
- Windows의 데스크톱 앱 카메라 접근 허용.

설치 대상에는 .NET SDK·Node·Python 개발환경이 필요하지 않습니다. **WSL/Podman 자체 설치·Windows 기능 활성화·관리자 권한·재부팅을 설치 파일이 자동 처리하지는 않습니다.** 처리 이미지 동봉과 OS 실행 기반 준비는 별개입니다. 생성한 설치 파일은 서명되지 않았으므로 SmartScreen 경고가 나올 수 있습니다.

설치 패키지는 로컬 `artifacts/velopack-<버전>-<임의값>/`에 생성합니다. Setup 실행 및 Surface에서의 실촬영 검증은 아직 수행하지 않았습니다. 사진 저장 위치는 사용자가 선택하며 앱 업데이트 디렉터리에 자동 저장하지 않습니다.

## 사용 흐름

1. 시작 화면에서 포토부스를 선택합니다. 프레임 선택 단계는 카메라를 켜지 않습니다.
2. 자체 제작 라이트/다크 프레임을 고르고 **카메라 미리보기 시작**을 누릅니다.
3. 효과 없음 또는 루돌프 코를 선택합니다. 효과 없음은 처리 컨테이너 없이 동작합니다.
4. 사진마다 3초 카운트다운으로 4장을 촬영합니다. 취소하면 이미 확정한 사진은 유지됩니다.
5. 검토에서 한 장을 선택해 다시 찍을 수 있습니다. 마지막 사진도 같은 재촬영 경로를 사용합니다.
6. 완성 프레임을 PNG/JPEG로 저장합니다. 저장 취소·실패 시 검토 상태와 확정 사진을 유지합니다.
7. 새 세션에서 사진·합성본·선택 상태를 초기화합니다. 미저장 사진은 앱 종료 후 복구하지 않습니다.

프레임은 자체 생성한 1200×1700의 2×2 배치입니다. 외곽 60px, 간격 40px, 사진별 520×650 중앙 크롭, 하단 300px이며 원본 종횡비를 늘이지 않습니다. 프린터·결제·QR/클라우드 공유는 제공하지 않습니다.

정적 사진 모드는 같은 로컬 처리 호스트를 사용합니다. 처리 중 다른 이미지로 바꾸지 않으며, 뒤로/종료는 요청 취소와 완료를 기다린 뒤 소유 Mat을 해제합니다. 얼굴 없음·처리 실패·저장 실패를 상태 문구로 구분합니다.

## 소스 빌드·실행

개발 도구: .NET SDK **10.0.403**, Node.js 24 이상, Podman 6 계열과 실행 중인 WSL2 머신. C++ 빌드 도구는 빌더 이미지에 있으며 호스트에 MSVC를 설치하는 구성은 아닙니다. Python은 MediaPipe 빌드·모델 준비 도구로만 사용하고 **제품 런타임에서는 사용하지 않습니다.**

저장소 루트에서:

```powershell
cd src/OhMyRudolph
dotnet tool restore
npm run verify

# 환경에 설치된 Podman CLI를 명시합니다.
$env:OHMYRUDOLPH_PODMAN = (Get-Command podman.exe).Source
$env:OHMYRUDOLPH_PODMAN_CONNECTION = 'podman-machine-default'

# 최초 네이티브·호스트 이미지 빌드와 OCI export. 빌드 의존성 다운로드에는 네트워크가 필요합니다.
node scripts/container-build.mjs --version 1.0.0 --podman $env:OHMYRUDOLPH_PODMAN

# 번들 해시·런타임 점검 후 앱 실행. 이 명령만으로 카메라를 켜거나 촬영하지 않습니다.
node scripts/dev.mjs doctor
node scripts/dev.mjs run
```

이미 검증된 네이티브 이미지가 같은 로컬 엔진에 있으면 `container-build.mjs`의 `--use-existing-native`로 관리 호스트와 최종 이미지부터 빌드할 수 있습니다. 고정 MediaPipe 소스·Bazel·모델 정보와 네이티브 검증 명령은 `native/OhMyRudolph.Native/BUILDING.txt`를 따릅니다.

소스에서 직접 `dotnet run`할 때는 `OHMYRUDOLPH_IMAGE_MANIFEST`를 `artifacts/container-bundle/processing-image.json`의 절대경로로 지정해야 합니다. 개발 하네스는 이 경로를 기본으로 전달합니다. 설치 앱은 실행 폴더의 `runtime/processing-image.json`을 사용합니다.

### 로컬 설치 파일 생성

```powershell
node scripts/package-build.mjs --version 1.0.0
```

처리 이미지·manifest·SHA256·앱 버전이 맞지 않으면 실패합니다. 빈 이미지나 설치 체크 우회 옵션을 사용하지 않습니다. Podman CLI 동봉은 명시적인 검증된 파일/라이선스/해시 옵션이 있을 때만 수행하며 개인 Podman 설정·SSH 키는 포함하지 않습니다. 세부 옵션은 [패키징 안내](docs/PACKAGING.md)를 따릅니다.

설치 파일·OCI 이미지·빌드 결과·시험 사진·캡처는 Git 제외입니다. Git push와 이미지 레지스트리 push는 별개이며 이미지 업로드나 설치 파일 공개는 자동 수행하지 않습니다.

## 구조

| 프로젝트 | 책임 |
| --- | --- |
| `OhMyRudolph.Domain` | BCL-only 촬영 상태·전이 |
| `OhMyRudolph.Abstractions` | BCL 얼굴 검출 메타데이터·좌표·응답 계약 |
| `OhMyRudolph.Core` | Windows 카메라·영상 소유권·최신 프레임·합성·Podman 수명·HTTP 클라이언트 |
| `OhMyRudolph.Interop` | C ABI v1 `LibraryImport`·SafeHandle, Linux 네이티브 호출 |
| `OhMyRudolph.ProcessingHost` | Linux .NET 10 HTTP 호스트·인증·요청 제한·네이티브 엔진 수명 |
| `native/OhMyRudolph.Native` | MediaPipe 0.10.21 CPU FaceMesh C ABI와 Linux 빌드 |
| `OhMyRudolph.Wpf.Core` / `Wpf.Controls` | 기존 WPF 변환·테마·재사용 CustomControl |
| `OhMyRudolph.Wpf.UserControls` | 업무 화면 조합·기존 ViewModel·이미지 표시/저장 대화상자 |
| `OhMyRudolph.Wpf` | Generic Host/DI·화면 매핑·Velopack 시작 처리·비동기 종료 |

기존 ViewModel 전체를 별도 WPF-free 프로젝트로 옮긴 구조는 아닙니다. 업무 화면은 UserControl, 재사용 UI는 기존 CustomControl·Theme로 구성합니다. 새 클라우드·플러그인 플랫폼은 추가하지 않았습니다.

## 영상·프로세스 계약

- 프리뷰는 처리 중 1장+최신 대기 1장만 유지합니다. 교체·거절·취소 프레임을 해제하며 확정 촬영본은 별도 소유 사본으로 보존합니다.
- 표시용 픽셀 복사본은 Freeze합니다. 카메라/세션/프레임 식별·원본 크기·미러·회전을 응답과 대조하고 이전 결과를 게시하지 않습니다.
- 촬영은 현재 Running 세션과 원본 캡처 후 **1초 이내** 프레임만 허용합니다. 추론·큐 지연도 나이에 포함하며, 오래된 프레임을 새 사진으로 확정하지 않습니다.
- 원본 JPEG는 Windows 루프백 HTTP로만 전달합니다. 실행별 인증 토큰을 사용하며 사진·좌표·토큰·개인 경로 원문을 제품 로그에 기록하지 않습니다. 프록시·리다이렉트를 허용하지 않습니다.
- 요청 상한8MiB, 메타4096바이트, 영상4096×4096, 응답64KiB, 추론 요청3초, 본문 수신3초입니다. 얼굴 없음과 연결/추론/프로토콜 실패는 별개입니다.
- 컨테이너는 UID10001, capability 제거, 카메라/호스트 데이터 볼륨 없이 실행합니다. 공개 포트는127.0.0.1만 사용합니다.
- 앱은 nonce/ID로 확인한 소유 컨테이너만 정리합니다. 다른 컨테이너·이미지 전체 정리·Podman 머신 종료는 하지 않습니다. 이미지 자동 pull 대신 검증한 동봉 아카이브만 사용합니다.
- 준비 대기의 취소는 프리뷰를 즉시 놓아주지만 이미 진행 중인 startup은 클라이언트 소유로 추적합니다. 효과 없음 프레임은 전송 정리를 기다리지 않으며, 모드 전환/종료는 이전 startup·정리 완료까지 기다립니다. 정리 실패를 성공으로 숨기거나 새 세션을 열지 않습니다.
- 인증된 요청/10초 heartbeat가 기본30초 소유자 lease를 갱신합니다. 소유자가 사라지면 해당 호스트가 종료하고 `--rm`으로 컨테이너를 제거합니다. heartbeat 없는 실제 실행에서 종료73·자동 정리를 관찰했습니다.
- 원격 create의 deadline/통신 단절로 결과가 불확실하면 소유권 marker를 유지하고 오류를 보고합니다. 그 상태에서 WPF까지 강제 종료되면 늦게 생성된 미시작 컨테이너의 자동 회수를 보장하지 못합니다.
- DirectShow의 native Read는 안전하게 강제 중단할 수 없습니다. 3초 중지 제한 뒤 읽기 완료 시 해제를 연기하며, 영구 정지한 드라이버의 즉시 자원 회수는 보장하지 않습니다.

## 개발 하네스·검증

| 명령 | 범위 |
| --- | --- |
| `npm run verify` | Node 정책 테스트·Release 빌드·xUnit. 앱·카메라 실행 없음 |
| `node scripts/dev.mjs doctor` | SDK·Node·Podman 연결·동봉 이미지 해시 점검 |
| `node scripts/dev.mjs container-smoke` | 실제 컨테이너의 합성·공개 양성 추론, 오류·수명 검증. 허용된 시험 JPEG 준비 필요 |
| `node scripts/dev.mjs format-check <파일...>` | 지정한 C#/XAML만 서식 검사 |
| `node scripts/dev.mjs ui-smoke --approved-no-camera` | 실행 직전 승인 후 카메라 없는 실제 UI 조작·정상 종료 |

상세 실행 조건·공개 시험 자료 출처·실측·미실행 범위는 [검증 기록](tests/VALIDATION.md)에 정리합니다. 실카메라 촬영, 실제 저장 실패, 장기 메모리 안정성, Surface 실사용, Setup 설치/업데이트 실행은 아직 통과로 주장하지 않습니다.

SDK는 `global.json`, 패키지는 CPM, 공통 빌드는 `Directory.Build.props`, 편집기는 `.editorconfig`, XAML은 고정 XAML Styler로 관리합니다. Node 하네스는 표준 모듈만 사용합니다. 앱 폴더의 `.omp/extensions/rudolph-guards.js`는 경로형 쓰기·일부 자격 증명·광범위 Python 종료를 점검하지만 셸 파서나 보안 샌드박스는 아닙니다. 현재 OMP 세션 자동 로드를 검증한 것은 아니며 전역 설정/Git 훅은 바꾸지 않았습니다.

## 역할·AI 보조·남은 권리 확인

- 기존 개발에서 소유자는 WPF 아키텍처·화면·CustomControl·Theme를 본인 담당으로 설명했습니다. 원래 개발 기간 기록은 2025년 7월 28일~8월 3일입니다.
- 기존 README의 Python·OpenCV 지식·Rx 연산자·Named Pipe에 대한 AI 보조 사실은 유지합니다. 해당 런타임/스트림 경로는 현재 구현에서 제거했습니다.
- 이번 촬영 흐름·수명 개선·C++/호스트·컨테이너·하네스·패키징·테스트·문서 작성과 검토에도 AI를 사용했습니다. 사용자 승인과 실제 작성자를 구분하며 직접 작성 비율·독자 얼굴 알고리즘 개발을 주장하지 않습니다. 얼굴 검출은 MediaPipe의 기존 모델/알고리즘입니다.
- 기존 정적 사진 모드의 `deadpool.png`는 권리 확인이 남아 있습니다. 새 포토부스 프레임은 이를 사용하지 않습니다. 설치 파일 공개/재배포 전 해당 자산과 포함된 제3자 의존성의 권리 검토가 필요합니다.
