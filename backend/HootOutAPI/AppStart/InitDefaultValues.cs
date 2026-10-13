using Autofac;
using HootOut.CommonDomain.DefaultValues;

namespace HootOut.HootOutAPI.AppStart
{
    public static class InitDefaultValues
    {
        public static async Task InitAsync(ILifetimeScope builder, CancellationToken ct = default)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            using (var container = builder.BeginLifetimeScope())
            {
                var defaultValues = container.Resolve<IEnumerable<IDefaultValues>>().OrderBy(x => x.Priority);

                foreach (var defaultValue in defaultValues)
                {
                    await defaultValue.InitAsync();
                }
            }
        }
    }
}
