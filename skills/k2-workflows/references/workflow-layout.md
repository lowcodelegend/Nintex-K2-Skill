# Native IPC and workflow layout

Read this when workflows must remain readable and editable in the HTML5 Designer. Runtime execution, configuration-panel hydration and canvas layout need separate checks.

## Native Call Subworkflow contract

The user-authored `Example\\Parent-Single` and `Public Folder\\Parent-Multi` definitions supplied the embedded DesignerIpcFixtures.json regressions. On tested K2 build 5.1021.26292.1:

- The event has wizardId **3004**, component 30021 and UI template **SubWorkflowStep**. The owning activity also uses **SubWorkflowStep**, its native IPC deadline and decisionOptionType=2. A generic activity or missing wizard ID may compile/run while giving the wrong Designer experience.
- Static selection contains the exact workflow full name and child's display title. A Designer process ID belongs to dependency validation, not the serialized static workflow selection.
- The Folio input row uses native token `09dfaa24-ac04-4827-8082-646bc8918f80`. Inputs use mapper rows; outputs use plain Expressions with the parent field reference in TextField.designTimeValue.
- Single/wait uses synchronous=true. Multiple requires subWorkflowCallType=2 and an instance-list expression; the verified three-child example uses `A;B;C`.
- Multiple/All requires **synchronous=true and waitMode=1 together**. The runtime test continued immediately when only waitMode was set. Designer's selectWaitMode sets both flags.
- Multiple/All clears/hides the scalar return mapper. Multiple/First uses synchronous=true, waitMode=2 and restores it. None uses synchronous=false and waitMode=0. Single scalar returns and Multiple/All were live-tested; Multiple/First still needs a dedicated live scenario.
- Account stores the localized display title, not the dropdown key. The tested explicit English value is `Originator`; the native fixtures omit the optional account setting.

Verify native widgets against fixtures and the live Designer, rather than component counts alone.

## Placement and routing

Every outgoing user-task path goes through a native decision, including a one-action task. Keep all task outcomes on that one decision and reference the owning task event and outcomes. Never connect a task directly to Set Variables, IPC, End, or a retry target. This is the requested authoring convention; native compilation/runtime success does not establish compliance.

For a routing-only parent, put task interaction, business rules, persistence and result validation/recovery in stage children. The parent contains Start, subprocess calls, decisions and terminal markers. A child must return a recognized result; an unexpected result pauses for investigation inside that child rather than leaking into an unmatched parent decision. Temporary review adapters may delegate to existing stage prototypes; flatten that extra layer into the real stage implementation during the loan rebuild.

Use consistent spacing, alignment, port sides and captions across workflows. Give the normal path visual continuity; keep exceptions nearby and rework in an outside corridor. Minimize bends, crossings, repeated recovery clusters and empty canvas. Review the saved Designer layout at normal zoom: geometry checks cannot establish visual elegance.

Measure the full drawing bounds, including return loops and captions: wide empty corridors force Designer to zoom out and shrink otherwise valid labels. For action rows, prefer one caption position and one return shape throughout. Duplicate floating outcome labels can be hidden when the destination captions already identify the routes; preserve the native titles and outcome associations. Saved-coordinate previews must honor showLabel and distinguish themselves from actual Designer screenshots.

Check structure before geometry and deployment:

```powershell
python scripts/validate-workflow-structure.py --input child.json
python scripts/validate-workflow-structure.py --input router.json --routing-only
```

The check reports a topology defect without rewriting business logic. Portable one-action templates now include native decisions; older prototype generators still need the upgrade before release. The earlier prerelease package does not incorporate this correction. For a state machine, use the central child-returned NextStage contract in [state-dispatch.md](state-dispatch.md); per-stage parent decisions are not equivalent.

For a sequential child, choose the main path first and align its steps consistently. For a central state router, arrange stage calls horizontally around one dispatcher with a deliberate return corridor. Put side outcomes in separate columns. Use one native decision for all task actions. Local rejection/cancellation exits avoid long connectors to a distant shared End. Reserve different channels for bypass, rework and recovery where the chosen topology needs them.

Place captions explicitly. Tested collapsed 56px nodes anchor labels at centre x and centre y+42. `ui.labelX=-140, labelY=-42` places an ordinary caption beside a vertical-flow node; decision captions use labelY=-70 to clear side outcomes. Recheck expanded, longer and translated captions visually.

Resolve ports before routes. The native boundary is 28px from the centre; ports on a side are 14px apart. Match both endpoints to their selected ports and keep link references consistent. Separate incoming ports keep return paths editable. Name connectors for the actual business outcome.

Route around node bodies and caption rectangles with orthogonal segments. Straight links follow the native six-point convention. Backward links use a reserved corridor and a suitable target-side port. Geometry checks supplement visual review; they do not prove that crossings and labels are readable.

The optional build-time helper requires Python 3 and changes only native JSON UI coordinates:

```powershell
python scripts/layout-native-workflow.py --input workflow.json --plan layout-plan.json --output workflow-laid-out.json
python scripts/layout-native-workflow.py --input workflow-laid-out.json --validate-only
```

Plans have optional positions and channels keyed by exact native systemName. Positions accept x, y, labelX and labelY; channels select a preferred link x coordinate. The helper preserves rules, mappings, task configuration and connectivity, and refuses an existing output unless --force is explicit. It routes an already planned graph; it does not infer business flow or replace binary-decision cascades.

## Reviewed templates and pending generator port

[Native examples](../assets/examples/native-ipc/) contain portable Child, Single, Multiple/All and Three Outcomes manifests and definitions derived from the verified examples. Adapt the existing root, environment-profile authentication, reviewer identity and placeholder Form URLs. Deploy the child before its parents. Example identities/URLs require configuration before use.

The operator confirmed the Single IPC panel and Three Outcomes layout in Designer. Runtime tests verified IPC timing, child inputs/folios, scalar return and all three decision paths. The earlier full router passed its runtime rework test, but subsequent user review rejected its direct task retry links and recovery work in the parent. That router is not a visual/structural acceptance fixture. A separate routing-only parent with child-owned validation/recovery is under review.

These are reference templates for the pending generator update. The older prototype stage-task/stage-router generator still needs the approved semantic placement and single-decision task topology port. Use the checked expert JSON examples while preparing that port; routing an old graph alone does not adopt the reviewed topology.

Promotion requires the native widget contract, runtime parent/child checks, saved-definition equality, port/geometry checks and Designer confirmation. Retain examples requested for review; disposable-test cleanup must preserve them.
