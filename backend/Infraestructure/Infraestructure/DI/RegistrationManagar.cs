using Autofac;
using Microsoft.Extensions.DependencyModel;
using System.Reflection;

namespace HootOut.Infraestructure.DI
{
    public static class RegistratorManager
    {
        private static readonly string projectStartingName = "HootOut";

        public static ContainerBuilder RegisterAllAssemblies(ContainerBuilder builder)
        {

            var assemblies = DependencyContext.Default?.RuntimeLibraries
            .Where(lib => lib.Name.StartsWith(projectStartingName))
            .SelectMany(lib => lib.GetDefaultAssemblyNames(DependencyContext.Default))
            .Select(Assembly.Load)
            .Distinct()
            .ToList();

            IEnumerable<Type> registrationTypes = assemblies?
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IRegistrationContainer).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList() ?? Enumerable.Empty<Type>();

            foreach (var type in registrationTypes)
            {
                if (Activator.CreateInstance(type) is IRegistrationContainer instance)
                    instance.Register(builder);
            }

            return builder;
        }
    }
}