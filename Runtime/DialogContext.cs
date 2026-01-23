using System;
using System.Collections.Generic;

namespace DialogSystem.Runtime
{
public interface IDialogContext
{
    bool TryGetVariable(string name, out object value);
    void SetVariable(string name, object value);
    bool TryCallFunction(string name, IReadOnlyList<object> args, out object result);
    bool TryResolveDialog(string dialogId, out DialogDefinition dialog);
    bool TryGetService<T>(out T service) where T : class;
    void RegisterService<T>(T service) where T : class;
}

public sealed class DialogContext : IDialogContext
{
    private readonly Dictionary<string, object> _variables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<IReadOnlyList<object>, object>> _functions =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DialogDefinition> _dialogs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, object> _services = new();

    public bool TryGetVariable(string name, out object value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            value = null;
            return false;
        }

        return _variables.TryGetValue(name, out value);
    }

    public void SetVariable(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _variables[name] = value;
    }

    public bool TryCallFunction(string name, IReadOnlyList<object> args, out object result)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            result = null;
            return false;
        }

        if (_functions.TryGetValue(name, out var func))
        {
            result = func(args);
            return true;
        }

        result = null;
        return false;
    }

    public bool TryResolveDialog(string dialogId, out DialogDefinition dialog)
    {
        if (string.IsNullOrWhiteSpace(dialogId))
        {
            dialog = null;
            return false;
        }

        return _dialogs.TryGetValue(dialogId, out dialog);
    }

    public void RegisterFunction(string name, Func<IReadOnlyList<object>, object> handler)
    {
        if (string.IsNullOrWhiteSpace(name) || handler == null)
        {
            return;
        }

        _functions[name] = handler;
    }

    public void RegisterDialogAsset(DialogAsset asset)
    {
        if (asset == null)
        {
            return;
        }

        foreach (var dialog in asset.Dialogs)
        {
            if (dialog == null || string.IsNullOrWhiteSpace(dialog.Id))
            {
                continue;
            }

            _dialogs[dialog.Id] = dialog;
        }
    }

    public bool TryGetService<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var stored))
        {
            service = stored as T;
            return service != null;
        }

        service = null;
        return false;
    }

    public void RegisterService<T>(T service) where T : class
    {
        if (service == null)
        {
            return;
        }

        _services[typeof(T)] = service;
    }
}
}
