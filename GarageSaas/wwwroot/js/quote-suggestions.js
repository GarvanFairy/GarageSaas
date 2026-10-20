(function () {
    const area = document.getElementById('quoteSuggestionArea');
    if (!area) return;
    const draft = document.getElementById('txtNewRepairInstructions');
    const suggest = document.getElementById('btnSuggestDescription');
    const spinner = document.getElementById('quoteSuggestionSpinner');
    const status = document.getElementById('quoteSuggestionStatus');
    const preview = document.getElementById('quoteSuggestionPreview');
    const text = document.getElementById('quoteSuggestionText');
    let version = 0, busy = false, pending = null, suggestion = '', original = '', workId = '';

    function reset() {
        version++;
        pending?.abort();
        pending = null;
        busy = false;
        suggest.disabled = false;
        spinner.hidden = true;
        preview.hidden = true;
        text.textContent = '';
        status.textContent = '';
        suggestion = '';
    }
    draft.addEventListener('input', reset);
    document.getElementById('btnDismissSuggestion').addEventListener('click', reset);
    if (window.jQuery) window.jQuery('#workItemModal').on('show.bs.modal hidden.bs.modal', reset);

    suggest.addEventListener('click', async function () {
        if (busy) return;
        reset();
        if (!draft.value.trim()) { status.textContent = 'Enter a short work description first.'; return; }
        if (draft.value.trim().length > Number(area.dataset.maxInput)) {
            status.textContent = 'Shorten the draft before requesting a suggestion.';
            return;
        }
        original = draft.value;
        workId = document.getElementById('editWorkItemId').value;
        const quoteId = document.getElementById('workQuoteId').value;
        const requestVersion = version;
        pending = new AbortController();
        busy = true;
        suggest.disabled = true;
        spinner.hidden = false;
        status.textContent = 'Generating suggestion...';
        try {
            const response = await window.fetch('/QuoteSuggestion/Suggest', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': area.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({ description: original.trim(), workQuoteId: Number(quoteId) || 0 }),
                signal: pending.signal
            });
            const result = await response.json();
            if (requestVersion !== version) return;
            if (draft.value !== original || document.getElementById('editWorkItemId').value !== workId ||
                document.getElementById('workQuoteId').value !== quoteId) {
                reset();
                status.textContent = 'The draft changed. Request a new suggestion for the current text.';
                return;
            }
            if (!response.ok || result.status !== 'Success') {
                status.textContent = result.message || 'AI suggestions are unavailable. You can still edit manually.';
                return;
            }
            if (typeof result.data?.suggestion !== 'string' || !result.data.suggestion.trim()) {
                status.textContent = 'No usable suggestion returned. You can still edit manually.';
                return;
            }
            suggestion = result.data.suggestion;
            text.textContent = suggestion;
            preview.hidden = false;
            status.textContent = 'Review the suggestion. Your original description has not changed.';
        } catch (error) {
            if (requestVersion === version && error.name !== 'AbortError')
                status.textContent = 'AI suggestions are unavailable. Your description is unchanged; you can edit manually.';
        } finally {
            if (requestVersion === version) {
                busy = false;
                pending = null;
                suggest.disabled = false;
                spinner.hidden = true;
            }
        }
    });
    document.getElementById('btnUseSuggestion').addEventListener('click', function () {
        if (!suggestion || draft.value !== original || document.getElementById('editWorkItemId').value !== workId) {
            reset(); return;
        }
        draft.value = suggestion;
        draft.dispatchEvent(new Event('input', { bubbles: true }));
        draft.focus();
    });
})();
