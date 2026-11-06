---
title: Android runtime rotation
type: decision
sources: [S001, S018, S019]
updated: 2026-11-06
---

# Android runtime rotation

## Android owns rotation, not Unity

In Unity 6000.6, native Auto Rotation on Android takes its decision from the legacy Input Manager accelerometer. The project uses only the Input System backend (R09), so that path receives no data and the player keeps its launch orientation. Controlled builds ruled out the other suspects: GameActivity versus Activity entry, User versus Sensor rotation behaviour, the rotation lock and the sensor rate. Enabling both input backends restored rotation, but the owner rejected it because it contradicts R09. (S001)

The decision (R19, T058) is that Android owns runtime rotation. `Assets/Plugins/Android/IdleFactoryGameActivity.java` subclasses `UnityPlayerGameActivity` and maps every orientation request to `fullUser`. A custom main manifest makes it the launcher activity. The activity is pure platform glue and holds no gameplay decision, which keeps the [core architecture](./core-architecture.md) boundary intact. (S001)

Rejected alternatives:

- A runtime JNI handoff: Unity re-requests its stale concrete orientation after every configuration change.
- A C# reimplementation from the Input System accelerometer: it would duplicate OS rules such as the rotation lock. (S001)

## Consequences

- A script-set concrete `Screen.orientation` has no effect on Android. A future orientation-locked screen needs a new decision. (S001)
- Android defers a rotation while a finger is down, so no gesture spans a transition. The navigation adapter's orientation-change `CancelAll` stays as the guard for other devices. (S001)
- **Upgrade obligation:** on every Unity version change, diff the custom manifest against Unity's `UnityManifest.xml` template. Verify the merged APK activity with `aapt2 dump xmltree`, and confirm that `UnityPlayerGameActivity` still exists. Edit Mode `AndroidRuntimeRotationTests` guard the manifest, the override and the input backend. (S001)

## Rotation and the offline lifecycle

Android reports a rotation as `OnApplicationFocus(false)` followed 50–320 ms later by `OnApplicationFocus(true)`, with no pause. `Screen` already reports the new size at the focus loss. HOME, the power lock and backgrounding always report `OnApplicationPause(true)` first. (S001)

A rotation therefore stays a normal FR-037 transition: a departure checkpoint and a return reconciliation are written, and offline accounting stays exactly-once (see [local checkpoints and offline return](./local-checkpoints-and-offline-return.md)). Under R20 (T060), `ApplicationLifecycleAdapter` labels a focus-only return as `ConfigurationChange` when the screen configuration changed within a 1 s window. `PersistencePresenter` then withholds that return's Durable offline return summary and keeps the last genuine one. A return that used the FR-037 fallback is never withheld. (S001) Since T119 both Feature 001 classes are retired: the classification types and the 1 s window moved unchanged into `RunLifecycleAdapter`, which applies the same rule to the run (see [Feature 001 retirement](./feature-001-retirement.md)). (S018, S019)

**Accepted residual risk:** the classifier is not platform-gated. A genuine focus-only departure that coincides with a screen change, such as a desktop resize or a foldable transition, loses its summary but not its accounting. (S001)

Emulator procedures for verifying rotation are in [Android emulator validation](./android-emulator-validation.md). (S001)

T124 (emulator) confirmed R20 on the shipped-catalog sets: a Remove and a Prestige confirmation survive rotation, the rotation reconciles and the summary is withheld. The OS back edge swipe is a separate path: in immersive fullscreen it cancels a confirmation only on the second swipe, an accepted limitation (T247, see [Android back gesture](./android-back-gesture.md)). (S018)

Since T250 the departure rules are gated by `LifecyclePlatform`, so the desktop case in the residual risk above no longer applies to desktop players; see [desktop focus is not a departure](./desktop-focus-lifecycle.md). (S018)
