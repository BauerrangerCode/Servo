// Random CRT artifacts and distortions. Wrapped by Shared/Effects/JsCrtEffectsService.cs; nothing else talks to it.
const storageKey = "servitor.crtEffects";
const effects = [tear, bar, flash, blocks];
let timer = null;
let overlay = null;

export function load() {
    try {
        return JSON.parse(localStorage.getItem(storageKey));
    } catch {
        return null;
    }
}

export function save(settings) {
    try {
        localStorage.setItem(storageKey, JSON.stringify(settings));
    } catch {
        // Private mode or blocked storage: the settings simply do not persist.
    }
}

export function start(minSeconds, maxSeconds) {
    stop();
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
        return;
    }

    overlay = document.createElement("div");
    overlay.id = "crt-glitch";
    document.body.appendChild(overlay);

    const schedule = () => {
        const delay = (minSeconds + Math.random() * (maxSeconds - minSeconds)) * 1000;
        timer = setTimeout(() => {
            effects[Math.floor(Math.random() * effects.length)]();
            schedule();
        }, delay);
    };
    schedule();
}

export function stop() {
    clearTimeout(timer);
    timer = null;
    overlay?.remove();
    overlay = null;
    resetApp();
}

function app() {
    return document.getElementById("app");
}

function resetApp() {
    const el = app();
    if (el) {
        el.style.transform = "";
        el.style.filter = "";
    }
}

function rand(min, max) {
    return min + Math.random() * (max - min);
}

function addElement(css, lifetimeMs) {
    if (!overlay) {
        return;
    }
    const el = document.createElement("div");
    el.style.cssText = "position:absolute;" + css;
    overlay.appendChild(el);
    setTimeout(() => el.remove(), lifetimeMs);
}

// Horizontal tear: the whole screen shifts sideways and skews for a moment.
function tear() {
    const el = app();
    if (!el) {
        return;
    }
    el.style.transform = `translateX(${rand(-14, 14)}px) skewX(${rand(-2, 2)}deg)`;
    el.style.filter = "brightness(1.4)";
    setTimeout(resetApp, rand(60, 180));
}

// Bright scan bar rolling over the screen.
function bar() {
    const height = rand(20, 90);
    addElement(
        `left:0;right:0;height:${height}px;top:${rand(0, 100)}vh;` +
        "background:linear-gradient(transparent,rgba(51,255,102,.25),transparent);" +
        "animation:crt-roll .5s linear forwards;",
        500);
}

// Whole-screen brightness/blur flash.
function flash() {
    const el = app();
    if (!el) {
        return;
    }
    el.style.filter = `brightness(${rand(1.6, 2.4)}) blur(${rand(0.5, 1.5)}px)`;
    setTimeout(resetApp, rand(40, 120));
}

// A few displaced blocks of noise, like a broken video buffer.
function blocks() {
    const count = Math.floor(rand(3, 8));
    for (let i = 0; i < count; i++) {
        addElement(
            `left:${rand(0, 90)}vw;top:${rand(0, 95)}vh;width:${rand(40, 320)}px;height:${rand(3, 22)}px;` +
            `background:rgba(51,255,102,${rand(0.15, 0.55)});`,
            rand(80, 220));
    }
}
