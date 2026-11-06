using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

public sealed class OverlayVistaTests
{
    private static BloqueCurva Bloque(double frenar, int vEntrada = 248, int vVertice = 96)
        => new() { FrenarEn = frenar, Vertice = frenar + 150, VEntradaKmh = vEntrada, VVerticeKmh = vVertice, Vueltas = 5 };

    private static PanelGuia Guia(EstadoPanel e, TipoCurva tipo = TipoCurva.Frenada, int? cartelIa = 200, int? cartelVos = 200,
        ReferenciaGuia principal = ReferenciaGuia.Vos, bool dosRefs = true, double metros = 185, int velP = 132, int velS = 135)
    {
        var secundaria = dosRefs ? (principal == ReferenciaGuia.Ia ? ReferenciaGuia.Vos : ReferenciaGuia.Ia) : (ReferenciaGuia?)null;
        int? cP = principal == ReferenciaGuia.Ia ? cartelIa : cartelVos;
        int? cS = secundaria is null ? null : (secundaria == ReferenciaGuia.Ia ? cartelIa : cartelVos);
        if (cS == cP) cS = null;
        return new PanelGuia
        {
            Estado = e, Visible = true, IndiceCurva = 6, NombreCurva = "Curva 7", Tipo = tipo,
            Curva = new CurvaGuardada { Nombre = "Curva 7", Ia = Bloque(1000), Vos = Bloque(1040) },
            Principal = principal, Secundaria = secundaria,
            MetrosHastaFrenar = metros, VentanaM = 300,
            CartelIa = cartelIa, CartelVos = cartelVos, CartelPrincipal = cP, CartelSecundario = cS,
            VelMinPrincipalKmh = velP, VelMinSecundariaKmh = dosRefs ? velS : null,
            TextoEstado = e switch { EstadoPanel.Frena => "FRENÁ", EstadoPanel.Toque => "TOQUE", EstadoPanel.Levanta => "LEVANTÁ", _ => "" },
        };
    }

    private static readonly ContextoVista Ctx = new("GT3", "Pista de ejemplo", "GP", 5000);

    [Fact]
    public void AproximacionConUnSoloCartelTieneEtiquetaDobleYReglaSinNumeros()
    {
        var v = ConstructorVista.Construir(Guia(EstadoPanel.Aproximacion), Ctx);
        Assert.Equal(TipoTarjeta.Guia, v.Tipo);
        Assert.Equal("CURVA 7", v.CurvaTxt);
        Assert.Equal("248 → 96 km/h", v.VelocidadesTxt);
        Assert.Equal("200", v.NumeroPrincipal);
        Assert.Equal("m", v.UnidadPrincipal);
        Assert.Equal(["IA", "VOS"], v.EtiquetasPrincipal.Select(e => e.Texto));
        Assert.Equal([FormaMarcador.Rombo, FormaMarcador.Circulo], v.EtiquetasPrincipal.Select(e => e.Forma));
        Assert.Null(v.NumeroSecundario);
        Assert.Null(v.TextoFranja);
        Assert.Equal("185 m", v.Metros);
        Assert.True(v.MostrarRegla);
        Assert.Equal(300, v.EscalaReglaM);                       // la regla usa la ventana vigente
        Assert.Equal(Paleta.Blanco, v.ColorNumero);
    }

    [Fact]
    public void DosCartelesDistintosVosGrandeIaALaDerecha()
    {
        var v = ConstructorVista.Construir(Guia(EstadoPanel.Aproximacion, cartelIa: 300, cartelVos: 200), Ctx);
        Assert.Equal("200", v.NumeroPrincipal);
        Assert.Equal(["VOS"], v.EtiquetasPrincipal.Select(e => e.Texto));
        Assert.Equal(Paleta.AzulVos, v.EtiquetasPrincipal[0].Color);
        Assert.Equal("300", v.NumeroSecundario);
        Assert.Equal("m", v.UnidadSecundaria);
        Assert.Equal("IA", v.EtiquetaSecundaria!.Texto);
        Assert.Equal(FormaMarcador.Rombo, v.EtiquetaSecundaria.Forma);
        Assert.Equal(Paleta.NaranjaIa, v.EtiquetaSecundaria.Color);
        // la regla lleva un marcador por referencia, distinguidos por forma
        Assert.Equal(2, v.Marcadores.Count);
        Assert.Contains(v.Marcadores, m => m.Forma == FormaMarcador.Rombo);
        Assert.Contains(v.Marcadores, m => m.Forma == FormaMarcador.Circulo);
        Assert.All(v.Marcadores, m => Assert.InRange(m.Fraccion, 0, 1));
    }

    [Fact]
    public void ElMarcadorDeLaOtraReferenciaSeCorrePorLaDiferenciaDeFrenado()
    {
        // vos frena en 1040 y la IA en 1000 (40 m antes): con 185 m para el de vos, la IA queda 40 m mas cerca
        var v = ConstructorVista.Construir(Guia(EstadoPanel.Aproximacion, cartelIa: 300), Ctx);
        double ia = v.Marcadores.Single(m => m.Forma == FormaMarcador.Rombo).Fraccion;
        double vos = v.Marcadores.Single(m => m.Forma == FormaMarcador.Circulo).Fraccion;
        Assert.Equal(185 / 300.0, vos, 6);
        Assert.Equal(145 / 300.0, ia, 6);
    }

    [Fact]
    public void InminentePintaAmbarSoloLaCifraYLaCuentaRegresiva()
    {
        var v = ConstructorVista.Construir(Guia(EstadoPanel.Inminente, metros: 62), Ctx);
        Assert.Equal(Paleta.Ambar, v.ColorNumero);
        Assert.Equal(Paleta.Ambar, v.ColorMetros);
        Assert.Equal("62 m", v.Metros);
        Assert.All(v.EtiquetasPrincipal, e => Assert.NotEqual(Paleta.Ambar, e.Color));     // nunca ambar en etiquetas
    }

    [Fact]
    public void FrenaTieneFranjaRojaSuperiorYNadaMas()
    {
        var v = ConstructorVista.Construir(Guia(EstadoPanel.Frena, cartelIa: 300), Ctx);
        Assert.Equal("FRENÁ", v.TextoFranja);
        Assert.Equal("#C62F26", v.ColorFranja);
        Assert.Equal("CURVA 7", v.CurvaTxt);
        Assert.Equal("200", v.NumeroPrincipal);
        Assert.Equal("300", v.NumeroSecundario);
        Assert.False(v.MostrarRegla);
        Assert.Empty(v.Marcadores);
        Assert.Null(v.Metros);
        Assert.Null(v.VelocidadesTxt);
        Assert.False(v.ContornoRojo);
    }

    [Fact]
    public void FrenaConUnSoloCartelEsEtiquetaDoble()
    {
        var v = ConstructorVista.Construir(Guia(EstadoPanel.Frena), Ctx);
        Assert.Equal(["IA", "VOS"], v.EtiquetasPrincipal.Select(e => e.Texto));
        Assert.Null(v.NumeroSecundario);
    }

    [Fact]
    public void RojoSuaveUsaFranjaOscuraYContornoRojo()
    {
        var v = ConstructorVista.Construir(Guia(EstadoPanel.Frena), Ctx with { RojoSuave = true });
        Assert.Equal("#5A1D19", v.ColorFranja);
        Assert.Equal("#FF9C94", v.ColorTextoFranja);
        Assert.True(v.ContornoRojo);
    }

    [Theory]
    [InlineData(EstadoPanel.Toque, "TOQUE")]
    [InlineData(EstadoPanel.Levanta, "LEVANTÁ")]
    public void ToqueYLevantaUsanFranjaNeutraNuncaRoja(EstadoPanel e, string texto)
    {
        var tipo = e == EstadoPanel.Toque ? TipoCurva.Toque : TipoCurva.Levanta;
        foreach (bool suave in new[] { false, true })
        {
            var v = ConstructorVista.Construir(Guia(e, tipo, null, null), Ctx with { RojoSuave = suave });
            Assert.Equal(texto, v.TextoFranja);
            Assert.Equal("#3B4652", v.ColorFranja);
            Assert.Equal(Paleta.Blanco, v.ColorTextoFranja);
            Assert.False(v.ContornoRojo);
        }
    }

    [Fact]
    public void ToqueMuestraVelocidadMinimaYMetrosChicosLevantaSinMetros()
    {
        var t = ConstructorVista.Construir(Guia(EstadoPanel.Toque, TipoCurva.Toque, null, null, velP: 132, velS: 140, metros: 85.4), Ctx);
        Assert.Equal("132", t.NumeroPrincipal);
        Assert.Equal("km/h", t.UnidadPrincipal);
        Assert.Equal("85 m", t.Metros);
        Assert.Equal("140", t.NumeroSecundario);
        Assert.Equal("km/h", t.UnidadSecundaria);

        var l = ConstructorVista.Construir(Guia(EstadoPanel.Levanta, TipoCurva.Levanta, null, null, velP: 187), Ctx);
        Assert.Equal("187", l.NumeroPrincipal);
        Assert.Null(l.Metros);
        Assert.False(l.MostrarRegla);
    }

    [Fact]
    public void VelocidadSecundariaSoloSiDifiereTresKmhOMas()
    {
        var igual = ConstructorVista.Construir(Guia(EstadoPanel.Toque, TipoCurva.Toque, null, null, velP: 132, velS: 134), Ctx);
        Assert.Null(igual.NumeroSecundario);
        Assert.Equal(["IA", "VOS"], igual.EtiquetasPrincipal.Select(e => e.Texto));
        var tres = ConstructorVista.Construir(Guia(EstadoPanel.Toque, TipoCurva.Toque, null, null, velP: 132, velS: 135), Ctx);
        Assert.Equal("135", tres.NumeroSecundario);
    }

    [Fact]
    public void UnaSolaReferenciaLlevaUnaSolaEtiqueta()
    {
        var v = ConstructorVista.Construir(Guia(EstadoPanel.Aproximacion, dosRefs: false, principal: ReferenciaGuia.Ia), Ctx);
        Assert.Equal(["IA"], v.EtiquetasPrincipal.Select(e => e.Texto));
        Assert.Single(v.Marcadores);
    }

    [Fact]
    public void LejosEsUnaLineaConMetrosRedondeadosA10()
    {
        var p = Guia(EstadoPanel.Lejos, metros: 643);
        var v = ConstructorVista.Construir(p, Ctx);
        Assert.Equal(TipoTarjeta.Lejos, v.Tipo);
        Assert.Equal("Próxima", v.Linea1);
        Assert.Equal("CURVA 7", v.CurvaTxt);
        Assert.Equal("640 m", v.Metros);
        Assert.Null(v.NumeroPrincipal);
    }

    [Fact]
    public void SinDatosDiceQueHacer()
    {
        var v = ConstructorVista.SinDatos(Ctx);
        Assert.Equal("SIN DATOS", v.Titulo);
        Assert.Equal("No hay curvas guardadas para GT3 en Pista de ejemplo · GP.", v.Linea1);
        Assert.Equal("Bandeja › Modo › Grabar y da unas 10 vueltas con IA en pista.", v.Linea2);
        Assert.Equal("ESPERANDO AMS2", ConstructorVista.EsperandoAms2().Titulo);
        Assert.Contains("Project CARS 2", ConstructorVista.EsperandoAms2().Linea2);
        Assert.Contains("13", ConstructorVista.VersionNoCompatible(13).Linea1);
        Assert.Contains("14", ConstructorVista.VersionNoCompatible(13).Linea1);
    }

    [Fact]
    public void FormatoDeMetros()
    {
        Assert.Equal("185 m", FormatoUi.Metros(184.6));
        Assert.Equal("0 m", FormatoUi.Metros(-3));
        Assert.Equal("640 m", FormatoUi.MetrosLejos(643));
        Assert.Equal("650 m", FormatoUi.MetrosLejos(645));
        Assert.Equal("248 → 96 km/h", FormatoUi.Velocidades(248, 96));
    }
}
