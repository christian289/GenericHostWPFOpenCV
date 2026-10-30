import { existsSync, realpathSync } from 'node:fs';
import { dirname, isAbsolute, relative, resolve } from 'node:path';

function canonical(path) {
    if (existsSync(path)) return realpathSync(path);
    const parent = dirname(path);
    return parent === path ? path : resolve(canonical(parent), relative(parent, path));
}
export function toolPolicy(root, event, cwd = root) {
    const name = event.toolName?.toLowerCase();
    const input = event.input ?? {};
    if (name === 'write' || name === 'edit') {
        const path = input.path ?? input.file_path;
        // Internal tool devices and hashline patch payloads are not filesystem paths.
        // This guard is intentionally not a sandbox or a shell parser.
        if (typeof path === 'string' && !/^[a-z]+:\/\//i.test(path)) {
            const rel = relative(canonical(root), canonical(resolve(cwd, path)));
            if (rel === '..' || rel.startsWith('..\\') || rel.startsWith('../') || isAbsolute(rel))
                return { block: true, reason: '이 세션의 쓰기 범위는 OhMyRudolph 앱 폴더 안입니다.' };
        }
        const content = input.content;
        if (typeof content === 'string' && /-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|Bearer\s+[A-Za-z0-9._~+/-]{24,}/i.test(content))
            return { block: true, reason: '자격 증명으로 보이는 내용을 소스에 기록할 수 없습니다. 원문은 출력하지 않습니다.' };
    }
    if (name === 'bash') {
        const command = String(input.command ?? '');
        if (/taskkill\b[^\r\n]*(?:\/im\s+["']?python|\/im\s+\*)|(?:Get-Process\s+python\w*|Get-CimInstance[^\r\n]*python)[^\r\n]*(?:Stop-Process|Terminate)|pkill\b[^\r\n]*python/i.test(command))
            return { block: true, reason: '전체 Python 프로세스 종료 금지. 앱이 소유한 정확한 인스턴스만 관리하세요.' };
    }
    return undefined;
}
