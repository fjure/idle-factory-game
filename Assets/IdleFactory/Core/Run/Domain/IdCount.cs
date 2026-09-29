namespace IdleFactory.Run.Domain
{
    /// <summary>An ID with a count or level; collections of these are always sorted by ordinal ID.</summary>
    public readonly struct IdCount
    {
        public string Id { get; }
        public long Count { get; }
        public IdCount(string id, long count)
        {
            Id = id;
            Count = count;
        }
    }
}
