namespace LatticeDbSharp;

/// <summary>A detached row snapshot with stable column order.</summary>
public sealed class LatticeRow
{
    private readonly LatticeRowMetadata metadata;
    private readonly IReadOnlyList<LatticeValue> values;

    internal LatticeRow(IReadOnlyList<string> columns, IReadOnlyList<LatticeValue> values)
        : this(new LatticeRowMetadata(columns), values)
    {
    }

    internal LatticeRow(LatticeRowMetadata metadata, IReadOnlyList<LatticeValue> values)
    {
        this.metadata = metadata;
        this.values = values;
        if (metadata.Columns.Count != values.Count)
            throw new ArgumentException("Column and value counts must match.", nameof(values));
    }

    /// <summary>The number of values in this row.</summary>
    public int Count => values.Count;

    /// <summary>Gets the column name at a zero-based ordinal.</summary>
    public string GetName(int ordinal)
    {
        CheckOrdinal(ordinal);
        return metadata.Columns[ordinal];
    }

    /// <summary>Gets the value at a zero-based ordinal.</summary>
    public LatticeValue GetValue(int ordinal)
    {
        CheckOrdinal(ordinal);
        return values[ordinal];
    }

    /// <summary>Gets a value by its zero-based ordinal.</summary>
    public LatticeValue this[int ordinal] => GetValue(ordinal);

    /// <summary>Gets the value of a uniquely named column.</summary>
    public LatticeValue this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            if (!metadata.Ordinals.TryGetValue(name, out var ordinal))
            {
                throw new KeyNotFoundException($"Column '{name}' was not found.");
            }

            if (ordinal == -2)
            {
                throw new InvalidOperationException($"Column name '{name}' is ambiguous.");
            }

            return values[ordinal];
        }
    }

    /// <summary>Resolves a unique column name to its ordinal, or false when missing or ambiguous.</summary>
    public bool TryGetOrdinal(string name, out int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!metadata.Ordinals.TryGetValue(name, out ordinal) || ordinal < 0)
        {
            ordinal = -1;
            return false;
        }

        return true;
    }

    private void CheckOrdinal(int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(ordinal, values.Count);
    }
}

internal sealed class LatticeRowMetadata
{
    internal LatticeRowMetadata(IReadOnlyList<string> columns)
    {
        Columns = columns;
        var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < columns.Count; index++)
        {
            if (lookup.ContainsKey(columns[index]))
                lookup[columns[index]] = -1;
            else
                lookup.Add(columns[index], index);
        }

        Ordinals = lookup;
    }

    internal IReadOnlyList<string> Columns { get; }

    internal IReadOnlyDictionary<string, int> Ordinals { get; }
}
