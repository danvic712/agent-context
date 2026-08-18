// Dashboard nav-link fix (single-port model, issue #15).
//
// The Aspire dashboard's nav links are hard-coded root paths ("/", "/consolelogs",
// "/metrics", ...) that bypass <base href>. The portal serves the dashboard under
// /monitor, so those links are rewritten to the /monitor prefix (with "/" mapped
// to /monitor/resources): Blazor then
// navigates in-place (enhanced navigation, no full-page redirect), which keeps the
// URL stable under /monitor and avoids the flash of a server-side redirect.
//
// The prefix is read from <base href> so this file stays agnostic of the actual
// value. The dashboard page loads this file via <script src="/navfix.js"> (same
// origin, so it passes the dashboard's CSP script-src 'self').
(function () {
  'use strict'

  var configuredPrefix = document.currentScript && document.currentScript.getAttribute('data-dashboard-prefix')
  var prefix = configuredPrefix || (new URL(document.baseURI).pathname.replace(/\/+$/, '')) || ''
  // The canonical Resources page uses /monitor/resources as its base, but
  // sibling Dashboard routes remain directly under /monitor.
  if (!configuredPrefix && prefix.endsWith('/resources')) prefix = prefix.slice(0, -'/resources'.length)
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
      // Skip protocol-relative URLs (//host) and empty links.
      if (!raw || raw.indexOf('//') === 0 || prefix === '') return

      var url
      try {
        url = new URL(raw, window.location.origin)
      } catch (_) {
        return
      }
      if (url.origin !== window.location.origin) return

      var path = url.pathname
      var resourcePrefix = prefix + '/resources'
      if (path === resourcePrefix || path === resourcePrefix + '/') {
        a.setAttribute('href', resourcePrefix + url.search + url.hash)
        return
      }
      if (path.indexOf(resourcePrefix + '/') === 0) {
        // Relative links generated under the Resources base must not become
        // /monitor/resources/consolelogs; sibling routes live under /monitor.
        a.setAttribute('href', prefix + path.slice(resourcePrefix.length) + url.search + url.hash)
        return
      }

      var alreadyPrefixed = path === prefix || path.indexOf(prefix + '/') === 0
      if (alreadyPrefixed) {
        // Normalize Aspire's root/home link to the canonical Resources route.
        if (path === prefix || path === prefix + '/') {
          a.setAttribute('href', prefix + '/resources' + url.search + url.hash)
        }
        return
      }
      if (!isDashboardRootPath(path)) return

      // Aspire's home link is "/", but the canonical public Resources route
      // is /resources. Keep every sidebar link under /monitor and preserve
      // query strings/fragments for resource views.
      var dashboardPath = path === '/' ? '/resources' : path
      a.setAttribute('href', prefix + dashboardPath + url.search + url.hash)
    })
  }

  if (document.body) fix()
  // Blazor renders the nav links after the initial HTML, so keep watching.
  new MutationObserver(fix).observe(document.documentElement, { childList: true, subtree: true })
})()
