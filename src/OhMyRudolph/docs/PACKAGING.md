# 로컬 OCI 이미지와 Velopack 패키징

이 절차는 Windows x64 WPF와 Linux amd64 CPU 처리 호스트를 대상으로 한다. **실행 결과나 Surface 설치 성공의 증거가 아니다.** UI·카메라·사진 저장과 실제 설치는 각각 별도 승인과 검증이 필요하다. 생성물은 앱 디렉터리의 무시된 `artifacts/`에만 남으며 업로드하지 않는다.

## 사전 조건

- Node.js 24, 저장소의 `global.json`에 고정된 .NET SDK 10.0.403, 복원된 로컬 도구 `vpk` 1.2.161 (`dotnet tool restore`) 및 NuGet 접근.
- WSL2와 Podman이 이미 준비되어 있어야 한다. 이 스크립트는 Windows 기능 활성화, WSL/Podman 설치, 관리자 권한 획득, 재부팅, 머신 시작·중지, 전역 설정 변경 또는 방화벽 변경을 하지 않는다. 다른 컨테이너도 종료하거나 정리하지 않는다.
- 지정 연결은 Linux amd64를 실행할 수 있어야 한다. 런타임 계약은 준비된 `podman-machine-default`의 rootless 연결 `podman-machine-default`와 `pasta`이다. 빌드 명령에만 `--network=host`를 전달한다. 런타임은 host 네트워크를 사용하지 않는다. 다른 연결을 명시할 경우에도 대상 머신이 위 런타임 계약과 일치하는지 별도 확인해야 한다. 스크립트는 전역 기본 연결을 바꾸지 않는다.
- WPF의 OpenCvSharp Windows 네이티브 라이브러리는 대상 Windows에서 필요한 Microsoft Visual C++ x64 런타임이 준비되어 있어야 한다. 이것은 WSL/Podman과 별개이며 본 패키지는 이를 자동 설치하지 않는다. WPF 앱은 self-contained .NET을 포함하지만 OS와 이 네이티브 선행 조건을 포함하지 않는다.
- `native/OhMyRudolph.Native/BUILDING.txt`의 고정 MediaPipe/모델 출처 및 라이선스를 확인하고, 실제 native 빌드와 권리 확인된 양성 얼굴 fixture를 포함한 FaceMesh 검증을 먼저 완료해야 한다. 빌드 성공이나 빈 결과만으로 추론 성공을 주장하지 않는다. Python은 native 빌드 전용이며 최종 이미지에 포함되지 않는다.
- 이 절차는 저장소의 완성된 새 컨테이너 클라이언트 전환 후 실행한다. 구 Python 경로가 남은 중간 상태를 배포하지 않는다.

## 이미지 빌드 및 OCI export

앱 디렉터리 `src/OhMyRudolph`에서 PowerShell 예시(경로는 실제 절대경로로 교체):

```powershell
$env:OHMYRUDOLPH_PODMAN = (Resolve-Path '.\artifacts\podman-6.0.2\usr\bin\podman.exe').Path
$env:OHMYRUDOLPH_PODMAN_CONNECTION = 'podman-machine-default'
node scripts/container-build.mjs --version 1.0.0 --use-existing-native
```

`--use-existing-native`는 이미 검증한 `localhost/ohmyrudolph-native-build:local`을 사용한다. 생략하면 native 이미지부터 다시 빌드한다. 실행 파일은 `--podman <절대경로>`, 연결은 `--connection <이름>`으로도 명시할 수 있다. 도움말은 `node scripts/container-build.mjs --help`.

최종 이미지는 self-contained .NET 10 호스트, ABI 라이브러리, 모델과 MediaPipe 라이선스만 선택해 복사한다. 고정 XNNPACK이 요구하는 GCC 13 플래그를 지원하기 위해 native 빌더와 최종 런타임을 모두 Ubuntu 24.04(Noble)로 맞추고 OpenCV 4.6 런타임 패키지를 사용한다. native `ldd`에서 누락이 확인되면 이미지 빌드가 실패한다. 실제 빌드 후 의존성과 실행도 별도로 검증해야 한다. `native_smoke`, Bazel, Python 및 원본 빌드 환경은 최종 이미지에 복사하지 않는다. 이미지 레이어 및 빌드 컨텍스트에 사진, 개인 설정, 자격 증명을 넣지 않는다. 컨텍스트는 `.containerignore` allowlist로 제한한다. Ubuntu 태그와 apt 저장소는 스냅샷 고정이 아니므로 비트 단위 재현 빌드를 보장하지 않는다.

생성 결과:

- `artifacts/container-bundle/processing-image.oci.tar`: `podman save --format oci-archive`로 내보낸 이미지.
- `artifacts/container-bundle/processing-image.json`: schema 1, 앱/호스트 버전, 계약/ABI 1, linux/amd64, Podman 이미지 config digest ID, OCI 아카이브 SHA-256, 머신/연결/네트워크/포트. 아카이브 파일 이름은 고정이며 임의 경로를 허용하지 않는다.

`--version`은 build metadata 없는 SemVer(예: `1.0.0` 또는 `1.0.0-rc.1`)이고 이미지 라벨, .NET 호스트, manifest와 패키지에 같은 값을 사용한다. 빌드 도우미는 이미지 ID와 아카이브 해시를 출력한다. 앱은 아카이브를 load하기 전 해시를 확인하고 load 후 불변 ID를 확인해야 한다. manifest 자체는 서명되지 않으므로 해시만으로 배포처 신뢰성이나 출처를 보장하지 않는다.

## Velopack 로컬 패키징

```powershell
# dotnet이 PATH에 없는 경우 --dotnet 'C:\Program Files\dotnet\dotnet.exe' 추가
node scripts/package-build.mjs --version 1.0.0
```

도움말은 `node scripts/package-build.mjs --help`. 다른 bundle을 사용하려면 `--manifest <절대경로>`를 지정한다. 패키징은 manifest 계약과 버전 및 실제 아카이브 SHA-256을 검사하고, 격리된 임시 스테이징에 `dotnet publish -r win-x64 --self-contained true`한 뒤 `runtime/`에 아카이브와 manifest를 복사하고 다시 해시를 검사한다. `dotnet tool run vpk`는 `--mainExe OhMyRudolph.Wpf.exe`를 명시한다. 결과는 매 실행마다 별도의 `artifacts/velopack-<버전>-<임의값>/`에 생성되고 경로가 출력된다. 도구 미복원, 누락/변조된 번들, 버전 불일치, publish 또는 pack 실패는 오류다. 확인 건너뛰기 옵션은 없다.

### 선택: 공식 portable Podman CLI 동봉

CLI 없이 패키징하는 경우 대상에 준비된 Podman 실행 파일이 필요하다. CLI를 동봉하려면 공식 배포본의 `usr/bin`에서 `podman.exe`, `gvproxy.exe`, `win-sshproxy.exe`와 해당 배포본 라이선스를 별도 확보하고 **신뢰 가능한 출처에 대조해 검증한** 각 SHA-256을 JSON에 넣는다. 대상 파일과 같은 곳에서 계산한 해시만으로 출처 신뢰를 입증할 수 없다. 예시 JSON 키는 정확히 `podman.exe`, `gvproxy.exe`, `win-sshproxy.exe`, `LICENSE`이며 값은 소문자 64자리 SHA-256이다. 사용자는 라이선스 및 제3자 고지 요건도 확인해야 한다.

```powershell
node scripts/package-build.mjs --version 1.0.0 `
  --podman-dir 'C:\absolute\verified-podman\usr\bin' `
  --podman-license 'C:\absolute\verified-podman\LICENSE' `
  --podman-hashes 'C:\absolute\verified-podman\sha256.json'
```

세 인자를 모두 지정해야 하며 파일과 복사본을 모두 검증한다. `runtime/podman/`에는 위 파일만 들어가고 Podman 사용자 설정, 머신 키, 인증서 또는 사진은 복사하지 않는다. CLI 동봉은 WSL2나 Podman 머신 설치를 대체하지 않는다.

## 운영 경계와 검증

사진과 사용자 상태는 Velopack 버전별 애플리케이션 디렉터리 및 이미지 밖에 보관한다. 자동 업데이트 feed, 업로드, 코드 서명, 인증서 또는 SmartScreen 신뢰 보장은 제공하지 않는다. 릴리스 전 별도 승인 하에 실제 대상에서 설치, 아카이브 load→ID 확인→loopback readiness→합성 요청→앱 소유 컨테이너 종료 및 재기동을 검증한다. 다른 머신이나 다른 앱 컨테이너는 중단하지 않는다.

정책 단위 테스트 명령은 `node --test scripts/container-policy.test.mjs`이다. 이는 이미지 빌드, 추론, 설치 또는 실제 Surface 테스트를 대신하지 않는다.
