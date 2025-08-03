# OhMyRudolph
- �� ������Ʈ�� APR Windows ������ JD ���� ���� �հ��ڸ� ��������� ���� ������Դϴ�.
- ���� �Ⱓ: 2025�� 7�� 28�� ~ 8�� 3��

## Ư¡
- Generic Host(Microsoft.Extensions.Hosting)�� ����� ViewModel ������ ����Ŭ ����
- System.Reative�� ����� ������ �ִ� Stream ����
- WPF ���鿡���� Clean Architecture ����.
- CustomControl�� Theme�������� �����Ͽ� ������������ ���ø����̼��� ����� �� �� �ִ� �������� ������ ����
- ���� �ܰ� ó���� FastAPI ����� Python Local ������ ��� ó��
- ���̺� ��Ʈ�� ó���� Windows Named Pipe ����� ���
- Python MediaPipe�� FaceMesh ����� ����Ͽ� ���� ��ǥ�� ����.
- ������ ���·� ������ Ÿ���� Ȱ���Ͽ� ����(d:DesignInstance, d:DataContext)
- CommunityToolkit.Mvvm�� Messenger�� ���� Event Aggregator ������ ���� ViewModel���� ���������ν� ������ Navigation ������ ���� ����. (View�� �Ź� �����ϴ� �������� ������)

## ������
- �ټ��� ī�޶��� ��� ī�޶� �����ϰ� ������ �� �ִ� ������ ������ �ʿ�
- ������ ���������� Log Viewer ����
- �پ��� �� �������� �ð��� ȿ���� Client�� WPF���� ó���� �� �ֵ��� MEF�� ���� Plugin ������ ȿ���� ���� �����Ͽ� ����ũ�� ���� ������Ʈ �Ѱ��� �غ�
- NSIS, Wix �� ��Ű¡ �ʿ�
- ���������� WPF ���ø����̼ǰ� Python ���� ������ Windows Named Pipe ��� �� ��� �߻�.
- ���̺� ��Ʈ�� ȭ�� UI �籸�� �ʿ�

## ������Ʈ ����
- OhMyRudolph.Abstractions: �� �ַ���� Root�� �Ǵ� �߻�ȭ ������ ������ ������ ���� ũ�� ������ �ʾ�����, ���� �������� �� ������ ������ �߻��� ��� �� ������Ʈ�� ���� Interface�� �����Ͽ� MVVM ���� ���� �ذ� ����.
- OhMyRudolph.Core: �� �ַ���� ������� �κп����� �ٽ� ����� ���� �ִ� ������Ʈ.
- OhMyRudolph.Wpf: �� �ַ���� ���� ������ WPF ������Ʈ��, ���� ������ ���ϴܿ��� ����
- OhMyRudolph.Wpf.Controls: WPF ����� **����** ��Ʈ�� ���̺귯�� ������Ʈ(CustomControl). �������� ������ �� ����� Theme�� ����� ��Ʈ�ѷ�, UI������ ���� ���� ����
- OhMyRudolph.Wpf.Core: UI���� �κп����� �ٽ� ����� ���� �ִ� ������Ʈ��, ����� Converter ������ ������ ���� �پ��� Extension�� �߰��Ͽ� �����ϰ� ��� ����.
- OhMyRudolPh.Wpf.UserControls: WPF ����� **����** ��Ʈ�� ���̺귯�� ������Ʈ(UserControl). UserControl ������, Page�μ��� ����� �ϰ� �ֱ⿡ ȭ���� ������ �� ���. UI Navigation�� Page �� Frame ��Ʈ���� ������� ����.

## ���� ȯ��
- Windows 10
- .NET 9 WPF
- Visual Studio 2022 17.14.9 (.slnx ���� ������ ���� �ʿ�)
- mediapipe 0.10.21
- opencv-contrib-python 4.11.0.86
- opencv-python 4.12.0.88
- numpy 1.26.4
- pywin32 311
- fastapi 0.116.1
- python-multipart 0.0.20
- uvicorn 0.35.0

## ���� ���
1. GitHub ���� Clone
2. .NET 9�� ��ġ�� �� .slnx ������ �ε��� �� �ִ� Visual Studio 2022 �������� ����
3. ���� ������Ʈ **OhMyRudolph.Wpf** �� ����
4. ī�޶� ��ġ ���� �� ������Ʈ �����
5. �Ǵ� ���� ��ο��� OhMyRudolph.Wpf.exe ����

## Claude AI�� ����� �κ�
- Python �ڵ� �ۼ�
- OpenCV�� ����ϴ� ����ó�� ���� ����
- System.Reactive Operator
- Windows Named Pipe ��� ����