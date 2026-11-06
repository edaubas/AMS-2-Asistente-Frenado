using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AMS2Frenado.Core.Overlay;

namespace AMS2Frenado.App;

/// <summary>
/// Tarjeta oscura con textura de carbono y esquina cortada a 45 grados abajo a la derecha (UI-SPEC 7.2). Dibuja una sola
/// geometria rellena (no usa Clip ni efectos de mapa de bits): primero el color base y encima la textura compartida y
/// congelada. El alfa va SOLO en los pinceles de fondo: el contenido (texto) queda al 100 %. Compila, SIN EJECUTAR.
/// </summary>
internal sealed class PanelCarbono : Decorator
{
    public static readonly DependencyProperty AlfaProperty = DependencyProperty.Register(nameof(Alfa), typeof(double), typeof(PanelCarbono),
        new FrameworkPropertyMetadata(MedidasUi.AlfaPorDefecto, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CorteProperty = DependencyProperty.Register(nameof(Corte), typeof(double), typeof(PanelCarbono),
        new FrameworkPropertyMetadata(MedidasUi.CorteEsquina, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ColorBaseProperty = DependencyProperty.Register(nameof(ColorBase), typeof(string), typeof(PanelCarbono),
        new FrameworkPropertyMetadata(Paleta.Fondo, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ConTexturaProperty = DependencyProperty.Register(nameof(ConTextura), typeof(bool), typeof(PanelCarbono),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty RellenoProperty = DependencyProperty.Register(nameof(Relleno), typeof(Thickness), typeof(PanelCarbono),
        new FrameworkPropertyMetadata(new Thickness(0), FrameworkPropertyMetadataOptions.AffectsMeasure));
    public static readonly DependencyProperty BordeProperty = DependencyProperty.Register(nameof(Borde), typeof(string), typeof(PanelCarbono),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Alfa del fondo (0..1). 1 = opaco (menu, ventana desbloqueada).</summary>
    public double Alfa { get => (double)GetValue(AlfaProperty); set => SetValue(AlfaProperty, value); }
    public double Corte { get => (double)GetValue(CorteProperty); set => SetValue(CorteProperty, value); }
    public string ColorBase { get => (string)GetValue(ColorBaseProperty); set => SetValue(ColorBaseProperty, value); }
    public bool ConTextura { get => (bool)GetValue(ConTexturaProperty); set => SetValue(ConTexturaProperty, value); }
    public Thickness Relleno { get => (Thickness)GetValue(RellenoProperty); set => SetValue(RellenoProperty, value); }
    /// <summary>Color "#RRGGBB" del borde de 1 px, o null.</summary>
    public string? Borde { get => (string?)GetValue(BordeProperty); set => SetValue(BordeProperty, value); }

    protected override Size MeasureOverride(Size limite)
    {
        var r = Relleno;
        double ph = r.Left + r.Right, pv = r.Top + r.Bottom;
        if (Child is null) return new Size(ph, pv);
        Child.Measure(new Size(Math.Max(0, limite.Width - ph), Math.Max(0, limite.Height - pv)));
        return new Size(Child.DesiredSize.Width + ph, Child.DesiredSize.Height + pv);
    }

    protected override Size ArrangeOverride(Size final)
    {
        var r = Relleno;
        Child?.Arrange(new Rect(r.Left, r.Top, Math.Max(0, final.Width - r.Left - r.Right), Math.Max(0, final.Height - r.Top - r.Bottom)));
        return final;
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var geo = Estilo.EsquinaCortada(ActualWidth, ActualHeight, Corte);
        double a = Math.Clamp(Alfa, 0, 1);
        dc.DrawGeometry(Pincel.De(ColorBase, a), Borde is null ? null : new Pen(Pincel.De(Borde), 1), geo);
        if (ConTextura)
        {
            // la capa de textura lleva el mismo alfa que el fondo: el panel entero se aclara/oscurece junto, el texto no
            dc.PushOpacity(a);
            dc.DrawGeometry(Estilo.Carbono, null, geo);
            dc.Pop();
        }
    }
}
