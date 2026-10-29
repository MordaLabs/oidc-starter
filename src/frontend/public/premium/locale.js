// Only an explicit language choice updates the shared Public Demo preference.
document.querySelectorAll('[data-locale]').forEach((link) => {
  link.addEventListener('click', () => {
    try {
      localStorage.setItem('oidc-starter.public-demo.locale', link.dataset.locale);
    } catch {
      // Navigation still works when storage is unavailable.
    }
  });
});
