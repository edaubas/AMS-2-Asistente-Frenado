using System.Text;

namespace AMS2Frenado.Core;

public sealed record ParticipanteDiag(int Indice, string Nombre, string Clase, float Distancia,
    float VelocidadKmh, uint Posicion, uint Vueltas);

/// <summary>Resumen legible de una lectura. Equivale a `dump` del prototipo.</summary>
public sealed record InstantaneaDiagnostico(
    uint Version, uint EstadoJuego, uint EstadoSesion, string Pista, string Variante,
    string Clase, string Auto, float LargoPista, int NumParticipantes, int IndiceVisto,
    float Freno, float Acelerador, float VelocidadKmh, int Marcha,
    IReadOnlyList<ParticipanteDiag> Participantes)
{
    private static readonly string[] NombresEstado =
        ["salido", "menu", "jugando", "pausa", "menu (tiempo corre)", "reiniciando", "replay", "menu de replay"];

    public static string NombreEstado(uint e) => e < NombresEstado.Length ? NombresEstado[e] : "?";

    public static InstantaneaDiagnostico De(in SharedMemory sm, int maxParticipantes = 64)
    {
        int visto = sm.mViewedParticipantIndex;
        int vistoOk = visto is >= 0 and < ConstantesShm.MaxParticipantes ? visto : 0;
        var lista = new List<ParticipanteDiag>();
        for (int i = 0; i < ConstantesShm.MaxParticipantes && lista.Count < maxParticipantes; i++)
        {
            ref readonly var p = ref sm.mParticipantInfo[i];
            if (p.mIsActive == 0) continue;
            // los pedales y mSpeed son solo del auto visto; el resto usa mSpeeds[i]
            float vel = i == vistoOk ? sm.mSpeed : sm.mSpeeds[i];
            lista.Add(new ParticipanteDiag(i, Texto.Leer(p.mName), Texto.Leer(sm.mCarClassNames[i]),
                p.mCurrentLapDistance, vel * 3.6f, p.mRacePosition, p.mLapsCompleted));
        }
        return new InstantaneaDiagnostico(sm.mVersion, sm.mGameState, sm.mSessionState,
            Texto.Leer(sm.mTrackLocation), Texto.Leer(sm.mTrackVariation),
            Texto.Leer(sm.mCarClassName), Texto.Leer(sm.mCarName), sm.mTrackLength,
            sm.mNumParticipants, visto, sm.mBrake, sm.mThrottle, sm.mSpeed * 3.6f, sm.mGear, lista);
    }

    public string ATexto()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Memoria: v{Version} (esperada {ConstantesShm.Version}) | juego: {EstadoJuego} ({NombreEstado(EstadoJuego)}) | sesion: {EstadoSesion}");
        sb.AppendLine($"Pista: {Pista} | {Variante} | largo {LargoPista:F0} m");
        sb.AppendLine($"Auto: {Auto} | clase: {Clase}");
        sb.AppendLine($"Participantes: {NumParticipantes} | viendo indice {IndiceVisto}");
        sb.AppendLine($"Auto visto: freno {Freno:F2} | gas {Acelerador:F2} | {VelocidadKmh:F0} km/h | marcha {Marcha}");
        sb.AppendLine();
        sb.AppendLine($"{"idx",3}  {"nombre",-18} {"clase",-14} {"dist(m)",8} {"km/h",5} {"pos",4} {"vueltas",7}");
        foreach (var p in Participantes)
            sb.AppendLine($"{p.Indice,3}  {Cortar(p.Nombre, 18),-18} {Cortar(p.Clase, 14),-14} {p.Distancia,8:F1} {p.VelocidadKmh,5:F0} {p.Posicion,4} {p.Vueltas,7}");
        return sb.ToString();
    }

    private static string Cortar(string s, int n) => s.Length <= n ? s : s[..n];
}
