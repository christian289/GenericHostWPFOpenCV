import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, resolve, relative, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const solution = 'OhMyRudolph.slnx';
const python = resolve(root, '.venv', 'Scripts', 'python.exe');
const [command = 'help', ...files] = process.argv.slice(2);
function run(executable, args) {
    const result = spawnSync(executable, args, {
        cwd: root,
        stdio: 'inherit',
        shell: false,
        env: { ...process.env, OHMYRUDOLPH_PYTHON: process.env.OHMYRUDOLPH_PYTHON ?? python }
    });
    if (result.error) throw result.error;
    if (result.status !== 0) process.exit(result.status ?? 1);
}
function build() { run('dotnet', ['build', solution, '-c', 'Release', '--nologo']); }
function test() { run('dotnet', ['test', solution, '-c', 'Release', '--no-build', '--nologo']); }
function scopedFile(file) {
    const path = resolve(root, file);
    const rel = relative(root, path);
    if (rel.startsWith('..') || isAbsolute(rel) || !existsSync(path)) throw new Error('앱 내부의 기존 파일만 지정하세요.');
    return rel;
}
function format(check) {
    if (!files.length) throw new Error('전체 재포맷 방지를 위해 파일을 명시하세요: format-check OhMyRudolph.Wpf/MainWindow.xaml');
    run('dotnet', ['tool', 'restore']);
    const cs = [];
    for (const file of files.map(scopedFile)) {
        if (file.endsWith('.xaml')) {
            run('dotnet', ['tool', 'run', 'xstyler', '--', '-f', file, '-c', 'Settings.XamlStyler', ...(check ? ['--passive'] : [])]);
        } else if (file.endsWith('.cs')) cs.push(file);
        else throw new Error('포맷 대상은 .cs 또는 .xaml이어야 합니다.');
    }
    if (cs.length) run('dotnet', ['format', solution, '--no-restore', ...(check ? ['--verify-no-changes'] : []), '--include', ...cs]);
}
try {
    switch (command) {
        case 'doctor':
            run('dotnet', ['--version']);
            console.log(`Node ${process.version}`);
            if (!existsSync(python)) throw new Error('앱 .venv가 없습니다. py -3.11 -m venv .venv 후 requirements.txt를 설치하세요.');
            run(python, ['-c', 'import sys, cv2, mediapipe, numpy, win32pipe, fastapi; print("Python", sys.version.split()[0]); print("MediaPipe", mediapipe.__version__); print("OpenCV", cv2.__version__)']);
            break;
        case 'restore': run('dotnet', ['tool', 'restore']); run('dotnet', ['restore', solution]); break;
        case 'build': build(); break;
        case 'test': build(); test(); break;
        case 'verify': run('dotnet', ['tool', 'restore']); build(); test(); break;
        case 'format': format(false); break;
        case 'format-check': format(true); break;
        case 'pipe-smoke':
            run('dotnet', ['run', '--project', 'tools/OhMyRudolph.PipeSmoke', '-c', 'Release']);
            break;
        case 'ui-smoke':
            if (files.length < 1 || files.length > 2 || files[0] !== '--approved-no-camera' ||
                (files.length === 2 && files[1] !== '--observe'))
                throw new Error('실행 직전 사용자 승인 후 ui-smoke --approved-no-camera로 실행하세요. 카메라/사진 저장은 실행하지 않습니다.');
            run('dotnet', ['run', '--project', 'tools/OhMyRudolph.UiSmoke', '-c', 'Release', '--',
                '--approved-no-camera', resolve(root, 'OhMyRudolph.Wpf/bin/Release/net10.0-windows/OhMyRudolph.Wpf.exe'),
                ...(files[1] === '--observe' ? ['--observe'] : [])]);
            break;
        case 'run':
            if (!existsSync(python)) throw new Error('먼저 doctor로 Python 환경을 확인하세요.');
            run('dotnet', ['run', '--project', 'OhMyRudolph.Wpf', '-c', 'Release']);
            break;
        case 'help':
            console.log('개발 하네스: node scripts/dev.mjs doctor|restore|build|test|verify|pipe-smoke|format[-check] <파일...>|run');
            console.log('실행 직전 승인 전용: ui-smoke --approved-no-camera [--observe] (기본 verify에서 실행하지 않음)');
            console.log('run은 실제 앱을 엽니다. UI 조작·실카메라 사용·사진 저장은 별도 승인 후 수행하세요.');
            break;
        default: throw new Error(`알 수 없는 명령: ${command}`);
    }
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}
