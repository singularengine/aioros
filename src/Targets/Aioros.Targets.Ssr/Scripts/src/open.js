document.addEventListener("click", (e) => {
  const a = e.target.closest && e.target.closest("a[data-open-app]"), s = document.querySelector("meta[name=aioros-app]");
  if (!a || !s || e.button || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
  e.preventDefault();
  e.stopPropagation();
  let away = false;
  const p = location.pathname + location.search, off = () => {
    away = true;
  }, on = [[window, "blur"], [window, "pagehide"], [document, "visibilitychange"]];
  on.forEach(([t, n]) => t.addEventListener(n, off));
  setTimeout(() => {
    on.forEach(([t, n]) => t.removeEventListener(n, off));
    if (!away && !document.hidden) {
      const u = new URL(a.href);
      u.searchParams.set("return", p);
      location.href = u;
    }
  }, 1500);
  location.href = s.content + "://open?path=" + encodeURIComponent(p);
}, true);
