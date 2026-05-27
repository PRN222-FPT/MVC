using System;
using System.Collections.Generic;

namespace DataAccessLayer.Models;

public partial class Message
{
    public Guid MessageId { get; set; }

    public Guid SessionId { get; set; }

    public string SenderRole { get; set; } = null!;

    public string MessageContent { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual Session Session { get; set; } = null!;
}
