using System.Globalization;

namespace AMS2Frenado.Core.Overlay;

public enum PaginaMenu { Principal, Datos, Atajo }

/// <summary>
/// Un deslizador del menu: rango, paso y como se lee y se escribe en <see cref="ConfigApp"/>. Los valores estan en las unidades
/// que se ven ("100" para 100 %, "0,8" para 0,8 s), no en las de la configuracion.
/// </summary>
public sealed record DeslizadorDef(
    string Id, string Etiqueta, double Min, double Max, double Paso, string Unidad, int Decimales,
    Func<ConfigApp, double> Leer, Action<ConfigApp, double> Escribir, string? Ayuda = null)
{
    /// <summary>Lleva el valor al paso mas cercano dentro del rango (lo que hace IsSnapToTickEnabled en la ventana).</summary>
    public double Ajustar(double v)
    {
        if (double.IsNaN(v)) v = Min;
        double pasos = Math.Round((Math.Clamp(v, Min, Max) - Min) / Paso, MidpointRounding.AwayFromZero);
        return Math.Round(Math.Clamp(Min + pasos * Paso, Min, Max), Decimales + 1);
    }

    /// <summary>"0,8 s" / "100 %": coma decimal, como el resto de la interfaz.</summary>
    public string Formatear(double v)
    {
        string n = Ajustar(v).ToString("F" + Decimales, CultureInfo.InvariantCulture).Replace('.', ',');
        return Unidad == "%" ? $"{n} %" : $"{n} {Unidad}";
    }

    /// <summary>Aplica el valor (ajustado al paso) a la configuracion y la normaliza. Devuelve el valor efectivo.</summary>
    public double Aplicar(ConfigApp cfg, double v)
    {
        Escribir(cfg, Ajustar(v));
        cfg.Normalizar();
        return Leer(cfg);
    }

    public string LeerTexto(ConfigApp cfg) => Formatear(Leer(cfg));
}

/// <summary>
/// Modelo del menu de configuracion de la fase 6 (UI-SPEC 7.5): que deslizadores hay, sus rangos, las paginas internas
/// ("Datos", "Atajo"), el autocierre a los 15 s con el mouse afuera y el guardado diferido. Logica pura: el tiempo llega por
/// parametro. Las opciones son las mismas de la bandeja, que queda como respaldo.
/// </summary>
public sealed class MenuConfig
{
    /// <summary>Se cierra solo tras 15 s con el mouse fuera del menu (P25).</summary>
    public const double AutocierreS = 15.0;
    /// <summary>El config.json se guarda 500 ms despues del ultimo cambio de un deslizador.</summary>
    public const double GuardadoDiferidoS = 0.5;

    public static readonly string[] AtajosSugeridos = ["Ctrl+Alt+L", "Ctrl+Alt+O", "Ctrl+Shift+F9", "Ctrl+Alt+F10"];
    public const string AtajoOtro = "Otro: editá \"atajo\" en config.json";

    /// <summary>Textos de los deslizadores del aviso que se repiten en la bandeja (una sola fuente).</summary>
    public static class Etiquetas
    {
        /// <summary>Vale para el pitido Y para el simbolo (correcciones-ui-1: "del aviso" se leia como solo el simbolo).</summary>
        public const string Anticipacion = "Anticipación (pitido y símbolo)";
        /// <summary>Antes "Inicio de la rampa": la palabra "rampa" no se entendia. Internamente sigue siendo avisoRampaS.</summary>
        public const string Aparicion = "Aparición del símbolo";
    }

    /// <summary>Tamano del overlay: 75 a 150 %, paso 5. Sigue activo con el overlay fijado.</summary>
    public static readonly DeslizadorDef Tamano = new("tamano", "Tamaño", 75, 150, 5, "%", 0,
        c => Math.Round(c.Escala * 100), (c, v) => c.Escala = v / 100);
    /// <summary>Opacidad del fondo: 85 a 95 %, paso 1 (el alfa real; el texto no se atenua).</summary>
    public static readonly DeslizadorDef Opacidad = new("opacidad", "Opacidad del fondo", 85, 95, 1, "%", 0,
        c => Math.Round(c.Opacidad * 100), (c, v) => c.Opacidad = v / 100);
    public static readonly DeslizadorDef TamanoAviso = new("tamanoAviso", "Tamaño del aviso", 50, 300, 10, "%", 0,
        c => Math.Round(c.AvisoEscala * 100), (c, v) => c.AvisoEscala = v / 100);
    public static readonly DeslizadorDef Anticipacion = new("anticipacion", Etiquetas.Anticipacion, DecisorAviso.AnticipacionMinS,
        DecisorAviso.AnticipacionMaxS, DecisorAviso.AnticipacionPasoS, "s", 1,
        c => c.AvisoAnticipacionS, (c, v) => c.AvisoAnticipacionS = v,
        "Segundos antes del punto de frenado en que suena el pitido y el símbolo llega al 100 %. 0 = justo en el punto.");
    public static readonly DeslizadorDef Duracion = new("duracion", "Duración máxima", AvisoFrenada.DuracionMinimaS, AvisoFrenada.DuracionMaximaS, 0.1, "s", 1,
        c => c.AvisoDuracionS, (c, v) => c.AvisoDuracionS = v);
    /// <summary>
    /// Aparicion del simbolo: cuantos segundos antes empieza a verse, de a poco (<c>avisoRampaS</c>, de la fase 5d). El spec de la fase 6 no la
    /// dibuja (propone 2,0 s fijos); se conserva configurable (1,0 a 3,0 s, paso 0,5, por defecto 2,0). Ya no se rotula "avanzado".
    /// </summary>
    public static readonly DeslizadorDef Rampa = new("rampa", Etiquetas.Aparicion, DecisorAviso.RampaMinS, DecisorAviso.RampaMaxS, 0.5, "s", 1,
        c => c.AvisoRampaS, (c, v) => c.AvisoRampaS = v,
        "Cuántos segundos antes empieza a verse el símbolo, de a poco. Por defecto 2,0 s.");

    public static readonly IReadOnlyList<DeslizadorDef> Deslizadores = [Tamano, Opacidad, TamanoAviso, Anticipacion, Duracion, Rampa];

    public static DeslizadorDef? PorId(string id) => Deslizadores.FirstOrDefault(d => d.Id == id);

    private double _tAbierto, _tAdentro, _tCambio = double.NegativeInfinity;
    private bool _pendienteGuardar;

    public bool Abierto { get; private set; }
    public PaginaMenu Pagina { get; private set; } = PaginaMenu.Principal;

    /// <summary>Abre el menu en la pagina principal. El instante de apertura cuenta como "mouse adentro".</summary>
    public void Abrir(double t) { Abierto = true; Pagina = PaginaMenu.Principal; _tAbierto = _tAdentro = t; }
    public void Cerrar() { Abierto = false; Pagina = PaginaMenu.Principal; }

    /// <summary>Entra a "Datos" o "Atajo de bloqueo" dentro del mismo menu (no hay submenus flotantes).</summary>
    public void IrA(PaginaMenu p) { if (Abierto) Pagina = p; }
    /// <summary>La flecha "‹": vuelve a la pagina principal.</summary>
    public void Volver() => Pagina = PaginaMenu.Principal;
    public string TituloPagina => Pagina switch { PaginaMenu.Datos => "DATOS", PaginaMenu.Atajo => "ATAJO DE BLOQUEO", _ => "AJUSTES" };

    /// <summary>
    /// Lo llama la ventana en cada tick con si el mouse esta sobre el menu. Devuelve verdadero si el menu se cerro por
    /// haber pasado 15 s con el mouse afuera.
    /// </summary>
    public bool Actualizar(double t, bool mouseAdentro)
    {
        if (!Abierto) return false;
        if (mouseAdentro) { _tAdentro = t; return false; }
        if (t - _tAdentro < AutocierreS) return false;
        Cerrar();
        return true;
    }

    /// <summary>Segundos que faltan para el autocierre (solo informativo; 15 con el mouse adentro).</summary>
    public double RestanteAutocierreS(double t) => Abierto ? Math.Clamp(AutocierreS - (t - _tAdentro), 0, AutocierreS) : 0;

    // ---- guardado diferido de config.json

    /// <summary>Se movio un deslizador: el guardado se pospone 500 ms desde este cambio.</summary>
    public void MarcarCambio(double t) { _tCambio = t; _pendienteGuardar = true; }

    /// <summary>Verdadero una sola vez, cuando pasaron 500 ms desde el ultimo cambio. Al cerrar el menu usar <see cref="DebeGuardarYa"/>.</summary>
    public bool DebeGuardar(double t)
    {
        if (!_pendienteGuardar || t - _tCambio < GuardadoDiferidoS) return false;
        _pendienteGuardar = false;
        return true;
    }

    /// <summary>Hay un cambio sin guardar: al cerrar el menu o salir se guarda sin esperar.</summary>
    public bool DebeGuardarYa()
    {
        if (!_pendienteGuardar) return false;
        _pendienteGuardar = false;
        return true;
    }
}
