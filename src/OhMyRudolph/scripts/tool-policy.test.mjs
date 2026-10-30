import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync, mkdirSync, symlinkSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { toolPolicy } from './tool-policy.mjs';

test('앱 밖 상대·절대 쓰기는 차단하고 내부 새 파일은 허용한다', () => {
    const temporary = mkdtempSync(join(tmpdir(), 'rudolph-guard-'));
    const root = join(temporary, 'app');
    mkdirSync(root);
    try {
        assert.equal(toolPolicy(root, { toolName: 'write', input: { path: '../other/a.cs' } }).block, true);
        assert.equal(toolPolicy(root, { toolName: 'write', input: { path: join(temporary, 'other.cs') } }).block, true);
        assert.equal(toolPolicy(root, { toolName: 'write', input: { path: 'new/a.cs' } }), undefined);
        assert.equal(toolPolicy(root, { toolName: 'read', input: { path: '../other/a.cs' } }), undefined);
    } finally { rmSync(temporary, { recursive: true, force: true }); }
});

test('기존 디렉터리 링크로 앱 범위를 우회할 수 없다', () => {
    const temporary = mkdtempSync(join(tmpdir(), 'rudolph-link-'));
    const root = join(temporary, 'app');
    const outside = join(temporary, 'outside');
    mkdirSync(root); mkdirSync(outside);
    try {
        symlinkSync(outside, join(root, 'linked'), process.platform === 'win32' ? 'junction' : 'dir');
        assert.equal(toolPolicy(root, { toolName: 'write', input: { path: 'linked/new.cs' } }).block, true);
    } finally { rmSync(temporary, { recursive: true, force: true }); }
});

test('광범위 Python 종료만 차단하고 특정 PID 관리는 허용한다', () => {
    assert.equal(toolPolicy(process.cwd(), { toolName: 'bash', input: { command: 'taskkill /F /IM python.exe' } }).block, true);
    assert.equal(toolPolicy(process.cwd(), { toolName: 'bash', input: { command: 'Get-Process python | Stop-Process' } }).block, true);
    assert.equal(toolPolicy(process.cwd(), { toolName: 'bash', input: { command: 'taskkill /PID 12345' } }), undefined);
});

test('자격 증명을 차단하는 진단은 원문을 포함하지 않는다', () => {
    const secret = 'Bearer ' + 'a'.repeat(30);
    const result = toolPolicy(process.cwd(), { toolName: 'write', input: { path: 'bad.txt', content: secret } });
    assert.equal(result.block, true);
    assert.equal(result.reason.includes(secret), false);
});
