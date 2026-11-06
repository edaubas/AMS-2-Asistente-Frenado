"""Pruebas de la logica con el simulador (no requieren el juego): python -m unittest -v"""
import os
import tempfile
import unittest

from ams2_shm import SharedMemory, muestra
from demo import IA, JUGADOR, LARGO, LectorDemo
from frenado import Grabador, Tabla, agrupar, dif_circ, estado_guia

PISTA, VAR, AUTO = "Demo Circuit", "GP", "Demo GT3"


def grabar(modo, idx, vueltas, ruta, semilla=1):
    lector = LectorDemo(realtime=False, semilla=semilla)
    tabla, g = Tabla(ruta), None
    for _ in range(300000):
        sm = lector.leer()
        m = muestra(sm, idx)
        if g is None:
            g = Grabador(tabla, m.pista, m.variante, m.auto, m.largo, modo)
        g.actualizar(m.dist, m.vel, m.freno, lector.clock(), m.vueltas, m.en_pits)
        if len(g.completas) >= vueltas:
            break
    return tabla, g


class TestLector(unittest.TestCase):
    def test_demo_estructura(self):
        sm = LectorDemo(realtime=False).leer()
        self.assertEqual(sm.mVersion, 14)
        self.assertEqual(sm.mSequenceNumber % 2, 0)
        m = muestra(sm)
        self.assertEqual((m.pista, m.variante, m.auto), (PISTA, VAR, AUTO))
        self.assertIsNotNone(m.freno)
        self.assertIsNone(muestra(sm, 1).freno)     # la IA no trae pedales en el demo

    def test_copia_de_bytes(self):
        sm = LectorDemo(realtime=False).leer()
        sm2 = SharedMemory.from_buffer_copy(bytes(sm))
        self.assertEqual(sm2.mTrackLength, sm.mTrackLength)


class TestGrabacion(unittest.TestCase):
    def _comparar(self, curvas, verdad_inicio, verdad_fin, tol_ini, tol_fin, etiqueta):
        self.assertEqual(len(curvas), 3, curvas)
        for c, (s, e, _), in zip(curvas, verdad_inicio):
            err_ini = dif_circ(c["frenar_en"], s, LARGO)
            err_apice = dif_circ(c["apice"], e, LARGO)
            print(f"  [{etiqueta}] {c['curva']}: frenar_en {c['frenar_en']:.1f} (real {s}) "
                  f"error {err_ini:+.1f} m | apice {c['apice']:.1f} (real {e}) error {err_apice:+.1f} m"
                  f" | frenada {c['largo_frenada']:.0f} m")
            self.assertLessEqual(abs(err_ini), tol_ini)
            self.assertLessEqual(abs(err_apice), tol_fin)

    def test_pedal_propio(self):
        with tempfile.TemporaryDirectory() as d:
            tabla, g = grabar("pedal", 0, 8, os.path.join(d, "t.json"))
            curvas = tabla.entrada(PISTA, VAR, AUTO)["curvas"]
            self._comparar(curvas, JUGADOR, None, 8, 8, "pedal")

    def test_ia_por_velocidad(self):
        with tempfile.TemporaryDirectory() as d:
            tabla, g = grabar("velocidad", 1, 8, os.path.join(d, "t.json"))
            curvas = tabla.entrada(PISTA, VAR, AUTO)["curvas"]
            self._comparar(curvas, IA, None, 12, 12, "velocidad")

    def test_pedal_en_ia_falla(self):
        sm = LectorDemo(realtime=False).leer()
        m = muestra(sm, 1)
        g = Grabador(Tabla(os.devnull + ".x"), m.pista, m.variante, m.auto, m.largo, "pedal")
        with self.assertRaises(ValueError):
            g.actualizar(m.dist, m.vel, m.freno, 0.0, m.vueltas, False)

    def test_conserva_nombres_editados(self):
        with tempfile.TemporaryDirectory() as d:
            ruta = os.path.join(d, "t.json")
            tabla, _ = grabar("pedal", 0, 4, ruta)
            ent = tabla.entrada(PISTA, VAR, AUTO)
            ent["curvas"][0]["curva"] = "La Horquilla"
            tabla.guardar()
            tabla2, _ = grabar("pedal", 0, 4, ruta, semilla=2)
            nombres = [c["curva"] for c in tabla2.entrada(PISTA, VAR, AUTO)["curvas"]]
            self.assertEqual(nombres[0], "La Horquilla")
            self.assertTrue(os.path.exists(ruta + ".bak"))


class TestGrabadorIA(unittest.TestCase):
    def test_varias_ia_a_la_vez(self):
        from frenado import GrabadorIA
        with tempfile.TemporaryDirectory() as d:
            tabla = Tabla(os.path.join(d, "t.json"))
            lector, g = LectorDemo(realtime=False), GrabadorIA(tabla)
            for _ in range(300000):
                sm = lector.leer()
                for i in (0, 1):        # el demo solo tiene 2 participantes
                    g.actualizar(muestra(sm, i), lector.clock())
                if g.total_vueltas() >= 12:
                    break
            ent = tabla.entrada(PISTA, VAR, AUTO)
            self.assertEqual(len(ent["curvas"]), 3)
            self.assertIn("2 autos", ent["curvas"][0]["origen"])
            for c, (s, _, _) in zip(ent["curvas"], JUGADOR):
                # mezcla jugador (440...) e IA (455...): la mediana queda entre ambos
                self.assertLessEqual(abs(dif_circ(c["frenar_en"], s + 8, LARGO)), 14)


class TestAgrupar(unittest.TestCase):
    def test_curva_en_linea_de_meta(self):
        ev = lambda s: {"frenar_en": s, "largo_frenada": 100.0, "apice": (s + 100) % 3000,
                        "v0": 60.0, "vmin": 20.0}
        por_vuelta = {1: [ev(2990)], 2: [ev(5)], 3: [ev(2995)]}
        curvas = agrupar(por_vuelta, 3000.0)
        self.assertEqual(len(curvas), 1)
        self.assertLessEqual(abs(dif_circ(curvas[0]["frenar_en"], 2995, 3000.0)), 6)

    def test_descarta_evento_aislado(self):
        ev = lambda s: {"frenar_en": s, "largo_frenada": 100.0, "apice": s + 100,
                        "v0": 60.0, "vmin": 20.0}
        por_vuelta = {1: [ev(500)], 2: [ev(505), ev(1500)], 3: [ev(498)], 4: [ev(502)]}
        curvas = agrupar(por_vuelta, 3000.0)
        self.assertEqual([round(c["frenar_en"]) for c in curvas], [501])


class TestGuia(unittest.TestCase):
    C = [{"curva": "A", "frenar_en": 2900.0, "largo_frenada": 150.0},
         {"curva": "B", "frenar_en": 1000.0, "largo_frenada": 100.0}]

    def test_antes_de_la_zona(self):
        e = estado_guia(self.C, 2800.0, 3000.0)
        self.assertEqual((e["curva"]["curva"], e["frenando"], round(e["faltan"])), ("A", False, 100))

    def test_dentro_zona_cruzando_meta(self):
        e = estado_guia(self.C, 2950.0, 3000.0)
        self.assertTrue(e["frenando"])
        self.assertEqual(round(e["faltan"]), 100)
        e = estado_guia(self.C, 40.0, 3000.0)       # ya cruzo la meta, aun dentro de la zona
        self.assertTrue(e["frenando"])
        self.assertEqual(round(e["faltan"]), 10)

    def test_despues_de_la_zona(self):
        e = estado_guia(self.C, 100.0, 3000.0)
        self.assertEqual((e["curva"]["curva"], e["frenando"], round(e["faltan"])), ("B", False, 900))


if __name__ == "__main__":
    unittest.main()
