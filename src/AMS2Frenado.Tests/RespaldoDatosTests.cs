using System.IO.Compression;
using System.Text;
using AMS2Frenado.Core.Datos;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>Exportar / importar el respaldo de datos. Los cuadros de dialogo de la App compilan pero no se ejecutaron.</summary>
public sealed class RespaldoDatosTests : IDisposable
{
    private readonly string _raiz = Simulador.CarpetaTemporal();
    private string Datos => Path.Combine(_raiz, "datos");
    private string Zip(string n = "r.zip") => Path.Combine(_raiz, n);
    public RespaldoDatosTests() => Directory.CreateDirectory(_raiz);
    public void Dispose() { try { Directory.Delete(_raiz, true); } catch { } }

    private void Escribir(string rel, string txt, string? baseDir = null)
    {
        string f = Path.Combine(baseDir ?? Datos, rel.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        File.WriteAllText(f, txt, new UTF8Encoding(false));
    }

    private string Leer(string rel) => File.ReadAllText(Path.Combine(Datos, rel.Replace('/', Path.DirectorySeparatorChar)));

    private static void CrearZip(string ruta, params (string nombre, string txt)[] entradas)
    {
        using var fs = new FileStream(ruta, FileMode.Create);
        using var z = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var (n, t) in entradas)
        {
            var e = z.CreateEntry(n);
            using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
            w.Write(t);
        }
    }

    private static List<string> Nombres(string zip)
    {
        using var z = ZipFile.OpenRead(zip);
        return z.Entries.Select(e => e.FullName).Order().ToList();
    }

    private void Sembrar()
    {
        Escribir("config.json", "{\"modo\":\"mostrar\"}");
        Escribir("Watkins Glen/GP/mapa.json", "{\"formato\":1}");
        Escribir("Watkins Glen/GP/GT3_Gen2.json", "{\"formato\":1,\"curvas\":[]}");
        Escribir("Watkins Glen/GP/GT3_Gen2.json.bak", "{}");
        Escribir("Watkins Glen/GP/mapa.json.tmp", "{");
        Escribir("capturas/captura-1.ams2cap", "AMS2CAP1 binario");
        Escribir("capturas/otra/captura-2.ams2cap", "AMS2CAP1 binario");
    }

    // ---------------------------------------------------------------- exportar

    [Fact]
    public void ExportarIncluyeLosDatosYNoLasCapturasNiLosBakNiTmp()
    {
        Sembrar();
        int n = RespaldoDatos.Exportar(Datos, Zip());
        Assert.Equal(3, n);
        Assert.Equal(["config.json", "Watkins Glen/GP/GT3_Gen2.json", "Watkins Glen/GP/mapa.json"], Nombres(Zip()));
        Assert.False(File.Exists(Zip() + ".tmp"));
    }

    [Fact]
    public void ExportarSobreUnZipDentroDeLaCarpetaDeDatosNoSeIncluyeASiMismo()
    {
        Sembrar();
        string dentro = Path.Combine(Datos, "backup.zip");
        RespaldoDatos.Exportar(Datos, dentro);
        RespaldoDatos.Exportar(Datos, dentro);          // segunda vez: el zip viejo no entra en el nuevo
        Assert.DoesNotContain("backup.zip", Nombres(dentro));
    }

    [Fact]
    public void ExportarSinCarpetaDeDatosAvisa()
        => Assert.Throws<DirectoryNotFoundException>(() => RespaldoDatos.Exportar(Path.Combine(_raiz, "nada"), Zip()));

    [Fact]
    public void ExportarEImportarEnOtraPcDejaLosMismosArchivos()
    {
        Sembrar();
        RespaldoDatos.Exportar(Datos, Zip());
        string otra = Path.Combine(_raiz, "otra");
        var r = RespaldoDatos.Importar(Zip(), otra);
        Assert.Equal(3, r.Importados);
        Assert.Equal(0, r.Respaldados);
        Assert.True(r.ConfigImportada);
        Assert.Empty(r.Omitidos);
        Assert.Equal("{\"formato\":1,\"curvas\":[]}", File.ReadAllText(Path.Combine(otra, "Watkins Glen", "GP", "GT3_Gen2.json")));
        Assert.False(Directory.Exists(Path.Combine(otra, "capturas")));
    }

    // ---------------------------------------------------------------- importar

    [Fact]
    public void ImportarHaceBakDeLoExistenteYNoTocaCapturasNiConfigSiNoVienen()
    {
        Sembrar();
        CrearZip(Zip(), ("Watkins Glen/GP/mapa.json", "{\"formato\":2}"), ("Monza/GP/mapa.json", "{\"formato\":2}"));
        var r = RespaldoDatos.Importar(Zip(), Datos);
        Assert.Equal(2, r.Importados);
        Assert.Equal(1, r.Respaldados);
        Assert.False(r.ConfigImportada);
        Assert.Equal("{\"formato\":2}", Leer("Watkins Glen/GP/mapa.json"));
        Assert.Equal("{\"formato\":1}", Leer("Watkins Glen/GP/mapa.json.bak"));          // lo de antes quedo respaldado
        Assert.Equal("{\"formato\":2}", Leer("Monza/GP/mapa.json"));
        Assert.False(File.Exists(Path.Combine(Datos, "Monza", "GP", "mapa.json.bak")));    // nuevo: nada que respaldar
        Assert.Equal("{\"modo\":\"mostrar\"}", Leer("config.json"));                        // intacto
        Assert.Equal("AMS2CAP1 binario", Leer("capturas/captura-1.ams2cap"));              // intacto
        Assert.Equal("AMS2CAP1 binario", Leer("capturas/otra/captura-2.ams2cap"));
        Assert.Equal("{\"formato\":1,\"curvas\":[]}", Leer("Watkins Glen/GP/GT3_Gen2.json")); // lo que no vino sigue ahi
        Assert.DoesNotContain(Directory.GetFiles(Datos, "*.tmp", SearchOption.AllDirectories), f => !f.EndsWith("mapa.json.tmp"));
    }

    [Fact]
    public void ImportarConConfigAvisaYLaReemplazaConBak()
    {
        Sembrar();
        CrearZip(Zip(), ("config.json", "{\"modo\":\"grabar\"}"));
        var r = RespaldoDatos.Importar(Zip(), Datos);
        Assert.True(r.ConfigImportada);
        Assert.Equal("{\"modo\":\"grabar\"}", Leer("config.json"));
        Assert.Equal("{\"modo\":\"mostrar\"}", Leer("config.json.bak"));
    }

    [Fact]
    public void ImportarOmiteCapturasBakTmpYNoJsonPeroSigueConLoDemas()
    {
        CrearZip(Zip(), ("capturas/x.ams2cap", "no"), ("A/B/c.json.bak", "{}"), ("A/B/d.exe", "MZ"), ("A/B/ok.json", "{}"), ("A/B/d.tmp", "x"));
        var r = RespaldoDatos.Importar(Zip(), Datos);
        Assert.Equal(1, r.Importados);
        Assert.Equal(4, r.Omitidos.Count);
        Assert.True(File.Exists(Path.Combine(Datos, "A", "B", "ok.json")));
        Assert.False(File.Exists(Path.Combine(Datos, "capturas", "x.ams2cap")));
        Assert.False(File.Exists(Path.Combine(Datos, "A", "B", "d.exe")));
    }

    [Theory]
    [InlineData("../evil.json")]
    [InlineData("../../evil.json")]
    [InlineData("a/../../evil.json")]
    [InlineData("a/../b.json")]
    [InlineData("..\\evil.json")]
    [InlineData("a\\..\\..\\evil.json")]
    [InlineData("/etc/evil.json")]
    [InlineData("\\evil.json")]
    [InlineData("C:\\Windows\\evil.json")]
    [InlineData("C:evil.json")]
    [InlineData("a//b.json")]
    [InlineData("./a.json")]
    [InlineData("a/./b.json")]
    [InlineData("a:b.json")]
    [InlineData("a/b.json:flujo")]
    [InlineData("a/ b.json")]
    [InlineData("a/b./c.json")]
    public void ZipSlipYRutasNoRelativasSeRechazanYNoSeTocaNada(string nombre)
    {
        Sembrar();
        CrearZip(Zip(), ("Monza/GP/mapa.json", "{\"formato\":9}"), (nombre, "{\"x\":1}"));
        var ex = Assert.Throws<RespaldoInvalidoException>(() => RespaldoDatos.Importar(Zip(), Datos));
        Assert.Contains("ruta no permitida", ex.Message);
        // ni siquiera la entrada buena se aplico: todo o nada
        Assert.False(File.Exists(Path.Combine(Datos, "Monza", "GP", "mapa.json")));
        Assert.False(File.Exists(Path.Combine(_raiz, "evil.json")));
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "evil.json")));
        Assert.Equal("{\"formato\":1}", Leer("Watkins Glen/GP/mapa.json"));
        Assert.False(File.Exists(Path.Combine(Datos, "Watkins Glen", "GP", "mapa.json.bak")));
    }

    [Fact]
    public void ZipSlipConElNombreExactoDeLaCarpetaHermanaNoEscapa()
    {
        // "datos-otro" empieza igual que "datos": no debe pasar por el chequeo de prefijo
        Directory.CreateDirectory(Datos);
        Assert.Throws<RespaldoInvalidoException>(() => RespaldoDatos.RutaSegura("../datos-otro/x.json", Datos));
        Assert.Equal("a/b.json", RespaldoDatos.RutaSegura("a\\b.json", Datos));
        Assert.Equal("Watkins Glen/GP/mapa.json", RespaldoDatos.RutaSegura("Watkins Glen/GP/mapa.json", Datos));
        Assert.Equal("Autódromo José/GP/mapa.json", RespaldoDatos.RutaSegura("Autódromo José/GP/mapa.json", Datos));
    }

    [Fact]
    public void UnJsonDanadoRechazaTodoElZip()
    {
        Sembrar();
        CrearZip(Zip(), ("Monza/GP/mapa.json", "{\"formato\":9}"), ("Monza/GP/GT3.json", "{esto no es json"));
        var ex = Assert.Throws<RespaldoInvalidoException>(() => RespaldoDatos.Importar(Zip(), Datos));
        Assert.Contains("JSON", ex.Message);
        Assert.False(File.Exists(Path.Combine(Datos, "Monza", "GP", "mapa.json")));
    }

    [Fact]
    public void UnArchivoQueNoEsZipSeRechaza()
    {
        File.WriteAllText(Zip(), "esto no es un zip");
        Assert.Throws<RespaldoInvalidoException>(() => RespaldoDatos.Importar(Zip(), Datos));
        Assert.Throws<FileNotFoundException>(() => RespaldoDatos.Importar(Zip("nada.zip"), Datos));
    }

    [Fact]
    public void NombresRepetidosEnElZipSeRechazan()
    {
        CrearZip(Zip(), ("a/x.json", "{}"), ("A/X.json", "{}"));
        Assert.Throws<RespaldoInvalidoException>(() => RespaldoDatos.Importar(Zip(), Datos));
    }

    [Fact]
    public void LasEntradasQueSonCarpetasSeIgnoran()
    {
        using (var fs = new FileStream(Zip(), FileMode.Create))
        using (var z = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            z.CreateEntry("Monza/");
            var e = z.CreateEntry("Monza/mapa.json");
            using var w = new StreamWriter(e.Open()); w.Write("{}");
        }
        Assert.Equal(1, RespaldoDatos.Importar(Zip(), Datos).Importados);
    }

    [Fact]
    public void ImportarDosVecesConservaElUltimoBakYNoDejaTmp()
    {
        Escribir("P/V/mapa.json", "{\"v\":1}");
        CrearZip(Zip(), ("P/V/mapa.json", "{\"v\":2}"));
        RespaldoDatos.Importar(Zip(), Datos);
        CrearZip(Zip("2.zip"), ("P/V/mapa.json", "{\"v\":3}"));
        RespaldoDatos.Importar(Zip("2.zip"), Datos);
        Assert.Equal("{\"v\":3}", Leer("P/V/mapa.json"));
        Assert.Equal("{\"v\":2}", Leer("P/V/mapa.json.bak"));
        Assert.False(Directory.GetFiles(Datos, "*.tmp", SearchOption.AllDirectories).Length > 0);
    }
}
