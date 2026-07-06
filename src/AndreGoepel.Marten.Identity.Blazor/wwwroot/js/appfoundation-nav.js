/*
 * AppFoundation off-canvas sidebar toggle (mobile / compact widths).
 *
 * Pure DOM + CSS: the hamburger and backdrop call these functions, which flip
 * data-nav-open on the .af-shell; the stylesheet's media queries do the rest.
 * A delegated listener closes the drawer when a sidebar link is followed.
 */
(function () {
  function shell() {
    return document.querySelector(".af-shell");
  }

  function set(open) {
    const s = shell();
    if (s) {
      s.setAttribute("data-nav-open", open ? "true" : "false");
    }
  }

  window.afNav = {
    toggle: function () {
      const s = shell();
      set(!(s && s.getAttribute("data-nav-open") === "true"));
    },
    open: function () {
      set(true);
    },
    close: function () {
      set(false);
    },
  };

  // Close the drawer after navigating from a sidebar link, and on Escape.
  document.addEventListener("click", function (e) {
    if (e.target.closest(".af-sidebar a")) {
      window.afNav.close();
    }
  });

  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") {
      window.afNav.close();
    }
  });
})();
