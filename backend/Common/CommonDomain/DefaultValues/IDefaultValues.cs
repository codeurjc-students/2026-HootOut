namespace HootOut.CommonDomain.DefaultValues
{
    public interface IDefaultValues
    {
        Task InitAsync(CancellationToken ct = default);

        int Priority => 100; // 0 Maximun priority, 100 lower
    }
}
