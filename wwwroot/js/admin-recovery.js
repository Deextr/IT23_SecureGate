(() => {
    const root = document.querySelector("[data-admin-recovery]");
    if (!root) return;

    const canReview = root.getAttribute("data-can-review") === "true";

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

    const filterForm = document.getElementById("sg-recovery-filter-form");
    const dateFrom = filterForm?.querySelector('[data-date-range="from"]');
    const dateTo = filterForm?.querySelector('[data-date-range="to"]');

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

    const rejectReason = document.getElementById("sg-recovery-reject-reason");
    const rejectOtherWrap = document.getElementById("sg-recovery-reject-other-wrap");
    const rejectOther = document.getElementById("sg-recovery-reject-other");
    const syncRejectOther = () => {
        const isOther = rejectReason?.value === "Other";
        if (rejectOtherWrap) rejectOtherWrap.hidden = !isOther;
        if (rejectOther) {
            rejectOther.required = isOther;
            if (!isOther) rejectOther.value = "";
        }
    };
    rejectReason?.addEventListener("change", syncRejectOther);
    syncRejectOther();

    const reviewModal = document.getElementById("sg-recovery-review-modal");
    let suppressRowOpenUntil = 0;

    const closeReview = () => {
        if (!reviewModal) return;
        suppressRowOpenUntil = Date.now() + 600;
        reviewModal.close?.();
        reviewModal.removeAttribute("open");
    };

    document.querySelectorAll("[data-recovery-review-close]").forEach((btn) => {
        btn.addEventListener("click", (event) => {
            event.preventDefault();
            closeReview();
        });
    });

    const els = {
        title: document.getElementById("sg-recovery-review-title"),
        subtitle: document.getElementById("sg-recovery-review-subtitle"),
        status: document.getElementById("sg-recovery-review-status"),
        created: document.getElementById("sg-recovery-review-created"),
        reviewedRow: document.getElementById("sg-recovery-review-reviewed-row"),
        reviewed: document.getElementById("sg-recovery-review-reviewed"),
        method: document.getElementById("sg-recovery-review-method"),
        requester: document.getElementById("sg-recovery-review-requester"),
        notes: document.getElementById("sg-recovery-review-notes"),
        decisionWrap: document.getElementById("sg-recovery-review-decision-wrap"),
        decision: document.getElementById("sg-recovery-review-decision"),
        actions: document.getElementById("sg-recovery-review-actions"),
        closedNote: document.getElementById("sg-recovery-review-closed-note"),
    };

    const PENDING_STATUS = "Pending";

    const requestIsPending = (request) =>
        request?.status === PENDING_STATUS;

    const setReviewActionsState = (enabled) => {
        if (!els.actions) return;
        els.actions.hidden = !enabled;
        if (enabled) {
            els.actions.removeAttribute("inert");
        } else {
            els.actions.setAttribute("inert", "");
        }
        els.actions.querySelectorAll("input, select, textarea, button").forEach((control) => {
            control.disabled = !enabled;
            if (!enabled && control.tagName === "INPUT" && control.type !== "hidden") {
                control.value = "";
            }
        });
        const approveConfirm = document.getElementById("sg-recovery-approve-confirm");
        if (approveConfirm) {
            approveConfirm.required = enabled;
        }
        if (rejectReason) {
            rejectReason.required = enabled;
            rejectReason.disabled = !enabled;
        }
        if (rejectOther) {
            rejectOther.required = enabled && rejectReason?.value === "Other";
            rejectOther.disabled = !enabled;
        }
    };

    const parseRequestRow = (row) => {
        const raw = row.getAttribute("data-request");
        if (!raw) return null;
        try {
            return JSON.parse(raw);
        } catch {
            return null;
        }
    };

    const setHiddenIds = (requestId) => {
        reviewModal?.querySelectorAll("[data-recovery-field=\"id\"]").forEach((input) => {
            input.value = String(requestId);
        });
    };

    const parseChecklistFromNotes = (notes) => {
        if (!notes || typeof notes !== "string") return [];
        let text = notes.trim();
        const prefix = "Verification performed:";
        if (text.toLowerCase().startsWith(prefix.toLowerCase())) {
            text = text.slice(prefix.length).trimStart();
        }
        if (/[\r\n]/.test(text)) {
            return text
                .split(/\r?\n/)
                .map((line) => line.trim().replace(/^\u2022\s*/, ""))
                .filter(Boolean);
        }
        return text
            .split("\u2022")
            .map((part) => part.trim())
            .filter(Boolean);
    };

    const renderVerificationChecklist = (request) => {
        if (!els.notes) return;
        const items = Array.isArray(request.checklistItems) && request.checklistItems.length
            ? request.checklistItems
            : parseChecklistFromNotes(request.notes);
        els.notes.innerHTML = "";
        if (!items.length) {
            els.notes.textContent = "—";
            return;
        }
        const ul = document.createElement("ul");
        ul.className = "sg-recovery-checklist sg-recovery-checklist--read";
        items.forEach((label) => {
            const li = document.createElement("li");
            li.className = "sg-recovery-checklist-read-item";
            li.textContent = label;
            ul.appendChild(li);
        });
        els.notes.appendChild(ul);
    };

    const populateReview = (request) => {
        if (!request) return;
        const pending = requestIsPending(request);
        const showDecisionActions = canReview && pending && request.canDecide === true;
        if (els.title) {
            els.title.textContent = pending ? "Review request" : "View request";
        }
        if (els.subtitle) {
            els.subtitle.textContent = `${request.targetName} (${request.targetEmail}) · ${request.targetRole}`;
        }
        if (els.status) {
            els.status.innerHTML = "";
            const badge = document.createElement("span");
            badge.className = `sg-badge ${request.statusBadge}`;
            badge.textContent = request.statusLabel;
            els.status.appendChild(badge);
        }
        if (els.created) els.created.textContent = request.createdAt;
        if (request.reviewedAt && els.reviewedRow && els.reviewed) {
            els.reviewedRow.hidden = false;
            els.reviewed.textContent = request.reviewedAt;
        } else if (els.reviewedRow) {
            els.reviewedRow.hidden = true;
        }
        if (els.method) els.method.textContent = request.method;
        if (els.requester) {
            els.requester.textContent = request.requesterEmail
                ? `${request.requesterName} (${request.requesterEmail})`
                : request.requesterName;
        }
        renderVerificationChecklist(request);
        if (els.decisionWrap && els.decision) {
            const hasDecision = Boolean(request.decisionNotes);
            els.decisionWrap.hidden = !hasDecision;
            els.decision.textContent = hasDecision ? request.decisionNotes : "";
        }
        if (els.closedNote) {
            els.closedNote.hidden = !(canReview && !pending);
        }
        setReviewActionsState(showDecisionActions);
        setHiddenIds(request.id);
        if (rejectReason) rejectReason.selectedIndex = 0;
        syncRejectOther();
    };

    const openReviewForRow = (row) => {
        if (!canReview) return;
        const request = parseRequestRow(row);
        if (!request || !reviewModal) return;
        populateReview(request);
        reviewModal.showModal();
    };

    const findRowByRequestId = (id) =>
        root.querySelector(`[data-request-id="${CSS.escape(String(id))}"]`);

    root.querySelectorAll(".sg-accounts-row").forEach((row) => {
        row.addEventListener("click", () => {
            if (Date.now() < suppressRowOpenUntil) return;
            openReviewForRow(row);
        });
        row.addEventListener("keydown", (event) => {
            if (event.key !== "Enter" && event.key !== " ") return;
            event.preventDefault();
            openReviewForRow(row);
        });
    });

    const openRequestId = root.getAttribute("data-open-request-id");
    if (openRequestId && canReview) {
        const row = findRowByRequestId(openRequestId);
        if (row) openReviewForRow(row);
        const url = new URL(window.location.href);
        if (url.searchParams.get("request") === openRequestId) {
            url.searchParams.delete("request");
            window.history.replaceState(null, "", url);
        }
    }

    reviewModal?.addEventListener("cancel", (event) => {
        event.preventDefault();
        closeReview();
    });
    reviewModal?.addEventListener("click", (event) => {
        if (event.target === reviewModal) {
            closeReview();
            return;
        }
        const card = reviewModal.querySelector(".sg-modal-card");
        if (card && !card.contains(event.target)) closeReview();
    });

    filterForm?.querySelectorAll("[data-auto-submit]").forEach((control) => {
        control.addEventListener("change", () => {
            filterForm.requestSubmit?.() ?? filterForm.submit();
        });
    });

    const searchInput = root.querySelector(".sg-accounts-search-input");
    let searchTimer;
    searchInput?.addEventListener("input", () => {
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(() => {
            filterForm?.requestSubmit?.();
        }, 350);
    });
})();
