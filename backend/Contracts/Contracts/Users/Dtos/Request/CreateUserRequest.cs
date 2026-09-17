using System;
using System.Collections.Generic;
using System.Text;

namespace HootOut.Contracts.Users.Dtos.Request
{
    public class CreateUserRequest
    {
        public required string Username { get; set; }
        public required string Email { get; set; }

        public required string Password { get; set; }
    }
}
