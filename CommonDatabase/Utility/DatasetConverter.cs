using System;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;

namespace CommonDatabase.Utility
{
    public static class DatasetConverter
    {
        public static string DatasetToJson(DataSet ds)
        {
            var result = new Dictionary<string, object>();

            for (int i = 0; i < ds.Tables.Count; i++)
            {
                var table = ds.Tables[i];
                var rows = new List<Dictionary<string, object>>();

                foreach (DataRow row in table.Rows)
                {
                    var rowData = new Dictionary<string, object>();
                    foreach (DataColumn column in table.Columns)
                    {
                        rowData[column.ColumnName] = row[column];
                    }
                    rows.Add(rowData);
                }

                result[$"Table{i + 1}"] = rows;
            }

            return JsonSerializer.Serialize(result);
        }
    }
}
