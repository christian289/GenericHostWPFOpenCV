import { spawnSync } from 'node:child_process';
import { copyFile, mkdir, mkdtemp, readFile, rm } from 'node:fs/promises';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { sha256File, validateVersion, verifyBundle } from './container-policy.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const artifacts = join(root, 'artifacts');
const usage = `사용법: node scripts/package-build.mjs --version 1.0.0 [--dotnet <절대경로>] [--manifest <절대경로>]
  [--podman-dir <절대경로> --podman-license <절대경로> --podman-hashes <절대경로>]
기본 manifest: artifacts/container-bundle/processing-image.json. dotnet은 PATH 또는 --dotnet 절대경로.
Podman CLI 동봉은 선택 사항이며, 세 옵션을 모두 지정해야 합니다. hash JSON에는 podman.exe, gvproxy.exe,
win-sshproxy.exe, LICENSE 각각의 독립적으로 검증한 소문자 SHA-256이 필요합니다.
출력: artifacts/velopack-<임의값>/ (업로드 및 설치 실행 없음).`;
function options(args) {
    const result = {};
    for (let i = 0; i < args.length; i++) {
        const key = args[i];
        if (!['--version', '--dotnet', '--manifest', '--podman-dir', '--podman-license', '--podman-hashes'].includes(key) || !args[i + 1] || args[i + 1].startsWith('--') || key in result) throw new Error(`잘못된 옵션: ${key}`);
        result[key] = args[++i];
    }
    return result;
}
function run(exe, args) {
    // Do not let ambient vpk options exclude the archive, supply a feed or enable signing.
    const env = Object.fromEntries(Object.entries(process.env).filter(([key]) => !key.toUpperCase().startsWith('VPK_')));
    const r = spawnSync(exe, args, { cwd: root, shell: false, stdio: 'inherit', env });
    if (r.error) throw r.error;
    if (r.status !== 0) throw new Error(`패키징 명령 실패 (종료 코드 ${r.status ?? 'signal'}).`);
}
async function includePodman(o, target) {
    const names = ['podman.exe', 'gvproxy.exe', 'win-sshproxy.exe', 'LICENSE'];
    const hashes = JSON.parse(await readFile(o['--podman-hashes'], 'utf8'));
    if (!hashes || typeof hashes !== 'object' || Array.isArray(hashes) || Object.keys(hashes).length !== names.length || names.some(name => !/^[0-9a-f]{64}$/.test(hashes[name]))) throw new Error('Podman 해시 목록이 잘못되었습니다.');
    await mkdir(target, { recursive: true });
    for (const name of names) {
        const source = name === 'LICENSE' ? o['--podman-license'] : join(o['--podman-dir'], name);
        if (await sha256File(source) !== hashes[name]) throw new Error(`Podman 파일 해시 불일치: ${name}`);
        const destination = join(target, name);
        await copyFile(source, destination);
        if (await sha256File(destination) !== hashes[name]) throw new Error(`Podman 복사본 해시 불일치: ${name}`);
    }
}
async function main() {
    if (!process.argv.slice(2).length || process.argv.slice(2).includes('--help')) { console.log(usage); return; }
    const o = options(process.argv.slice(2));
    const version = validateVersion(o['--version']);
    for (const key of ['--dotnet', '--manifest', '--podman-dir', '--podman-license', '--podman-hashes']) {
        if (o[key] && !isAbsolute(o[key])) throw new Error(`${key}는 절대경로여야 합니다.`);
    }
    const podmanCount = ['--podman-dir', '--podman-license', '--podman-hashes'].filter(k => o[k]).length;
    if (podmanCount !== 0 && podmanCount !== 3) throw new Error('Podman 동봉 옵션 세 개를 모두 지정하세요.');
    const dotnet = o['--dotnet'] ?? 'dotnet';
    const manifest = o['--manifest'] ?? join(artifacts, 'container-bundle', 'processing-image.json');
    const bundle = await verifyBundle(manifest, version);
    await mkdir(artifacts, { recursive: true });
    const staging = await mkdtemp(join(artifacts, 'package-staging-'));
    try {
        run(dotnet, ['publish', 'OhMyRudolph.Wpf/OhMyRudolph.Wpf.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', staging, `-p:Version=${version}`]);
        const runtime = join(staging, 'runtime');
        await mkdir(runtime, { recursive: true });
        await copyFile(bundle.archivePath, join(runtime, 'processing-image.oci.tar'));
        await copyFile(manifest, join(runtime, 'processing-image.json'));
        await verifyBundle(join(runtime, 'processing-image.json'), version);
        if (podmanCount) await includePodman(o, join(runtime, 'podman'));
        const releases = await mkdtemp(join(artifacts, `velopack-${version}-`));
        run(dotnet, ['tool', 'run', 'vpk', '--', 'pack', '--packId', 'OhMyRudolph', '--packVersion', version,
            '--packDir', staging, '--mainExe', 'OhMyRudolph.Wpf.exe', '--runtime', 'win-x64', '--outputDir', releases]);
        console.log(`Velopack 출력: ${releases}`);
    } finally { await rm(staging, { recursive: true, force: true }); }
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
