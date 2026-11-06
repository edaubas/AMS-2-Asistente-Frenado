namespace AMS2Frenado.Core.Overlay;

public enum BotonBarra { Modo, Ajustes, Fijar, Cerrar }

/// <summary>Lo que la ventana tiene que hacer despues de un clic en la barra.</summary>
public enum AccionBarra
{
    Ninguna,
    /// <summary>Fijar o liberar (el pin). El estado lo guarda quien llama en <see cref="ConfigApp.Bloqueado"/>.</summary>
    AlternarFijado,
    /// <summary>Abrir o cerrar el menu de configuracion (el estado nuevo esta en <see cref="BarraBotones.MenuAbierto"/>).</summary>
    AlternarMenu,
    /// <summary>Se eligio un modo en el popover (<see cref="BarraBotones.ModoElegido"/>).</summary>
    CambiarModo,
    /// <summary>Se confirmo la X: cerrar la aplicacion.</summary>
    CerrarApp,
}

/// <summary>Textos de la barra de botones (UI-SPEC 7.4). Se usan tal cual en la ventana y en las pruebas.</summary>
public static class TextosBarra
{
    public const string EtiquetaFijo = "FIJO", EtiquetaLibre = "LIBRE";
    public const string TooltipAjustes = "Ajustes";
    public const string TooltipCerrar = "Cerrar la aplicación";
    public const string PreguntaCerrar = "¿Cerrar la app?";
    public const string Cerrar = "CERRAR", Cancelar = "CANCELAR";
    public const string AvisoGrabando = "Estás grabando: se corta la grabación.";
    public const string ChipLibre = "LIBRE · arrastrá para mover · rueda: tamaño";
    public const string AjustesOculto = "Ajustes (el overlay está oculto)";

    public static string NombreModo(ModoApp m) => m switch
    {
        ModoApp.Grabar => "Grabar todo",
        ModoApp.GrabarSoloVos => "Grabar solo vos",
        _ => "Mostrar",
    };

    /// <summary>Ayuda de una linea de cada modo (popover y menu).</summary>
    public static string AyudaModo(ModoApp m) => m switch
    {
        ModoApp.Grabar => "Graba a la IA y a vos.",
        ModoApp.GrabarSoloVos => "Solo se graba tu auto: la IA guardada no se toca.",
        _ => "Solo muestra la guía.",
    };

    public static string TooltipModo(ModoApp actual) => $"Modo: {NombreModo(actual)} · clic para cambiar";

    public static string TooltipPin(bool fijado, string atajo) => fijado
        ? $"Fijado: no se puede mover ni cambiar de tamaño. Clic para liberar ({atajo})"
        : $"Libre: arrastrá para mover, rueda para el tamaño. Clic para fijar ({atajo})";
}

/// <summary>
/// Modelo de la barra de botones de la fase 6: modo, ajustes, fijar y cerrar (UI-SPEC 7.4). Logica pura, sin WPF ni reloj:
/// el tiempo (segundos) llega por parametro. Reglas:
///  - El engranaje, el pin, el modo y la X funcionan SIEMPRE, tambien con el overlay fijado: "fijado" solo es no mover ni
///    cambiar el tamano.
///  - El modo abre un popover de tres opciones (nunca cicla); abrir uno cierra la confirmacion de cerrar y viceversa.
///  - La X pide confirmacion en el lugar: CERRAR a la izquierda y CANCELAR donde estaba la X. Se cancela sola a los
///    <see cref="CancelarCierreS"/> s. Si se esta grabando, la pregunta lo avisa.
///  - Con el overlay oculto solo queda la pastilla del engranaje (<see cref="Oculto"/>).
/// </summary>
public sealed class BarraBotones
{
    /// <summary>La confirmacion de cierre se cancela sola a los 5 s.</summary>
    public const double CancelarCierreS = 5.0;

    private double _tConfirmacion;

    public bool Fijado { get; set; } = true;
    public ModoApp Modo { get; set; } = ModoApp.Mostrar;
    /// <summary>Verdadero en cualquiera de los dos modos de grabacion.</summary>
    public bool Grabando => Modo is ModoApp.Grabar or ModoApp.GrabarSoloVos;
    /// <summary>Atajo configurado, tal cual (por ejemplo "Ctrl+Alt+L").</summary>
    public string Atajo { get; set; } = AtajoTeclado.PorDefecto.Texto;
    /// <summary>El overlay esta oculto: queda solo la pastilla de 32 x 32 con el engranaje.</summary>
    public bool Oculto { get; set; }

    public bool MenuAbierto { get; private set; }
    public bool PopoverModoAbierto { get; private set; }
    public bool ConfirmandoCierre { get; private set; }
    /// <summary>Modo elegido en el popover; valido tras <see cref="AccionBarra.CambiarModo"/>.</summary>
    public ModoApp ModoElegido { get; private set; }

    // ---- lo que se dibuja

    public string EtiquetaPin => Fijado ? TextosBarra.EtiquetaFijo : TextosBarra.EtiquetaLibre;
    public string TooltipPin => TextosBarra.TooltipPin(Fijado, Atajo);
    public string TooltipModo => TextosBarra.TooltipModo(Modo);
    public string TooltipAjustes => Oculto ? TextosBarra.AjustesOculto : TextosBarra.TooltipAjustes;
    public string TooltipCerrar => TextosBarra.TooltipCerrar;
    /// <summary>Chincheta vertical y rellena cuando esta fijado; inclinada 45 grados y solo contorno cuando esta libre.</summary>
    public bool PinRelleno => Fijado;
    public double PinAnguloGrados => Fijado ? 0 : 45;
    /// <summary>Mientras esta libre se muestra el chip de una linea arriba (reemplaza al cartel grande de la 5b).</summary>
    public bool MostrarChipLibre => !Fijado && !Oculto;
    public string TextoChipLibre => TextosBarra.ChipLibre;
    /// <summary>Botones visibles, de izquierda a derecha. Oculto: solo el engranaje.</summary>
    public IReadOnlyList<BotonBarra> BotonesVisibles => Oculto
        ? [BotonBarra.Ajustes]
        : [BotonBarra.Modo, BotonBarra.Ajustes, BotonBarra.Fijar, BotonBarra.Cerrar];
    /// <summary>"Estas grabando: ..." solo durante la confirmacion y solo si se graba.</summary>
    public string? AvisoCierre => ConfirmandoCierre && Grabando ? TextosBarra.AvisoGrabando : null;
    /// <summary>Opciones del popover en orden, con la actual marcada.</summary>
    public IReadOnlyList<(ModoApp Modo, string Nombre, string Ayuda, bool Actual)> OpcionesModo =>
        [.. new[] { ModoApp.Grabar, ModoApp.GrabarSoloVos, ModoApp.Mostrar }
            .Select(m => (m, TextosBarra.NombreModo(m), TextosBarra.AyudaModo(m), m == Modo))];

    // ---- clics

    public AccionBarra Pulsar(BotonBarra boton, double t)
    {
        if (Oculto && boton != BotonBarra.Ajustes) return AccionBarra.Ninguna;
        switch (boton)
        {
            case BotonBarra.Ajustes:
                PopoverModoAbierto = false; ConfirmandoCierre = false;
                MenuAbierto = !MenuAbierto;
                return AccionBarra.AlternarMenu;
            case BotonBarra.Fijar:
                PopoverModoAbierto = false; ConfirmandoCierre = false;
                Fijado = !Fijado;
                return AccionBarra.AlternarFijado;
            case BotonBarra.Modo:
                ConfirmandoCierre = false;
                PopoverModoAbierto = !PopoverModoAbierto;
                return AccionBarra.Ninguna;
            case BotonBarra.Cerrar:
                // la X abre la pregunta; el boton que queda en su lugar (CANCELAR) la cierra: un doble clic accidental cancela
                if (ConfirmandoCierre) { ConfirmandoCierre = false; return AccionBarra.Ninguna; }
                PopoverModoAbierto = false;
                ConfirmandoCierre = true; _tConfirmacion = t;
                return AccionBarra.Ninguna;
        }
        return AccionBarra.Ninguna;
    }

    /// <summary>Opcion del popover de modo. Cierra el popover y devuelve <see cref="AccionBarra.CambiarModo"/>.</summary>
    public AccionBarra ElegirModo(ModoApp modo)
    {
        PopoverModoAbierto = false;
        if (!Enum.IsDefined(modo)) return AccionBarra.Ninguna;
        ModoElegido = modo; Modo = modo;
        return AccionBarra.CambiarModo;
    }

    /// <summary>Boton CERRAR de la pregunta. Solo actua si se esta confirmando.</summary>
    public AccionBarra ConfirmarCierre()
    {
        if (!ConfirmandoCierre) return AccionBarra.Ninguna;
        ConfirmandoCierre = false;
        return AccionBarra.CerrarApp;
    }

    public void CancelarCierre() => ConfirmandoCierre = false;
    public void CerrarPopover() => PopoverModoAbierto = false;

    /// <summary>Lo llama la ventana al abrir o cerrar el menu por otros caminos (X del menu, autocierre, ocultar overlay).</summary>
    public void FijarMenuAbierto(bool abierto) => MenuAbierto = abierto;

    /// <summary>Autocancelacion de la confirmacion de cierre. Devuelve verdadero si cambio algo (hay que repintar).</summary>
    public bool Actualizar(double t)
    {
        if (ConfirmandoCierre && t - _tConfirmacion >= CancelarCierreS) { ConfirmandoCierre = false; return true; }
        return false;
    }

    /// <summary>Fraccion que queda de la barra fina de la pregunta (1 al abrir, 0 a los 5 s); 0 si no se confirma.</summary>
    public double FraccionRestante(double t)
        => ConfirmandoCierre ? Math.Clamp(1 - (t - _tConfirmacion) / CancelarCierreS, 0, 1) : 0;

    /// <summary>Ancho de la ventana de botones: 140 normalmente, 292 durante la pregunta, 32 con el overlay oculto.</summary>
    public double AnchoVentana => Oculto ? MedidasUi.BotonClicable : ConfirmandoCierre ? MedidasUi.BarraAncha : MedidasUi.BarraAncho;
}
