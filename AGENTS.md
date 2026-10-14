# Project Rules

## Project overview

- An arrow game built with Unity **6000.6.0f1**. Render pipeline: URP (Universal Renderer, `PC_Renderer` / `Mobile_Renderer`).
- The game design is not defined yet. When it is, describe the core loop, main scene and system wiring here.
- There is no scene yet. When the main scene is created, name it here and add it to the Build Profiles scene list.
- Game scripts live in the `ArrowGame.Gameplay` namespace; editor tools live in `ArrowGame.EditorTools` under `Assets/Editor/`.

### Folder structure

| Folder | Contents |
|---|---|
| `Assets/Scripts/<Feature>/` | Scripts grouped by feature |
| `Assets/Data/` | The project's ScriptableObject assets |
| `Assets/Prefabs/` | Prefabs |
| `Assets/Materials/`, `Assets/Shaders/` | Materials and shaders |
| `Assets/Audio/` | Audio files |
| `Assets/UI/` | UXML, USS, Panel Settings, fonts, themes and UI icons |
| `Assets/Scenes/` | Scenes; `Assets/Scenes/Dev/` is only for development and preview scenes |
| `Assets/Settings/` | Unity/URP project settings assets only; game data does not go here |
| `Assets/Editor/` | Editor-only tools, not included in builds (e.g. `ProjectValidator.cs`) |
| `Tools/agent/` | Helper scripts for agents (outside `Assets/`, so Unity does not import them) |

- Put a new script in the matching feature folder; if none fits, create a new feature folder.

## Communication

- Reply in the language the user writes in. Keep sentences short, clear and simple.
- The user does not write code. The agent does all setup, including Inspector wiring.
- Do not ask the user code questions; when a technical decision is needed, explain the options in plain language and state your recommendation.

## Working style

- Keep changes small and focused on the request so they are easy to revert. Do not add unrequested improvements or refactors; ask before a larger change.
- At the end of each task, report:
  - which files changed,
  - which new Inspector fields were added (name and default value),
  - how to test it in Play Mode, in short steps.
- Keep this file up to date: when the folder structure or a rule changes, update `AGENTS.md` in the same task.

## Unity scene and prefab dependencies

- Do not create or look up in code the dependencies that are visible in the Inspector and must be wired by hand. Declare them explicitly with `[SerializeField]` and wire them by drag and drop in the scene/prefab. This rule also applies to the operations the agent performs through the Unity Editor.
- If a required reference is not assigned, do not hide it with `Debug.Log`, an automatic `AddComponent`, a `GetComponent` assumption or a silent fix. Do not add runtime null checks, disable the component or throw a custom exception; a missing reference must produce a visible error at the point of use. Do not repeat the same checks in frequently called methods such as `Update()`.
- The `Debug.Log` ban only applies to hiding missing references. `Debug.Log` may be used when logging is needed for another purpose.
- The same rule applies to cameras: when a camera field is `null`, do not assign `Camera.main` or use any other fallback.
- Because `OnValidate()` can run before the Inspector setup is complete, it may only `return` on missing visual references. This is an editor setup exception; at runtime the reference is used directly.
- Code may only create game objects that genuinely need to be generated dynamically. Silently compensating for an Inspector dependency does not count as dynamic generation.
- Changing a serialized field's default value in code does not change the value already saved in scenes and prefabs. To change the value the game actually uses, change it in the scene or prefab.
- When a change should apply to every instance, edit the prefab asset itself. Use scene instance overrides only on purpose.
- When a serialized field is renamed, temporarily add `[FormerlySerializedAs("oldName")]` so scene/prefab values are not lost. Remove the attribute once the affected scenes and prefabs have been saved with the new name.

## Static visual surfaces

- Do not generate a `Mesh` at runtime for decorative backgrounds or other static surfaces. Use Unity's built-in meshes (`Quad`, `Plane`, etc.) or an explicit mesh asset in the project.
- The size, position, `MeshFilter`, `MeshRenderer` and material assignment of these surfaces are defined explicitly in the scene/prefab.
- Runtime mesh generation is only allowed when the geometry is truly dynamic: when vertices/topology change during gameplay or no suitable static mesh exists.

## Visuals and shaders

- The project uses URP. New shaders are written in HLSL for URP (`HLSLPROGRAM`, `"RenderPipeline" = "UniversalPipeline"`, URP `Core.hlsl`). Do not use the built-in pipeline's `CGPROGRAM` / `UnityCG.cginc`.
- Use `MaterialPropertyBlock` to give objects that share a material different per-object colors or values. Do not create material copies through `renderer.material` for this.

## UI development

- For Unity 6000.6 runtime UI, use `Panel Renderer + UXML + USS + Panel Settings`; do not use `UIDocument` for new UI.
- Link USS files through UXML/UI Builder; put UI behavior in C# scripts.
- Access UI elements through the root passed by `PanelRenderer.RegisterUIReloadCallback`. `PanelRenderer` has no `rootVisualElement`.
- Views keep UXML element names in `[SerializeField] string` fields whose names end with `Name` (`...ButtonName` for a `Button`, `...LabelName` for a `Label`); USS class names end with `ClassName`.
- References: [Panel Renderer](https://docs.unity3d.com/6000.6/Documentation/Manual/ui-systems/panel-renderer-component.html), [Adding USS to UXML](https://docs.unity3d.com/6000.6/Documentation/Manual/UIE-add-style-to-uxml.html), [Panel Settings](https://docs.unity3d.com/6000.6/Documentation/Manual/UIE-Runtime-Panel-Settings.html).

## Input and Unity 6 API

- The project is set to the new Input System only (Active Input Handling: Input System Package). The legacy Input Manager API (`Input.GetKey`, `Input.mousePosition`, etc.) throws at runtime; use `UnityEngine.InputSystem` (`Mouse.current`, `Keyboard.current`, `InputAction`). The project's actions are in `Assets/InputSystem_Actions.inputactions`.
- Do not use APIs that are obsolete in Unity 6. Common cases:

| Old | New |
|---|---|
| `Rigidbody.velocity` / `Rigidbody2D.velocity` | `linearVelocity` |
| `Rigidbody.drag` / `Rigidbody2D.drag` | `linearDamping` |
| `Rigidbody.angularDrag` / `Rigidbody2D.angularDrag` | `angularDamping` |
| `FindObjectOfType` | `FindAnyObjectByType` / `FindFirstObjectByType` |

- If you are unsure whether an API exists or what its current name is, check it with reflection in the installed Unity version before using it.

## Unity CLI usage

- Unity Editor operations are done with the `unity command` CLI: scene, GameObject, component and Inspector edits, compilation, asset import and reading the Console.
- Do not hand-edit scene or prefab files as raw text; make changes through the Unity Editor.
- For fields that `set_component_properties` cannot assign, such as structs or arrays, use `SerializedObject` inside `eval`.
- Before editing a scene, check that the Editor is not in Play Mode (`EditorApplication.isPlayingOrWillChangePlaymode`). Scene changes made in Play Mode are lost.
- After editing a scene, save it (`EditorSceneManager.SaveScene`); unsaved scene changes are lost.
- An `eval` call may have completed even if it timed out. Check the result on the file system or in the Editor before running it again.
- `AssetDatabase.SaveAssets` can re-save unrelated assets (e.g. files in `Assets/Settings/`). Check with `git status` after the work and revert unwanted changes.
- After a change, verify it with `Tools/agent/unity-check.py` (see Verification).

## Asset operations

- Move, rename and delete assets with `AssetDatabase.MoveAsset` / `RenameAsset` / `DeleteAsset`. This keeps `.meta` files and GUID references intact.
- Never use `rm -rf` in project folders.
- Do not save screenshots or temporary output inside `Assets/`; Unity imports them as assets.
- Before deleting an asset, check that it is not referenced by a scene, prefab or project setting.

## C# code style

- Classes are `sealed` unless they are meant to be inherited.
- In MonoBehaviours and ScriptableObjects, Inspector fields are `[SerializeField] private` and named in camelCase without a `_` prefix (e.g. `shotCooldown`). Expose them to other classes through properties when needed, not public fields.
- Gameplay tuning values (speed, duration, volume, counts, etc.) are not hard-coded constants; they are serialized Inspector fields so the user can tune the game without code.
- Keep game balance values in a single config ScriptableObject under `Assets/Data/` instead of spreading them across components. Visual and audio tuning stays on the component that uses it.
- Limit tuning fields with `[Min]` or `[Range]` (e.g. `[SerializeField, Min(0.01f)]`, `[SerializeField, Range(0f, 1f)]`).
- Comments are in English and explain why, not what. Only comment where the reason is not obvious from the code.

## C# member and method order

- Class members are defined in this order: Fields (serialized and private fields) ➔ Properties ➔ Unity lifecycle methods ➔ Public methods ➔ Private helper methods.
- Lifecycle methods called automatically by Unity (`Awake`, `OnEnable`, `Start`, `Update`, `FixedUpdate`, `LateUpdate`, `OnDisable`, `OnDestroy`, `OnValidate`, etc.) sit at the top of the methods block.
- When there are several lifecycle methods, keep Unity's logical call order; other public and private methods are defined below them.

## Git

- Commit and push only when the user asks.
- Each topic gets its own commit; do not put unrelated changes in the same commit.
- Commit messages are in English, short and in the imperative mood (e.g. `Add arrow trail effect`).
- Dynamic font atlas changes in `Assets/UI/Fonts/*-SDF.asset` files are generated automatically by Unity; do not commit them unless the user asks.

## Verification

- Do not add automated tests.
- Run `Tools/agent/unity-check.py mark` before changing the project and `check` after. Do not report a task as done while `check` shows problems. Revert any changed file that the task did not mean to change.
- `Tools/agent/code-guard.py` catches code patterns forbidden by these rules. It runs automatically after Claude Code edits (`.claude/settings.json` hook) and inside `check`. Fix the violation; add `// agent-guard: allow <reason>` to a line only for a real exception.
- `check` also runs the Unity project validator (Tools > ArrowGame > Validate Project), which reports unassigned or empty Inspector fields on game scripts, missing scripts, broken references and prefab overrides in scenes. Fix its errors; confirm that reported overrides are intentional.
- The agent does not run Play Mode tests; the user does the play testing. After `check` is clean, tell the user in short steps what to test in Play Mode and how.
