// Lets the Servo launcher know that the app is open, so it can stop itself once the window is closed.
// Harmless anywhere else: the requests simply go unanswered or are ignored by the server.
(function () {
  const heartbeat = () => fetch('servo-heartbeat', { cache: 'no-store' }).catch(() => { });
  heartbeat();
  setInterval(heartbeat, 5000);
  window.addEventListener('pagehide', () => navigator.sendBeacon('servo-closing'));
})();
