import { spawnSync } from 'node:child_process';
import { mkdir, rename, rm, writeFile } from 'node:fs/promises';
import { dirname, isAbsolute, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { normalizeImageId, sha256File, validateVersion } from './container-policy.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const output = join(root, 'artifacts', 'container-bundle');
const usage = `사용법: node scripts/container-build.mjs --version 1.0.0 --podman <절대경로> [--connection 이름] [--use-existing-native]
기본 연결: OHMYRUDOLPH_PODMAN_CONNECTION 또는 podman-machine-default.
--podman 대신 절대경로 OHMYRUDOLPH_PODMAN 환경 변수를 사용할 수 있습니다.
기본적으로 native와 최종 이미지를 빌드합니다. 이미 검증된 native 이미지가 있으면 --use-existing-native를 사용하세요.
출력: artifacts/container-bundle/processing-image.oci.tar 및 processing-image.json`;
function options(args) {
    const result = {};
    for (let i = 0; i < args.length; i++) {
        const key = args[i];
        if (key === '--use-existing-native') { if (result.existing) throw new Error('중복 옵션'); result.existing = true; continue; }
        if (!['--version', '--podman', '--connection'].includes(key) || !args[i + 1] || args[i + 1].startsWith('--') || key in result) throw new Error(`잘못된 옵션: ${key}`);
        result[key] = args[++i];
    }
    return result;
}
function run(exe, args, capture = false) {
    const r = spawnSync(exe, args, { cwd: root, shell: false, encoding: 'utf8', stdio: capture ? ['ignore', 'pipe', 'inherit'] : 'inherit' });
    if (r.error) throw r.error;
    if (r.status !== 0) throw new Error(`Podman 명령 실패 (종료 코드 ${r.status ?? 'signal'}).`);
    return r.stdout?.trim();
}
async function main() {
    if (!process.argv.slice(2).length || process.argv.slice(2).includes('--help')) { console.log(usage); return; }
    const o = options(process.argv.slice(2));
    const version = validateVersion(o['--version']);
    const podman = o['--podman'] ?? process.env.OHMYRUDOLPH_PODMAN;
    const connection = o['--connection'] ?? process.env.OHMYRUDOLPH_PODMAN_CONNECTION ?? 'podman-machine-default';
    if (!podman || !isAbsolute(podman)) throw new Error('Podman 실행 파일의 절대경로를 지정하세요.');
    if (!connection || connection.startsWith('-')) throw new Error('유효한 Podman 연결 이름을 지정하세요.');
    const prefix = ['--connection', connection];
    if (!o.existing) run(podman, [...prefix, 'build', '--network=host', '--platform=linux/amd64', '--target', 'native', '-f', 'native/OhMyRudolph.Native/Containerfile.native', '-t', 'localhost/ohmyrudolph-native-build:local', '.']);
    run(podman, [...prefix, 'build', '--network=host', '--platform=linux/amd64', '--build-arg', `APP_VERSION=${version}`, '-f', 'Containerfile', '-t', 'localhost/ohmyrudolph-processing:local', '.']);
    const imageId = normalizeImageId(run(podman, [...prefix, 'image', 'inspect', '--format', '{{.Id}}', 'localhost/ohmyrudolph-processing:local'], true));
    await mkdir(output, { recursive: true });
    const archive = join(output, `processing-image.oci.tar.tmp-${process.pid}`);
    const tempManifest = join(output, `processing-image.json.tmp-${process.pid}`);
    try {
        run(podman, [...prefix, 'save', '--format', 'oci-archive', '--output', archive, imageId]);
        const manifest = {
            schemaVersion: 1, applicationId: 'OhMyRudolph', packageVersion: version, hostVersion: version,
            contractVersion: 1, abiVersion: 1, platform: 'linux/amd64', imageId,
            archiveFile: 'processing-image.oci.tar', archiveSha256: await sha256File(archive),
            podmanMachine: 'podman-machine-default', podmanConnection: 'podman-machine-default', network: 'pasta', containerPort: 8080
        };
        await writeFile(tempManifest, `${JSON.stringify(manifest, null, 2)}\n`, { flag: 'wx' });
        await rename(archive, join(output, manifest.archiveFile));
        await rename(tempManifest, join(output, 'processing-image.json'));
        console.log(`이미지: ${imageId}\n아카이브 SHA-256: ${manifest.archiveSha256}\n출력: ${output}`);
    } finally { await rm(archive, { force: true }); await rm(tempManifest, { force: true }); }
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
