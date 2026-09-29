namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// Outcome of an atomic run-state mutation. Anything other than <see cref="Ok"/> means the state was not
    /// changed at all (no partial mutation).
    ///
    /// Mapping for the application layer (T096) to the stable codes of contracts/run-application.md:
    /// SlotOccupied to slot.occupied, SlotUnavailable to slot.unavailable, CashInsufficient to cash.insufficient,
    /// LevelMax to level.max, NotPermutation to priority.not-permutation, ResourceLocked to resource.locked,
    /// ContractActiveExists to contract.active-exists, ContractNoneActive to contract.none-active,
    /// ContractExpired to contract.expired, StockInsufficient to stock.insufficient, ItemNone to item.none,
    /// EffectConflict to effect.conflict, RpInsufficient to rp.insufficient, OverdriveCoolingDown to
    /// overdrive.cooling-down, OverdriveActive to overdrive.active, IpInsufficient to ip.insufficient, and
    /// ValueLimit to value.limit (checked overflow). The remaining results have no code of their own:
    /// CapacityExceeded is a store-full rejection, UnknownInstance is an ownership failure, and
    /// InvalidArgument, DuplicateInstance and InvariantViolated are programming or load errors that the
    /// application maps to a static rejection.
    /// </summary>
    public enum RunStateResult
    {
        Ok = 0,
        InvalidArgument,
        ValueLimit,
        SlotOccupied,
        SlotUnavailable,
        DuplicateInstance,
        UnknownInstance,
        NotPermutation,
        ResourceLocked,
        CashInsufficient,
        RpInsufficient,
        IpInsufficient,
        LevelMax,
        StockInsufficient,
        CapacityExceeded,
        EffectConflict,
        ContractActiveExists,
        ContractNoneActive,
        ContractExpired,
        ItemNone,
        OverdriveCoolingDown,
        OverdriveActive,
        InvariantViolated,
    }
}
