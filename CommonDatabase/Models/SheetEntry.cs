using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonDatabase.Models
{
    public class EditedCells : Dictionary<string, double>
    {
        // This allows dynamic keys like "a4", "b7" with double values
    }

    public class SheetData
    {
        public string Url { get; set; }
        public EditedCells EditedCells { get; set; }
        public SheetModel SheetJSON { get; set; }
    }

    public class SheetEntry
    {
        public string Type { get; set; } // "json" or "html"
        public SheetData Data { get; set; }
        public string SheetName { get; set; }
        public int SheetId { get; set; }
        public string LastUpdated { get; set; }
    }
    public class SheetModel
    {
        public int TotalRows { get; set; }
        public int TotalColumns { get; set; }
        public Dictionary<string, Cell> Cells { get; set; }
    }

    public class Cell
    {
        public object Value { get; set; }   // can be null, string, number, etc.
        public string Formula { get; set; }
        public string Type { get; set; }    // e.g. "static"
        public CellFormat Format { get; set; }
    }

    public class CellFormat
    {
        public double FontSize { get; set; }
        public string FontStyle { get; set; }          // e.g. "r"
        public string FontColor { get; set; }          // hex color
        public string BackgroundColor { get; set; }    // hex color
        public string NumberFormat { get; set; }       // e.g. "General"
        public string HorizontalAlign { get; set; }
        public string VerticalAlign { get; set; }
    }

}
