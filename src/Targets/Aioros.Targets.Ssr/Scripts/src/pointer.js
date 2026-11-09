let target, point, frame, frameRequest;
const setPointer = (n, v) => target.style[v ? "setProperty" : "removeProperty"]("--aioros-pointer-" + n, v),
  clear = () => {
    target && (setPointer("x"), setPointer("y"));
    target = frame = 0;
  },
  write = f => {
    f && (frameRequest = 0);
    if (!target) return;
    frame = frame || (target.dataset.aiorosPointer == "viewbox" ? target.getScreenCTM().inverse() : target.getBoundingClientRect());
    const p = frame.inverse ? point.matrixTransform(frame) : {
      x: point.x - frame.x,
      y: point.y - frame.y
    };
    setPointer("x", p.x + "px");
    setPointer("y", p.y + "px");
  };
["pointermove", "pointerover"].forEach(n => document.addEventListener(n, e => {
  const t = e.pointerType != "touch" && e.target.closest?.("[data-aioros-pointer]");
  if (t != target) clear();
  if (t) {
    target = t;
    point = new DOMPoint(e.clientX, e.clientY);
    frameRequest = frameRequest || requestAnimationFrame(write);
  }
}, {
  passive: 1
}));
document.addEventListener("pointerleave", e => e.target == target && clear(), true);
["scroll", "resize"].forEach(n => addEventListener(n, () => frame = 0, true));
var module = {
  k: a => a == target && write()
};
export { module as default };
