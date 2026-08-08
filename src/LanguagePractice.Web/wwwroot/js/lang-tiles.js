(() => {
  function findHiddenForGrid(grid) {
    const prev = grid.previousElementSibling;
    if (prev && prev.classList?.contains("lang-tiles-value")) {
      return prev;
    }

    const form = grid.closest("form");
    if (form) {
      const fromForm = form.querySelector(".lang-tiles-value");
      if (fromForm) return fromForm;
    }

    return document.getElementById("languageCode") || document.getElementById("homeLang");
  }

  function bindTiles(root = document) {
    root.querySelectorAll("[data-lang-tiles]").forEach((grid) => {
      if (grid.dataset.bound === "1") return;
      grid.dataset.bound = "1";

      const hidden = findHiddenForGrid(grid);

      grid.addEventListener("click", (e) => {
        const tile = e.target.closest("[data-lang-tile]");
        if (!tile || tile.disabled || grid.classList.contains("disabled")) return;

        const code = tile.getAttribute("data-lang");
        if (!code) return;

        grid.querySelectorAll("[data-lang-tile]").forEach((btn) => {
          const on = btn === tile;
          btn.classList.toggle("selected", on);
          btn.setAttribute("aria-selected", on ? "true" : "false");
        });

        if (hidden) hidden.value = code;
        grid.dispatchEvent(new CustomEvent("lang:change", { bubbles: true, detail: { languageCode: code } }));
      });
    });
  }

  async function refreshCounts() {
    try {
      const res = await fetch("/api/presence/online", { headers: { Accept: "application/json" } });
      if (!res.ok) return;
      const data = await res.json();

      if (typeof data.online === "number") {
        const onlineEl = document.getElementById("onlineCount");
        if (onlineEl) onlineEl.textContent = data.online;
      }

      const byLang = data.byLanguage || {};
      document.querySelectorAll("[data-lang-count]").forEach((el) => {
        const code = el.getAttribute("data-lang-count");
        const n = Number(byLang[code] ?? 0);
        el.textContent = String(Number.isFinite(n) ? n : 0);
      });
    } catch (_) {
      /* ignore */
    }
  }

  function startPolling(ms = 6000) {
    refreshCounts();
    window.setInterval(refreshCounts, ms);
  }

  document.addEventListener("DOMContentLoaded", () => {
    bindTiles();
    if (document.querySelector("[data-lang-count]")) {
      startPolling(6000);
    }
  });

  window.LinguaLangTiles = { bindTiles, refreshCounts, startPolling };
})();
