const doc = document,
  isAnswered = i => i.type == "radio" || i.type == "checkbox" ? i.checked : i.type != "hidden" && i.value != "",
  updateButton = b => {
    const p = b.closest("[data-aioros-answers]");
    p && (b.disabled = ![...p.querySelectorAll("input,textarea")].some(i => i.checkVisibility() && isAnswered(i)));
  },
  updateAll = () => doc.querySelectorAll("button[data-aioros-needs-answer]").forEach(updateButton);
doc.addEventListener("change", updateAll);
doc.addEventListener("input", updateAll);
updateAll();
var module = {
  i() {
    updateAll();
  }
};
export { module as default };
