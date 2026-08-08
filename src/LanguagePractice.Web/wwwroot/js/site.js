(() => {
  function closeSwitcher(root) {
    const btn = root.querySelector(".language-switcher-btn");
    const menu = root.querySelector(".language-menu");
    if (!menu || !btn) return;
    menu.hidden = true;
    btn.setAttribute("aria-expanded", "false");
    root.classList.remove("is-open");
  }

  function openSwitcher(root) {
    const btn = root.querySelector(".language-switcher-btn");
    const menu = root.querySelector(".language-menu");
    if (!menu || !btn) return;
    menu.hidden = false;
    btn.setAttribute("aria-expanded", "true");
    root.classList.add("is-open");
  }

  function initLanguageSwitcher() {
    const root = document.querySelector("[data-lang-switcher]");
    if (!root) return;

    const btn = root.querySelector(".language-switcher-btn");
    const menu = root.querySelector(".language-menu");
    if (!btn || !menu) return;

    // Başlangıçta kapalı
    menu.hidden = true;
    btn.setAttribute("aria-expanded", "false");
    root.classList.remove("is-open");

    btn.addEventListener("click", (e) => {
      e.preventDefault();
      e.stopPropagation();
      if (menu.hidden) {
        openSwitcher(root);
      } else {
        closeSwitcher(root);
      }
    });

    document.addEventListener("click", (e) => {
      if (!root.contains(e.target)) {
        closeSwitcher(root);
      }
    });

    document.addEventListener("keydown", (e) => {
      if (e.key === "Escape") {
        closeSwitcher(root);
      }
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initLanguageSwitcher);
  } else {
    initLanguageSwitcher();
  }
})();
