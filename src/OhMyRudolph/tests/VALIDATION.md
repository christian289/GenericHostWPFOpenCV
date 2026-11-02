# 검증 기록

현재 Windows WPF → 루프백 HTTP → Podman의 .NET Linux 호스트 → MediaPipe C++ 경로의 실행 증거입니다. 과거 Python/Named Pipe 검증은 체크포인트 `ea2f99aa6b001f0ba98e354086ddd7e3363925f1`의 이력이며 현재 실행 수에 합산하지 않습니다. 원래 사용자가 관찰한 모든 간헐 오류의 원인을 특정하거나 무결함을 증명한 기록이 아닙니다.

## 환경과 명령

- 개발 PC: Windows x64, SDK 10.0.403 / .NET 10.0.14, Node 24.14.0.
- WSL2의 기존 `podman-machine-default`, 공식 portable Podman 6.0.2, 명시적 rootless 연결 `podman-machine-default`, 런타임 `pasta`.
- Linux amd64 Ubuntu 24.04, GCC 13, Bazel 6.5.0, MediaPipe 0.10.21, OpenCV 4.6. Python은 native 빌드 단계에만 사용합니다.
- 전역 기본 연결·방화벽·커널 설정은 바꾸지 않았습니다. 카메라·사용자 사진·외부 업무 자료는 사용하지 않았습니다.

앱 디렉터리에서 실행했습니다. CLI 위치는 환경변수의 검증된 절대경로 또는 PATH로 지정합니다.

```powershell
npm run verify
node scripts/dev.mjs doctor
node scripts/dev.mjs container-smoke
node scripts/package-build.mjs --version 1.0.0
```

`container-smoke`는 아래 공개 fixture를 준비해야 합니다. UI를 열거나 카메라를 켜지 않습니다. 이미지 빌드/패키징 절차와 선행 조건은 `../docs/PACKAGING.md`를 따릅니다.

## 빌드·단위 계약

| 대상 | 실제 관찰 |
| --- | --- |
| Release 솔루션 빌드 | 오류 0. 기존 nullable 경고 5건(Core helper/기존 효과 3, WPF 기존 converter 2)은 남음 |
| Domain xUnit | 11/11 통과, 실패·건너뜀 0 |
| 영상·컨테이너 클라이언트 xUnit | 49/49 통과, 실패·건너뜀 0 |
| 처리 호스트 xUnit | 19/19 통과, 실패·건너뜀 0 |
| Node 정책 테스트 | 8/8 통과 |
| 선택적 포맷 | 새/변경 C# 구현·테스트 및 PhotoView.xaml에 적용, 종료 코드 0. workspace 로드 경고가 있어 전체 솔루션 무경고/전체 재포맷을 주장하지 않음 |

각 프로젝트 `.claude/test-inventory.md`에서 메서드와 실행 사례를 구분합니다. 시작 대기 취소·동시 종료 합류·시작 중 폐기·종료 대기 취소·정리 실패 시 재시작 차단은 제어 가능한 외부 runtime 수명으로 검사합니다. 실제 컨테이너 증거와 가짜 runtime 기반 수명 단위 테스트를 혼동하지 않습니다.

컴파일된 `dotnet test --list-tests` 목록과 인벤토리를 대조해 Domain 9메서드/11사례, Core 35메서드/49사례, Host 9메서드/19사례 모두 누락·초과 0을 확인했습니다.

## 실제 Linux C++ 추론

- 공식 CPU Hello World 실행에서 메시지 10회를 확인했습니다.
- 최초 native 분석은 비공개 Bazel target 참조로 실패했습니다. 고정 upstream의 공개 `calculator_framework`로 수정했습니다.
- Ubuntu 22.04/GCC 11 native 빌드는 XNNPACK의 `-mavxvnniint8` 미지원으로 실패했습니다. 커널을 끄거나 Windows를 수정하지 않고 native 빌더와 런타임을 Ubuntu 24.04/GCC 13으로 일치시킨 뒤 실제 빌드했습니다.
- 최종 native 빌드: 2,486 action, native 단계 1,095.458초, 전체 이미지 빌드 1,809.33초. 이는 해당 PC·캐시 상태의 한 실행이지 일반 설치 소요 시간이 아닙니다.
- ABI 1, 누락 모델 실패, create/destroy 반복 3회, 합성 얼굴 없음, JPEG 손상, 크기 불일치, 실제 양성 얼굴, 양성 출력 버퍼 부족을 native smoke에서 확인했습니다.
- 얼굴 좌표는 로그에 출력하지 않았습니다. 정확도·다양한 자세·조명·인구집단별 성능 검증은 아닙니다.

### 허용된 공개 시험 자료

[scikit-image 0.25.2의 astronaut 자료와 NASA public-domain 설명](https://scikit-image.org/docs/0.25.x/api/skimage.data.html#skimage.data.astronaut)을 사용했습니다. [고정 원본 PNG](https://raw.githubusercontent.com/scikit-image/scikit-image/v0.25.2/skimage/data/astronaut.png)는 Git 제외 `artifacts/astronaut.png`에만 보관하며 사진을 소스/로그/이미지/설치 파일에 넣지 않습니다.

- 원본: 512×512 RGB, SHA-256 `88431cd9653ccd539741b555fb0a46b61558b301d4110412b5bc28b5e3ea6cb5`.
- 준비 명령: `dotnet run --project tools/OhMyRudolph.ContainerSmoke -c Release -- --prepare-fixture artifacts/astronaut.png artifacts/astronaut.jpg`.
- 파생 JPEG: quality 95, SHA-256 `011901a3f9084e22497e2b27642b44a39e8965c4c2febc5ddf2c3ccf298c8787`.
- 변환에서 libpng의 incorrect sRGB ICC 경고를 관찰했습니다. 색 보정 품질을 검증한 자료로 사용하지 않습니다.

## 실제 컨테이너 통신·수명·번들

| 실행 | 조건과 관찰 |
| --- | --- |
| 1.0.0 정상/오류 | 3세션 × (합성 검은 320×240 12회 + 공개 양성 512×512 1회), 총 39회 정상 추론. 인증 401·잘못된 봉투 400·과대 본문 413·손상 JPEG 400·부분 본문 408 확인. 총 21.47초 |
| 1.0.0 cold import | 이번 앱의 해당 이미지 태그만 non-force 제거 후 동봉 OCI load·ID 확인·3세션 39회와 오류 경로 통과. 총 31.27초. 다른 이미지/컨테이너 prune 없음 |
| 1.0.0 heartbeat | 35초 유휴 중 10초 heartbeat가 30초 lease 유지, 같은 세션 요청 재개. 3세션 총 40회, 총 58.24초. 정상 합성 36회 왕복 p50 4.80ms, p95 45.91ms |
| 1.0.1 버전 교체 | 호스트·manifest·Windows 클라이언트를 모두 1.0.1로 빌드, 3세션 총 40회와 같은 오류·35초 유휴 경로 통과. 총 56.79초, 합성 왕복 p50 4.93ms, p95 45.23ms |
| 버전 불일치 | 기본 1.0.0 클라이언트가 1.0.1 manifest를 시작 전 InvalidDataException으로 거부(종료 1). 기본 개발 번들은 검증된 1.0.0으로 복원 |
| 소유자 소실 | 검사용 소유 컨테이너에서 heartbeat 없이 실제 호스트 종료 코드 73, 약 30.82초 후 종료 및 `--rm` 자동 정리 확인 |
| 프로세스 격리 | 실제 공개 양성 추론 뒤 이번 검사 소유 ID만 SIGKILL. 후속 HTTP 연결 실패와 검사 클라이언트 프로세스의 계속 실행 확인. 실제 WPF 창의 장애 UI 검증은 아님 |
| 최종 PATH 전용 CLI | 명시 실행 파일 환경변수와 동봉/개발용 기본 경로를 사용하지 않고 PATH만으로 3세션 40회·오류·heartbeat 통과. 총 60.12초, 합성 왕복 p50 6.00ms, p95 49.37ms. 검사 후 개발용 CLI 위치 복원 |

왕복 시간은 위 PC의 짧은 합성 입력 HTTP 처리 시간입니다. 실카메라·1080p·Surface·end-to-end 촬영 지연·장기 메모리 안정성으로 일반화하지 않습니다. native 호출은 협력 취소를 지원하지 않으므로 요청 실패 뒤 소유 컨테이너 경계로 격리하며 모든 Python/Podman 프로세스를 종료하지 않습니다.

### 실패 전·수정 후

- 부분 본문을 보낸 채 연결을 유지하는 실제 HTTP smoke는 최초에 기대한 시간초과 응답을 얻지 못해 실패했습니다. 호스트에 3초 본문 수신 제한과 명시적 408을 추가한 뒤 같은 raw TCP 시나리오가 통과했습니다.
- 실제 Podman image ID 출력이 prefix 없는 64자리임을 관찰하여 strict digest 정규화로 수정했습니다. 운영체제 확인은 지원되지 않는 template 대신 실제 `info --format json` 계약으로 수정했습니다.
- 최종 정적 리뷰에서 이전 영상 작업의 늦은 공유 클라이언트 종료, 생성 대기 중 효과 없음 복귀 지연을 발견했습니다. 세대 검증·정리 체인 및 클라이언트 소유 startup/호출자 대기 분리로 수정했고 관련 수명 xUnit을 실행했습니다. 원래 사용자의 카메라 장애를 이 경합으로 재현했다는 의미는 아닙니다.

### 합성 영상으로 실제 업무 ViewModel 연동

카메라 장치가 아닌 자체 생성 64×64 MJPEG AVI(50,000프레임, 파일 디코딩 속도는 실카메라 fps가 아님)를 실제 `CameraService`와 `DetectingNoseViewModel`에 연결했습니다. 창을 열지 않았고, 외부 runtime만 생성 완료를 제어하는 지연 구현을 사용했습니다. 얼굴 추론을 통과한 것으로 계산하지 않습니다.

- 생성 대기가 끝나지 않은 상태에서 효과를 해제한 뒤 원본 프리뷰가 **14.64ms**에 게시되고 합성 현재 프레임 확정이 가능한 것을 한 번 관찰했습니다. 성능 목표나 실카메라 수치가 아닙니다.
- Stop이 생성·정리 완료까지 기다리는지, 이후 새 모드 세션 시작과 이전 프레임 작업자 폐기 뒤에도 새 소유 세션이 유지되는지 확인했습니다.
- 첫 검사에서는 위 동작 뒤 시험 AVI를 핸들 해제 전에 삭제하는 검사 도구 정리 오류가 발생했습니다. 도구를 실제 `CameraService.DisposeAsync` 뒤 삭제하도록 고쳐 같은 검사를 종료 코드 0으로 완료했습니다. 이를 제품의 영상 파일 저장 실패로 계산하지 않습니다.
- 임시 검사 프로젝트·합성 AVI는 검증 뒤 제거합니다. 물리 카메라·실제 UI·사용자 사진 저장은 실행하지 않았습니다.

## 패키징 경계

실제 Velopack 1.2.161로 win-x64 self-contained publish, portable ZIP, full nupkg, Setup.exe를 생성했습니다. 패키지 안 `runtime/processing-image.json`과 OCI 파일을 열어 버전·SHA-256 일치를 확인했습니다. 1.0.0 처리 이미지 ID는 `sha256:aa3d982a3ed4ea65f9c26bd7d2a0bd9b115c57293fd74ab8fb1cf65eb4ac862f`, OCI SHA-256은 `8a8c77aa99c3b3c381e7f6f530f4f7947dc4dff4ef08edf04c1f53b093e57da7`, OCI 크기는 361,622,016 bytes입니다. Windows 패키지 항목에 Python 스크립트·venv·Git·시험 사진이 없는 것도 확인했습니다.

설치 프로그램은 서명하지 않았습니다. **Setup 실행/설치·실제 Velopack 업그레이드·깨끗한 PC·Surface Pro 9는 미실행**입니다. 1.0.1 검사도 호스트/manifest/client 정합성 검사이지 설치 업그레이드 성공이 아닙니다. WSL2·Podman 머신·필요 VC++ x64 런타임은 별도 선행 조건입니다. CLI 동봉 선택 경로는 최종 기본 패키지에서 사용하지 않았으며 대상의 Podman CLI가 필요합니다. 런타임 이미지와 소스의 제3자 고지는 공개 전 별도로 검토해야 합니다.

## UI·장치·업무 흐름의 미실행 경계

- 이전 체크포인트에서 별도 승인한 FlaUI UIA3의 시작 → 포토부스 → 라이트/다크 선택 → 뒤로 → 정상 닫기는 통과했습니다. 당시 창 캡처는 Git 제외 파일입니다. **컨테이너 전환 이후 실제 창 재검증은 사용자가 나중에 진행하기로 선택하여 실행하지 않았습니다.** 실카메라 촬영도 포함하지 않습니다.
- 카운트다운 취소·마지막 사진 재촬영·저장 실패 후 검토 보존·다음 세션 초기화는 Domain 상태/경계 xUnit 통과입니다. 실제 촬영 UI/저장 대화상자/권한 오류 검증은 아닙니다.
- 실제 카메라 교체·분리·드라이버 정체·반복 시작/정지, 실촬영 사진 4장과 합성 저장은 미실행입니다. 세대·식별·1초 최신성·합성 배치/크롭·소유권만 단위 테스트에서 확인했습니다.
- DirectShow의 native Read는 안전하게 강제 중단할 수 없습니다. 3초 중지 제한 뒤 읽기 종료 시 핸들 해제를 연기하며, 오래된 프레임은 촬영으로 확정하지 않습니다.
- 원격 create가 불명확한 채 frontend가 강제 종료되면 뒤늦게 생성된 미시작 컨테이너는 host lease를 실행할 수 없습니다. 소유 이름/nonce를 보존하는 fail-closed 경계이며, 모든 컨테이너 정리로 숨기지 않습니다.
- 프린터·결제·클라우드·외부 공유는 제품 기능도, 실행한 검증도 아닙니다. 기존 정적 이미지 효과의 `deadpool.png` 권리는 확인되지 않았으므로 공개 설치물/시연 자료 사용 전 확인이 필요합니다.
- 구현은 전용 기능 브랜치에서 관리하고 승인된 개인 원격의 해당 브랜치에만 push합니다. main 병합·설치 파일 공개 업로드·이미지 레지스트리 push는 하지 않습니다. 정확한 커밋은 최종 전달 기록을 따릅니다.
