# Requirements

> Canonical implementation documents are grouped by epic and story in
> [`../superpowers/`](../superpowers/). New plan/spec names use `EPIC-##-US-###-slug`; dates belong
> only to historical notes. The delivered cross-cutting behavior is summarized in
> [`../superpowers/plans/EPIC-12-US-000-as-built-alignment.md`](../superpowers/plans/EPIC-12-US-000-as-built-alignment.md).

The delivery source of truth for the Customer Support CRM, structured per the rule specification
§7 (`docs/customer-support-crm-sdd-specification.md`).

| | |
|---|---|
| Epics | **13** areas (12 rule-file areas + `EPIC-13` cross-cutting UI fidelity) — [`epics/`](./epics/) |
| Stories | **49 specified** (216 points, ids `US-001`–`US-133`) · backlog titles reserved `US-002…US-100` where unspecified |
| Criteria covered | **101** claimed — all 68 `AC-n` and 33 `FND-n`. **Proven against the current backend: 3.** See [`slice-s1-coverage.md`](./slice-s1-coverage.md) |
| Coverage proof | [`slice-s1-coverage.md`](./slice-s1-coverage.md) |
| Delivery order | [`delivery-plan.md`](./delivery-plan.md) — 15 sprints across 9 slices |
| Features | **13** vertical increments (`FEAT-01`–`FEAT-13`) covering slice S1 |

## The unit of delivery is a feature, not a layer

**A feature ships as backend + frontend + tests, together, or it has not shipped.** Each story
carries three rows binding it into one:

- **Feature** — the `FEAT-nn` it belongs to, defined in [`delivery-plan.md`](./delivery-plan.md).
- **Layer** — `Backend` or `Frontend`.
- **Ships with** — the counterpart stories that must land in the same increment. **A backend story
  with a counterpart is not done until that counterpart is done.**

Pairings are symmetric and never span a sprint, so every feature is completable where it sits. Nine
of the thirteen features are single-layer for recorded reasons — infrastructure with no user surface,
a capability whose UI the spec places in another feature's screen, or cross-cutting frontend work
whose server half shipped earlier. The delivery plan states which and why; an unrecorded missing
layer is indistinguishable from a forgotten one.

The normative loop is in
[`.claude/skills/sdd-workflow/SKILL.md`](../../.claude/skills/sdd-workflow/SKILL.md): backend plan →
backend implementation → **frontend plan for the same feature** → frontend implementation → tests →
ship.

## What is authoritative here, and what is not

This folder owns **story decomposition, feature grouping, epic grouping under the twelve areas,
estimates and delivery status**. It does **not** own acceptance criteria: where a slice has a spec,
the spec's `AC-n` / `FND-n` are authoritative and stories *cite* them. If a story file and its spec
disagree, **the spec is right and the story file is stale.**

```
docs/assessment/brief.md              the client's twelve areas, verbatim + slice decomposition
        ↓
docs/brd/customer-support-crm-brd.md  objectives, FR-<area>.<n>, business rules, KPIs, gaps
        ↓
docs/product/                         vision, priorities, personas, glossary, assumptions/OQ register
        ↓
docs/requirements/                    ← this folder: epics, stories, delivery plan, status
        ↓
docs/superpowers/specs/               per-slice acceptance criteria (AC-n, FND-n)
        ↓
docs/superpowers/plans/<feature>/       implementation-plan.md, README.md, tasks/
        ↓
test naming the criterion → commit
```

## Identifiers

| Prefix | Meaning | Stability |
|---|---|---|
| `US-nnn` | Story. Rule-file numbers `US-001`–`US-100` are **reserved by title**; specified stories adopt the number of the proposal they realise; stories without a counterpart take appended numbers from `US-101`. Unspecified proposals keep their reserved number with no file | Permanent |
| `EPIC-nn` | The twelve rule-file areas | Permanent |
| `AC-n` / `FND-n` | Acceptance criterion, owned by a spec | Permanent |
| `FR-<area>.<n>` | Functional requirement, owned by the BRD | Permanent |
| `BR-n` `NFR-n` `KPI-n` `PA-n` `DEP-n` `RSK-n` `OQ-n` `G-n` `CON-n` | Rules, targets, assumptions, dependencies, risks, questions, gaps — owned by the BRD (OQ register indexed in [`../product/05-assumptions-and-open-questions.md`](../product/05-assumptions-and-open-questions.md)) | Permanent |

Roadmap stories have **titles but no files** until their slice is specified. Allocating a permanent
file to unspecified work is how a placeholder becomes a requirement nobody chose.

<details>
<summary><strong>Migration from the previous structure</strong></summary>

Stories were keyed `US-<slice>.<nn>` (e.g. `US-1.30`) and lived in sprint folders. The 2026-08-24
restructure adopted the rule specification's global scheme. Each story's header records its old id
— `*(was US-1.XX)*` — so provenance never depends on this table.

Rule-file numbers adopted: 1.16→001, 1.19→002, 1.18→004, 1.44→006, 1.43→007, 1.46→008, 1.21→009,
1.24→010, 1.22→013, 1.28→014, 1.25→016, 1.32→022, 1.27→026, 1.23→035, 1.37→038, 1.41→093.
Appended sequence `US-101`–`US-133` took the remainder in dependency order: 1.01–1.15 → 101–115,
then 1.17→116, 1.20→117, 1.26→118, 1.29→119, 1.30→120, 1.31→121, 1.33→122, 1.34→123, 1.35→124,
1.36→125, 1.38→126, 1.39→127, 1.40→128, 1.42→129, 1.45→130, 1.47→131, 1.48→132, 1.49→133.
Secondary realisations (one story covering two proposals): US-002 also covers US-003; US-013 also
covers US-012; US-093 also covers US-094/US-095. Sprint folders were dissolved into
[`delivery-plan.md`](./delivery-plan.md); former epics `EP-x.nn` were absorbed into the twelve
[`EPIC-nn`](./epics/) files.

</details>

## Status vocabulary

Set from what is committed and executed. **Never from what is planned.**

| Status | Means |
|---|---|
| `done` | Implemented, and its criteria are covered by tests that were run and passed |
| `partial` | Some criteria proven, others not provable yet — the story names which and why |
| `failing` | Implementation attempted and its tests are **red**. Named, not hidden |
| `not started` | No implementation exists |
| `superseded` | The implementation that satisfied it was replaced. The requirement stands; this codebase is no longer shown to meet it. Never leave such a story reading `done` |

Every story carries a **Status evidence** section naming the test files and outcomes behind its
status. A clean build is not a passing test, and a passing test is not a working feature.

**Current state, verified 2026-08-25:** 1 `done` · 16 `superseded` · 32 `not started`.

The backend was replaced on 2026-08-25 when the CCE Platform reference was adopted as the baseline
([ADR-0009](../adr/0009-adopt-the-support-platform-as-the-crm-baseline.md)). Sixteen stories that read
`done` or `partial` now read `superseded`: their criteria stand as requirements, but the code that
proved them is archived rather than running. Only `US-112` (staff sign-in) is `done` against the new
baseline, and by live probe rather than by a test naming its criteria — which is weaker evidence
than it previously had, and is recorded that way in the story.

The current backend passes **97 inherited tests**. Those cover the platform concerns, not the
brief's criteria, so a green suite here is not evidence that any `AC-n` is met.

## Story file anatomy

Header table — story id (+ *was* provenance), epic, **feature, layer, ships with**, rule proposal,
actor, priority, sprint, estimate, status, BRD requirements, spec criteria, dependencies — then:

- **Story** — the one-sentence as-a / I-want / so-that.
- **Business rules** — cited `BR-n` from the BRD where one governs; otherwise rules derived from the
  story's own cited criteria, labelled as such. Nothing invented here.
- **Acceptance criteria** — named Given/When/Then scenarios in the rule specification's style, each
  anchored to its global spec id: `#### ACn — Scenario name (spec AC-7)`. The local number orders
  the scenarios; the parenthesised id is the authority.
- **SQL tables** — persisted data this story reads or writes; excerpts only, linked to the
  authoritative [S1 schema](../superpowers/specs/EPIC-12-US-000-s1-schema.md); a story without
  persistence says so explicitly.
- **Test cases** — one row per test mapped to an already-cited criterion id, naming level, test
  name, given/when/then → expected. ✅ = exists and has passed; otherwise `planned`. No test case
  may cite a criterion the story does not already claim.
- **Notes** — the reasoning, and the failure mode the story exists to prevent.
- **Open questions** — registered `OQ/G/PA/DEP/RSK/CON` ids touching this story, or `None.`
- **Status evidence** — which tests, and their actual outcome.

## Definition of Ready

1. Cites the `FR-<area>.<n>` it satisfies.
2. For a specified slice, cites the `AC-n`/`FND-n` that will prove it.
3. Dependencies named; none in a later sprint.
4. Criteria observable — given, when, then. "Works correctly" is not an acceptance criterion.
5. Small enough to be one commit. If describing it needs "and", it is probably two stories.

## Definition of Done

From `CLAUDE.md`, not negotiable: reviewed line by line; failing test written first and **seen to
fail**; suite run with actual output pasted; clean build under warnings-as-errors; every cited
criterion covered by a test naming it; conventional commit stating criteria; anything skipped or red
stated plainly here and in `rubric-traceability.md`.

**And the feature gate:** a story with a **Ships with** counterpart is not done until that
counterpart is done, with both layers' tests green. An endpoint no screen consumes has shipped
nothing. The single Playwright journey (`AC-64`) is terminal, not per feature — the spec defines
exactly one, and adding more would mean amending an approved spec.

## Verifying this folder

Structure is checkable, so it is checked rather than trusted. From `assessment-sdd/`:

```bash
# Every AC-1..AC-68 and FND-1..FND-32 (+13a) claimed exactly once
python - <<'PY'
import re,glob,collections
c=collections.Counter()
for f in glob.glob("docs/requirements/user-stories/*.md"):
    s=open(f,encoding="utf-8").read()
    m=re.search(r"\*\*Spec criteria\*\* \| ([^|]+)",s)
    c.update(x.strip() for x in m.group(1).split(","))
want=[f"AC-{i}" for i in range(1,69)]+[f"FND-{i}" for i in range(1,33)]+["FND-13a"]
print("missing:",[k for k in want if c[k]==0] or "none")
print("duplicated:",[k for k,v in c.items() if v>1] or "none")
print("unexpected:",[k for k in c if k not in want] or "none")
PY

# Every story carries all sections; every test case cites a criterion the story claims;
# every criterion scenario heading anchors a global id
python - <<'PY'
import re,glob
bad=[]
for f in glob.glob("docs/requirements/user-stories/*.md"):
    s=open(f,encoding="utf-8").read()
    for sec in ("## Business rules","## SQL tables","## Test cases","## Open questions","## Status evidence"):
        if sec not in s: bad.append((f,"missing "+sec))
    if "| **Actor** |" not in s: bad.append((f,"missing Actor row"))
    claimed=set(x.strip() for x in re.search(r"\*\*Spec criteria\*\* \| ([^|]+)",s).group(1).split(","))
    body=s.split("## Acceptance criteria",1)[-1].split("## SQL tables",1)[0]
    claimed.update(re.findall(r"\b((?:AC|FND|FR)-\d+[a]?(?:\.\d+)?)\b",body))
    for tc in re.finditer(r"\| TC-\d+ \| ([A-Za-z]+-[0-9a.]+)",s):
        if tc.group(1) not in claimed: bad.append((f,tc.group(1)+" not claimed"))
print("violations:",bad or "none")
PY

# No dead relative links anywhere in requirements/
grep -rhoE '\]\(([^)#]+\.md)' docs/requirements | sed 's/](//' | sort -u
```

The exactly-once check is the one that matters: it is the difference between a story map and a
folder of prose that looks organised.

## Keeping this folder honest

- A criterion added to a spec must be added to a story and to the coverage table.
- A story cut for time goes in `rubric-traceability.md` under **Scope cuts** — which criteria, why.
- Status moves only on evidence; red tests mean `failing`.
- When a roadmap slice is specified, its stories are **rewritten from the spec**, not edited towards
  it. Reserved rule-file numbers get their files then.

## Structure deviations from rule specification §7

Two deliberate, documented deviations: `docs/superpowers/{specs,plans}/` stay at their CLAUDE.md
SDD-gate paths rather than moving under this tree, and architecture/ADRs remain in `docs/adr/`
(their own convention) alongside the §7 `architecture/` views. Everything else follows §7:
`product/`, `requirements/{epics,user-stories}/`, `architecture/`, `non-functional/`.
