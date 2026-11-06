using AMS2Frenado.Core;

namespace AMS2Frenado.Tests;

/// <summary>
/// Simulador minimo de la memoria compartida (port de prototipo/demo.py) para probar sin el juego.
/// Es un modelo propio y simple (frenadas a desaceleracion constante en una pista circular de 3000 m);
/// sirve para probar la logica, no representa la fisica ni la IA reales de AMS2.
/// El participante 0 es el auto visto (con pedales); los demas son "IA" (solo velocidad y distancia).
/// </summary>
internal sealed class Simulador
{
    public const double Largo = 3000.0;
    public const double VMax = 70.0;      // m/s
    public const double Acel = 6.0;       // m/s2
    public const double Dt = 1.0 / 30;

    /// <summary>Zona de baja velocidad: del metro Ini al Fin se baja hasta VMin (m/s).</summary>
    public readonly record struct Zona(double Ini, double Fin, double VMin, bool SoloLevanta = false);

    // tres frenadas fuertes (como el demo), un toque (cae 21,6 km/h) y una levantada (cae 9 km/h)
    public static readonly Zona[] Jugador =
    [
        new(150, 230, 67.5, true), new(440, 590, 25), new(1200, 1340, 30), new(1750, 1830, 64), new(2250, 2400, 22),
    ];
    public static readonly Zona[] Ia =
    [
        new(155, 235, 67.5, true), new(455, 600, 26), new(1215, 1345, 31), new(1765, 1835, 64.5), new(2270, 2405, 23),
    ];

    public sealed class Auto
    {
        public Zona[] Zonas; public double Jitter; public Random Rng;
        public double D, V = 40.0; public uint Vueltas;
        public double Freno, Gas;
        public bool EnPits, Invalida;
        public string Clase = "GT3";
        private Zona[] _zl = [];

        public Auto(Zona[] zonas, double jitter, Random rng) { Zonas = zonas; Jitter = jitter; Rng = rng; Sortear(); }

        private void Sortear()
        {
            double j = Jitter;
            _zl = Zonas.Select(z => z with { Ini = z.Ini + (Rng.NextDouble() * 2 - 1) * j }).ToArray();
        }

        public void Paso(double dt)
        {
            Zona? zona = null;
            foreach (var z in _zl) if (z.Ini <= D && D < z.Fin) { zona = z; break; }
            double a = 0; Freno = Gas = 0;
            if (zona is { } zz)
            {
                if (V > zz.VMin)
                {
                    a = -(V * V - zz.VMin * zz.VMin) / (2 * Math.Max(zz.Fin - D, 1.0));
                    if (!zz.SoloLevanta) Freno = Math.Min(1.0, Math.Max(0.3, -a / 15.0));
                }
            }
            else if (V < VMax) { a = Acel; Gas = 1.0; }
            else Gas = 0.9;
            V = Math.Max(5.0, V + a * dt);
            D += V * dt;
            if (D >= Largo) { D -= Largo; Vueltas++; Sortear(); Invalida = false; }
        }
    }

    public readonly List<Auto> Autos = [];
    public string Pista = "Demo Circuit", Variante = "GP";
    public double Tiempo;
    private uint _n;

    /// <param name="ias">cantidad de autos IA (ademas del jugador)</param>
    public Simulador(int ias = 1, int semilla = 1, string clase = "GT3")
    {
        var rng = new Random(semilla);
        Autos.Add(new Auto(Jugador, 6.0, rng) { Clase = clase });
        for (int i = 0; i < ias; i++)
        {
            // cada IA arranca en otro punto de la pista para que no vayan todas pegadas
            Autos.Add(new Auto(Ia, 3.0, rng) { Clase = clase, D = (i + 1) * 311.0 % Largo });
        }
    }

    public SharedMemory Paso()
    {
        Tiempo += Dt;
        _n++;
        foreach (var a in Autos) a.Paso(Dt);
        return Armar();
    }

    public SharedMemory Armar()
    {
        var sm = new SharedMemory
        {
            mVersion = ConstantesShm.Version,
            mSequenceNumber = 2 * _n,
            mGameState = 2,
            mSessionState = 1,
            mViewedParticipantIndex = 0,
            mNumParticipants = Autos.Count,
            mTrackLength = (float)Largo,
        };
        Muestras.Texto(sm.mTrackLocation, Pista);
        Muestras.Texto(sm.mTrackVariation, Variante);
        Muestras.Texto(sm.mCarName, "Demo GT3");
        Muestras.Texto(sm.mCarClassName, Autos[0].Clase);
        var j = Autos[0];
        sm.mBrake = (float)j.Freno; sm.mUnfilteredBrake = (float)j.Freno;
        sm.mThrottle = (float)j.Gas;
        sm.mSpeed = (float)j.V;
        sm.mLapInvalidated = (byte)(j.Invalida ? 1 : 0);
        double radio = Largo / (2 * Math.PI);
        for (int i = 0; i < Autos.Count; i++)
        {
            var a = Autos[i];
            ref var p = ref sm.mParticipantInfo[i];
            p.mIsActive = 1;
            Muestras.Texto(p.mName, i == 0 ? "Jugador" : $"IA {i}");
            p.mCurrentLapDistance = (float)a.D;
            p.mLapsCompleted = p.mCurrentLap = a.Vueltas;
            p.mRacePosition = (uint)(i + 1);
            double ang = 2 * Math.PI * a.D / Largo;
            p.mWorldPosition[0] = (float)(radio * Math.Cos(ang));
            p.mWorldPosition[1] = 0;
            p.mWorldPosition[2] = (float)(radio * Math.Sin(ang));
            sm.mSpeeds[i] = (float)a.V;
            sm.mPitModes[i] = a.EnPits ? 3u : 0u;
            sm.mLapsInvalidated[i] = (byte)(a.Invalida ? 1 : 0);
            Muestras.Texto(sm.mCarNames[i], "Demo GT3");
            Muestras.Texto(sm.mCarClassNames[i], a.Clase);
        }
        return sm;
    }

    public static string CarpetaTemporal() => Path.Combine(Path.GetTempPath(), "ams2-frenado-test-" + Guid.NewGuid().ToString("N"));
}
