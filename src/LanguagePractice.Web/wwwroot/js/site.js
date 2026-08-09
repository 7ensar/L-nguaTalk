(() => {
  function closePanel(root, btnSel, menuSel) {
    const btn = root.querySelector(btnSel);
    const menu = root.querySelector(menuSel);
    if (!menu || !btn) return;
    menu.hidden = true;
    btn.setAttribute("aria-expanded", "false");
    root.classList.remove("is-open");
  }

  function openPanel(root, btnSel, menuSel) {
    const btn = root.querySelector(btnSel);
    const menu = root.querySelector(menuSel);
    if (!menu || !btn) return;
    menu.hidden = false;
    btn.setAttribute("aria-expanded", "true");
    root.classList.add("is-open");
  }

  function initToggleMenu(rootSel, btnSel, menuSel) {
    const root = document.querySelector(rootSel);
    if (!root) return null;

    const btn = root.querySelector(btnSel);
    const menu = root.querySelector(menuSel);
    if (!btn || !menu) return null;

    // Başlangıçta her zaman kapalı
    menu.hidden = true;
    btn.setAttribute("aria-expanded", "false");
    root.classList.remove("is-open");

    return { root, btn, menu, btnSel, menuSel };
  }

  function initHeaderMenus() {
    const lang = initToggleMenu("[data-lang-switcher]", ".language-switcher-btn", ".language-menu");
    const profile = initToggleMenu("[data-profile-switcher]", ".profile-toggle", ".profile-menu");
    const menus = [lang, profile].filter(Boolean);

    function closeAll(except) {
      for (const m of menus) {
        if (m !== except) {
          closePanel(m.root, m.btnSel, m.menuSel);
        }
      }
    }

    for (const m of menus) {
      m.btn.addEventListener("click", (e) => {
        e.preventDefault();
        e.stopPropagation();
        if (m.menu.hidden) {
          closeAll(m);
          openPanel(m.root, m.btnSel, m.menuSel);
        } else {
          closePanel(m.root, m.btnSel, m.menuSel);
        }
      });
    }

    document.addEventListener("click", (e) => {
      for (const m of menus) {
        if (!m.root.contains(e.target)) {
          closePanel(m.root, m.btnSel, m.menuSel);
        }
      }
    });

    document.addEventListener("keydown", (e) => {
      if (e.key === "Escape") {
        closeAll(null);
      }
    });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", initHeaderMenus);
  } else {
    initHeaderMenus();
  }
})();
