exec(open(__import__('os').path.join(__import__('os').path.dirname(__file__),'contraste.py')).read().split("print('## Texto")[0])
panel=h('0C0E11'); W=h('FFFFFF'); S=h('BFD8EE')
print('alpha de fondo sobre blanco: blanco FFFFFF | A9B2BC | naranja | azul | 8D97A2')
for a in (0.88,0.8,0.7,0.6,0.5):
    bg=over(panel,a,W); print(a,[round(cr(h(c),bg),2) for c in ('FFFFFF','A9B2BC','FF9A3D','5AA9FF','8D97A2')])
print('bandas')
for n,f,b in [('FFFFFF/3B4652','FFFFFF','3B4652'),('FFFFFF/2F3842','FFFFFF','2F3842'),('FFFFFF/C62F26','FFFFFF','C62F26'),('FFFFFF/B3261E','FFFFFF','B3261E'),('FFC24D sobre panel','FFC24D','0C0E11'),('C62F26 borde vs panel','C62F26','0C0E11'),('FF6A5E vs panel','FF6A5E','0C0E11')]:
    print(n, round(cr(h(f),h(b)),2))
# FRENA red panel over bright backgrounds: red alpha .92 over white
for a in (1,0.92):
  bg=over(h('C62F26'),a,W); print('rojo C62F26 a',a,'sobre blanco cr texto blanco',round(cr(W,bg),2))
print('mapa ring colors vs asfalto 56616C con borde 0C0E11 -> contra borde', round(cr(h('FF9A3D'),h('0C0E11')),2))
print('chip GRABANDO dot FF5A4E vs 4A1F1C',round(cr(h('FF5A4E'),h('4A1F1C')),2))
