using System;
using IdleFactory.Run.Application;
using UnityEngine;
using UnityEngine.UIElements;

namespace IdleFactory.Unity.UI
{
    /// <summary>Why a hold ended (FR-042, F5-R20 VEIN-2): the release, leaving the vein, a second pointer, a rotation, focus loss or a dialog.</summary>
    public enum OreVeinHoldEnd
    {
        None = 0,
        Released = 1,
        Left = 2,
        SecondPointer = 3,
        Rotation = 4,
        FocusLoss = 5,
        Dialog = 6,
        Hidden = 7,
        CaptureLost = 8,
        Unbound = 9,
        Unavailable = 10,
    }

    /// <summary>
    /// The ore vein overlay's input and feedback (T359, FR-040 to FR-044, F5-R20 VEIN-1 to VEIN-12). It turns a pointer click, a
    /// touch tap, Space or Enter, and the hold of each, into <c>Mine(1)</c> calls on its <see cref="IOreVeinSource"/>, and shows the
    /// result. It holds no economy rule: the rate cap, the store check and the yield belong to the command, so the view only
    /// paces a hold at the content's rate on the run timeline and reports what the command returned.
    ///
    /// Hold accrual: the first press counts immediately. After that one activation is due every 1/cap seconds of run timeline
    /// (the k-th at <c>start + k x 1000 / cap</c> ms), at most one per frame, so a long frame yields one catch-up and never a burst.
    /// A hold ends on release, leaving the vein, a second pointer, a rotation, focus loss, a dialog or hiding the vein, and it
    /// grants nothing after it ends. Keyboard input uses KeyDown to start and KeyUp to end; OS key repeat adds nothing, and a
    /// NavigationSubmit that comes with a key press is dropped, so one press never counts twice.
    ///
    /// The control never attaches the exact-value gestures (hover, focus detail, tap-to-pin, long press): a 600 ms touch hold
    /// mines and opens no peek.
    /// </summary>
    public sealed class OreVeinControl : IDisposable
    {
        public const string RootName = "factory-ore-vein";
        public const string ProgressName = "factory-ore-vein-progress";
        public const string ProgressFillName = "factory-ore-vein-progress-fill";
        public const string PulseName = "factory-ore-vein-pulse";
        public const string ReasonName = "factory-ore-vein-reason";

        /// <summary>How long the pulse stays after the last counted activation.</summary>
        public const float PulseSeconds = 0.6f;

        private const string ShownClass = "run-vein__shown";
        private const string MaxRateClass = "run-vein--max-rate";
        private const string HeldClass = "run-vein--held";
        private const string DisabledCode = "vein.disabled";

        private enum HoldKind
        {
            None = 0,
            Pointer = 1,
            Key = 2,
        }

        private readonly VisualElement root;
        private readonly VisualElement progress;
        private readonly VisualElement progressFill;
        private readonly Label pulse;
        private readonly Label reasonLabel;
        private readonly Label captionLabel;

        private IOreVeinSource source;
        private object runToken;
        private HoldKind hold;
        private int heldPointer = -1;
        private KeyCode heldKey;
        private long holdStartMs;
        private long holdIndex;
        private int lastKeyFrame = -1;

        private bool dialogOpen;
        private string unavailableReason;
        private string disabledReason;
        private string reasonCode;
        private string blockingCode;

        private long remainderMilli;
        private float pulseUntil;
        private long pulseCount;
        private bool maxRateShown;
        private bool disposed;

        private string shownLabel;
        private long shownYield = -1;
        private string accessibleName = string.Empty;

        private OreVeinControl(VisualElement root, VisualElement progress, VisualElement progressFill, Label pulse, Label reasonLabel, Label captionLabel)
        {
            this.root = root;
            this.progress = progress;
            this.progressFill = progressFill;
            this.pulse = pulse;
            this.reasonLabel = reasonLabel;
            this.captionLabel = captionLabel;
            root.focusable = true;
            root.tabIndex = 0;
            root.RegisterCallback<PointerDownEvent>(OnPointerDown);
            root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnPointerUp);
            root.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            root.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            root.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            root.RegisterCallback<KeyUpEvent>(OnKeyUp);
            root.RegisterCallback<NavigationSubmitEvent>(OnNavigationSubmit);
            root.RegisterCallback<FocusInEvent>(OnFocusIn);
            root.RegisterCallback<FocusOutEvent>(OnFocusOut);
        }

        /// <summary>Wraps the vein element of the shell UXML, or returns null when one of its named parts is missing.</summary>
        public static OreVeinControl TryCreate(VisualElement root)
        {
            if (root == null)
            {
                return null;
            }

            var fill = root.Q<VisualElement>(ProgressFillName);
            var pulse = root.Q<Label>(PulseName);
            var reason = root.Q<Label>(ReasonName);
            var caption = root.Q<Label>("factory-ore-vein-label");
            var progress = root.Q<VisualElement>(ProgressName);
            if (fill == null || pulse == null || reason == null || caption == null || progress == null)
            {
                return null;
            }

            return new OreVeinControl(root, progress, fill, pulse, reason, caption);
        }

        /// <summary>The vein element (<c>factory-ore-vein</c>).</summary>
        public VisualElement Root => root;

        /// <summary>The accessible name, for example "Mine Iron Ore, +0.1 per click". It changes with state and yield, never per click.</summary>
        public string AccessibleName => accessibleName;

        /// <summary>How often the accessible name changed since it was bound (a state or yield change, never a click).</summary>
        public int AccessibleNameChanges { get; private set; }

        /// <summary>The pulse text ("+0.1", "+0.1 ×n" or "Max rate") while it is shown, otherwise empty.</summary>
        public string PulseText => IsPulseShown ? pulse.text : string.Empty;

        public bool IsPulseShown => pulse.ClassListContains(ShownClass);

        /// <summary>The reason text while it is shown, otherwise empty. The label keeps its space while hidden, so clearing never moves the layout.</summary>
        public string ReasonText => IsReasonShown ? reasonLabel.text : string.Empty;

        public bool IsReasonShown => reasonLabel.ClassListContains(ShownClass);

        /// <summary>The rejection code behind the shown reason, or null.</summary>
        public string ReasonCode => reasonCode;

        /// <summary>Whether the "max rate" cue is shown.</summary>
        public bool IsMaxRateShown => maxRateShown && IsPulseShown;

        /// <summary>The mining remainder toward the next Ore in tenths (0 to 9), read from the application's mining snapshot.</summary>
        public int ProgressTenths => (int)Math.Min(9L, Math.Max(0L, remainderMilli / 100L));

        public bool IsHolding => hold != HoldKind.None;

        public OreVeinHoldEnd LastHoldEnd { get; private set; }

        /// <summary>How many holds ended since the control was bound.</summary>
        public int HoldEndCount { get; private set; }

        /// <summary>How many <c>Mine(1)</c> calls the control issued since it was bound.</summary>
        public int RequestedActivations { get; private set; }

        /// <summary>
        /// Whether the pulse fades instead of staying static. Unity exposes no platform reduced-motion preference, so the default
        /// is the static label (VEIN-7); a platform adapter can switch the fade on.
        /// </summary>
        public bool AnimatePulse { get; set; }

        public bool IsSoftDisabled => disabledReason != null;

        public string DisabledReason => disabledReason;

        public bool IsBound => source != null;

        /// <summary>Whether the shell hid the vein (nothing bound, or a sheet covers it). A hidden vein never activates (VEIN-1).</summary>
        public bool IsHidden => root.style.display == DisplayStyle.None;

        /// <summary>Binds the source and resets everything transient (hold, pulse, reason, progress).</summary>
        public void Bind(IOreVeinSource newSource)
        {
            CancelHold(OreVeinHoldEnd.Unbound);
            source = newSource;
            runToken = newSource?.RunToken;
            pulseCount = 0;
            RequestedActivations = 0;
            HoldEndCount = 0;
            LastHoldEnd = OreVeinHoldEnd.None;
            AccessibleNameChanges = 0;
            shownLabel = null;
            shownYield = -1;
            accessibleName = string.Empty;
            HidePulse();
            ClearReason();
            UpdateProgress();
            if (source != null)
            {
                SyncState();
            }
        }

        /// <summary>Drops the source. A running hold ends.</summary>
        public void Unbind() => Bind(null);

        /// <summary>
        /// Sets the reason the vein is soft-disabled for outside a dialog (the factory is unavailable), or null. A hold ends. The
        /// vein stays focusable and a press shows the reason.
        /// </summary>
        public void SetUnavailable(string reason)
        {
            if (string.Equals(unavailableReason, reason, StringComparison.Ordinal))
            {
                return;
            }

            unavailableReason = string.IsNullOrEmpty(reason) ? null : reason;
            ApplyDisabled();
        }

        /// <summary>A dialog disables the vein and ends a hold (FR-042). Closing it enables the vein again.</summary>
        public void SetDialogOpen(bool open)
        {
            if (dialogOpen == open)
            {
                return;
            }

            dialogOpen = open;
            ApplyDisabled();
        }

        /// <summary>Ends a running hold. Nothing is credited after it.</summary>
        public void CancelHold(OreVeinHoldEnd reason)
        {
            if (hold == HoldKind.None)
            {
                return;
            }

            var kind = hold;
            var pointer = heldPointer;
            hold = HoldKind.None;
            heldPointer = -1;
            heldKey = KeyCode.None;
            LastHoldEnd = reason;
            HoldEndCount++;
            root.RemoveFromClassList(HeldClass);
            if (kind == HoldKind.Pointer && root.panel != null && root.HasPointerCapture(pointer))
            {
                root.ReleasePointer(pointer);
            }
        }

        /// <summary>
        /// Per-frame update: keeps the label, the name and the cues in step with the source, lets the pulse expire and accrues a
        /// hold on the run timeline (one activation per 1/cap s, at most one per frame).
        /// </summary>
        public void Tick()
        {
            if (source == null || disposed)
            {
                return;
            }

            SyncState();
            UpdatePulse();
            if (hold == HoldKind.None)
            {
                return;
            }

            if (disabledReason != null)
            {
                CancelHold(OreVeinHoldEnd.Unavailable);
                return;
            }

            var now = source.TimelineMs;
            if (now < holdStartMs)
            {
                // The timeline moved backwards (a rebind): the schedule starts over from here.
                holdStartMs = now;
                holdIndex = 1;
                return;
            }

            if (now < Due(holdIndex))
            {
                return;
            }

            Activate();
            holdIndex++;
            var cap = Math.Max(1L, source.ActivationsPerSecond);
            var latest = (now - holdStartMs) * cap / 1000L;
            if (holdIndex < latest)
            {
                // A long frame leaves more than one activation behind: keep one catch-up for the next frame and drop the rest.
                holdIndex = latest;
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            CancelHold(OreVeinHoldEnd.Unbound);
            disposed = true;
            source = null;
            root.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            root.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            root.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            root.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            root.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            root.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
            root.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            root.UnregisterCallback<KeyUpEvent>(OnKeyUp);
            root.UnregisterCallback<NavigationSubmitEvent>(OnNavigationSubmit);
            root.UnregisterCallback<FocusInEvent>(OnFocusIn);
            root.UnregisterCallback<FocusOutEvent>(OnFocusOut);
        }

        // ------------------------------------------------------------------------------------------------------
        // Activation
        // ------------------------------------------------------------------------------------------------------

        private long Due(long index)
        {
            var cap = Math.Max(1L, source.ActivationsPerSecond);
            return holdStartMs + index * 1000L / cap;
        }

        private void BeginHold(HoldKind kind)
        {
            hold = kind;
            holdStartMs = source.TimelineMs;
            holdIndex = 1;
            LastHoldEnd = OreVeinHoldEnd.None;
            root.AddToClassList(HeldClass);
        }

        /// <summary>One activation: a click, a tap, a key press or a hold tick. The command decides what counts.</summary>
        private void Activate()
        {
            if (source == null || IsHidden)
            {
                return;
            }

            if (disabledReason != null)
            {
                ShowReason(DisabledCode, disabledReason);
                return;
            }

            // The yield is read before the command, so the progress tracking uses the yield the command used.
            var yield = source.YieldMilli;
            RequestedActivations++;
            Apply(source.Mine(1), yield);
        }

        private void Apply(OreVeinMineOutcome outcome, long yield)
        {
            if (!outcome.Success)
            {
                ShowReason(outcome.ReasonCode, OreVeinCopy.Reason(outcome.ReasonCode));
                return;
            }

            if (outcome.Counted > 0)
            {
                UpdateProgress();
                if (reasonCode != null)
                {
                    // A counted activation proves the blocking condition is gone.
                    ClearReason();
                }

                ShowPulse(outcome.Counted, yield);
            }
            else if (outcome.Ignored > 0)
            {
                ShowMaxRate();
            }
        }

        private void UpdateProgress()
        {
            // The remainder is the application's (mining snapshot), never tracked here, so it stays exact with any other miner.
            remainderMilli = source == null ? 0L : source.RemainderMilli;
            progressFill.style.width = new Length(ProgressTenths * 10f, LengthUnit.Percent);
            progress.tooltip = ProgressTenths + " tenths of the next Ore";
        }

        // ------------------------------------------------------------------------------------------------------
        // Feedback
        // ------------------------------------------------------------------------------------------------------

        private void ShowPulse(long counted, long yield)
        {
            var now = Time.unscaledTime;
            var continuing = IsPulseShown && !maxRateShown && now < pulseUntil;
            pulseCount = continuing ? pulseCount + counted : counted;
            maxRateShown = false;
            root.RemoveFromClassList(MaxRateClass);
            pulse.text = OreVeinCopy.Pulse(yield, pulseCount);
            pulse.AddToClassList(ShownClass);
            pulse.style.opacity = 1f;
            pulseUntil = now + PulseSeconds;
        }

        private void ShowMaxRate()
        {
            maxRateShown = true;
            pulseCount = 0;
            root.AddToClassList(MaxRateClass);
            pulse.text = OreVeinCopy.MaxRate;
            pulse.AddToClassList(ShownClass);
            pulse.style.opacity = 1f;
            pulseUntil = Time.unscaledTime + PulseSeconds;
        }

        private void UpdatePulse()
        {
            if (!IsPulseShown)
            {
                return;
            }

            var remaining = pulseUntil - Time.unscaledTime;
            if (remaining <= 0f)
            {
                HidePulse();
            }
            else if (AnimatePulse)
            {
                pulse.style.opacity = Mathf.Clamp01(remaining / (PulseSeconds * 0.5f));
            }
        }

        private void HidePulse()
        {
            pulse.RemoveFromClassList(ShownClass);
            root.RemoveFromClassList(MaxRateClass);
            maxRateShown = false;
            pulseCount = 0;
        }

        private void ShowReason(string code, string text)
        {
            reasonCode = code;
            blockingCode = code == DisabledCode ? blockingCode : code;
            reasonLabel.text = text;
            // The visible text stays short ("Store full"); the tooltip carries the full sentence.
            reasonLabel.tooltip = code == DisabledCode ? text : RunRejectionCopy.For(code);
            reasonLabel.AddToClassList(ShownClass);
            RefreshName();
        }

        private void ClearReason()
        {
            if (reasonCode == null)
            {
                return;
            }

            // The text stays in the label, hidden but still measured, so the container keeps its size and nothing moves.
            reasonCode = null;
            blockingCode = null;
            reasonLabel.RemoveFromClassList(ShownClass);
            RefreshName();
        }

        private void RefreshName()
        {
            if (source == null)
            {
                return;
            }

            var name = OreVeinCopy.AccessibleName(source.ResourceLabel, source.YieldMilli, blockingCode);
            if (name == accessibleName)
            {
                return;
            }

            accessibleName = name;
            AccessibleNameChanges++;
            root.tooltip = name;
        }

        /// <summary>Follows the source: label, yield, a replaced run, and the conditions that clear a cue.</summary>
        private void SyncState()
        {
            var token = source.RunToken;
            if (!ReferenceEquals(token, runToken))
            {
                runToken = token;
                UpdateProgress();
                CancelHold(OreVeinHoldEnd.Unbound);
                ClearReason();
            }

            if (reasonCode == RunReasons.StorageFull && source.HasRoom)
            {
                ClearReason();
            }
            else if (reasonCode == RunReasons.LifecycleBlocked && !source.IsLifecycleBlocked)
            {
                ClearReason();
            }

            var label = source.ResourceLabel;
            var yield = source.YieldMilli;
            if (yield != shownYield || label != shownLabel)
            {
                shownYield = yield;
                shownLabel = label;
                captionLabel.text = OreVeinCopy.Caption;
                RefreshName();
            }
        }

        private void ApplyDisabled()
        {
            disabledReason = dialogOpen ? OreVeinCopy.DialogOpen : unavailableReason;
            if (disabledReason != null)
            {
                CancelHold(dialogOpen ? OreVeinHoldEnd.Dialog : OreVeinHoldEnd.Unavailable);
            }

            RunShellDocument.SetSoftDisabled(root, disabledReason != null);
            if (disabledReason == null && reasonCode == DisabledCode)
            {
                ClearReason();
            }
            else if (disabledReason != null && reasonCode == DisabledCode)
            {
                reasonLabel.text = disabledReason;
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Pointer input
        // ------------------------------------------------------------------------------------------------------

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (source == null || IsHidden || evt.button > 0)
            {
                return;
            }

            evt.StopPropagation();
            if (hold != HoldKind.None)
            {
                // A second pointer (a pinch, a second finger) ends the hold; the press itself starts nothing.
                if (hold == HoldKind.Pointer && evt.pointerId != heldPointer)
                {
                    CancelHold(OreVeinHoldEnd.SecondPointer);
                }

                return;
            }

            if (disabledReason != null)
            {
                Activate();
                return;
            }

            BeginHold(HoldKind.Pointer);
            heldPointer = evt.pointerId;
            root.CapturePointer(evt.pointerId);
            Activate();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (hold == HoldKind.Pointer && evt.pointerId == heldPointer && !root.worldBound.Contains(evt.position))
            {
                CancelHold(OreVeinHoldEnd.Left);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (hold == HoldKind.Pointer && evt.pointerId == heldPointer)
            {
                evt.StopPropagation();
                CancelHold(OreVeinHoldEnd.Released);
            }
        }

        private void OnPointerCancel(PointerCancelEvent evt)
        {
            if (hold == HoldKind.Pointer && evt.pointerId == heldPointer)
            {
                CancelHold(OreVeinHoldEnd.Released);
            }
        }

        private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (hold == HoldKind.Pointer && evt.pointerId == heldPointer)
            {
                CancelHold(OreVeinHoldEnd.CaptureLost);
            }
        }

        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (hold == HoldKind.Pointer && evt.pointerId == heldPointer)
            {
                CancelHold(OreVeinHoldEnd.Left);
            }
        }

        // ------------------------------------------------------------------------------------------------------
        // Keyboard and navigation input
        // ------------------------------------------------------------------------------------------------------

        private static bool IsActivationKey(KeyCode key) =>
            key == KeyCode.Space || key == KeyCode.Return || key == KeyCode.KeypadEnter;

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (source == null || IsHidden || !IsActivationKey(evt.keyCode) ||
                (evt.modifiers & (EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command)) != 0)
            {
                return;
            }

            evt.StopPropagation();
            lastKeyFrame = Time.frameCount;
            if (hold != HoldKind.None)
            {
                // OS key repeat, or a second key: the hold already runs on the timeline, so there is nothing to add.
                return;
            }

            if (disabledReason != null)
            {
                Activate();
                return;
            }

            BeginHold(HoldKind.Key);
            heldKey = evt.keyCode;
            Activate();
        }

        private void OnKeyUp(KeyUpEvent evt)
        {
            if (hold == HoldKind.Key && evt.keyCode == heldKey)
            {
                evt.StopPropagation();
                CancelHold(OreVeinHoldEnd.Released);
            }
        }

        private void OnNavigationSubmit(NavigationSubmitEvent evt)
        {
            evt.StopPropagation();
            if (source == null || IsHidden)
            {
                return;
            }

            // The input system raises a key event and a navigation event for one Space or Enter press: the key event wins.
            if (hold == HoldKind.Key || lastKeyFrame == Time.frameCount)
            {
                return;
            }

            Activate();
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            if (disabledReason != null && evt.target == root)
            {
                ShowReason(DisabledCode, disabledReason);
            }
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            if (evt.target != root)
            {
                return;
            }

            // A KeyUp goes to the focused element, so a hold must not outlive the focus (Tab ends a hold too).
            CancelHold(OreVeinHoldEnd.FocusLoss);
            if (reasonCode == DisabledCode)
            {
                ClearReason();
            }
        }
    }
}
