---
name: unity-player-input
description: >
  How to DRIVE player input on behalf of a player through Unity's new Input System,
  so a mechanic can be exercised end-to-end without a human at the controls — join/
  leave a player, then press/hold/tap an input action or stick. Two delivery paths,
  same mechanism: (a) a human/notebook runs the project's `player_input` unity-cli
  tool; (b) an agent AUTHORS the equivalent C# the runtime executes in the live
  Editor. Three flexibility axes: PROVIDER (keyboard/gamepad/mouse, real or a virtual
  device added on demand), PLAYER ID (which joined PlayerInput), and INPUT TYPE
  (float button/axis vs Vector2 stick; named action vs explicit control path). Use
  when you need to simulate a button press / movement to verify a reaction, command
  sequence, or any input-triggered chain actually fires. Portable to any project with
  com.unity.inputsystem + PlayerInput/PlayerInputManager; worked example from vex-ee.
---

# Driving player input (new Input System)

A carried-by-all capability: almost any mechanic is ultimately *triggered by input*,
so every specialist needs a focus-independent way to make a player "press the
button" and confirm the chain (CommandSequence → ConditionEvent → Reaction → …)
actually fires. This is purely Input-System level — **no ECS writes**. The input
flows the real route: `PlayerInput action → PlayerInputBridge → InputConsumer →
CommandSequence → reaction`.

## Discover first (portability)

Never assume the capability or the action names exist. Discover them:

- Is the human-facing tool installed? `unity-cli list` → look for a `player_input`
  tool. If present, a designer/notebook can drive input without writing code.
- What actions does the player have? Join a player, then read
  `PlayerInput.all[i].actions` filtered to `currentActionMap` (e.g. `Move`, `Look`,
  `Jump`, `Attack`). Names are project-specific — read them, don't guess.
- What providers exist? `InputSystem.devices` (keyboard/gamepad/mouse/touch). If none
  fit, you may **add a virtual one** (see below) so it works headless.

If there is no `PlayerInputManager` / no `PlayerInput` prefab, report that honestly
and stop — that is a missing prerequisite, not something to improvise.

## The mechanism (the part that matters)

Four hard-won rules — get these wrong and input silently does nothing:

1. **Inject with a queued state event, not `InputState.Change`.** Keyboard keys are
   *bitfield* controls; `InputState.Change(key, 1f)` throws
   ("Cannot change state of bitfield control"). The portable write that works for
   buttons AND Vector2 sticks:
   ```csharp
   using (StateEvent.From(control.device, out var eventPtr)) {
       ((InputControl<float>)control).WriteValueIntoEvent(value, eventPtr);   // or InputControl<Vector2> + Vector2
       InputSystem.QueueEvent(eventPtr);
   }
   // Do NOT call InputSystem.Update() — let the play loop consume it next frame.
   ```
2. **Defeat Game-View focus gating.** In the Editor, injected device input is gated to
   a focused Game view by default, so the action never fires while you drive it from a
   tool/headless. Set once before injecting:
   ```csharp
   InputSystem.settings.editorInputBehaviorInPlayMode =
       InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
   ```
3. **Hold across frames for a clean down-edge.** Press + release in the *same* frame is
   never sampled — the ECS InputConsumer / CommandSequence "Down" phase needs to see
   up→down→…→up over real frames. Press one frame, hold N frames, then release. A
   "tap" = press now + schedule the release ~6 frames later.
4. **Resolve the right control.** For a button, the action's first resolved control is
   fine. For movement, a *gamepad* `Move` resolves to a single `Vector2` stick (write
   `(x,y)` directly); a *keyboard* `Move` is a WASD composite of separate key controls
   (drive the individual keys, you cannot write an arbitrary vector). Pick the control
   on the device/provider you intend.

## Virtual devices (fully headless)

No hardware needed: `InputSystem.AddDevice("Gamepad")` (or `Keyboard`/`Mouse`/
`Touchscreen`), join a player paired to it, then drive its controls. Remove with
`InputSystem.RemoveDevice(device)`. This is how input is driven on a CI/headless
editor.

## The `player_input` unity-cli tool (human / notebook path)

When the project ships the tool (vex-ee does), a designer or the marimo controller
calls it instead of writing C#. Ops:
`list`, `devices`, `add_device`, `remove_device`, `join`, `pair`, `leave`,
`leave_all`, `press`, `release`, `tap`. Key params: `op`, `player`, `action`,
`control`, `value`, `x`, `y`, `hold_frames`, `provider`, `scheme`.

```bash
unity-cli player_input --params '{"op":"join","provider":"gamepad"}'
unity-cli player_input --params '{"op":"tap","action":"Jump","hold_frames":15}'
unity-cli player_input --params '{"op":"press","action":"Move","provider":"gamepad","x":1,"y":0}'
unity-cli player_input --params '{"op":"add_device","provider":"gamepad"}'   # virtual, headless
```

## Agent contract (you author C#, you do not shell out)

A flue specialist has no unity-cli of its own — you AUTHOR the C# above and the
runtime runs it in the live Editor. Same discipline as any mutation: capture PRE
state (which players are joined, the action's current value), drive the input, then
verify the EFFECT you expected (entity-count delta, a stat change, a spawned object's
read-back) — input fired ≠ mechanic worked. Driving input is generally transient and
self-reversing (release returns to rest); if you add a virtual device or join a
player to test, remove/leave it again so you leave no broken state.
