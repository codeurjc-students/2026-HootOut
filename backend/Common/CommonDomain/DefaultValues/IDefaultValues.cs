namespace HootOut.CommonDomain.DefaultValues
{
    public interface IDefaultValues
    {
        Task InitAsync(CancellationToken ct = default);

        /// <summary>
        ///  0 Maximun priority, 100 lower
        /// </summary>
        int Priority => 100;
    }
}
