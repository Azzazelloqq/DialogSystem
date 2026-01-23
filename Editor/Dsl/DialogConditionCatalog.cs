using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DialogSystem.Runtime.Conditions;
using UnityEditor;

namespace DialogSystem.Editor.Dsl
{
public static class DialogConditionCatalog
{
    private static List<DialogConditionInfo> _cached;
    private static bool _dirty = true;
    private static int _version;

    static DialogConditionCatalog()
    {
        AssemblyReloadEvents.afterAssemblyReload += MarkDirty;
    }

    public static int Version => _version;

    public static IReadOnlyList<DialogConditionInfo> GetConditions()
    {
        if (_cached == null || _dirty)
        {
            _cached = BuildConditions();
            _dirty = false;
            _version++;
        }

        return _cached;
    }

    public static void MarkDirty()
    {
        _dirty = true;
    }

    private static List<DialogConditionInfo> BuildConditions()
    {
        var list = new List<DialogConditionInfo>();
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t != null);
                }
            })
            .Where(type => typeof(IDialogCondition).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
            .ToList();

        foreach (var type in types)
        {
            var attribute = type.GetCustomAttribute<DialogConditionAttribute>();
            if (attribute != null)
            {
                list.Add(new DialogConditionInfo(attribute.Id, attribute.DisplayName));
                continue;
            }

            if (type.GetConstructor(Type.EmptyTypes) == null)
            {
                continue;
            }

            try
            {
                if (Activator.CreateInstance(type) is IDialogCondition instance &&
                    !string.IsNullOrWhiteSpace(instance.Id))
                {
                    list.Add(new DialogConditionInfo(instance.Id, instance.DisplayName));
                }
            }
            catch
            {
                // Ignore faulty conditions.
            }
        }

        return list
            .Distinct(new DialogConditionInfoComparer())
            .OrderBy(info => info.DisplayName)
            .ToList();
    }

    private sealed class DialogConditionInfoComparer : IEqualityComparer<DialogConditionInfo>
    {
        public bool Equals(DialogConditionInfo x, DialogConditionInfo y)
        {
            return string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(DialogConditionInfo obj)
        {
            return obj.Id?.ToLowerInvariant().GetHashCode() ?? 0;
        }
    }
}

public readonly struct DialogConditionInfo
{
    public string Id { get; }
    public string DisplayName { get; }

    public DialogConditionInfo(string id, string displayName)
    {
        Id = id;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
    }
}
}
