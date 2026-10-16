/* BodyOps — push reminders on this phone or computer (Web Push, same as Holo). The browser subscribes with the
   server's public key; the server encrypts each reminder for it and sends it through the browser's own push service.
   Needs HTTPS; on iPhone and iPad Milo must be on the Home Screen first. */
'use strict';

(() => {
  const push = BO.push = {
    state: null,     // PushDTO from /api/push
    here: null,      // this browser's device key when it is one of the account's devices
  };

  push.supported = () => window.isSecureContext && 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
  push.appleMobile = () => /iPhone|iPad|iPod/.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  push.installed = () => window.matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;

  /** Why this browser can't get reminders, in words the person can act on; null when it can. */
  push.problem = function problem() {
    if (!window.isSecureContext) return `This page isn't HTTPS (${location.origin}). Open Milo at your https address.`;
    if (!('serviceWorker' in navigator)) return "This browser has service workers turned off (private mode or a setting), so it can't get reminders.";
    if (!('PushManager' in window) || !('Notification' in window)) {
      if (push.appleMobile()) {
        return push.installed()
          ? 'iOS needs 16.4 or later, and the icon added from Safari on the https address: remove it, open Milo in Safari, tap Share › Add to Home Screen, and open it from the new icon.'
          : 'On iPhone and iPad: tap Share › Add to Home Screen, open Milo from there and turn reminders on.';
      }
      return "This browser doesn't support web push. Try Chrome, Edge, Firefox or Safari.";
    }
    return null;
  };

  const bytes = text => {
    const b64 = text.replace(/-/g, '+').replace(/_/g, '/');
    const raw = atob(b64 + '='.repeat((4 - (b64.length % 4)) % 4));
    return Uint8Array.from(raw, c => c.charCodeAt(0));
  };
  const base64Url = buffer => btoa(String.fromCharCode(...new Uint8Array(buffer))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

  /** The key the server gives an endpoint (first 12 hex characters of its SHA-256). */
  async function keyOf(endpoint) {
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(endpoint));
    return [...new Uint8Array(digest)].map(b => b.toString(16).padStart(2, '0')).join('').slice(0, 12);
  }

  /** "iPhone · Milo app", "Windows · Edge"… so the device list says which is which. */
  push.deviceLabel = function deviceLabel() {
    const a = navigator.userAgent;
    const device = /iPad/.test(a) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) ? 'iPad'
      : /iPhone/.test(a) ? 'iPhone' : /Android/.test(a) ? 'Android' : /Mac OS X/.test(a) ? 'Mac' : /Windows/.test(a) ? 'Windows' : /Linux/.test(a) ? 'Linux' : 'Device';
    const browser = /Edg\//.test(a) ? 'Edge' : /SamsungBrowser/.test(a) ? 'Samsung Internet' : /Firefox\//.test(a) ? 'Firefox'
      : /OPR\//.test(a) ? 'Opera' : /Chrome\//.test(a) ? 'Chrome' : /Safari\//.test(a) ? 'Safari' : 'Browser';
    return `${device} · ${push.installed() ? 'Milo app' : browser}`;
  };

  async function registration() {
    await navigator.serviceWorker.register('/sw.js');
    return navigator.serviceWorker.ready;
  }

  async function currentSubscription() {
    if (!push.supported()) return null;
    const reg = await navigator.serviceWorker.getRegistration();
    return reg ? reg.pushManager.getSubscription() : null;
  }

  /** Loads the push state and whether this browser is one of the devices. */
  push.load = async function load() {
    try { push.state = await BO.api('GET', '/api/push'); } catch (e) { push.state = { enabled: false, publicKey: '', devices: [], failed: true }; }
    push.here = null;
    if (push.supported() && Notification.permission === 'granted') {
      try {
        const subscription = await currentSubscription();
        if (subscription) {
          const key = await keyOf(subscription.endpoint);
          if (push.state.devices.some(d => d.key === key)) push.here = key;
        }
      } catch (e) { /* not this browser */ }
    }
    return push.state;
  };

  /** Subscribes this browser (asks for permission first, in the same tap) and registers it with the server. */
  push.enable = async function enable({ quiet = false } = {}) {
    if (!push.supported()) { if (!quiet) BO.toast(push.problem() || "This browser can't get reminders here.", { error: true }); return false; }
    if (!push.state || !push.state.publicKey) await push.load();
    if (!push.state.publicKey) { if (!quiet) BO.toast("Couldn't get the server's push key.", { error: true }); return false; }

    const permission = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission();
    if (permission !== 'granted') {
      if (!quiet) {
        BO.toast(permission === 'denied'
          ? (push.appleMobile() ? 'Notifications are off for Milo. Turn them on in Settings › Notifications › Milo, then try again.'
            : 'Notifications are blocked for Milo. Allow them in the site settings (the icon next to the address) and try again.')
          : 'The prompt closed without an answer. Tap Turn on again and choose Allow.', { error: true });
      }
      await push.load();
      return false;
    }

    try {
      const reg = await registration();
      let subscription = await reg.pushManager.getSubscription();
      const current = subscription && subscription.options && subscription.options.applicationServerKey;
      if (subscription && current && base64Url(current) !== push.state.publicKey) {
        await subscription.unsubscribe();   // made with another key (the server's data folder was reset)
        subscription = null;
      }
      subscription = subscription || await reg.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: bytes(push.state.publicKey) });
      const json = subscription.toJSON();
      await BO.api('POST', '/api/push/subscribe', { endpoint: json.endpoint, p256dh: json.keys.p256dh, auth: json.keys.auth, device: push.deviceLabel() });
      if (!quiet) BO.toast('Reminders are on for this device');
    } catch (e) {
      if (!quiet) {
        BO.toast(e && (e.name === 'NotAllowedError' || e.name === 'AbortError')
          ? "The browser refused push here. Private windows can't get it; in a normal window, allow notifications for Milo and try again."
          : (e && e.message) || 'Turning reminders on failed.', { error: true });
      }
    }
    await push.load();
    return Boolean(push.here);
  };

  /** Unsubscribes this browser and removes it from the server. */
  push.disable = async function disable() {
    try {
      const subscription = await currentSubscription();
      if (subscription) {
        await BO.api('POST', '/api/push/unsubscribe', { endpoint: subscription.endpoint });
        await subscription.unsubscribe();
      }
      BO.toast('Reminders are off for this device');
    } catch (e) { BO.toast(e.message, { error: true }); }
    await push.load();
  };

  /** Removes another device of the account. */
  push.remove = async function remove(id) {
    try { await BO.saving(BO.api('POST', '/api/push/unsubscribe', { id })); } catch (e) { }
    await push.load();
  };

  /** Sends a test reminder to every device of the account. */
  push.test = async function test() {
    try {
      const result = await BO.saving(BO.api('POST', '/api/push/test'));
      BO.toast(result.failed ? `Sent to ${BO.plural(result.reached, 'device')}; ${result.failed} failed` : `Test sent to ${BO.plural(result.reached, 'device')}`, { error: !result.reached });
    } catch (e) { }
    await push.load();
  };

  // The service worker: a new subscription to send, or a meal ticked from a notification.
  if ('serviceWorker' in navigator) {
    navigator.serviceWorker.addEventListener('message', event => {
      const type = event.data && event.data.type;
      if (type === 'push-resubscribe') push.enable({ quiet: true });
      if (type === 'day-changed' && location.pathname === '/today' && BO.reload) BO.reload();
    });
  }
})();
