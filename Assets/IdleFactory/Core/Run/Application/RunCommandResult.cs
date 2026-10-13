using System;
using System.Collections.Generic;
using IdleFactory.Run.Domain;
using IdleFactory.Run.Simulation;

namespace IdleFactory.Run.Application
{
    /// <summary>Whether a run command was applied. A rejection changed no state at all.</summary>
    public enum RunCommandOutcome
    {
        Success = 0,
        Rejected = 1,
    }

    /// <summary>
    /// The stable, player-readable rejection codes of contracts/run-application.md (Rejection reasons), plus the
    /// mapping from the domain and simulator results. UI copy keys off these strings, so they never change.
    /// </summary>
    public static class RunReasons
    {
        public const string SlotOccupied = "slot.occupied";
        public const string SlotUnavailable = "slot.unavailable";
        public const string MachineLocked = "machine.locked";
        public const string CashInsufficient = "cash.insufficient";
        public const string LevelMax = "level.max";
        public const string ConfirmationRequired = "confirmation.required";
        public const string PriorityNotPermutation = "priority.not-permutation";
        public const string ResourceLocked = "resource.locked";
        public const string ContractActiveExists = "contract.active-exists";
        public const string ContractNoneActive = "contract.none-active";
        public const string ContractExpired = "contract.expired";
        public const string StockInsufficient = "stock.insufficient";
        public const string ItemNone = "item.none";
        public const string ItemTargetInvalid = "item.target.invalid";
        public const string EffectConflict = "effect.conflict";
        public const string ResearchPrerequisite = "research.prerequisite";
        public const string RpInsufficient = "rp.insufficient";
        public const string OverdriveCoolingDown = "overdrive.cooling-down";
        public const string OverdriveActive = "overdrive.active";
        public const string IpInsufficient = "ip.insufficient";
        public const string PrestigeBelowThreshold = "prestige.below-threshold";
        public const string TransitionPending = "transition.pending";
        public const string ValueLimit = "value.limit";
        public const string LifecycleBlocked = "lifecycle.blocked";

        /// <summary>
        /// F005 (T323): the expected build price differs from the price at the command instant. Not part of the 24
        /// original contract codes in <see cref="All"/>; listed in docs/contracts/run-reason-codes.md.
        /// </summary>
        public const string BuildPriceChanged = "build.price.changed";

        /// <summary>
        /// F005 (T317): a Sell quantity below 1. Like <see cref="BuildPriceChanged"/> it is not one of the 24 contract
        /// codes in <see cref="All"/> (RunSessionTests pins that count); listed in docs/contracts/run-reason-codes.md.
        /// </summary>
        public const string SellQuantityInvalid = "sell.quantity-invalid";

        /// <summary>
        /// Not one of the 24 contract codes: a rejected request that is a programming or load error (a negative
        /// tick count, an unknown instance, a state that broke its invariants) or has no code of its own (a
        /// full store). The contract lists no code for these, so the application reports one static value.
        /// </summary>
        public const string CommandInvalid = "command.invalid";

        private static readonly string[] Stable =
        {
            SlotOccupied, SlotUnavailable, MachineLocked, CashInsufficient, LevelMax, ConfirmationRequired,
            PriorityNotPermutation, ResourceLocked, ContractActiveExists, ContractNoneActive, ContractExpired,
            StockInsufficient, ItemNone, ItemTargetInvalid, EffectConflict, ResearchPrerequisite, RpInsufficient,
            OverdriveCoolingDown, OverdriveActive, IpInsufficient, PrestigeBelowThreshold, TransitionPending,
            ValueLimit, LifecycleBlocked,
        };

        /// <summary>The 24 stable codes of the contract, in contract order.</summary>
        public static IReadOnlyList<string> All => Stable;

        /// <summary>
        /// Maps a domain result to its code; <see cref="RunStateResult.Ok"/> has none (null). CapacityExceeded,
        /// UnknownInstance, InvalidArgument, DuplicateInstance and InvariantViolated map to
        /// <see cref="CommandInvalid"/>.
        /// </summary>
        public static string From(RunStateResult result)
        {
            switch (result)
            {
                case RunStateResult.Ok: return null;
                case RunStateResult.ValueLimit: return ValueLimit;
                case RunStateResult.SlotOccupied: return SlotOccupied;
                case RunStateResult.SlotUnavailable: return SlotUnavailable;
                case RunStateResult.NotPermutation: return PriorityNotPermutation;
                case RunStateResult.ResourceLocked: return ResourceLocked;
                case RunStateResult.CashInsufficient: return CashInsufficient;
                case RunStateResult.RpInsufficient: return RpInsufficient;
                case RunStateResult.IpInsufficient: return IpInsufficient;
                case RunStateResult.LevelMax: return LevelMax;
                case RunStateResult.StockInsufficient: return StockInsufficient;
                case RunStateResult.EffectConflict: return EffectConflict;
                case RunStateResult.ContractActiveExists: return ContractActiveExists;
                case RunStateResult.ContractNoneActive: return ContractNoneActive;
                case RunStateResult.ContractExpired: return ContractExpired;
                case RunStateResult.ItemNone: return ItemNone;
                case RunStateResult.OverdriveCoolingDown: return OverdriveCoolingDown;
                case RunStateResult.OverdriveActive: return OverdriveActive;
                default: return CommandInvalid;
            }
        }

        /// <summary>
        /// Maps a simulator result: Ok has no code, ArithmeticOverflow is <see cref="ValueLimit"/> and
        /// InvalidArgument is <see cref="CommandInvalid"/>.
        /// </summary>
        public static string From(RunAdvanceResult result)
        {
            switch (result)
            {
                case RunAdvanceResult.Ok: return null;
                case RunAdvanceResult.ArithmeticOverflow: return ValueLimit;
                default: return CommandInvalid;
            }
        }
    }

    /// <summary>Static factories for <see cref="RunCommandResult{T}"/>.</summary>
    public static class RunCommandResult
    {
        public static RunCommandResult<T> Success<T>(T value) => new RunCommandResult<T>(RunCommandOutcome.Success, null, value);

        public static RunCommandResult<T> Reject<T>(string reasonCode)
        {
            if (string.IsNullOrEmpty(reasonCode)) throw new ArgumentException("A rejection needs a reason code.", nameof(reasonCode));
            return new RunCommandResult<T>(RunCommandOutcome.Rejected, reasonCode, default);
        }
    }

    /// <summary>
    /// The result of a run command (contracts/run-application.md). New type with its own reason codes; the
    /// Feature 001 <c>CommandResult</c> and <c>CommandOutcome</c> stay unchanged (GD-2). A value struct, so a
    /// command result never allocates. On rejection <see cref="Value"/> is the default and no state changed.
    /// </summary>
    public readonly struct RunCommandResult<T>
    {
        public RunCommandOutcome Outcome { get; }

        /// <summary>The stable rejection code (<see cref="RunReasons"/>), or null on success.</summary>
        public string ReasonCode { get; }

        public T Value { get; }

        public bool IsSuccess => Outcome == RunCommandOutcome.Success;

        internal RunCommandResult(RunCommandOutcome outcome, string reasonCode, T value)
        {
            Outcome = outcome;
            ReasonCode = reasonCode;
            Value = value;
        }
    }
}
