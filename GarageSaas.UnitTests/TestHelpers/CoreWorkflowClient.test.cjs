const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../../GarageSaas/Views/WorkQuote/AddEdit.cshtml'), 'utf8');
const code = source.match(/<script>([\s\S]*?)<\/script>/)[1];

function screen(id) {
    const fields = new Map(), handlers = new Map(), requests = [];
    function $(selector) {
        if (typeof selector === 'function') return;
        if (!fields.has(selector)) fields.set(selector, { value: '', html: '', text: '' });
        const state = fields.get(selector);
        const element = {
            val(value) { if (arguments.length) { state.value = value; return element; } return state.value; },
            html(value) { if (arguments.length) { state.html = value; return element; } return state.html; },
            text(value) { if (arguments.length) { state.text = value; state.html = String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;'); return element; } return state.text; },
            on(event, callback) { const key = selector + ':' + event; handlers.set(key, [...(handlers.get(key) || []), callback]); return element; },
            prop() { return element; }, hide() { return element; }, show() { return element; }, attr() { return element; },
            empty() { state.html = ''; return element; }, append() { return element; }, modal() { return element; },
            map() { return { get() { return []; } }; }
        };
        return element;
    }
    $.getJSON = (url, data) => {
        const request = { url, data }; requests.push(request);
        const chain = { done(callback) { request.complete = callback; return chain; }, fail() { return chain; } }; return chain;
    };
    $('#workQuoteId').val(String(id));
    const context = vm.createContext({ $, document: {}, console, window: {} });
    vm.runInContext(code, context);
    return { $, context, handlers, requests, run: code => vm.runInContext(code, context) };
}

const create = screen(0);
create.$('#ddlCustomer').val('10'); create.$('#ddlVehicle').val('100');
create.run('loadWorkItems([])');
assert.equal(create.requests[0].url, '/WorkItem/GetByVehicle');
assert.equal(create.requests[0].data.vehicleId, 100);
assert.equal(create.handlers.get('#ddlVehicle:change').length, 1);
assert.equal(create.handlers.get('#btnAddWorkItem:click').length, 1);
create.$('#editWorkItemId').val('999');
create.handlers.get('#btnAddWorkItem:click')[0]();
assert.equal(create.$('#editWorkItemId').val(), '0');
create.run('selectedWorkItemIds = [1, 2]; clearWorkItems()');
assert.equal(create.run('selectedWorkItemIds.length'), 0);
create.$('#txtLabour').val('100'); create.run('recalculateTotals()');
assert.equal(create.$('#txtTotal').val(), '123.50');

const edit = screen(42);
edit.run('loadVehiclesForCustomer(10, 100, [7, 8])');
assert.equal(edit.$('#ddlVehicle').val(), 100);
assert.equal(edit.requests.length, 1);
assert.equal(edit.requests[0].url, '/WorkItem/GetAvailableForQuote');
edit.requests[0].complete({ status: 'Success', data: [{ id: 7, repairInstructions: 'Brake pads' }] });
assert.equal(edit.run('selectedWorkItemIds.join(",")'), '7,8');
assert.match(edit.$('#workItemsContainer').html(), /Brake pads/);
assert.equal((source.match(/id="workItemsContainer"/g) || []).length, 1);
console.log('Quote client checks passed: vehicle scoping, single handlers, add/edit reset, historical selections, consistent VAT preview.');
