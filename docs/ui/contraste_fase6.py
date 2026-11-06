"""Contraste WCAG 2.x de la fase 6 (rediseño). Uso: python3 docs/ui/contraste_fase6.py
Calcula contraste de texto y de componentes con los colores NUEVOS, incluida la textura de carbono (peor caso = pico de textura)."""
import numpy as np
def h(x): x=x.lstrip('#'); return np.array([int(x[i:i+2],16) for i in (0,2,4)],float)
def lin(c): c=np.asarray(c)/255; return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def L(c): r,g,b=lin(c); return 0.2126*r+0.7152*g+0.0722*b
def cr(a,b):
    la,lb=L(a),L(b)
    if la<lb: la,lb=lb,la
    return (la+0.05)/(lb+0.05)
def over(fg,a,bg): return fg*a+bg*(1-a)
def hx(c): return '#%02X%02X%02X'%tuple(int(round(v)) for v in c)

BASE=h('0C0E11'); WHITE=h('FFFFFF')
TEX_PICO=0.04            # alfa máximo de la línea de textura (blanco) sobre la base
BASE_TEX=over(WHITE,TEX_PICO,BASE)   # base en el punto más claro de la textura
FONDOS={'negro':h('000000'),'cielo lienzo #5B6E7C':h('5B6E7C'),'piano #C9CDD1':h('C9CDD1'),'cielo claro #BFD8EE':h('BFD8EE'),'blanco (peor)':h('FFFFFF')}
TEXTOS=[('Principal / cifras #FFFFFF','FFFFFF'),('Título #EEF1F4','EEF1F4'),('Secundario #DDE2E7','DDE2E7'),('SIN DATOS #C3CAD1','C3CAD1'),
 ('Apoyo #A9B2BC','A9B2BC'),('Ámbar inminente #FFC24D','FFC24D'),('Naranja IA #FF9A3D','FF9A3D'),('Azul VOS #5AA9FF','5AA9FF'),
 ('Etiqueta roja chica #FF8A80','FF8A80'),('Aviso/chip rojo #FF9C94','FF9C94')]
print('## A. Texto sobre panel de guía (base #0C0E11 + pico de textura 4 %), alfa de fondo 0,80 / 0,85 / 0,88 / 0,95')
for a in (0.80,0.85,0.88,0.95):
    print(f'\nalfa {a}:  fondo | '+' | '.join(FONDOS))
    for n,c in TEXTOS:
        row=[cr(h(c),over(BASE_TEX,a,bg)) for bg in FONDOS.values()]
        print(f'| {n} | '+' | '.join(f'{x:.2f}' for x in row)+f' | mín {min(row):.2f}')
print('\nEfecto de la textura (blanco puro sobre blanco, alfa 0,85): sin textura',round(cr(WHITE,over(BASE,0.85,WHITE)),2),'con pico',round(cr(WHITE,over(BASE_TEX,0.85,WHITE)),2))
print('\nAlfa mínimo del fondo (sobre blanco, con pico de textura) para llegar a 4,5:1 por color:')
for n,c in [('Apoyo A9B2BC','A9B2BC'),('Azul 5AA9FF','5AA9FF'),('Naranja FF9A3D','FF9A3D'),('Rojo etiqueta FF8A80','FF8A80')]:
    print(' ',n,next(x/100 for x in range(50,101) if cr(h(c),over(BASE_TEX,x/100,WHITE))>=4.5))
print('\n## B. Superficies opacas (menú y botones; no dependen del juego)')
SUP={'menú #0F1216':'0F1216','tarjeta del menú #161A1F':'161A1F','hover #222831':'222831','presionado #2F3842':'2F3842'}
for sn,s in SUP.items():
    print(f'\nsobre {sn}')
    for n,c in [('#FFFFFF','FFFFFF'),('#EEF1F4','EEF1F4'),('#DDE2E7','DDE2E7'),('#A9B2BC','A9B2BC'),('rojo chico #FF8A80','FF8A80'),('desactivado #7C8794','7C8794'),('naranja','FF9A3D'),('azul','5AA9FF')]:
        print(f'  {n}: {cr(h(c),h(s)):.2f}')
print('\n## C. Pares con fondo propio')
pares=[('FRENÁ: blanco sobre rojo #C62F26 (plano, sin textura)','FFFFFF','C62F26'),
('FRENÁ: blanco sobre rojo al 0,92 sobre blanco',None,None),
('TOQUE/LEVANTÁ: blanco sobre #3B4652','FFFFFF','3B4652'),
('Opción elegida del segmentado: #0C0E11 sobre #FFFFFF','0C0E11','FFFFFF'),
('Opción no elegida: #DDE2E7 sobre #161A1F','DDE2E7','161A1F'),
('Botón CERRAR: blanco sobre #C62F26','FFFFFF','C62F26'),
('Botón CERRAR: blanco sobre #C62F26 en hover (#D8382E)','FFFFFF','D8382E'),
('Interruptor ON: tilde #C62F26 sobre perilla blanca','C62F26','FFFFFF'),
('Tecla "Ctrl+Alt+L" #EEF1F4 sobre #2A3139','EEF1F4','2A3139'),
('Chip MOSTRAR #8EE0A9 sobre #1E3A2A','8EE0A9','1E3A2A'),
('Chip GRABANDO #FF9C94 sobre #4A1F1C','FF9C94','4A1F1C'),
('Chip LIBRE #8CC3FF sobre #17304D','8CC3FF','17304D'),
('Símbolo de aviso #E53935 contra su contorno #0C0E11','E53935','0C0E11'),
('Símbolo de aviso #E53935 sobre cielo claro #BFD8EE (sin contorno)','E53935','BFD8EE')]
for n,f,b in pares:
    if f is None:
        bg=over(h('C62F26'),0.92,WHITE); print(f'| {n} | {cr(WHITE,bg):.2f} |'); continue
    print(f'| {n} | {cr(h(f),h(b)):.2f} |')
print('\n## D. Componentes gráficos (WCAG 1.4.11, mínimo 3:1)')
comp=[('Borde de botón #5A6673 sobre menú #0F1216','5A6673','0F1216'),
('Icono de botón #DDE2E7 sobre #161A1F','DDE2E7','161A1F'),
('Icono de botón #DDE2E7 sobre #222831 (hover)','DDE2E7','222831'),
('Icono pin fijado #FF8A80 sobre #3A1614','FF8A80','3A1614'),
('Icono ⇄ (discreto, reposo) #A9B2BC sobre panel #0C0E11','A9B2BC','0C0E11'),
('Icono ✕ (discreto, reposo) #A9B2BC sobre panel #0C0E11','A9B2BC','0C0E11'),
('Relleno del deslizador #FFFFFF sobre pista #33414E','FFFFFF','33414E'),
('Pista del deslizador #33414E sobre tarjeta #161A1F (descartada)','33414E','161A1F'),
('Pista del deslizador #66727F sobre tarjeta #161A1F (la usada)','66727F','161A1F'),
('Perilla blanca sobre pista #66727F','FFFFFF','66727F'),
('Interruptor ON pista #C62F26 sobre tarjeta #161A1F','C62F26','161A1F'),
('Interruptor OFF borde #7C8794 sobre tarjeta #161A1F','7C8794','161A1F'),
('Segmento "ahora" #FFFFFF sobre segmento vacío #232A31','FFFFFF','232A31'),
('Segmento ya recorrido #7C8794 sobre vacío #232A31','7C8794','232A31'),
('Segmento ya recorrido #7C8794 sobre panel #0C0E11','7C8794','0C0E11'),
('Segmento vacío #2B343D sobre panel #0C0E11 (decorativo, no lleva información)','2B343D','0C0E11'),
('Rombo naranja sobre panel','FF9A3D','0C0E11'),('Círculo azul sobre panel','5AA9FF','0C0E11'),
('Contorno LIBRE azul #5AA9FF vs panel','5AA9FF','0C0E11'),
('Acento rojo (subrayado) #C62F26 sobre panel #0C0E11','C62F26','0C0E11'),
('Foco de teclado blanco #FFFFFF sobre #161A1F','FFFFFF','161A1F')]
for n,f,b in comp:
    v=cr(h(f),h(b)); print(f'| {n} | {v:.2f} | {"cumple" if v>=3 else "NO cumple (<3)"} |')
print('\n## E. Superficie roja (aritmética)')
print('Franja FRENÁ 320x40 =',320*40,'px2; pila FRENÁ ≈ 320x(72+8+170)=',320*250,'->',round(320*40/(320*250)*100,1),'%')
print('Estados sin FRENÁ: acento 28x3 + 4 marcas ≈ 0,2 % del conjunto')
