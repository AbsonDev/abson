const moeda = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL'
});

export const brl = (valor: number | null | undefined): string =>
  moeda.format(Number(valor ?? 0));

export const percentual = (valor: number | null | undefined): string =>
  `${new Intl.NumberFormat('pt-BR', {
    minimumFractionDigits: 0,
    maximumFractionDigits: 1
  }).format(Number(valor ?? 0))}%`;

export const numero = (valor: number | null | undefined, casas = 2): string =>
  new Intl.NumberFormat('pt-BR', {
    minimumFractionDigits: 0,
    maximumFractionDigits: casas
  }).format(Number(valor ?? 0));

export const dataCurta = (iso: string): string =>
  new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    timeZone: 'America/Sao_Paulo'
  }).format(new Date(iso));

export const horas = (minutos: number): string => {
  const h = Math.floor(minutos / 60);
  const m = minutos % 60;

  if (h === 0) return `${m} min`;
  return m === 0 ? `${h} h` : `${h} h ${m} min`;
};
