# GenericHostWPFOpenCV
.NET Generic Host와 OpenCV를 사용하여 DevExpress WPF MVVM 개발 예제입니다.

1. App.xaml.cs
    - DevExpress MVVM Framework(이하 dxmvvm)에서 Messenger 방식으로 ViewModel간 데이터 교환을 합니다.
    - ServiceProvider를 이용하여 Generic Host를 사용합니다.
    - Generic Host에 각 ViewModel을 dxmvvm POCO형태로 등록합니다.
2. ViewModel/ViewModelLocator.cs
    - 뷰모델 로케이터에서 dxmvvm의 POCO 형태로 뷰모델 리소스를 지정합니다.
    - dxmvvm의 사용법은 [DevExpress WPF MVVM Framework](https://docs.devexpress.com/WPF/15112/mvvm-framework)에서 알 수 있습니다.
    - DevExpress의 WPF 컨트롤을 이용하는 것은 라이센스가 필요하지만, Nuget에 있는 MVVM Framework를 이용하는 것에는 별도 라이센스 없이 사용할 수 있습니다.
3. View/Canny.xaml, View/FindContour_ApproxPolyDP.xaml, View/FindContour_MinAreaRect.xaml, View/MainWindow.xaml
    - DataContext에서 ViewModelLocator를 이용하여 뷰모델을 지정합니다.
