using System;

namespace DataAccessLayer.Models;

public partial class ChunkingSetting
{
    public short Id { get; set; }

    public int ChunkSizeCharacters { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedBy { get; set; }

    public virtual User? UpdatedByNavigation { get; set; }
}
