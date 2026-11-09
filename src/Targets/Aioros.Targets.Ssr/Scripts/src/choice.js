const doc = document,
  choiceSelector = "[data-aioros-choice]",
  ownElements = (p, s) => [...p.querySelectorAll(s)].filter(x => x.closest(choiceSelector) == p);
doc.addEventListener("click", e => {
  const b = e.target.closest?.("[data-aioros-chooses]"),
    p = b?.closest(choiceSelector),
    h = p?.querySelector(":scope>input[type=hidden]");
  if (!h || b.disabled) return;
  e.preventDefault();
  let v = +b.dataset.aiorosChooses;
  if (p.hasAttribute("data-aioros-toggle") && b.getAttribute("aria-checked") == "true") v = -1;
  h.value = v;
  h.disabled = false;
  ownElements(p, "[data-aioros-chooses]").forEach(o => o.setAttribute("aria-checked", +o.dataset.aiorosChooses === v));
  const t = p.querySelector(`:scope>template[data-aioros-value="${v}"]`),
    s = ownElements(p, "[data-aioros-shows]")[0],
    n = t?.content.firstElementChild;
  if (t && s) n ? s.replaceWith(n.cloneNode(true)) : s.hidden = true;
  try {
    b.closest("[popover]")?.hidePopover();
  } catch {}
  h.hasAttribute("data-change") && h.dispatchEvent(new Event("change", {
    bubbles: true
  }));
});
var module = {
  i() {}
};
export { module as default };
