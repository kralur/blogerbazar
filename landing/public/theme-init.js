// Runs before the first paint (the CSP allows no inline scripts): the saved theme, or the system one.
(function () {
  var choice = "system";
  try {
    choice = localStorage.getItem("bloggerbazar.landing.theme") || "system";
  } catch (error) {}
  var dark = choice === "dark" || (choice !== "light" && window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches);
  document.documentElement.dataset.theme = dark ? "dark" : "light";
})();
