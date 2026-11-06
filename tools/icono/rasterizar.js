// Rasteriza SVG a PNG con Chromium (Playwright). Uso: node rasterizar.js pedidos.json  ([{svg, png, px}])
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const pedidos = JSON.parse(fs.readFileSync(process.argv[2], 'utf8'));
  const navegador = await chromium.launch();
  for (const p of pedidos) {
    const pagina = await navegador.newPage({ viewport: { width: p.px, height: p.px }, deviceScaleFactor: 1 });
    const svg = fs.readFileSync(p.svg, 'utf8').replace('width="256" height="256"', `width="${p.px}" height="${p.px}"`);
    await pagina.setContent(`<html><body style="margin:0;background:transparent">${svg}</body></html>`);
    await pagina.screenshot({ path: p.png, omitBackground: true, clip: { x: 0, y: 0, width: p.px, height: p.px } });
    await pagina.close();
  }
  await navegador.close();
})();
