import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { createHash } from 'node:crypto';
import { verifyBundle, validateVersion, normalizeImageId } from './container-policy.mjs';

const digest = data => createHash('sha256').update(data).digest('hex');

test('Podman 이미지 ID는 전체 SHA-256만 정규화한다', () => {
    const hash = 'a'.repeat(64);
    assert.equal(normalizeImageId(hash), `sha256:${hash}`);
    assert.equal(normalizeImageId(`sha256:${hash}`), `sha256:${hash}`);
    for (const value of ['a'.repeat(63), 'A'.repeat(64), `sha256:${hash}x`, 'sha512:' + hash]) {
        assert.throws(() => normalizeImageId(value));
    }
});
function fixture() {
    const dir = mkdtempSync(join(tmpdir(), 'rudolph-bundle-'));
    const archive = 'real archive bytes';
    writeFileSync(join(dir, 'processing-image.oci.tar'), archive);
    const manifest = {
        schemaVersion: 1, applicationId: 'OhMyRudolph', packageVersion: '1.0.0', hostVersion: '1.0.0',
        contractVersion: 1, abiVersion: 1, platform: 'linux/amd64', imageId: `sha256:${'a'.repeat(64)}`,
        archiveFile: 'processing-image.oci.tar', archiveSha256: digest(archive),
        podmanMachine: 'podman-machine-default', podmanConnection: 'podman-machine-default', network: 'pasta', containerPort: 8080
    };
    return { dir, manifest, save: () => writeFileSync(join(dir, 'processing-image.json'), JSON.stringify(manifest)) };
}

test('정상 번들의 실제 파일 해시와 버전을 확인한다', async () => {
    const f = fixture();
    try { f.save(); assert.equal((await verifyBundle(join(f.dir, 'processing-image.json'), '1.0.0')).manifest.imageId, f.manifest.imageId); }
    finally { rmSync(f.dir, { recursive: true, force: true }); }
});

test('아카이브 경로 탈출과 변조 및 버전 불일치를 거부한다', async () => {
    const f = fixture();
    try {
        f.manifest.archiveFile = '../outside.tar'; f.save();
        await assert.rejects(verifyBundle(join(f.dir, 'processing-image.json'), '1.0.0'));
        f.manifest.archiveFile = 'processing-image.oci.tar'; f.manifest.archiveSha256 = 'b'.repeat(64); f.save();
        await assert.rejects(verifyBundle(join(f.dir, 'processing-image.json'), '1.0.0'));
        f.manifest.archiveSha256 = digest('real archive bytes'); f.save();
        await assert.rejects(verifyBundle(join(f.dir, 'processing-image.json'), '1.0.1'));
    } finally { rmSync(f.dir, { recursive: true, force: true }); }
});

test('유효하지 않은 버전과 잘못된 런타임 프로필을 거부한다', async () => {
    for (const value of ['1.0', '01.0.0', '1.0.0+build', '1.0.0-01', '../1.0.0']) assert.throws(() => validateVersion(value));
    assert.equal(validateVersion('1.2.3-rc.1'), '1.2.3-rc.1');
    const f = fixture();
    try { f.manifest.network = 'host'; f.save(); await assert.rejects(verifyBundle(join(f.dir, 'processing-image.json'), '1.0.0')); }
    finally { rmSync(f.dir, { recursive: true, force: true }); }
});
