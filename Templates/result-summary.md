# Template — describing a benchmark result

A reusable pattern for turning a `Results/<id>.md` into a short, neutral write-up you can share — a post,
a Slack message, a README blurb. The goal is to **share something interesting and possibly useful** — not
to sell a conclusion.

---

## How to use

1. Run the use case, then **screenshot the tables** from `Results/<id>.compact.md` — narrow, share-ready
   (Throughput / Time / Difference / Size). (The console table or `Results/<id>.md` also work.)
2. Fill in the skeleton below. Aim for **~150–180 words**.
3. Share the table as the **image**. Most places you'll paste this (feeds, chat, docs) render Markdown
   tables poorly, and feeds truncate after a few lines — so lead with the point and attach the table.

## Rules

- **Front-load.** Hook + the single finding go in the first two lines.
- **Title** (where the context has one — a doc, an article): plain and descriptive — name what was
  measured ("Indexed JSONB property: search & update"), not a teaser. In a feed there's no title field,
  so the hook is the title.
- **Plain language.** No term you'd have to define (or define it in three words). Prefer
  "about the same / ~2× / half" over raw numbers in the prose.
- **Table → image**, not text.
- **State, don't steer.** Report what you measured. No "you should", no "what would you pick",
  no rules-of-thumb. Let the reader draw their own conclusion.
- **No drama.** Skip "surprising / you won't believe / bigger than I expected." The numbers are the interest.
- **Footnote the scope.** One line on limits (one run, one machine, repeatable) keeps it honest.

## Formatting (depends where you paste it)

Markdown support varies a lot — check the surface before you post:

| Surface | Tables | Bold / italic / headings | Lists | Links |
|---|---|---|---|---|
| Docs / GitHub (README) | ✅ real table | ✅ full Markdown | ✅ `-` or `1.` | ✅ `[text](url)` |
| Slack | ❌ → image / code block | `*bold*` `_italic_`, no headings | manual (`•` + newline) | auto-links raw URLs |
| LinkedIn / X feed | ❌ → image | ❌ none (shows literally) | manual (`—`/`•` + newline) | raw URL only |

- **Feeds (no Markdown):** don't use `#`, `*`, `_`, backticks or `[]()` — they appear as raw characters.
  Build lists by hand with a leading `—` or `•` + a line break; separate ideas with one blank line.
  Don't fake bold with Unicode letters (it breaks screen readers). Many feeds down-rank posts with an
  outbound link in the body — put the link in the first comment instead.
- **Slack:** `*bold*`, `_italic_`, `` `code` ``; no tables → paste the table as an image (or a code block for alignment).
- **Docs / README:** full Markdown is fine — use a real table, not an image.
- **Lists, everywhere:** one line per bullet, parallel wording, lead with the thing measured, 3–5 max.
- **The table is always the result image** — except in docs, where a real Markdown table is better.

## Skeleton

```
1. Title       — short, plain, descriptive (optional). A doc/article wants a real title; in a feed the
   (optional)    hook below doubles as the title, so skip it.
2. Hook        — one line: what you tested, as a plain statement or question of fact.
3. Setup       — 2–3 lines: what you compared and the conditions (same data, same setup).
4. Results     — [ image of the table ]   (+ one number in the text only if it helps)
5. What it     — 3–5 one-line bullets, everyday language, purely factual.
   showed
6. A neutral   — one line: what the numbers indicate. An observation, not advice.
   reading
7. Footnote    — one line: scope/limits.   (Hashtags optional — only for social feeds.)
```

---

## Example — indexed JSONB property: search & update

**Title** (for a doc/article): *Indexed JSONB property — search & update*
*(In a feed, skip the title; the first line below opens the post.)*

> Postgres can put an index on a single property *inside* a JSONB column. I wanted to see how fast it is
> to **search and update that one property** — next to a plain column and to MongoDB.
>
> Setup: the same records stored three ways — plain table columns, one JSONB document, and MongoDB
> documents — with the same data and the same index on the same property. (The records are "devices," but
> the shape doesn't matter; it's just an object with ~20 fields.)
>
> [ image: results table — operations per second, higher = faster ]
>
> What I measured, and what one run showed:
> — Search by an indexed property: JSONB stayed close to a plain column. The index does its job.
> — Update by an indexed property: about the same across all three.
> — Search by a property with no index: JSONB and MongoDB ran ~2–3× slower — they scan and parse each document.
> — Disk space: the JSONB version used roughly 2× a plain column.
>
> So indexed-property access to JSONB looks practical; the cost lands on un-indexed access and storage —
> the price of keeping the data flexible.
>
> Numbers are from a single run on one machine; the test is repeatable on any record shape.
>
> #postgresql #jsonb #mongodb #databases

_(~150 words. The relatives — "~2–3×", "~2×" — stay valid after re-running at a larger size; drop the final
figures into the image.)_
