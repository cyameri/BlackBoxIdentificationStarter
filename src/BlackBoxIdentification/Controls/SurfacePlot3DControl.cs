using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.IO;
using System.Windows.Forms;

namespace BlackBoxIdentification.Controls;

public sealed class SurfacePlot3DControl : Control
{
    private double[,]? _values;
    private double _minimum;
    private double _maximum;
    private double _yaw = -0.75;
    private double _pitch = 0.62;
    private double _zoom = 0.88;
    private bool _dragging;
    private Point _lastMouse;

    public SurfacePlot3DControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        ForeColor = Color.Black;
        TabStop = true;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(
            "Сохранить изображение...",
            null,
            (_, _) => SaveImageWithDialog());
        contextMenu.Items.Add(
            "Копировать изображение",
            null,
            (_, _) => CopyImageToClipboard());
        ContextMenuStrip = contextMenu;
    }

    public string Title { get; set; } = "Идентифицированное ядро второго порядка K2(s1, s2)";

    /// <summary>
    /// Создаёт снимок трёхмерного графика в его текущем ракурсе и масштабе.
    /// </summary>
    public Bitmap CreateImage()
    {
        int width = Math.Max(1, ClientSize.Width);
        int height = Math.Max(1, ClientSize.Height);
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        // SurfacePlot3DControl рисуется средствами GDI+, поэтому DrawToBitmap
        // сохраняет ровно тот ракурс, который пользователь видит на экране.
        DrawToBitmap(bitmap, new Rectangle(0, 0, width, height));
        return bitmap;
    }

    public void SaveImageWithDialog()
    {
        if (_values is null)
        {
            MessageBox.Show(
                FindForm(),
                "Сначала выполните идентификацию, чтобы построить поверхность K2(s1, s2).",
                "Нет данных для сохранения",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить трёхмерный график",
            Filter =
                "PNG image (*.png)|*.png|" +
                "JPEG image (*.jpg;*.jpeg)|*.jpg;*.jpeg|" +
                "Bitmap image (*.bmp)|*.bmp",
            DefaultExt = "png",
            AddExtension = true,
            FileName = "K2_surface.png"
        };

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        try
        {
            using Bitmap bitmap = CreateImage();
            bitmap.Save(dialog.FileName, ImageFormatForPath(dialog.FileName));
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                FindForm(),
                $"Не удалось сохранить изображение.\n\n{exception.Message}",
                "Ошибка сохранения",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    public void CopyImageToClipboard()
    {
        if (_values is null)
        {
            MessageBox.Show(
                FindForm(),
                "Сначала выполните идентификацию, чтобы построить поверхность K2(s1, s2).",
                "Нет данных для копирования",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        try
        {
            using Bitmap bitmap = CreateImage();
            using Image clipboardImage = (Image)bitmap.Clone();
            Clipboard.SetImage(clipboardImage);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                FindForm(),
                $"Не удалось скопировать изображение.\n\n{exception.Message}",
                "Ошибка копирования",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static ImageFormat ImageFormatForPath(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp,
            _ => ImageFormat.Png
        };
    }

    public void ClearSurface()
    {
        _values = null;
        Invalidate();
    }

    public void SetSurface(double[,] values)
    {
        if (values.GetLength(0) < 2 || values.GetLength(1) < 2)
            throw new ArgumentException("Поверхность должна содержать не менее 2x2 точек.", nameof(values));

        _values = (double[,])values.Clone();
        _minimum = double.PositiveInfinity;
        _maximum = double.NegativeInfinity;

        foreach (double value in _values)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                continue;

            if (value < _minimum)
                _minimum = value;

            if (value > _maximum)
                _maximum = value;
        }

        if (double.IsInfinity(_minimum) || double.IsInfinity(_maximum))
        {
            _minimum = -1.0;
            _maximum = 1.0;
        }

        if (Math.Abs(_maximum - _minimum) < 1e-14)
        {
            _minimum -= 0.5;
            _maximum += 0.5;
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(BackColor);

        using var titleFont = new Font(Font.FontFamily, Math.Max(10.0f, Font.Size + 1.0f), FontStyle.Bold);
        using var textFont = new Font(Font.FontFamily, Math.Max(8.0f, Font.Size - 0.5f), FontStyle.Regular);
        using var axisPen = new Pen(Color.FromArgb(210, 30, 30, 30), 1.4f);
        using var meshPen = new Pen(Color.FromArgb(95, 25, 25, 25), 0.7f);

        SizeF titleSize = graphics.MeasureString(Title, titleFont);
        graphics.DrawString(Title, titleFont, Brushes.Black, Math.Max(5.0f, (Width - titleSize.Width) / 2.0f), 8.0f);

        if (_values is null)
        {
            const string message = "Выполните идентификацию для построения поверхности K2(s1, s2).";
            SizeF messageSize = graphics.MeasureString(message, textFont);
            graphics.DrawString(
                message,
                textFont,
                Brushes.DimGray,
                Math.Max(5.0f, (Width - messageSize.Width) / 2.0f),
                Math.Max(35.0f, (Height - messageSize.Height) / 2.0f));
            return;
        }

        int rows = _values.GetLength(0);
        int columns = _values.GetLength(1);
        var projected = new ProjectedPoint[rows, columns];

        for (int row = 0; row < rows; row++)
        {
            double s1 = row / (double)(rows - 1);

            for (int column = 0; column < columns; column++)
            {
                double s2 = column / (double)(columns - 1);
                projected[row, column] = Project(s1, s2, _values[row, column]);
            }
        }

        var cells = new List<SurfaceCell>((rows - 1) * (columns - 1));
        for (int row = 0; row < rows - 1; row++)
        {
            for (int column = 0; column < columns - 1; column++)
            {
                ProjectedPoint p00 = projected[row, column];
                ProjectedPoint p10 = projected[row + 1, column];
                ProjectedPoint p11 = projected[row + 1, column + 1];
                ProjectedPoint p01 = projected[row, column + 1];

                double averageValue = (
                    _values[row, column] +
                    _values[row + 1, column] +
                    _values[row + 1, column + 1] +
                    _values[row, column + 1]) / 4.0;

                float depth = (p00.Depth + p10.Depth + p11.Depth + p01.Depth) / 4.0f;
                cells.Add(new SurfaceCell(
                    new[] { p00.Point, p10.Point, p11.Point, p01.Point },
                    depth,
                    averageValue));
            }
        }

        foreach (SurfaceCell cell in cells.OrderBy(cell => cell.Depth))
        {
            using var brush = new SolidBrush(ColorForValue(cell.Value));
            graphics.FillPolygon(brush, cell.Points);
            graphics.DrawPolygon(meshPen, cell.Points);
        }

        DrawAxes(graphics, axisPen, textFont);

        const string hint = "Левая кнопка — вращение, колесо — масштаб, правая кнопка — сохранение";
        SizeF hintSize = graphics.MeasureString(hint, textFont);
        graphics.DrawString(
            hint,
            textFont,
            Brushes.DimGray,
            Math.Max(5.0f, Width - hintSize.Width - 8.0f),
            Math.Max(5.0f, Height - hintSize.Height - 6.0f));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
            return;

        _dragging = true;
        _lastMouse = e.Location;
        Capture = true;
        Focus();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_dragging)
            return;

        int deltaX = e.X - _lastMouse.X;
        int deltaY = e.Y - _lastMouse.Y;
        _lastMouse = e.Location;

        _yaw += deltaX * 0.012;
        _pitch = Math.Clamp(_pitch + deltaY * 0.009, -1.25, 1.25);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
            return;

        _dragging = false;
        Capture = false;
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _zoom = Math.Clamp(_zoom * (e.Delta > 0 ? 1.10 : 0.91), 0.45, 1.80);
        Invalidate();
    }

    private ProjectedPoint Project(double s1, double s2, double value)
    {
        double x = s1 - 0.5;
        double y = s2 - 0.5;
        double normalizedZ = (value - _minimum) / (_maximum - _minimum) - 0.5;
        double z = normalizedZ * 0.82;

        double cosYaw = Math.Cos(_yaw);
        double sinYaw = Math.Sin(_yaw);
        double x1 = x * cosYaw - y * sinYaw;
        double y1 = x * sinYaw + y * cosYaw;

        double cosPitch = Math.Cos(_pitch);
        double sinPitch = Math.Sin(_pitch);
        double y2 = y1 * cosPitch - z * sinPitch;
        double depth = y1 * sinPitch + z * cosPitch;

        float scale = (float)(Math.Min(Width, Height) * 0.72 * _zoom);
        float centerX = Width * 0.50f;
        float centerY = Height * 0.55f;

        var point = new PointF(
            centerX + (float)x1 * scale,
            centerY - (float)y2 * scale);

        return new ProjectedPoint(point, (float)depth);
    }

    private void DrawAxes(Graphics graphics, Pen pen, Font font)
    {
        double baseValue = _minimum;
        ProjectedPoint origin = Project(0.0, 0.0, baseValue);
        ProjectedPoint axisS1 = Project(1.0, 0.0, baseValue);
        ProjectedPoint axisS2 = Project(0.0, 1.0, baseValue);
        ProjectedPoint axisZ = Project(0.0, 0.0, _maximum);

        graphics.DrawLine(pen, origin.Point, axisS1.Point);
        graphics.DrawLine(pen, origin.Point, axisS2.Point);
        graphics.DrawLine(pen, origin.Point, axisZ.Point);

        graphics.DrawString("s1", font, Brushes.Black, axisS1.Point.X + 3.0f, axisS1.Point.Y - 4.0f);
        graphics.DrawString("s2", font, Brushes.Black, axisS2.Point.X + 3.0f, axisS2.Point.Y - 4.0f);
        graphics.DrawString("K2", font, Brushes.Black, axisZ.Point.X + 3.0f, axisZ.Point.Y - 4.0f);

        string minimumText = _minimum.ToString("G4");
        string maximumText = _maximum.ToString("G4");
        graphics.DrawString(minimumText, font, Brushes.DimGray, origin.Point.X + 3.0f, origin.Point.Y + 3.0f);
        graphics.DrawString(maximumText, font, Brushes.DimGray, axisZ.Point.X + 3.0f, axisZ.Point.Y + 12.0f);
    }

    private Color ColorForValue(double value)
    {
        double t = Math.Clamp((value - _minimum) / (_maximum - _minimum), 0.0, 1.0);

        double r;
        double g;
        double b;

        if (t < 0.25)
        {
            double u = t / 0.25;
            r = 35.0;
            g = 90.0 + 130.0 * u;
            b = 210.0 + 35.0 * u;
        }
        else if (t < 0.50)
        {
            double u = (t - 0.25) / 0.25;
            r = 35.0 + 80.0 * u;
            g = 220.0 + 25.0 * u;
            b = 245.0 - 145.0 * u;
        }
        else if (t < 0.75)
        {
            double u = (t - 0.50) / 0.25;
            r = 115.0 + 140.0 * u;
            g = 245.0 - 65.0 * u;
            b = 100.0 - 65.0 * u;
        }
        else
        {
            double u = (t - 0.75) / 0.25;
            r = 255.0;
            g = 180.0 - 130.0 * u;
            b = 35.0 - 20.0 * u;
        }

        return Color.FromArgb(220, (int)r, (int)g, (int)b);
    }

    private readonly record struct ProjectedPoint(PointF Point, float Depth);

    private sealed record SurfaceCell(PointF[] Points, float Depth, double Value);
}
