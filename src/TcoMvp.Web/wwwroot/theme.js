(function () {
  const KEY = "tco-theme";
  const root = document.documentElement;

  function stored() {
    try { return localStorage.getItem(KEY); } catch { return null; }
  }

  function apply(theme) {
    if (theme === "light") root.setAttribute("data-theme", "light");
    else root.removeAttribute("data-theme");
  }

  function current() {
    return root.getAttribute("data-theme") === "light" ? "light" : "dark";
  }

  function render(button) {
    const light = current() === "light";
    button.setAttribute("aria-pressed", String(light));
    button.title = light ? "Zum dunklen Modus wechseln" : "Zum hellen Modus wechseln";
    button.innerHTML = light
      ? '<span class="icon" aria-hidden="true">☾</span>'
      : '<span class="icon" aria-hidden="true">☀</span>';
  }

  apply(stored());

  document.addEventListener("DOMContentLoaded", () => {
    const button = document.getElementById("theme-toggle");
    if (!button) return;
    render(button);
    button.addEventListener("click", () => {
      const next = current() === "light" ? "dark" : "light";
      apply(next);
      try { localStorage.setItem(KEY, next); } catch { /* Speichern optional */ }
      render(button);
    });
  });
})();
