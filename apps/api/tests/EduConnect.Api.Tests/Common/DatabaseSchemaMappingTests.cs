using EduConnect.Api.Infrastructure.Database;
using EduConnect.Api.Infrastructure.Database.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace EduConnect.Api.Tests.Common;

public class DatabaseSchemaMappingTests
{
    [Fact]
    public void UserEntity_MapsToSnakeCaseTableAndColumns()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"SchemaMapping_{Guid.NewGuid()}")
            .Options;

        using var context = new AppDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(UserEntity));
        entityType.Should().NotBeNull();

        var userEntityType = entityType!;
        userEntityType.GetTableName().Should().Be("users");

        var table = StoreObjectIdentifier.Table(userEntityType.GetTableName()!, userEntityType.GetSchema());

        userEntityType.FindProperty(nameof(UserEntity.Id))!.GetColumnName(table).Should().Be("id");
        userEntityType.FindProperty(nameof(UserEntity.SchoolId))!.GetColumnName(table).Should().Be("school_id");
        userEntityType.FindProperty(nameof(UserEntity.PinHash))!.GetColumnName(table).Should().Be("pin_hash");
        userEntityType.FindProperty(nameof(UserEntity.CreatedAt))!.GetColumnName(table).Should().Be("created_at");
    }

    [Fact]
    public void HomeworkEntity_MapsToHomeworkTable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"SchemaMapping_{Guid.NewGuid()}")
            .Options;

        using var context = new AppDbContext(options);

        context.Model.FindEntityType(typeof(HomeworkEntity))!
            .GetTableName()
            .Should().Be("homework");
    }
}
