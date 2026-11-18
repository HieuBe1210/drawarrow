# Initial arrow tail correction

The requirement is enforced when constructing a 2D ArrowBoard, before any arrow is drawn or its occupancy and footprint dots are built. Each arrow's forward ray is checked against its entire original body. If it points into its own tail/body, the tail prefix is removed through the last crossing, with a short clearance margin. The head, direction and arrow count stay the same; a safe arrow is unchanged. Runtime Paths, hit testing, motion, occupancy and footprint dots all use the shortened path. Archived source level packs are unchanged.

The previous animation-only tail-retraction change has been removed. ArrowMotionPath again moves a fixed-length interval along the shortened, rounded path.

Validation uses the actual ArrowBoard and ArrowMotionPath, then independently intersects the initially rendered rounded body with a rectangle along the head's forward ray. It checks all 49,160 boards in the classic, maininteriors, escape107 and clash packs, containing 4,469,142 arrows. 74,540 arrows were trimmed and zero initial self-pointing arrows remain. Checks also cover rotated spirals, a U-shaped arrow, unchanged safe arrows, unchanged heads and idempotence of normalization. The detailed result is `arrow-initial-tail.json`.

To repeat: run `node Tools/export_motion_validation.cjs` from the project root, compile `Tools/ValidateInitialArrowTail.cs` against the current game assembly and Unity managed references, then run it with Unity's Mono runtime from the project root. The binary input is temporary and is written under `Temp/`.

Compilation passed with Unity references. This is standalone geometry validation; visual verification in Play Mode has not been performed.
