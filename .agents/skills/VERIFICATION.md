# Compilation And Test Verification

**Last Updated**: 2026-06-13

Use this procedure after changing runtime code, editor code, tests, examples, serialized assets,
or assembly definitions.

## Working Directories

The plugin repository is:

```text
Assets/Plugins/UniversalDragAndDrop
```

Unity-generated `.csproj` files are located at the Unity project root:

```text
/mnt/e/UnityProjects/UniversalDragAndDropAsset
```

Run `dotnet build` from the Unity project root, not from the plugin directory.

## Compilation

Run these builds sequentially:

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build UDND.Runtime.csproj --no-restore
'/mnt/c/Program Files/dotnet/dotnet.exe' build DragAndDropSystem.Tests.Editor.csproj --no-restore
'/mnt/c/Program Files/dotnet/dotnet.exe' build UDND.Examples.csproj --no-restore
```

Required result for each command:

```text
0 Warning(s)
0 Error(s)
```

Build additional projects when the changed files belong to them, for example:

```bash
'/mnt/c/Program Files/dotnet/dotnet.exe' build UDND.Editor.csproj --no-restore
'/mnt/c/Program Files/dotnet/dotnet.exe' build UDND.Examples.Craft.Editor.csproj --no-restore
'/mnt/c/Program Files/dotnet/dotnet.exe' build UDND.Examples.Minecraft.Editor.csproj --no-restore
```

Do not use `dotnet test`: these are Unity test assemblies, not standalone .NET test projects.

### Generated Project Caveat

Unity owns the `.csproj` files. After adding, deleting, or moving `.cs` files, the generated project
may be stale until Unity refreshes the project files.

- Do not edit generated `.csproj` files as the permanent fix.
- If an old deleted file is still explicitly included, let Unity refresh before relying on the build.
- If a new file is missing from the project, compilation success does not prove that file compiles.
- When this happens, report the stale generated project explicitly.

## Unity EditMode Tests

Check `ProjectSettings/ProjectVersion.txt` and use the matching Unity executable. The current project
uses Unity `2022.3.62f3`.

Run a focused fixture first:

```bash
'/mnt/c/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' \
  -batchmode \
  -nographics \
  -projectPath 'E:\UnityProjects\UniversalDragAndDropAsset' \
  -runTests \
  -testPlatform EditMode \
  -testFilter 'UDND.Tests.Inventories.ShapedItemPlacementTests' \
  -testResults 'E:\UnityProjects\UniversalDragAndDropAsset\Temp\udnd-editmode-tests.xml' \
  -logFile - \
  -quit
```

Replace `-testFilter` with the narrowest affected fixture or fully qualified test name. Examples:

```text
UDND.Tests.Inventories.InventoryTransferServiceTests
UDND.Tests.Inventories.StackableItemStrategyTests
UDND.Tests.Inventories.SeparableStacksStrategyTests
UDND.Tests.Inventories.ShapedItemPlacementTests
```

After focused tests pass, run the complete plugin EditMode assembly for broad or architectural
changes:

```bash
'/mnt/c/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' \
  -batchmode \
  -nographics \
  -projectPath 'E:\UnityProjects\UniversalDragAndDropAsset' \
  -runTests \
  -testPlatform EditMode \
  -assemblyNames 'DragAndDropSystem.Tests.Editor' \
  -testResults 'E:\UnityProjects\UniversalDragAndDropAsset\Temp\udnd-editmode-all.xml' \
  -logFile - \
  -quit
```

Treat the process exit code and the XML test result as authoritative. A successful assembly build is
not a successful test run.

## Open Unity Editor

Unity cannot open the same project twice. If batchmode reports:

```text
It looks like another Unity instance is running with this project open.
```

then:

- do not kill or close the user's Editor;
- do not claim that tests passed;
- report that compilation succeeded but Unity tests were blocked by the open Editor;
- leave the exact fixture/assembly command ready to rerun after the Editor is closed.

The lock failure is an environment limitation, not a test failure.

## Verification Scope

Use the narrowest meaningful scope, then broaden according to risk:

- strategy or placement change: strategy fixture plus shaped-placement fixture;
- transfer pipeline, rollback, policy, events, or conversion: transfer service fixture plus related
  integration fixtures;
- topology or `IPlacementInventory` change: all shaped-placement tests;
- DataBinding change: binding tests plus transfer integration tests;
- serialized policy or example change: runtime, tests, examples builds and relevant demo smoke check;
- shared architecture change: full plugin EditMode assembly.

## Final Report

Always distinguish:

1. projects that compiled;
2. Unity fixtures or assemblies that actually ran;
3. tests blocked by an open Editor or another environment problem;
4. remaining manual checks.

Never summarize `dotnet build` as "tests passed".

Before the final report, also run:

```bash
git diff --check
```

Line-ending conversion warnings are informational. Actual whitespace errors must be fixed.
