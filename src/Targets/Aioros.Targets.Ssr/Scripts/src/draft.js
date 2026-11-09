const draftScope = () => (document.cookie.match(/(?:^|; )aioros-draft-scope=([^;]*)/) || [])[1] || "",
  pageKey = () => (draftScope() && draftScope() + " ") + location.pathname + location.search,
  openDatabase = (v, r, j) => {
    const o = v ? indexedDB.open("aioros-drafts", v) : indexedDB.open("aioros-drafts");
    o.onupgradeneeded = () => o.result.createObjectStore("pages");
    o.onerror = () => j(o.error);
    o.onsuccess = () => {
      const d = o.result;
      if (d.objectStoreNames.contains("pages")) return r(d);
      const n = d.version + 1;
      d.close();
      openDatabase(n, r, j);
    };
  },
  database = () => new Promise((r, j) => openDatabase(0, r, j)),
  transact = (m, f) => database().then(d => new Promise(r => {
    const t2 = d.transaction("pages", m),
      q = f(t2.objectStore("pages"));
    t2.oncomplete = () => r(q.result);
    t2.onerror = () => r(null);
  })).catch(() => null),
  draftedElements = () => {
    const f = document.querySelector("form");
    if (!f) return [];
    const lo = new Set([...f.querySelectorAll("[data-aioros-local]")].map(x => x.name));
    return [...f.elements].filter(e => e.name && !e.disabled && !lo.has(e.name) && !/^(hidden|file|submit|button|reset|image|password)$/.test(e.type));
  },
  isToggle = e => e.type === "checkbox" || e.type === "radio",
  valueOf = e => isToggle(e) ? e.checked : e.value,
  defaultOf = e => isToggle(e) ? e.defaultChecked : e.tagName === "SELECT" ? ([...e.options].find(o => o.defaultSelected) || e.options[0] || {}).value : e.defaultValue,
  keyOf = e => e.type === "radio" ? e.name + "=" + e.value : e.name,
  edits = () => {
    const o = {};
    for (const e of draftedElements()) if (valueOf(e) !== defaultOf(e)) o[keyOf(e)] = valueOf(e);
    return o;
  },
  mark = v => document.documentElement.setAttribute("data-aioros-draft", v),
  save = () => {
    const o = edits(),
      k = Object.keys(o).length;
    return transact("readwrite", s => k ? s.put(o, pageKey()) : s.delete(pageKey())).then(() => mark(k ? "saved" : "none"));
  };
let timer = 0;
const later = e => {
  if (!e.isTrusted) return;
  clearTimeout(timer);
  timer = setTimeout(save, 300);
};
document.addEventListener("input", later);
document.addEventListener("change", later);
let offered = "";
function offer() {
  if (offered === pageKey()) return;
  offered = pageKey();
  document.querySelector(".aioros-draft")?.remove();
  transact("readonly", s => s.get(pageKey())).then(o => {
    if (!o) return;
    const m = draftedElements().filter(e => keyOf(e) in o && valueOf(e) !== o[keyOf(e)]);
    if (!m.length) return;
    const n = document.createElement("div");
    n.className = "aioros-draft";
    n.setAttribute("role", "status");
    n.innerHTML = '<span>Unsaved changes from your last visit.</span><button type="button" data-restore>Restore</button><button type="button" data-discard>Discard</button>';
    n.querySelector("[data-restore]").onclick = () => {
      n.remove();
      for (const e of m) {
        const v = o[keyOf(e)];
        isToggle(e) ? e.checked = v : e.value = v;
        e.dispatchEvent(new Event("input", {
          bubbles: true
        }));
        e.dispatchEvent(new Event("change", {
          bubbles: true
        }));
      }
    };
    n.querySelector("[data-discard]").onclick = () => {
      n.remove();
      transact("readwrite", s => s.delete(pageKey()));
    };
    document.body.append(n);
  });
}
offer();
var module = {
  i: offer,
  c() {
    if (!Object.keys(edits()).length) transact("readwrite", s => s.delete(pageKey())).then(() => mark("none"));
  }
};
export { module as default };
