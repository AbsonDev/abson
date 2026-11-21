// Copia o tokens.css canônico do design system do Fisga para o site.
// A fonte de verdade continua em design-system/fisga/tokens.css; o destino é
// gitignorado e regenerado por `predev`, `prebuild` e `precheck`.
import { copyFileSync, existsSync, mkdirSync } from 'node:fs';

const origem = new URL('../../design-system/fisga/tokens.css', import.meta.url);
const diretorioDeDestino = new URL('../src/styles/gerado/', import.meta.url);
const destino = new URL('tokens.css', diretorioDeDestino);

if (!existsSync(origem)) {
  console.error('tokens.css não encontrado em design-system/fisga/');
  process.exit(1);
}

mkdirSync(diretorioDeDestino, { recursive: true });
copyFileSync(origem, destino);
console.log('tokens.css copiado para src/styles/gerado/tokens.css');
