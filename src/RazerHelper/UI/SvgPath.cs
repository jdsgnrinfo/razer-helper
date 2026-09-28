using System.Drawing.Drawing2D;
using System.Globalization;

namespace RazerHelper.UI;

/// <summary>
/// Turns the path data of an SVG icon (the d attribute) into a GraphicsPath,
/// so icons taken from an icon set can be drawn as vectors like the rest.
/// Handles every path command, absolute and relative; arcs become Bézier curves.
/// </summary>
internal static class SvgPath
{
    public static GraphicsPath Parse(string data)
    {
        // SVG's default fill rule is nonzero, which GDI+ calls Winding.
        var path = new GraphicsPath(FillMode.Winding);
        var reader = new Reader(data);

        var current = PointF.Empty;
        var start = PointF.Empty;
        var lastControl = PointF.Empty;
        var command = ' ';
        var previous = ' ';

        while (reader.HasMore)
        {
            if (reader.TryReadCommand(out var next))
                command = next;
            else if (command is 'M')
                command = 'L'; // Coordinates after a move are lines.
            else if (command is 'm')
                command = 'l';

            var relative = char.IsLower(command);
            var origin = relative ? current : PointF.Empty;
            PointF Point() => new(origin.X + reader.Number(), origin.Y + reader.Number());

            switch (char.ToUpperInvariant(command))
            {
                case 'M':
                    path.StartFigure();
                    current = start = Point();
                    break;

                case 'L':
                    current = Line(path, current, Point());
                    break;

                case 'H':
                    current = Line(path, current, new PointF((relative ? current.X : 0) + reader.Number(), current.Y));
                    break;

                case 'V':
                    current = Line(path, current, new PointF(current.X, (relative ? current.Y : 0) + reader.Number()));
                    break;

                case 'C':
                {
                    var first = Point();
                    lastControl = Point();
                    var end = Point();
                    path.AddBezier(current, first, lastControl, end);
                    current = end;
                    break;
                }

                case 'S':
                {
                    var first = char.ToUpperInvariant(previous) is 'C' or 'S' ? Reflect(lastControl, current) : current;
                    lastControl = Point();
                    var end = Point();
                    path.AddBezier(current, first, lastControl, end);
                    current = end;
                    break;
                }

                case 'Q':
                {
                    lastControl = Point();
                    var end = Point();
                    AddQuadratic(path, current, lastControl, end);
                    current = end;
                    break;
                }

                case 'T':
                {
                    lastControl = char.ToUpperInvariant(previous) is 'Q' or 'T' ? Reflect(lastControl, current) : current;
                    var end = Point();
                    AddQuadratic(path, current, lastControl, end);
                    current = end;
                    break;
                }

                case 'A':
                {
                    var radiusX = reader.Number();
                    var radiusY = reader.Number();
                    var rotation = reader.Number();
                    var largeArc = reader.Flag();
                    var sweep = reader.Flag();
                    var end = Point();
                    AddArc(path, current, end, radiusX, radiusY, rotation, largeArc, sweep);
                    current = end;
                    break;
                }

                case 'Z':
                    path.CloseFigure();
                    current = start;
                    break;

                default:
                    throw new FormatException($"Unknown SVG path command '{command}'.");
            }

            previous = command;
        }

        return path;
    }

    private static PointF Line(GraphicsPath path, PointF from, PointF to)
    {
        path.AddLine(from, to);
        return to;
    }

    private static PointF Reflect(PointF control, PointF about) =>
        new(2 * about.X - control.X, 2 * about.Y - control.Y);

    private static void AddQuadratic(GraphicsPath path, PointF from, PointF control, PointF to) =>
        path.AddBezier(
            from,
            new PointF(from.X + 2f / 3 * (control.X - from.X), from.Y + 2f / 3 * (control.Y - from.Y)),
            new PointF(to.X + 2f / 3 * (control.X - to.X), to.Y + 2f / 3 * (control.Y - to.Y)),
            to);

    // The SVG arc from its endpoints (SVG 1.1, appendix F.6), drawn as
    // Bézier curves of at most a quarter turn each.
    private static void AddArc(GraphicsPath path, PointF from, PointF to, double rx, double ry, double rotationDegrees, bool largeArc, bool sweep)
    {
        if (from == to)
            return;

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);

        if (rx == 0 || ry == 0)
        {
            path.AddLine(from, to);
            return;
        }

        var phi = rotationDegrees * Math.PI / 180;
        var (sinPhi, cosPhi) = Math.SinCos(phi);

        var dx = (from.X - to.X) / 2.0;
        var dy = (from.Y - to.Y) / 2.0;
        var x1 = cosPhi * dx + sinPhi * dy;
        var y1 = -sinPhi * dx + cosPhi * dy;

        // Radii too small to reach are scaled up just enough.
        var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);

        if (lambda > 1)
        {
            rx *= Math.Sqrt(lambda);
            ry *= Math.Sqrt(lambda);
        }

        var numerator = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
        var denominator = rx * rx * y1 * y1 + ry * ry * x1 * x1;
        var factor = Math.Sqrt(Math.Max(0, numerator / denominator)) * (largeArc == sweep ? -1 : 1);

        var cx1 = factor * rx * y1 / ry;
        var cy1 = -factor * ry * x1 / rx;
        var cx = cosPhi * cx1 - sinPhi * cy1 + (from.X + to.X) / 2.0;
        var cy = sinPhi * cx1 + cosPhi * cy1 + (from.Y + to.Y) / 2.0;

        var startAngle = Math.Atan2((y1 - cy1) / ry, (x1 - cx1) / rx);
        var endAngle = Math.Atan2((-y1 - cy1) / ry, (-x1 - cx1) / rx);
        var delta = endAngle - startAngle;

        if (sweep && delta < 0)
            delta += 2 * Math.PI;
        else if (!sweep && delta > 0)
            delta -= 2 * Math.PI;

        var segments = (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2));
        var step = delta / segments;
        var handle = 4.0 / 3 * Math.Tan(step / 4);

        PointF At(double angle, double along)
        {
            var (sin, cos) = Math.SinCos(angle);
            var x = rx * (cos - along * sin);
            var y = ry * (sin + along * cos);
            return new PointF((float)(cosPhi * x - sinPhi * y + cx), (float)(sinPhi * x + cosPhi * y + cy));
        }

        var angle0 = startAngle;
        var point0 = from;

        for (var segment = 0; segment < segments; segment++)
        {
            var angle1 = angle0 + step;
            var point1 = segment == segments - 1 ? to : At(angle1, 0);
            path.AddBezier(point0, At(angle0, handle), At(angle1, -handle), point1);
            angle0 = angle1;
            point0 = point1;
        }
    }

    private sealed class Reader(string data)
    {
        private int _index;

        public bool HasMore
        {
            get
            {
                SkipSeparators();
                return _index < data.Length;
            }
        }

        public bool TryReadCommand(out char command)
        {
            SkipSeparators();
            command = data[_index];

            if (!char.IsLetter(command) || command is 'e' or 'E')
                return false;

            _index++;
            return true;
        }

        // An arc flag is a single 0 or 1, which may run straight into the next number.
        public bool Flag()
        {
            SkipSeparators();
            return data[_index++] == '1';
        }

        public float Number()
        {
            SkipSeparators();
            var begin = _index;

            if (_index < data.Length && data[_index] is '-' or '+')
                _index++;

            var seenDot = false;

            while (_index < data.Length)
            {
                var character = data[_index];

                if (char.IsDigit(character))
                {
                    _index++;
                }
                else if (character == '.' && !seenDot)
                {
                    seenDot = true;
                    _index++;
                }
                else if (character is 'e' or 'E')
                {
                    _index++;

                    if (_index < data.Length && data[_index] is '-' or '+')
                        _index++;
                }
                else
                {
                    break;
                }
            }

            return float.Parse(data.AsSpan(begin, _index - begin), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private void SkipSeparators()
        {
            while (_index < data.Length && (char.IsWhiteSpace(data[_index]) || data[_index] == ','))
                _index++;
        }
    }
}
