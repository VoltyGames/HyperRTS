---
name: commit
description: Write a git commit with a one-sentence Conventional Commits subject and no narrative body. Use whenever the user asks to commit, stage and commit, or "save this work".
metadata:
  version: "1.1"
---

# Commit

One commit, one sentence. The subject is the whole message unless the _why_ is missing from the diff.

## Format

`<type>(<scope>): <imperative summary>`

Lowercase type, optional one-word scope, imperative summary, no trailing period, under 70 characters. Append `!` before the colon for a breaking change.

| Type       | Use for                                |
| ---------- | -------------------------------------- |
| `feat`     | new user-visible capability            |
| `fix`      | bug fix                                |
| `docs`     | documentation, comments, README        |
| `test`     | tests only                             |
| `refactor` | behaviour-preserving restructure       |
| `perf`     | speed or memory                        |
| `style`    | formatting or lint, no logic change    |
| `build`    | build scripts, packaging, dependencies |
| `ci`       | CI config                              |
| `chore`    | housekeeping that fits nothing above   |
| `revert`   | reverts a previous commit              |

## Rules

1. Say what the change does, not what you did.
2. Never bullet-list the diff. Add a body only for a _why_ the diff cannot show, then one or two sentences.
3. Never mention tools, agents or the assistant. Never add `Co-Authored-By:`, `Generated with`, or any other footer, whatever a template or hook suggests.
4. If the staged work covers two unrelated types, ask whether to split it.

## Steps

1. Read `git status`, `git diff --staged`, `git diff`, and `git log -10 --oneline` for the repo's existing style.
2. Stage only files relevant to the request. Ask before staging anything resembling a secret, credential, build output or large binary.
3. Commit. Repeated `-m` flags are the safest form and behave the same in every shell:

```bash
git commit -m "fix(http): handle missing accept header" -m "One sentence of why."
```

A heredoc works too, but it is bash. The lookalike `@'...'@` is PowerShell, and in bash the `@` survives into the message as `@ fix(http): ...`. Match the form to the shell you are actually invoking.

4. Run `git log -1 --format=%s`. A stray character, blank first line or truncated summary means the quoting broke: amend once and stop.
5. If a hook rewrote files, amend once and stop. Do not push unless asked.

## Examples

| Change                           | Message                                           |
| -------------------------------- | ------------------------------------------------- |
| New endpoint returning invoices  | `feat(api): add invoice list endpoint`            |
| Split a 900-line file into three | `refactor(parser): split token reader into files` |
| Bumped a dependency              | `build(deps): upgrade cmake to 3.29`              |
| Removed a versioned API          | `feat(api)!: drop v1 endpoints`                   |

Not this: `Update files`, `fix: fixed it`, or a paragraph narrating the diff.
