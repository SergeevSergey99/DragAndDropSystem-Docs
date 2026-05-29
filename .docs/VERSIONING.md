# Versioned docs with `mike`

This site uses [`mike`](https://github.com/jimporter/mike) to publish multiple
documentation versions side by side. The version dropdown shows up in the header
next to the existing language switcher (English / Русский / Español).

`mike` and `mkdocs-static-i18n` are **orthogonal**:

- `mkdocs-static-i18n` builds **all languages in a single `mkdocs build`**.
- `mike` wraps that whole multi-language build into a per-version folder on the
  `gh-pages` branch.

So a published URL looks like:

```
/<repo>/<version>/<lang>/<page>/
        ^^^^^^^^^  ^^^^^^
        mike       i18n
```

---

## Mental model: what you maintain vs what gets published

| | |
|---|---|
| **Source (`content/`)** | One single set of files, all languages — exactly as today. You never duplicate it per version. |
| **Published (`gh-pages`)** | `mike` stores a **full independent snapshot** of the rendered site under each version folder (`/1.0/`, `/2.0/`, `/latest/`). This is generated output, not something you hand-edit. |

When you run `mike deploy 2.0`, mike takes the **current** state of `content/`,
runs a normal build (all languages at once), and drops the result into the
`2.0/` folder. Previously deployed versions stay frozen as they were.

### There is no "diff/overlay" between versions

`mike` cannot "inherit version 1.0 and override only the changed files". Every
published version is a **complete, self-contained snapshot**. What you do instead:

- Edit only the pages that changed — **in the single `content/` tree**.
- Run `mike deploy <version>` — mike rebuilds the whole site from the current
  source and publishes it as that version. Unchanged pages come along
  automatically; you don't copy anything by hand.

Consequence: a fix you want in **both** 1.0 and 2.0 must be deployed to **both**
(re-deploy 1.0 from its updated source). Old versions are otherwise considered
frozen — which is the normal expectation for versioned docs.

---

## One-time setup (already done in this repo)

- `mkdocs.yml` → `extra.version.provider: mike` (enables the dropdown,
  `alias: true`, `default: latest`).
- `content/javascripts/lang-switch-prefix-fix.js` recovers the deployment base
  (repo **and** mike version segment) and prepends it to the language/hreflang
  links, so switching language keeps you in the same version.
- `requirements.txt` pins `mike` alongside the mkdocs stack.

> **Why no `site_url`:** on a GitHub project site the language links are
> root-absolute and miss the `/<repo>/<version>` base. We rely on the JS hack to
> patch them at runtime rather than on `site_url`, which keeps the hack working
> regardless of the deploy path. Do not add `site_url` without re-testing the
> language switch under a deployed version.

---

## Install

The venv on this machine is Windows-layout (`.venv/Scripts/`). From `.docs/`:

```bash
.venv/Scripts/pip.exe install -r requirements.txt
```

(or `pip install mike` if you only need to add it to an existing env).

---

## Daily workflow

### 1. Write docs normally

Edit files under `content/` (all languages). Preview the *current* (unversioned)
build the usual way:

```bash
.venv/Scripts/mkdocs.exe serve
```

### 2. Cut a version on release

Tag the release in git first so the version is reproducible, then deploy:

```bash
# publish the current content as version "1.0" and also update the "latest" alias
.venv/Scripts/mike.exe deploy --push --update-aliases 1.0 latest

# make "latest" the page visitors land on at the site root (run once, or when it changes)
.venv/Scripts/mike.exe set-default --push latest
```

- `1.0` — the concrete version folder.
- `latest` — a moving alias that always points at the newest release.
- `--push` — commits to `gh-pages` **and** pushes to the `gh-pages` remote.
  Drop it to deploy locally first and inspect, then push manually.
- `--update-aliases` — lets `latest` be re-pointed to this deploy.

### 3. Preview the versioned site locally (optional)

```bash
.venv/Scripts/mike.exe serve
```

This serves the `gh-pages` branch with the version dropdown live — use it to
confirm the language switch behaves under a version segment before pushing.

---

## Useful commands

| Command | What it does |
|---|---|
| `mike list` | List all deployed versions and aliases |
| `mike deploy --push 2.0 latest --update-aliases` | Publish 2.0 and move `latest` onto it |
| `mike set-default --push latest` | Choose which version `/` redirects to |
| `mike alias --push 2.0 stable` | Add another alias (e.g. `stable`) to a version |
| `mike retitle --push 1.0 "1.0 (legacy)"` | Change the label shown in the dropdown |
| `mike delete --push 0.9` | Remove an old version from `gh-pages` |
| `mike serve` | Local preview of the full versioned site |

All write commands accept `--push`; without it they only update the local
`gh-pages` branch so you can review before pushing.

---

## Recommended release recipe

```bash
# 1. finish + commit doc changes on your working branch
# 2. tag the release
git tag docs-v2.0

# 3. publish the new version and advance "latest"
cd .docs
.venv/Scripts/mike.exe deploy --push --update-aliases 2.0 latest
```

To rebuild an **old** version (e.g. backport a fix into 1.0):

```bash
git checkout docs-v1.0   # bring content/ back to the 1.0 state (+ your fix)
cd .docs
.venv/Scripts/mike.exe deploy --push 1.0
git checkout -            # return to your branch
```

---

## Gotchas

- **Old versions are frozen.** Re-deploy a version explicitly to change it.
- **Switching version may drop you at the version root**, not the same page —
  this is mike's default selector behavior, independent of the language hack.
- **First deploy bootstraps `gh-pages`.** If the branch was previously published
  with plain `mkdocs gh-deploy` (a flat, unversioned site), the first
  `mike deploy` restructures it into version folders. Back up / confirm before
  the first run; afterwards always deploy through `mike`, not `mkdocs gh-deploy`.
- **Don't add `site_url`** without re-testing the language switch — see the note
  in the one-time-setup section.
