using AMS2Frenado.Core.Grabacion;
using AMS2Frenado.Core.Guia;
using AMS2Frenado.Core.Overlay;
using Xunit;

namespace AMS2Frenado.Tests;

/// <summary>
/// Decisor unico del aviso (simbolo y pitido): rampa, "ya frenando", referencia principal y una vez por pasada.
/// Todo es logica pura; la ventana WPF y el sonido compilan pero no se ejecutaron.
/// </summary>
public sealed class DecisorAvisoTests
{
    private const double Dt = 1 / 27.0;       // telemetria efectiva de AMS2

    /// <summary>Panel como lo daria el motor: el estado sale de los metros hasta el punto (Lejos > 300 m, Inminente < 100 m).</summary>
    private static PanelGuia P(double metros, int curva = 3, ReferenciaGuia principal = ReferenciaGuia.Ia,
        TipoCurva tipo = TipoCurva.Frenada, EstadoPanel? estado = null)
    {
        var e = estado ?? (metros <= 0 ? EstadoPanel.Frena : metros < 100 ? EstadoPanel.Inminente : metros <= 300 ? EstadoPanel.Aproximacion : EstadoPanel.Lejos);
        return new PanelGuia
        {
            Estado = e, Visible = e != EstadoPanel.Lejos, IndiceCurva = curva, Tipo = tipo, Principal = principal,
            MetrosHastaFrenar = Math.Max(0, metros),
        };
    }

    /// <summary>Un acercamiento a v constante: muestras cada Dt desde 'desde' metros hasta el punto (0) y un poco mas.</summary>
    private static List<(double metros, double t, DecisionAviso d)> Acercar(DecisorAviso dec, double desde, double v, double t0,
        Func<double, double>? freno = null, ReferenciaGuia elegida = ReferenciaGuia.Ia, ReferenciaGuia principal = ReferenciaGuia.Ia, int curva = 3,
        double? paso = null)
    {
        var res = new List<(double, double, DecisionAviso)>();
        double dt = paso ?? Dt;
        double m = desde, t = t0;
        while (true)
        {
            var d = dec.Evaluar(P(m, curva, principal), elegida, v, freno?.Invoke(m) ?? 0, t);
            res.Add((m, t, d));
            if (m == 0) break;
            m -= v * dt; t += dt;
            if (m < 1e-6) m = 0;            // la ultima muestra cae justo en el punto de frenado
        }
        return res;
    }

    // ---------------------------------------------------------------- rampa

    [Fact]
    public void LaRampaSubeDe0A1EnFuncionDelTiempoRestanteYLlegaA1EnElPunto()
    {
        var dec = new DecisorAviso();                       // 2,0 s por defecto
        const double v = 50;                                // 180 km/h: 2 s = 100 m
        var r = Acercar(dec, 400, v, 0);

        Assert.All(r.Where(x => x.metros >= 100), x => Assert.Equal(0, x.d.Progreso));            // antes de 2 s no se ve nada
        foreach (var x in r.Where(x => x.metros is < 100 and > 0))
            Assert.Equal(1 - x.metros / (v * 2.0), x.d.Progreso, 6);                              // lineal en el tiempo restante
        Assert.Equal(1, r[^1].d.Progreso);                                                        // exactamente 1 en el punto
        Assert.Equal(0, r[^1].metros);
        for (int i = 1; i < r.Count; i++) Assert.True(r[i].d.Progreso >= r[i - 1].d.Progreso);   // nunca baja
        // y a un segundo del punto va por la mitad
        var medio = r.MinBy(x => Math.Abs(x.metros - 50));
        Assert.InRange(medio.d.Progreso, 0.45, 0.55);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    public void LaRampaEmpiezaAlosSegundosConfigurados(double rampa)
    {
        var dec = new DecisorAviso { RampaS = rampa };
        const double v = 40;
        var r = Acercar(dec, 300, v, 0);
        double primeroVisible = r.First(x => x.d.Progreso > 0).metros;
        Assert.InRange(primeroVisible, v * rampa - v * Dt - 0.01, v * rampa);
        Assert.Equal(1, r[^1].d.Progreso);
    }

    [Fact]
    public void LaRampaConfigurableSeLimitaEntre1y3s()
    {
        var d = new DecisorAviso();
        Assert.Equal(2.0, d.RampaS);
        d.RampaS = 0.2; Assert.Equal(1.0, d.RampaS);
        d.RampaS = 9; Assert.Equal(3.0, d.RampaS);
        d.RampaS = double.NaN; Assert.Equal(2.0, d.RampaS);
        d.RampaS = 2.5; Assert.Equal(2.5, d.RampaS);
        Assert.Equal(0.5, DecisorAviso.ProgresoRampa(1, 2));
        Assert.Equal(0, DecisorAviso.ProgresoRampa(5, 2));
        Assert.Equal(1, DecisorAviso.ProgresoRampa(0, 2));
        Assert.Equal(0, DecisorAviso.ProgresoRampa(double.PositiveInfinity, 2));
    }

    [Fact]
    public void ConVelocidadVariableLaRampaNuncaVuelveAtras()
    {
        // se viene levantando: la velocidad cae a la mitad y el "tiempo restante" calculado con la velocidad actual
        // subiria; el simbolo no debe bajar
        var dec = new DecisorAviso();
        double m = 260, v = 70, t = 0, ult = 0;
        var vistos = new List<double>();
        while (m > 0)
        {
            var d = dec.Evaluar(P(m), ReferenciaGuia.Ia, v, 0, t);
            Assert.True(d.Progreso >= ult - 1e-12, $"bajo de {ult:0.000} a {d.Progreso:0.000} a {m:0} m");
            ult = d.Progreso; vistos.Add(d.Progreso);
            m -= v * Dt; t += Dt;
            v = Math.Max(25, v - 0.9);                       // desacelera sin frenar
        }
        Assert.Contains(vistos, p => p is > 0.2 and < 1);
        Assert.Equal(1, dec.Evaluar(P(0), ReferenciaGuia.Ia, v, 0, t).Progreso);
    }

    [Fact]
    public void ConUnHuecoDeDatosDe450msNoBajaNiDuplicaYLlegaAlPunto()
    {
        var dec = new DecisorAviso();
        const double v = 50;
        // muestras cada 0,45 s en todo el acercamiento
        var r = Acercar(dec, 300, v, 0, paso: 0.45);
        for (int i = 1; i < r.Count; i++) Assert.True(r[i].d.Progreso >= r[i - 1].d.Progreso);
        Assert.Equal(1, r[^1].d.Progreso);
        Assert.Single(r, x => x.d.Pitar);                    // un solo pitido, en la muestra del punto

        // un hueco justo antes del punto: de 12 m a 10 m pasado (el punto cae dentro del hueco)
        var dec2 = new DecisorAviso();
        double t = 0;
        var a = dec2.Evaluar(P(60), ReferenciaGuia.Ia, v, 0, t);
        var b = dec2.Evaluar(P(12), ReferenciaGuia.Ia, v, 0, t += Dt);
        var c = dec2.Evaluar(P(0), ReferenciaGuia.Ia, v, 0, t += 0.45);
        Assert.True(a.Progreso < b.Progreso && b.Progreso < c.Progreso);
        Assert.Equal(1, c.Progreso);
        Assert.True(c.Pitar);
        Assert.False(dec2.Evaluar(P(0), ReferenciaGuia.Ia, v, 0, t + Dt).Pitar);
    }

    [Fact]
    public void ParadoNoHayDivisionPorCeroNiRampaPorTiempo()
    {
        var dec = new DecisorAviso();
        var d = dec.Evaluar(P(250), ReferenciaGuia.Ia, 0, 0, 0);
        Assert.Equal(0, d.Progreso);
        d = dec.Evaluar(P(250), ReferenciaGuia.Ia, double.NaN, 0, 0.04);
        Assert.Equal(0, d.Progreso);
    }

    // ---------------------------------------------------------------- pleno, pitido y fin

    [Fact]
    public void EnElPuntoPitaUnaVezYSeMantieneEn1HastaLos1_5s()
    {
        var dec = new DecisorAviso();
        var r = Acercar(dec, 200, 50, 0);
        double tPunto = r[^1].t;
        Assert.True(r[^1].d.Pitar);
        Assert.Equal(1, r[^1].d.Progreso);
        Assert.Equal(tPunto + 1.5, r[^1].d.ExpiraEn, 9);

        double t = tPunto;
        var seguido = new List<DecisionAviso>();
        while (t < tPunto + 2.0) { t += Dt; seguido.Add(dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0, t)); }
        Assert.All(seguido, d => Assert.False(d.Pitar));
        Assert.Equal(1, seguido.Last(d => d.Progreso > 0).Progreso);
        double fin = seguido.Select((d, i) => (d, i)).First(x => x.d.Progreso == 0).i * Dt;
        Assert.InRange(fin, 1.5 - 2 * Dt, 1.5 + 2 * Dt);
    }

    [Fact]
    public void LaDuracionDespuesDelPuntoSeLimitaEntre1y2s()
    {
        var dec = new DecisorAviso { DuracionS = 9 };
        Assert.Equal(2.0, dec.DuracionS);
        dec.DuracionS = 1.2;
        var r = Acercar(dec, 100, 50, 0);
        Assert.Equal(r[^1].t + 1.2, r[^1].d.ExpiraEn, 9);
    }

    [Fact]
    public void ElPedalDespuesDelPuntoApagaElAvisoYNoVuelveAunqueSueltes()
    {
        var dec = new DecisorAviso();
        var r = Acercar(dec, 150, 50, 0);
        double t = r[^1].t;
        Assert.Equal(1, dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0.15, t += Dt).Progreso);        // justo en el umbral: no cuenta
        Assert.Equal(0, dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0.16, t += Dt).Progreso);
        Assert.Equal(0, dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0, t += Dt).Progreso);           // soltar no lo trae de vuelta
        Assert.Equal(0, dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0, t += Dt).Progreso);
    }

    [Fact]
    public void AlPasarElVerticeOLaSalidaDeFrenaSeApaga()
    {
        var dec = new DecisorAviso();
        var r = Acercar(dec, 100, 50, 0);
        double t = r[^1].t;
        // la guia baja de FRENA (retroceso de estado) en la misma curva: se apaga y no vuelve a aparecer
        Assert.Equal(0, dec.Evaluar(P(30), ReferenciaGuia.Ia, 50, 0, t + Dt).Progreso);
        Assert.Equal(0, dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0, t + 2 * Dt).Progreso);
        // pasa el vertice: la guia cambia de curva (y esa recien viene de lejos)
        Assert.Equal(0, dec.Evaluar(P(900, curva: 4), ReferenciaGuia.Ia, 50, 0, t + 3 * Dt).Progreso);
    }

    // ---------------------------------------------------------------- ya frenando

    [Fact]
    public void SiYaFrenasAlLlegarAlPuntoNoPitaNiMuestra()
    {
        var dec = new DecisorAviso();
        var r = Acercar(dec, 400, 50, 0, freno: m => m <= 0 ? 0.7 : 0);       // el pedal recien aparece en el punto
        Assert.All(r, x => Assert.False(x.d.Pitar));
        Assert.Equal(0, r[^1].d.Progreso);
        double t = r[^1].t;
        // y soltar despues no lo dispara
        Assert.False(dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0, t + Dt).Pitar);
        Assert.Equal(0, dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0, t + Dt).Progreso);
    }

    [Fact]
    public void SiFrenasDentroDeLaZonaInmediataAntesDelPuntoNoPitaAunqueSueltes()
    {
        // frenaste 30 m antes (menos de 1 s a 50 m/s... 0,6 s) y soltaste: en el punto ya no pita ni se ve
        var dec = new DecisorAviso();
        var r = Acercar(dec, 300, 50, 0, freno: m => m is <= 32 and >= 20 ? 0.5 : 0);
        Assert.All(r, x => Assert.False(x.d.Pitar));
        Assert.Equal(0, r[^1].d.Progreso);
        Assert.All(r.Where(x => x.metros <= 32), x => Assert.Equal(0, x.d.Progreso));
    }

    [Theory]
    [InlineData(80, 70)]      // 70 m a 80 m/s = 0,9 s: dentro de la zona por tiempo aunque pase de 40 m
    [InlineData(15, 38)]      // 38 m a 15 m/s = 2,5 s: dentro por distancia (< 40 m)
    public void LaZonaInmediataEsUnSegundoOCuarentaMetros(double v, double metros)
    {
        var dec = new DecisorAviso();
        double t = 0;
        dec.Evaluar(P(metros + 5), ReferenciaGuia.Ia, v, 0, t);
        dec.Evaluar(P(metros), ReferenciaGuia.Ia, v, 0.5, t += Dt);
        var r = dec.Evaluar(P(0), ReferenciaGuia.Ia, v, 0, t += Dt);
        Assert.False(r.Pitar);
        Assert.Equal(0, r.Progreso);
    }

    [Fact]
    public void UnToqueDePedalLejosDelPuntoSoloOcultaMientrasDuraYElPitidoSigueEnPie()
    {
        // 1,8 s antes del punto (90 m a 50 m/s), fuera de la zona inmediata, pisas y soltas (por ejemplo, el final de la frenada anterior)
        var dec = new DecisorAviso();
        const double v = 50;
        double t = 0;
        var antes = dec.Evaluar(P(110), ReferenciaGuia.Ia, v, 0, t);                  // todavia no se ve
        var viendo = dec.Evaluar(P(90), ReferenciaGuia.Ia, v, 0, t += Dt);
        var pisando = dec.Evaluar(P(88), ReferenciaGuia.Ia, v, 0.6, t += Dt);
        var suelto = dec.Evaluar(P(86), ReferenciaGuia.Ia, v, 0, t += Dt);
        Assert.Equal(0, antes.Progreso);
        Assert.True(viendo.Progreso > 0);
        Assert.Equal(0, pisando.Progreso);
        Assert.True(suelto.Progreso >= viendo.Progreso);                              // retoma sin perder lo ganado
        var punto = dec.Evaluar(P(0), ReferenciaGuia.Ia, v, 0, t += 1.0);
        Assert.True(punto.Pitar);
        Assert.Equal(1, punto.Progreso);
    }

    [Fact]
    public void NoRepiteEnLaMismaPasadaPeroSiEnLaSiguiente()
    {
        var dec = new DecisorAviso();
        var r = Acercar(dec, 150, 50, 0);
        double t = r[^1].t;
        // se apaga por tiempo
        t += 2.0;
        Assert.Equal(0, dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0, t).Progreso);
        // la guia retrocede a Inminente y vuelve a FRENA en la misma curva: ni simbolo ni pitido
        Assert.Equal(0, dec.Evaluar(P(40), ReferenciaGuia.Ia, 50, 0, t += Dt).Progreso);
        var otra = dec.Evaluar(P(0), ReferenciaGuia.Ia, 50, 0, t += Dt);
        Assert.Equal(0, otra.Progreso);
        Assert.False(otra.Pitar);
        // vuelve a Lejos (la vuelta siguiente): nueva pasada
        dec.Evaluar(P(2500), ReferenciaGuia.Ia, 50, 0, t += Dt);
        var nueva = Acercar(dec, 150, 50, t += Dt);
        Assert.Single(nueva, x => x.d.Pitar);
        Assert.Equal(1, nueva[^1].d.Progreso);
    }

    [Fact]
    public void UnaChicaneAvisaCadaCurvaPorSeparado()
    {
        var dec = new DecisorAviso();
        var a = Acercar(dec, 120, 40, 0, curva: 3);
        Assert.True(a[^1].d.Pitar);
        // curva siguiente, directo en rampa (sin pasar por Lejos)
        var b = Acercar(dec, 80, 40, a[^1].t + Dt, curva: 4);
        Assert.Single(b, x => x.d.Pitar);
        Assert.Equal(1, b[^1].d.Progreso);
    }

    // ---------------------------------------------------------------- tipo y referencia

    [Theory]
    [InlineData(TipoCurva.Toque)]
    [InlineData(TipoCurva.Levanta)]
    public void ToqueYLevantaNuncaAvisan(TipoCurva tipo)
    {
        var dec = new DecisorAviso();
        double t = 0;
        for (double m = 200; m >= 0; m -= 2)
        {
            var d = dec.Evaluar(P(m, tipo: tipo, estado: m > 0 ? EstadoPanel.Aproximacion : EstadoPanel.Toque), ReferenciaGuia.Ia, 50, 0, t += Dt);
            Assert.Equal(0, d.Progreso);
            Assert.False(d.Pitar);
        }
    }

    [Theory]
    [InlineData(ReferenciaGuia.Ia, ReferenciaGuia.Vos)]
    [InlineData(ReferenciaGuia.Vos, ReferenciaGuia.Ia)]
    public void SoloAvisaConLaReferenciaPrincipalElegida(ReferenciaGuia elegida, ReferenciaGuia otra)
    {
        // la guia muestra la otra referencia porque a la elegida le falta el bloque: ni simbolo ni pitido
        var dec = new DecisorAviso();
        var r = Acercar(dec, 300, 50, 0, elegida: elegida, principal: otra);
        Assert.All(r, x => { Assert.Equal(0, x.d.Progreso); Assert.False(x.d.Pitar); });

        // con la elegida presente sí
        var dec2 = new DecisorAviso();
        var ok = Acercar(dec2, 300, 50, 0, elegida: elegida, principal: elegida);
        Assert.Equal(1, ok[^1].d.Progreso);
        Assert.Single(ok, x => x.d.Pitar);
    }

    [Fact]
    public void CambiarLaReferenciaElegidaCambiaCuandoAvisa()
    {
        var dec = new DecisorAviso();
        const double v = 50;
        // la curva solo tiene bloque de vos: con IA elegida no avisa
        Assert.Equal(0, dec.Evaluar(P(60, principal: ReferenciaGuia.Vos), ReferenciaGuia.Ia, v, 0, 0).Progreso);
        // el usuario pasa a Vos como principal: rige la nueva
        Assert.True(dec.Evaluar(P(55, principal: ReferenciaGuia.Vos), ReferenciaGuia.Vos, v, 0, Dt).Progreso > 0);
    }

    // ---------------------------------------------------------------- sin lectura

    [Fact]
    public void SinPanelOSinDatosSeReiniciaYNoAvisa()
    {
        var dec = new DecisorAviso();
        var r = Acercar(dec, 100, 50, 0);
        Assert.Equal(1, r[^1].d.Progreso);
        Assert.Equal(0, dec.Evaluar(null, ReferenciaGuia.Ia, 50, 0, 10).Progreso);
        Assert.Equal(0, dec.Evaluar(new PanelGuia { Estado = EstadoPanel.SinDatos }, ReferenciaGuia.Ia, 50, 0, 11).Progreso);
        // y la pasada siguiente arranca limpia
        var otra = Acercar(dec, 100, 50, 12);
        Assert.Single(otra, x => x.d.Pitar);
    }

    [Fact]
    public void ElFrenoNoValidoSeTomaComoQueNoFrena()
    {
        var dec = new DecisorAviso();
        var r = Acercar(dec, 100, 50, 0, freno: _ => double.NaN);
        Assert.Equal(1, r[^1].d.Progreso);
        Assert.True(r[^1].d.Pitar);
    }
}
