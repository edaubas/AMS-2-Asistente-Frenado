"""
Simulador de la memoria compartida para probar sin el juego (--demo).

OJO: es un modelo propio y muy simple (frenadas a desaceleracion constante, pista de
3000 m con 3 curvas). Sirve para probar la logica del programa, no representa la
fisica ni la IA reales de AMS2. Los pedales existen solo para el participante 0
(el "jugador"); el participante 1 es una "IA" con velocidad y distancia nada mas.
"""
import random
import time

from ams2_shm import SHARED_MEMORY_VERSION, SharedMemory

LARGO = 3000.0
VMAX = 70.0          # m/s
ACEL = 6.0           # m/s2
# (metro donde empieza el freno, metro donde termina, velocidad minima m/s)
JUGADOR = [(440, 590, 25), (1200, 1340, 30), (2250, 2400, 22)]
IA = [(455, 600, 26), (1215, 1345, 31), (2270, 2405, 23)]


class _Auto:
    def __init__(self, zonas, jitter, rng):
        self.zonas, self.jitter, self.rng = zonas, jitter, rng
        self.d, self.v, self.vueltas = 0.0, 40.0, 0
        self.freno = self.gas = 0.0
        self._sorteo()

    def _sorteo(self):
        j = self.jitter
        self.zl = [(s + self.rng.uniform(-j, j), e, vm) for s, e, vm in self.zonas]

    def paso(self, dt):
        zona = next(((s, e, vm) for s, e, vm in self.zl if s <= self.d < e), None)
        a, self.freno, self.gas = 0.0, 0.0, 0.0
        if zona:
            s, e, vm = zona
            if self.v > vm:
                a = -(self.v ** 2 - vm ** 2) / (2 * max(e - self.d, 1.0))
                self.freno = min(1.0, max(0.3, -a / 15.0))
        elif self.v < VMAX:
            a, self.gas = ACEL, 1.0
        else:
            self.gas = 0.9
        self.v = max(5.0, self.v + a * dt)
        self.d += self.v * dt
        if self.d >= LARGO:
            self.d -= LARGO
            self.vueltas += 1
            self._sorteo()


class LectorDemo:
    def __init__(self, realtime=True, semilla=1):
        rng = random.Random(semilla)
        self.jug = _Auto(JUGADOR, 6.0, rng)
        self.ia = _Auto(IA, 3.0, rng)
        self.t, self.dt, self.n = 0.0, 1 / 30, 0
        self.realtime = realtime

    def clock(self):
        return self.t

    def dormir(self, seg):
        if self.realtime:
            time.sleep(seg)

    def leer(self):
        self.t += self.dt
        self.n += 1
        self.jug.paso(self.dt)
        self.ia.paso(self.dt)
        sm = SharedMemory()
        sm.mVersion = SHARED_MEMORY_VERSION
        sm.mSequenceNumber = 2 * self.n
        sm.mGameState, sm.mSessionState = 2, 1
        sm.mViewedParticipantIndex, sm.mNumParticipants = 0, 2
        sm.mTrackLocation, sm.mTrackVariation = b"Demo Circuit", b"GP"
        sm.mTrackLength = LARGO
        sm.mCarName = b"Demo GT3"
        sm.mBrake, sm.mUnfilteredBrake = self.jug.freno, self.jug.freno
        sm.mThrottle = self.jug.gas
        sm.mSpeed = self.jug.v
        for i, (auto, nombre) in enumerate(((self.jug, b"Jugador"), (self.ia, b"IA 1"))):
            p = sm.mParticipantInfo[i]
            p.mIsActive = True
            p.mName = nombre
            p.mCurrentLapDistance = auto.d
            p.mLapsCompleted = p.mCurrentLap = auto.vueltas
            p.mRacePosition = i + 1
            sm.mSpeeds[i] = auto.v
            sm.mCarNames[i].value = b"Demo GT3"
        return sm
