using System.Globalization;
using System.Text;

bool interactiveMode = args.Length == 0;

if (!interactiveMode)
{
    string input = string.Join(" ", args);
    ProcessLine(input);
}
else
{
    Console.WriteLine("Консольный калькулятор запущен. Примеры:");
    Console.WriteLine("  -45+120*(5/3**2^0.5)*sin100/(3&1)-5%2");
    Console.WriteLine("  $16 20          -> перевод 20 в 16-ричную СС");
    Console.WriteLine("  $2 0xFF & 0b101 -> вычисление и вывод в 2-ичной СС");
    Console.WriteLine("Введите 'exit' или 'quit' для выхода.\n");

    ReadLine.HistoryEnabled = true;
    while (true)
    {
        string? line = ReadLine.Read("> ");
        if (line == null) break;

        string trimmed = line.Trim();
        if (trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(trimmed)) continue;

        ProcessLine(trimmed);
    }
}

void ProcessLine(string line)
{
    try
    {
        int targetBase = 10;
        string expr = line.Trim();

        // Проверка на команду перевода в систему счисления: $<base> <expression>
        if (expr.StartsWith('$'))
        {
            int spaceIdx = expr.IndexOf(' ');
            if (spaceIdx == -1)
                throw new ArgumentException("После $<основание> через пробел должно идти число или выражение (например: $16 20).");

            string baseStr = expr.Substring(1, spaceIdx - 1);
            if (!int.TryParse(baseStr, out targetBase) || targetBase < 2 || targetBase > 36)
                throw new ArgumentException("Основание системы счисления должно быть от 2 до 36.");

            expr = expr.Substring(spaceIdx + 1).Trim();
        }

        double result = calculate(expr);

        if (targetBase != 10)
        {
            string converted = ConvertToBase(result, targetBase);
            Console.WriteLine($"= {converted} (осн. {targetBase})");
        }
        else
        {
            Console.WriteLine($"= {result.ToString(CultureInfo.InvariantCulture)}");
        }
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Ошибка: {ex.Message}");
        Console.ResetColor();
    }
}

double calculate(string line)
{
    var parser = new ExpressionParser(line);
    return parser.Parse();
}

// Перевод дробных и целых чисел в любую систему счисления (2..36)
string ConvertToBase(double value, int toBase)
{
    if (double.IsNaN(value) || double.IsInfinity(value))
        return value.ToString(CultureInfo.InvariantCulture);

    const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    bool isNegative = value < 0;
    value = Math.Abs(value);

    long integerPart = (long)Math.Truncate(value);
    double fractionalPart = value - integerPart;

    StringBuilder sb = new StringBuilder();
    if (integerPart == 0)
    {
        sb.Append('0');
    }
    else
    {
        while (integerPart > 0)
        {
            sb.Insert(0, chars[(int)(integerPart % toBase)]);
            integerPart /= toBase;
        }
    }

    if (isNegative) sb.Insert(0, '-');

    // Если есть дробная часть, переводим и её (до 8 знаков после запятой)
    if (fractionalPart > 1e-9)
    {
        sb.Append('.');
        int precision = 8;
        while (fractionalPart > 1e-9 && precision > 0)
        {
            fractionalPart *= toBase;
            int digit = (int)Math.Truncate(fractionalPart);
            sb.Append(chars[digit]);
            fractionalPart -= digit;
            precision--;
        }
    }

    return sb.ToString();
}

// Парсер математических выражений методом рекурсивного спуска
class ExpressionParser
{
    private readonly string _text;
    private int _pos;

    public ExpressionParser(string text)
    {
        _text = text;
        _pos = 0;
    }
public double Parse()
    {
        double result = ParseBitwiseOr();
        SkipWhitespace();
        if (_pos < _text.Length)
            throw new FormatException($"Неожиданный символ '{_text[_pos]}' на позиции {_pos}");
        return result;
    }

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            _pos++;
    }

    private bool Match(string token)
    {
        SkipWhitespace();
        if (_text.AsSpan(_pos).StartsWith(token, StringComparison.OrdinalIgnoreCase))
        {
            // Защита от срабатывания текстовых операторов (например, "xor") внутри имён функций
            if (char.IsLetter(token[0]) && _pos + token.Length < _text.Length && char.IsLetterOrDigit(_text[_pos + token.Length]))
                return false;

            _pos += token.Length;
            return true;
        }
        return false;
    }

    private double ParseBitwiseOr()
    {
        double left = ParseBitwiseXor();
        while (Match("|"))
        {
            long r = (long)ParseBitwiseXor();
            left = (long)left | r;
        }
        return left;
    }

    private double ParseBitwiseXor()
    {
        double left = ParseBitwiseAnd();
        while (Match("xor"))
        {
            long r = (long)ParseBitwiseAnd();
            left = (long)left ^ r;
        }
        return left;
    }

    private double ParseBitwiseAnd()
    {
        double left = ParseShift();
        while (Match("&"))
        {
            long r = (long)ParseShift();
            left = (long)left & r;
        }
        return left;
    }

    private double ParseShift()
    {
        double left = ParseAddSub();
        while (true)
        {
            if (Match("<<")) left = (long)left << (int)ParseAddSub();
            else if (Match(">>")) left = (long)left >> (int)ParseAddSub();
            else break;
        }
        return left;
    }

    private double ParseAddSub()
    {
        double left = ParseMulDivMod();
        while (true)
        {
            if (Match("+")) left += ParseMulDivMod();
            else if (Match("-")) left -= ParseMulDivMod();
            else break;
        }
        return left;
    }

    private double ParseMulDivMod()
    {
        double left = ParsePower();
        while (true)
        {
            SkipWhitespace();
            if (_pos + 1 < _text.Length && _text[_pos] == '*' && _text[_pos + 1] == '*')
                break;

            if (Match("*")) left *= ParsePower();
            else if (Match("/"))
            {
                double right = ParsePower();
                if (right == 0) throw new DivideByZeroException("Деление на ноль.");
                left /= right;
            }
            else if (Match("%")) left %= ParsePower();
            else break;
        }
        return left;
    }

    private double ParsePower()
    {
        double left = ParseUnary();
        SkipWhitespace();
        if (Match("**") || Match("^"))
        {
            double right = ParsePower();
            return Math.Pow(left, right);
        }
        return left;
    }

    private double ParseUnary()
    {
        if (Match("+")) return ParseUnary();
        if (Match("-")) return -ParseUnary();
        if (Match("~")) return ~(long)ParseUnary();
        return ParsePostfix();
    }
    private double ParsePostfix()
    {
        double val = ParsePrimary();
        while (Match("!"))
        {
            val = Factorial(val);
        }
        return val;
    }

    private double ParsePrimary()
    {
        SkipWhitespace();
        if (_pos >= _text.Length)
            throw new FormatException("Неожиданный конец выражения.");

        // Выражение в скобках
        if (Match("("))
        {
            double val = ParseBitwiseOr();
            if (!Match(")"))
                throw new FormatException("Пропущена закрывающая скобка ')'.");
            return val;
        }

        // Функции и константы
        if (char.IsLetter(_text[_pos]))
        {
            int start = _pos;
            while (_pos < _text.Length && char.IsLetter(_text[_pos]))
                _pos++;

            string name = _text.Substring(start, _pos - start).ToLowerInvariant();

            // Константы
            if (name == "pi") return Math.PI;
            if (name == "e") return Math.E;
            if (name == "tau") return Math.Tau;

            List<double> args = new List<double>();
            SkipWhitespace();
            if (Match("("))
            {
                if (!Match(")"))
                {
                    do
                    {
                        args.Add(ParseBitwiseOr());
                    } while (Match(","));

                    if (!Match(")"))
                        throw new FormatException($"Пропущена закрывающая скобка для функции '{name}'.");
                }
            }
            else
            {
                args.Add(ParseUnary());
            }

            return ApplyFunction(name, args);
        }

        return ParseNumber();
    }

    private double ParseNumber()
    {
        SkipWhitespace();
        int start = _pos;

        if (_pos + 2 <= _text.Length && _text[_pos] == '0')
        {
            char type = char.ToLowerInvariant(_text[_pos + 1]);
            if (type == 'x' || type == 'b' || type == 'o')
            {
                int numBase = type == 'x' ? 16 : (type == 'b' ? 2 : 8);
                _pos += 2;
                int numStart = _pos;
                while (_pos < _text.Length && char.IsLetterOrDigit(_text[_pos]))
                    _pos++;

                string digits = _text.Substring(numStart, _pos - numStart);
                if (string.IsNullOrEmpty(digits))
                    throw new FormatException($"Ожидались цифры после префикса 0{type}.");

                return Convert.ToInt64(digits, numBase);
            }
        }

        while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || _text[_pos] == '.' || _text[_pos] == ','))
            _pos++;

        if (_pos < _text.Length && (_text[_pos] == 'e' || _text[_pos] == 'E'))
        {
            _pos++;
            if (_pos < _text.Length && (_text[_pos] == '+' || _text[_pos] == '-'))
                _pos++;
            while (_pos < _text.Length && char.IsDigit(_text[_pos]))
                _pos++;
        }

        if (start == _pos)
            throw new FormatException($"Ожидалось число или выражение на позиции {_pos} ('{_text[_pos]}').");

        string numStr = _text.Substring(start, _pos - start).Replace(',', '.');
        if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            throw new FormatException($"Некорректный формат числа: '{numStr}'.");

        return result;
    }
private double ApplyFunction(string name, List<double> args)
    {
        if (args.Count == 0)
            throw new ArgumentException($"Функция '{name}' требует хотя бы один аргумент.");

        double a = args[0];
        return name switch
        {
            "sin"   => Math.Sin(a),
            "cos"   => Math.Cos(a),
            "tan" or "tg" => Math.Tan(a),
            "cot" or "ctg" => 1.0 / Math.Tan(a),
            "asin"  => Math.Asin(a),
            "acos"  => Math.Acos(a),
            "atan" or "atg" => Math.Atan(a),
            "sinh"  => Math.Sinh(a),
            "cosh"  => Math.Cosh(a),
            "tanh"  => Math.Tanh(a),
            "sqrt"  => Math.Sqrt(a),
            "cbrt"  => Math.Cbrt(a),
            "ln"    => Math.Log(a),
            "log"   => args.Count > 1 ? Math.Log(a, args[1]) : Math.Log10(a),
            "exp"   => Math.Exp(a),
            "abs"   => Math.Abs(a),
            "floor" => Math.Floor(a),
            "ceil"  => Math.Ceiling(a),
            "round" => Math.Round(a),
            "sign"  => Math.Sign(a),
            "rad"   => a * Math.PI / 180.0, // перевод градусов в радианы: sin(rad(30))
            "deg"   => a * 180.0 / Math.PI, // перевод радиан в градусы
            "pow"   => args.Count > 1 ? Math.Pow(a, args[1]) : throw new ArgumentException("Функция pow(x, y) требует 2 аргумента."),
            "min"   => args.Count > 1 ? Math.Min(a, args[1]) : a,
            "max"   => args.Count > 1 ? Math.Max(a, args[1]) : a,
            _ => throw new ArgumentException($"Неизвестная функция '{name}'.")
        };
    }

    private double Factorial(double n)
    {
        if (n < 0 || n != Math.Truncate(n))
            throw new ArgumentException("Факториал определён только для целых неотрицательных чисел.");
        if (n > 170)
            return double.PositiveInfinity;

        double res = 1;
        for (int i = 2; i <= (int)n; i++)
            res *= i;
        return res;
    }
}