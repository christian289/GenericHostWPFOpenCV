import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { toolPolicy } from '../../scripts/tool-policy.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
export default function rudolphGuards(pi) {
    pi.on('tool_call', (event, context) => toolPolicy(root, event, context?.cwd ?? root));
}
