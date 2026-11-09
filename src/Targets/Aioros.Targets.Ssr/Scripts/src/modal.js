const doc = document,
  modalSelector = "dialog[data-aioros-modal]",
  lacksCommands = !("commandForElement" in HTMLButtonElement.prototype),
  enableFieldset = g => {
    const f = g?.matches?.(modalSelector) && g.querySelector(":scope>fieldset");
    if (f) f.disabled = false;
  },
  syncModal = g => {
    if (!g?.matches?.(modalSelector)) return;
    const o = g.open,
      s = doc.getElementsByName(g.dataset.aiorosModal)[0],
      c = g.querySelector(":scope>input[type=checkbox][hidden]"),
      f = g.querySelector(":scope>fieldset");
    if (s) s.value = o ? "True" : "False";
    if (f) f.disabled = !o;
    const a = doc.activeElement;
    if (o && f && (!g.contains(a) || a.parentElement === f)) f.querySelector('input:not([type=hidden]),button,select,textarea,a[href],[tabindex]:not([tabindex="-1"])')?.focus();
    if (c && c.checked !== o) {
      c.checked = o;
      g.hasAttribute("data-change") && c.dispatchEvent(new Event("change", {
        bubbles: true
      }));
    }
  };
doc.addEventListener("click", e => {
  const t = e.target,
    b = lacksCommands && t.closest?.("button[commandfor]"),
    g = b && doc.getElementById(b.getAttribute("commandfor"));
  if (g && g.matches(modalSelector)) {
    e.preventDefault();
    b.getAttribute("command") == "close" ? g.close() : g.open || (enableFieldset(g), g.showModal());
    syncModal(g);
    return;
  }
  const h = t.closest?.(modalSelector);
  h && t.classList.contains("pm") && h.hasAttribute("data-aioros-dismiss") && h.close();
});
doc.addEventListener("command", e => e.command == "show-modal" ? enableFieldset(e.target) : queueMicrotask(() => syncModal(e.target)), true);
doc.addEventListener("close", e => syncModal(e.target), true);
doc.addEventListener("toggle", e => syncModal(e.target), true);
const init = () => doc.querySelectorAll(modalSelector).forEach(syncModal);
init();
var module = {
  i: init
};
export { module as default };
