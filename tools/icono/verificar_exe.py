#!/usr/bin/env python3
"""Revisa que el .exe publicado traiga el icono (recursos RT_GROUP_ICON / RT_ICON). Uso: python3 tools/icono/verificar_exe.py AMS2Frenado.exe  (pip install pefile)"""
import struct, sys
import pefile

pe = pefile.PE(sys.argv[1])
tamanos, iconos = [], 0
for tipo in pe.DIRECTORY_ENTRY_RESOURCE.entries:
    if tipo.id == 3: iconos = sum(len(n.directory.entries) for n in tipo.directory.entries)
    if tipo.id == 14:
        for nombre in tipo.directory.entries:
            for idioma in nombre.directory.entries:
                d = pe.get_data(idioma.data.struct.OffsetToData, idioma.data.struct.Size)
                tamanos = [d[6 + 14 * i] or 256 for i in range(struct.unpack("<H", d[4:6])[0])]
print(f"RT_ICON: {iconos}; grupo de iconos: {tamanos}")
sys.exit(0 if tamanos == [16, 24, 32, 48, 64, 128, 256] else 1)
