function setupDocumentDeleteConfirmation(deleteDocument) {
    const modal = $('#deleteDocumentModal');
    const confirmButton = $('#confirmDocumentDelete');
    let selectedId = null;

    modal.on('show.bs.modal', function (event) {
        const trigger = event.relatedTarget;
        selectedId = Number(trigger?.dataset.documentId) || null;
        $('#deleteDocumentReference').text(trigger?.dataset.documentReference || 'this document');
        confirmButton.prop('disabled', !selectedId);
    });
    modal.on('hidden.bs.modal', function () { selectedId = null; });
    confirmButton.on('click', function () {
        if (!selectedId) return;
        const id = selectedId;
        selectedId = null;
        confirmButton.prop('disabled', true);
        modal.modal('hide');
        Promise.resolve(deleteDocument(id)).finally(function () {
            confirmButton.prop('disabled', false);
        });
    });
}
