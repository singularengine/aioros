(() => {
  let s = null, o = null, l = 0;
  const bx = () => {
    if (!o) {
      o = document.body.appendChild(document.createElement("pre"));
      o.setAttribute("data-aioros-fault", "");
      o.style.cssText = "position:fixed;left:16px;right:16px;bottom:16px;z-index:9999;margin:0;padding:12px 16px;border-radius:8px;background:#7f1d1d;color:#fff;font:12px/1.5 ui-monospace,monospace;white-space:pre-wrap;max-height:70vh;overflow:auto";
    }
    o.textContent = "";
    o.removeAttribute("data-aioros-build");
    return o;
  }, rl = () => {
    o && o.remove();
    o = null;
    window.aioros && aioros.go ? aioros.go(location.href, false, true) : location.reload();
  }, op = () => {
    s = new EventSource("/_aioros/dev");
    s.onerror = () => {
      l = 1;
    };
    s.onopen = () => {
      l && rl();
      l = 0;
    };
    s.addEventListener("reload", rl);
    s.addEventListener("failure", (e) => {
      bx().textContent = JSON.parse(e.data).message;
    });
    s.addEventListener("build", (e) => bd(e));
  };
  const t = (p, n, x) => {
    const e = p.appendChild(document.createElement(n));
    e.textContent = x;
    return e;
  }, at = (b, file, line, column, code) => {
    t(b, "div", file + ":" + line + (column ? ":" + column : ""));
    for (const c of code) {
      const e = t(b, "div", (c.line === line ? ">" : " ") + String(c.line).padStart(5) + " | " + c.text);
      if (c.line === line) e.style.background = "#991b1b";
    }
  };
  window.aiorosFault = (f) => {
    const b = bx();
    t(b, "strong", f.type + ": " + f.message);
    if (f.file) at(b, f.file, f.line, f.column, f.code);
    else if (f.at) t(b, "div", "at " + f.at);
    const d = b.appendChild(document.createElement("details"));
    t(d, "summary", "Stack");
    t(d, "div", f.stack);
  };
  const bd = (e) => {
    const r = JSON.parse(e.data);
    if (!r.errors.length) {
      if (o && o.hasAttribute("data-aioros-build")) {
        o.remove();
        o = null;
      }
      return;
    }
    const b = bx();
    b.setAttribute("data-aioros-build", "");
    t(b, "strong", "Build failed");
    for (const x of r.errors) {
      t(b, "div", " ");
      t(b, "div", x.code + ": " + x.message);
      at(b, x.file, x.line, x.column, x.source);
    }
  };
  addEventListener("pagehide", () => {
    s && s.close();
    s = null;
  });
  addEventListener("pageshow", (e) => {
    e.persisted && !s && op();
  });
  (navigator.webdriver || !/HeadlessChrome/.test(navigator.userAgent)) && op();
})();
