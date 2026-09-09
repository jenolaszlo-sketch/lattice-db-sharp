namespace LatticeDBSharp;

/// <summary>A detached row snapshot with stable column order.</summary>
public sealed class LatticeRow
{
    private readonly IReadOnlyList<string> columns;
    private readonly IReadOnlyList<LatticeValue> values;

    internal LatticeRow(IReadOnlyList<string> columns, IReadOnlyList<LatticeValue> values)
    {
        this.columns = columns;
        this.values = values;
    }

    /// <summary>The number of values in this row.</summary>
    public int Count => values.Count;

    public string GetName(int ordinal)
    {
        CheckOrdinal(ordinal);
        return columns[ordinal];
    }

    public LatticeValue GetValue(int ordinal)
    {
        CheckOrdinal(ordinal);
        return values[ordinal];
    }

    /// <summary>Gets a value by its zero-based ordinal.</summary>
    public LatticeValue this[int ordinal] => GetValue(ordinal);

    public LatticeValue this[string name]
    {
        get
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            var ordinal = -1;
            for (var index = 0; index < columns.Count; index++)
            {
                if (!string.Equals(columns[index], name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ordinal >= 0)
                {
                    throw new InvalidOperationException($"Column name '{name}' is ambiguous.");
                }

                ordinal = index;
            }

            if (ordinal < 0)
            {
                throw new KeyNotFoundException($"Column '{name}' was not found.");
            }

            return values[ordinal];
        }
    }

    public bool TryGetOrdinal(string name, out int ordinal)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ordinal = -1;
        for (var index = 0; index < columns.Count; index++)
        {
            if (!string.Equals(columns[index], name, StringComparison.Ordinal))
            {
                continue;
            }

            if (ordinal >= 0)
            {
                ordinal = -1;
                return false;
            }

            ordinal = index;
        }

        return ordinal >= 0;
    }

    private void CheckOrdinal(int ordinal)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(ordinal, values.Count);
    }
}
