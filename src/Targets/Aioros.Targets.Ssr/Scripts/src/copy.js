document.addEventListener("click", (event) => {
  const button = event.target.closest?.("[data-aioros-copies]");
  if (!button || button.disabled) {
    return;
  }

  event.preventDefault();
  navigator.clipboard.writeText(button.dataset.aiorosCopies).then(() => {
    button.setAttribute("data-copied", "");
    clearTimeout(button.aiorosCopied);
    button.aiorosCopied = setTimeout(() => button.removeAttribute("data-copied"), 2000);
  }, () => {});
});

export default { i() {} };
