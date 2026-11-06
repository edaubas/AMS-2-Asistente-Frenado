"""
Lectura de la memoria compartida de Automobilista 2.

La estructura SharedMemory esta copiada campo por campo de SharedMemory.h
(SHARED_MEMORY_VERSION = 14). verificar_offsets.py compila el header real y
compara offsets y tamanios contra esta definicion.
"""
import ctypes
import sys
import time
from collections import namedtuple

SHARED_MEMORY_VERSION = 14
MAP_NAME = "$pcars2$"
STRING_LENGTH_MAX = 64
STORED_PARTICIPANTS_MAX = 64
TYRE_COMPOUND_NAME_LENGTH_MAX = 40
TYRE_MAX = 4
VEC_MAX = 3

GAME_INGAME_PLAYING = 2
GAME_INGAME_REPLAY = 6
ESTADOS_ACTIVOS = (GAME_INGAME_PLAYING, GAME_INGAME_REPLAY)
NOMBRES_ESTADO = ["salido", "menu", "jugando", "pausa", "menu (tiempo corre)",
                  "reiniciando", "replay", "menu de replay"]

_f, _u, _i, _b = ctypes.c_float, ctypes.c_uint, ctypes.c_int, ctypes.c_bool
_S = ctypes.c_char * STRING_LENGTH_MAX
_P = STORED_PARTICIPANTS_MAX
_f2, _f3, _f4 = _f * 2, _f * VEC_MAX, _f * TYRE_MAX
_u4 = _u * TYRE_MAX
_fP, _uP = _f * _P, _u * _P


class ParticipantInfo(ctypes.Structure):
    _fields_ = [
        ("mIsActive", _b),
        ("mName", _S),
        ("mWorldPosition", _f3),
        ("mCurrentLapDistance", _f),
        ("mRacePosition", _u),
        ("mLapsCompleted", _u),
        ("mCurrentLap", _u),
        ("mCurrentSector", _i),
    ]


class SharedMemory(ctypes.Structure):
    _fields_ = [
        ("mVersion", _u), ("mBuildVersionNumber", _u),
        ("mGameState", _u), ("mSessionState", _u), ("mRaceState", _u),
        ("mViewedParticipantIndex", _i), ("mNumParticipants", _i),
        ("mParticipantInfo", ParticipantInfo * _P),
        ("mUnfilteredThrottle", _f), ("mUnfilteredBrake", _f),
        ("mUnfilteredSteering", _f), ("mUnfilteredClutch", _f),
        ("mCarName", _S), ("mCarClassName", _S),
        ("mLapsInEvent", _u),
        ("mTrackLocation", _S), ("mTrackVariation", _S), ("mTrackLength", _f),
        ("mNumSectors", _i), ("mLapInvalidated", _b),
        ("mBestLapTime", _f), ("mLastLapTime", _f), ("mCurrentTime", _f),
        ("mSplitTimeAhead", _f), ("mSplitTimeBehind", _f), ("mSplitTime", _f),
        ("mEventTimeRemaining", _f), ("mPersonalFastestLapTime", _f),
        ("mWorldFastestLapTime", _f),
        ("mCurrentSector1Time", _f), ("mCurrentSector2Time", _f), ("mCurrentSector3Time", _f),
        ("mFastestSector1Time", _f), ("mFastestSector2Time", _f), ("mFastestSector3Time", _f),
        ("mPersonalFastestSector1Time", _f), ("mPersonalFastestSector2Time", _f),
        ("mPersonalFastestSector3Time", _f),
        ("mWorldFastestSector1Time", _f), ("mWorldFastestSector2Time", _f),
        ("mWorldFastestSector3Time", _f),
        ("mHighestFlagColour", _u), ("mHighestFlagReason", _u),
        ("mPitMode", _u), ("mPitSchedule", _u),
        ("mCarFlags", _u),
        ("mOilTempCelsius", _f), ("mOilPressureKPa", _f),
        ("mWaterTempCelsius", _f), ("mWaterPressureKPa", _f),
        ("mFuelPressureKPa", _f), ("mFuelLevel", _f), ("mFuelCapacity", _f),
        ("mSpeed", _f), ("mRpm", _f), ("mMaxRPM", _f),
        ("mBrake", _f), ("mThrottle", _f), ("mClutch", _f), ("mSteering", _f),
        ("mGear", _i), ("mNumGears", _i),
        ("mOdometerKM", _f), ("mAntiLockActive", _b),
        ("mLastOpponentCollisionIndex", _i), ("mLastOpponentCollisionMagnitude", _f),
        ("mBoostActive", _b), ("mBoostAmount", _f),
        ("mOrientation", _f3), ("mLocalVelocity", _f3), ("mWorldVelocity", _f3),
        ("mAngularVelocity", _f3), ("mLocalAcceleration", _f3),
        ("mWorldAcceleration", _f3), ("mExtentsCentre", _f3),
        ("mTyreFlags", _u4), ("mTerrain", _u4),
        ("mTyreY", _f4), ("mTyreRPS", _f4), ("mTyreSlipSpeed", _f4), ("mTyreTemp", _f4),
        ("mTyreGrip", _f4), ("mTyreHeightAboveGround", _f4), ("mTyreLateralStiffness", _f4),
        ("mTyreWear", _f4), ("mBrakeDamage", _f4), ("mSuspensionDamage", _f4),
        ("mBrakeTempCelsius", _f4), ("mTyreTreadTemp", _f4), ("mTyreLayerTemp", _f4),
        ("mTyreCarcassTemp", _f4), ("mTyreRimTemp", _f4), ("mTyreInternalAirTemp", _f4),
        ("mCrashState", _u), ("mAeroDamage", _f), ("mEngineDamage", _f),
        ("mAmbientTemperature", _f), ("mTrackTemperature", _f), ("mRainDensity", _f),
        ("mWindSpeed", _f), ("mWindDirectionX", _f), ("mWindDirectionY", _f),
        ("mCloudBrightness", _f),
        # PCars2 additions (version 8)
        ("mSequenceNumber", _u),
        ("mWheelLocalPositionY", _f4), ("mSuspensionTravel", _f4),
        ("mSuspensionVelocity", _f4), ("mAirPressure", _f4),
        ("mEngineSpeed", _f), ("mEngineTorque", _f), ("mWings", _f2), ("mHandBrake", _f),
        ("mCurrentSector1Times", _fP), ("mCurrentSector2Times", _fP),
        ("mCurrentSector3Times", _fP), ("mFastestSector1Times", _fP),
        ("mFastestSector2Times", _fP), ("mFastestSector3Times", _fP),
        ("mFastestLapTimes", _fP), ("mLastLapTimes", _fP),
        ("mLapsInvalidated", _b * _P),
        ("mRaceStates", _uP), ("mPitModes", _uP),
        ("mOrientations", _f3 * _P),
        ("mSpeeds", _fP),
        ("mCarNames", _S * _P), ("mCarClassNames", _S * _P),
        ("mEnforcedPitStopLap", _i),
        ("mTranslatedTrackLocation", _S), ("mTranslatedTrackVariation", _S),
        ("mBrakeBias", _f), ("mTurboBoostPressure", _f),
        ("mTyreCompound", (ctypes.c_char * TYRE_COMPOUND_NAME_LENGTH_MAX) * TYRE_MAX),
        ("mPitSchedules", _uP), ("mHighestFlagColours", _uP),
        ("mHighestFlagReasons", _uP), ("mNationalities", _uP),
        ("mSnowDensity", _f),
        # AMS2 additions (v10...)
        ("mSessionDuration", _f), ("mSessionAdditionalLaps", _i),
        ("mTyreTempLeft", _f4), ("mTyreTempCenter", _f4), ("mTyreTempRight", _f4),
        ("mDrsState", _u),
        ("mRideHeight", _f4),
        ("mJoyPad0", _u), ("mDPad", _u),
        ("mAntiLockSetting", _i), ("mTractionControlSetting", _i),
        ("mErsDeploymentMode", _i), ("mErsAutoModeEnabled", _b),
        ("mClutchTemp", _f), ("mClutchWear", _f),
        ("mClutchOverheated", _b), ("mClutchSlipping", _b),
        ("mYellowFlagState", _i),
        ("mSessionIsPrivate", _b),
        ("mLaunchStage", _i),
    ]


def txt(x):
    """bytes / array de char -> str (corta en el primer NUL)."""
    if hasattr(x, "value"):
        x = x.value
    return x.split(b"\0")[0].decode("utf-8", "replace").strip()


class VersionDistinta(Exception):
    def __init__(self, version):
        super().__init__(f"Version de memoria compartida {version} "
                         f"(este programa lee la {SHARED_MEMORY_VERSION})")
        self.version = version


class LectorWindows:
    """Lee $pcars2$ con la API de Windows. Devuelve None si el juego no la publico aun."""
    realtime = True

    def __init__(self):
        if sys.platform != "win32":
            raise OSError("La memoria compartida de AMS2 solo se lee en Windows "
                          "(para probar sin el juego usa --demo).")
        from ctypes import wintypes
        k = ctypes.WinDLL("kernel32", use_last_error=True)
        k.OpenFileMappingW.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.LPCWSTR]
        k.OpenFileMappingW.restype = wintypes.HANDLE
        k.MapViewOfFile.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.DWORD,
                                    wintypes.DWORD, ctypes.c_size_t]
        k.MapViewOfFile.restype = ctypes.c_void_p
        k.CloseHandle.argtypes = [wintypes.HANDLE]
        self._k = k
        self._ptr = None
        self._seq_off = SharedMemory.mSequenceNumber.offset
        self._n = ctypes.sizeof(SharedMemory)

    def _abrir(self):
        FILE_MAP_READ = 0x0004
        h = self._k.OpenFileMappingW(FILE_MAP_READ, False, MAP_NAME)
        if not h:
            return False
        p = self._k.MapViewOfFile(h, FILE_MAP_READ, 0, 0, 0)
        if not p:
            self._k.CloseHandle(h)
            return False
        self._ptr = p
        return True

    def leer(self):
        if self._ptr is None and not self._abrir():
            return None
        ver = ctypes.c_uint.from_address(self._ptr).value
        if ver == 0:
            return None          # el juego todavia no inicializo el bloque
        if ver != SHARED_MEMORY_VERSION:
            raise VersionDistinta(ver)   # no leemos mas bytes: el layout podria diferir
        buf = b""
        for _ in range(6):       # mSequenceNumber: par = bloque estable
            s1 = ctypes.c_uint.from_address(self._ptr + self._seq_off).value
            buf = ctypes.string_at(self._ptr, self._n)
            s2 = ctypes.c_uint.from_address(self._ptr + self._seq_off).value
            if s1 == s2 and s1 % 2 == 0:
                break
        return SharedMemory.from_buffer_copy(buf)

    def clock(self):
        return time.monotonic()

    def dormir(self, seg):
        time.sleep(seg)


Muestra = namedtuple(
    "Muestra", "idx nombre dist vel freno vueltas en_pits auto pista variante largo")


def jugador(sm):
    """Indice del participante que se esta viendo (el jugador en conduccion normal)."""
    i = sm.mViewedParticipantIndex
    return i if 0 <= i < STORED_PARTICIPANTS_MAX else 0


def muestra(sm, idx=None):
    """Datos de un participante. Los pedales solo existen para el auto visto."""
    vista = jugador(sm)
    if idx is None:
        idx = vista
    es_vista = idx == vista
    p = sm.mParticipantInfo[idx]
    vel = sm.mSpeed if es_vista else sm.mSpeeds[idx]
    freno = sm.mBrake if es_vista else None
    pit = sm.mPitMode if es_vista else sm.mPitModes[idx]
    auto = (txt(sm.mCarName) if es_vista else "") or txt(sm.mCarNames[idx]) \
        or txt(sm.mCarName)
    return Muestra(idx, txt(p.mName), p.mCurrentLapDistance, vel, freno,
                   p.mLapsCompleted, pit != 0, auto, txt(sm.mTrackLocation),
                   txt(sm.mTrackVariation), sm.mTrackLength)
