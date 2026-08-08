(() => {
  const rootId = "lt-toast-root";

  function ensureRoot() {
    let root = document.getElementById(rootId);
    if (root) return root;
    root = document.createElement("div");
    root.id = rootId;
    root.className = "lt-toast-root";
    root.setAttribute("aria-live", "polite");
    root.setAttribute("aria-relevant", "additions");
    document.body.appendChild(root);
    return root;
  }

  function showToast(message, type = "info", timeoutMs = 3800) {
    if (!message) return;
    const root = ensureRoot();
    const el = document.createElement("div");
    el.className = `lt-toast lt-toast-${type}`;
    el.innerHTML = `<span class="lt-toast-dot"></span><span class="lt-toast-msg"></span>`;
    el.querySelector(".lt-toast-msg").textContent = message;
    root.appendChild(el);

    requestAnimationFrame(() => el.classList.add("show"));

    const close = () => {
      el.classList.remove("show");
      setTimeout(() => el.remove(), 220);
    };

    el.addEventListener("click", close);
    setTimeout(close, timeoutMs);
  }

  window.LinguaToast = {
    info: (msg, ms) => showToast(msg, "info", ms),
    success: (msg, ms) => showToast(msg, "success", ms),
    warn: (msg, ms) => showToast(msg, "warn", ms),
    error: (msg, ms) => showToast(msg, "error", ms)
  };
})();
