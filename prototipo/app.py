"""
AMS2 - guia de puntos de frenado (app externa, lee la memoria compartida).

  python app.py dump              muestra lo que lee del juego (para verificar la conexion)
  python app.py run               ventana siempre visible: proxima curva y metros hasta frenar
  python app.py run --consola     lo mismo en la consola
  python app.py record            graba tus frenadas vuelta a vuelta en puntos_frenado.json
  python app.py record --ia todas graba a TODAS las IA a la vez (mediana por curva y auto)
  python app.py record --ia 3     infiere las frenadas del participante 3 (por velocidad)

Agregar --demo prueba todo sin el juego (simulacion propia, ver demo.py).
"""
import argparse
import os
import sys
import threading

from ams2_shm import (ESTADOS_ACTIVOS, NOMBRES_ESTADO, SHARED_MEMORY_VERSION,
                      STORED_PARTICIPANTS_MAX, LectorWindows, VersionDistinta,
                      jugador, muestra, txt)
from frenado import Grabador, GrabadorIA, Tabla, estado_guia

RUTA_TABLA = os.path.join(os.path.dirname(os.path.abspath(__file__)), "puntos_frenado.json")
AYUDA_ACTIVAR = "En AMS2: Options > System > Shared Memory = Project CARS 2, UDP Frequency = OFF"


def crear_lector(args):
    if args.demo:
        from demo import LectorDemo
        return LectorDemo(realtime=not args.rapido)
    return LectorWindows()


def pitar(frecuencia):
    try:
        import winsound
    except ImportError:
        return
    threading.Thread(target=winsound.Beep, args=(frecuencia, 70), daemon=True).start()


# ------------------------------------------------------------------ dump

def imprimir(sm):
    est = sm.mGameState
    nombre = NOMBRES_ESTADO[est] if est < len(NOMBRES_ESTADO) else "?"
    print(f"Memoria: v{sm.mVersion} (esperada {SHARED_MEMORY_VERSION}) | juego: {est} ({nombre}) "
          f"| sesion: {sm.mSessionState}")
    print(f"Pista: {txt(sm.mTrackLocation)} | {txt(sm.mTrackVariation)} | largo {sm.mTrackLength:.0f} m")
    print(f"Auto: {txt(sm.mCarName)} ({txt(sm.mCarClassName)})")
    print(f"Participantes: {sm.mNumParticipants} | viendo indice {sm.mViewedParticipantIndex}")
    print(f"Auto visto: freno {sm.mBrake:.2f} | gas {sm.mThrottle:.2f} | "
          f"{sm.mSpeed * 3.6:.0f} km/h | marcha {sm.mGear}")
    print("Participantes activos (primeros 8):  idx  nombre            dist(m)  km/h  pos  vueltas")
    mostrados = 0
    for i in range(STORED_PARTICIPANTS_MAX):
        p = sm.mParticipantInfo[i]
        if p.mIsActive and mostrados < 8:
            print(f"                                    {i:>3}  {txt(p.mName)[:16]:<16} "
                  f"{p.mCurrentLapDistance:>8.1f}  {sm.mSpeeds[i] * 3.6:>4.0f}  "
                  f"{p.mRacePosition:>3}  {p.mLapsCompleted:>5}")
            mostrados += 1


def cmd_dump(args, lector):
    while True:
        try:
            sm = lector.leer()
        except VersionDistinta as e:
            print(e)
            return 1
        if sm is None:
            print("No detecto AMS2 (memoria compartida $pcars2$ no disponible).\n" + AYUDA_ACTIVAR)
        else:
            imprimir(sm)
        if not args.seguir:
            return 0
        print()
        lector.dormir(1.0)


# ------------------------------------------------------------------- run

class Guia:
    UMBRALES = ((150, 800), (75, 1000), (0, 1400))    # metros, Hz del pitido

    def __init__(self, lector, tabla, pitidos=False):
        self.lector, self.tabla, self.pitidos = lector, tabla, pitidos
        self._clave, self.curvas, self.ref = None, [], None
        self._curva_actual, self._pitados = None, set()

    @staticmethod
    def _v(info, curva, dist, nota, nivel):
        return {"info": info, "curva": curva, "dist": dist, "nota": nota, "nivel": nivel}

    def _cargar(self, pista, variante, auto):
        entrada, self.ref = self.tabla.entrada(pista, variante, auto), None
        if entrada is None:
            otros = self.tabla.autos(pista, variante)
            if otros:
                entrada, self.ref = self.tabla.entrada(pista, variante, otros[0]), otros[0]
        self.curvas = sorted(entrada["curvas"], key=lambda c: c["frenar_en"]) if entrada else []

    def paso(self):
        try:
            sm = self.lector.leer()
        except VersionDistinta as e:
            return self._v("", "", "!", str(e), "error")
        if sm is None:
            return self._v("", "", "-", "Esperando AMS2... " + AYUDA_ACTIVAR, "info")
        if sm.mGameState not in ESTADOS_ACTIVOS:
            return self._v("", "", "-", "Sin pista (menu / pausa)", "info")
        m = muestra(sm)
        if m.largo <= 0 or not m.pista:
            return self._v("", "", "-", "Esperando datos de la pista", "info")
        clave = (m.pista, m.variante, m.auto)
        if clave != self._clave:
            self._clave = clave
            self._cargar(*clave)
        info = f"{m.pista} | {m.variante} | {m.auto}"
        if not self.curvas:
            return self._v(info, "", "-", "Sin datos para esta pista/auto. Grabalos con: app.py record", "info")
        est = estado_guia(self.curvas, m.dist, m.largo)
        c = est["curva"]
        ref = f"  [ref. de otro auto: {self.ref}]" if self.ref else ""
        if c["curva"] != self._curva_actual:
            self._curva_actual, self._pitados = c["curva"], set()
        if est["frenando"]:
            return self._v(info, c["curva"], "FRENA!",
                           f"solta en {est['faltan']:.0f} m{ref}", "zona")
        faltan = est["faltan"]
        if self.pitidos:
            for umbral, hz in self.UMBRALES:
                if faltan <= umbral and umbral not in self._pitados:
                    self._pitados.add(umbral)
                    pitar(hz)
                    break
        nivel = "alerta" if faltan < 120 else "aviso" if faltan < 250 else "ok"
        dist = "FRENA!" if faltan <= 25 else f"{faltan:.0f} m"
        nota = (f"frenar en {c['frenar_en']:.0f} m | frenada {c['largo_frenada']:.0f} m | "
                f"{c.get('v_entrada_kmh', '?')} > {c.get('v_apice_kmh', '?')} km/h{ref}")
        return self._v(info, c["curva"], dist, nota, nivel)


def correr_consola(guia, lector, pasos):
    n = 0
    try:
        while pasos is None or n < pasos:
            v = guia.paso()
            linea = f"{v['curva'] or '-':<9} {v['dist']:>8}  {v['nota']}"
            sys.stdout.write("\r" + linea.ljust(118)[:118])
            sys.stdout.flush()
            lector.dormir(1 / 30)
            n += 1
    except KeyboardInterrupt:
        pass
    print()


def correr_ventana(guia):
    import tkinter as tk
    colores = {"ok": "#5CF28A", "aviso": "#FFC857", "alerta": "#FF5C5C",
               "zona": "#FF3DCB", "info": "#9AA0A6", "error": "#FF5C5C"}
    root = tk.Tk()
    root.title("AMS2 frenado")
    root.configure(bg="#111111")
    root.attributes("-topmost", True)
    root.attributes("-alpha", 0.92)
    root.geometry("460x200+40+40")
    root.bind("<Escape>", lambda e: root.destroy())
    info = tk.Label(root, bg="#111111", fg="#9AA0A6", font=("Segoe UI", 9), anchor="w")
    curva = tk.Label(root, bg="#111111", fg="#FFFFFF", font=("Segoe UI", 18, "bold"), anchor="w")
    dist = tk.Label(root, bg="#111111", fg="#9AA0A6", font=("Segoe UI", 46, "bold"))
    nota = tk.Label(root, bg="#111111", fg="#9AA0A6", font=("Segoe UI", 9),
                    anchor="w", wraplength=440, justify="left")
    for w in (info, curva, dist, nota):
        w.pack(fill="x", padx=10)

    def tick():
        v = guia.paso()
        info.config(text=v["info"])
        curva.config(text=v["curva"])
        dist.config(text=v["dist"], fg=colores.get(v["nivel"], "#FFFFFF"))
        nota.config(text=v["nota"])
        root.after(33, tick)

    tick()
    root.mainloop()


def cmd_run(args, lector):
    try:
        tabla = Tabla(args.tabla)
    except ValueError as e:
        print(f"No pude leer {args.tabla}: {e}")
        return 1
    guia = Guia(lector, tabla, pitidos=args.beep)
    if args.consola or args.demo:
        correr_consola(guia, lector, args.pasos)
    else:
        correr_ventana(guia)
    return 0


# ---------------------------------------------------------------- record

def record_todas(args, lector, tabla):
    """Graba a todas las IA a la vez; la tabla queda con la mediana por curva y auto."""
    grab, pasos = GrabadorIA(tabla), 0
    print(f"Grabando a todas las IA (por velocidad) en {args.tabla}. Ctrl+C para terminar.")
    print("La primera vuelta de cada auto no cuenta; las vueltas con boxes tampoco.")
    try:
        while args.pasos is None or pasos < args.pasos:
            pasos += 1
            sm = lector.leer()
            if sm is None or sm.mGameState not in ESTADOS_ACTIVOS or sm.mTrackLength <= 0:
                lector.dormir(0.2)
                continue
            t, vista = lector.clock(), jugador(sm)
            for i in range(STORED_PARTICIPANTS_MAX):
                if not sm.mParticipantInfo[i].mIsActive:
                    continue
                if i == vista and not args.incluir_jugador:
                    continue
                r = grab.actualizar(muestra(sm, i), t)
                if r:
                    print(f"{r[0]}: {r[1]} curvas guardadas ({r[2]} vueltas acumuladas)")
            if args.vueltas and grab.total_vueltas() >= args.vueltas:
                break
            lector.dormir(1 / 30)
    except KeyboardInterrupt:
        print()
    for (pista, variante, auto) in grab.pool:
        ent = tabla.entrada(pista, variante, auto)
        if not ent:
            continue
        print(f"{pista} | {variante} | {auto}")
        for c in ent["curvas"]:
            print(f"  {c['curva']:<9} frenar en {c['frenar_en']:>7.1f} m | frenada {c['largo_frenada']:>5.1f} m"
                  f" | apice {c['apice']:>7.1f} m | {c['v_entrada_kmh']} > {c['v_apice_kmh']} km/h"
                  f" | {c['vueltas']} vueltas")
    return 0


def cmd_record(args, lector):
    try:
        tabla = Tabla(args.tabla)
    except ValueError as e:
        print(f"No pude leer {args.tabla}: {e}")
        return 1
    if args.ia == "todas":
        return record_todas(args, lector, tabla)
    ia = int(args.ia) if args.ia is not None else None
    args.ia = ia
    modo = args.fuente or ("velocidad" if args.ia is not None else "pedal")
    grab, clave, pasos = None, None, 0
    print(f"Grabando ({modo}) en {args.tabla}. Ctrl+C para terminar.")
    print("La primera vuelta que ve la app no cuenta; las vueltas con boxes tampoco.")
    try:
        while args.pasos is None or pasos < args.pasos:
            pasos += 1
            sm = lector.leer()
            if sm is None or sm.mGameState not in ESTADOS_ACTIVOS or sm.mTrackLength <= 0:
                lector.dormir(0.2)
                continue
            idx = args.ia if args.ia is not None else jugador(sm)
            m = muestra(sm, idx)
            if modo == "pedal" and m.freno is None:
                print(f"El participante {idx} no es el auto visto: no hay pedales. Usa --fuente velocidad.")
                return 1
            if (m.pista, m.variante, m.auto, idx) != clave:
                clave = (m.pista, m.variante, m.auto, idx)
                origen = "propio" if args.ia is None else f"IA #{idx}"
                grab = Grabador(tabla, m.pista, m.variante, m.auto, m.largo, modo, origen)
                print(f"Pista: {m.pista} | {m.variante} | auto: {m.auto} | participante {idx}")
            n = grab.actualizar(m.dist, m.vel, m.freno, lector.clock(), m.vueltas, m.en_pits)
            if n is not None:
                print(f"Vuelta completa #{len(grab.completas)}: {n} curvas guardadas")
                if args.vueltas and len(grab.completas) >= args.vueltas:
                    break
            lector.dormir(1 / 30)
    except KeyboardInterrupt:
        print()
    if grab and grab.completas:
        for c in grab.tabla.entrada(grab.pista, grab.variante, grab.auto)["curvas"]:
            print(f"  {c['curva']:<9} frenar en {c['frenar_en']:>7.1f} m | frenada {c['largo_frenada']:>5.1f} m"
                  f" | apice {c['apice']:>7.1f} m | {c['v_entrada_kmh']} > {c['v_apice_kmh']} km/h"
                  f" | {c['vueltas']} vueltas")
    return 0


# ------------------------------------------------------------------ main

def main():
    comun = argparse.ArgumentParser(add_help=False)
    comun.add_argument("--demo", action="store_true", help="simula el juego (prueba sin AMS2)")
    comun.add_argument("--rapido", action="store_true", help="con --demo: sin esperar tiempo real")
    comun.add_argument("--pasos", type=int, default=None, help=argparse.SUPPRESS)
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    p = sub.add_parser("dump", parents=[comun], help="mostrar lo que se lee del juego")
    p.add_argument("--seguir", action="store_true", help="repetir cada segundo")

    p = sub.add_parser("run", parents=[comun], help="guia de frenado en vivo")
    p.add_argument("--tabla", default=RUTA_TABLA)
    p.add_argument("--consola", action="store_true", help="en consola, sin ventana")
    p.add_argument("--beep", action="store_true", help="pitidos al acercarte (Windows)")

    p = sub.add_parser("record", parents=[comun], help="grabar puntos de frenado")
    p.add_argument("--tabla", default=RUTA_TABLA)
    p.add_argument("--ia", default=None, metavar="N|todas",
                   help="IA a inferir: un indice (ver dump) o 'todas'")
    p.add_argument("--incluir-jugador", action="store_true",
                   help="con --ia todas: sumar tambien al auto visto (por defecto se excluye)")
    p.add_argument("--fuente", choices=("pedal", "velocidad"), default=None)
    p.add_argument("--vueltas", type=int, default=0, help="parar tras N vueltas validas")

    args = ap.parse_args()
    try:
        lector = crear_lector(args)
    except OSError as e:
        print(e)
        return 1
    try:
        return {"dump": cmd_dump, "run": cmd_run, "record": cmd_record}[args.cmd](args, lector)
    except VersionDistinta as e:
        print(e)
        return 1


if __name__ == "__main__":
    sys.exit(main())
