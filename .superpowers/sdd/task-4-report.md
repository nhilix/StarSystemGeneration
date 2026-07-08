# Task 4 Report: AtlasNavigator (drill-down state machine)

(Note: this file previously carried a stale report for a differently-numbered task
on an unrelated branch, `hex-geometry` — that content has been replaced below with
this task's report, per the unity-atlas plan. HEAD on `unity-atlas` at task start was
`d29401c`, "feat: GalaxyService seam for the atlas" — Task 3's commit.)

## Status
COMPLETE — TDD followed exactly per brief. Test file transcribed verbatim from the
brief, RED gate proven via real batchmode Unity run (compile failure, expected),
implementation added verbatim, GREEN gate proven via real batchmode Unity run
(12/12 edit-mode pass — 4 new `AtlasNavigatorTests` + 8 prior), `dotnet test`
unaffected at 101/101, committed.

## Preconditions confirmed
- `git status` clean at start on branch `unity-atlas`, HEAD at `d29401c`.
- `tasklist //FI "IMAGENAME eq Unity.exe"` returned no matching tasks — editor
  confirmed closed, not BLOCKED.
- `HexCoordinate` (`src/Core/Model/HexCoordinate.cs`) is a `readonly struct
  IEquatable<HexCoordinate>` with value-based `Equals`/`GetHashCode` — safe as a
  nullable field and with `Assert.AreEqual`/`Assert.IsNull`, no adaptation needed.
- `unity/Assets/Scripts/Atlas/AtlasNavigator.cs` confirmed absent before Step 1.

## TDD evidence

### Step 1 (RED — write failing tests)
Created `unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs` verbatim from the
brief (4 test methods: `DrillDown_AndBack_WalksTheLadder`, `IllegalTransitions_Throw`,
`EveryMutation_FiresChangedOnce`, `DrillToCell_FromCell_SwitchesCell_AndClearsHex`).

### Step 2 (verify RED — real batchmode run)
```
UNITY="/c/Program Files/Unity/Hub/Editor/6000.5.2f1/Editor/Unity.exe"
"$UNITY" -batchmode -projectPath "$(pwd)/unity" -runTests -testPlatform EditMode \
  -testResults "$(pwd)/unity/test-results.xml" -logFile "$(pwd)/unity/test.log"
```
Result: **exit code 1** — `Aborting batchmode due to failure: Scripts have compiler errors.`
```
$ grep -E "error CS" unity/test.log | sort -u
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(12,27): error CS0246: The type or namespace name 'AtlasNavigator' could not be found (are you missing a using directive or an assembly reference?)
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(13,29): error CS0103: The name 'AtlasScreen' does not exist in the current context
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(15,29): error CS0103: The name 'AtlasScreen' does not exist in the current context
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(17,29): error CS0103: The name 'AtlasScreen' does not exist in the current context
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(23,29): error CS0103: The name 'AtlasScreen' does not exist in the current context
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(25,29): error CS0103: The name 'AtlasScreen' does not exist in the current context
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(28,29): error CS0103: The name 'AtlasScreen' does not exist in the current context
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(30,29): error CS0103: The name 'AtlasScreen' does not exist in the current context
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(36,27): error CS0246: The type or namespace name 'AtlasNavigator' could not be found (are you missing a using directive or an assembly reference?)
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(44,27): error CS0246: The type or namespace name 'AtlasNavigator' could not be found (are you missing a using directive or an assembly reference?)
Assets\Scripts\Atlas\Tests\AtlasNavigatorTests.cs(59,27): error CS0246: The type or namespace name 'AtlasNavigator' could not be found (are you missing a using directive or an assembly reference?)
```
Expected: FAIL (API missing, not a typo or pre-existing pass). Confirmed. ✓

### Step 3 (GREEN — implement)
Created `unity/Assets/Scripts/Atlas/AtlasNavigator.cs` verbatim from the brief:
`enum AtlasScreen { Setup, Galaxy, Cell }` and `sealed class AtlasNavigator` with
`Screen`, `SelectedCell`, `SelectedHex`, `event Action? Changed`, and
`EnterGalaxy()`/`DrillToCell()`/`SelectHex()`/`ClearHexSelection()`/`Back()`/`Reset()`
exactly as specified (illegal-transition guards throw `InvalidOperationException`;
every mutation path invokes `Changed` exactly once; `Back()` at `Setup` returns early
without firing).

### Step 4 (GREEN gate — real batchmode run)
```
"$UNITY" -batchmode -projectPath "$(pwd)/unity" -runTests -testPlatform EditMode \
  -testResults "$(pwd)/unity/test-results.xml" -logFile "$(pwd)/unity/test.log"
```
Result: **exit code 0**.

**Verification evidence (captured immediately after the GREEN gate):**
```
$ stat -c "%n %y" unity/test-results.xml
unity/test-results.xml 2026-07-08 07:20:25.975621800 -0700

$ grep -oE '<test-run id="[0-9]+" testcasecount="[0-9]+" result="[^"]+" total="[0-9]+" passed="[0-9]+" failed="[0-9]+"' unity/test-results.xml
<test-run id="2" testcasecount="12" result="Passed" total="12" passed="12" failed="0"
```

Individual `AtlasNavigatorTests` confirmed `result="Passed"` for all 4:
```
$ grep -o 'name="[^"]*AtlasNavigator[^"]*"[^/]*result="[A-Za-z]*"' unity/test-results.xml
name="AtlasNavigatorTests" ... testcasecount="4" result="Passed"
name="StarGen.Atlas.Tests.AtlasNavigatorTests.DrillDown_AndBack_WalksTheLadder" ... result="Passed"
name="StarGen.Atlas.Tests.AtlasNavigatorTests.DrillToCell_FromCell_SwitchesCell_AndClearsHex" ... result="Passed"
name="StarGen.Atlas.Tests.AtlasNavigatorTests.EveryMutation_FiresChangedOnce" ... result="Passed"
name="StarGen.Atlas.Tests.AtlasNavigatorTests.IllegalTransitions_Throw" ... result="Passed"
```
The remaining 8 of the 12 total are the prior `GalaxyServiceTests` (4) and
`LayerPaletteTests` (4) suites (unaffected regression).

`.meta` files were let-Unity-generate during this batchmode import (not
hand-authored) — minimal 2-line form:
```
$ cat unity/Assets/Scripts/Atlas/AtlasNavigator.cs.meta
fileFormatVersion: 2
guid: c3a463ce0bdb95842af25140a867eb2b

$ cat unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs.meta
fileFormatVersion: 2
guid: a9db511fd0279a345b1abb07c2e1f07e
```
`git status --short` immediately before staging showed exactly 4 untracked files
(the two `.cs` + their `.meta`), nothing else.

### dotnet suite guard
```
$ dotnet test
Passed!  - Failed: 0, Passed: 101, Skipped: 0, Total: 101, Duration: 360 ms - StarGen.Core.Tests.dll (net10.0)
```
101/101, unchanged. No `src/Core` files touched — confirmed via `git status`
before staging (only the 4 new `unity/` files present).

### Step 5: Commit
```
git add unity/Assets/Scripts/Atlas/AtlasNavigator.cs unity/Assets/Scripts/Atlas/AtlasNavigator.cs.meta \
        unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs.meta
git commit -m "feat: atlas drill-down navigator

Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>"
```
Commit `8b8198f`. 4 files changed, 138 insertions(+).

## Files changed
- Created: `unity/Assets/Scripts/Atlas/AtlasNavigator.cs` (+ `.meta`)
- Created: `unity/Assets/Scripts/Atlas/Tests/AtlasNavigatorTests.cs` (+ `.meta`)
- Not committed (gitignored, regenerated each run): `unity/test.log`, `unity/test-results.xml`

## Self-review

**Correctness:**
- `AtlasNavigator` consumes only `HexCoordinate` from Core — no Unity types, no
  service dependency — matches "pure state" requirement, fully edit-mode testable.
- Starts at `AtlasScreen.Setup`; `EnterGalaxy()` clears both selections regardless of
  entry point.
- `DrillToCell` throws unless currently `Galaxy` or `Cell` (covers both the initial
  drill and the "breadcrumb-style sibling jump" case from `Cell` to a different
  cell); always clears `SelectedHex` on entry to a (possibly new) cell.
- `SelectHex` throws unless currently `Cell`.
- `Back()` precedence is hex-first: if a hex is selected, `Back()` clears only the
  hex and does not change `Screen`; otherwise it walks `Cell → Galaxy → Setup`;
  at `Setup` it returns before invoking `Changed` (no-op, no event) — matches the RED
  test's explicit assertion sequence and the `EveryMutation_FiresChangedOnce` count
  of exactly 6 (`EnterGalaxy`, `DrillToCell`, `SelectHex`, `ClearHexSelection`,
  `Back`, `Reset` — one `Changed` firing each).
- `Reset()` unconditionally returns to `Setup` with both selections cleared and
  always fires `Changed` (verified by the same test — it's the 6th firing).
- Nullable `HexCoordinate?` compares correctly against a struct with value equality;
  no boxing-related surprises in `Assert.AreEqual`/`Assert.IsNull`.

**Process integrity:**
- Both RED and GREEN gates were real batchmode Unity runs launched via Bash tool
  calls in this session — exit codes, the compiler-error grep, and the post-GREEN
  `stat`/`grep` evidence above are taken directly from that tool output, not
  narrated.
- `.meta` files confirmed to be Unity's minimal 2-line generated form (`guid` only),
  not hand-authored, then `git add`-ed alongside their `.cs` files per instructions.
- Test file and implementation are both verbatim transcriptions of the brief's
  fenced code blocks — no adaptation was needed since `HexCoordinate`'s public shape
  already matched what the brief assumed.

**Regression:**
- Full edit-mode suite: 12/12 (4 new `AtlasNavigatorTests` + 4 prior
  `GalaxyServiceTests` + 4 prior `LayerPaletteTests`), 0 failures.
- `dotnet test`: 101/101, unchanged — no Core regression, no `src/Core` files
  touched.
- `git status` clean except the 4 intended new files at every checkpoint.

**Concerns:** None blocking. Editor was closed throughout (confirmed via `tasklist`
before starting); no BLOCKED condition encountered.

## Commit
```
Commit: 8b8198f
Message: feat: atlas drill-down navigator
Trailer: Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>
Branch: unity-atlas
Files changed: 4 (138 insertions)
```
