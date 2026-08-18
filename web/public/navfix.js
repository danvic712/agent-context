// Dashboard nav-link fix (single-port model, issue #15).
//
// The Aspire dashboard's nav links are hard-coded root paths ("/", "/consolelogs",
// "/metrics", ...) that bypass <base href>. The portal serves the dashboard under
// /monitor, so those links are rewritten to the /monitor prefix: Blazor then
// navigates in-place (enhanced navigation, no full-page redirect), which keeps the
// URL stable under /monitor and avoids the flash of a server-side redirect.
//
// The prefix is read from <base href> so this file stays agnostic of the actual
// value. The dashboard page loads this file via <script src="/navfix.js"> (same
// origin, so it passes the dashboard's CSP script-src 'self').
(function () {
  'use strict'

  var prefix = (new URL(document.baseURI).pathname.replace(/\/+$/, '')) || ''
  var pagePrefixes = ['/resources', '/consolelogs', '/structuredlogs', '/traces', '/metrics', '/login']

  function isDashboardRootPath(path) {
    if (path === '/') return true
    for (var i = 0; i < pagePrefixes.length; i++) {
      var p = pagePrefixes[i]
      if (path === p || path.indexOf(p + '/') === 0) return true
    }
    return false
  }

  function fix() {
    document.querySelectorAll('a[href^="/"]').forEach(function (a) {
      var raw = a.getAttribute('href')
      // Skip protocol-relative URLs (//host), empty links, and links that
      // already carry the dashboard prefix.
      if (!raw || raw.indexOf('//') === 0 || prefix === '' ||
          raw === prefix || raw.indexOf(prefix + '/') === 0) return

      var url
      try {
        url = new URL(raw, window.location.origin)
      } catch (_) {
        return
      }
      if (url.origin !== window.location.origin || !isDashboardRootPath(url.pathname)) return
      // Match on pathname so query strings and fragments are preserved.
      a.setAttribute('href', prefix + url.pathname + url.search + url.hash)
    })
  }

  if (document.body) fix()
  // Blazor renders the nav links after the initial HTML, so keep watching.
  new MutationObserver(fix).observe(document.documentElement, { childList: true, subtree: true })
})()
