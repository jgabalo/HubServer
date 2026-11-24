// Guard rail: the display must never be able to publish or trade.
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { TRADE_HUB } from './hub.js';

const srcDir = new URL('.', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const stripComments = text => text.replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\s*\/\/.*$/gm, '');
const appSources = readdirSync(srcDir)
    .filter(f => f.endsWith('.js') && !f.endsWith('.test.js'))
    .map(f => ({ file: f, text: stripComments(readFileSync(join(srcDir, f), 'utf8')) }));

describe('read-only display', () => {
    it('only knows read-side hub members', () => {
        expect(Object.values(TRADE_HUB).sort()).toEqual(['GetFeedStatus', 'GetRecentTrades', 'feedStatusChanged', 'tradesReceived']);
    });

    it.each(appSources.map(s => [s.file, s.text]))('%s never invokes PublishTrades or sends to the hub', (_file, code) => {
        expect(code).not.toMatch(/PublishTrades/);
        expect(code).not.toMatch(/\.send\(/);
    });

    it('never renders feed data as HTML', () => {
        for (const { text } of appSources) {
            expect(text).not.toMatch(/innerHTML|outerHTML|insertAdjacentHTML/);
        }
    });
});
