using System;
using System.Linq;
using System.Reflection;
using StardewModdingAPI;

namespace DynamicPortraitSlicer;

internal static class GmcmReflection
{
    public static MethodInfo GetMethodByParamCount(IMonitor monitor, Type apiType, string name, int paramCount)
    {
        var all = apiType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == name)
            .ToArray();

        var matches = all
            .Where(m => m.GetParameters().Length == paramCount)
            .ToArray();

        if (matches.Length == 1)
            return matches[0];

        monitor.Log($"GMCM reflection: couldn't uniquely resolve {name} with {paramCount} params (found {matches.Length}).", LogLevel.Warn);
        foreach (var m in all)
        {
            string sig = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName));
            monitor.Log($"GMCM reflection overload: {name}({sig})", LogLevel.Warn);
        }

        throw new MissingMethodException($"GMCM method '{name}' with {paramCount} params not found or ambiguous.");
    }

    public static MethodInfo GetMethodByParamTypes(IMonitor monitor, Type apiType, string name, params Type[] paramTypes)
    {
        var all = apiType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == name)
            .ToArray();

        var matches = all
            .Where(m =>
            {
                var p = m.GetParameters();
                if (p.Length != paramTypes.Length)
                    return false;

                for (int i = 0; i < p.Length; i++)
                {
                    if (p[i].ParameterType != paramTypes[i])
                        return false;
                }

                return true;
            })
            .ToArray();

        if (matches.Length == 1)
            return matches[0];

        monitor.Log($"GMCM reflection: couldn't uniquely resolve {name} with specified param types (found {matches.Length}).", LogLevel.Warn);
        foreach (var m in all)
        {
            string sig = string.Join(", ", m.GetParameters().Select(p => p.ParameterType.FullName));
            monitor.Log($"GMCM reflection overload: {name}({sig})", LogLevel.Warn);
        }

        throw new MissingMethodException($"GMCM method '{name}' with the requested parameter types not found or ambiguous.");
    }
}