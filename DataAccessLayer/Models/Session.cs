using System;
using System.Collections.Generic;

namespace DataAccessLayer.Models;

public partial class Session
{
    public Guid SessionId { get; set; }

    public Guid UserId { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public virtual ICollection<Message> Messages { get; set; } = new List<Message>();

    public virtual User User { get; set; } = null!;
}
