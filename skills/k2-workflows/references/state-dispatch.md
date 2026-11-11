# Central state dispatcher

Use this pattern when any stage may choose any next stage, as in NAC's state machine. A sequence of per-stage transition decisions is a different contract.

The parent has RequestId and NextStage, one central decision, one synchronous native IPC per stage, a terminal End and an optional recovery-child call. Each child receives RequestId and returns scalar NextStage into the same parent field. Every child connector returns to the dispatcher. Only the dispatcher chooses the next child; no stage-to-stage connectors or stage-specific business conditions belong in the parent.

Children own task decisions, business rules, data/status/audit operations, integrations and next-stage selection. Task actions first enter one native decision carrying every outcome, including single-action tasks. Return exact stage keys or declared terminal values. Keep invalid-stage investigation in a child with explicit user action; avoid silent termination or automatic retry loops.

The checked [native JSON templates](../assets/examples/state-dispatch/) use six loan stages plus Completed, Rejected and Cancelled. The parent recognizes all six destinations after every child, independent of its originating stage. Blank initial NextStage selects Review. The sample stage rules reproduce the reviewed transition map; future stage logic can return any supported destination without changing the parent.

For a horizontal canvas, keep a compact, evenly spaced action row below the dispatcher. Use the same caption position and exit shape for every subprocess; do not alternate label sides or route one child differently just to avoid its caption. The current template uses 224px columns, centered captions below the nodes, a 20px downward exit and a 112px right jog before the return rail. Terminal and recovery calls share the action row. These are tested template dimensions, not universal limits for every workflow.

Ordered orthogonal dispatch fans feed the row. The saved native templates merge returns at the dispatcher's incoming port; incomingLinkReferences contains every merged link. Shared geometry is intentional only for these same-direction return links. Keep unrelated routes separate. Destination captions make the row readable without a second set of floating connector labels; retain exact connector titles/outcome rules even when showLabel=false. Keep recovery interaction inside its child. Review Designer fit-to-window readability, caption overlap and drawing bounds as well as geometric validity.

Deploy children and recovery before the parent. These are json-file templates, not a newly implemented CLI manifest kind. Adapt the environment profile, category, identities and Form URLs. The sample tasks are prototypes; business persistence and real Form task integration remain separate work.

```powershell
python scripts/validate-workflow-structure.py --input router.definition.json --state-router
python scripts/validate-workflow-structure.py --input stage-credit.definition.json
```

Check saved-definition equality, native IPC panels, every entry stage, child-returned jumps/rework, terminal values, unknown-stage recovery and correlation. Geometry checks do not replace Designer acceptance. Preserve the intentional return bus when reflowing; a generic independent-link router need not preserve that structure.

The generic C# stage-router/stage-task builders still need this contract and layout port. The earlier stage-router prototype and prerelease package do not implement the central NextStage pattern.
