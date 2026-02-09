using System.ComponentModel.DataAnnotations;

namespace InfotecsWebApi.Models
{
    public class ValueEntry
    {
        [Key]
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public double ExecutionTime { get; set; }
        public double Value { get; set; }
    }
}
