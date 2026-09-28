namespace IdleFactory.Unity.Content
{
    /// <summary>
    /// Runs the strict reader, semantic validator, and domain mapping in contract order.
    /// </summary>
    public static class GameplayContentLoader
    {
        public static ContentLoadResult Load(byte[] bytes)
        {
            var readResult = GameplayContentReader.Read(bytes);
            if (!readResult.IsSuccess)
            {
                return ContentLoadResult.Rejected(readResult.Diagnostic);
            }

            return JsonDefinitionCatalogBuilder.Build(readResult.Document);
        }
    }
}
