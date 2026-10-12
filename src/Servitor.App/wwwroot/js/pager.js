// Scroll-snap pager helpers. Wrapped by Shared/Pager/JsPagerInterop.cs; nothing else talks to it.
const settleDelayMs = 80;
const listeners = new WeakMap();

export function attach(track, callback) {
    detach(track);

    let timer = null;
    let reported = currentIndex(track);
    const onScroll = () => {
        clearTimeout(timer);
        timer = setTimeout(() => {
            const index = currentIndex(track);
            if (index !== reported) {
                reported = index;
                callback.invokeMethodAsync("OnPageChanged", index);
            }
        }, settleDelayMs);
    };

    track.addEventListener("scroll", onScroll, { passive: true });
    listeners.set(track, () => {
        clearTimeout(timer);
        track.removeEventListener("scroll", onScroll);
    });
}

export function detach(track) {
    listeners.get(track)?.();
    listeners.delete(track);
}

export function scrollToPage(track, index) {
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    track.scrollTo({ left: index * track.clientWidth, behavior: reduceMotion ? "auto" : "smooth" });
}

function currentIndex(track) {
    return track.clientWidth === 0 ? 0 : Math.round(track.scrollLeft / track.clientWidth);
}
