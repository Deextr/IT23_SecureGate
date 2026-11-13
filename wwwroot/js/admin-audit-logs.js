(() => {
    const root = document.querySelector("[data-admin-audit-logs]");
    if (!root) return;

    const filterForm = document.getElementById("sg-audit-filter-form");
    const searchInput = filterForm?.querySelector("#search");
    const dateFrom = filterForm?.querySelector('[data-date-range="from"]');
    const dateTo = filterForm?.querySelector('[data-date-range="to"]');

    let searchTimer = 0;
    searchInput?.addEventListener("input", () => {
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(() => filterForm?.requestSubmit?.(), 350);
    });

    const validateDateRange = () => {
        if (!dateFrom?.value || !dateTo?.value) return true;
        return dateFrom.value <= dateTo.value;
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

    const detailModal = document.getElementById("sg-audit-detail-modal");
    let suppressRowOpenUntil = 0;

    const closeDetail = () => {
        if (!detailModal) return;
        suppressRowOpenUntil = Date.now() + 600;
        detailModal.close?.();
        detailModal.removeAttribute("open");
    };

    document.querySelectorAll("[data-audit-detail-close]").forEach((btn) => {
        btn.addEventListener("click", (event) => {
            event.preventDefault();
            closeDetail();
        });
    });

    detailModal?.addEventListener("cancel", (event) => {
        event.preventDefault();
        closeDetail();
    });
    detailModal?.addEventListener("click", (event) => {
        if (event.target === detailModal) closeDetail();
    });

    const els = {
        title: document.getElementById("sg-audit-detail-title"),
        subtitle: document.getElementById("sg-audit-detail-subtitle"),
        timestamp: document.getElementById("sg-audit-detail-timestamp"),
        outcome: document.getElementById("sg-audit-detail-outcome"),
        severity: document.getElementById("sg-audit-detail-severity"),
        module: document.getElementById("sg-audit-detail-module"),
        account: document.getElementById("sg-audit-detail-account"),
        subjectRow: document.getElementById("sg-audit-detail-subject-row"),
        subject: document.getElementById("sg-audit-detail-subject"),
        ip: document.getElementById("sg-audit-detail-ip"),
        correlationRow: document.getElementById("sg-audit-detail-correlation-row"),
        correlation: document.getElementById("sg-audit-detail-correlation"),
        description: document.getElementById("sg-audit-detail-description"),
        ids: document.getElementById("sg-audit-detail-ids"),
    };

    const parseRow = (row) => {
        const raw = row.getAttribute("data-audit-event");
        if (!raw) return null;
        try {
            return JSON.parse(raw);
        } catch {
            return null;
        }
    };

    const formatParty = (line, role) => {
        if (!line) return "—";
        return role ? `${line} · ${role}` : line;
    };

    const populateDetail = (event) => {
        if (!event) return;
        if (els.title) els.title.textContent = event.actionLabel || "Audit event";
        if (els.subtitle) {
            els.subtitle.textContent = event.module
                ? `${event.module} · Event #${event.id}`
                : `Event #${event.id}`;
        }
        if (els.timestamp) els.timestamp.textContent = event.timestamp || "—";
        if (els.outcome) {
            els.outcome.innerHTML = "";
            const badge = document.createElement("span");
            badge.className = `sg-badge ${event.outcomeBadge || "sg-badge-neutral"}`;
            badge.textContent = event.outcome || "—";
            els.outcome.appendChild(badge);
        }
        if (els.severity) els.severity.textContent = event.severity || "—";
        if (els.module) els.module.textContent = event.module || "—";
        if (els.account) {
            els.account.innerHTML = "";
            const primary = document.createElement("div");
            primary.textContent = formatParty(event.accountLine, event.actorRole);
            els.account.appendChild(primary);
            if (event.targetLine && event.targetLine !== "—" && event.targetLine !== event.accountLine) {
                const target = document.createElement("div");
                target.className = "sg-accounts-email";
                target.textContent = `Target: ${formatParty(event.targetLine, event.targetRole)}`;
                els.account.appendChild(target);
            }
        }
        if (els.subjectRow && els.subject) {
            const hasSubject = Boolean(event.subjectIdentifier);
            els.subjectRow.hidden = !hasSubject;
            els.subject.textContent = hasSubject ? event.subjectIdentifier : "";
        }
        if (els.ip) els.ip.textContent = event.ipAddress || "None";
        if (els.correlationRow && els.correlation) {
            const hasCorrelation = Boolean(event.correlationId);
            els.correlationRow.hidden = !hasCorrelation;
            els.correlation.textContent = hasCorrelation ? event.correlationId : "";
        }
        if (els.description) els.description.textContent = event.description || "—";
        if (els.ids) {
            const idParts = [];
            if (event.actorUserId) idParts.push(`Actor ID: ${event.actorUserId}`);
            if (event.targetUserId) idParts.push(`Target ID: ${event.targetUserId}`);
            const showIds = idParts.length > 0;
            els.ids.hidden = !showIds;
            els.ids.textContent = showIds ? idParts.join(" · ") : "";
        }
    };

    const openDetailForRow = (row) => {
        if (Date.now() < suppressRowOpenUntil) return;
        const event = parseRow(row);
        if (!event || !detailModal) return;
        populateDetail(event);
        detailModal.showModal?.();
    };

    root.querySelectorAll(".sg-accounts-row[data-audit-event]").forEach((row) => {
        row.addEventListener("click", () => openDetailForRow(row));
        row.addEventListener("keydown", (keyEvent) => {
            if (keyEvent.key === "Enter" || keyEvent.key === " ") {
                keyEvent.preventDefault();
                openDetailForRow(row);
            }
        });
    });
})();
