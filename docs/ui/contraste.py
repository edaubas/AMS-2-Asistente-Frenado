import numpy as np, math
def h(x): x=x.lstrip('#'); return np.array([int(x[i:i+2],16) for i in (0,2,4)],float)
def lin(c): c=np.asarray(c)/255; return np.where(c<=0.04045,c/12.92,((c+0.055)/1.055)**2.4)
def delin(l): l=np.clip(l,0,1); return np.where(l<=0.0031308,12.92*l,1.055*l**(1/2.4)-0.055)*255
def L(c): r,g,b=lin(c); return 0.2126*r+0.7152*g+0.0722*b
def cr(a,b):
    la,lb=L(a),L(b); 
    if la<lb: la,lb=lb,la
    return (la+0.05)/(lb+0.05)
def over(fg,a,bg): return fg*a+bg*(1-a)
panel=h('0C0E11')
backs={'negro':h('000000'),'asfalto #4B4F54':h('4B4F54'),'cielo lienzo #5B6E7C':h('5B6E7C'),'piano #C9CDD1':h('C9CDD1'),'cielo claro #BFD8EE':h('BFD8EE'),'blanco':h('FFFFFF')}
texts=[('Número grande #FFFFFF','FFFFFF'),('Secundario #DDE2E7','DDE2E7'),('Título curva #EEF1F4','EEF1F4'),('Pista/leyenda #A9B2BC','A9B2BC'),('Eje mapa/regla #8D97A2 (10px)','8D97A2'),('SIN DATOS #C3CAD1','C3CAD1'),('Ámbar INMINENTE #FFC24D','FFC24D'),('Naranja IA #FF9A3D','FF9A3D'),('Azul VOS #5AA9FF','5AA9FF')]
out=[]
print('## Texto sobre panel rgba(12,14,17,0.88)  (peor caso: fondo del juego detrás)')
print('| Texto | negro | cielo lienzo | piano | cielo claro | blanco |'); print('|---|---|---|---|---|---|')
for n,c in texts:
    row=[]
    for k in ['negro','cielo lienzo #5B6E7C','piano #C9CDD1','cielo claro #BFD8EE','blanco']:
        bg=over(panel,0.88,backs[k]); row.append(cr(h(c),bg))
    print(f'| {n} | '+' | '.join(f'{x:.2f}' for x in row)+' |')
print('\n## Mismo con panel del mapa 0.84')
for n,c in texts[3:5]+texts[7:]:
    row=[cr(h(c),over(panel,0.84,backs[k])) for k in ['negro','cielo claro #BFD8EE','blanco']]
    print(n,[round(x,2) for x in row])
print('\n## Fondos efectivos'); 
for a in (0.84,0.88):
  for k in ['blanco','cielo claro #BFD8EE']: print(a,k,'#%02X%02X%02X'%tuple(int(round(v)) for v in over(panel,a,backs[k])))
print('\n## Con slider de opacidad (grupo entero, fondo blanco): contraste blanco puro / A9B2BC / naranja')
for o in (1,0.85,0.7,0.55,0.4):
    r=[]
    for c in ('FFFFFF','A9B2BC','FF9A3D','5AA9FF'):
        bg=over(panel,0.88,backs['blanco']); bgo=over(bg,o,backs['blanco']); fgo=over(h(c),o,backs['blanco'])
        # texto sobre panel: ambos atenuados contra fondo blanco
        r.append(cr(fgo,bgo))
    print(o,[round(x,2) for x in r])
print('\n## Estados con color de fondo propio')
for n,f,b in [('FRENÁ blanco sobre rojo #D93A30','FFFFFF','D93A30'),('Chip MOSTRAR #8EE0A9/#1E3A2A','8EE0A9','1E3A2A'),('Chip GRABANDO #FF9C94/#4A1F1C','FF9C94','4A1F1C'),('Chip GT3 #EEF1F4/#2A3139','EEF1F4','2A3139'),('Texto A9B2BC sobre D93A30','A9B2BC','D93A30'),('Rojo D93A30 vs panel(negro)','D93A30','0C0E11'),('Rojo B3261E con blanco','FFFFFF','B3261E'),('Rojo C62F26 con blanco','FFFFFF','C62F26'),('Rojo 9E1F19 blanco','FFFFFF','9E1F19')]:
    print(f'| {n} | {cr(h(f),h(b)):.2f} |')
print('\n## No texto (>=3:1): marcadores sobre pista #232A31 y mapa')
for n,f,b in [('Naranja vs regla #232A31','FF9A3D','232A31'),('Azul vs regla #232A31','5AA9FF','232A31'),('Naranja vs azul (L)','FF9A3D','5AA9FF'),('Trazo mapa #C3CAD1 vs panel','C3CAD1','0C0E11'),('Borde pista #56616C vs panel','56616C','0C0E11'),('Naranja sobre asfalto del mapa #56616C','FF9A3D','56616C'),('Azul sobre asfalto del mapa #56616C','5AA9FF','56616C')]:
    print(f'| {n} | {cr(h(f),h(b)):.2f} |')
# cartel principal azul sobre FRENA ?
print('\n## Daltonismo (Machado 2009, sev 1.0, RGB lineal)')
M={'protanopía':[[0.152286,1.052583,-0.204868],[0.114503,0.786281,0.099216],[-0.003882,-0.048116,1.051998]],
'deuteranopía':[[0.367322,0.860646,-0.227968],[0.280085,0.672501,0.047413],[-0.011820,0.042940,0.968881]],
'tritanopía':[[1.255528,-0.076749,-0.178779],[-0.078411,0.930809,0.147602],[0.004733,0.691367,0.303900]]}
def lab(c):
    r,g,b=lin(c); X=0.4124*r+0.3576*g+0.1805*b; Y=0.2126*r+0.7152*g+0.0722*b; Z=0.0193*r+0.1192*g+0.9505*b
    w=(0.95047,1,1.08883); f=lambda t: t**(1/3) if t>0.008856 else 7.787*t+16/116
    fx,fy,fz=f(X/w[0]),f(Y/w[1]),f(Z/w[2]); return np.array([116*fy-16,500*(fx-fy),200*(fy-fz)])
def de2000(l1,l2):
    L1,a1,b1=l1;L2,a2,b2=l2
    C1=math.hypot(a1,b1);C2=math.hypot(a2,b2);Cb=(C1+C2)/2
    G=0.5*(1-math.sqrt(Cb**7/(Cb**7+25**7)))
    a1p,a2p=(1+G)*a1,(1+G)*a2
    C1p,C2p=math.hypot(a1p,b1),math.hypot(a2p,b2)
    h1=math.degrees(math.atan2(b1,a1p))%360;h2=math.degrees(math.atan2(b2,a2p))%360
    dL=L2-L1;dC=C2p-C1p
    dh=h2-h1
    if dh>180:dh-=360
    if dh<-180:dh+=360
    dH=2*math.sqrt(C1p*C2p)*math.sin(math.radians(dh/2))
    Lb=(L1+L2)/2;Cbp=(C1p+C2p)/2
    hb=(h1+h2)/2 if abs(h1-h2)<=180 else ((h1+h2+360)/2 if h1+h2<360 else (h1+h2-360)/2)
    T=1-0.17*math.cos(math.radians(hb-30))+0.24*math.cos(math.radians(2*hb))+0.32*math.cos(math.radians(3*hb+6))-0.2*math.cos(math.radians(4*hb-63))
    SL=1+0.015*(Lb-50)**2/math.sqrt(20+(Lb-50)**2);SC=1+0.045*Cbp;SH=1+0.015*Cbp*T
    RT=-2*math.sqrt(Cbp**7/(Cbp**7+25**7))*math.sin(math.radians(60*math.exp(-((hb-275)/25)**2)))
    return math.sqrt((dL/SL)**2+(dC/SC)**2+(dH/SH)**2+RT*(dC/SC)*(dH/SH))
o,b=h('FF9A3D'),h('5AA9FF')
print('| visión | L* naranja | L* azul | ΔL* | ΔE2000 | contraste WCAG |'); print('|---|---|---|---|---|---|')
for n,m in [('normal',None)]+list(M.items()):
    def sim(c): return c if m is None else delin(np.array(m)@lin(c))
    so,sb=sim(o),sim(b); lo,lb=lab(so),lab(sb)
    print(f'| {n} | {lo[0]:.1f} | {lb[0]:.1f} | {abs(lo[0]-lb[0]):.1f} | {de2000(lo,lb):.1f} | {cr(so,sb):.2f} |')
    if m is not None: print('   sim hex', '#%02X%02X%02X'%tuple(int(round(v)) for v in so), '#%02X%02X%02X'%tuple(int(round(v)) for v in sb))
print('\nÁmbar FFC24D vs blanco FFFFFF (número inminente) L*:',lab(h('FFC24D'))[0],'; vs naranja:',de2000(lab(h('FFC24D')),lab(o)))
# proposed alternatives
for n,c in [('azul alt #4DA3FF',h('4DA3FF')),('naranja alt #FFA94D',h('FFA94D')),('naranja #FF8A1F',h('FF8A1F')),('azul #3D8BFF',h('3D8BFF')),('azul #2F7FE8',h('2F7FE8'))]:
    print(n,'L*',round(lab(c)[0],1),'cr vs naranja',round(cr(c,o),2),'cr panel blanco',round(cr(c,over(panel,.88,backs['blanco'])),2))
# cartel texts: number size
