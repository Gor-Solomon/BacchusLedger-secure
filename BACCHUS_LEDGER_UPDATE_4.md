# Bacchus Ledger — Update 4: "Marani Assistant" (AI chat that answers ONLY from our data)

Work on a new git branch `assistant`. Keep everything that works. All existing rules stay (source chips, no invented data,
Georgian/English, append-only). `/api/selftest` must keep passing every existing check + the new ones.
**Timebox: keep it small and robust. If something is risky, leave it out.** Merge into `main` only when selftest passes.

## 1. What it is
A floating round button bottom-right (original icon: a small qvevri, wine colour, NOT any real person or film character).
Click → chat panel (380 px wide, full screen on phone). Title: "Marani Assistant". Language follows the app language toggle;
the assistant answers in the language of the question (English or Georgian).

## 2. Two layers (so it never fails on stage)
**Layer A — Quick questions (NO AI, always works, offline):** 5 buttons at the top of the panel, answered by our own code from the database:
1. "How much wine did W01 lose, and where?" → per-step losses + total (W01: racking 280, stock check 0, 2nd racking 100, filtration 50, bottling 20 = 450 L + 20 L remainder; $1,345.50 at $2.99/L).
2. "Which steps are above normal?" → list of flagged stages (W03 filtration 1.00%, W03 bottling 0.51%).
3. "Where did W01's grapes come from?" → farmers + kg + % (DEMO split, labelled).
4. "Show W03 readings" → latest temperature / pH / TA from A4.2 + link to the charts.
5. "Any live alerts?" → current live notifications (labelled SIMULATED) or "No alerts".
Each answer = short text + source chips + a "Open in app" link to the right page.

**Layer B — Free questions (AI, when online and a key exists):** the user types anything.
- Use **tool calling** (function calling) over our own API — NOT free-form knowledge. Tools:
  `get_overview()`, `list_batches()`, `get_batch(code)` (stages, losses, checks, origins), `get_readings(batch_or_vessel, type, from, to)`,
  `get_vessel(cellar_label_or_code)`, `get_journal(n, batch)`, `get_live_alerts()`, `search_facts(query)`.
- `search_facts` = simple keyword search over a local file `knowledge/facts.md` (create it from the problem/market facts already in the app's Overview and Data sources page: each fact = one line with its source). No embeddings needed.
- **System prompt rules (put in code, enforce):**
  - Answer ONLY with numbers returned by tools. Every number must keep its source tag (Dataset / Calculated / DEMO ASSUMPTION / SIMULATED / Entered).
  - If the tools don't return it: say "I don't have that data in the ledger." Never guess, never use outside knowledge for numbers.
  - Short answers (max ~6 lines), plain words, same language as the question.
  - Read-only: the assistant can NOT create, change or delete anything.
- Show the tool results used as small chips under the answer ("Used: batch W01-B1-2025, journal 5").

## 3. Providers and keys (security)
- Read keys ONLY from environment variables: `OPENAI_API_KEY`, `GEMINI_API_KEY`. Optional: `OPENAI_MODEL`, `GEMINI_MODEL`, `ASSISTANT_PROVIDER` (`openai` default, `gemini`), `ASSISTANT_ENABLED` (default `true`).
- Use a current small, fast, low-cost model by default (configurable via the env vars above). Timeout 15 s; on error or timeout → try the other provider once → then reply: "AI is offline right now — use the quick questions above."
- `docker-compose.yml`: add `env_file: .env` (optional, app must start without it). Create `.env.example` with empty values. Add `.env` to `.gitignore`.
- **Never** write a key into code, the prompt files, logs, the browser, API responses or git. Calls to the AI provider happen on the server only.
- No key → the panel shows "Quick questions only (AI not configured)" and Layer A still works.

## 4. UI details
- Typing indicator, message bubbles (user right, assistant left), auto-scroll, Enter to send, max 500 characters input.
- In Jury mode the panel opens with the 5 quick questions visible; first message: "Ask me about any batch, vessel or reading. I only answer from the ledger."
- Matches the Update 3 design system (tokens, Inter, radius, shadows).

## 5. New selftest checks
- Quick question 1 returns 450 L total and the 5 step values above; question 2 returns exactly the two W03 flagged stages.
- `POST /api/assistant/ask` with no key configured returns HTTP 200 and the "AI not configured" message (no crash).
- No served file (wwwroot) and no API response contains the strings `sk-` or `AQ.` (key leak check).
- The assistant endpoints are read-only (no DB writes happen during an ask).

## 6. Finish
1. Selftest passes (old + new). 2. `docker compose up --build -d`. 3. Test Layer A with the network OFF, Layer B with a key.
4. README: how to create `.env`, the 5 quick questions, 3 good free questions to ask on stage, and the warning to rotate keys after the event.
5. Commit on `assistant`, then merge to `main` only if everything passes.
