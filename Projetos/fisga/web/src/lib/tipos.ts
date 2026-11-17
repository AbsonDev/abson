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
  fotos?: string[] | null;
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
  fotos: string[];
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
  privacidadeDoLocal?: 'Exato' | 'Cidade' | 'Oculto';
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
  privacidadeDoLocal: 'Exato' | 'Cidade' | 'Oculto';
  podeEditar: boolean;
}

export interface PescariaSalva {
  id: string;
  shareToken: string;
  url: string;
}

export interface Usuario {
  id: string;
  email: string;
  nome: string | null;
  nomeExibicao: string;
  aceitaNovidades: boolean;
  criadoEm: string;
  ultimoAcessoEm: string | null;
}

export interface RespostaSessao {
  autenticado: boolean;
  usuario: Usuario | null;
}

export interface SessaoDaConta {
  id: string;
  userAgent: string | null;
  criadoEm: string;
  ultimoUsoEm: string;
  expiraEm: string;
  atual: boolean;
}

export interface PermissaoDaPescaria {
  pescariaId: string;
  podeEditar: boolean;
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
  taxaDeSoltura: number;
  insights: Array<{ rotulo: string; valor: string; detalhe: string }>;
  recordesPorEspecie: Array<{ especie: string; pesoKg: number; comprimentoCm: number; capturas: number }>;
  iscasPorEspecie: Array<{ especie: string; isca: string; capturas: number }>;
  pescarias: PescariaResumo[];
}
