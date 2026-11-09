const doc = document,
  scrollerSelector = "[data-aioros-scroller]",
  scrolledLists = new WeakSet();
let timer = 0,
  button = null;
const stop = () => {
    clearInterval(timer);
    timer = 0;
    button = null;
  },
  scrollStep = () => {
    if (!button || !button.isConnected || getComputedStyle(button).display === "none") return stop();
    const e = button.parentElement,
      o = [...e.querySelectorAll(":scope>option")],
      a = o.find(x => x.offsetHeight),
      h = [e.selectedOptions[0], ...o.filter(x => !x.disabled)].find(x => x?.offsetHeight)?.offsetHeight,
      p = getComputedStyle(e, "::picker(select)"),
      r = button.getBoundingClientRect(),
      u = button.dataset.aiorosScroller === "up";
    if (!a || !h) return stop();
    const top = u ? r.top : r.bottom - parseFloat(p.height) + parseFloat(p.borderTopWidth) + parseFloat(p.borderBottomWidth),
      m = a.getBoundingClientRect().top - (u ? -h : h) - top - parseFloat(p.scrollPaddingTop);
    a.style.scrollMarginTop = m + "px";
    a.scrollIntoView({
      block: "start"
    });
    a.style.scrollMarginTop = "";
  };
doc.addEventListener("focusin", e => {
  const o = e.target,
    l = o.parentElement;
  if (o.tagName === "SELECT") return void scrolledLists.delete(o);
  if (o.tagName !== "OPTION" || scrolledLists.has(l) || !l.querySelector(":scope>" + scrollerSelector)) return;
  scrolledLists.add(l);
  const u = l.querySelector(":scope>[data-aioros-scroller=up]");
  [...l.querySelectorAll(":scope>option")].find(x => x.offsetHeight)?.scrollIntoView({
    block: "start"
  });
  if (u && getComputedStyle(u).display === "none") o.style.scrollMarginBottom = getComputedStyle(u).height;
  o.scrollIntoView({
    block: "nearest"
  });
  o.style.scrollMarginBottom = "";
});
doc.addEventListener("pointerover", e => {
  const x = e.target.closest?.(scrollerSelector);
  if (x && x !== button) {
    stop();
    button = x;
    timer = setInterval(scrollStep, 50);
  }
});
doc.addEventListener("pointerout", e => {
  const x = e.target.closest?.(scrollerSelector);
  if (x && x === button && !x.contains(e.relatedTarget)) stop();
});
var module = {};
export { module as default };
