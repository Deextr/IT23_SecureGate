(() => {
    const root = document.querySelector("[data-admin-accounts]");
    if (!root) return;

    const actorUserId = root.getAttribute("data-actor-user-id") || "";
    const actorIsSuperadmin = root.hasAttribute("data-actor-is-superadmin");
    const superadminRoleKey = root.getAttribute("data-role-superadmin") || "Superadmin";

    const createModal = document.getElementById("sg-create-admin-modal");
    const openCreate = () => createModal?.showModal?.();
    const closeCreate = () => createModal?.close();

    if (root.hasAttribute("data-open-create-admin")) {
        openCreate();
        const url = new URL(window.location.href);
        if (url.searchParams.get("create") === "open") {
            url.searchParams.delete("create");
            window.history.replaceState(null, "", url);
        }
    }

    root.querySelectorAll("[data-create-admin-open]").forEach((btn) => {
        btn.addEventListener("click", openCreate);
    });
    document.querySelectorAll("[data-create-admin-close]").forEach((btn) => {
        btn.addEventListener("click", closeCreate);
    });

    createModal?.addEventListener("cancel", (event) => {
        event.preventDefault();
        closeCreate();
    });
    createModal?.addEventListener("click", (event) => {
        if (event.target === createModal) closeCreate();
    });

    const detailsModal = document.getElementById("sg-user-details-modal");
    let suppressRowOpenUntil = 0;

    const isDetailsOpen = () =>
        Boolean(detailsModal && (detailsModal.open || detailsModal.hasAttribute("open")));

    const closeDetails = () => {
        if (!detailsModal) return;
        suppressRowOpenUntil = Date.now() + 600;
        if (typeof detailsModal.close === "function") {
            detailsModal.close();
        }
        detailsModal.removeAttribute("open");
    };

    const bindDetailsCloseButton = (button) => {
        button.addEventListener("click", (event) => {
            event.preventDefault();
            event.stopPropagation();
            suppressRowOpenUntil = Date.now() + 600;
            window.setTimeout(closeDetails, 0);
        });
    };

    detailsModal?.querySelectorAll("[data-user-details-close]").forEach((button) => {
        if (button.dataset.closeBound === "1") return;
        button.dataset.closeBound = "1";
        bindDetailsCloseButton(button);
    });

    const detailsEls = {
        title: document.getElementById("sg-user-details-title"),
        avatar: document.getElementById("sg-user-details-avatar"),
        email: document.getElementById("sg-user-details-email"),
        badges: document.getElementById("sg-user-details-badges"),
        meta: document.getElementById("sg-user-details-meta"),
        roleSelect: document.getElementById("sg-user-details-new-role"),
        disableAction: document.getElementById("sg-user-details-disable-action"),
        reactivateAction: document.getElementById("sg-user-details-reactivate-action"),
        paneOverview: document.getElementById("sg-user-details-pane-overview"),
        paneManage: document.getElementById("sg-user-details-pane-manage"),
        tabOverview: document.getElementById("sg-user-details-tab-overview"),
        tabManage: document.getElementById("sg-user-details-tab-manage"),
        manageHint: document.getElementById("sg-user-details-manage-hint"),
        resetAction: document.getElementById("sg-user-details-reset-action"),
        roleForm: document.getElementById("sg-user-details-role-form"),
    };

    const tabButtons = detailsModal?.querySelectorAll("[data-user-details-tab]") ?? [];

    const activateTab = (name) => {
        const isOverview = name === "overview";
        tabButtons.forEach((btn) => {
            const active = btn.getAttribute("data-user-details-tab") === name;
            btn.classList.toggle("is-active", active);
            btn.setAttribute("aria-selected", active ? "true" : "false");
            btn.tabIndex = active ? 0 : -1;
        });
        if (detailsEls.paneOverview) {
            detailsEls.paneOverview.classList.toggle("is-active", isOverview);
            detailsEls.paneOverview.hidden = !isOverview;
        }
        if (detailsEls.paneManage) {
            detailsEls.paneManage.classList.toggle("is-active", !isOverview);
            detailsEls.paneManage.hidden = isOverview;
        }
    };

    tabButtons.forEach((btn) => {
        btn.addEventListener("click", () => {
            activateTab(btn.getAttribute("data-user-details-tab") || "overview");
        });
    });

    detailsModal?.querySelectorAll("[data-user-details-tab-jump]").forEach((btn) => {
        btn.addEventListener("click", () => activateTab(btn.getAttribute("data-user-details-tab-jump") || "manage"));
    });

    const makeBadge = (text, className) => {
        const span = document.createElement("span");
        span.className = `sg-badge ${className}`;
        span.textContent = text;
        return span;
    };

    const renderAvatar = (account) => {
        const wrap = detailsEls.avatar;
        if (!wrap) return;
        wrap.replaceChildren();
        if (account.photoUrl) {
            const img = document.createElement("img");
            img.className = "sg-user-details-avatar sg-user-details-avatar-img";
            img.src = account.photoUrl;
            img.width = 80;
            img.height = 80;
            img.alt = "";
            img.loading = "lazy";
            wrap.appendChild(img);
        } else {
            const initials = document.createElement("span");
            initials.className = "sg-user-details-avatar sg-user-details-avatar-initials";
            initials.textContent = account.initials || "?";
            wrap.appendChild(initials);
        }
    };

    const addMetaItem = (label, value) => {
        const li = document.createElement("li");
        li.className = "sg-user-details-stats-item";
        const dt = document.createElement("span");
        dt.className = "sg-user-details-stats-label";
        dt.textContent = label;
        const dd = document.createElement("span");
        dd.className = "sg-user-details-stats-value";
        dd.textContent = value;
        li.append(dt, dd);
        return li;
    };

    const renderMeta = (account) => {
        const list = detailsEls.meta;
        if (!list) return;
        list.replaceChildren();
        list.appendChild(addMetaItem("Created", account.createdAt || "—"));
        list.appendChild(
            addMetaItem("Last sign-in", account.lastSignInAt || "None recorded")
        );
        if (!account.isActive && account.deactivatedAt) {
            list.appendChild(addMetaItem("Deactivated", account.deactivatedAt));
        }
    };

    const setAccountIdOnForms = (accountId) => {
        detailsModal?.querySelectorAll('[data-user-details-field="id"]').forEach((input) => {
            input.value = accountId || "";
        });
    };

    const toggleStatusActions = (isActive) => {
        if (detailsEls.disableAction) detailsEls.disableAction.hidden = !isActive;
        if (detailsEls.reactivateAction) detailsEls.reactivateAction.hidden = isActive;
    };

    const isSuperadminAccount = (account) => account?.roleKey === superadminRoleKey;

    const applyAccountManagementVisibility = (account) => {
        if (!account) return;

        const self = actorUserId && account.id === actorUserId;
        const selfSuperadminReadOnly = self && isSuperadminAccount(account);
        const protectedSuperadminTarget = isSuperadminAccount(account) && !self;

        if (detailsEls.tabManage) {
            detailsEls.tabManage.hidden = selfSuperadminReadOnly;
        }
        if (detailsEls.manageHint) {
            detailsEls.manageHint.hidden = selfSuperadminReadOnly;
        }
        if (detailsEls.resetAction) {
            detailsEls.resetAction.hidden = protectedSuperadminTarget || (self && actorIsSuperadmin);
        }
        const lockProtectedTarget = protectedSuperadminTarget;
        if (detailsEls.roleForm) detailsEls.roleForm.hidden = lockProtectedTarget || self;
        if (detailsEls.disableAction) {
            detailsEls.disableAction.hidden = lockProtectedTarget || self || !account.isActive;
        }
        if (detailsEls.reactivateAction) {
            detailsEls.reactivateAction.hidden = lockProtectedTarget || self || account.isActive;
        }
    };

    const populateDetails = (account) => {
        if (!account) return;

        const displayName = account.displayName || "Account";
        if (detailsEls.title) detailsEls.title.textContent = displayName;
        if (detailsEls.email) detailsEls.email.textContent = account.email || "—";

        setAccountIdOnForms(account.id);
        toggleStatusActions(Boolean(account.isActive));
        applyAccountManagementVisibility(account);

        if (detailsEls.roleSelect && account.roleKey) {
            detailsEls.roleSelect.value = account.roleKey;
        } else if (detailsEls.roleSelect) {
            detailsEls.roleSelect.selectedIndex = 0;
        }

        renderAvatar(account);
        renderMeta(account);

        if (detailsEls.badges) {
            detailsEls.badges.replaceChildren();
            if (account.role) {
                detailsEls.badges.appendChild(makeBadge(account.role, "sg-badge-neutral"));
            }
            detailsEls.badges.appendChild(
                makeBadge(account.isActive ? "Active" : "Disabled", account.isActive ? "sg-badge-good" : "sg-badge-bad")
            );
            if (account.mustChangePassword) {
                detailsEls.badges.appendChild(makeBadge("Must change password", "sg-badge-warn"));
            }
        }
    };

    const openDetails = (account) => {
        if (!detailsModal) return;
        if (Date.now() < suppressRowOpenUntil) return;
        populateDetails(account);
        activateTab("overview");
        detailsModal.showModal();
    };

    const parseAccountRow = (row) => {
        const raw = row.getAttribute("data-account");
        if (!raw) return null;
        try {
            return JSON.parse(raw);
        } catch {
            return null;
        }
    };

    const findRowByAccountId = (accountId) =>
        root.querySelector(`.sg-accounts-row[data-account-id="${accountId}"]`);

    root.querySelectorAll(".sg-accounts-row[data-account]").forEach((row) => {
        const openFromRow = () => {
            const account = parseAccountRow(row);
            if (account) openDetails(account);
        };

        row.addEventListener("click", (event) => {
            if (isDetailsOpen()) return;
            if (Date.now() < suppressRowOpenUntil) return;
            if (event.target.closest("a, button")) return;
            openFromRow();
        });
        row.addEventListener("keydown", (event) => {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                openFromRow();
            }
        });
    });

    const openAccountId = root.getAttribute("data-open-account-id");
    if (openAccountId) {
        const row = findRowByAccountId(openAccountId);
        const account = row ? parseAccountRow(row) : null;
        if (account) {
            populateDetails(account);
            const selfSuperadmin =
                actorUserId && account.id === actorUserId && isSuperadminAccount(account);
            activateTab(selfSuperadmin ? "overview" : "manage");
            detailsModal?.showModal();
        }

        const url = new URL(window.location.href);
        if (url.searchParams.get("account") === openAccountId) {
            url.searchParams.delete("account");
            window.history.replaceState(null, "", url);
        }
    }

    detailsModal?.addEventListener("cancel", (event) => {
        event.preventDefault();
        closeDetails();
    });

    detailsModal?.addEventListener("click", (event) => {
        if (event.target === detailsModal) {
            event.preventDefault();
            closeDetails();
            return;
        }
        const card = detailsModal.querySelector(".sg-modal-card");
        if (card && !card.contains(event.target)) {
            closeDetails();
        }
    });

    root.querySelectorAll("[data-auto-submit]").forEach((control) => {
        control.addEventListener("change", () => {
            const form = control.closest("form");
            form?.requestSubmit?.() ?? form?.submit();
        });
    });

    const searchInput = root.querySelector(".sg-accounts-search-input");
    let searchTimer;
    searchInput?.addEventListener("input", () => {
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(() => {
            document.getElementById("sg-accounts-filter-form")?.requestSubmit();
        }, 350);
    });
})();
