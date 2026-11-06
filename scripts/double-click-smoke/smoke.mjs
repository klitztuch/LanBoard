// Double-clicks "Set active" on /admin/party in a single JS tick, so both clicks reach the server before
// the disabled state renders. Fails if the circuit dies or the clicked party doesn't become active.
// Usage: node smoke.mjs [base-url] [runs]   (CHROMIUM_PATH selects a browser binary)
import { chromium } from 'playwright';

const baseUrl = process.argv[2] ?? 'https://localhost:7155';
const runs = Number(process.argv[3] ?? 30);

// Selectors avoid localized text: column 6 holds the "Active" badge or the "Set active" button.
const setActiveButtons = 'tbody tr td:nth-child(6) button';

const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined });
let failures = 0;
try {
    const page = await (await browser.newContext({ ignoreHTTPSErrors: true })).newPage();

    let socketFrames = 0;
    page.on('websocket', ws => ws.on('framereceived', () => socketFrames++));

    await gotoRetrying(page, `${baseUrl}/auth/dev-login?name=Alice`);
    await gotoRetrying(page, `${baseUrl}/admin/party`);
    // Interactive once the circuit's websocket delivers its first render batch; then let the load finish.
    for (let waited = 0; socketFrames === 0; waited += 100) {
        if (waited > 15_000) throw new Error('No Blazor circuit after 15 s');
        await page.waitForTimeout(100);
    }
    await page.waitForTimeout(1_000);

    if ((await page.locator('tbody tr').count()) < 2) {
        console.error('Need at least 2 parties on /admin/party; create one first.');
        process.exit(2);
    }

    for (let run = 1; run <= runs; run++) {
        const partyLink = await page.evaluate(selector => {
            const button = document.querySelector(selector);
            const link = button.closest('tr').querySelector('a[href^="/admin/party/"]').getAttribute('href');
            button.click();
            button.click();
            return link;
        }, setActiveButtons);

        const row = page.locator('tbody tr').filter({ has: page.locator(`a[href="${partyLink}"]`) });
        let error = null;
        try {
            await row.locator('td:nth-child(6) span').waitFor({ timeout: 10_000 });
            await page.waitForFunction(s => !document.querySelector(`${s}:disabled`), setActiveButtons, { timeout: 10_000 });
        } catch {
            error = 'clicked party did not become active';
        }
        if (await page.evaluate(() => document.getElementById('components-reconnect-modal')?.open))
            error = 'circuit lost (reconnect dialog open)';

        console.log(`  run ${String(run).padStart(2)}: ${error ?? 'ok'}`);
        if (error) {
            failures++;
            break;   // a dead circuit fails every later run too
        }
    }
} finally {
    await browser.close();
}
process.exit(failures ? 1 : 0);

// Chromium aborts navigations while Docker is still adding network interfaces after an AppHost start.
async function gotoRetrying(page, url) {
    for (let attempt = 1; ; attempt++) {
        try {
            return await page.goto(url);
        } catch (e) {
            if (attempt === 5 || !String(e.message).includes('ERR_NETWORK_CHANGED')) throw e;
            await page.waitForTimeout(1_000);
        }
    }
}
