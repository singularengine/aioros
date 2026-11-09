const doc = document,
  flagSelector = "[data-aioros-flag]",
  isNarrow = p => innerWidth < +p.dataset.aiorosBreakpoint,
  sheetOf = p => [...p.querySelectorAll("dialog[data-aioros-sheet]")].find(x => x.closest(flagSelector) == p),
  markFlips = (p, o) => p.querySelectorAll(`button[data-aioros-flips="${CSS.escape(p.dataset.aiorosFlag)}"]`).forEach(x => x.closest(flagSelector) == p && x.setAttribute("aria-expanded", o)),
  followDetails = (p, o) => p.querySelectorAll(`details[data-aioros-follows="${CSS.escape(p.dataset.aiorosFlag)}"]`).forEach(x => x.closest(flagSelector) == p && (x.open = x.dataset.aiorosOpenOn == "true" == o)),
  toggleFlag = p => {
    const o = p.toggleAttribute("data-aioros-on");
    markFlips(p, o);
    followDetails(p, o);
    const c = p.querySelector(":scope>input[type=checkbox][hidden]");
    if (c) {
      c.checked = o;
      p.hasAttribute("data-change") && c.dispatchEvent(new Event("change", {
        bubbles: true
      }));
    }
    const k = p.dataset.aiorosCookie;
    k && (doc.cookie = k + "=" + o + ";path=/;max-age=604800;samesite=lax" + (isSecureContext ? ";secure" : ""));
  };
doc.addEventListener("click", e => {
  const b = e.target.closest?.("[data-aioros-flips]"),
    p = b?.closest(`[data-aioros-flag="${CSS.escape(b.dataset.aiorosFlips)}"]`);
  if (!p || b.disabled) return;
  e.preventDefault();
  const s = sheetOf(p);
  if (s && isNarrow(p)) {
    s.open ? s.close() : (s.showModal(), markFlips(p, true));
    return;
  }
  toggleFlag(p);
});
doc.addEventListener("toggle", e => {
  const x = e.target;
  if (!x.matches?.("details[data-aioros-follows]")) return;
  const p = x.closest(`[data-aioros-flag="${CSS.escape(x.dataset.aiorosFollows)}"]`);
  p && p.hasAttribute("data-aioros-on") != (x.open == (x.dataset.aiorosOpenOn == "true")) && toggleFlag(p);
}, true);
doc.addEventListener("close", e => {
  const s = e.target,
    p = s.matches?.("dialog[data-aioros-sheet]") && s.closest(flagSelector);
  p && markFlips(p, !isNarrow(p) && p.hasAttribute("data-aioros-on"));
}, true);
addEventListener("resize", () => doc.querySelectorAll("dialog[data-aioros-sheet][open]").forEach(s => {
  const p = s.closest(flagSelector);
  p && !isNarrow(p) && s.close();
}));
var module = {
  i() {}
};
export { module as default };
