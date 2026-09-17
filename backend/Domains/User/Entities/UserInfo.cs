using HootOut.Contracts.Common.Saver;

namespace HootOut.Users.Entities
{
    public class UserInfo : Persistable
    {
        public string Username { get; set; }

        public string Email { get; set; }

        public string Password { get; set; }
    }
}
