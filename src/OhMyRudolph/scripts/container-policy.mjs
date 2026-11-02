import { createHash } from 'node:crypto';
import { createReadStream } from 'node:fs';
import { lstat, readFile } from 'node:fs/promises';
import { dirname, join } from 'node:path';

export function validateVersion(version) {
    // SemVer without build metadata: the same value is used by Velopack and host readiness.
    if (typeof version !== 'string' || !/^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$/.test(version) ||
        (version.split('-').slice(1).join('-').split('.').some(x => /^0\d+$/.test(x)))) {
        throw new Error('버전은 build metadata 없는 유효한 SemVer여야 합니다.');
    }
    return version;
}

export function normalizeImageId(value) {
    if (typeof value !== 'string' || !/^(?:sha256:)?[0-9a-f]{64}$/.test(value)) {
        throw new Error('Podman 이미지 ID 형식이 잘못되었습니다.');
    }
    return value.startsWith('sha256:') ? value : `sha256:${value}`;
}

export async function sha256File(path) {
    const stat = await lstat(path);
    if (!stat.isFile() || stat.isSymbolicLink()) throw new Error('일반 파일만 허용됩니다.');
    const hash = createHash('sha256');
    for await (const chunk of createReadStream(path)) hash.update(chunk);
    return hash.digest('hex');
}

export async function verifyBundle(manifestPath, version) {
    validateVersion(version);
    const stat = await lstat(manifestPath);
    if (!stat.isFile() || stat.isSymbolicLink() || stat.size > 16384) throw new Error('잘못된 manifest 파일입니다.');
    const m = JSON.parse(await readFile(manifestPath, 'utf8'));
    if (!m || typeof m !== 'object' || Array.isArray(m) ||
        m.schemaVersion !== 1 || m.applicationId !== 'OhMyRudolph' ||
        m.packageVersion !== version || m.hostVersion !== version || m.contractVersion !== 1 || m.abiVersion !== 1 ||
        m.platform !== 'linux/amd64' || !/^sha256:[0-9a-f]{64}$/.test(m.imageId) ||
        m.archiveFile !== 'processing-image.oci.tar' || !/^[0-9a-f]{64}$/.test(m.archiveSha256) ||
        m.podmanMachine !== 'podman-machine-default' || m.podmanConnection !== 'podman-machine-default' ||
        m.network !== 'pasta' || m.containerPort !== 8080) throw new Error('manifest 계약 또는 버전이 일치하지 않습니다.');
    const archivePath = join(dirname(manifestPath), m.archiveFile);
    if (await sha256File(archivePath) !== m.archiveSha256) throw new Error('OCI 아카이브 SHA-256이 일치하지 않습니다.');
    return { manifest: m, archivePath };
}
