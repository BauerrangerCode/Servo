// Screen Wake Lock. Wrapped by Shared/WakeLock/JsWakeLockService.cs; nothing else talks to it.
let sentinel = null;
let wanted = false;

// The browser drops the lock whenever the page is hidden, so it is requested again once the page is visible.
document.addEventListener("visibilitychange", () => {
    if (wanted && document.visibilityState === "visible") {
        request();
    }
});

export async function acquire() {
    wanted = true;
    return await request();
}

export async function release() {
    wanted = false;
    const current = sentinel;
    sentinel = null;
    await current?.release();
}

async function request() {
    if (!("wakeLock" in navigator)) {
        return false;
    }

    if (sentinel && !sentinel.released) {
        return true;
    }

    try {
        sentinel = await navigator.wakeLock.request("screen");
        return true;
    } catch {
        // Refused (battery saver, no permission): the screen may then dim as usual, which is not an error.
        return false;
    }
}
