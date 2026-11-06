using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

public sealed class OverlayConfigTests : IDisposable
{
    private readonly string _dir = Simulador.CarpetaTemporal();
    private string Ruta => Path.Combine(_dir, "config.json");
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    [Fact]
    public void PrimerArranqueDaLosValoresPorDefectoDelHandover()
    {
        var a = new AlmacenConfig(Ruta);
        var c = a.Cargar();
        Assert.True(a.EsPrimerArranque);
        Assert.True(c.Bloqueado);
        Assert.False(c.Pitido);                       // apagado por defecto
        Assert.Equal(ModoApp.Mostrar, c.Modo);
        Assert.Equal(ReferenciaGuia.Ia, c.ReferenciaPrincipal);
        Assert.Equal(Esquina.ArribaDerecha, c.Esquina);
        Assert.Equal("Ctrl+Alt+L", c.Atajo);
        Assert.Equal(1.0, c.Escala);
        Assert.Equal(0.88, c.Opacidad);
        Assert.False(c.AvisoInicialMostrado);
        Assert.True(c.AvisoActivo);                   // el aviso de frenada viene activado
        Assert.Equal(1.5, c.AvisoDuracionS);
        Assert.Equal(1.0, c.AvisoEscala);
    }

    [Fact]
    public void ElAvisoSeNormalizaYUnConfigDeLaFase5ConCamposViejosSeLee()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
        File.WriteAllText(Ruta, """
            { "formato": 1, "modo": "grabar", "mapaVisible": false, "mapaCompacto": true, "rojoSuave": true,
              "avisoDuracionS": 9, "avisoEscala": 0.1, "escala": 1.25 }
            """);
        var c = new AlmacenConfig(Ruta).Cargar();
        Assert.Equal(ModoApp.Grabar, c.Modo);
        Assert.Equal(1.25, c.Escala);
        Assert.Equal(2.0, c.AvisoDuracionS);           // 1 a 2 s
        Assert.Equal(0.5, c.AvisoEscala);              // 50 a 300 %
        // y al guardar no reaparecen los campos que ya no existen
        new AlmacenConfig(Ruta).Guardar(c);
        string txt = File.ReadAllText(Ruta);
        Assert.DoesNotContain("mapaVisible", txt);
        Assert.DoesNotContain("rojoSuave", txt);
    }

    [Fact]
    public void AvisoEscalaYLado()
    {
        Assert.Equal(120, MedidasAviso.LadoDeEscala(1));
        Assert.Equal(60, MedidasAviso.LadoDeEscala(0));
        Assert.Equal(360, MedidasAviso.LadoDeEscala(99));
        Assert.Equal(0.5, MedidasAviso.EscalaDeLado(10));
        Assert.Equal(2.0, MedidasAviso.EscalaDeLado(240));
        Assert.Equal(1.0, MedidasAviso.LimitarEscala(double.NaN));
    }

    [Fact]
    public void CopiarDeLlevaTodosLosValores()
    {
        var o = new ConfigApp { Modo = ModoApp.Grabar, Pitido = true, AvisoActivo = false, AvisoEscala = 2, Opacidad = 0.95 };
        o.Posiciones["m"] = new PosicionVentana { X = 1, Y = 2 };
        var c = new ConfigApp();
        c.CopiarDe(o);
        Assert.Equal(ModoApp.Grabar, c.Modo);
        Assert.True(c.Pitido);
        Assert.False(c.AvisoActivo);
        Assert.Equal(2, c.AvisoEscala);
        Assert.Equal(0.95, c.Opacidad);
        Assert.Equal(1, c.Posiciones["m"].X);
        Assert.NotSame(o.Posiciones, c.Posiciones);
    }

    [Fact]
    public void GuardarYCargarConservaTodo()
    {
        var a = new AlmacenConfig(Ruta);
        var c = a.Cargar();
        c.Modo = ModoApp.Grabar; c.Bloqueado = false; c.Pitido = true; c.ReferenciaPrincipal = ReferenciaGuia.Vos;
        c.Esquina = Esquina.AbajoIzquierda; c.Escala = 1.25; c.Opacidad = 0.92; c.Atajo = "Ctrl+Shift+F9";
        c.AvisoActivo = false; c.AvisoDuracionS = 1.8; c.AvisoEscala = 2.5; c.AvisoInicialMostrado = true;
        c.AvisoPosiciones["\\\\.\\DISPLAY1"] = new PosicionVentana { X = 800, Y = 300 };
        c.Posiciones["\\\\.\\DISPLAY1"] = new PosicionVentana { X = 100.5, Y = 40 };
        a.Guardar(c);

        var b = new AlmacenConfig(Ruta);
        var r = b.Cargar();
        Assert.False(b.EsPrimerArranque);
        Assert.False(b.LecturaDanada);
        Assert.Equal(ModoApp.Grabar, r.Modo);
        Assert.False(r.Bloqueado);
        Assert.True(r.Pitido);
        Assert.Equal(ReferenciaGuia.Vos, r.ReferenciaPrincipal);
        Assert.Equal(Esquina.AbajoIzquierda, r.Esquina);
        Assert.Equal(1.25, r.Escala);
        Assert.Equal(0.92, r.Opacidad);
        Assert.Equal("Ctrl+Shift+F9", r.Atajo);
        Assert.True(r.AvisoInicialMostrado);
        Assert.False(r.AvisoActivo);
        Assert.Equal(1.8, r.AvisoDuracionS);
        Assert.Equal(2.5, r.AvisoEscala);
        Assert.Equal(800, r.AvisoPosiciones["\\\\.\\DISPLAY1"].X);
        Assert.Equal(100.5, r.Posiciones["\\\\.\\DISPLAY1"].X);
    }

    [Fact]
    public void LaEscrituraEsAtomicaYNoDejaTmp()
    {
        var a = new AlmacenConfig(Ruta);
        a.Guardar(new ConfigApp());
        a.Guardar(new ConfigApp { Pitido = true });
        Assert.False(File.Exists(Ruta + ".tmp"));
        Assert.True(File.Exists(Ruta + ".bak"));
        Assert.True(new AlmacenConfig(Ruta).Cargar().Pitido);
    }

    [Fact]
    public void ArchivoDanadoUsaElRespaldoOLosValoresPorDefecto()
    {
        var a = new AlmacenConfig(Ruta);
        a.Guardar(new ConfigApp { Escala = 1.5 });
        a.Guardar(new ConfigApp { Escala = 1.25 });      // el .bak queda con la anterior (1,5)
        File.WriteAllText(Ruta, "{ esto no es json");
        var b = new AlmacenConfig(Ruta);
        var c = b.Cargar();
        Assert.True(b.LecturaDanada);
        Assert.Equal(1.5, c.Escala);

        File.WriteAllText(Ruta + ".bak", "basura");
        var d = new AlmacenConfig(Ruta).Cargar();
        Assert.Equal(1.0, d.Escala);                      // todo danado: valores por defecto, sin lanzar
    }

    [Fact]
    public void CamposInvalidosSeCorrigenUnoPorUno()
    {
        Directory.CreateDirectory(_dir);
        Directory.CreateDirectory(Path.GetDirectoryName(Ruta)!);
        File.WriteAllText(Ruta, """
            { "modo": "otraCosa", "escala": 9, "opacidad": 0.1, "pitido": true, "atajo": "L",
              "esquina": 7, "referenciaPrincipal": "vos", "posiciones": { "m1": { "x": 5, "y": 6 } } }
            """);
        var c = new AlmacenConfig(Ruta).Cargar();
        Assert.Equal(ModoApp.Mostrar, c.Modo);               // enum desconocido: por defecto
        Assert.Equal(1.5, c.Escala);                         // limitada
        Assert.Equal(0.80, c.Opacidad);
        Assert.True(c.Pitido);                               // los campos buenos se conservan
        Assert.Equal("Ctrl+Alt+L", c.Atajo);                 // sin modificador: invalido
        Assert.Equal(Esquina.ArribaDerecha, c.Esquina);
        Assert.Equal(ReferenciaGuia.Vos, c.ReferenciaPrincipal);
        Assert.Equal(6, c.Posiciones["m1"].Y);
    }

    [Fact]
    public void AtajoSeParseaYSeValida()
    {
        var a = AtajoTeclado.Parsear("ctrl + alt + l")!;
        Assert.Equal(Modificadores.Control | Modificadores.Alt, a.Modificadores);
        Assert.Equal("Ctrl+Alt+L", a.Texto);
        Assert.Equal('L', a.CodigoVirtual);
        Assert.Equal(0x78, AtajoTeclado.Parsear("Alt+F9")!.CodigoVirtual);
        Assert.Null(AtajoTeclado.Parsear("L"));
        Assert.Null(AtajoTeclado.Parsear("Ctrl+Alt"));
        Assert.Null(AtajoTeclado.Parsear("Ctrl+Ctrl+L"));
        Assert.Null(AtajoTeclado.Parsear("Ctrl+F13"));
        Assert.Null(AtajoTeclado.Parsear("Ctrl+A+B"));
        Assert.Null(AtajoTeclado.Parsear(""));
    }

    [Fact]
    public void EsquinasUsanMargen24()
    {
        var a = new AreaPantalla(0, 0, 1920, 1080);
        Assert.Equal((1920 - 24 - 320.0, 24.0), Esquinas.Posicionar(Esquina.ArribaDerecha, a, 320, 400));
        Assert.Equal((24.0, 24.0), Esquinas.Posicionar(Esquina.ArribaIzquierda, a, 320, 400));
        Assert.Equal((1920 - 24 - 320.0, 1080 - 24 - 400.0), Esquinas.Posicionar(Esquina.AbajoDerecha, a, 320, 400));
        Assert.Equal((24.0, 1080 - 24 - 400.0), Esquinas.Posicionar(Esquina.AbajoIzquierda, a, 320, 400));
        var b = new AreaPantalla(1920, 0, 1920, 1040);       // segundo monitor a la derecha
        Assert.Equal((3840 - 24 - 320.0, 24.0), Esquinas.Posicionar(Esquina.ArribaDerecha, b, 320, 400));
    }

    [Fact]
    public void AjustarDentroYVisible()
    {
        var a = new AreaPantalla(0, 0, 1920, 1080);
        Assert.Equal((1600.0, 680.0), Esquinas.AjustarDentro((5000, 5000), a, 320, 400));
        Assert.Equal((0.0, 0.0), Esquinas.AjustarDentro((-50, -9), a, 320, 400));
        Assert.True(Esquinas.Visible((100, 100), a, 320, 400));
        Assert.False(Esquinas.Visible((4000, 100), a, 320, 400));       // el monitor ya no esta
    }

    [Fact]
    public void OpacidadSoloDelFondoEnRango()
    {
        Assert.Equal(0.80, MedidasUi.AlfaDeSlider(0), 6);
        Assert.Equal(0.95, MedidasUi.AlfaDeSlider(1), 6);
        Assert.Equal(0.80, MedidasUi.AlfaDeSlider(-3), 6);
        Assert.Equal(0.5, MedidasUi.SliderDeAlfa(0.875), 6);
        Assert.Equal(1.0, MedidasUi.AlfaPanel(0.8, desbloqueada: true));     // desbloqueada: 100 %
        Assert.Equal(320, MedidasUi.AnchoDeEscala(1));
        Assert.Equal(240, MedidasUi.AnchoDeEscala(0.1));
        Assert.Equal(480, MedidasUi.AnchoDeEscala(9));
        Assert.Equal(0.75, MedidasUi.EscalaDeAncho(100));
    }
}
