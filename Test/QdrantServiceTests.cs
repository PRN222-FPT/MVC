using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using ServiceLayer.Interfaces;
using ServiceLayer.Options;
using ServiceLayer.Services;
using Xunit;

namespace Test;

/// <summary>
/// Contains unit tests for the <see cref="QdrantService"/> implementation.
/// </summary>
public class QdrantServiceTests
{
    [Fact]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        var options = Options.Create(new QdrantOptions());
        var logger = NullLogger<QdrantService>.Instance;

        Assert.Throws<ArgumentNullException>(() => new QdrantService(null!, options, logger));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        var client = new QdrantClient("localhost");
        var logger = NullLogger<QdrantService>.Instance;

        Assert.Throws<ArgumentNullException>(() => new QdrantService(client, null!, logger));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var client = new QdrantClient("localhost");
        var options = Options.Create(new QdrantOptions());

        Assert.Throws<ArgumentNullException>(() => new QdrantService(client, options, null!));
    }

    [Fact]
    public async Task UpsertVectorsAsync_NullPoints_ThrowsArgumentNullException()
    {
        var client = new QdrantClient("localhost");
        var options = Options.Create(new QdrantOptions());
        var logger = NullLogger<QdrantService>.Instance;
        var service = new QdrantService(client, options, logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.UpsertVectorsAsync(null!));
    }

    [Fact]
    public async Task SearchAsync_NullQueryVector_ThrowsArgumentNullException()
    {
        var client = new QdrantClient("localhost");
        var options = Options.Create(new QdrantOptions());
        var logger = NullLogger<QdrantService>.Instance;
        var service = new QdrantService(client, options, logger);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SearchAsync(null!));
    }
}
