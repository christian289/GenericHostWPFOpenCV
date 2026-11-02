import { spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { readFile } from 'node:fs/promises';
import { dirname, resolve, relative, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';
import { verifyBundle } from './container-policy.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const solution = 'OhMyRudolph.slnx';
const portablePodman = resolve(root, 'artifacts/podman-6.0.2/usr/bin/podman.exe');
const bundledPodman = resolve(root, 'runtime/podman/podman.exe');
const podman = process.env.OHMYRUDOLPH_PODMAN ?? (existsSync(bundledPodman) ? bundledPodman : existsSync(portablePodman) ? portablePodman : 'podman');
const connection = process.env.OHMYRUDOLPH_PODMAN_CONNECTION ?? 'podman-machine-default';
const manifest = process.env.OHMYRUDOLPH_IMAGE_MANIFEST ?? resolve(root, 'artifacts/container-bundle/processing-image.json');
const [command = 'help', ...files] = process.argv.slice(2);
function run(executable, args) {
    const env = { ...process.env, OHMYRUDOLPH_IMAGE_MANIFEST: manifest, OHMYRUDOLPH_PODMAN_CONNECTION: connection };
    if (isAbsolute(podman)) env.OHMYRUDOLPH_PODMAN = podman;
    const result = spawnSync(executable, args, { cwd: root, stdio: 'inherit', shell: false, env });
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
    if (!files.length) throw new Error('전체 재포맷 방지를 위해 파일을 명시하세요.');
    run('dotnet', ['tool', 'restore']);
    const cs = [];
    for (const file of files.map(scopedFile)) {
        if (file.endsWith('.xaml')) run('dotnet', ['tool', 'run', 'xstyler', '--', '-f', file, '-c', 'Settings.XamlStyler', ...(check ? ['--passive'] : [])]);
        else if (file.endsWith('.cs')) cs.push(file);
        else throw new Error('포맷 대상은 .cs 또는 .xaml이어야 합니다.');
    }
    if (cs.length) run('dotnet', ['format', solution, '--no-restore', ...(check ? ['--verify-no-changes'] : []), '--include', ...cs]);
}
try {
    switch (command) {
        case 'doctor': {
            run('dotnet', ['--version']);
            console.log(`Node ${process.version}`);
            run(podman, ['--version']);
            run(podman, ['--connection', connection, 'info', '--format', '{{.Host.Arch}}']);
            const header = JSON.parse(await readFile(manifest, 'utf8'));
            await verifyBundle(manifest, header.packageVersion);
            console.log('처리 이미지 manifest·아카이브 해시 검증 통과. 앱 실행 버전과의 대조는 실제 시작 시 수행합니다.');
            break;
        }
        case 'restore': run('dotnet', ['tool', 'restore']); run('dotnet', ['restore', solution]); break;
        case 'build': build(); break;
        case 'test': build(); test(); break;
        case 'verify': run('dotnet', ['tool', 'restore']); build(); test(); break;
        case 'format': format(false); break;
        case 'format-check': format(true); break;
        case 'container-smoke':
            if (!existsSync(resolve(root, 'artifacts/astronaut.jpg'))) throw new Error('먼저 문서의 공개 시험 자료 준비 절차를 수행하세요. 사용자 사진으로 대체하지 않습니다.');
            run('dotnet', ['run', '--project', 'tools/OhMyRudolph.ContainerSmoke', '-c', 'Release', '--', '--manifest', manifest,
                '--fixture', resolve(root, 'artifacts/astronaut.jpg'), ...(isAbsolute(podman) ? ['--podman', podman] : []), '--connection', connection]);
            break;
        case 'image-build': run(process.execPath, ['scripts/container-build.mjs', ...files]); break;
        case 'package': run(process.execPath, ['scripts/package-build.mjs', ...files]); break;
        case 'ui-smoke':
            if (files.length < 1 || files.length > 2 || files[0] !== '--approved-no-camera' || (files.length === 2 && files[1] !== '--observe'))
                throw new Error('실행 직전 사용자 승인 후 ui-smoke --approved-no-camera로 실행하세요. 카메라/사진 저장은 실행하지 않습니다.');
            run('dotnet', ['run', '--project', 'tools/OhMyRudolph.UiSmoke', '-c', 'Release', '--', '--approved-no-camera',
                resolve(root, 'OhMyRudolph.Wpf/bin/Release/net10.0-windows/OhMyRudolph.Wpf.exe'), ...(files[1] === '--observe' ? ['--observe'] : [])]);
            break;
        case 'run': run('dotnet', ['run', '--project', 'OhMyRudolph.Wpf', '-c', 'Release']); break;
        case 'help':
            console.log('개발 하네스: doctor|restore|build|test|verify|container-smoke|image-build|package|format[-check] <파일...>|run');
            console.log('실행 직전 승인 전용: ui-smoke --approved-no-camera [--observe]. 기본 verify는 앱/카메라를 실행하지 않습니다.');
            break;
        default: throw new Error(`알 수 없는 명령: ${command}`);
    }
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}
