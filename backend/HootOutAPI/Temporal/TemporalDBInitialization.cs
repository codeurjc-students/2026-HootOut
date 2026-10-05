using HootOut.CommonDomain.DefaultValues;
using HootOut.Contracts.Users.Dtos.Request;
using HootOut.Contracts.Users.Services;

namespace HootOut.HootOutAPI.Temporal
{
    public class TemporalDBInitialization : IDefaultValues
    {
        private IUserService userService;
        public TemporalDBInitialization(IUserService userService)
        {
            this.userService = userService ?? throw new ArgumentNullException(nameof(userService));
        }
        public void Init()
        {
            for (int i = 0; i < 10; i++)
            {
                string tempId = Guid.NewGuid().ToString();
                userService.CreateUser(new CreateUserRequest
                {
                    Email = $"email{tempId}.email.com",
                    Password = "password",
                    Username = $"username-{tempId}",
                });
            }
        }
    }
}
