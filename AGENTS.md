# Repository instructions

## Documentation versioning

- The user requires previous documentation to be archived before updates.
- Use the exact repository folder name `archiveedDocs`.
- Before editing existing `README.md` or anything under `docs/`, copy the complete
  current `README.md` and `docs/` tree into a new, immutable archive folder named
  `archiveedDocs/vNNN_YYYY-MM-DD/`. Use the current version from `docs/VERSION.md`
  and the user's America/Chicago date. Never overwrite an existing archive.
- Verify copied files match their originals before editing the active documents.
- Keep current documents at their existing paths to preserve links. Increment the
  documentation-set version in `docs/VERSION.md` and record the update there.
- One snapshot/version covers one coherent documentation update, including its
  revisions and verification. Do not archive each intermediate edit separately.
- Update `archiveedDocs/README.md` to register the archived version. The archive
  index is bookkeeping, not part of the documentation set; never recursively
  archive `archiveedDocs`, build outputs, or application source.
- Do not edit historical snapshots to fix old content or links. Explain the
  correction in the new active version. Preserve the original engineering kickoff.
- New documentation files join the new active version; do not invent older copies.

See `docs/DOCUMENTATION_VERSIONING.md` for the full convention.
