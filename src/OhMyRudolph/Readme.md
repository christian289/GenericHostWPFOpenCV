# OhMyRudolph

무인 사진 촬영 기기의 촬영 흐름을 구현하는 Windows WPF 포토부스 앱입니다. 기존 정적 사진 필터 모드를 유지하면서 **프레임 선택 → 라이브 프리뷰 → 4컷 촬영 → 선택 재촬영 → 합성 사진 로컬 저장 → 새 세션** 흐름을 구성합니다.

기존 원격 소스: https://github.com/christian289/GenericHostWPFOpenCV/tree/main/src/OhMyRudolph

현재 개선은 로컬 작업이며 원격 저장소에는 반영하지 않았습니다.

## 실행 환경과 개발 하네스

- Windows x64, .NET SDK **10.0.403**. `global.json`의 `rollForward=disable`로 이 버전을 사용합니다.
- 실행 앱·영상 구현은 `net10.0-windows`, Domain·Abstractions는 `net10.0`입니다.
- Node.js 24 이상. 하네스는 표준 모듈만 사용하며 npm 패키지 설치가 필요하지 않습니다.
- Python 3.11 전용 가상환경. MediaPipe 0.10.21, OpenCV contrib 4.11.0.86, NumPy 1.26.4 등은 `requirements.txt`로 고정합니다.
- `opencv-python`과 `opencv-contrib-python`을 함께 설치하지 않습니다. 기존 OpenCV 4.12/NumPy 1.26 조합은 의존성 해결에 실패하여 contrib 4.11 하나로 정리했습니다.

저장소 루트에서 다음 폴더로 이동해 실행합니다. 이 폴더의 설정은 다른 샘플에 적용되지 않습니다.

```powershell
cd src/OhMyRudolph
py -3.11 -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r OhMyRudolph.Wpf/requirements.txt
node scripts/dev.mjs doctor
node scripts/dev.mjs restore
node scripts/dev.mjs verify
```

실제 앱을 직접 사용할 때:

```powershell
dotnet run --project OhMyRudolph.Wpf -c Release
```

Python 실행 파일은 `OHMYRUDOLPH_PYTHON`이 지정되면 그 파일을 사용하고, 아니면 실행 폴더의 상위 경로에서 프로젝트 `.venv/Scripts/python.exe`를 찾습니다. 전역 Python으로 조용히 전환하지 않습니다. 배포 폴더에서 실행할 때도 이 환경 또는 명시적인 실행 파일 설정이 필요합니다. 설치 프로그램·독립 실행형 패키징은 제공하지 않습니다.

| 명령 | 범위 |
| --- | --- |
| `npm test` | Node 하네스 정책 테스트. 제품 테스트가 아님 |
| `npm run verify` | 정책 테스트 + .NET 도구 복원·Release 빌드·xUnit. UI와 카메라는 실행하지 않음 |
| `node scripts/dev.mjs doctor` | SDK·Node·프로젝트 Python 환경 점검 |
| `node scripts/dev.mjs pipe-smoke` | 합성 검은 프레임으로 실제 Python/MediaPipe/Named Pipe 실행. 카메라·사용자 사진·저장 없음 |
| `node scripts/dev.mjs format-check OhMyRudolph.Wpf.UserControls/VideoView.xaml` | 지정한 파일의 XAML Styler 검사 |
| `node scripts/dev.mjs format <파일...>` | 지정한 C#/XAML만 포맷. 전체 자동 재포맷 없음 |
| `node scripts/dev.mjs run` | 실제 앱 실행. 자동 촬영하지 않음 |

패키지 버전은 `Directory.Packages.props`, 공통 빌드 속성은 `Directory.Build.props`, 편집기 규칙은 `.editorconfig`, XAML 규칙은 `Settings.XamlStyler`가 담당합니다. XAML Styler 3.2501.8은 로컬 도구 manifest로 고정합니다. 런타임 전환과 무관한 패키지 일괄 업그레이드는 하지 않았습니다.

### 개발 도구의 안전 경계

앱 폴더에서 OMP 세션을 새로 시작하면 `.omp/extensions/rudolph-guards.js`가 프로젝트 진입점입니다. 정책 구현은 `scripts/tool-policy.mjs`이며 경로 인자를 가진 쓰기의 앱 밖 이탈·링크 우회, 일부 자격 증명 패턴, 광범위 Python 종료 명령을 검사합니다. **셸 파서·보안 샌드박스·비공개 자료 전체 검사기가 아니며 hashline patch 전체를 분석하지 않습니다.** 현재 세션에서 자동 로드됐다고 주장하지 않습니다. 실제 로드는 재시작 후 `/extensions`에서 확인해야 합니다. 전역 설정이나 Git 훅은 변경하지 않습니다.

개발 하네스는 제품 기능에 포함하지 않습니다. 사진·얼굴 좌표·자격 증명·개인 경로·외부 업무 자료를 소스나 로그에 넣지 않습니다. 로컬 가상환경·검증 출력은 Git 제외입니다. 공개 전 별도 권리·비공개 자료·정확한 변경 내용 검토와 소유자 승인이 필요합니다.

## 사용 흐름

1. 시작 화면에서 모드를 선택합니다. 포토부스 진입만으로 카메라를 켜지 않습니다.
2. 직접 만든 라이트/다크 프레임을 선택하고 **미리보기 시작**을 누릅니다.
3. 효과 없음 또는 직접 그리는 루돌프 코 효과를 선택합니다. 효과 없음은 Python 추론 서버 없이 동작합니다.
4. 촬영을 시작하면 사진마다 3초 카운트다운으로 4장을 촬영합니다. 취소하면 이미 확정한 사진은 남습니다.
5. 검토 화면에서 사진을 골라 한 장만 다시 찍을 수 있습니다. 마지막 사진도 같은 경로를 사용합니다.
6. 합성 미리보기를 확인하고 PNG/JPEG를 로컬에 저장합니다. 저장 취소·실패는 검토 상태와 확정 사진을 유지합니다.
7. 새 세션을 선택하면 이전 사진·합성본·선택 상태를 초기화합니다. 앱 종료 시 메모리의 미저장 사진은 복구하지 않습니다.

프레임은 자체 생성한 1200×1700의 2×2 배치입니다. 외곽 60px, 간격 40px, 각 사진 520×650 중앙 크롭, 하단 300px이며 원본 종횡비를 늘이지 않습니다. 원본 전체를 보존하는 사진 보관 서비스는 아닙니다. 프린터·결제·QR/클라우드 공유 기능은 없습니다.

## 구조와 책임

| 프로젝트 | 책임 |
| --- | --- |
| `OhMyRudolph.Domain` | 외부 패키지·WPF에 의존하지 않는 촬영 상태·전이 |
| `OhMyRudolph.Abstractions` | BCL 통신 메타데이터·결과 계약. 패키지 모음으로 사용하지 않음 |
| `OhMyRudolph.Core` | Windows 카메라·소유 영상 버퍼·최신 프레임 처리·Python 통신·프레임 합성 |
| `OhMyRudolph.Wpf.Core` | 기존 WPF 변환·테마 리소스 |
| `OhMyRudolph.Wpf.Controls` | 기존 재사용 CustomControl·개별 테마 |
| `OhMyRudolph.Wpf.UserControls` | 업무 화면 조합·기존 ViewModel·WPF 이미지 표시/저장 대화상자 |
| `OhMyRudolph.Wpf` | Generic Host/DI 조립·화면 매핑·앱 시작과 비동기 종료 |

Domain/계약/실행 앱 조립의 의존 방향을 정렬했습니다. 기존 ViewModel을 별도 WPF-free 프로젝트로 전부 이전한 구조는 아닙니다. 업무 화면은 UserControl로 조합하고 재사용 단위는 기존 CustomControl·Theme를 사용합니다. 외부 프로젝트를 실행 의존성으로 두지 않습니다.

## 영상·통신 수명 계약

- 프리뷰 처리 중 1장 + 최신 대기 1장만 소유합니다. 새 대기 프레임이 오면 이전 대기 프레임을 해제합니다. 비동기 구독 콜백을 겹쳐 실행하지 않습니다.
- 프레임은 카메라·세션·프레임 식별자와 원본 크기·미러·회전 정보를 가집니다. 현재 입력은 BGR, 미러 없음·회전 0도입니다. 전환·중지 전 결과를 새 세션에 게시하지 않습니다.
- 표시 이미지는 픽셀을 복사하고 Freeze합니다. 원본 Mat의 소유권은 명시적으로 이동하며 확정 촬영에서만 별도 사본을 만듭니다. 프리뷰 폐기 정책으로 확정 사진을 버리지 않습니다.
- 촬영 snapshot은 현재 실행 중인 카메라·세대와 **원본 캡처 후 1초 이내**의 프레임만 허용합니다. 느린 추론·카메라 정체 시 오래된 사진을 새 촬영으로 확정하는 대신 촬영을 거부합니다.
- 파이프는 앱 인스턴스별로 분리하며 **소유 Python을 새로 시작할 때마다 새 이름**을 만듭니다. 종료 중인 이전 프로세스의 파이프에 재접속하지 않습니다. 요청/응답 길이는 4바이트 little-endian, 전체 요청 상한 8MiB, 응답 상한 64KiB입니다. 요청 본문은 메타데이터 JSON 길이 4바이트 + JSON + JPEG입니다. 응답은 메타데이터·상태·좌표를 담은 JSON입니다.
- 한 연결에는 한 요청/응답만 진행합니다. 연결 제한은 10초, 요청/응답 제한은 3초입니다. 서버도 부분 헤더·본문·쓰기의 대기를 제한합니다. EOF·취소·잘못된 응답·시간초과 뒤 깨진 연결은 재사용하지 않습니다.
- 얼굴 없음, 추론 실패, 연결 실패, 카메라 읽기 실패를 구분합니다. 실패를 빈 성공으로 변환하지 않습니다. 오류 상태에서 원본 프리뷰를 표시하는 것은 화면에 명시하며 효과 적용 성공으로 표시하지 않습니다.
- 소유한 Python 프로세스만 종료합니다. 정적 사진 모드는 기존 FastAPI 경로를 유지하며 전역 Python 프로세스를 일괄 종료하지 않습니다.
- DirectShow 드라이버의 네이티브 Read를 안전하게 강제 중단할 수는 없습니다. 중지 대기는 3초로 제한하고, 정체 시 세대부터 무효화한 뒤 읽기가 끝나면 핸들을 해제합니다. 영구 정지한 드라이버의 즉시 자원 회수는 보장하지 않습니다.

## 역할과 AI 보조

- **기존 개발:** 소유자는 WPF 아키텍처·화면 구성·CustomControl·Theme 설계를 본인 담당으로 설명했습니다. 최초 개발 기간 기록은 2025년 7월 28일~8월 3일입니다.
- **기존 AI 보조:** 원래 README에 Python 코드, OpenCV 영상처리 지식, System.Reactive 연산자, Windows Named Pipe 통신의 Claude AI 보조가 명시돼 있었습니다. 이번 프리뷰 수명 개선으로 Rx 경로는 제거했지만 원래 도움받은 사실은 지우지 않습니다.
- **이번 개선:** 사용자가 범위·설계·환경 전환·구조 정렬을 승인했고, 코드·테스트·하네스·문서 작성과 검토에는 AI를 사용했습니다. 사용자가 이번 코드를 전부 직접 작성했거나 개인 알고리즘을 독자 개발했다고 주장하지 않습니다.
- 코드 작성 비율·작업 시간·무결함·상용 무인 운영 적합성을 주장하지 않습니다. 면접에서는 상태 전이, 최신 프레임 정책, Mat 소유권, 파이프 오류와 검증 한계를 설명하는 것을 목표로 합니다.

## 검증과 남은 제한

검증 명령·시나리오와 실제 관찰 범위는 [검증 기록](tests/VALIDATION.md)에 정리합니다. 단위/로컬 통합 검증과 실물 WPF·실카메라 검증을 구분합니다.

실제 UI 자동조작은 실행 직전 승인 후에만 다음 도구를 실행합니다. 기본 verify는 이 도구를 실행하지 않습니다.

```powershell
node scripts/dev.mjs ui-smoke --approved-no-camera
```

이 경로는 FlaUI UIA3로 앱 열기 → 시작 → 포토부스 → 두 프레임 선택과 선택 상태 → 뒤로 → 정상 닫기만 검사합니다. `--observe`를 추가하면 프레임 선택 화면에서 Enter 입력까지 기다리므로 앱 창만 별도로 관찰·캡처할 수 있습니다. 승인된 관찰에서 창 캡처를 `artifacts/ui-smoke.png`에 보관했습니다. **카메라·실촬영·사진 저장은 하지 않습니다.** 실제 카메라 사용과 사용자 사진 저장은 별도 승인이 필요합니다.

기존 정적 사진 모드에는 권리가 확인되지 않은 `deadpool.png`가 남아 있습니다. 새 포토부스는 이 자산을 사용하지 않습니다. 정적 모드·해당 자산의 공개/재배포 가능 여부는 미확인이며, 공개 전 확인이 필요합니다. 새 플랫폼·추론 엔진·프린터·공유 기능·자동 업데이트·설치 프로그램은 이번 범위가 아닙니다.
