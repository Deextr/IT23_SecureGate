(function () {
    var key = "sg-theme";
    var stored = localStorage.getItem(key);
    var theme = stored === "dark" || stored === "light" ? stored : "light";
    document.documentElement.setAttribute("data-theme", theme);
})();
