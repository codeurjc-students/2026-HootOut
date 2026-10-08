namespace HootOut.Contracts.Authentication.Services
{
    public interface IPasswordHasherService
    {
        string HashPassword(string password);

        bool VerifyPassword(string password, string hashedPassword);

        public string DummyHash => HashPassword(Guid.NewGuid().ToString());
    }
}
