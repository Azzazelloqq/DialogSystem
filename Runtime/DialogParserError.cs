using System;
using UnityEngine;

namespace DialogSystem.Runtime
{
[Serializable]
public sealed class DialogParserError
{
    [SerializeField] private int _line;
    [SerializeField] private string _message;
    [SerializeField] private string _context;

    public int Line => _line;
    public string Message => _message;
    public string Context => _context;

    public DialogParserError(int line, string message, string context)
    {
        _line = line;
        _message = message;
        _context = context;
    }
}
}
