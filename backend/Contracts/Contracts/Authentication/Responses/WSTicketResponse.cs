using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.Authentication.Responses
{
    public class WSTicketResponse
    {
        public string Ticket { get; init; } = string.Empty;

        public int ExpiresIn { get; init; }
    }
}
