# OhMyRudolph
- GitHub 저장소 주소: https://github.com/christian289/GenericHostWPFOpenCV/tree/main/src/OhMyRudolph
- 본 프로젝트는 APR Windows 개발자 JD 서류 전형 합격자를 대상으로한 과제 결과물입니다.
- 개발 기간: 2025년 7월 28일 ~ 8월 3일

## 특징
- Generic Host(Microsoft.Extensions.Hosting)을 사용한 ViewModel 라이프 사이클 관리
- System.Reative를 사용한 가독성 있는 Stream 제어
- WPF 측면에서의 Clean Architecture 적용.
- CustomControl과 Theme컨셉으로 개발하여 엔터프라이즈 어플리케이션의 기반이 될 수 있는 유지보수 구조를 갖춤
- 사진 단건 처리는 FastAPI 기반의 Python Local 서버를 띄워 처리
- 라이브 스트림 처리는 Windows Named Pipe 통신을 사용
- Python MediaPipe의 FaceMesh 기능을 사용하여 코의 좌표를 얻어옴.
- 정적인 형태로 디자인 타임을 활용하여 개발(d:DesignInstance, d:DataContext)
- CommunityToolkit.Mvvm의 Messenger를 통한 Event Aggregator 패턴을 통해 ViewModel만을 제어함으로써 유연한 Navigation 구조를 갖고 있음. (View를 매번 생성하는 오버헤드는 존재함)

## 개선점
- 다수의 카메라일 경우 카메라를 선택하고 관리할 수 있는 관리자 페이지 필요
- 관리자 페이지에서 Log Viewer 제공
- 다양한 얼굴 필터적인 시각적 효과를 Client인 WPF에서 처리할 수 있도록 MEF와 같은 Plugin 구조로 효과를 별도 제공하여 데스크톱 앱의 업데이트 한계점 극복
- NSIS, Wix 등 패키징 필요
- 간헐적으로 WPF 어플리케이션과 Python 로컬 서버와 Windows Named Pipe 통신 시 장애 발생.
- 라이브 스트림 화면 UI 재구성 필요

## 프로젝트 구조
- OhMyRudolph.Abstractions: 본 솔루션의 Root가 되는 추상화 계층을 정의한 것으로 현재 크게 사용되지 않았지만, 추후 유지보수 중 의존성 문제가 발생할 경우 이 프로젝트를 통해 Interface를 정의하여 MVVM 의존 문제 해결 가능.
- OhMyRudolph.Core: 본 솔루션의 기능적인 부분에서의 핵심 기능을 갖고 있는 프로젝트.
- OhMyRudolph.Wpf: 본 솔루션의 실행 가능한 WPF 프로젝트로, 의존 계층의 최하단에서 동작
- OhMyRudolph.Wpf.Controls: WPF 사용자 **지정** 컨트롤 라이브러리 프로젝트(CustomControl). 페이지를 구성할 때 사용할 Theme가 적용된 컨트롤로, UI적으로 가장 작은 단위
- OhMyRudolph.Wpf.Core: UI적인 부분에서의 핵심 기능을 갖고 있는 프로젝트로, 현재는 Converter 정도만 있지만 추후 다양한 Extension을 추가하여 유연하게 사용 가능.
- OhMyRudolPh.Wpf.UserControls: WPF 사용자 **정의** 컨트롤 라이브러리 프로젝트(UserControl). UserControl 이지만, Page로서의 기능을 하고 있기에 화면을 구성할 떄 사용. UI Navigation시 Page 및 Frame 컨트롤은 사용하지 않음.

## 개발 환경
- Windows 10
- .NET 9 WPF
- Visual Studio 2022 17.14.9 (.slnx 파일 지원을 위해 필요)
- mediapipe 0.10.21
- opencv-contrib-python 4.11.0.86
- opencv-python 4.12.0.88
- numpy 1.26.4
- pywin32 311
- fastapi 0.116.1
- python-multipart 0.0.20
- uvicorn 0.35.0

## 실행 방법
1. GitHub 에서 Clone
2. .NET 9를 설치한 뒤 .slnx 파일을 로드할 수 있는 Visual Studio 2022 버전으로 실행
3. 시작 프로젝트 **OhMyRudolph.Wpf** 로 설정
4. 카메라 장치 연결 후 프로젝트 디버깅
5. 또는 빌드 경로에서 OhMyRudolph.Wpf.exe 실행

## Claude AI를 사용한 부분
- Python 코드 작성
- OpenCV를 사용하는 영상처리 지식 전반
- System.Reactive Operator
- Windows Named Pipe 통신 전반