namespace IdleFactory.Run.Content
{
    public sealed class ResourceDef
    {
        public string Id { get; }
        public string Label { get; }
        public string PresentationKey { get; }
        public long BasePrice { get; }
        public UnlockCondition Unlock { get; }

        public ResourceDef(string id, string label, string presentationKey, long basePrice, UnlockCondition unlock)
        {
            Id = ContentGuard.Id(id, nameof(id));
            Label = ContentGuard.Text(label, nameof(label));
            PresentationKey = ContentGuard.Text(presentationKey, nameof(presentationKey));
            BasePrice = ContentGuard.Quantity(basePrice, nameof(basePrice));
            Unlock = unlock ?? throw new System.ArgumentNullException(nameof(unlock));
        }
    }
}
