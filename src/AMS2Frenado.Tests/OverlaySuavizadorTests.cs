using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

public sealed class OverlaySuavizadorTests
{
    private static VistaPanel Guia(EstadoPanel e, int curva = 1, string metros = "185 m", string numero = "200", string? franja = null)
        => new() { Tipo = TipoTarjeta.Guia, Estado = e, IndiceCurva = curva, CurvaTxt = $"CURVA {curva}", NumeroPrincipal = numero, Metros = metros, TextoFranja = franja };

    private static VistaPanel Lejos(int curva = 1) => new() { Tipo = TipoTarjeta.Lejos, Estado = EstadoPanel.Lejos, IndiceCurva = curva, CurvaTxt = $"CURVA {curva}" };

    [Fact]
    public void LaGuiaEntraConFundidoDe120Ms()
    {
        var s = new Suavizador();
        s.Actualizar(Lejos(), 0);
        var a = s.Actualizar(Guia(EstadoPanel.Aproximacion), 0.0);
        Assert.Equal(0, a.OpacidadTarjeta, 6);
        var b = s.Actualizar(Guia(EstadoPanel.Aproximacion), 0.060);
        Assert.Equal(0.5, b.OpacidadTarjeta, 6);
        var c = s.Actualizar(Guia(EstadoPanel.Aproximacion), 0.200);
        Assert.Equal(1, c.OpacidadTarjeta, 6);
    }

    [Fact]
    public void LaGuiaSaleConFundidoDe200MsMostrandoElUltimoContenido()
    {
        var s = new Suavizador();
        for (double t = 0; t < 0.5; t += 0.016) s.Actualizar(Guia(EstadoPanel.Frena, franja: "FRENÁ"), t);
        var a = s.Actualizar(Lejos(2), 0.5);
        Assert.Equal(TipoTarjeta.Guia, a.Panel.Tipo);
        var b = s.Actualizar(Lejos(2), 0.6);
        Assert.Equal(0.5, b.OpacidadTarjeta, 1);
        Assert.Equal(TipoTarjeta.Guia, b.Panel.Tipo);
        var c = s.Actualizar(Lejos(2), 0.75);
        Assert.Equal(TipoTarjeta.Lejos, c.Panel.Tipo);
        Assert.Equal(1, c.OpacidadTarjeta);
    }

    [Fact]
    public void CurvaSiguienteCambiaElContenidoAlInstanteSinFundidoDeSalida()
    {
        var s = new Suavizador();
        for (double t = 0; t < 0.5; t += 0.016) s.Actualizar(Guia(EstadoPanel.Frena, curva: 1, franja: "FRENÁ"), t);
        var x = s.Actualizar(Guia(EstadoPanel.Aproximacion, curva: 2, numero: "100"), 0.52);
        Assert.Equal("CURVA 2", x.Panel.CurvaTxt);
        Assert.Equal("100", x.Panel.NumeroPrincipal);
        Assert.Equal(1, x.OpacidadTarjeta, 6);
    }

    [Fact]
    public void LaFranjaApareceEn80MsYNoPulsa()
    {
        var s = new Suavizador();
        for (double t = 0; t < 0.3; t += 0.016) s.Actualizar(Guia(EstadoPanel.Inminente), t);
        s.Actualizar(Guia(EstadoPanel.Inminente), 0.3);
        var a = s.Actualizar(Guia(EstadoPanel.Frena, franja: "FRENÁ"), 0.3);
        Assert.Equal(0, a.OpacidadFranja, 6);
        var b = s.Actualizar(Guia(EstadoPanel.Frena, franja: "FRENÁ"), 0.340);
        Assert.Equal(0.5, b.OpacidadFranja, 6);
        double previa = 0;
        for (double t = 0.34; t < 1.5; t += 0.016)
        {
            var f = s.Actualizar(Guia(EstadoPanel.Frena, franja: "FRENÁ"), t).OpacidadFranja;
            Assert.True(f >= previa - 1e-9, "la franja no baja ni pulsa");
            previa = f;
        }
        Assert.Equal(1, previa, 6);
    }

    [Fact]
    public void LasCifrasSeActualizanComoMaximoACadaDiezHertz()
    {
        var s = new Suavizador();
        var vistos = new List<string?>();
        double m = 300;
        // 60 Hz durante 1 s: la cuenta regresiva cambia en cada tick
        for (int i = 0; i <= 60; i++)
        {
            var c = s.Actualizar(Guia(EstadoPanel.Aproximacion, metros: $"{m:0} m"), i / 60.0);
            vistos.Add(c.Panel.Metros);
            m -= 1;
        }
        int cambios = 0;
        for (int i = 1; i < vistos.Count; i++) if (vistos[i] != vistos[i - 1]) cambios++;
        Assert.InRange(cambios, 1, 10);       // 1 s -> a lo sumo ~10 actualizaciones
    }

    [Fact]
    public void UnCambioDeEstadoOCartelPasaAlInstanteAunqueNoHayanPasado100Ms()
    {
        var s = new Suavizador();
        s.Actualizar(Guia(EstadoPanel.Aproximacion, metros: "150 m"), 1.0);
        var c = s.Actualizar(Guia(EstadoPanel.Inminente, metros: "99 m"), 1.016);
        Assert.Equal(EstadoPanel.Inminente, c.Panel.Estado);
        Assert.Equal("99 m", c.Panel.Metros);
        var d = s.Actualizar(Guia(EstadoPanel.Inminente, metros: "98 m", numero: "100"), 1.032);
        Assert.Equal("100", d.Panel.NumeroPrincipal);
    }

    [Fact]
    public void LasTarjetasQueNoSonGuiaSeMuestranSinFundido()
    {
        var s = new Suavizador();
        var c = s.Actualizar(ConstructorVista.SinDatos(new ContextoVista()), 0);
        Assert.Equal(1, c.OpacidadTarjeta);
        Assert.Equal(TipoTarjeta.SinDatos, c.Panel.Tipo);
    }

    [Fact]
    public void ElPitidoSuenaUnaVezPorCurvaYSoloSiEstaHabilitado()
    {
        var g = new GestorPitido { Habilitado = true };
        PanelGuia P(EstadoPanel e, int i) => new() { Estado = e, IndiceCurva = i };
        Assert.False(g.Debe(P(EstadoPanel.Inminente, 1)));
        Assert.True(g.Debe(P(EstadoPanel.Frena, 1)));
        Assert.False(g.Debe(P(EstadoPanel.Frena, 1)));
        Assert.False(g.Debe(P(EstadoPanel.Frena, 1)));
        Assert.False(g.Debe(P(EstadoPanel.Lejos, 2)));
        Assert.True(g.Debe(P(EstadoPanel.Frena, 2)));
        g.Habilitado = false;
        Assert.False(g.Debe(P(EstadoPanel.Lejos, 3)));
        Assert.False(g.Debe(P(EstadoPanel.Frena, 3)));
    }
}
