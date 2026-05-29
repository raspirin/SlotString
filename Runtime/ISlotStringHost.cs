namespace SlotStrings
{
    /// <summary>The data source a <see cref="SlotString"/> reads placeholder values and cache-invalidation tokens from.</summary>
    public interface ISlotStringHost
    {
        /// <summary>Returns the value for the placeholder at <paramref name="slot"/>; must be non-null.</summary>
        string Access(int slot);

        /// <summary>Returns an opaque token that changes whenever any data <see cref="Access"/> could return changes; compared only for equality (counters and hashes are both valid).</summary>
        int GetStateToken();
    }
}
