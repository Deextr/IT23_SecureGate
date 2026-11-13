(() => {
    const root = document.querySelector("[data-admin-settings]");
    if (!root) return;

    const form = document.getElementById("sg-settings-form");
    const navButtons = root.querySelectorAll("[data-settings-nav]");
    const jumpButtons = root.querySelectorAll("[data-settings-nav-jump]");
    const panes = root.querySelectorAll("[data-settings-pane]");
    const footer = root.querySelector("[data-settings-footer]");
    const dirtyNote = root.querySelector("[data-settings-dirty-note]");
    const editableSections = new Set(["signin", "authenticator", "recovery", "admin", "audit"]);

    const inputs = () => Array.from(form?.querySelectorAll("[data-settings-input]") ?? []);
    const snapshot = () => {
        inputs().forEach((input) => {
            input.dataset.initialValue = input.value;
        });
    };

    const isDirty = () =>
        inputs().some((input) => input.value !== (input.dataset.initialValue ?? input.value));

    const setDirtyUi = () => {
        const dirty = isDirty();
        if (dirtyNote) dirtyNote.hidden = !dirty;
        form?.classList.toggle("is-dirty", dirty);
    };

    const activateSection = (name) => {
        navButtons.forEach((btn) => {
            const active = btn.getAttribute("data-settings-nav") === name;
            btn.classList.toggle("is-active", active);
            if (active) btn.setAttribute("aria-current", "page");
            else btn.removeAttribute("aria-current");
        });
        panes.forEach((pane) => {
            const active = pane.getAttribute("data-settings-pane") === name;
            pane.hidden = !active;
        });
        if (footer) footer.hidden = !editableSections.has(name);

        const sectionField = document.getElementById("sg-settings-section");
        if (sectionField) sectionField.value = name;

        const url = new URL(window.location.href);
        if (name === "overview") url.searchParams.delete("section");
        else url.searchParams.set("section", name);
        window.history.replaceState({}, "", url);

        setDirtyUi();
    };

    const initial = (root.getAttribute("data-initial-section") || "overview").toLowerCase();
    activateSection(initial);

    navButtons.forEach((btn) => {
        btn.addEventListener("click", () => {
            activateSection(btn.getAttribute("data-settings-nav") || "overview");
        });
    });

    jumpButtons.forEach((btn) => {
        btn.addEventListener("click", () => {
            activateSection(btn.getAttribute("data-settings-nav-jump") || "overview");
        });
    });

    snapshot();

    inputs().forEach((input) => {
        input.addEventListener("input", setDirtyUi);
        input.addEventListener("change", setDirtyUi);
    });

    form?.addEventListener("submit", (event) => {
        if (window.jQuery && window.jQuery.fn.valid && !window.jQuery(form).valid()) {
            event.preventDefault();
            return;
        }
        const message =
            "Save these security settings? Changes apply to new sign-in and verification attempts and are recorded in the audit log.";
        if (!window.confirm(message)) event.preventDefault();
    });

    window.addEventListener("beforeunload", (event) => {
        if (!isDirty()) return;
        event.preventDefault();
        event.returnValue = "";
    });
})();
