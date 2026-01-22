using System;
using System.Collections.Generic;
using System.Globalization;
using DialogSystem.Runtime;

namespace DialogSystem.Runtime.Expressions
{
public sealed class DialogExpression
{
    private readonly Expr _root;

    private DialogExpression(Expr root)
    {
        _root = root;
    }

    public static bool TryParse(string text, out DialogExpression expression, out string error)
    {
        expression = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Expression is empty.";
            return false;
        }

        var lexer = new Lexer(text);
        var parser = new Parser(lexer);
        if (!parser.TryParse(out var root, out error))
        {
            return false;
        }

        expression = new DialogExpression(root);
        return true;
    }

    public object Evaluate(IDialogContext context, out string error)
    {
        error = null;
        return _root.Evaluate(context, ref error);
    }

    public bool EvaluateBool(IDialogContext context, out string error)
    {
        var value = Evaluate(context, out error);
        return ValueUtils.ToBool(value);
    }

    private abstract class Expr
    {
        public abstract object Evaluate(IDialogContext context, ref string error);
    }

    private sealed class LiteralExpr : Expr
    {
        private readonly object _value;

        public LiteralExpr(object value)
        {
            _value = value;
        }

        public override object Evaluate(IDialogContext context, ref string error) => _value;
    }

    private sealed class VariableExpr : Expr
    {
        private readonly string _name;

        public VariableExpr(string name)
        {
            _name = name;
        }

        public override object Evaluate(IDialogContext context, ref string error)
        {
            if (context == null)
            {
                error = "Dialog context is null.";
                return null;
            }

            if (context.TryGetVariable(_name, out var value))
            {
                return value;
            }

            return null;
        }
    }

    private sealed class UnaryExpr : Expr
    {
        private readonly string _op;
        private readonly Expr _right;

        public UnaryExpr(string op, Expr right)
        {
            _op = op;
            _right = right;
        }

        public override object Evaluate(IDialogContext context, ref string error)
        {
            var right = _right.Evaluate(context, ref error);
            if (error != null)
            {
                return null;
            }

            switch (_op)
            {
                case "!":
                    return !ValueUtils.ToBool(right);
                case "-":
                    if (ValueUtils.TryToNumber(right, out var number))
                    {
                        return -number;
                    }

                    error = "Unary '-' expects a number.";
                    return null;
                default:
                    error = $"Unsupported unary operator '{_op}'.";
                    return null;
            }
        }
    }

    private sealed class BinaryExpr : Expr
    {
        private readonly string _op;
        private readonly Expr _left;
        private readonly Expr _right;

        public BinaryExpr(string op, Expr left, Expr right)
        {
            _op = op;
            _left = left;
            _right = right;
        }

        public override object Evaluate(IDialogContext context, ref string error)
        {
            if (_op == "&&" || _op == "||")
            {
                var leftBool = ValueUtils.ToBool(_left.Evaluate(context, ref error));
                if (error != null)
                {
                    return null;
                }

                if (_op == "&&")
                {
                    if (!leftBool)
                    {
                        return false;
                    }

                    return ValueUtils.ToBool(_right.Evaluate(context, ref error));
                }

                if (leftBool)
                {
                    return true;
                }

                return ValueUtils.ToBool(_right.Evaluate(context, ref error));
            }

            var leftValue = _left.Evaluate(context, ref error);
            if (error != null)
            {
                return null;
            }

            var rightValue = _right.Evaluate(context, ref error);
            if (error != null)
            {
                return null;
            }

            switch (_op)
            {
                case "==":
                    return ValueUtils.AreEqual(leftValue, rightValue);
                case "!=":
                    return !ValueUtils.AreEqual(leftValue, rightValue);
                case ">":
                case ">=":
                case "<":
                case "<=":
                    if (ValueUtils.TryCompare(leftValue, rightValue, out var compareResult))
                    {
                        return _op switch
                        {
                            ">" => compareResult > 0,
                            ">=" => compareResult >= 0,
                            "<" => compareResult < 0,
                            "<=" => compareResult <= 0,
                            _ => false
                        };
                    }

                    error = "Comparison requires numbers or strings.";
                    return null;
                case "+":
                    return ValueUtils.Add(leftValue, rightValue);
                case "-":
                    return ValueUtils.Arithmetic(leftValue, rightValue, (a, b) => a - b, out error);
                case "*":
                    return ValueUtils.Arithmetic(leftValue, rightValue, (a, b) => a * b, out error);
                case "/":
                    return ValueUtils.Arithmetic(leftValue, rightValue, (a, b) => a / b, out error);
                case "%":
                    return ValueUtils.Arithmetic(leftValue, rightValue, (a, b) => a % b, out error);
                default:
                    error = $"Unsupported operator '{_op}'.";
                    return null;
            }
        }
    }

    private sealed class CallExpr : Expr
    {
        private readonly string _name;
        private readonly List<Expr> _args;

        public CallExpr(string name, List<Expr> args)
        {
            _name = name;
            _args = args;
        }

        public override object Evaluate(IDialogContext context, ref string error)
        {
            if (context == null)
            {
                error = "Dialog context is null.";
                return null;
            }

            var values = new object[_args.Count];
            for (int i = 0; i < _args.Count; i++)
            {
                values[i] = _args[i].Evaluate(context, ref error);
                if (error != null)
                {
                    return null;
                }
            }

            if (!context.TryCallFunction(_name, values, out var result))
            {
                error = $"Unknown function '{_name}'.";
                return null;
            }

            return result;
        }
    }

    private sealed class Parser
    {
        private readonly Lexer _lexer;
        private Token _current;

        public Parser(Lexer lexer)
        {
            _lexer = lexer;
            _current = _lexer.NextToken();
        }

        public bool TryParse(out Expr expr, out string error)
        {
            expr = null;
            error = null;

            try
            {
                expr = ParseExpression();
                if (_current.Type != TokenType.End)
                {
                    error = $"Unexpected token '{_current.Text}'.";
                    return false;
                }

                return true;
            }
            catch (ParseException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private Expr ParseExpression() => ParseOr();

        private Expr ParseOr()
        {
            var expr = ParseAnd();
            while (MatchOperator("||"))
            {
                var right = ParseAnd();
                expr = new BinaryExpr("||", expr, right);
            }

            return expr;
        }

        private Expr ParseAnd()
        {
            var expr = ParseEquality();
            while (MatchOperator("&&"))
            {
                var right = ParseEquality();
                expr = new BinaryExpr("&&", expr, right);
            }

            return expr;
        }

        private Expr ParseEquality()
        {
            var expr = ParseComparison();
            while (MatchOperator("==") || MatchOperator("!="))
            {
                var op = _previous.Text;
                var right = ParseComparison();
                expr = new BinaryExpr(op, expr, right);
            }

            return expr;
        }

        private Expr ParseComparison()
        {
            var expr = ParseTerm();
            while (MatchOperator(">") || MatchOperator(">=") || MatchOperator("<") || MatchOperator("<="))
            {
                var op = _previous.Text;
                var right = ParseTerm();
                expr = new BinaryExpr(op, expr, right);
            }

            return expr;
        }

        private Expr ParseTerm()
        {
            var expr = ParseFactor();
            while (MatchOperator("+") || MatchOperator("-"))
            {
                var op = _previous.Text;
                var right = ParseFactor();
                expr = new BinaryExpr(op, expr, right);
            }

            return expr;
        }

        private Expr ParseFactor()
        {
            var expr = ParseUnary();
            while (MatchOperator("*") || MatchOperator("/") || MatchOperator("%"))
            {
                var op = _previous.Text;
                var right = ParseUnary();
                expr = new BinaryExpr(op, expr, right);
            }

            return expr;
        }

        private Expr ParseUnary()
        {
            if (MatchOperator("!") || MatchOperator("-"))
            {
                var op = _previous.Text;
                var right = ParseUnary();
                return new UnaryExpr(op, right);
            }

            return ParsePrimary();
        }

        private Expr ParsePrimary()
        {
            if (Match(TokenType.Number))
            {
                if (double.TryParse(_previous.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return new LiteralExpr(number);
                }

                throw new ParseException($"Invalid number '{_previous.Text}'.");
            }

            if (Match(TokenType.String))
            {
                return new LiteralExpr(_previous.Text);
            }

            if (Match(TokenType.True))
            {
                return new LiteralExpr(true);
            }

            if (Match(TokenType.False))
            {
                return new LiteralExpr(false);
            }

            if (Match(TokenType.Null))
            {
                return new LiteralExpr(null);
            }

            if (Match(TokenType.Identifier))
            {
                var identifier = _previous.Text;
                if (Match(TokenType.LeftParen))
                {
                    var args = new List<Expr>();
                    if (!Check(TokenType.RightParen))
                    {
                        do
                        {
                            args.Add(ParseExpression());
                        } while (Match(TokenType.Comma));
                    }

                    Consume(TokenType.RightParen, "Expected ')' after function arguments.");
                    return new CallExpr(identifier, args);
                }

                return new VariableExpr(identifier);
            }

            if (Match(TokenType.LeftParen))
            {
                var expr = ParseExpression();
                Consume(TokenType.RightParen, "Expected ')'.");
                return expr;
            }

            throw new ParseException($"Unexpected token '{_current.Text}'.");
        }

        private bool MatchOperator(string op)
        {
            if (_current.Type == TokenType.Operator && _current.Text == op)
            {
                Advance();
                return true;
            }

            return false;
        }

        private bool Match(TokenType type)
        {
            if (_current.Type == type)
            {
                Advance();
                return true;
            }

            return false;
        }

        private bool Check(TokenType type) => _current.Type == type;

        private void Consume(TokenType type, string message)
        {
            if (_current.Type == type)
            {
                Advance();
                return;
            }

            throw new ParseException(message);
        }

        private Token _previous;

        private void Advance()
        {
            _previous = _current;
            _current = _lexer.NextToken();
        }
    }

    private sealed class Lexer
    {
        private readonly string _text;
        private int _pos;

        public Lexer(string text)
        {
            _text = text ?? string.Empty;
        }

        public Token NextToken()
        {
            SkipWhitespace();

            if (_pos >= _text.Length)
            {
                return new Token(TokenType.End, string.Empty);
            }

            var c = _text[_pos];
            if (IsIdentifierStart(c))
            {
                return ReadIdentifier();
            }

            if (char.IsDigit(c))
            {
                return ReadNumber();
            }

            if (c == '"')
            {
                return ReadString();
            }

            _pos++;
            switch (c)
            {
                case '(':
                    return new Token(TokenType.LeftParen, "(");
                case ')':
                    return new Token(TokenType.RightParen, ")");
                case ',':
                    return new Token(TokenType.Comma, ",");
            }

            if (IsOperatorStart(c))
            {
                return ReadOperator(c);
            }

            return new Token(TokenType.Invalid, c.ToString());
        }

        private Token ReadIdentifier()
        {
            var start = _pos;
            _pos++;
            while (_pos < _text.Length && IsIdentifierPart(_text[_pos]))
            {
                _pos++;
            }

            var text = _text.Substring(start, _pos - start);
            return text switch
            {
                "true" => new Token(TokenType.True, text),
                "false" => new Token(TokenType.False, text),
                "null" => new Token(TokenType.Null, text),
                _ => new Token(TokenType.Identifier, text)
            };
        }

        private Token ReadNumber()
        {
            var start = _pos;
            _pos++;
            while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.'))
            {
                _pos++;
            }

            var text = _text.Substring(start, _pos - start);
            return new Token(TokenType.Number, text);
        }

        private Token ReadString()
        {
            _pos++;
            var buffer = new System.Text.StringBuilder();
            while (_pos < _text.Length)
            {
                var c = _text[_pos];
                if (c == '"')
                {
                    _pos++;
                    return new Token(TokenType.String, buffer.ToString());
                }

                if (c == '\\' && _pos + 1 < _text.Length)
                {
                    var next = _text[_pos + 1];
                    buffer.Append(next switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '"' => '"',
                        '\\' => '\\',
                        _ => next
                    });
                    _pos += 2;
                    continue;
                }

                buffer.Append(c);
                _pos++;
            }

            throw new ParseException("Unterminated string literal.");
        }

        private Token ReadOperator(char first)
        {
            if (_pos < _text.Length)
            {
                var next = _text[_pos];
                var combined = $"{first}{next}";
                if (combined == "&&" || combined == "||" || combined == "==" || combined == "!=" ||
                    combined == ">=" || combined == "<=")
                {
                    _pos++;
                    return new Token(TokenType.Operator, combined);
                }
            }

            return new Token(TokenType.Operator, first.ToString());
        }

        private void SkipWhitespace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
        }

        private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';
        private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '.';
        private static bool IsOperatorStart(char c) => "+-*/%!<>=&|".IndexOf(c) >= 0;
    }

    private readonly struct Token
    {
        public readonly TokenType Type;
        public readonly string Text;

        public Token(TokenType type, string text)
        {
            Type = type;
            Text = text;
        }
    }

    private enum TokenType
    {
        Identifier,
        Number,
        String,
        True,
        False,
        Null,
        Operator,
        LeftParen,
        RightParen,
        Comma,
        Invalid,
        End
    }

    private sealed class ParseException : Exception
    {
        public ParseException(string message) : base(message)
        {
        }
    }

    private static class ValueUtils
    {
        public static bool ToBool(object value)
        {
            if (value == null)
            {
                return false;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            if (TryToNumber(value, out var number))
            {
                return Math.Abs(number) > double.Epsilon;
            }

            if (value is string str)
            {
                return !string.IsNullOrEmpty(str);
            }

            return true;
        }

        public static bool TryToNumber(object value, out double number)
        {
            switch (value)
            {
                case null:
                    number = 0;
                    return false;
                case double d:
                    number = d;
                    return true;
                case float f:
                    number = f;
                    return true;
                case int i:
                    number = i;
                    return true;
                case long l:
                    number = l;
                    return true;
                case short s:
                    number = s;
                    return true;
                case byte b:
                    number = b;
                    return true;
                case string str when double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                    number = parsed;
                    return true;
                default:
                    number = 0;
                    return false;
            }
        }

        public static bool AreEqual(object left, object right)
        {
            if (TryToNumber(left, out var leftNumber) && TryToNumber(right, out var rightNumber))
            {
                return Math.Abs(leftNumber - rightNumber) <= double.Epsilon;
            }

            if (left == null || right == null)
            {
                return left == right;
            }

            return left.Equals(right);
        }

        public static bool TryCompare(object left, object right, out int result)
        {
            if (TryToNumber(left, out var leftNumber) && TryToNumber(right, out var rightNumber))
            {
                result = leftNumber.CompareTo(rightNumber);
                return true;
            }

            if (left is string leftString && right is string rightString)
            {
                result = string.Compare(leftString, rightString, StringComparison.Ordinal);
                return true;
            }

            result = 0;
            return false;
        }

        public static object Add(object left, object right)
        {
            if (left is string || right is string)
            {
                return $"{ToStringInvariant(left)}{ToStringInvariant(right)}";
            }

            if (TryToNumber(left, out var leftNumber) && TryToNumber(right, out var rightNumber))
            {
                return leftNumber + rightNumber;
            }

            return $"{ToStringInvariant(left)}{ToStringInvariant(right)}";
        }

        public static object Arithmetic(object left, object right, Func<double, double, double> op, out string error)
        {
            error = null;
            if (!TryToNumber(left, out var leftNumber) || !TryToNumber(right, out var rightNumber))
            {
                error = "Arithmetic operation expects numbers.";
                return null;
            }

            return op(leftNumber, rightNumber);
        }

        private static string ToStringInvariant(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            return value switch
            {
                double d => d.ToString(CultureInfo.InvariantCulture),
                float f => f.ToString(CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture)
            };
        }
    }
}
}
