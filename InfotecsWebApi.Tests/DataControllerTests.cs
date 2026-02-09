namespace InfotecsWebApi.Tests;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using InfotecsWebApi.Data;
using InfotecsWebApi.Controllers;
using InfotecsWebApi.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;

public class DataControllerTests
{
    private ApplicationDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private IWebHostEnvironment GetMockWebHostEnvironment()
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(e => e.WebRootPath).Returns("wwwroot");
        mock.Setup(e => e.ContentRootPath).Returns(Directory.GetCurrentDirectory());
        mock.Setup(e => e.EnvironmentName).Returns("Test");
        return mock.Object;
    }

    [Fact]
    public async Task ReadCSV_ValidFile_ReturnsOkAndSavesData()
    {
        //arrange
        var dbContext = GetInMemoryDbContext();
        var environment = GetMockWebHostEnvironment();
        var controller = new DataController(dbContext, environment);

        var csvContent = "Date;ExecutionTime;Value\n2026-02-01T10-00-00.0000Z;1.4;10.1\n2026-02-01T10-05-00.0000Z;2.0;15.2";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var formFile = new FormFile(stream, 0, stream.Length, "file", "test.csv");

        //act
        var result = await controller.ReadCSV(formFile);

        //assert
        var okResult = Assert.IsType<OkResult>(result);
        Assert.NotNull(okResult);

        Assert.Equal(2, await dbContext.Values.CountAsync());
        Assert.Equal(1, await dbContext.Results.CountAsync());

        var savedResult = await dbContext.Results.FirstAsync();
        Assert.Equal("test.csv", savedResult.FileName);
        Assert.Equal(300,savedResult.DeltaTimeSec);
        Assert.Equal(1.7, savedResult.AverageExecutionTime);
        Assert.Equal(12.65, savedResult.AverageValue);
    }

    [Fact]
    public async Task ReadCSV_InvalidDateBefore2000_ReturnsBadRequest()
    {
        //arrange
        var dbContext = GetInMemoryDbContext();
        var environment = GetMockWebHostEnvironment();
        var controller = new DataController(dbContext, environment);

        var csvContent = "Date;ExecutionTime;Value\n1999-01-01T10-00-00.0000Z;1.4;10.1";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var formFile = new FormFile(stream, 0, stream.Length, "file", "invalid_date_before.csv");

        //act
        var result = await controller.ReadCSV(formFile);

        //assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = badRequestResult.Value as dynamic;

        Assert.NotNull(errorResponse);
        Assert.Contains("Date is incorrect", errorResponse);
        Assert.Equal(0, await dbContext.Values.CountAsync());
        Assert.Equal(0, await dbContext.Results.CountAsync());
    }

    [Fact]
    public async Task ReadCSV_InvalidDateAfterCurrent_ReturnsBadRequest()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        var environment = GetMockWebHostEnvironment();
        var controller = new DataController(dbContext, environment);

        var futureDate = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-ddTHH-mm-ss.ffffZ");
        var csvContent = $"Date;ExecutionTime;Value\n{futureDate};1.5;10.5";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var formFile = new FormFile(stream, 0, stream.Length, "file", "invalid_date_after.csv");

        // Act
        var result = await controller.ReadCSV(formFile);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = badRequestResult.Value as dynamic;
        Assert.NotNull(errorResponse);
        Assert.Contains("Date is incorrect", errorResponse);
        Assert.Equal(0, await dbContext.Values.CountAsync());
        Assert.Equal(0, await dbContext.Results.CountAsync());
    }

    [Fact]
    public async Task ReadCSV_MissingValue_ReturnsBadRequest()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        var environment = GetMockWebHostEnvironment();
        var controller = new DataController(dbContext, environment);

        var csvContent = "Date;ExecutionTime;Value\n2026-02-01T10-00-00.0000Z;1.5;";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var formFile = new FormFile(stream, 0, stream.Length, "file", "missing_value.csv");

        // Act
        var result = await controller.ReadCSV(formFile);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = badRequestResult.Value as dynamic;
        Assert.NotNull(errorResponse);
        Assert.Contains("Wrong input line 1", errorResponse);
        Assert.Equal(0, await dbContext.Values.CountAsync());
        Assert.Equal(0, await dbContext.Results.CountAsync());
    }

    private async Task SeedResults(ApplicationDbContext dbContext)
    {
        dbContext.Results.AddRange(
            new ResultEntry { Id = 1, FileName = "file1", MinDateTime = new DateTime(2026, 1, 1), AverageValue = 10.0, AverageExecutionTime = 1.0 },
            new ResultEntry { Id = 2, FileName = "file2", MinDateTime = new DateTime(2026, 1, 15), AverageValue = 20.0, AverageExecutionTime = 2.0 },
            new ResultEntry { Id = 3, FileName = "file1", MinDateTime = new DateTime(2026, 2, 1), AverageValue = 15.0, AverageExecutionTime = 1.5 },
            new ResultEntry { Id = 4, FileName = "file3", MinDateTime = new DateTime(2026, 2, 9), AverageValue = 25.0, AverageExecutionTime = 2.5 }
        );
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task FilterResults_FilterByFileName_ReturnsFilteredResults()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        await SeedResults(dbContext);
        var environment = GetMockWebHostEnvironment();
        var controller = new DataController(dbContext, environment);

        // Act
        var result = await controller.FilterResults("file1", null, null, null, null, null, null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var results = Assert.IsAssignableFrom<IEnumerable<ResultEntry>>(okResult.Value);
        Assert.Equal(2, results.Count());
        Assert.All(results, r => Assert.Equal("file1", r.FileName));
    }

    private async Task SeedValues(ApplicationDbContext dbContext, string fileName, int count)
    {
        for (int i = 0; i < count; i++)
        {
            dbContext.Values.Add(new ValueEntry
            {
                Id = i + 1,
                FileName = fileName,
                Date = DateTime.UtcNow.AddMinutes(-i),
                ExecutionTime = i + 0.5,
                Value = i * 10.0
            });
        }
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task ListOfTenResults_ExistingFileWithMoreThan10Values_ReturnsLast10()
    {
        // Arrange
        var dbContext = GetInMemoryDbContext();
        await SeedValues(dbContext, "test_file.csv", 15); // 15 записей
        var environment = GetMockWebHostEnvironment();
        var controller = new DataController(dbContext, environment);

        // Act
        var result = await controller.ListOfTenResults("test_file.csv");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var values = Assert.IsAssignableFrom<IEnumerable<ValueEntry>>(okResult.Value);
        Assert.Equal(10, values.Count());

        // Проверяем, что они отсортированы по убыванию даты
        var sortedValues = values.ToList();
        for (int i = 0; i < sortedValues.Count - 1; i++)
        {
            Assert.True(sortedValues[i].Date >= sortedValues[i + 1].Date);
        }
    }
}