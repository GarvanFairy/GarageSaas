// Run with node. No browser, network, database, or additional packages needed.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const origin = 'https://garage.test';
class BrowserRequest extends Request {
    constructor(input, init) { super(typeof input === 'string' ? new URL(input, origin).href : input, init); }
}
let prefilter, onShow, reloads = 0;
const calls = [];
const jquery = { ajaxPrefilter: fn => { prefilter = fn; } };
const window = {
    location: { href: origin + '/WorkQuote', origin, reload: () => { reloads++; } },
    jQuery: jquery,
    fetch: async request => { calls.push(request); return new Response('{}'); },
    addEventListener: (_, fn) => { onShow = fn; }
};
vm.runInNewContext(fs.readFileSync(path.resolve(__dirname, '../../GarageSaas/wwwroot/js/site.js'), 'utf8'), {
    window, document: { querySelector: () => ({ content: 'protected-context' }) }, $: jquery,
    URL, Headers, Request: BrowserRequest
});
(async () => {
    const headers = {};
    prefilter({ url: '/WorkItem/AddWorkItem' }, {}, { setRequestHeader: (k, v) => { headers[k] = v; } });
    assert.equal(headers['X-Garage-Context'], 'protected-context');
    prefilter({ url: 'https://outside.test/api' }, {}, { setRequestHeader: () => assert.fail('Context leaked outside origin') });
    const payload = '{"description":"unchanged"}';
    await window.fetch('/WorkQuote/AddWorkQuote3', { method: 'POST', body: payload,
        headers: { 'Content-Type': 'application/json', RequestVerificationToken: 'existing-antiforgery' } });
    assert.equal(calls[0].headers.get('X-Garage-Context'), 'protected-context');
    assert.equal(calls[0].headers.get('RequestVerificationToken'), 'existing-antiforgery');
    assert.equal(calls[0].headers.get('Content-Type'), 'application/json');
    assert.equal(await calls[0].text(), payload);
    await window.fetch('https://outside.test/api');
    assert.equal(calls[1].headers.get('X-Garage-Context'), null);
    onShow({ persisted: false }); assert.equal(reloads, 0);
    onShow({ persisted: true }); assert.equal(reloads, 1);
    console.log('Garage context client checks passed: jQuery, fetch, origin isolation, unchanged payload/antiforgery, BFCache reload.');
})().catch(error => { console.error(error); process.exitCode = 1; });
