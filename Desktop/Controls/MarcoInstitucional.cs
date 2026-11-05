using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Clarisa.Desktop.Controls;
public sealed class MarcoInstitucional : Decorator
{
    public static readonly DependencyProperty ContornoProperty = DependencyProperty.Register(nameof(Contorno), typeof(Brush), typeof(MarcoInstitucional), new FrameworkPropertyMetadata(Brushes.DarkGreen, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CorteProperty = DependencyProperty.Register(nameof(Corte), typeof(double), typeof(MarcoInstitucional), new FrameworkPropertyMetadata(26d, FrameworkPropertyMetadataOptions.AffectsRender), valor => valor is double numero && double.IsFinite(numero) && numero >= 0);
    public static readonly DependencyProperty RellenoProperty = DependencyProperty.Register(nameof(Relleno), typeof(Brush), typeof(MarcoInstitucional), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MargenInteriorProperty = DependencyProperty.Register(nameof(MargenInterior), typeof(Thickness), typeof(MarcoInstitucional), new FrameworkPropertyMetadata(new Thickness(40), FrameworkPropertyMetadataOptions.AffectsMeasure));
    public Brush Contorno { get => (Brush)GetValue(ContornoProperty); set => SetValue(ContornoProperty, value); }
    public Brush Relleno { get => (Brush)GetValue(RellenoProperty); set => SetValue(RellenoProperty, value); }
    public double Corte { get => (double)GetValue(CorteProperty); set => SetValue(CorteProperty, value); }
    public Thickness MargenInterior { get => (Thickness)GetValue(MargenInteriorProperty); set => SetValue(MargenInteriorProperty, value); }

    protected override Size MeasureOverride(Size disponible)
    {
        var margen = MargenInterior;
        Child?.Measure(new Size(Math.Max(0, disponible.Width - margen.Left - margen.Right), Math.Max(0, disponible.Height - margen.Top - margen.Bottom)));
        var deseado = Child?.DesiredSize ?? new Size();
        return new Size(deseado.Width + margen.Left + margen.Right, deseado.Height + margen.Top + margen.Bottom);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var margen = MargenInterior;
        Child?.Arrange(new Rect(margen.Left, margen.Top, Math.Max(0, final.Width - margen.Left - margen.Right), Math.Max(0, final.Height - margen.Top - margen.Bottom)));
        return final;
    }

    protected override void OnRender(DrawingContext dibujo)
    {
        var ancho = ActualWidth - 1;
        var alto = ActualHeight - 1;
        if (ancho <= 0 || alto <= 0)
            return;
        var corte = Math.Min(Corte, Math.Min(ancho, alto) / 3);
        var geometria = new StreamGeometry();
        using (var trazo = geometria.Open())
        {
            trazo.BeginFigure(new Point(corte, .5), true, true);
            trazo.PolyLineTo(new Point[] { new(ancho - corte, .5), new(ancho, corte), new(ancho, alto - corte), new(ancho - corte, alto), new(corte, alto), new(.5, alto - corte), new(.5, corte) }, true, false);
        }

        geometria.Freeze();
        dibujo.DrawGeometry(Relleno, new Pen(Contorno, 1), geometria);
    }
}
