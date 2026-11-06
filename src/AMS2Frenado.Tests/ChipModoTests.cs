using AMS2Frenado.Core;
using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>
/// Correccion de errores 1, punto 4: el chip del modo (MOSTRAR / GRABANDO / GRABANDO · SOLO VOS) tiene que reflejar el modo
/// configurado con o sin lectura de AMS2. Antes se armaba solo en Procesar (lectura Ok y en pista): sin juego quedaba en MOSTRAR.
/// </summary>
public sealed class ChipModoTests : IDisposable
{
    private readonly string _dir = Simulador.CarpetaTemporal();
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private static ResultadoLectura Ok(in SharedMemory sm) => new(EstadoLectura.Ok, ConstantesShm.Version, sm);

    public static TheoryData<ModoApp, string> Modos => new()
    {
        { ModoApp.Mostrar, "MOSTRAR" },
        { ModoApp.Grabar, "GRABANDO" },
        { ModoApp.GrabarSoloVos, "GRABANDO · SOLO VOS" },
    };

    [Theory]
    [MemberData(nameof(Modos))]
    public void SinJuegoElChipMuestraElModoConfiguradoYSeActualizaAlCambiarlo(ModoApp modo, string chip)
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        // arranca en Mostrar, sin juego; el usuario elige otro modo en el menu y el siguiente tick (sin juego) lo debe mostrar
        var v = s.Paso(ResultadoLectura.SinJuego, 0);
        Assert.Equal("ESPERANDO AMS2", v.Panel.Titulo);
        Assert.Equal("MOSTRAR", v.TextoChipModo);

        s.Modo = modo;
        Assert.Equal(chip, s.Ultima.TextoChipModo);               // inmediato, antes de cualquier lectura
        v = s.Paso(ResultadoLectura.SinJuego, 0.016);
        Assert.Equal("ESPERANDO AMS2", v.Panel.Titulo);
        Assert.Equal(chip, v.TextoChipModo);
        Assert.Equal(modo != ModoApp.Mostrar, v.Grabando);
        Assert.Equal(modo == ModoApp.GrabarSoloVos, v.SoloVos);
    }

    [Theory]
    [MemberData(nameof(Modos))]
    public void ElChipSigueElModoEnLosPanelesEsperandoVersionYEntrarAPista(ModoApp modo, string chip)
    {
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = modo };

        Assert.Equal(chip, s.Paso(ResultadoLectura.SinJuego, 0).TextoChipModo);

        var ver = s.Paso(new ResultadoLectura(EstadoLectura.VersionDistinta, 13, default), 1);
        Assert.Equal("VERSIÓN NO COMPATIBLE", ver.Panel.Titulo);
        Assert.Equal(chip, ver.TextoChipModo);

        var menu = new Simulador().Armar(); menu.mGameState = 1;     // menu de AMS2: sin sesion en pista
        var pista = s.Paso(Ok(menu), 2);
        Assert.Equal("Esperando una sesión en pista.", pista.Panel.Linea1);
        Assert.Equal(chip, pista.TextoChipModo);

        // lectura inestable: se mantiene lo ultimo, pero el chip sigue el modo
        Assert.Equal(chip, s.Paso(new ResultadoLectura(EstadoLectura.Inestable, 14, default), 3).TextoChipModo);
    }

    [Fact]
    public void CambiarDeModoConLaUltimaVistaDeUnPanelViejoYaActualizaElChip()
    {
        var s = new ServicioOverlay(new Almacen(_dir));
        s.Paso(ResultadoLectura.SinJuego, 0);
        s.Modo = ModoApp.Grabar;
        Assert.True(s.Ultima.Grabando);
        s.Modo = ModoApp.GrabarSoloVos;
        Assert.True(s.Ultima.SoloVos);
        // la lectura inestable que sigue conserva el chip nuevo, no el viejo
        Assert.Equal("GRABANDO · SOLO VOS", s.Paso(new ResultadoLectura(EstadoLectura.Inestable, 14, default), 1).TextoChipModo);
        s.Modo = ModoApp.Mostrar;
        Assert.Equal("MOSTRAR", s.Ultima.TextoChipModo);
        Assert.False(s.Ultima.Grabando);
    }

    [Theory]
    [MemberData(nameof(Modos))]
    public void ConJuegoEnPistaElChipTambienSigueElModo(ModoApp modo, string chip)
    {
        var sim = new Simulador(ias: 3);
        var s = new ServicioOverlay(new Almacen(_dir)) { Modo = ModoApp.Mostrar };
        for (int i = 0; i < 50; i++) s.Paso(Ok(sim.Paso()), sim.Tiempo);
        s.Modo = modo;
        // misma muestra repetida (no se reprocesa) y muestra nueva: ambas con el chip del modo
        var sm = sim.Paso();
        Assert.Equal(chip, s.Paso(Ok(sm), sim.Tiempo).TextoChipModo);
        Assert.Equal(chip, s.Paso(Ok(sm), sim.Tiempo + 0.001).TextoChipModo);
    }
}
