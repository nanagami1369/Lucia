export const showModal = (element) => {
    if (!(element instanceof HTMLDialogElement)) {
        return;
    }
    element.showModal();
};
export const close = (element) => {
    if (!(element instanceof HTMLDialogElement)) {
        return;
    }
    element.close();
};
//# sourceMappingURL=Modal.razor.js.map