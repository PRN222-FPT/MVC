using System;
using System.Collections.Generic;
using Pgvector;

namespace DataAccessLayer.Models;

public partial class Chunk
{
    public Guid ChunkId { get; set; }

    public Guid DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public Vector? Embedding { get; set; }

    public virtual Document Document { get; set; } = null!;
}
