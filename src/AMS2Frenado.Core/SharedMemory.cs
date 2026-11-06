// Port a C# de SharedMemory.h v14 (AMS2 / Project CARS 2), campo por campo desde
// prototipo/ams2_shm.py. Los nombres de campo son los del header en C.
//
// Alineamiento: todos los campos son de 1 o 4 bytes (no hay double ni int64), asi que el
// layout natural del C (y de ctypes) coincide con Pack=4; el tamano total es 20 700 bytes.
// Los bool de C son 1 byte: se representan con byte para que el struct sea blittable
// (un bool de C# se marshalearia como 4 bytes).
// Los arrays fijos usan InlineArray (ver Arreglos.cs).
// Sin probar en AMS2: el layout esta verificado contra el prototipo, no contra el juego real.
using System.Runtime.InteropServices;

namespace AMS2Frenado.Core;

public static class ConstantesShm
{
    public const uint Version = 14;
    public const string NombreMapa = "$pcars2$";
    public const int TamanoBytes = 20700;
    public const int MaxParticipantes = 64;
    public const int LargoTexto = 64;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ParticipanteInfo
{
    public byte mIsActive; // bool de C (1 byte)
    public Arr64<byte> mName;
    public Arr3<float> mWorldPosition;
    public float mCurrentLapDistance;
    public uint mRacePosition;
    public uint mLapsCompleted;
    public uint mCurrentLap;
    public int mCurrentSector;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct SharedMemory
{
    public uint mVersion;
    public uint mBuildVersionNumber;
    public uint mGameState;
    public uint mSessionState;
    public uint mRaceState;
    public int mViewedParticipantIndex;
    public int mNumParticipants;
    public Arr64<ParticipanteInfo> mParticipantInfo;
    public float mUnfilteredThrottle;
    public float mUnfilteredBrake;
    public float mUnfilteredSteering;
    public float mUnfilteredClutch;
    public Arr64<byte> mCarName;
    public Arr64<byte> mCarClassName;
    public uint mLapsInEvent;
    public Arr64<byte> mTrackLocation;
    public Arr64<byte> mTrackVariation;
    public float mTrackLength;
    public int mNumSectors;
    public byte mLapInvalidated; // bool de C (1 byte)
    public float mBestLapTime;
    public float mLastLapTime;
    public float mCurrentTime;
    public float mSplitTimeAhead;
    public float mSplitTimeBehind;
    public float mSplitTime;
    public float mEventTimeRemaining;
    public float mPersonalFastestLapTime;
    public float mWorldFastestLapTime;
    public float mCurrentSector1Time;
    public float mCurrentSector2Time;
    public float mCurrentSector3Time;
    public float mFastestSector1Time;
    public float mFastestSector2Time;
    public float mFastestSector3Time;
    public float mPersonalFastestSector1Time;
    public float mPersonalFastestSector2Time;
    public float mPersonalFastestSector3Time;
    public float mWorldFastestSector1Time;
    public float mWorldFastestSector2Time;
    public float mWorldFastestSector3Time;
    public uint mHighestFlagColour;
    public uint mHighestFlagReason;
    public uint mPitMode;
    public uint mPitSchedule;
    public uint mCarFlags;
    public float mOilTempCelsius;
    public float mOilPressureKPa;
    public float mWaterTempCelsius;
    public float mWaterPressureKPa;
    public float mFuelPressureKPa;
    public float mFuelLevel;
    public float mFuelCapacity;
    public float mSpeed;
    public float mRpm;
    public float mMaxRPM;
    public float mBrake;
    public float mThrottle;
    public float mClutch;
    public float mSteering;
    public int mGear;
    public int mNumGears;
    public float mOdometerKM;
    public byte mAntiLockActive; // bool de C (1 byte)
    public int mLastOpponentCollisionIndex;
    public float mLastOpponentCollisionMagnitude;
    public byte mBoostActive; // bool de C (1 byte)
    public float mBoostAmount;
    public Arr3<float> mOrientation;
    public Arr3<float> mLocalVelocity;
    public Arr3<float> mWorldVelocity;
    public Arr3<float> mAngularVelocity;
    public Arr3<float> mLocalAcceleration;
    public Arr3<float> mWorldAcceleration;
    public Arr3<float> mExtentsCentre;
    public Arr4<uint> mTyreFlags;
    public Arr4<uint> mTerrain;
    public Arr4<float> mTyreY;
    public Arr4<float> mTyreRPS;
    public Arr4<float> mTyreSlipSpeed;
    public Arr4<float> mTyreTemp;
    public Arr4<float> mTyreGrip;
    public Arr4<float> mTyreHeightAboveGround;
    public Arr4<float> mTyreLateralStiffness;
    public Arr4<float> mTyreWear;
    public Arr4<float> mBrakeDamage;
    public Arr4<float> mSuspensionDamage;
    public Arr4<float> mBrakeTempCelsius;
    public Arr4<float> mTyreTreadTemp;
    public Arr4<float> mTyreLayerTemp;
    public Arr4<float> mTyreCarcassTemp;
    public Arr4<float> mTyreRimTemp;
    public Arr4<float> mTyreInternalAirTemp;
    public uint mCrashState;
    public float mAeroDamage;
    public float mEngineDamage;
    public float mAmbientTemperature;
    public float mTrackTemperature;
    public float mRainDensity;
    public float mWindSpeed;
    public float mWindDirectionX;
    public float mWindDirectionY;
    public float mCloudBrightness;
    public uint mSequenceNumber;
    public Arr4<float> mWheelLocalPositionY;
    public Arr4<float> mSuspensionTravel;
    public Arr4<float> mSuspensionVelocity;
    public Arr4<float> mAirPressure;
    public float mEngineSpeed;
    public float mEngineTorque;
    public Arr2<float> mWings;
    public float mHandBrake;
    public Arr64<float> mCurrentSector1Times;
    public Arr64<float> mCurrentSector2Times;
    public Arr64<float> mCurrentSector3Times;
    public Arr64<float> mFastestSector1Times;
    public Arr64<float> mFastestSector2Times;
    public Arr64<float> mFastestSector3Times;
    public Arr64<float> mFastestLapTimes;
    public Arr64<float> mLastLapTimes;
    public Arr64<byte> mLapsInvalidated;
    public Arr64<uint> mRaceStates;
    public Arr64<uint> mPitModes;
    public Arr64<Arr3<float>> mOrientations;
    public Arr64<float> mSpeeds;
    public Arr64<Arr64<byte>> mCarNames;
    public Arr64<Arr64<byte>> mCarClassNames;
    public int mEnforcedPitStopLap;
    public Arr64<byte> mTranslatedTrackLocation;
    public Arr64<byte> mTranslatedTrackVariation;
    public float mBrakeBias;
    public float mTurboBoostPressure;
    public Arr4<Arr40<byte>> mTyreCompound;
    public Arr64<uint> mPitSchedules;
    public Arr64<uint> mHighestFlagColours;
    public Arr64<uint> mHighestFlagReasons;
    public Arr64<uint> mNationalities;
    public float mSnowDensity;
    public float mSessionDuration;
    public int mSessionAdditionalLaps;
    public Arr4<float> mTyreTempLeft;
    public Arr4<float> mTyreTempCenter;
    public Arr4<float> mTyreTempRight;
    public uint mDrsState;
    public Arr4<float> mRideHeight;
    public uint mJoyPad0;
    public uint mDPad;
    public int mAntiLockSetting;
    public int mTractionControlSetting;
    public int mErsDeploymentMode;
    public byte mErsAutoModeEnabled; // bool de C (1 byte)
    public float mClutchTemp;
    public float mClutchWear;
    public byte mClutchOverheated; // bool de C (1 byte)
    public byte mClutchSlipping; // bool de C (1 byte)
    public int mYellowFlagState;
    public byte mSessionIsPrivate; // bool de C (1 byte)
    public int mLaunchStage;
}
