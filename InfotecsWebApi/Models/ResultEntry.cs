using System.ComponentModel.DataAnnotations;

namespace InfotecsWebApi.Models
{
    public class ResultEntry
    {
        [Key]
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public double DeltaTimeSec { get; set; }
        public DateTime MinDateTime { get; set; }
        public double AverageExecutionTime { get; set; }
        public double AverageValue { get; set; }
        public double MedianValue { get; set; }
        public double MaxValue { get; set; }
        public double MinValue { get; set; }

    }
}
