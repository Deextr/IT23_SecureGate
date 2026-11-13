(() => {
    const root = document.querySelector(".sg-recovery-officer");
    if (!root) return;

    const submitModal = document.getElementById("sg-recovery-submit-modal");
    const openSubmit = () => submitModal?.showModal?.();
    const closeSubmit = () => submitModal?.close();

    if (root.hasAttribute("data-open-submit-request")) {
        openSubmit();
        const url = new URL(window.location.href);
        if (url.searchParams.get("submit") === "open") {
            url.searchParams.delete("submit");
            window.history.replaceState(null, "", url);
        }
    }

    root.querySelectorAll("[data-recovery-submit-open]").forEach((btn) => {
        btn.addEventListener("click", openSubmit);
    });
    document.querySelectorAll("[data-recovery-submit-close]").forEach((btn) => {
        btn.addEventListener("click", closeSubmit);
    });
    submitModal?.addEventListener("cancel", (event) => {
        event.preventDefault();
        closeSubmit();
    });
    submitModal?.addEventListener("click", (event) => {
        if (event.target === submitModal) closeSubmit();
    });
})();
