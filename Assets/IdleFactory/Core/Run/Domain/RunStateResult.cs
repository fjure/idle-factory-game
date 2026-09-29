namespace IdleFactory.Run.Domain
{
    /// <summary>
    /// Outcome of an atomic run-state mutation. Anything other than <see cref="Ok"/> means the state was not
    /// changed at all (no partial mutation). The names map one-to-one to the stable rejection codes of
    /// contracts/run-application.md that the application layer will expose.
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
