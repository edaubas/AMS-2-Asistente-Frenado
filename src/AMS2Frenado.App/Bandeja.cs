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
/// Los comandos se publican como eventos; el estado marcado se refresca con <see cref="Actualizar"/>.
/// Compila, SIN EJECUTAR: nunca se vio el menu en Windows.
/// </summary>
internal sealed class Bandeja : IDisposable
{
    private readonly Forms.NotifyIcon _icono;
    private readonly Forms.ContextMenuStrip _menu = new();
    private Dibujo.Icon? _iconoActual;

    private Forms.ToolStripMenuItem _bloquear = null!, _mostrar = null!, _pitido = null!, _rojoSuave = null!, _mapaVisible = null!, _mapaCompacto = null!;
    private readonly Dictionary<ModoApp, Forms.ToolStripMenuItem> _modos = [];
    private readonly Dictionary<ReferenciaGuia, Forms.ToolStripMenuItem> _refs = [];
    private readonly Dictionary<Esquina, Forms.ToolStripMenuItem> _esquinas = [];
    private readonly Dictionary<double, Forms.ToolStripMenuItem> _escalas = [];
    private readonly Dictionary<double, Forms.ToolStripMenuItem> _opacidades = [];
    private readonly Dictionary<string, Forms.ToolStripMenuItem> _atajos = [];

    public static readonly string[] AtajosSugeridos = ["Ctrl+Alt+L", "Ctrl+Alt+O", "Ctrl+Shift+F9", "Ctrl+Alt+F10"];

    public event Action? AlternarBloqueo, AlternarVisible, AlternarPitido, AlternarRojoSuave, AlternarMapa, AlternarMapaCompacto;
    public event Action? AbrirCarpeta, AbrirDiagnostico, Salir;
    public event Action<ModoApp>? ModoElegido;
    public event Action<ReferenciaGuia>? ReferenciaElegida;
    public event Action<Esquina>? EsquinaElegida;
    public event Action<double>? EscalaElegida;
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
        foreach (var m in new[] { ModoApp.Mostrar, ModoApp.Grabar })
        {
            var it = Item(m == ModoApp.Mostrar ? "Mostrar (usar lo guardado)" : "Grabar (aprender la pista)", () => ModoElegido?.Invoke(m));
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

        var pos = new Forms.ToolStripMenuItem("Posición");
        foreach (var e in new[] { Esquina.ArribaDerecha, Esquina.ArribaIzquierda, Esquina.AbajoDerecha, Esquina.AbajoIzquierda })
        {
            var it = Item(Esquinas.Nombre(e), () => EsquinaElegida?.Invoke(e));
            _esquinas[e] = it; pos.DropDownItems.Add(it);
        }
        _menu.Items.Add(pos);

        var tam = new Forms.ToolStripMenuItem("Tamaño");
        foreach (var s in new[] { 0.75, 1.0, 1.25, 1.5 })
        {
            var it = Item($"{s * 100:0} %", () => EscalaElegida?.Invoke(s));
            _escalas[s] = it; tam.DropDownItems.Add(it);
        }
        _menu.Items.Add(tam);

        var opa = new Forms.ToolStripMenuItem("Opacidad del fondo");
        foreach (var a in new[] { 0.80, 0.88, 0.95 })
        {
            var it = Item($"{a * 100:0} %", () => OpacidadElegida?.Invoke(a));
            _opacidades[a] = it; opa.DropDownItems.Add(it);
        }
        _menu.Items.Add(opa);

        _mapaVisible = Item("Mostrar mapa", () => AlternarMapa?.Invoke());
        _mapaCompacto = Item("Mapa compacto", () => AlternarMapaCompacto?.Invoke());
        _rojoSuave = Item("Rojo suave en FRENÁ", () => AlternarRojoSuave?.Invoke());
        _pitido = Item("Pitido al frenar", () => AlternarPitido?.Invoke());
        _menu.Items.AddRange([_mapaVisible, _mapaCompacto, _rojoSuave, _pitido]);

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
        _menu.Items.Add(Item("Abrir carpeta de datos", () => AbrirCarpeta?.Invoke()));
        _menu.Items.Add(Item("Diagnóstico…", () => AbrirDiagnostico?.Invoke()));
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
        foreach (var (e, it) in _esquinas) it.Checked = cfg.Esquina == e;
        foreach (var (s, it) in _escalas) it.Checked = Math.Abs(cfg.Escala - s) < 0.01;
        foreach (var (a, it) in _opacidades) it.Checked = Math.Abs(cfg.Opacidad - a) < 0.005;
        foreach (var (t, it) in _atajos) it.Checked = string.Equals(cfg.Atajo, t, StringComparison.OrdinalIgnoreCase);
        _mapaVisible.Checked = cfg.MapaVisible;
        _mapaCompacto.Checked = cfg.MapaCompacto;
        _rojoSuave.Checked = cfg.RojoSuave;
        _pitido.Checked = cfg.Pitido;
        PonerIcono(cfg.Bloqueado);
        _icono.Text = $"AMS2 Asistente de Frenado · {(cfg.Modo == ModoApp.Grabar ? "grabando" : "mostrando")}";
    }

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

    /// <summary>Icono dibujado en tiempo de ejecucion (candado cerrado = bloqueado, abierto = desbloqueado).</summary>
    private void PonerIcono(bool bloqueado)
    {
        using var bmp = new Dibujo.Bitmap(32, 32);
        using (var g = Dibujo.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Dibujo.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Dibujo.Color.Transparent);
            using var fondo = new Dibujo.SolidBrush(Dibujo.Color.FromArgb(255, 12, 14, 17));
            g.FillEllipse(fondo, 1, 1, 30, 30);
            using var trazo = new Dibujo.Pen(Dibujo.Color.White, 3f);
            using var naranja = new Dibujo.SolidBrush(Dibujo.Color.FromArgb(255, 255, 154, 61));
            // cuerpo del candado
            g.FillRectangle(naranja, 9, 15, 14, 10);
            // arco: cerrado = centrado; abierto = levantado hacia un costado
            if (bloqueado) g.DrawArc(trazo, 11, 6, 10, 12, 180, 180);
            else g.DrawArc(trazo, 15, 4, 10, 12, 180, 180);
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
    }
}
