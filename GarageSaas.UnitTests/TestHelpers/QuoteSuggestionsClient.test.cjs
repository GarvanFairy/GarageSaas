const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../../GarageSaas/wwwroot/js/quote-suggestions.js'), 'utf8');
function editor() {
    const fields = new Map();
    class Element {
        constructor() { this.value = ''; this.textContent = ''; this.hidden = true; this.disabled = false; this.events = {}; this.dataset = {}; }
        addEventListener(name, callback) { this.events[name] = callback; }
        dispatchEvent(event) { return this.events[event.type]?.(event); }
        focus() { this.focused = true; }
        querySelector() { return { value: 'synthetic-antiforgery' }; }
        click() { return this.events.click?.(); }
    }
    function element(id) { if (!fields.has(id)) fields.set(id, new Element()); return fields.get(id); }
    element('quoteSuggestionArea').dataset.maxInput = '1000'; element('workQuoteId').value = '42'; element('editWorkItemId').value = '7';
    const requests = [], modalEvents = {};
    const window = {
        fetch(url, options) {
            let resolve, reject;
            const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
            requests.push({ url, options, resolve, reject }); return promise;
        },
        jQuery() { return { on(names, callback) { for (const name of names.split(' ')) modalEvents[name] = callback; } }; }
    };
    const context = vm.createContext({ document: { getElementById: element }, window, AbortController,
        Event: class { constructor(type) { this.type = type; } } });
    vm.runInContext(source, context);
    return { element, requests, modalEvents, respond(text = 'Replace the front brake pads.') {
        requests.at(-1).resolve({ ok: true, json: async () => ({ status: 'Success', data: { suggestion: text } }) });
    } };
}
(async () => {
    const ui = editor(), e = ui.element;
    e('txtNewRepairInstructions').value = 'Replace pads';
    const pending = e('btnSuggestDescription').click();
    assert.equal(e('btnSuggestDescription').disabled, true); assert.equal(e('quoteSuggestionSpinner').hidden, false);
    assert.equal(e('quoteSuggestionStatus').textContent, 'Generating suggestion...');
    await e('btnSuggestDescription').click(); assert.equal(ui.requests.length, 1);
    assert.deepEqual(JSON.parse(ui.requests[0].options.body), { description: 'Replace pads', workQuoteId: 42 });
    assert.equal(ui.requests[0].options.headers.RequestVerificationToken, 'synthetic-antiforgery');
    ui.respond(); await pending;
    assert.equal(e('txtNewRepairInstructions').value, 'Replace pads'); assert.equal(e('quoteSuggestionPreview').hidden, false);
    assert.equal(e('quoteSuggestionText').textContent, 'Replace the front brake pads.');
    assert.equal(e('btnSuggestDescription').disabled, false); assert.equal(e('quoteSuggestionSpinner').hidden, true);
    e('btnDismissSuggestion').click(); assert.equal(e('txtNewRepairInstructions').value, 'Replace pads'); assert.equal(e('quoteSuggestionPreview').hidden, true);
    const accept = e('btnSuggestDescription').click(); ui.respond(); await accept; e('btnUseSuggestion').click();
    assert.equal(e('txtNewRepairInstructions').value, 'Replace the front brake pads.'); assert.equal(e('quoteSuggestionPreview').hidden, true);
    e('txtNewRepairInstructions').value += ' Check discs.'; e('txtNewRepairInstructions').dispatchEvent({ type: 'input' });
    assert.match(e('txtNewRepairInstructions').value, /Check discs/);
    const failed = e('btnSuggestDescription').click(); ui.requests.at(-1).reject(new Error('network failure')); await failed;
    assert.match(e('quoteSuggestionStatus').textContent, /edit manually/); assert.equal(e('btnSuggestDescription').disabled, false);
    const stale = e('btnSuggestDescription').click(); e('txtNewRepairInstructions').value = 'New draft'; e('txtNewRepairInstructions').dispatchEvent({ type: 'input' }); ui.respond('Old suggestion'); await stale;
    assert.equal(e('txtNewRepairInstructions').value, 'New draft'); assert.equal(e('quoteSuggestionPreview').hidden, true);
    const closed = e('btnSuggestDescription').click(); ui.modalEvents['hidden.bs.modal'](); ui.respond(); await closed;
    assert.equal(e('quoteSuggestionPreview').hidden, true);
    const html = e('btnSuggestDescription').click(); ui.respond('<img src=x onerror=alert(1)>'); await html;
    assert.equal(e('quoteSuggestionText').textContent, '<img src=x onerror=alert(1)>');
    const newQuote = editor(); newQuote.element('workQuoteId').value = '0'; newQuote.element('txtNewRepairInstructions').value = 'Replace pads';
    const newPending = newQuote.element('btnSuggestDescription').click(); assert.equal(JSON.parse(newQuote.requests[0].options.body).workQuoteId, 0); newQuote.respond(); await newPending;
    console.log('AI UI checks passed: new/existing quote, loading, no automatic overwrite, accept/edit/dismiss, duplicate prevention, stale/closed modal, safe text and manual editing after failure.');
})().catch(error => { console.error(error); process.exitCode = 1; });
