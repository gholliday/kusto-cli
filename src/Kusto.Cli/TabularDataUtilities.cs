namespace Kusto.Cli;

public static class TabularDataUtilities
{
    public static int GetPreferredColumnIndex(TabularData table, string preferredColumnName)
    {
        if (table.TryGetColumnIndex(preferredColumnName, out var index))
        {
            return index;
        }

        return table.Columns.Count > 0 ? 0 : -1;
    }

    public static Dictionary<string, string?> ConvertRowToProperties(TabularData table, int rowIndex)
    {
        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (rowIndex >= table.Rows.Count)
        {
            return properties;
        }

        var row = table.Rows[rowIndex];
        for (var i = 0; i < table.Columns.Count; i++)
        {
            var value = i < row.Count ? row[i] : null;
            properties[table.Columns[i]] = value;
        }

        return properties;
    }
}
