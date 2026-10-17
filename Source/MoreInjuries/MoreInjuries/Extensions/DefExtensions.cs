using System.Collections.Generic;
using Verse;

namespace MoreInjuries.Extensions;

public static class DefExtensions
{
    extension(Def self)
    {
        public T? GetCachedModExtension<T>() where T : DefModExtension => ModExtensionCache<T>.Get(self);

        public T GetRequiredModExtension<T>() where T : DefModExtension
        {
            T? modExtension = self.GetModExtension<T>();
            if (modExtension is null)
            {
                self.ThrowMissingRequiredModExtensionException<T>();
            }
            return modExtension;
        }

        [DoesNotReturn]
        private void ThrowMissingRequiredModExtensionException<T>() =>
            throw new InvalidOperationException($"Def {self.defName} is missing required mod extension of type {typeof(T).FullName}");
    }
}

file static class ModExtensionCache<T> where T : DefModExtension
{
    private static readonly Dictionary<Def, T?> s_extensions = [];

    public static T? Get(Def def)
    {
        if (s_extensions.TryGetValue(def, out T? extension))
        {
            return extension;
        }
        extension = def.GetModExtension<T>();
        s_extensions.Add(def, extension);
        return extension;
    }
}
