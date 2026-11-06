"""
Logica pura (no toca el juego): tabla de puntos de frenado, detector de frenadas,
grabador y guia. Todas las distancias son metros de distancia de vuelta
(mCurrentLapDistance), de 0 a largo de pista.
"""
import json
import math
import os
import shutil
import statistics

FORMATO = 1


def dif_circ(a, b, largo):
    """a - b en una pista circular, en [-largo/2, largo/2)."""
    d = (a - b) % largo
    return d - largo if d >= largo / 2 else d


# ---------------------------------------------------------------- tabla

class Tabla:
    """JSON: pistas -> variante -> auto -> {largo, curvas: [...]}"""

    def __init__(self, ruta):
        self.ruta = ruta
        self.datos = {"formato": FORMATO, "pistas": {}}
        self._respaldado = False
        if os.path.exists(ruta):
            with open(ruta, "r", encoding="utf-8") as f:
                self.datos = json.load(f)
            self.datos.setdefault("pistas", {})

    def entrada(self, pista, variante, auto):
        return self.datos["pistas"].get(pista, {}).get(variante, {}).get(auto)

    def autos(self, pista, variante):
        return sorted(self.datos["pistas"].get(pista, {}).get(variante, {}))

    def guardar_entrada(self, pista, variante, auto, largo, curvas):
        pistas = self.datos["pistas"]
        pistas.setdefault(pista, {}).setdefault(variante, {})[auto] = {
            "largo": round(float(largo), 1), "curvas": curvas}
        self.guardar()

    def guardar(self):
        if os.path.exists(self.ruta) and not self._respaldado:
            shutil.copyfile(self.ruta, self.ruta + ".bak")   # una copia por sesion
            self._respaldado = True
        tmp = self.ruta + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(self.datos, f, ensure_ascii=False, indent=2)
        os.replace(tmp, self.ruta)


# -------------------------------------------------------------- detector

class Detector:
    """
    Detecta frenadas a partir de muestras (distancia de vuelta, velocidad, freno, tiempo).

    modo "pedal":     usa el pedal de freno (solo auto propio / auto visto).
    modo "velocidad": infiere la frenada por la caida de velocidad (sirve para la IA).

    El inicio de la frenada se toma cuando la senal supera el umbral debil
    (freno >= b_off, o desaceleracion >= a_off), no cuando supera el fuerte, para
    reducir el retraso. Cada evento trae: frenar_en, largo_frenada, apice, v0, vmin.
    """

    def __init__(self, largo, modo="pedal", v_min=20.0, dv_min=8.0,
                 b_on=0.25, b_off=0.10, a_on=3.0, a_off=1.0, t_off=0.2, post_m=120.0):
        self.largo, self.modo = float(largo), modo
        self.v_min, self.dv_min = v_min, dv_min
        self.b_on, self.b_off = b_on, b_off
        self.a_on, self.a_off = a_on, a_off
        self.t_off, self.post_m = t_off, post_m
        self.estado = "libre"
        self.prev_d = self.prev_t = self.prev_v = None
        self.u = 0.0            # distancia acumulada sin saltos de vuelta
        self.a = None           # aceleracion suavizada (m/s2)
        self.onset = None

    def _empezar(self, d, v, vuelta):
        u0, d0, v0 = self.onset
        self.estado = "frenando"
        self.ini_u, self.ini_d, self.ini_v = u0, d0, v0
        self.vuelta = vuelta
        self.v_min_pos, self.d_min = v, d
        self.off_desde = None
        self.fin_u = None

    def _cerrar(self):
        self.estado = "libre"
        if self.ini_v - self.v_min_pos < self.dv_min:
            return None   # toque de freno, no una frenada real
        return {"frenar_en": self.ini_d, "largo_frenada": self.fin_u - self.ini_u,
                "apice": self.d_min, "v0": self.ini_v, "vmin": self.v_min_pos,
                "vuelta": self.vuelta}

    def actualizar(self, d, v, freno, t, vuelta=0):
        """Devuelve la lista de eventos que terminaron con esta muestra."""
        salida = []
        self.u = d if self.prev_d is None else self.u + dif_circ(d, self.prev_d, self.largo)
        if self.prev_t is not None and t > self.prev_t:
            a_raw = (v - self.prev_v) / (t - self.prev_t)
            self.a = a_raw if self.a is None else self.a + 0.4 * (a_raw - self.a)
        self.prev_d, self.prev_t, self.prev_v = d, t, v

        if self.modo == "pedal":
            if freno is None:
                raise ValueError("modo pedal: este participante no tiene dato de freno")
            on = freno >= self.b_on and v >= self.v_min
            off = freno < self.b_off
        else:
            on = self.a is not None and self.a <= -self.a_on and v >= self.v_min
            off = self.a is None or self.a > -self.a_off

        if off:
            self.onset = None
        elif self.onset is None:
            self.onset = (self.u, d, v)

        if self.estado == "libre":
            if on and self.onset is not None:
                self._empezar(d, v, vuelta)
            return salida

        if v < self.v_min_pos:
            self.v_min_pos, self.d_min = v, d

        if self.estado == "frenando":
            if off:
                if self.off_desde is None:
                    self.off_desde = (t, self.u)
                elif t - self.off_desde[0] >= self.t_off:
                    self.fin_u = self.off_desde[1]
                    self.estado = "post"
            else:
                self.off_desde = None
            return salida

        # estado "post": seguimos buscando la velocidad minima (el apice)
        if on and self.onset is not None:          # otra frenada encadenada
            e = self._cerrar()
            if e:
                salida.append(e)
            self._empezar(d, v, vuelta)
        elif self.u - self.fin_u >= self.post_m or v > self.v_min_pos + 3.0:
            e = self._cerrar()
            if e:
                salida.append(e)
        return salida


# ------------------------------------------------------------- agrupado

def agrupar(eventos_por_vuelta, largo, tol=70.0, frac=0.5):
    """
    Junta frenadas de varias vueltas en curvas. Una curva debe aparecer en al menos
    la mitad de las vueltas. Usa la mediana (robusta a una frenada rara).
    """
    n = len(eventos_por_vuelta)
    if n == 0:
        return []
    todos = sorted(((e["frenar_en"], vl, e)
                    for vl, evs in eventos_por_vuelta.items() for e in evs),
                   key=lambda x: x[0])
    grupos = []
    for item in todos:
        if grupos and item[0] - grupos[-1][0][0] <= tol:
            grupos[-1].append(item)
        else:
            grupos.append([item])
    if len(grupos) > 1 and (grupos[0][0][0] + largo) - grupos[-1][-1][0] <= tol:
        grupos[-1].extend(grupos.pop(0))     # curva que cruza la linea de meta

    minimo = max(1, math.ceil(n * frac))
    curvas = []
    for g in grupos:
        vueltas = {vl for _, vl, _ in g}
        if len(vueltas) < minimo:
            continue
        ref = g[0][0]
        inicio = statistics.median(ref + dif_circ(s, ref, largo) for s, _, _ in g) % largo
        evs = [e for _, _, e in g]
        largo_fr = statistics.median(e["largo_frenada"] for e in evs)
        apice_off = statistics.median((e["apice"] - e["frenar_en"]) % largo for e in evs)
        curvas.append({
            "frenar_en": round(inicio, 1),
            "largo_frenada": round(largo_fr, 1),
            "apice": round((inicio + apice_off) % largo, 1),
            "v_entrada_kmh": round(statistics.median(e["v0"] for e in evs) * 3.6),
            "v_apice_kmh": round(statistics.median(e["vmin"] for e in evs) * 3.6),
            "vueltas": len(vueltas),
        })
    return curvas


def nombrar(curvas, previas, largo, radio=60.0):
    """Ordena por distancia y pone nombre; conserva nombre/nota de una grabacion previa."""
    curvas = sorted(curvas, key=lambda c: c["frenar_en"])
    usadas, salida = set(), []
    for i, c in enumerate(curvas, 1):
        nombre, nota = f"Curva {i}", None
        for j, p in enumerate(previas):
            if j not in usadas and abs(dif_circ(c["frenar_en"], p["frenar_en"], largo)) <= radio:
                nombre, nota = p.get("curva", nombre), p.get("nota")
                usadas.add(j)
                break
        nueva = {"curva": nombre, **c}
        if nota:
            nueva["nota"] = nota
        salida.append(nueva)
    return salida


# -------------------------------------------------------------- grabador

class Grabador:
    """
    Graba frenadas vuelta a vuelta y guarda la tabla al completar cada vuelta.
    La primera vuelta que ve (probablemente incompleta) y las vueltas con paso
    por boxes no cuentan.
    """

    def __init__(self, tabla, pista, variante, auto, largo, modo="pedal", origen="propio"):
        self.tabla, self.pista, self.variante, self.auto = tabla, pista, variante, auto
        self.largo, self.modo, self.origen = float(largo), modo, origen
        self.det = Detector(largo, modo)
        self.eventos = {}        # vuelta -> [evento]
        self.completas = []
        self.sucias = set()
        self.vuelta = None

    def actualizar(self, d, v, freno, t, vuelta, en_pits):
        """Devuelve la cantidad de curvas guardadas si se completo una vuelta valida."""
        guardo = None
        if self.vuelta is None:
            self.vuelta = vuelta
            self.sucias.add(vuelta)              # vuelta de arranque: puede ser parcial
        elif vuelta != self.vuelta:
            if self.vuelta not in self.sucias:
                self.completas.append(self.vuelta)
                guardo = self._guardar()
            self.vuelta = vuelta
        if en_pits:
            self.sucias.add(self.vuelta)
        for e in self.det.actualizar(d, v, freno, t, vuelta):
            self.eventos.setdefault(e["vuelta"], []).append(e)
        return guardo

    def curvas(self):
        evs = {v: e for v, e in self.eventos.items() if v in self.completas}
        return agrupar(evs, self.largo)

    def _guardar(self):
        previas = (self.tabla.entrada(self.pista, self.variante, self.auto) or {}).get("curvas", [])
        curvas = nombrar(self.curvas(), previas, self.largo)
        if not curvas:
            return 0             # no pisamos una tabla existente con una vacia
        for c in curvas:
            c["origen"] = f"{self.origen}, {len(self.completas)} vueltas"
        self.tabla.guardar_entrada(self.pista, self.variante, self.auto, self.largo, curvas)
        return len(curvas)


class GrabadorIA:
    """
    Graba a varios participantes a la vez (por velocidad, sin pedales) y junta las
    frenadas de todos los que usan el mismo auto en una sola entrada de la tabla.
    Cada (participante, vuelta) cuenta como una vuelta; se usa la mediana.
    """

    def __init__(self, tabla):
        self.tabla = tabla
        self.est = {}      # idx -> estado del participante
        self.pool = {}     # (pista, variante, auto) -> eventos y vueltas completas

    def total_vueltas(self):
        return sum(len(p["completas"]) for p in self.pool.values())

    def actualizar(self, m, t):
        """m: Muestra. Devuelve (auto, curvas, vueltas) si se guardo algo."""
        clave = (m.pista, m.variante, m.auto)
        e = self.est.get(m.idx)
        if e is None or e["clave"] != clave:
            e = {"clave": clave, "det": Detector(m.largo, "velocidad"),
                 "vuelta": None, "sucias": set()}
            self.est[m.idx] = e
        pool = self.pool.setdefault(
            clave, {"eventos": {}, "completas": set(), "largo": float(m.largo)})
        guardo = None
        if e["vuelta"] is None:
            e["vuelta"] = m.vueltas
            e["sucias"].add(m.vueltas)             # vuelta de arranque: puede ser parcial
        elif m.vueltas != e["vuelta"]:
            if e["vuelta"] not in e["sucias"]:
                pool["completas"].add((m.idx, e["vuelta"]))
                guardo = self._guardar(clave)
            e["vuelta"] = m.vueltas
        if m.en_pits:
            e["sucias"].add(e["vuelta"])
        for ev in e["det"].actualizar(m.dist, m.vel, None, t, m.vueltas):
            pool["eventos"].setdefault((m.idx, ev["vuelta"]), []).append(ev)
        return guardo

    def _guardar(self, clave):
        pool = self.pool[clave]
        evs = {k: v for k, v in pool["eventos"].items() if k in pool["completas"]}
        previas = (self.tabla.entrada(*clave) or {}).get("curvas", [])
        curvas = nombrar(agrupar(evs, pool["largo"]), previas, pool["largo"])
        if not curvas:
            return None
        autos = len({k[0] for k in pool["completas"]})
        for c in curvas:
            c["origen"] = f"IA, {len(pool['completas'])} vueltas de {autos} autos"
        self.tabla.guardar_entrada(*clave, pool["largo"], curvas)
        return (clave[2], len(curvas), len(pool["completas"]))


# ------------------------------------------------------------------ guia

def estado_guia(curvas, d, largo):
    """
    Proxima curva respecto de la distancia de vuelta d.
      frenando=False -> faltan = metros hasta el punto de frenado.
      frenando=True  -> estamos dentro de la zona; faltan = metros hasta soltar.
    """
    if not curvas or not largo:
        return None
    mejor = None
    for c in curvas:
        ya = (d - c["frenar_en"]) % largo
        if ya <= c["largo_frenada"]:
            return {"curva": c, "frenando": True, "faltan": c["largo_frenada"] - ya}
        delante = (c["frenar_en"] - d) % largo
        if mejor is None or delante < mejor[0]:
            mejor = (delante, c)
    return {"curva": mejor[1], "frenando": False, "faltan": mejor[0]}
