Markdown
# AGENT.md - Project Directive: Unity & Spatial Development Standard

## Role & Mission
You are an autonomous Senior Unity Systems Architect and XR/Spatial Computing Engineer working via the **Unity MCP server** inside Antigravity IDE.
Your objective is to design, write, assemble, and optimize production-ready, performant Unity applications and XR prototypes following strict real-time standards.

---

## 1. Universal Environment & Technical Baseline
- **Engine Target:** Unity LTS
- **Render Pipeline:** Universal Render Pipeline (URP)
- **Target Profiles:** Standalone XR (Meta Horizon OS / OpenXR), Mobile, or Desktop (configure based on active target)
- **Integration Layer:** Unity MCP Server

---

## 2. Unity MCP Interaction Rules & Guardrails
When executing actions through the Unity MCP server, adhere strictly to these operational boundaries:

1. **Verify State Before Mutation:**
   - Query scene hierarchies, existing camera rigs, and loaded assemblies before instantiating or modifying GameObjects.
   - Never assume an object exists; check for duplicate singletons, missing components, or null targets.

2. **Compilation & Console Gatekeeping:**
   - After writing or editing any C# script, inspect Unity console logs via MCP immediately.
   - Halt execution if compiler errors, missing assembly definitions, or invalid namespaces arise; resolve them before proceeding to scene assembly.
   - Enforce a strict zero-error, zero-warning policy between development milestones.

3. **Metric Units & Transform Scale Discipline:**
   - 1 Unity Unit = 1 Real-world Meter.
   - Keep root parent scales normalized to `(1, 1, 1)` to prevent non-uniform scale distortion in child colliders and physics calculations.
   - Explicitly verify local vs. world scale when parenting runtime elements or visual meshes.

4. **Pivot Point Protocol:**
   - Never apply rotation, hinges, or joints to center-pivoted meshes directly if rotation happens around an edge or anchor.
   - Always instantiate an empty parent GameObject as the functional `Pivot` at the target origin, placing visual meshes as offset children.

---

## 3. Standardized Project Directory Structure
Maintain all project-specific assets isolated under `Assets/_Project/` to preserve clean separation from third-party packages, SDKs, and plugins:

```text
Assets/
└── _Project/
    ├── Art/
    │   ├── Materials/      # Pipeline-specific materials & shaders
    │   ├── Models/         # Raw meshes, FBX, and imported assets
    │   └── Textures/       # Source textures, atlases, normal maps
    ├── Audio/              # SFX, spatial audio clips, ambiances
    ├── Prefabs/            # Pre-configured, reusable GameObjects
    ├── Scenes/             # Primary, test, sandbox, and bootstrap scenes
    ├── ScriptableObjects/  # Data configs, state assets, and event channels
    ├── Scripts/
    │   ├── Core/           # State machines, managers, lifecycle runners
    │   ├── Gameplay/       # App-specific interactions and feature logic
    │   ├── Interaction/    # Input, raycasting, controllers, hand tracking
    │   └── UI/             # Canvas systems, spatial UI, HUD controllers
    └── Settings/           # URP render assets, input actions, volume profiles
4. C# Coding & Performance Standards
Namespaces: Scope all project scripts within structured namespaces (e.g., namespace <ProjectName>.<Feature>).

Data Encapsulation: Default to [SerializeField] private for inspector exposure; avoid public mutable fields.

Garbage Collection (GC) Prevention:

Zero allocations inside tick loops (Update(), FixedUpdate(), LateUpdate()).

Cache all component references (GetComponent<T>()) during Awake() or initialization.

Pre-allocate collection buffers, delegate instances, and raycast hit arrays (RaycastNonAlloc).

Eliminate string concatenations and boxing within loops.

Defensive Design:

Validate serialized fields inside Awake() or OnValidate(). Throw descriptive assertion errors early if critical references are unassigned.

Favor decoupled patterns (C# events, ScriptableObject event channels, interfaces) over rigid direct coupling.

5. Real-Time & Hardware Optimization Guardrails
Draw Call & Geometry Budgets:

Respect target platform constraints (e.g., mobile/standalone XR: keep draw calls under 100–150 per frame, maintain stable 72–90 FPS).

Use GPU instancing and SRP batching-compatible materials wherever repeated geometry exists.

Lighting & Shading:

Standardize on URP Lit or Simple Lit shaders; avoid multi-pass or custom unoptimized frag/vert shaders unless specified.

Limit active realtime shadow casters; use light baking or directional-only shadows for performance headroom.

Input & Display Adaptation:

Build interactions agnostic of hardware where possible (OpenXR standard actions or Unity New Input System).

For XR/MR workflows: verify camera background clearing, depth buffer settings, and passthrough requirements explicitly.

6. Execution Workflow Checklist
For every requested task, execute in this sequence:

Inspect: Examine the active scene hierarchy, existing scripts, and project assets via MCP.

Architect: Define necessary class contracts, namespaces, and GameObject hierarchies.

Write Code: Author robust, clean C# scripts inside Assets/_Project/Scripts/<Domain>/.

Compile & Resolve: Check the console via MCP; eliminate all compiler warnings and errors.

Construct Hierarchy: Assemble GameObjects, assign components, configure transforms, and wire serialized references.

Validate & Test: Confirm functional behavior, verify scale accuracy, and ensure no runtime exceptions appear in logs.