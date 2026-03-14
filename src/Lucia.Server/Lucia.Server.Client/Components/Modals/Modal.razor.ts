export const showModal = (element: HTMLDialogElement | unknown | undefined) => {
    if (!(element instanceof HTMLDialogElement)) { return }

    element.showModal()
}
export const close = (element: HTMLDialogElement | unknown | undefined) => {
    if (!(element instanceof HTMLDialogElement)) { return }

    element.close()
}
