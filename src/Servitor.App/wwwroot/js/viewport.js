// Viewport helpers. Wrapped by Shared/Viewport/JsViewportInterop.cs; nothing else talks to it.
export function scrollSelectedIntoView(container) {
    const selected = container.querySelector(".selected");
    if (!selected) {
        return;
    }

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const left = selected.offsetLeft - (container.clientWidth - selected.offsetWidth) / 2;
    container.scrollTo({ left, behavior: reduceMotion ? "auto" : "smooth" });
}
