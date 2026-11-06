using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

public sealed class RegistroDiagnosticoTests : IDisposable
{
    private readonly string _dir = Simulador.CarpetaTemporal();
    private string Ruta => Path.Combine(_dir, "registro.log");
    private static readonly DateTime T0 = new(2026, 11, 6, 11, 35, 51, 123);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private RegistroDiagnostico Nuevo(long max = RegistroDiagnostico.TamanoMaximoPorDefecto) => new(Ruta, max, () => T0);

    [Fact]
    public void ApagadoPorDefectoNoCreaNingunArchivo()
    {
        var r = Nuevo();
        Assert.False(r.Activo);
        r.Escribir("x", "hola");
        r.Error("x", new InvalidOperationException("no"));
        Assert.False(r.Tick(5000));
        Assert.False(r.Cambio("lectura", "estado", "Ok"));
        Assert.False(File.Exists(Ruta));
    }

    [Fact]
    public void LineaTieneMarcaDeTiempoCategoriaYMensaje()
    {
        Assert.Equal("2026-11-06 11:35:51.123 [ventana] grande: visible", FormatoRegistro.Linea(T0, "ventana", "grande: visible"));
    }

    [Fact]
    public void LineaSinSaltosYRecortada()
    {
        string l = FormatoRegistro.Linea(T0, "e", "a\r\nb\tc" + new string('z', 1000));
        Assert.DoesNotContain('\n', l);
        Assert.DoesNotContain('\t', l);
        Assert.Equal(FormatoRegistro.MaxLinea, l.Length);
        Assert.EndsWith("…", l);
    }

    [Fact]
    public void NoGuardaLaCarpetaDelUsuario()
    {
        string perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (perfil.Length <= 3) return;       // entorno sin perfil: nada que probar
        string l = FormatoRegistro.Linea(T0, "error", $"no se pudo abrir {Path.Combine(perfil, "AppData", "config.json")}");
        Assert.DoesNotContain(perfil, l, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("~", l);
    }

    [Fact]
    public void ActivarYApagarDejanMarcasYEscribeSoloMientrasEstaActivo()
    {
        var r = Nuevo();
        r.Activo = true;
        r.Escribir("arranque", "ok");
        r.Activo = false;
        r.Escribir("arranque", "ignorada");
        var lineas = File.ReadAllLines(Ruta);
        Assert.Equal(3, lineas.Length);
        Assert.EndsWith("[registro] activado", lineas[0]);
        Assert.EndsWith("[arranque] ok", lineas[1]);
        Assert.EndsWith("[registro] desactivado", lineas[2]);
    }

    [Fact]
    public void TickSoloSeAnotaSiPasaElUmbral()
    {
        var r = Nuevo(); r.Activo = true;
        Assert.False(r.Tick(99.9));
        Assert.True(r.Tick(100));
        Assert.True(r.Tick(1234.4));
        var lineas = File.ReadAllLines(Ruta);
        Assert.Contains(lineas, l => l.EndsWith("[tick-lento] 100 ms"));
        Assert.Contains(lineas, l => l.EndsWith("[tick-lento] 1234 ms"));
        Assert.DoesNotContain(lineas, l => l.Contains("99"));
    }

    [Fact]
    public void CambioNoRepiteElMismoValor()
    {
        var r = Nuevo(); r.Activo = true;
        Assert.True(r.Cambio("lectura", "estado", "SinJuego"));
        Assert.False(r.Cambio("lectura", "estado", "SinJuego"));
        Assert.True(r.Cambio("lectura", "estado", "Ok"));
        Assert.True(r.Cambio("lectura", "estado", "SinJuego"));      // volver a un valor anterior si cuenta
        Assert.Equal(3, File.ReadAllLines(Ruta).Count(l => l.Contains("[lectura] estado:")));
    }

    [Fact]
    public void RotaAlLlegarAlMaximoYNoCreceSinLimite()
    {
        const long max = 2000;
        var r = Nuevo(max); r.Activo = true;
        for (int i = 0; i < 500; i++) r.Escribir("relleno", $"linea {i} " + new string('x', 40));
        Assert.True(File.Exists(Ruta + ".1"));
        Assert.True(new FileInfo(Ruta).Length <= max);
        Assert.True(new FileInfo(Ruta + ".1").Length <= max);
        Assert.Contains("linea 499", File.ReadAllText(Ruta));     // lo ultimo queda en el actual
        Assert.False(File.Exists(Ruta + ".2"));
    }

    [Fact]
    public void ContinuaElArchivoExistenteAlReiniciar()
    {
        var a = Nuevo(); a.Activo = true; a.Escribir("a", "uno");
        var b = Nuevo(); b.Activo = true; b.Escribir("b", "dos");
        var texto = File.ReadAllText(Ruta);
        Assert.Contains("[a] uno", texto);
        Assert.Contains("[b] dos", texto);
    }

    [Fact]
    public void NuncaLanzaSiNoSePuedeEscribir()
    {
        string archivo = Path.Combine(_dir, "ocupado");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(archivo, "no soy una carpeta");
        var r = new RegistroDiagnostico(Path.Combine(archivo, "registro.log"), reloj: () => T0);
        r.Activo = true;
        r.Escribir("x", "y");
        r.Error("x", new IOException("boom"));
        Assert.True(r.Tick(500));       // sigue funcionando
    }

    [Fact]
    public void ErrorAnotaTipoYMensajeSinPila()
    {
        var r = Nuevo(); r.Activo = true;
        try { throw new InvalidOperationException("fallo raro"); }
        catch (Exception e) { r.Error("tick", e); }
        var l = File.ReadAllLines(Ruta).Last();
        Assert.EndsWith("[error] tick: InvalidOperationException: fallo raro", l);
        Assert.DoesNotContain(" at ", l);
    }
}
