# Harder main-campaign milestones

Applied only to main levels 5, 10, 15, ..., 1570: 314 boards. All 22,229 other main-level mappings and all 412 pre-existing level packs are unchanged. Other game modes keep their existing mappings.

Every selected board has 25-40% more arrows (integer counts), and a longer dependency chain than its frozen baseline. Total additions: 6,431 arrows. Of those, 6,307 are placed in unused cells. On crowded boards, 124 additions come from splitting long arrows, avoiding a larger grid or a smaller display scale. Original heads and board bounds are retained. Paths are normalized before generation so no original self-pointing tail is reintroduced.

New assets are the three `Assets/Resources/Levels/milestonehard_*.bytes` packs. The catalog routes only the selected main levels to this group. The baseline, including original mappings and level data, is saved in `Recovery/MilestoneDifficulty/baseline.bytes`. Regeneration always starts from this baseline, so rerunning does not compound the arrow count.

## Verification

- All 314 boards passed the actual ArrowBoard solver and replayed their complete solutions using runtime Remove().
- 906,322 checks cover arrow-count range, board bounds, original heads, path length, overlapping occupancy cells and self-pointing rays against the rounded initial render geometry.
- None of the generated arrows needs runtime tail correction.
- Existing grid dimensions and content bounds are unchanged, so cell and stroke sizes are not reduced by adding these arrows. The smallest cell is about 7.94 physical pixels in a 540-pixel-wide reference layout, the same scale as its baseline. Legibility on a real device has not been verified.
- Runtime scripts and Editor validation scripts compile. Editor compilation retains the existing obsolete PlayerSettings icon API warning.
- No direct Play Mode or on-device verification was performed.

The generation, runtime validation and application reports are separate JSON files in this directory. The existing interior-variant validator accounts for the 311 interior variants superseded by this milestone group.

## Repeating the workflow

1. Run `node Tools/build_milestone_difficulty.cjs` to stage candidates and the binary fixture under `Temp/MilestoneDifficulty/`.
2. Compile `Tools/ValidateMilestoneDifficulty.cs` against the current game assembly and Unity managed references; run it using Unity's Mono runtime from the project root.
3. Run `node Tools/apply_milestone_difficulty.cjs`. It requires a passing runtime validation report whose SHA-256 matches the staged candidates, then checks unchanged mappings and source packs before saving the catalog.

Examples: level 5 increases from 5 to 7 arrows; level 10 from 7 to 9; level 20 from 10 to 13. Every selected board, including level 1570, was validated.
