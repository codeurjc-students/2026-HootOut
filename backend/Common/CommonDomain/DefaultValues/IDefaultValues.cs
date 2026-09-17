namespace HootOut.CommonDomain.DefaultValues
{
    public interface IDefaultValues
    {
        void Init();

        int Priority => 100; // 0 Maximun priority, 100 lower
    }
}
