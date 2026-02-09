using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using InfotecsWebApi.Models;
using InfotecsWebApi.Data;
using System.IO;
using System.Text;
using System.Globalization;
using System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace InfotecsWebApi.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class DataController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        public DataController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> ReadCSV(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("File not uploaded");
            }

            CultureInfo provider = CultureInfo.InvariantCulture;
            string format = "yyyy-MM-dd'T'HH-mm-ss.ffff'Z'";

            List<ValueEntry> values = new List<ValueEntry>();
            double totalExecutionTime = 0;
            List<double> totalValues = new List<double>();
            ResultEntry results = new ResultEntry();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string? line;
                int i = -1;
                DateTime dateMaxCount = new DateTime(2000, 1, 1, 0, 0, 0);
                DateTime dateMinCount = DateTime.Now;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    i++;
                    Console.WriteLine(line);
                    if (line[0] != 'D')
                    {
                        string[] substrings = line.Split(';');
                        if (substrings.Length != 3)
                        {
                            return BadRequest($"Wrong input line {i}");
                        }

                        DateTime dates = new DateTime();
                        bool dateError = DateTime.TryParseExact(substrings[0], format, provider, DateTimeStyles.None, out dates);

                        double execTime;
                        bool execError = Double.TryParse(substrings[1], provider, out execTime);

                        double value;
                        bool valueError = Double.TryParse(substrings[2], provider, out value);

                        if (dateError == false || execError == false || valueError == false)
                        {
                            return BadRequest($"Wrong input line {i}");
                        }

                        DateTime dateMin = new DateTime(2000, 1, 1, 0, 0, 0);
                        DateTime dateMax = DateTime.Now;
                        int resultMax = DateTime.Compare(dateMax, dates);
                        int resultMin = DateTime.Compare(dates, dateMin);

                        if (resultMax == -1 || resultMin == -1)
                        {
                            return BadRequest("Date is incorrect");
                        }
                        if (execTime < 0)
                        {
                            return BadRequest("Executione time is incorrect");
                        }
                        if (value < 0)
                        {
                            return BadRequest("Value is incorrect");
                        }
                        if (DateTime.Compare(dates, dateMinCount) < 0)
                        {
                            dateMinCount = dates;
                        }
                        if (DateTime.Compare(dates, dateMaxCount) > 0)
                        {
                            dateMaxCount = dates;
                        }

                        totalExecutionTime += execTime;
                        totalValues.Add(value);

                        ValueEntry temp = new ValueEntry();
                        temp.Date = DateTime.SpecifyKind(dates, DateTimeKind.Utc);
                        temp.Value = value;
                        temp.ExecutionTime = execTime;
                        temp.FileName = file.FileName;
                        values.Add(temp);
                    }
                }

                if (i > 10000)
                {
                    return BadRequest("String overflow");
                }
                   
                TimeSpan answerSeconds = dateMaxCount - dateMinCount;

                results.FileName = file.FileName;
                results.DeltaTimeSec = answerSeconds.TotalSeconds;              
                results.MinDateTime = DateTime.SpecifyKind(dateMinCount, DateTimeKind.Utc);
                results.AverageExecutionTime = Math.Round(totalExecutionTime / i, 2);          
                results.AverageValue = Math.Round(totalValues.Sum() / totalValues.Count, 2); 
                
                totalValues.Sort();
                if (totalValues.Count % 2 == 0)
                {
                    int mid = totalValues.Count / 2;
                    results.MedianValue = Math.Round((totalValues[mid - 1] + totalValues[mid]) / 2.0, 2); 
                }
                else
                {
                    results.MedianValue = totalValues[(totalValues.Count - 1) / 2];
                }       

                results.MaxValue = totalValues.Max();                           
                results.MinValue = totalValues.Min();                           
            }
            var removeValues = _context.Values.Where(x => x.FileName == file.FileName);
            _context.Values.RemoveRange(removeValues);
            var removeResults = _context.Results.Where(x => x.FileName == file.FileName);
            _context.Results.RemoveRange(removeResults);

            await _context.Values.AddRangeAsync(values);
            await _context.Results.AddAsync(results);

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("results")]
        public async Task<IActionResult> FilterResults([FromQuery] string? fileName,
            [FromQuery] DateTime? minDate,
            [FromQuery] DateTime? maxDate,
            [FromQuery] double? minAverageValue,
            [FromQuery] double? maxAverageValue,
            [FromQuery] double? minAverageExecutionTime,
            [FromQuery] double? maxAverageExecutionTime)
        {
            IQueryable<ResultEntry> query = _context.Results.AsQueryable();
            if (fileName != null)
            {
                query = query.Where(x => x.FileName == fileName);
            }
            if (minDate != null)
            {
                query = query.Where(x => x.MinDateTime >= minDate);
            }
            if (maxDate != null)
            {
                query = query.Where(x => x.MinDateTime <= maxDate);
            }
            if (minAverageValue != null)
            {
                query = query.Where(x => x.AverageValue >= minAverageValue);
            }
            if (maxAverageValue != null)
            {
                query = query.Where(x => x.AverageValue <= maxAverageValue);
            }
            if (minAverageExecutionTime != null)
            {
                query = query.Where(x => x.AverageExecutionTime >= minAverageExecutionTime);
            }
            if (maxAverageExecutionTime != null)
            {
                query = query.Where(x => x.AverageExecutionTime <= maxAverageExecutionTime);
            }

            var output = await query.ToListAsync();

            return Ok(output);
        }

        [HttpGet("{fileName}")]
        public async Task<IActionResult> ListOfTenResults(string fileName)
        {
            var output = await _context.Values
                .Where(x => x.FileName == fileName)
                .OrderByDescending(x => x.Date)
                .Take(10)
                .ToListAsync(); 

            return Ok(output);
        }
    }
}
