# Domain Docs

How engineering skills should consume this repository’s domain documentation when exploring the codebase.

## Before exploring, read these

- **`CONTEXT.md`** at the repository root.
- **`CONTEXT-MAP.md`** at the repository root if it exists; it points to context-specific `CONTEXT.md` files.
- **`docs/adr/`** for decisions touching the area being changed.

If these files don’t exist, proceed silently. The domain-modeling skill creates them lazily when terminology or decisions are resolved.

## File structure

This repository uses a single-context layout:

/
├── CONTEXT.md
├── docs/adr/
│   ├── 0001-example-decision.md
│   └── 0002-another-decision.md
└── Openthesia/

## Use the glossary’s vocabulary

When output names a domain concept—in an issue title, refactor proposal, hypothesis, or test—use the term defined in `CONTEXT.md`. Don’t drift to synonyms that the glossary explicitly avoids.

If a needed concept isn’t defined, reconsider whether it belongs to the project’s language or note the gap for domain modeling.

## Flag ADR conflicts

If proposed work contradicts an existing ADR, surface the conflict explicitly instead of silently overriding it.
