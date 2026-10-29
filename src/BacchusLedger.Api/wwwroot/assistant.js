// Bacchus Ledger — Marani Assistant (Update 4). Quick questions are answered by the server's own code (no AI, offline);
// free questions go to POST /api/assistant/ask (AI with read-only tools, server side). No key ever reaches the browser.
"use strict";

const Assistant = {
  open: false, busy: false, greeted: false, status: null,
  // Original icon: a small qvevri (egg-shaped clay vessel) with its rim.
  icon: `<svg viewBox="0 0 32 32" aria-hidden="true"><path d="M11.5 7.2h9v1.6c4 1.7 6.5 4.9 6.5 8.4 0 4.6-4.4 8.6-11 11.3C9.4 25.8 5 21.8 5 17.2c0-3.5 2.5-6.7 6.5-8.4z" fill="currentColor"/>` +
    `<rect x="10" y="4.5" width="12" height="2.6" rx="1.3" fill="currentColor"/><path d="M9.2 15.6c4.4 1.5 9.2 1.5 13.6 0" fill="none" stroke="var(--asst-line, #fff)" stroke-width="1.4" stroke-linecap="round" opacity=".7"/></svg>`,

  init() {
    const b = document.createElement("button");
    b.id = "asstBtn"; b.className = "asst-fab no-print"; b.title = "Marani Assistant"; b.setAttribute("aria-label", "Marani Assistant");
    b.setAttribute("aria-expanded", "false"); b.innerHTML = this.icon;
    b.onclick = () => this.toggle();
    const p = document.createElement("section");
    p.id = "asstPanel"; p.className = "asst-panel hidden no-print"; p.setAttribute("role", "dialog"); p.setAttribute("aria-label", "Marani Assistant");
    p.innerHTML = `<header class="asst-head"><span class="asst-mark">${this.icon}</span>
        <div class="grow-1"><b>Marani Assistant</b><small id="asstStatus">…</small></div>
        <button class="icon-btn" id="asstClose" aria-label="Close">${ico("x")}</button></header>
      <div id="asstQuick"></div>
      <div class="asst-log" id="asstLog" aria-live="polite"></div>
      <form class="asst-form" id="asstForm">
        <textarea id="asstInput" rows="1" maxlength="500" aria-label="Question"></textarea>
        <button class="btn small" type="submit" id="asstSend"></button>
        <small class="asst-count" id="asstCount">0/500</small>
      </form>`;
    document.body.append(b, p);
    $("#asstClose").onclick = () => this.toggle(false);
    const input = $("#asstInput");
    input.addEventListener("input", () => { $("#asstCount").textContent = `${input.value.length}/500`; });
    input.addEventListener("keydown", (e) => { if (e.key === "Enter" && !e.shiftKey && !e.isComposing) { e.preventDefault(); this.send(); } });
    $("#asstForm").onsubmit = (e) => { e.preventDefault(); this.send(); };
    document.addEventListener("keydown", (e) => { if (e.key === "Escape" && this.open) this.toggle(false); });
  },

  async toggle(force) {
    this.open = force ?? !this.open;
    $("#asstPanel").classList.toggle("hidden", !this.open);
    $("#asstBtn").classList.toggle("on", this.open);
    $("#asstBtn").setAttribute("aria-expanded", String(this.open));
    if (!this.open) return;
    await this.refresh();
    if (!window.matchMedia("(max-width: 640px)").matches) $("#asstInput").focus();
  },

  /** Header, quick questions and labels follow the app language and Jury/Expert mode. */
  async refresh() {
    try { this.status = await api(`/api/assistant/status?lang=${LANG}`); } catch { this.status = null; }
    const s = this.status;
    $("#asstStatus").textContent = s ? s.status_text : t("Assistant unavailable");
    $("#asstStatus").classList.toggle("off", !s || !s.ai_configured);
    const btns = (s?.quick || []).map((q) => `<button type="button" class="asst-q" data-n="${q.n}">${esc(q.title)}</button>`).join("");
    $("#asstQuick").innerHTML = isJury()
      ? `<div class="asst-quick">${btns}</div>`
      : `<details class="asst-quick-d"><summary>${esc(t("Quick questions"))}</summary><div class="asst-quick">${btns}</div></details>`;
    $$(".asst-q", $("#asstQuick")).forEach((b) => (b.onclick = () => this.ask(b.textContent, +b.dataset.n)));
    $("#asstInput").placeholder = t("Ask about a batch, vessel or reading…");
    $("#asstSend").textContent = t("Send");
    if (!this.greeted && s) { this.greeted = true; this.add("bot", esc(s.greeting)); }
  },

  send() {
    const input = $("#asstInput");
    const q = input.value.trim().slice(0, 500);
    if (!q || this.busy) return;
    input.value = ""; $("#asstCount").textContent = "0/500";
    this.ask(q);
  },

  async ask(text, quickN) {
    if (this.busy) return;
    this.busy = true;
    $("#asstSend").disabled = true;
    this.add("user", esc(text));
    const typing = this.add("bot typing", `<span class="dots" aria-label="${esc(t("Typing"))}"><i></i><i></i><i></i></span>`);
    try {
      const a = quickN ? await api(`/api/assistant/quick/${quickN}?lang=${LANG}`) : await post("/api/assistant/ask", { question: text });
      typing.remove();
      this.answer(a);
    } catch {
      typing.remove();
      this.add("bot muted", esc(t("AI is offline right now — use the quick questions above.")));
    } finally {
      this.busy = false;
      $("#asstSend").disabled = false;
    }
  },

  answer(a) {
    // [Source tag] in the text becomes a source chip; everything else is escaped text.
    const body = esc(a.text).replace(/\[([^\[\]]{2,60})\]/g, (m, tags) => tags.split(/[,;]\s*/).map((tag) => `<span class="chip ${chipClass(tag)}">${tag}</span>`).join("")).replace(/\n/g, "<br>");
    const src = (a.sources || []).length ? `<div class="asst-chips">${a.sources.map((s) => `<span class="chip ${chipClass(s)}">${esc(s)}</span>`).join("")}</div>` : "";
    const used = (a.used || []).length ? `<div class="asst-used">${esc(t("Used"))}: ${a.used.map((u) => `<span>${esc(u)}</span>`).join("")}</div>` : "";
    const link = a.link && /^#\/[\w/?=&.-]*$/.test(a.link) ? `<a class="asst-link" href="${esc(a.link)}">${esc(t("Open in app"))}${a.link_label ? " · " + esc(a.link_label) : ""} ${ico("chevron-right")}</a>` : "";
    const el = this.add(`bot ${a.mode === "ai" || a.mode === "quick" ? "" : "muted"}`, body + src + used + link);
    const l = $(".asst-link", el);
    if (l) l.onclick = () => { if (window.matchMedia("(max-width: 640px)").matches) this.toggle(false); };
  },

  add(cls, html) {
    const log = $("#asstLog");
    const d = document.createElement("div");
    d.className = `asst-msg ${cls}`;
    d.innerHTML = html;
    log.appendChild(d);
    log.scrollTop = log.scrollHeight;
    return d;
  },
};
