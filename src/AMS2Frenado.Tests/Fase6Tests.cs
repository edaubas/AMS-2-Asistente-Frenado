using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>Fase 6: barra de botones, menu de configuracion, escala inversa, alfa minimo y textos sin "Bandeja" (UI-SPEC 7).</summary>
public class BarraBotonesTests
{
    [Fact]
    public void FijadoYLibreCambianFormaYTooltipSinRotulosDeTexto()
    {
        var b = new BarraBotones { Atajo = "Ctrl+Alt+L", Fijado = true };
        Assert.True(b.PinRelleno);
        Assert.Equal(0, b.PinAnguloGrados);
        Assert.Equal("Fijado: no se puede mover ni cambiar de tamaño. Clic para liberar (Ctrl+Alt+L)", b.TooltipPin);

        Assert.Equal(AccionBarra.AlternarFijado, b.Pulsar(BotonBarra.Fijar, 0));
        Assert.False(b.Fijado);
        Assert.False(b.PinRelleno);
        Assert.Equal(45, b.PinAnguloGrados);
        Assert.Equal("Libre: arrastrá para mover, rueda para el tamaño. Clic para fijar (Ctrl+Alt+L)", b.TooltipPin);

        Assert.Equal(AccionBarra.AlternarFijado, b.Pulsar(BotonBarra.Fijar, 1));
        Assert.True(b.Fijado);
    }

    [Fact]
    public void TodosLosBotonesFuncionanConElOverlayFijado()
    {
        var b = new BarraBotones { Fijado = true };
        Assert.Equal(AccionBarra.AlternarMenu, b.Pulsar(BotonBarra.Ajustes, 0));
        Assert.True(b.MenuAbierto);
        Assert.Equal(AccionBarra.AlternarFijado, b.Pulsar(BotonBarra.Fijar, 0));
        b.Fijado = true;
        b.Pulsar(BotonBarra.Modo, 0);
        Assert.True(b.PopoverModoAbierto);
        b.Pulsar(BotonBarra.Cerrar, 0);
        Assert.True(b.ConfirmandoCierre);
        Assert.Equal(AccionBarra.CerrarApp, b.ConfirmarCierre());
    }

    [Fact]
    public void EngranajeAlternaElMenu()
    {
        var b = new BarraBotones();
        Assert.Equal(AccionBarra.AlternarMenu, b.Pulsar(BotonBarra.Ajustes, 0));
        Assert.True(b.MenuAbierto);
        Assert.Equal(AccionBarra.AlternarMenu, b.Pulsar(BotonBarra.Ajustes, 1));
        Assert.False(b.MenuAbierto);
        b.FijarMenuAbierto(true);
        Assert.True(b.MenuAbierto);
    }

    [Fact]
    public void CerrarEsDeDosPasosYElSegundoClicEnLaXCancela()
    {
        var b = new BarraBotones();
        Assert.Equal(AccionBarra.Ninguna, b.Pulsar(BotonBarra.Cerrar, 10));
        Assert.True(b.ConfirmandoCierre);
        Assert.Equal(MedidasUi.BarraAncha, b.AnchoVentana);
        // un doble clic accidental: el segundo cae sobre CANCELAR, que esta donde estaba la X
        Assert.Equal(AccionBarra.Ninguna, b.Pulsar(BotonBarra.Cerrar, 10.2));
        Assert.False(b.ConfirmandoCierre);
        Assert.Equal(MedidasUi.BarraAncho, b.AnchoVentana);
        Assert.Equal(AccionBarra.Ninguna, b.ConfirmarCierre());          // sin pregunta abierta no cierra nada
    }

    [Fact]
    public void LaConfirmacionDeCierreSeCancelaSolaALos5Segundos()
    {
        var b = new BarraBotones();
        b.Pulsar(BotonBarra.Cerrar, 100);
        Assert.False(b.Actualizar(104.9));
        Assert.True(b.ConfirmandoCierre);
        Assert.Equal(0.02, b.FraccionRestante(104.9), 3);
        Assert.True(b.Actualizar(105.0));
        Assert.False(b.ConfirmandoCierre);
        Assert.Equal(0, b.FraccionRestante(105));
        Assert.False(b.Actualizar(106));
        Assert.Equal(AccionBarra.Ninguna, b.ConfirmarCierre());
    }

    [Fact]
    public void AvisoDeGrabacionSoloEnLaPreguntaYSoloGrabando()
    {
        var b = new BarraBotones { Modo = ModoApp.Grabar };
        Assert.Null(b.AvisoCierre);
        b.Pulsar(BotonBarra.Cerrar, 0);
        Assert.Equal("Estás grabando: se corta la grabación.", b.AvisoCierre);
        b.Modo = ModoApp.GrabarSoloVos;
        Assert.NotNull(b.AvisoCierre);
        b.Modo = ModoApp.Mostrar;
        Assert.Null(b.AvisoCierre);
    }

    [Fact]
    public void ElPopoverDeModoTieneTresOpcionesNoCiclaYMarcaElActual()
    {
        var b = new BarraBotones { Modo = ModoApp.Mostrar };
        Assert.Equal("Modo: Mostrar · clic para cambiar", b.TooltipModo);
        b.Pulsar(BotonBarra.Modo, 0);
        Assert.True(b.PopoverModoAbierto);
        Assert.Equal(ModoApp.Mostrar, b.Modo);                            // un clic solo abre: no cambia el modo
        var o = b.OpcionesModo;
        Assert.Equal([ModoApp.Grabar, ModoApp.GrabarSoloVos, ModoApp.Mostrar], o.Select(x => x.Modo));
        Assert.Equal(["Grabar todo", "Grabar solo vos", "Mostrar"], o.Select(x => x.Nombre));
        Assert.Equal([false, false, true], o.Select(x => x.Actual));
        Assert.Equal(AccionBarra.CambiarModo, b.ElegirModo(ModoApp.Grabar));
        Assert.Equal(ModoApp.Grabar, b.ModoElegido);
        Assert.False(b.PopoverModoAbierto);
        Assert.Equal("Modo: Grabar todo · clic para cambiar", b.TooltipModo);
        Assert.Equal(AccionBarra.Ninguna, b.ElegirModo((ModoApp)99));
    }

    [Fact]
    public void ElPopoverYLaPreguntaDeCerrarNoConviven()
    {
        var b = new BarraBotones();
        b.Pulsar(BotonBarra.Cerrar, 0);
        b.Pulsar(BotonBarra.Modo, 1);
        Assert.False(b.ConfirmandoCierre);
        Assert.True(b.PopoverModoAbierto);
        b.Pulsar(BotonBarra.Cerrar, 2);
        Assert.True(b.ConfirmandoCierre);
        Assert.False(b.PopoverModoAbierto);
    }

    [Fact]
    public void ConElOverlayOcultoSoloQuedaLaPastillaDelEngranaje()
    {
        var b = new BarraBotones { Oculto = true };
        Assert.Equal([BotonBarra.Ajustes], b.BotonesVisibles);
        Assert.Equal(32, b.AnchoVentana);
        Assert.Equal(AccionBarra.Ninguna, b.Pulsar(BotonBarra.Cerrar, 0));
        Assert.False(b.ConfirmandoCierre);
        Assert.Equal(AccionBarra.Ninguna, b.Pulsar(BotonBarra.Fijar, 0));
        Assert.Equal(AccionBarra.AlternarMenu, b.Pulsar(BotonBarra.Ajustes, 0));
        b.Oculto = false;
        Assert.Equal([BotonBarra.Modo, BotonBarra.Ajustes, BotonBarra.Fijar, BotonBarra.Cerrar], b.BotonesVisibles);
    }
}

public class MenuConfigTests
{
    [Fact]
    public void RangosYPasosSonLosDelSpec()
    {
        Assert.Equal((75.0, 150.0, 5.0), (MenuConfig.Tamano.Min, MenuConfig.Tamano.Max, MenuConfig.Tamano.Paso));
        Assert.Equal((85.0, 95.0, 1.0), (MenuConfig.Opacidad.Min, MenuConfig.Opacidad.Max, MenuConfig.Opacidad.Paso));
        Assert.Equal((50.0, 300.0, 10.0), (MenuConfig.TamanoAviso.Min, MenuConfig.TamanoAviso.Max, MenuConfig.TamanoAviso.Paso));
        Assert.Equal((0.0, 2.0, 0.1), (MenuConfig.Anticipacion.Min, MenuConfig.Anticipacion.Max, MenuConfig.Anticipacion.Paso));
        Assert.Equal((1.0, 2.0, 0.1), (MenuConfig.Duracion.Min, MenuConfig.Duracion.Max, MenuConfig.Duracion.Paso));
        Assert.Equal((1.0, 3.0), (MenuConfig.Rampa.Min, MenuConfig.Rampa.Max));
    }

    [Fact]
    public void LosValoresSeFormateanConComaDecimal()
    {
        var c = new ConfigApp { AvisoAnticipacionS = 0.8, Escala = 1.0, Opacidad = 0.88, AvisoDuracionS = 1.5 };
        Assert.Equal("0,8 s", MenuConfig.Anticipacion.LeerTexto(c));
        Assert.Equal("100 %", MenuConfig.Tamano.LeerTexto(c));
        Assert.Equal("88 %", MenuConfig.Opacidad.LeerTexto(c));
        Assert.Equal("1,5 s", MenuConfig.Duracion.LeerTexto(c));
        Assert.Equal("0,0 s", MenuConfig.Anticipacion.Formatear(0));
        Assert.Equal("2,0 s", MenuConfig.Rampa.LeerTexto(new ConfigApp()));      // aparicion del simbolo por defecto de la 5d
    }

    [Fact]
    public void LosTextosDelAvisoNoDicenRampaNiAvanzadoYAclaranQueVaParaPitidoYSimbolo()
    {
        Assert.Equal("Anticipación (pitido y símbolo)", MenuConfig.Anticipacion.Etiqueta);
        Assert.Equal("Segundos antes del punto de frenado en que suena el pitido y el símbolo llega al 100 %. 0 = justo en el punto.", MenuConfig.Anticipacion.Ayuda);
        Assert.Equal("Aparición del símbolo", MenuConfig.Rampa.Etiqueta);
        Assert.Equal("Cuántos segundos antes empieza a verse el símbolo, de a poco. Por defecto 2,0 s.", MenuConfig.Rampa.Ayuda);
        foreach (var d in MenuConfig.Deslizadores)
        {
            Assert.DoesNotContain("rampa", d.Etiqueta, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("avanzado", d.Etiqueta, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("rampa", d.Ayuda ?? "", StringComparison.OrdinalIgnoreCase);
        }
        // la bandeja usa los mismos textos
        Assert.Equal(MenuConfig.Etiquetas.Anticipacion, MenuConfig.Anticipacion.Etiqueta);
        Assert.Equal(MenuConfig.Etiquetas.Aparicion, MenuConfig.Rampa.Etiqueta);
    }

    [Fact]
    public void ElEstadoFijadoLibreNoTieneRotuloNiChipDeTexto()
    {
        // el estado se ve en el pin y en su tooltip: no quedan constantes ni propiedades de rotulo/chip
        var miembros = typeof(BarraBotones).GetMembers().Select(m => m.Name).Concat(typeof(TextosBarra).GetMembers().Select(m => m.Name)).ToList();
        foreach (var nombre in new[] { "EtiquetaPin", "EtiquetaFijo", "EtiquetaLibre", "ChipLibre", "MostrarChipLibre", "TextoChipLibre" })
            Assert.DoesNotContain(nombre, miembros);
        var b = new BarraBotones { Atajo = "Ctrl+Alt+L", Fijado = false };
        Assert.Contains("Ctrl+Alt+L", b.TooltipPin);
    }

    [Theory]
    [InlineData("tamano", 63, 75)]
    [InlineData("tamano", 78, 80)]
    [InlineData("tamano", 500, 150)]
    [InlineData("opacidad", 80, 85)]
    [InlineData("opacidad", 99, 95)]
    [InlineData("opacidad", 90.4, 90)]
    [InlineData("tamanoAviso", 94, 90)]
    [InlineData("anticipacion", 0.84, 0.8)]
    [InlineData("anticipacion", 3, 2.0)]
    [InlineData("anticipacion", -1, 0)]
    [InlineData("duracion", 0.2, 1.0)]
    [InlineData("rampa", 2.3, 2.5)]
    public void AjustarLlevaAlPasoMasCercanoDentroDelRango(string id, double entrada, double esperado)
        => Assert.Equal(esperado, MenuConfig.PorId(id)!.Ajustar(entrada), 6);

    [Fact]
    public void AjustarToleraValoresNoFinitos()
    {
        Assert.Equal(0, MenuConfig.Anticipacion.Ajustar(double.NaN));
        Assert.Equal(75, MenuConfig.Tamano.Ajustar(double.NegativeInfinity), 6);
        Assert.Equal(150, MenuConfig.Tamano.Ajustar(double.PositiveInfinity), 6);
    }

    [Fact]
    public void AplicarEscribeEnLaConfigYLaNormaliza()
    {
        var c = new ConfigApp();
        Assert.Equal(125, MenuConfig.Tamano.Aplicar(c, 125));
        Assert.Equal(1.25, c.Escala, 6);
        Assert.Equal(90, MenuConfig.Opacidad.Aplicar(c, 90));
        Assert.Equal(0.90, c.Opacidad, 6);
        Assert.Equal(200, MenuConfig.TamanoAviso.Aplicar(c, 200));
        Assert.Equal(2.0, c.AvisoEscala, 6);
        Assert.Equal(0.8, MenuConfig.Anticipacion.Aplicar(c, 0.8), 6);
        Assert.Equal(0.8, c.AvisoAnticipacionS, 6);
        Assert.Equal(1.2, MenuConfig.Duracion.Aplicar(c, 1.2), 6);
        Assert.Equal(1.2, c.AvisoDuracionS, 6);
        Assert.Equal(3.0, MenuConfig.Rampa.Aplicar(c, 3.0), 6);
        Assert.Equal(3.0, c.AvisoRampaS, 6);
    }

    [Fact]
    public void ElMenuAbreEnLaPrincipalYNavegaPorPaginasInternas()
    {
        var m = new MenuConfig();
        m.IrA(PaginaMenu.Datos);                       // cerrado: no hace nada
        Assert.Equal(PaginaMenu.Principal, m.Pagina);
        m.Abrir(0);
        Assert.Equal("AJUSTES", m.TituloPagina);
        m.IrA(PaginaMenu.Datos);
        Assert.Equal("DATOS", m.TituloPagina);
        m.Volver();
        m.IrA(PaginaMenu.Atajo);
        Assert.Equal("ATAJO DE BLOQUEO", m.TituloPagina);
        m.Cerrar();
        m.Abrir(5);
        Assert.Equal(PaginaMenu.Principal, m.Pagina);   // siempre abre en la principal
    }

    [Fact]
    public void ElMenuSeCierraA15SegundosConElMouseAfueraYNoSiVuelve()
    {
        var m = new MenuConfig();
        m.Abrir(100);
        Assert.False(m.Actualizar(114.9, mouseAdentro: false));
        Assert.True(m.Abierto);
        Assert.True(m.RestanteAutocierreS(114.9) < 0.2);
        Assert.False(m.Actualizar(114.95, mouseAdentro: true));       // el mouse volvio: se reinicia la cuenta
        Assert.False(m.Actualizar(125, mouseAdentro: false));
        Assert.True(m.Actualizar(130, mouseAdentro: false));
        Assert.False(m.Abierto);
        Assert.False(m.Actualizar(300, mouseAdentro: false));          // cerrado: nada que hacer
    }

    [Fact]
    public void ElGuardadoSeDifiere500msDesdeElUltimoCambioYSucedeUnaSolaVez()
    {
        var m = new MenuConfig();
        Assert.False(m.DebeGuardar(0));
        m.MarcarCambio(10.0);
        m.MarcarCambio(10.3);                                          // sigue moviendo la perilla
        Assert.False(m.DebeGuardar(10.7));
        Assert.True(m.DebeGuardar(10.8));
        Assert.False(m.DebeGuardar(11.5));
        m.MarcarCambio(20);
        Assert.True(m.DebeGuardarYa());                                // al cerrar el menu no se espera
        Assert.False(m.DebeGuardarYa());
        Assert.False(m.DebeGuardar(30));
    }

    [Fact]
    public void LaBandejaYElMenuComparten4AtajosSugeridos()
    {
        Assert.Equal(["Ctrl+Alt+L", "Ctrl+Alt+O", "Ctrl+Shift+F9", "Ctrl+Alt+F10"], MenuConfig.AtajosSugeridos);
        Assert.All(MenuConfig.AtajosSugeridos, a => Assert.NotNull(AtajoTeclado.Parsear(a)));
    }
}

public class MedidasFase6Tests
{
    [Fact]
    public void ElAlfaMinimoEs085YUnConfigViejoConMenosSeLlevaAlMinimo()
    {
        Assert.Equal(0.85, MedidasUi.AlfaMinimo);
        Assert.Equal(0.85, MedidasUi.LimitarAlfa(0.80));
        Assert.Equal(0.85, MedidasUi.LimitarAlfa(0.70));
        Assert.Equal(0.95, MedidasUi.LimitarAlfa(0.99));
        Assert.Equal(0.88, MedidasUi.LimitarAlfa(double.NaN));
        var dir = Path.Combine(Path.GetTempPath(), "ams2-f6-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            var ruta = Path.Combine(dir, "config.json");
            File.WriteAllText(ruta, """{ "formato": 1, "opacidad": 0.8 }""");
            Assert.Equal(0.85, new AlmacenConfig(ruta).Cargar().Opacidad);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(0.875, 1.0)]
    public void SinEscalaInversaConElOverlayDe875OMas(double escala, double esperada)
        => Assert.Equal(esperada, MedidasUi.EscalaBarra(escala), 9);

    [Theory]
    [InlineData(0.75)]
    [InlineData(0.8)]
    [InlineData(0.85)]
    public void ConMenosDe875LaEscalaInversaMantieneElAreaClicableEn28PxReales(double escala)
    {
        Assert.Equal(0.875 / escala, MedidasUi.EscalaBarra(escala), 9);
        Assert.Equal(28, MedidasUi.AreaClicableRealPx(escala), 9);
    }

    [Fact]
    public void ElAreaClicableNuncaBajaDe28PxEnTodoElRango()
    {
        for (double e = 0.75; e <= 1.5; e += 0.05)
            Assert.True(MedidasUi.AreaClicableRealPx(e) >= 28 - 1e-9, $"escala {e}");
        Assert.Equal(1.0, MedidasUi.EscalaBarra(double.NaN));
    }

    [Fact]
    public void LasMedidasDelSpecEstanFijadas()
    {
        Assert.Equal(14, MedidasUi.CorteEsquina);
        Assert.Equal(16, MedidasUi.CorteEsquinaMenu);
        Assert.Equal(6, MedidasUi.TexturaLado);
        Assert.Equal(0.04, MedidasUi.TexturaAlfa);
        Assert.Equal((28.0, 32.0), (MedidasUi.BotonVisible, MedidasUi.BotonClicable));
        Assert.Equal(624, MedidasUi.MenuAncho);
        Assert.Equal(16, MedidasUi.SegmentosBarra);
    }
}

public class TextosSinBandejaTests
{
    private static readonly ContextoVista Ctx = new("GT3", "Pista de ejemplo", "GP", 5000);

    [Fact]
    public void LosTextosDeSinDatosYGrabandoApuntanAlOverlayYNoALaBandeja()
    {
        var textos = new List<string?>
        {
            ConstructorVista.SinCurvas(Ctx).Linea2,
            ConstructorVista.SinDatos(new PanelGuia { Estado = EstadoPanel.SinDatos, Causa = CausaSinDatos.LargoDistinto, LargoGuardadoM = 5000, LargoJuegoM = 5001 }, Ctx).Linea2,
            ConstructorVista.SinDatos(new PanelGuia { Estado = EstadoPanel.SinDatos }, Ctx with { MotivoArchivo = MotivoLectura.Danado }).Linea2,
        };
        foreach (var t in textos) { Assert.NotNull(t); Assert.DoesNotContain("Bandeja", t); }
        Assert.Contains("⇄", textos[0]);
        Assert.Contains("⇄ › Grabar todo", textos[1]);
        Assert.Contains("⚙ › Datos", textos[2]);
    }
}

public class PosicionMenuTests
{
    private static readonly AreaPantalla Pantalla = new(0, 0, 1920, 1040);

    [Fact]
    public void AbreAbajoAlineadoALaDerechaDeLaBarra()
    {
        var barra = new AreaPantalla(1400, 40, 140, 32);
        var (x, y) = Esquinas.PosicionMenu(barra, Pantalla, 624, 540);
        Assert.Equal(1540 - 624, x);
        Assert.Equal(72 + 6, y);
    }

    [Fact]
    public void SiAbajoNoEntraAbreArriba()
    {
        var barra = new AreaPantalla(1400, 900, 140, 32);
        var (_, y) = Esquinas.PosicionMenu(barra, Pantalla, 624, 540);
        Assert.Equal(900 - 6 - 540, y);
    }

    [Fact]
    public void SiNoHayLugarALaIzquierdaAlineaAlBordeIzquierdoDeLaBarra()
    {
        var barra = new AreaPantalla(100, 40, 140, 32);
        var (x, _) = Esquinas.PosicionMenu(barra, Pantalla, 624, 540);
        Assert.Equal(100, x);
    }

    [Fact]
    public void SiguePosicionadoDentroDeLaPantallaYLaPantallaChicaPasaAUnaColumna()
    {
        var chica = new AreaPantalla(0, 0, 700, 500);
        var (x, y) = Esquinas.PosicionMenu(new AreaPantalla(500, 20, 140, 32), chica, 624, 540);
        Assert.True(x >= 0 && x + 624 <= 700 + 1e-9 || 624 > 700);
        Assert.True(y >= 0);
        Assert.True(Esquinas.MenuEnUnaColumna(chica));
        Assert.False(Esquinas.MenuEnUnaColumna(Pantalla));
    }
}

public class ConfirmacionTemporizadaTests
{
    [Fact]
    public void PedirConfirmarYCancelar()
    {
        var c = new ConfirmacionTemporizada();
        Assert.False(c.Confirmar());                       // sin pedir, no confirma
        c.Pedir(10);
        Assert.True(c.Activa);
        Assert.True(c.Confirmar());
        Assert.False(c.Activa);
        c.Pedir(20); c.Cancelar();
        Assert.False(c.Confirmar());
    }

    [Fact]
    public void SeCancelaSolaALos5SegundosYLaFraccionBaja()
    {
        var c = new ConfirmacionTemporizada();
        c.Pedir(0);
        Assert.Equal(1.0, c.FraccionRestante(0), 6);
        Assert.Equal(0.5, c.FraccionRestante(2.5), 6);
        Assert.False(c.Actualizar(4.99));
        Assert.True(c.Actualizar(5.0));
        Assert.False(c.Activa);
        Assert.Equal(0, c.FraccionRestante(6));
    }
}
