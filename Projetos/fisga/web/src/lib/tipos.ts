export interface ModeloDeCaptura {
  especie: string;
  pesoKg: number;
  comprimentoCm: number;
  isca: string;
  solto: boolean;
  observacao: string | null;
}

export interface ModeloDePescaria {
  local: string;
  pescador: string;
  cidade: string;
  estado: string;
  ambiente: string;
  data: string;
  periodo: string;
  mare: string;
  lua: string;
  clima: string;
  clarezaDaAgua: string;
  observacoes: string | null;
  capturas: ModeloDeCaptura[];
}

export interface Catalogo {
  especies: string[];
  iscas: string[];
  ambientes: string[];
  periodos: string[];
  fasesDeMare: string[];
  fasesDeLua: string[];
  climas: string[];
  clarezasDeAgua: string[];
  exemplo: ModeloDePescaria;
}

export interface EntradaDeCaptura {
  id?: string | null;
  especie: string;
  pesoKg: number;
  comprimentoCm: number;
  isca?: string | null;
  solto: boolean;
  fotoUrl?: string | null;
  observacao?: string | null;
}

export interface Captura {
  id: string;
  especie: string;
  pesoKg: number;
  comprimentoCm: number;
  isca: string | null;
  solto: boolean;
  fotoUrl: string | null;
  observacao: string | null;
}

export interface EntradaDePescaria {
  local: string;
  pescador?: string | null;
  cidade?: string | null;
  estado?: string | null;
  ambiente: string;
  data: string;
  periodo: string;
  mare: string;
  lua: string;
  clima: string;
  clarezaDaAgua: string;
  observacoes?: string | null;
  capturas: EntradaDeCaptura[];
}

export interface PescariaDetalhada {
  id: string;
  shareToken: string;
  entrada: EntradaDePescaria;
  capturas: Captura[];
  criadoEm: string;
  atualizadoEm: string;
}

export interface PescariaSalva {
  id: string;
  shareToken: string;
  editToken: string;
  url: string;
}

export interface PescariaResumo {
  id: string;
  shareToken: string;
  local: string;
  data: string;
  ambiente: string;
  ambienteNome: string;
  capturas: number;
  maiorPesoKg: number;
  fotoCapa: string | null;
}

export interface Diario {
  totalPescarias: number;
  totalCapturas: number;
  especiesDistintas: number;
  totalSoltos: number;
  maiorPesoKg: number;
  maiorComprimentoCm: number;
  especieTop: string | null;
  iscaTop: string | null;
  localTop: string | null;
  pescarias: PescariaResumo[];
}
