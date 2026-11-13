(() => {
    const root = document.querySelector("[data-security-operations]");
    if (!root) return;

    const filterForm = document.getElementById("sg-secops-filter-form");
    const searchInput = filterForm?.querySelector(".sg-accounts-search-input");
    let searchTimer = 0;
    searchInput?.addEventListener("input", () => {
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(() => filterForm?.requestSubmit?.(), 350);
    });

    const validateDateRange = () => {
        const from = filterForm?.querySelector('[data-date-range="from"]');
        const to = filterForm?.querySelector('[data-date-range="to"]');
        if (!from?.value || !to?.value) return true;
        return from.value <= to.value;
    };

    const submitIfValidDates = () => {
        if (!validateDateRange()) {
            window.alert("To date must be on or after the from date.");
            return;
        }
        filterForm?.requestSubmit?.();
    };

    filterForm?.querySelectorAll("[data-date-range]").forEach((input) => {
        input.addEventListener("change", submitIfValidDates);
    });

    filterForm?.querySelectorAll("[data-auto-submit]").forEach((control) => {
        control.addEventListener("change", () => filterForm?.requestSubmit?.());
    });

    const investigationLabel = (status) => {
        if (status === "InProgress") return "In progress";
        if (status === "Closed") return "Closed";
        if (status === "Open") return "Open";
        return "—";
    };

    const parseRow = (row) => {
        const raw = row.getAttribute("data-secops-row");
        if (!raw) return null;
        try {
            return JSON.parse(raw);
        } catch {
            return null;
        }
    };

    let suppressRowOpenUntil = 0;
    const alertModal = document.getElementById("sg-secops-alert-modal");
    const investigateModal = document.getElementById("sg-secops-investigate-modal");
    const resolveForm = document.getElementById("sg-secops-resolve-form");
    const resolveIdInput = document.getElementById("sg-secops-resolve-id");

    const bindDialog = (modal, closeSelector, onClose) => {
        if (!modal) return;
        const close = () => {
            suppressRowOpenUntil = Date.now() + 500;
            onClose?.();
            modal.close?.();
            modal.removeAttribute("open");
        };
        document.querySelectorAll(closeSelector).forEach((btn) => {
            btn.addEventListener("click", (event) => {
                event.preventDefault();
                close();
            });
        });
        modal.addEventListener("cancel", (event) => {
            event.preventDefault();
            close();
        });
        modal.addEventListener("click", (event) => {
            if (event.target === modal) close();
        });
    };

    const openAlertDetail = (data) => {
        if (!alertModal || !data) return;
        document.getElementById("sg-secops-alert-title").textContent = data.title || "Alert";
        document.getElementById("sg-secops-alert-subtitle").textContent = `Alert #${data.alertId}`;
        document.getElementById("sg-secops-alert-created").textContent = data.createdAt || "—";
        document.getElementById("sg-secops-alert-severity").textContent = data.severity || "—";
        document.getElementById("sg-secops-alert-status").textContent = data.status || "—";
        document.getElementById("sg-secops-alert-rule").textContent = data.ruleKey || "—";

        const ipRow = document.getElementById("sg-secops-alert-ip-row");
        if (data.sourceIp) {
            ipRow.hidden = false;
            document.getElementById("sg-secops-alert-ip").textContent = data.sourceIp;
        } else {
            ipRow.hidden = true;
        }

        const invRow = document.getElementById("sg-secops-alert-inv-row");
        if (data.investigationStatus) {
            invRow.hidden = false;
            document.getElementById("sg-secops-alert-inv").textContent = investigationLabel(data.investigationStatus);
        } else {
            invRow.hidden = true;
        }

        if (typeof alertModal.showModal === "function") alertModal.showModal();
    };

    const openInvestigate = (data) => {
        if (!investigateModal || !data) return;
        if (data.isResolved) {
            window.alert("This alert is resolved. Investigations cannot be opened or changed.");
            return;
        }
        document.getElementById("sg-secops-investigate-title").textContent = "Investigate alert";
        document.getElementById("sg-secops-investigate-subtitle").textContent = `Alert #${data.alertId}`;
        document.getElementById("sg-secops-investigate-alert-line").textContent = `${data.severity} · ${data.title}`;
        document.getElementById("sg-secops-investigate-alert-id").value = data.alertId;

        const statusSelect = document.getElementById("investigateStatus");
        const notesInput = document.getElementById("investigateNotes");
        if (statusSelect) {
            statusSelect.value = data.investigationStatus || "Open";
        }
        if (notesInput) {
            notesInput.value = data.investigationNotes || "";
        }

        if (typeof investigateModal.showModal === "function") investigateModal.showModal();
    };

    bindDialog(alertModal, "[data-secops-alert-close]");
    bindDialog(investigateModal, "[data-secops-investigate-close]", () => closeAllMenus());

    const closeAllMenus = () => {
        root.querySelectorAll("[data-row-menu].is-open").forEach((menu) => {
            menu.classList.remove("is-open");
            const trigger = menu.querySelector(".sg-row-menu-trigger");
            const panel = menu.querySelector(".sg-row-menu-panel");
            if (trigger) trigger.setAttribute("aria-expanded", "false");
            if (panel) panel.hidden = true;
        });
    };

    const toggleMenu = (menu, open) => {
        const trigger = menu.querySelector(".sg-row-menu-trigger");
        const panel = menu.querySelector(".sg-row-menu-panel");
        if (!trigger || !panel) return;
        closeAllMenus();
        if (open) {
            menu.classList.add("is-open");
            trigger.setAttribute("aria-expanded", "true");
            panel.hidden = false;
        }
    };

    root.querySelectorAll("[data-row-menu]").forEach((menu) => {
        const trigger = menu.querySelector(".sg-row-menu-trigger");
        trigger?.addEventListener("click", (event) => {
            event.stopPropagation();
            const willOpen = !menu.classList.contains("is-open");
            toggleMenu(menu, willOpen);
        });

        menu.querySelectorAll("[data-secops-resolve]").forEach((item) => {
            item.addEventListener("click", (event) => {
                event.stopPropagation();
                const row = menu.closest("[data-secops-row]");
                const data = row ? parseRow(row) : null;
                if (!data?.alertId || !resolveForm || !resolveIdInput) return;
                closeAllMenus();
                resolveIdInput.value = String(data.alertId);
                resolveForm.requestSubmit();
            });
        });

        menu.querySelectorAll("[data-secops-investigate]").forEach((item) => {
            item.addEventListener("click", (event) => {
                event.stopPropagation();
                const row = menu.closest("[data-secops-row]");
                const data = row ? parseRow(row) : null;
                closeAllMenus();
                openInvestigate(data);
            });
        });
    });

    document.addEventListener("click", (event) => {
        if (!event.target.closest("[data-row-menu]")) closeAllMenus();
    });
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") closeAllMenus();
    });

    root.querySelectorAll("[data-secops-row]").forEach((row) => {
        row.addEventListener("click", (event) => {
            if (Date.now() < suppressRowOpenUntil) return;
            if (event.target.closest(".sg-secops-col-menu, .sg-row-menu, [data-row-menu], button")) return;
            openAlertDetail(parseRow(row));
        });
        row.addEventListener("keydown", (event) => {
            if (event.key !== "Enter" && event.key !== " ") return;
            if (event.target.closest(".sg-secops-col-menu, .sg-row-menu, [data-row-menu], button")) return;
            event.preventDefault();
            openAlertDetail(parseRow(row));
        });
    });
})();
