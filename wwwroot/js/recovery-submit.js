(() => {
    const modal = document.getElementById("sg-recovery-submit-modal");
    if (!modal) return;

    const root = document.querySelector("[data-recovery-submit-root]");
    const searchUrl = root?.getAttribute("data-employee-search-url") || "/MfaResets/EmployeeSearch";

    const queryInput = modal.querySelector("[data-recovery-employee-query]");
    const idInput = modal.querySelector("[data-recovery-employee-id]");
    const resultsList = modal.querySelector("[data-recovery-employee-results]");
    const selectedPanel = modal.querySelector("[data-recovery-employee-selected]");
    const selectedName = modal.querySelector("[data-recovery-selected-name]");
    const selectedMeta = modal.querySelector("[data-recovery-selected-meta]");
    const clearBtn = modal.querySelector("[data-recovery-employee-clear]");
    const errorEl = modal.querySelector("[data-recovery-employee-error]");
    const otherWrap = modal.querySelector("[data-recovery-other-wrap]");
    const otherInput = modal.querySelector("[data-recovery-other-notes]");
    const otherCheck = modal.querySelector("[data-recovery-check-other]");
    const form = modal.querySelector("#sg-recovery-submit-form");

    let searchTimer;
    let activeController;

    const showPicker = () => {
        if (!queryInput || !selectedPanel) return;
        selectedPanel.hidden = true;
        queryInput.closest("[data-recovery-employee-picker]")?.querySelector("[data-recovery-employee-query]")
            ?.removeAttribute("hidden");
        queryInput.hidden = false;
        queryInput.focus();
    };

    const showSelected = (employee) => {
        if (!idInput || !selectedPanel || !queryInput || !selectedName || !selectedMeta) return;
        idInput.value = employee.id;
        selectedName.textContent = employee.displayName;
        selectedMeta.textContent = employee.email ? `${employee.email} · ID ${employee.id}` : `ID ${employee.id}`;
        queryInput.value = "";
        queryInput.hidden = true;
        resultsList && (resultsList.hidden = true);
        selectedPanel.hidden = false;
        setEmployeeError("");
    };

    const setEmployeeError = (message) => {
        if (!errorEl) return;
        if (message) {
            errorEl.textContent = message;
            errorEl.hidden = false;
        } else {
            errorEl.textContent = "";
            errorEl.hidden = true;
        }
    };

    const setResultsLoading = () => {
        if (!resultsList) return;
        resultsList.replaceChildren();
        const li = document.createElement("li");
        li.className = "sg-recovery-employee-results-status";
        li.textContent = "Searching…";
        resultsList.appendChild(li);
        resultsList.hidden = false;
        queryInput?.setAttribute("aria-expanded", "true");
    };

    const setResultsEmpty = (message) => {
        if (!resultsList) return;
        resultsList.replaceChildren();
        const li = document.createElement("li");
        li.className = "sg-recovery-employee-results-status";
        li.textContent = message;
        resultsList.appendChild(li);
        resultsList.hidden = false;
    };

    const renderResults = (items) => {
        if (!resultsList) return;
        resultsList.replaceChildren();
        items.forEach((item) => {
            const li = document.createElement("li");
            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "sg-recovery-employee-option";
            btn.setAttribute("role", "option");
            btn.innerHTML = `<span class="sg-recovery-employee-option-name"></span><span class="sg-recovery-employee-option-meta"></span>`;
            btn.querySelector(".sg-recovery-employee-option-name").textContent = item.displayName;
            btn.querySelector(".sg-recovery-employee-option-meta").textContent =
                item.email ? `${item.email} · ${item.id}` : item.id;
            btn.addEventListener("click", () => showSelected(item));
            li.appendChild(btn);
            resultsList.appendChild(li);
        });
        resultsList.hidden = items.length === 0;
        queryInput?.setAttribute("aria-expanded", items.length > 0 ? "true" : "false");
    };

    const runSearch = async (term) => {
        if (activeController) activeController.abort();
        if (!term || term.length < 2) {
            resultsList && (resultsList.hidden = true);
            return;
        }
        setResultsLoading();
        activeController = new AbortController();
        try {
            const url = new URL(searchUrl, window.location.origin);
            url.searchParams.set("q", term);
            const response = await fetch(url.toString(), {
                signal: activeController.signal,
                headers: { Accept: "application/json" },
            });
            if (!response.ok) {
                setResultsEmpty("Could not load employees. Try again.");
                return;
            }
            const data = await response.json();
            if (!Array.isArray(data) || data.length === 0) {
                setResultsEmpty("No matching employees. Refine your search.");
                return;
            }
            renderResults(data);
        } catch (error) {
            if (error.name === "AbortError") return;
            setResultsEmpty("Could not load employees. Try again.");
        }
    };

    queryInput?.addEventListener("input", () => {
        const term = queryInput.value.trim();
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(() => runSearch(term), 300);
    });

    clearBtn?.addEventListener("click", () => {
        if (idInput) idInput.value = "";
        showPicker();
        setEmployeeError("");
    });

    const syncOtherField = () => {
        const show = Boolean(otherCheck?.checked);
        if (otherWrap) otherWrap.hidden = !show;
        if (otherInput) {
            otherInput.required = show;
            if (!show) otherInput.value = "";
        }
    };

    modal.querySelectorAll("[data-recovery-check]").forEach((box) => {
        box.addEventListener("change", syncOtherField);
    });
    syncOtherField();

    form?.addEventListener("submit", (event) => {
        const checks = form.querySelectorAll("[data-recovery-check]:checked");
        if (checks.length === 0) {
            event.preventDefault();
            window.alert("Select at least one verification check you performed.");
            return;
        }
        if (!idInput?.value) {
            event.preventDefault();
            setEmployeeError("Select an employee from the search results.");
            showPicker();
        }
    });

    const resetForm = () => {
        if (idInput) idInput.value = "";
        if (queryInput) {
            queryInput.value = "";
            queryInput.hidden = false;
        }
        if (selectedPanel) selectedPanel.hidden = true;
        if (resultsList) {
            resultsList.hidden = true;
            resultsList.replaceChildren();
        }
        setEmployeeError("");
        syncOtherField();
    };

    modal.addEventListener("close", resetForm);
    window.initRecoverySubmitModal = {
        reset: resetForm,
    };
})();
