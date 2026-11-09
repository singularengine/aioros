const doc = document,
  followed = new WeakMap(),
  atEnd = s => s.hasAttribute("data-aioros-follow") ? s.scrollTop >= -1 : s.scrollHeight - s.clientHeight - s.scrollTop <= 1,
  scrolledBy = b => {
    for (let p = b.parentElement; p; p = p.parentElement) {
      const s = p.querySelector(`[data-aioros-scroll-name="${CSS.escape(b.dataset.aiorosScrolls)}"]`);
      if (s) return s;
    }
    return null;
  },
  update = () => {
    doc.querySelectorAll("[data-aioros-scroll-name]").forEach(s => followed.set(s, atEnd(s)));
    doc.querySelectorAll("[data-aioros-scrolls]").forEach(b => {
      const s = scrolledBy(b);
      s && b.toggleAttribute("data-aioros-at-end", atEnd(s));
    });
  },
  keepFollowing = () => {
    doc.querySelectorAll("[data-aioros-follow]").forEach(s => {
      followed.get(s) && !atEnd(s) && (s.scrollTop = 0);
    });
    update();
  },
  resizes = new ResizeObserver(keepFollowing),
  init = () => {
    doc.querySelectorAll("[data-aioros-scroll-name]").forEach(s => {
      resizes.observe(s);
      s.firstElementChild && resizes.observe(s.firstElementChild);
    });
    update();
  };
doc.addEventListener("scroll", update, true);
doc.addEventListener("click", e => {
  const b = e.target.closest?.("[data-aioros-scrolls]"),
    s = b && scrolledBy(b);
  if (!s) return;
  e.preventDefault();
  s.scrollTo({
    top: s.scrollHeight,
    behavior: "smooth"
  });
});
init();
var module = {
  i: init
};
export { module as default };
