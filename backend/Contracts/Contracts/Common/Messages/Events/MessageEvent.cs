using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.Common.Messages.Events
{
    public class MessageEvent
    {
        public Guid ID { get; set; } = Guid.CreateVersion7();

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public required string Queue { get; set; }
    }
}
