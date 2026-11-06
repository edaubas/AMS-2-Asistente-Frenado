"""
Compila SharedMemory.h (el del juego) con g++/gcc y compara tamanio y offset de cada
campo contra la definicion de ams2_shm.py. Sirve para detectar un header distinto
(por ejemplo si Reiza sube la version) antes de leer datos basura.

Uso:  python verificar_offsets.py "C:\\...\\AMS2_SharedMemoryExampleApp\\SharedMemory.h"
Requiere un compilador de C++ (g++ o MSVC no; en Windows: MinGW g++).
"""
import ctypes
import os
import shutil
import subprocess
import sys
import tempfile

from ams2_shm import ParticipantInfo, SharedMemory


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    header = os.path.abspath(sys.argv[1])
    cxx = shutil.which("g++") or shutil.which("c++")
    if not cxx:
        print("No encontre g++. Instala MinGW o corre esto en Linux/WSL.")
        return 2

    lineas = ['#include <cstddef>', '#include <cstdio>', f'#include "{os.path.basename(header)}"',
              'int main(){',
              'printf("SIZE SharedMemory %zu\\n", sizeof(SharedMemory));',
              'printf("SIZE ParticipantInfo %zu\\n", sizeof(ParticipantInfo));']
    for clase, tipo in ((SharedMemory, "SharedMemory"), (ParticipantInfo, "ParticipantInfo")):
        for nombre, _ in clase._fields_:
            lineas.append(f'printf("OFF {tipo} {nombre} %zu %zu\\n", '
                          f'offsetof({tipo}, {nombre}), sizeof((({tipo}*)0)->{nombre}));')
    lineas.append("return 0;}")

    with tempfile.TemporaryDirectory() as tmp:
        shutil.copyfile(header, os.path.join(tmp, os.path.basename(header)))
        fuente, exe = os.path.join(tmp, "chk.cpp"), os.path.join(tmp, "chk")
        with open(fuente, "w") as f:
            f.write("\n".join(lineas))
        r = subprocess.run([cxx, "-Wno-invalid-offsetof", "-o", exe, fuente],
                           capture_output=True, text=True)
        if r.returncode:
            print("Error compilando:\n", r.stderr[-1500:])
            return 2
        salida = subprocess.run([exe], capture_output=True, text=True).stdout.splitlines()

    errores, n = [], 0
    for linea in salida:
        p = linea.split()
        if p[0] == "SIZE":
            real = ctypes.sizeof(SharedMemory if p[1] == "SharedMemory" else ParticipantInfo)
            n += 1
            if int(p[2]) != real:
                errores.append(f"tamanio {p[1]}: header={p[2]} python={real}")
        else:
            clase = SharedMemory if p[1] == "SharedMemory" else ParticipantInfo
            campo = getattr(clase, p[2])
            n += 1
            if (int(p[3]), int(p[4])) != (campo.offset, campo.size):
                errores.append(f"{p[1]}.{p[2]}: header off/tam={p[3]}/{p[4]} "
                               f"python={campo.offset}/{campo.size}")
    if errores:
        print(f"DIFERENCIAS ({len(errores)}):")
        print("\n".join(errores[:30]))
        return 1
    print(f"OK: {n} comprobaciones, sizeof(SharedMemory)={ctypes.sizeof(SharedMemory)} bytes, "
          f"coinciden con el header.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
