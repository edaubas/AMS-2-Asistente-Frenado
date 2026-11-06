using System.Diagnostics;
using System.IO;
using AMS2Frenado.App.Interop;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Dibujo = System.Drawing;
using Forms = System.Windows.Forms;

namespace AMS2Frenado.App;

/// <summary>
/// Icono de la bandeja y su menu. Se usa <c>System.Windows.Forms.NotifyIcon</c> (UseWindowsForms) en lugar de
/// P/Invoke a Shell_NotifyIcon: es la forma corta y conocida en WPF. Solo se usa para esto.
/// Desde la fase 6 es el RESPALDO: las mismas opciones estan en el engranaje del overlay (VentanaMenu).
/// Los comandos se publican como eventos; el estado marcado se refresca con <see cref="Actualizar"/>.
/// Compila, SIN EJECUTAR: nunca se vio el menu en Windows.
/// </summary>
internal sealed class Bandeja : IDisposable
{
    private readonly Forms.NotifyIcon _icono;
    private readonly Forms.ContextMenuStrip _menu = new();
    private Dibujo.Icon? _iconoActual;

    private Forms.ToolStripMenuItem _bloquear = null!, _mostrar = null!, _pitido = null!, _aviso = null!, _registro = null!;
    private readonly Dictionary<ModoApp, Forms.ToolStripMenuItem> _modos = [];
    private readonly Dictionary<ReferenciaGuia, Forms.ToolStripMenuItem> _refs = [];
    private readonly Dictionary<double, Forms.ToolStripMenuItem> _escalasAviso = [];
    private readonly Dictionary<double, Forms.ToolStripMenuItem> _duracionesAviso = [];
    private readonly Dictionary<double, Forms.ToolStripMenuItem> _rampasAviso = [];
    private readonly Dictionary<double, Forms.ToolStripMenuItem> _anticipaciones = [];
    private readonly Dictionary<double, Forms.ToolStripMenuItem> _escalas = [];
    private readonly Dictionary<double, Forms.ToolStripMenuItem> _opacidades = [];
    private readonly Dictionary<string, Forms.ToolStripMenuItem> _atajos = [];

    public static readonly string[] AtajosSugeridos = MenuConfig.AtajosSugeridos;

    public event Action? AlternarBloqueo, AlternarVisible, AlternarPitido, AlternarAviso, ProbarAviso, ProbarPitido;
    public event Action? AbrirCarpeta, AbrirCarpetaCapturas, ExportarDatos, ImportarDatos, AbrirDiagnostico, AlternarRegistro, Salir;
    public event Action<ModoApp>? ModoElegido;
    public event Action<ReferenciaGuia>? ReferenciaElegida;
    public event Action<double>? EscalaElegida, EscalaAvisoElegida, DuracionAvisoElegida, RampaAvisoElegida, AnticipacionElegida;
    public event Action<double>? OpacidadElegida;
    public event Action<string>? AtajoElegido;

    public Bandeja()
    {
        _icono = new Forms.NotifyIcon { Text = "AMS2 Asistente de Frenado", ContextMenuStrip = _menu, Visible = true };
        ConstruirMenu();
        PonerIcono(bloqueado: true);
    }

    private Forms.ToolStripMenuItem Item(string texto, Action? accion)
    {
        var i = new Forms.ToolStripMenuItem(texto) { CheckOnClick = false };
        if (accion is not null) i.Click += (_, _) => accion();
        return i;
    }

    private void ConstruirMenu()
    {
        _bloquear = Item("Bloquear overlay", () => AlternarBloqueo?.Invoke());
        _menu.Items.Add(_bloquear);

        var modo = new Forms.ToolStripMenuItem("Modo");
        foreach (var m in new[] { ModoApp.Grabar, ModoApp.GrabarSoloVos, ModoApp.Mostrar })
        {
            string texto = m switch
            {
                ModoApp.Grabar => "Grabar todo (IA + vos)",
                ModoApp.GrabarSoloVos => "Grabar solo vos",
                _ => "Mostrar",
            };
            var it = Item(texto, () => ModoElegido?.Invoke(m));
            _modos[m] = it; modo.DropDownItems.Add(it);
        }
        _menu.Items.Add(modo);

        var refp = new Forms.ToolStripMenuItem("Referencia principal");
        foreach (var r in new[] { ReferenciaGuia.Ia, ReferenciaGuia.Vos })
        {
            var it = Item(r == ReferenciaGuia.Ia ? "◆ IA" : "● Vos", () => ReferenciaElegida?.Invoke(r));
            _refs[r] = it; refp.DropDownItems.Add(it);
        }
        _menu.Items.Add(refp);

        var tam = new Forms.ToolStripMenuItem("Tamaño del overlay");
        foreach (var s in new[] { 0.75, 1.0, 1.25, 1.5 })
        {
            var it = Item($"{s * 100:0} %", () => EscalaElegida?.Invoke(s));
            _escalas[s] = it; tam.DropDownItems.Add(it);
        }
        _menu.Items.Add(tam);

        var opa = new Forms.ToolStripMenuItem("Opacidad del fondo");
        foreach (var a in new[] { 0.85, 0.88, 0.95 })     // el minimo subio de 0,80 a 0,85 en la fase 6
        {
            var it = Item($"{a * 100:0} %", () => OpacidadElegida?.Invoke(a));
            _opacidades[a] = it; opa.DropDownItems.Add(it);
        }
        _menu.Items.Add(opa);

        _menu.Items.Add(new Forms.ToolStripSeparator());

        // aviso de frenada: simbolo de freno en una ventana aparte
        _aviso = Item("Aviso de frenada (símbolo)", () => AlternarAviso?.Invoke());
        _menu.Items.Add(_aviso);
        _menu.Items.Add(Item("Probar aviso", () => ProbarAviso?.Invoke()));
        var tamAviso = new Forms.ToolStripMenuItem("Tamaño del aviso");
        foreach (var s in new[] { 0.5, 0.75, 1.0, 1.5, 2.0, 3.0 })
        {
            var it = Item($"{s * 100:0} %", () => EscalaAvisoElegida?.Invoke(s));
            _escalasAviso[s] = it; tamAviso.DropDownItems.Add(it);
        }
        _menu.Items.Add(tamAviso);
        // submenu provisorio hasta que exista el deslizador del menu de configuracion (0 a 2,0 s, paso 0,1 s)
        var antic = new Forms.ToolStripMenuItem(MenuConfig.Etiquetas.Anticipacion);
        foreach (var d in DecisorAviso.AnticipacionesSugeridasS)
        {
            var it = Item(d == 0 ? "0 s (en el punto de frenado)" : $"{d:0.0} s antes del punto".Replace('.', ','), () => AnticipacionElegida?.Invoke(d));
            _anticipaciones[d] = it; antic.DropDownItems.Add(it);
        }
        _menu.Items.Add(antic);
        var rampaAviso = new Forms.ToolStripMenuItem(MenuConfig.Etiquetas.Aparicion);
        foreach (var d in new[] { 1.0, 1.5, 2.0, 2.5, 3.0 })
        {
            var it = Item($"{d:0.0} s antes", () => RampaAvisoElegida?.Invoke(d));
            _rampasAviso[d] = it; rampaAviso.DropDownItems.Add(it);
        }
        _menu.Items.Add(rampaAviso);
        var durAviso = new Forms.ToolStripMenuItem("Duración del aviso después del punto");
        foreach (var d in new[] { 1.0, 1.5, 2.0 })
        {
            var it = Item($"{d:0.0} s", () => DuracionAvisoElegida?.Invoke(d));
            _duracionesAviso[d] = it; durAviso.DropDownItems.Add(it);
        }
        _menu.Items.Add(durAviso);

        _pitido = Item("Pitido al frenar", () => AlternarPitido?.Invoke());
        _menu.Items.Add(_pitido);
        _menu.Items.Add(Item("Probar pitido", () => ProbarPitido?.Invoke()));
        _menu.Items.Add(new Forms.ToolStripSeparator());

        var atajo = new Forms.ToolStripMenuItem("Atajo de bloqueo");
        foreach (var a in AtajosSugeridos)
        {
            var it = Item(a, () => AtajoElegido?.Invoke(a));
            _atajos[a] = it; atajo.DropDownItems.Add(it);
        }
        atajo.DropDownItems.Add(new Forms.ToolStripSeparator());
        atajo.DropDownItems.Add(new Forms.ToolStripMenuItem("Otro: editar \"atajo\" en config.json") { Enabled = false });
        _menu.Items.Add(atajo);

        _menu.Items.Add(new Forms.ToolStripSeparator());
        _mostrar = Item("Ocultar overlay", () => AlternarVisible?.Invoke());
        _menu.Items.Add(_mostrar);
        var datos = new Forms.ToolStripMenuItem("Datos");
        datos.DropDownItems.Add(Item("Abrir carpeta de datos", () => AbrirCarpeta?.Invoke()));
        datos.DropDownItems.Add(Item("Abrir carpeta de capturas", () => AbrirCarpetaCapturas?.Invoke()));
        datos.DropDownItems.Add(new Forms.ToolStripSeparator());
        datos.DropDownItems.Add(Item("Exportar datos…", () => ExportarDatos?.Invoke()));
        datos.DropDownItems.Add(Item("Importar datos…", () => ImportarDatos?.Invoke()));
        _menu.Items.Add(datos);
        _menu.Items.Add(Item("Diagnóstico…", () => AbrirDiagnostico?.Invoke()));
        _registro = Item("Guardar registro de diagnóstico", () => AlternarRegistro?.Invoke());
        _registro.Checked = Diag.Registro.Activo;
        _menu.Items.Add(_registro);
        _menu.Items.Add(new Forms.ToolStripSeparator());
        _menu.Items.Add(Item("Salir", () => Salir?.Invoke()));
    }

    /// <summary>Refleja la configuracion en las marcas del menu y el icono (candado cerrado / abierto).</summary>
    public void Actualizar(ConfigApp cfg, AtajoTeclado? atajoActivo)
    {
        _bloquear.Checked = cfg.Bloqueado;
        _bloquear.Text = "Bloquear overlay" + (atajoActivo is null ? "" : $" ({atajoActivo.Texto})");
        _mostrar.Text = cfg.Visible ? "Ocultar overlay" : "Mostrar overlay";
        foreach (var (m, it) in _modos) it.Checked = cfg.Modo == m;
        foreach (var (r, it) in _refs) it.Checked = cfg.ReferenciaPrincipal == r;
        foreach (var (e, it) in _escalasAviso) it.Checked = Math.Abs(cfg.AvisoEscala - e) < 0.01;
        foreach (var (d, it) in _duracionesAviso) it.Checked = Math.Abs(cfg.AvisoDuracionS - d) < 0.01;
        foreach (var (d, it) in _anticipaciones) it.Checked = Math.Abs(cfg.AvisoAnticipacionS - d) < 0.05;
        foreach (var (d, it) in _rampasAviso) it.Checked = Math.Abs(cfg.AvisoRampaS - d) < 0.01;
        foreach (var (s, it) in _escalas) it.Checked = Math.Abs(cfg.Escala - s) < 0.01;
        foreach (var (a, it) in _opacidades) it.Checked = Math.Abs(cfg.Opacidad - a) < 0.005;
        foreach (var (t, it) in _atajos) it.Checked = string.Equals(cfg.Atajo, t, StringComparison.OrdinalIgnoreCase);
        _aviso.Checked = cfg.AvisoActivo;
        _pitido.Checked = cfg.Pitido;
        PonerIcono(cfg.Bloqueado);
        _icono.Text = $"AMS2 Asistente de Frenado · {(cfg.Modo switch { ModoApp.Grabar => "grabando", ModoApp.GrabarSoloVos => "grabando solo vos", _ => "mostrando" })}";
    }

    public void MarcarRegistro(bool activo) => _registro.Checked = activo;

    public void Globo(string titulo, string texto, bool error = false, int ms = 8000)
    {
        _icono.BalloonTipTitle = titulo;
        _icono.BalloonTipText = texto;
        _icono.BalloonTipIcon = error ? Forms.ToolTipIcon.Warning : Forms.ToolTipIcon.Info;
        _icono.ShowBalloonTip(ms);
    }

    public void AbrirEnExplorador(string carpeta)
    {
        try
        {
            Directory.CreateDirectory(carpeta);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{carpeta}\"") { UseShellExecute = true });
        }
        catch (Exception e) { Globo("No se pudo abrir la carpeta", e.Message, error: true); }
    }

    private Dibujo.Bitmap? _base;
    private bool _baseIntentada;

    /// <summary>El icono de la app (ficha roja con 2) a 32 px, tomado del .ico embebido. Null si no se pudo leer (se usa el dibujo de respaldo).</summary>
    private Dibujo.Bitmap? BaseIcono()
    {
        if (_baseIntentada) return _base;
        _baseIntentada = true;
        try
        {
            var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Recursos/icono.ico"));
            if (info is null) return null;
            using var s = info.Stream;
            using var ico = new Dibujo.Icon(s, new Dibujo.Size(32, 32));
            _base = ico.ToBitmap();
        }
        catch (Exception e) when (e is IOException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            _base = null;
        }
        return _base;
    }

    /// <summary>
    /// Icono de la bandeja: el icono de la app con un indicador chico abajo a la derecha que dice si el overlay esta fijado
    /// (candado cerrado) o libre (candado abierto). A 16 px (100 % de escala) el indicador se lee como un punto: el estado tambien
    /// esta en el tooltip del icono y en el pin del overlay. Si el .ico no carga, queda el dibujo de antes (candado en un disco).
    /// Dibujado en tiempo de ejecucion; compila, SIN EJECUTAR.
    /// </summary>
    private void PonerIcono(bool bloqueado)
    {
        using var bmp = new Dibujo.Bitmap(32, 32);
        using (var g = Dibujo.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Dibujo.Drawing2D.SmoothingMode.AntiAlias;
            g.InterpolationMode = Dibujo.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.Clear(Dibujo.Color.Transparent);
            var icono = BaseIcono();
            using var trazo = new Dibujo.Pen(Dibujo.Color.White, icono is null ? 3f : 2f);
            using var naranja = new Dibujo.SolidBrush(Dibujo.Color.FromArgb(255, 255, 154, 61));
            if (icono is null)
            {
                using var fondo = new Dibujo.SolidBrush(Dibujo.Color.FromArgb(255, 12, 14, 17));
                g.FillEllipse(fondo, 1, 1, 30, 30);
                g.FillRectangle(naranja, 9, 15, 14, 10);
                if (bloqueado) g.DrawArc(trazo, 11, 6, 10, 12, 180, 180);
                else g.DrawArc(trazo, 15, 4, 10, 12, 180, 180);
            }
            else
            {
                g.DrawImage(icono, 0, 0, 32, 32);
                // indicador: disco oscuro de 15 px con un candado minimo (cuerpo naranja + arco blanco: centrado = cerrado, levantado = abierto)
                using var disco = new Dibujo.SolidBrush(Dibujo.Color.FromArgb(255, 12, 14, 17));
                g.FillEllipse(disco, 16, 16, 16, 16);
                g.FillRectangle(naranja, 20, 24, 8, 6);
                if (bloqueado) g.DrawArc(trazo, 21.5f, 18.5f, 5, 7, 180, 180);
                else g.DrawArc(trazo, 24f, 17f, 5, 7, 180, 180);
            }
        }
        var h = bmp.GetHicon();
        var anterior = _iconoActual;
        using (var tmp = Dibujo.Icon.FromHandle(h)) _iconoActual = (Dibujo.Icon)tmp.Clone();
        Win32.DestroyIcon(h);
        _icono.Icon = _iconoActual;
        anterior?.Dispose();
    }

    public void Dispose()
    {
        _icono.Visible = false;
        _icono.Dispose();
        _menu.Dispose();
        _iconoActual?.Dispose();
        _base?.Dispose();
    }
}
