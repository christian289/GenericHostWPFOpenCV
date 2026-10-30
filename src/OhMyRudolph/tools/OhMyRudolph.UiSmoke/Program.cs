using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

if (args.Length is not (2 or 3) || args[0] != "--approved-no-camera" ||
    (args.Length == 3 && args[2] != "--observe"))
{
    Console.Error.WriteLine("실행 직전 사용자 승인이 필요합니다. 승인 범위: 앱 열기→시작→포토부스→프레임 선택→뒤로→닫기. 카메라/사진 저장 없음.");
    return 2;
}
string executable = Path.GetFullPath(args[1]);
using var process = Process.Start(new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)! })
    ?? throw new InvalidOperationException("검증 대상 앱을 시작하지 못했습니다.");
// FlaUI replaces/disposes its own Process wrapper while discovering the main window.
// Keep the original started handle separate so exit-code evidence survives that replacement.
using var app = Application.Attach(process.Id);
using var automation = new UIA3Automation();
try
{
    var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(15))
        ?? throw new InvalidOperationException("실제 WPF 창이 열리지 않았습니다.");
    AutomationElement Find(string id)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(5))
        {
            var element = window.FindFirstDescendant(id);
            if (element is { IsOffscreen: false }) return element;
            Thread.Sleep(50);
        }
        throw new InvalidOperationException($"실제 화면 요소 미발견: {id}");
    }
    void Invoke(string id)
    {
        var element = Find(id);
        if (!element.IsEnabled) throw new InvalidOperationException($"화면 조작 비활성: {id}");
        element.Patterns.Invoke.Pattern.Invoke();
    }
    Invoke("Main.Begin");
    Invoke("Mode.Booth");
    Invoke("Booth.Frame.Dark");
    if (Find("Booth.Frame.Dark").ItemStatus != "선택됨" ||
        Find("Booth.Frame.Light").ItemStatus != "선택 안 됨")
        throw new InvalidOperationException("어두운 프레임 선택 상태가 실제 화면에 반영되지 않았습니다.");
    Invoke("Booth.Frame.Light");
    if (Find("Booth.Frame.Light").ItemStatus != "선택됨" ||
        Find("Booth.Frame.Dark").ItemStatus != "선택 안 됨")
        throw new InvalidOperationException("밝은 프레임 선택 상태가 실제 화면에 반영되지 않았습니다.");
    var start = Find("Booth.StartPreview");
    if (!start.IsEnabled) throw new InvalidOperationException("프레임 선택 후 프리뷰 시작이 비활성입니다.");
    Console.WriteLine("UIA3: 시작→모드→프레임 두 종류 선택→프리뷰 시작 버튼 표시 통과 (카메라는 실행하지 않음)");
    if (args.Length == 3)
    {
        Console.WriteLine("실제 UI 관찰 대기: 앱 창만 관찰한 뒤 Enter를 보내면 뒤로와 정상 닫기를 검증합니다.");
        Console.ReadLine();
    }
    Invoke("Booth.Back");
    _ = Find("Mode.Booth");
    if (!app.Close(false)) throw new InvalidOperationException("실제 앱 정상 닫기 실패");
    if (!process.WaitForExit(15000)) throw new TimeoutException("정상 닫기 뒤 프로세스가 남았습니다.");
    if (process.ExitCode != 0) throw new InvalidOperationException($"앱 종료 코드 {process.ExitCode}");
    Console.WriteLine("UIA3: 뒤로→정상 종료 코드 0 통과. 카메라·촬영·저장 검증은 미실행.");
    return 0;
}
finally
{
    // 이 도구가 띄운 정확한 앱 인스턴스만 정상 닫기를 요청한다.
    if (!process.HasExited) app.Close(false);
}
