(() => {
    const root = document.querySelector("[data-dashboard-root]");
    const form = document.querySelector("[data-dashboard-form]");
    const body = document.querySelector("[data-dashboard-body]");
    if (!root || !form || !body) return;

    let lastSection = null;

    const palette = () => {
        const root = document.documentElement;
        const styles = getComputedStyle(root);
        const isDark = root.getAttribute("data-theme") === "dark";
        const navy = styles.getPropertyValue("--sg-navy").trim() || "#0b192c";
        const navySoft = styles.getPropertyValue("--sg-navy-soft").trim() || "#13283f";
        return {
            isDark,
            navy,
            navySoft,
            axis: styles.getPropertyValue("--sg-line").trim() || "#d5dee8",
            text: styles.getPropertyValue("--sg-text-muted").trim() || "#7a8a9c",
            strokeStart: isDark ? "#6b9fd4" : "#1a3550",
            strokeEnd: isDark ? "#b8d4f0" : "#3d6289",
            fillTop: isDark ? "rgba(107, 159, 212, 0.55)" : "rgba(38, 74, 110, 0.32)",
            fillMid: isDark ? "rgba(61, 98, 137, 0.22)" : "rgba(26, 53, 80, 0.14)",
            fillBottom: isDark ? "rgba(11, 25, 44, 0)" : "rgba(11, 25, 44, 0.02)",
            dotCore: isDark ? "#e8edf3" : navy,
            dotRing: isDark ? "rgba(107, 159, 212, 0.95)" : "#ffffff",
            glow: isDark ? "rgba(158, 197, 232, 0.65)" : "rgba(11, 25, 44, 0.15)"
        };
    };

    const traceSmoothLine = (ctx, points, tension = 0.38) => {
        if (points.length === 0) return;
        if (points.length === 1) {
            ctx.moveTo(points[0].x, points[0].y);
            return;
        }
        ctx.moveTo(points[0].x, points[0].y);
        for (let i = 0; i < points.length - 1; i++) {
            const p0 = points[Math.max(0, i - 1)];
            const p1 = points[i];
            const p2 = points[i + 1];
            const p3 = points[Math.min(points.length - 1, i + 2)];
            const cp1x = p1.x + (p2.x - p0.x) * tension;
            const cp1y = p1.y + (p2.y - p0.y) * tension;
            const cp2x = p2.x - (p3.x - p1.x) * tension;
            const cp2y = p2.y - (p3.y - p1.y) * tension;
            ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, p2.x, p2.y);
        }
    };

    const escapeHtml = (value) => String(value ?? "")
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;");

    const renderFeed = (items, emptyMessage) => {
        if (!items || items.length === 0) return `<p class="sg-empty">${escapeHtml(emptyMessage)}</p>`;
        return `<ul class="sg-dash-feed">${items.map((item) => {
            const main = `<span class="sg-dash-feed-main"><span class="sg-dash-feed-title">${escapeHtml(item.title)}</span><span class="sg-dash-feed-meta">${escapeHtml(item.detail)}</span></span>`;
            const when = `<span class="sg-dash-feed-when">${escapeHtml(item.when)}</span>`;
            if (item.href) {
                return `<li class="sg-dash-feed-item"><a class="sg-dash-feed-link" href="${escapeHtml(item.href)}">${main}${when}<span class="sg-dash-chevron" aria-hidden="true"></span></a></li>`;
            }
            return `<li class="sg-dash-feed-item"><div class="sg-dash-feed-link is-static">${main}${when}</div></li>`;
        }).join("")}</ul>`;
    };

    const chartLayout = (labels, values, width, height) => {
        const pad = { l: 44, r: 16, t: 22, b: 36 };
        const w = width - pad.l - pad.r;
        const h = height - pad.t - pad.b;
        const baseline = pad.t + h;
        const max = Math.max(1, ...values);
        const n = Math.max(1, values.length - 1);
        const points = values.map((_, i) => {
            const x = pad.l + (values.length === 1 ? w / 2 : (i / n) * w);
            const y = baseline - (values[i] / max) * h;
            return { x, y, index: i };
        });
        return { labels, values, points, max, pad, baseline, width, height };
    };

    const drawLine = (ctx, layout, colors, activeIndex) => {
        const { labels, values, points, max, pad, baseline, width, height } = layout;
        const w = width - pad.l - pad.r;

        ctx.strokeStyle = colors.axis;
        ctx.lineWidth = 1;
        ctx.globalAlpha = colors.isDark ? 0.35 : 0.85;
        ctx.beginPath();
        ctx.moveTo(pad.l, baseline);
        ctx.lineTo(pad.l + w, baseline);
        ctx.stroke();
        ctx.globalAlpha = 1;

        const fill = ctx.createLinearGradient(0, pad.t, 0, baseline);
        fill.addColorStop(0, colors.fillTop);
        fill.addColorStop(0.55, colors.fillMid);
        fill.addColorStop(1, colors.fillBottom);
        ctx.beginPath();
        traceSmoothLine(ctx, points);
        ctx.lineTo(points[points.length - 1].x, baseline);
        ctx.lineTo(points[0].x, baseline);
        ctx.closePath();
        ctx.fillStyle = fill;
        ctx.fill();

        const stroke = ctx.createLinearGradient(pad.l, 0, pad.l + w, 0);
        stroke.addColorStop(0, colors.strokeStart);
        stroke.addColorStop(1, colors.strokeEnd);
        ctx.lineCap = "round";
        ctx.lineJoin = "round";
        ctx.shadowColor = colors.glow;
        ctx.shadowBlur = colors.isDark ? 14 : 4;
        ctx.beginPath();
        traceSmoothLine(ctx, points);
        ctx.strokeStyle = stroke;
        ctx.lineWidth = colors.isDark ? 2.75 : 2.25;
        ctx.stroke();
        ctx.shadowBlur = 0;

        points.forEach((p, i) => {
            const isActive = activeIndex === i;
            const outer = isActive ? (colors.isDark ? 7.5 : 6.5) : (colors.isDark ? 5.5 : 4.5);
            const inner = isActive ? 4 : 3;
            ctx.beginPath();
            ctx.arc(p.x, p.y, outer, 0, Math.PI * 2);
            ctx.fillStyle = colors.dotRing;
            ctx.globalAlpha = colors.isDark ? 0.95 : 0.9;
            ctx.fill();
            ctx.globalAlpha = 1;
            ctx.beginPath();
            ctx.arc(p.x, p.y, inner, 0, Math.PI * 2);
            ctx.fillStyle = colors.dotCore;
            ctx.fill();
        });

        if (activeIndex !== null && activeIndex >= 0 && points[activeIndex]) {
            const active = points[activeIndex];
            ctx.strokeStyle = colors.strokeStart;
            ctx.lineWidth = 1.5;
            ctx.globalAlpha = colors.isDark ? 0.85 : 0.55;
            ctx.beginPath();
            ctx.moveTo(active.x, baseline);
            ctx.lineTo(active.x, active.y);
            ctx.stroke();
            ctx.globalAlpha = 1;
        }

        ctx.fillStyle = colors.text;
        ctx.font = "11px Segoe UI, sans-serif";
        ctx.textAlign = "right";
        ctx.fillText(String(max), pad.l - 6, pad.t + 10);
        ctx.fillText("0", pad.l - 6, baseline + 4);
        ctx.textAlign = "left";

        const step = Math.max(1, Math.ceil(labels.length / 6));
        labels.forEach((label, i) => {
            if (i % step !== 0 && i !== labels.length - 1) return;
            const p = points[i];
            ctx.fillText(label, Math.max(pad.l, p.x - 20), height - 10);
        });
    };

    const paintChart = (canvas) => {
        const state = canvas.__sgChart;
        if (!state || !state.layout) return;
        const colors = palette();
        const ratio = window.devicePixelRatio || 1;
        const width = state.layout.width;
        const height = state.layout.height;
        canvas.width = Math.floor(width * ratio);
        canvas.height = Math.floor(height * ratio);
        const ctx = canvas.getContext("2d");
        if (!ctx) return;
        ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
        ctx.clearRect(0, 0, width, height);
        drawLine(ctx, state.layout, colors, state.activeIndex);
    };

    const ensureChartUi = (canvas, frame) => {
        if (!frame) return;
        if (canvas.dataset.chartBound === "1") return;
        canvas.dataset.chartBound = "1";
        canvas.classList.add("sg-dash-chart-canvas");
        let tip = frame.querySelector(".sg-dash-chart-tooltip");
        if (!tip) {
            tip = document.createElement("div");
            tip.className = "sg-dash-chart-tooltip";
            tip.hidden = true;
            tip.setAttribute("role", "tooltip");
            frame.appendChild(tip);
        }
        const state = canvas.__sgChart || {};
        canvas.__sgChart = state;
        canvas.__sgChart.tip = tip;
        if (canvas.__sgChart.activeIndex === undefined) canvas.__sgChart.activeIndex = null;

        const hideTip = () => {
            canvas.__sgChart.activeIndex = null;
            tip.hidden = true;
            paintChart(canvas);
        };

        const showTip = (index) => {
            const state = canvas.__sgChart;
            if (!state.layout || index < 0) {
                hideTip();
                return;
            }
            state.activeIndex = index;
            const point = state.layout.points[index];
            const label = state.layout.labels[index] || "";
            const value = state.layout.values[index] ?? 0;
            tip.innerHTML = `<p class="sg-dash-chart-tooltip-label">${escapeHtml(label)}</p><p class="sg-dash-chart-tooltip-value">${escapeHtml(value)}</p>`;
            tip.style.left = `${point.x}px`;
            tip.style.top = `${point.y}px`;
            tip.hidden = false;
            paintChart(canvas);
        };

        const indexFromEvent = (event) => {
            const state = canvas.__sgChart;
            if (!state.layout) return -1;
            const rect = canvas.getBoundingClientRect();
            const x = event.clientX - rect.left;
            const { pad, width, points } = state.layout;
            if (x < pad.l - 12 || x > width - pad.r + 12) return -1;
            let best = 0;
            let bestDist = Number.POSITIVE_INFINITY;
            points.forEach((p, i) => {
                const d = Math.abs(p.x - x);
                if (d < bestDist) {
                    bestDist = d;
                    best = i;
                }
            });
            return best;
        };

        canvas.addEventListener("mousemove", (event) => showTip(indexFromEvent(event)));
        canvas.addEventListener("mouseleave", hideTip);
        canvas.addEventListener("touchstart", (event) => {
            const touch = event.touches[0];
            if (touch) showTip(indexFromEvent(touch));
        }, { passive: true });
        canvas.addEventListener("touchend", hideTip);
    };

    const chartSize = (canvas, frame) => {
        const height = canvas.clientHeight || Number(canvas.getAttribute("height")) || 240;
        const width = Math.max(
            canvas.clientWidth,
            frame?.clientWidth ?? 0,
            canvas.parentElement?.clientWidth ?? 0,
            320
        );
        return { width, height };
    };

    const renderCharts = (section) => {
        if (!section) return;
        body.querySelectorAll("[data-chart]").forEach((canvas) => {
            const key = canvas.getAttribute("data-chart");
            const series = section[key] || { labels: [], values: [] };
            const labels = series.labels || [];
            const values = series.values || [];
            const frame = canvas.closest("[data-chart-empty]") || canvas.parentElement;
            if (frame?.hasAttribute("data-chart-empty")) {
                frame.classList.toggle("is-empty", labels.length === 0);
            }
            if (labels.length === 0 || values.length === 0) {
                if (canvas.__sgChart?.tip) canvas.__sgChart.tip.hidden = true;
                if (canvas.__sgChart) canvas.__sgChart.layout = null;
                return;
            }
            const { width, height } = chartSize(canvas, frame);
            if (!canvas.__sgChart) canvas.__sgChart = { activeIndex: null, layout: null, tip: null };
            ensureChartUi(canvas, frame);
            const prevIndex = canvas.__sgChart.activeIndex;
            canvas.__sgChart.layout = chartLayout(labels, values, width, height);
            canvas.__sgChart.activeIndex = prevIndex;
            paintChart(canvas);
        });
    };

    const updateAttention = (section) => {
        body.querySelectorAll(".sg-dash-attention-link").forEach((link) => {
            const countEl = link.querySelector(".sg-dash-attention-count[data-kpi]");
            if (!countEl) return;
            const key = countEl.getAttribute("data-kpi");
            const value = key && Object.prototype.hasOwnProperty.call(section, key) ? Number(section[key]) : 0;
            countEl.textContent = value;
            link.classList.toggle("is-highlight", value > 0);
        });
    };

    const applyPayload = (payload) => {
        const errorEl = document.querySelector("[data-range-error]");
        const labelEl = document.querySelector("[data-range-label]");
        const invalidEl = document.querySelector("[data-dashboard-invalid]");
        const custom = form.querySelector("[data-dash-custom]");

        if (labelEl) labelEl.textContent = payload.rangeLabel || "";
        if (errorEl) {
            errorEl.textContent = payload.error || "";
            errorEl.hidden = !payload.error;
        }
        if (invalidEl) invalidEl.hidden = !payload.error;
        body.hidden = !!payload.error;

        form.querySelectorAll(".sg-dash-preset").forEach((btn) => {
            const isCustomBtn = btn.hasAttribute("data-dash-show-custom");
            const active = isCustomBtn
                ? payload.preset === "custom"
                : btn.value === payload.preset;
            btn.classList.toggle("is-active", active);
        });
        if (custom) custom.classList.toggle("is-collapsed", payload.preset !== "custom");

        const fromInput = form.querySelector("[name=from]");
        const toInput = form.querySelector("[name=to]");
        if (fromInput && payload.from) fromInput.value = payload.from;
        if (toInput && payload.to) toInput.value = payload.to;

        if (payload.error) return;

        const section = payload.admin || payload.officer || payload.employee;
        lastSection = section;
        if (!section) return;

        body.querySelectorAll("[data-kpi]").forEach((el) => {
            const key = el.getAttribute("data-kpi");
            if (key && Object.prototype.hasOwnProperty.call(section, key))
                el.textContent = section[key];
        });
        body.querySelectorAll("[data-text]").forEach((el) => {
            const key = el.getAttribute("data-text");
            if (key && Object.prototype.hasOwnProperty.call(section, key))
                el.textContent = section[key];
        });

        updateAttention(section);

        const feeds = {
            recentAudit: ["recentAudit", "No audit events in this range."],
            recentSecurityActivity: ["recentSecurityActivity", "No security events in this range."],
            recentActivity: ["recentActivity", "No account activity in this range."]
        };
        Object.entries(feeds).forEach(([name, [key, empty]]) => {
            const target = body.querySelector(`[data-list="${name}"]`);
            if (target) target.innerHTML = renderFeed(section[key], empty);
        });

        renderCharts(section);
    };

    const showRangeError = (message) => {
        const errorEl = document.querySelector("[data-range-error]");
        if (errorEl) {
            errorEl.textContent = message;
            errorEl.hidden = false;
        }
    };

    const navigateWithPreset = (preset, from, to) => {
        const url = new URL(window.location.pathname, window.location.origin);
        url.searchParams.set("preset", preset);
        if (preset === "custom") {
            url.searchParams.set("from", from);
            url.searchParams.set("to", to);
        }
        const profile = new URL(window.location.href).searchParams.get("profile");
        if (profile) url.searchParams.set("profile", profile);
        window.location.assign(url);
    };

    root.querySelector("[data-dash-show-custom]")?.addEventListener("click", () => {
        form.querySelector("[data-dash-custom]")?.classList.remove("is-collapsed");
        form.querySelectorAll(".sg-dash-preset").forEach((btn) => {
            btn.classList.toggle("is-active", btn.hasAttribute("data-dash-show-custom"));
        });
        const errorEl = document.querySelector("[data-range-error]");
        if (errorEl) errorEl.hidden = true;
    });

    form.addEventListener("submit", (event) => {
        event.preventDefault();
        const submitter = event.submitter;
        const preset = submitter && submitter.name === "preset" ? submitter.value : "custom";
        const from = form.querySelector("[name=from]")?.value ?? "";
        const to = form.querySelector("[name=to]")?.value ?? "";

        if (preset === "custom") {
            if (!from || !to) {
                showRangeError("Choose a start date and an end date.");
                return;
            }
            if (to < from) {
                showRangeError("The end date must be on or after the start date.");
                return;
            }
        }

        const errorEl = document.querySelector("[data-range-error]");
        if (errorEl) errorEl.hidden = true;
        navigateWithPreset(preset, from, to);
    });

    const bootDashboard = () => {
        const initialNode = document.getElementById("sg-dashboard-data");
        if (!initialNode?.textContent) return;
        try {
            applyPayload(JSON.parse(initialNode.textContent));
        } catch {
            /* server-rendered markup */
        }
    };

    const scheduleChartPaint = () => {
        if (!lastSection) return;
        requestAnimationFrame(() => renderCharts(lastSection));
    };

    bootDashboard();
    requestAnimationFrame(scheduleChartPaint);

    if (!body.dataset.chartsObserved && typeof ResizeObserver !== "undefined") {
        body.dataset.chartsObserved = "1";
        body.querySelectorAll("[data-chart-empty]").forEach((frame) => {
            new ResizeObserver(scheduleChartPaint).observe(frame);
        });
    }

    window.addEventListener("resize", scheduleChartPaint);
    window.addEventListener("load", scheduleChartPaint);

    new MutationObserver(scheduleChartPaint).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ["data-theme"]
    });
})();
