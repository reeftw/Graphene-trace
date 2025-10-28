using System;
using System.IO;
using System.Linq;
using Xunit;

namespace GrapheneTrace.Tests
{
    public class HeatmapSmokeTest
    {
        [Fact]
        public void CsvFile_CanBeRead_AndHas32ColumnsPerRow()
        {
            // Arrange
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
            var csvPath = Path.Combine(root, "GrapheneTrace", "wwwroot", "GTLBData", "1c0fd777_20251011.csv");

            Assert.True(File.Exists(csvPath), $"Test CSV not found at {csvPath}");

            // Act
            var lines = File.ReadLines(csvPath).Take(32).ToArray();

            // Assert
            Assert.Equal(32, lines.Length);
            foreach (var line in lines)
            {
                var cols = line.Split(',');
                Assert.True(cols.Length >= 32, "Each row should have at least 32 comma-separated values");
                // ensure each value parses as int (or empty treated as zero)
                foreach (var c in cols.Take(32))
                {
                    if (!int.TryParse(c.Trim(), out _))
                    {
                        // allow empty strings but not non-numeric garbage
                        Assert.True(string.IsNullOrWhiteSpace(c), "Numeric value expected or empty cell");
                    }
                }
            }
        }
    }
}
