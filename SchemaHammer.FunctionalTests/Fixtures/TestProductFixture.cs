// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using Schema.Domain;
using Schema.Utility;

namespace SchemaHammer.FunctionalTests.Fixtures;

public abstract class TestProductFixture
{
    public string TempDir { get; private set; } = string.Empty;
    public Product? Product { get; protected set; }

    [SetUp]
    public void SetUp()
    {
        TempDir = Path.Combine(Path.GetTempPath(), "SchemaHammerFT_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDir);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(TempDir))
                Directory.Delete(TempDir, recursive: true);
        }
        catch
        {
            // Best effort — ignore cleanup failures
        }
    }

    public Product BuildStandardSqlServerProduct()
    {
        Product = new TestProductBuilder()
            .WithTemplate("Main", t => t
                .WithTable("[dbo].[Users]", table => table
                    .WithColumn("[Id]", "int", nullable: false)
                    .WithColumn("[Name]", "nvarchar(100)")
                    .WithColumn("[Email]", "nvarchar(255)")
                    .WithIndex("[IX_Users_Email]", "[Email]", unique: true))
                .WithTable("[dbo].[Orders]", table => table
                    .WithColumn("[OrderId]", "int", nullable: false)
                    .WithColumn("[UserId]", "int", nullable: false)
                    .WithColumn("[OrderDate]", "datetime2")
                    .WithForeignKey("[FK_Orders_Users]", "[UserId]", "[dbo].[Users]", "[Id]"))
                .WithScriptFolder("Views")
                .WithScript("Views", "vw_ActiveUsers.sql",
                    "CREATE OR ALTER VIEW [dbo].[vw_ActiveUsers] AS SELECT [Id], [Name], [Email] FROM [dbo].[Users];"))
            .Build(TempDir);

        return Product;
    }

    public Product ReloadProduct()
    {
        // LoadProduct takes the directory path, not the .json file path
        var service = new SchemaHammer.Services.ProductTreeService();
        service.LoadProduct(TempDir);
        Product = service.Product;
        return Product!;
    }
}
