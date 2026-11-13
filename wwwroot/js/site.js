(() => {
    const themeKey = "sg-theme";
    const applyTheme = (theme) => {
        if (theme !== "light" && theme !== "dark") theme = "light";
        document.documentElement.setAttribute("data-theme", theme);
        localStorage.setItem(themeKey, theme);
        const isDark = theme === "dark";
        document.querySelectorAll("[data-theme-toggle]").forEach((btn) => {
            const switchLabel = isDark
                ? "Appearance: dark mode. Activate to switch to light mode."
                : "Appearance: light mode. Activate to switch to dark mode.";
            const title = isDark ? "Switch to light mode" : "Switch to dark mode";
            btn.setAttribute("aria-checked", isDark ? "true" : "false");
            btn.setAttribute("aria-label", switchLabel);
            btn.setAttribute("title", title);
        });
    };

    applyTheme(document.documentElement.getAttribute("data-theme") || "light");

    document.querySelectorAll("[data-theme-toggle]").forEach((btn) => {
        btn.addEventListener("click", () => {
            const current = document.documentElement.getAttribute("data-theme") || "light";
            applyTheme(current === "light" ? "dark" : "light");
        });
    });

    document.querySelectorAll("[data-profile-menu]").forEach((menu) => {
        const toggle = menu.querySelector("[data-profile-toggle]");
        const panel = menu.querySelector(".sg-profile-dropdown");
        if (!toggle || !panel) return;

        const setOpen = (open) => {
            menu.classList.toggle("is-open", open);
            toggle.setAttribute("aria-expanded", open ? "true" : "false");
            panel.hidden = !open;
        };

        toggle.addEventListener("click", (event) => {
            event.stopPropagation();
            setOpen(panel.hidden);
        });

        toggle.addEventListener("keydown", (event) => {
            if (event.key === "Escape") setOpen(false);
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                setOpen(panel.hidden);
            }
        });

        panel.querySelectorAll("button[role='menuitem']").forEach((item) => {
            if (item.hasAttribute("data-profile-open")) return;
            item.addEventListener("click", () => setOpen(false));
        });

        document.addEventListener("click", (event) => {
            if (!menu.contains(event.target)) setOpen(false);
        });

        document.addEventListener("keydown", (event) => {
            if (event.key === "Escape") setOpen(false);
        });
    });

    const profileModal = document.getElementById("sg-profile-modal");
    const openProfileModal = () => {
        if (!profileModal || typeof profileModal.showModal !== "function") return;
        profileModal.showModal();
    };
    const closeProfileModal = () => profileModal?.close();

    const shouldOpenProfile =
        document.querySelector("[data-profile-open-on-load]") ||
        new URLSearchParams(window.location.search).get("profile") === "open";
    if (shouldOpenProfile) {
        openProfileModal();
        const url = new URL(window.location.href);
        if (url.searchParams.get("profile") === "open") {
            url.searchParams.delete("profile");
            window.history.replaceState(null, "", url);
        }
    }

    document.querySelectorAll("[data-profile-open]").forEach((button) => {
        button.addEventListener("click", () => {
            document.querySelectorAll("[data-profile-menu]").forEach((menu) => {
                menu.classList.remove("is-open");
                const toggle = menu.querySelector("[data-profile-toggle]");
                const panel = menu.querySelector(".sg-profile-dropdown");
                toggle?.setAttribute("aria-expanded", "false");
                if (panel) panel.hidden = true;
            });
            openProfileModal();
        });
    });

    document.querySelectorAll("[data-profile-close]").forEach((button) => {
        button.addEventListener("click", closeProfileModal);
    });

    profileModal?.addEventListener("cancel", (event) => {
        event.preventDefault();
        closeProfileModal();
    });

    profileModal?.addEventListener("click", (event) => {
        if (event.target === profileModal) closeProfileModal();
    });

    const profileFile = document.querySelector("[data-profile-file]");
    const previewWrap = document.querySelector(".sg-profile-modal-preview--single");
    const displayNameInput = document.getElementById("DisplayName");
    const asideName = document.getElementById("sg-profile-aside-name");
    const uploadTrigger = document.querySelector("[data-profile-upload-trigger]");
    const removeTrigger = document.querySelector("[data-profile-remove-trigger]");

    const setRemoveVisible = (visible) => {
        if (!removeTrigger) return;
        removeTrigger.hidden = !visible;
    };

    const initialsFromName = () => {
        const value = displayNameInput?.value.trim() || "";
        const parts = value.split(/\s+/).filter(Boolean);
        if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
        if (parts.length > 1) return `${parts[0].charAt(0)}${parts[parts.length - 1].charAt(0)}`.toUpperCase();
        const fallback = previewWrap?.closest("[data-initials]")?.getAttribute("data-initials");
        return fallback || "?";
    };

    const clearPreviewFace = () => {
        previewWrap?.querySelectorAll(".sg-profile-preview-face").forEach((node) => node.remove());
    };

    const showPreviewPhoto = (url) => {
        if (!previewWrap) return;
        clearPreviewFace();
        const img = document.createElement("img");
        img.id = "sg-profile-preview-img";
        img.className = "sg-avatar sg-avatar-img sg-avatar-xl sg-profile-preview-face";
        img.width = 112;
        img.height = 112;
        img.alt = "";
        img.src = url;
        previewWrap.appendChild(img);
    };

    const showPreviewInitials = () => {
        if (!previewWrap) return;
        clearPreviewFace();
        const span = document.createElement("span");
        span.id = "sg-profile-preview-initials";
        span.className = "sg-avatar sg-avatar-initials sg-avatar-xl sg-profile-preview-face";
        span.textContent = initialsFromName();
        previewWrap.appendChild(span);
    };

    const hasPreviewPhoto = () => Boolean(previewWrap?.querySelector("#sg-profile-preview-img"));

    displayNameInput?.addEventListener("input", () => {
        if (asideName) asideName.textContent = displayNameInput.value.trim() || asideName.textContent;
        const remove = document.querySelector("[data-profile-remove]");
        if (remove?.checked) return;
        if (hasPreviewPhoto()) return;
        const initials = previewWrap?.querySelector("#sg-profile-preview-initials");
        if (initials) initials.textContent = initialsFromName();
    });

    uploadTrigger?.addEventListener("click", () => profileFile?.click());

    profileFile?.addEventListener("change", () => {
        const file = profileFile.files?.[0];
        const remove = document.querySelector("[data-profile-remove]");
        if (remove) remove.checked = false;
        if (!file) return;
        if (file.size > 5 * 1024 * 1024) {
            window.alert("Profile photo must be 5 MB or smaller.");
            profileFile.value = "";
            return;
        }
        showPreviewPhoto(URL.createObjectURL(file));
        setRemoveVisible(true);
    });

    removeTrigger?.addEventListener("click", () => {
        const remove = document.querySelector("[data-profile-remove]");
        if (remove) remove.checked = true;
        showPreviewInitials();
        if (profileFile) profileFile.value = "";
        setRemoveVisible(false);
    });

    document.querySelector("[data-profile-remove]")?.addEventListener("change", (event) => {
        if (!event.target.checked) return;
        showPreviewInitials();
        if (profileFile) profileFile.value = "";
        setRemoveVisible(false);
    });

    const sidebar = document.querySelector("[data-sidebar]");
    const toggle = document.querySelector("[data-nav-toggle]");
    const scrim = document.querySelector("[data-nav-scrim]");

    const setOpen = (open) => {
        document.body.classList.toggle("sg-nav-open", open);
        if (toggle) toggle.setAttribute("aria-expanded", open ? "true" : "false");
        if (scrim) scrim.hidden = !open;
    };

    toggle?.addEventListener("click", () => setOpen(!document.body.classList.contains("sg-nav-open")));
    scrim?.addEventListener("click", () => setOpen(false));
    document.addEventListener("keydown", (event) => {
        if (event.key === "Escape") setOpen(false);
    });

    const current = location.pathname.replace(/\/$/, "") || "/";
    document.querySelectorAll(".sg-nav-link").forEach((link) => {
        const href = (link.getAttribute("href") || "").split("?")[0].replace(/\/$/, "");
        if (!href) return;
        if (current === href || (href !== "" && current.startsWith(href))) {
            link.classList.add("is-active");
            link.setAttribute("aria-current", "page");
        }
    });

    document.querySelectorAll("[data-toggle-password]").forEach((button) => {
        const fieldId = button.getAttribute("data-toggle-password");
        const input = fieldId ? document.getElementById(fieldId) : null;
        const showIcon = button.querySelector("[data-icon-show]");
        const hideIcon = button.querySelector("[data-icon-hide]");

        const setVisibility = (revealed) => {
            if (!input) return;
            input.type = revealed ? "text" : "password";
            button.setAttribute("aria-pressed", revealed ? "true" : "false");
            const label = revealed ? "Hide password" : "Show password";
            button.setAttribute("aria-label", label);
            button.setAttribute("title", label);
            if (showIcon) showIcon.hidden = revealed;
            if (hideIcon) hideIcon.hidden = !revealed;
        };

        button.addEventListener("click", () => {
            setVisibility(input?.type === "password");
        });
    });

    document.querySelectorAll("form").forEach((form) => {
        form.addEventListener("submit", () => {
            if (window.jQuery && window.jQuery.fn.valid && !window.jQuery(form).valid()) return;
            const button = form.querySelector("[data-submit]");
            if (!button) return;
            button.disabled = true;
            button.dataset.label = button.textContent;
            button.textContent = "Working…";
        });
    });

    const bindOtp = (group) => {
        const boxes = Array.from(group.querySelectorAll(".sg-otp__box"));
        const targetId = group.getAttribute("data-otp-target") || "Code";
        const hidden = document.getElementById(targetId);
        if (boxes.length !== 6) return;

        const digitsOnly = (value) => (value || "").replace(/\D/g, "");

        const sync = () => {
            if (hidden) hidden.value = boxes.map((box) => digitsOnly(box.value).slice(0, 1)).join("");
        };

        const setDigits = (raw) => {
            const chars = digitsOnly(raw).slice(0, 6).split("");
            boxes.forEach((box, index) => {
                box.value = chars[index] || "";
            });
            sync();
            return chars.length;
        };

        boxes.forEach((box, index) => {
            box.addEventListener("input", (event) => {
                const value = digitsOnly(event.target.value);
                if (value.length > 1) {
                    const filled = setDigits(
                        boxes.slice(0, index).map((item) => item.value).join("") + value
                    );
                    boxes[Math.min(filled, boxes.length) - 1]?.focus();
                    return;
                }
                event.target.value = value.slice(-1);
                sync();
                if (value && index < boxes.length - 1) boxes[index + 1].focus();
            });

            box.addEventListener("keydown", (event) => {
                if (event.key === "Backspace") {
                    if (box.value) {
                        box.value = "";
                        sync();
                        event.preventDefault();
                        return;
                    }
                    if (index > 0) {
                        boxes[index - 1].value = "";
                        boxes[index - 1].focus();
                        sync();
                        event.preventDefault();
                    }
                    return;
                }
                if (event.key === "ArrowLeft" && index > 0) {
                    event.preventDefault();
                    boxes[index - 1].focus();
                }
                if (event.key === "ArrowRight" && index < boxes.length - 1) {
                    event.preventDefault();
                    boxes[index + 1].focus();
                }
            });

            box.addEventListener("paste", (event) => {
                event.preventDefault();
                const text = event.clipboardData?.getData("text") || "";
                const filled = setDigits(text);
                boxes[Math.max(Math.min(filled, boxes.length) - 1, 0)].focus();
            });

            box.addEventListener("focus", () => box.select());
        });

        group.closest("form")?.addEventListener("submit", sync);
        if (hidden?.value) setDigits(hidden.value);
    };

    document.querySelectorAll("[data-otp]").forEach(bindOtp);

    const stepUpModal = document.getElementById("sg-step-up-modal");
    if (stepUpModal) {
        const locked = stepUpModal.hasAttribute("data-step-up-lock");
        const focusOtp = () => {
            const first = stepUpModal.querySelector(".sg-otp__box");
            first?.focus();
        };
        const openStepUp = () => {
            if (typeof stepUpModal.showModal !== "function" || stepUpModal.open) return;
            stepUpModal.showModal();
            window.setTimeout(focusOtp, 0);
        };
        if (stepUpModal.hasAttribute("data-step-up-required")) openStepUp();

        stepUpModal.addEventListener("cancel", (event) => {
            if (locked) event.preventDefault();
        });

        stepUpModal.addEventListener("click", (event) => {
            if (locked && event.target === stepUpModal) event.preventDefault();
        });
    }
})();
