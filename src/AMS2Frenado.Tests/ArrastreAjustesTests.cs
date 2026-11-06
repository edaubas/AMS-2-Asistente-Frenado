using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>Correcciones 1, punto 5: mover la ventana de ajustes por la cabecera (limites, memoria de posicion, sin pisar la elegida).</summary>
public class ArrastreAjustesTests
{
    private static readonly AreaPantalla Pantalla = new(0, 0, 1920, 1040);          // monitor 1 (area de trabajo)
    private static readonly AreaPantalla Segundo = new(1920, 0, 1280, 984);         // monitor 2, a la derecha
    private static AreaPantalla AreaEn(double x, double y) => x >= 1920 ? Segundo : Pantalla;

    [Fact]
    public void ElArrastreSiguePorDesfaseDelCursor()
    {
        var a = new ArrastreVentana();
        a.Iniciar(cursorX: 500, cursorY: 300, ventanaX: 400, ventanaY: 250);       // el cursor agarra la ventana 100 px a la derecha y 50 abajo de su esquina
        Assert.True(a.Activo);
        Assert.Equal((300.0, 150.0), a.Mover(400, 200, Pantalla, 624, 590));
        Assert.Equal((900.0, 450.0), a.Mover(1000, 500, Pantalla, 624, 590));
        Assert.True(a.Desplazado);
    }

    [Fact]
    public void NoSaleDeLaPantallaPorNingunLado()
    {
        var a = new ArrastreVentana();
        a.Iniciar(100, 100, 50, 50);
        Assert.Equal((0.0, 0.0), a.Mover(-500, -500, Pantalla, 624, 590));                        // arriba a la izquierda
        Assert.Equal((1920 - 624.0, 1040 - 590.0), a.Mover(9000, 9000, Pantalla, 624, 590));      // abajo a la derecha
        Assert.Equal((1920 - 624.0, 0.0), a.Mover(9000, -9000, Pantalla, 624, 590));
    }

    [Fact]
    public void ConUnaVentanaMasGrandeQueElAreaQuedaEnLaEsquinaSuperiorIzquierda()
    {
        var a = new ArrastreVentana();
        a.Iniciar(10, 10, 0, 0);
        Assert.Equal((0.0, 0.0), a.Mover(600, 600, new AreaPantalla(0, 0, 500, 400), 624, 590));
    }

    [Fact]
    public void PuedePasarAOtroMonitorYQuedaDentroDeEse()
    {
        var a = new ArrastreVentana();
        a.Iniciar(1000, 100, 900, 50);
        var (x, y) = a.Mover(2000, 100, AreaEn(2000, 100), 624, 590);                  // el cursor ya esta en el monitor 2
        Assert.Equal(1920.0, x);        // cursor 2000 + desfase -100 = 1900: se lleva dentro del monitor 2
        Assert.Equal(50.0, y);
        Assert.True(x >= Segundo.X && x + 624 <= Segundo.Derecha);
    }

    [Fact]
    public void SinArrastreActivoNoMueveYUnClicSinMoverNoCuentaComoDesplazamiento()
    {
        var a = new ArrastreVentana();
        Assert.Equal((0.0, 0.0), a.Mover(500, 500, Pantalla, 624, 590));
        a.Iniciar(300, 300, 200, 200);
        a.Mover(300, 300, Pantalla, 624, 590);
        Assert.False(a.Desplazado);
        a.Terminar();
        Assert.False(a.Activo);
        Assert.Equal((200.0, 200.0), a.Mover(900, 900, Pantalla, 624, 590));          // ya no sigue al cursor
        a.Iniciar(0, 0, 0, 0);
        Assert.Equal((0.0, 0.0), a.Mover(double.NaN, 5, Pantalla, 624, 590));          // cursor invalido: se queda
    }

    [Fact]
    public void SinPosicionElegidaVaJuntoALaBarraYConElegidaManda()
    {
        var barra = new AreaPantalla(1700, 24, 140, 32);
        var p = new PosicionAjustes();
        var auto = p.Resolver(barra, Pantalla, AreaEn, 624, 590);
        Assert.Equal(Esquinas.PosicionMenu(barra, Pantalla, 624, 590), auto);

        p.Elegir(300, 200);
        Assert.Equal((300.0, 200.0), p.Resolver(barra, Pantalla, AreaEn, 624, 590));
        // el recalculo con el alto real (o una barra que se movio) NO pisa la posicion elegida
        var otraBarra = new AreaPantalla(100, 500, 140, 32);
        Assert.Equal((300.0, 200.0), p.Resolver(otraBarra, Pantalla, AreaEn, 624, 640));
    }

    [Fact]
    public void LaPosicionElegidaSeRecuerdaYSeLlevaDentroSiLaPantallaCambio()
    {
        var p = new PosicionAjustes();
        p.Elegir(1500, 900);                                                           // se salia: el alto real o la resolucion cambiaron
        var (x, y) = p.Resolver(new AreaPantalla(0, 0, 140, 32), Pantalla, AreaEn, 624, 590);
        Assert.Equal((1920 - 624.0, 1040 - 590.0), (x, y));
        // en el segundo monitor se queda en el segundo monitor
        p.Elegir(2500, 100);
        Assert.Equal((2500.0, 100.0), p.Resolver(new AreaPantalla(0, 0, 140, 32), Pantalla, AreaEn, 624, 590));
        p.Elegir(double.NaN, 3);                                                       // valor invalido: se ignora
        Assert.Equal((2500.0, 100.0), (p.X, p.Y));
        p.Olvidar();
        Assert.False(p.Elegida);
    }

    [Fact]
    public void LaPosicionDeAjustesSePersisteYSeIgnoraSiEstaDanada()
    {
        string dir = Simulador.CarpetaTemporal();
        try
        {
            var alm = new AlmacenConfig(Path.Combine(dir, "config.json"));
            alm.Guardar(new ConfigApp { AjustesPosicion = new PosicionVentana { X = 321.5, Y = 120 } });
            var c = alm.Cargar();
            Assert.Equal((321.5, 120.0), (c.AjustesPosicion!.X, c.AjustesPosicion.Y));

            File.WriteAllText(alm.Ruta, """{ "formato": 1, "ajustesPosicion": null }""");
            Assert.Null(alm.Cargar().AjustesPosicion);
            File.WriteAllText(alm.Ruta, """{ "formato": 1, "ajustesPosicion": { "x": "hola" } }""");
            Assert.Null(alm.Cargar().AjustesPosicion);                                  // tipo incorrecto: queda automatica
            File.WriteAllText(alm.Ruta, """{ "formato": 1, "modo": "mostrar" }""");
            Assert.Null(alm.Cargar().AjustesPosicion);                                  // config vieja sin el campo

            var d = new ConfigApp();
            d.CopiarDe(new ConfigApp { AjustesPosicion = new PosicionVentana { X = 5, Y = 6 } });
            Assert.Equal(5, d.AjustesPosicion!.X);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
