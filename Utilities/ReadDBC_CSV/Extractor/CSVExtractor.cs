using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using CsvHelper;

namespace ReadDBC_CSV
{
    internal sealed class CSVExtractor
    {
        public List<string> ColumnIndexes { get; } = new();

        public Action HeaderAction;

        public void ExtractTemplate(string file, Action<string[]> extractLine)
        {
            using StreamReader reader = new(file);
            using CsvReader csv = new(reader, CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            ColumnIndexes.AddRange(csv.HeaderRecord);
            HeaderAction();

            while (csv.Read())
            {
                IDictionary<string, object> record = csv.GetRecord<dynamic>();
                string[] data = new string[ColumnIndexes.Count];
                int i = 0;
                foreach (KeyValuePair<string, object> r in record)
                {
                    data[i++] = r.Value.ToString();
                }

                extractLine(data);
            }
        }

        public int FindIndex(string v, string vFallback = "", int def = -1)
        {
            for (int i = 0; i < ColumnIndexes.Count; i++)
            {
                string column = ColumnIndexes[i];

                if (column.StartsWith(v, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
                else if (!string.IsNullOrEmpty(vFallback) &&
                    column.StartsWith(vFallback, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            Console.WriteLine($"  WARN '{v}' or '{vFallback}' not found using {def}");
            return def;
            //throw new ArgumentOutOfRangeException(v);
        }
    }
}
