# Interior gap filling, main levels 1-1570

This pass addresses large empty regions rather than only increasing arrow counts. It changes 915 main-campaign boards, including 239 X5/X0 milestones, and adds 7,026 arrows on top of the previously applied milestone variants. Already dense boards are kept on their current mappings. The earlier 25-40% increase is not used as an upper limit for this additional gap-filling request.

An interior mask is derived from the existing row and column extents, retaining the outside silhouette and the board bounds. The generator searches the largest empty rectangles first. It adds longer straight or L-shaped arrows, up to 49 path cells, preferring cells far from existing strokes. Milestones receive stricter empty-area and density targets. New arrows must participate in existing blocking dependencies without creating cycles. No extra splitting or grid refinement is used in this pass.

Level 600: 57 -> 83 arrows. Largest empty rectangle inside the interior mask: 75 -> 10 grid cells (86.7% reduction). Interior occupancy: 60.8% -> 91.2%. See `gapfill-level600-preview.png` for a standalone geometry comparison rendered from the actual runtime paths; it is not a Play Mode screenshot.

Validation covers all 1,570 staged boards using the actual ArrowBoard and ArrowMotionPath. All solve fully and replay their complete solutions successfully. 4,527,964 checks passed, including unchanged bounds and heads, non-overlapping occupancy, no initial self-pointing rounded paths, and no need for additional runtime tail trimming. Source grid size is unchanged, so this pass does not shrink cell or stroke sizes. Direct Play Mode and on-device touch verification remain unperformed.

Only the 915 changed main-level mappings point to the new `gapfilled` group. All 21,628 other main-level mappings, all other mode mappings and the 415 existing source packs are unchanged. The 13 new packs contain the staged boards, and their content is validated against the staged JSON before application. Original baseline levels and mappings are archived in `Recovery/GapFill/baseline.bytes` to prevent repeated increases on regeneration.

Reproduction:

1. `node Tools/build_gapfilled_levels.cjs`
2. Compile/run `Tools/ValidateGapFilledLevels.cs` against the current game assembly and Unity managed references using Unity's Mono runtime.
3. `node Tools/apply_gapfilled_levels.cjs` (requires a passing runtime report with the candidate SHA-256).

Generation metrics, validation evidence and application scope are in `gapfill-generation.json`, `gapfill-validation.json` and `gapfill-applied.json`. The older interior-variant validator now accounts for the additional 676 interior variants superseded by this pass.
