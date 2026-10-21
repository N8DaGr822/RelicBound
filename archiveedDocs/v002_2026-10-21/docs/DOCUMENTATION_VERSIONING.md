# Documentation versioning policy

The user requires old documentation to be archived and a new version created
when documentation is updated. This repository uses versioned snapshots of the
documentation set, while keeping the latest files at stable paths.

## Layout

```text
README.md                         Current project entry point
docs/                             Current documentation
    VERSION.md                    Current documentation-set version
archiveedDocs/
    README.md                     Archive index
    v001_2026-10-21/
        README.md                 Previous project entry point
        docs/                     Previous documentation, unchanged
```

`archiveedDocs` is intentional spelling. Versions increase monotonically: v001,
v002, v003, and so on. The date in an archive folder is the date the snapshot was
created in America/Chicago, not necessarily its original authoring date. Multiple
updates on one day get distinct version numbers.

## Required update workflow

1. Read `docs/VERSION.md` to identify the active version.
2. Before editing existing documentation, copy the root `README.md` and entire
   `docs/` tree into `archiveedDocs/vNNN_YYYY-MM-DD/` using that active version.
   Refuse to overwrite a folder that already exists. If it belongs to an update
   already in progress, verify it and resume that update instead of replacing it.
3. Verify the snapshot matches its source, using file hashes or equivalent
   byte-for-byte checks. Only then edit the active documentation.
4. Make the coherent update at the existing paths, including any new documents.
   Increment the version in `docs/VERSION.md`, record the date and changes, and
   register the previous version in `archiveedDocs/README.md`.
5. Validate active local links and review statements against code/test evidence.
   Archive revisions are not proof of application verification; record only checks
   actually performed. Do not rerun application tests solely for a prose edit.

One version covers a coherent update, including corrections made while drafting
and verifying it. It does not create a snapshot for each intermediate save.
Future separate updates, including corrections, archive the then-current set first.
Brand-new documents enter the new version; no fictional previous version is created.

## Preservation rules

- Treat historical snapshots as immutable. Correct old mistakes in the new version.
- Preserve the original engineering kickoff as reference material.
- Do not archive the archive itself, application source, binaries, credentials,
  or generated build directories. Archive-index bookkeeping is outside the set.
- Keep current filenames stable so existing links and bookmarks still work.
- Archive source-code links keep their original relative text and may not resolve
  within the snapshot. Consult the matching repository source path when needed;
  these archives preserve documentation, not a versioned code checkout.

The root [AGENTS.md](../AGENTS.md) records this rule for future engineering sessions.
The workflow is a repository convention, not an automatic filesystem watcher.
