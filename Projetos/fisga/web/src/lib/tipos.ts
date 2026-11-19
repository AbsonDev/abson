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
  privacidadeDoLocal?: 'Exato' | 'Trecho' | 'CorpoAgua' | 'Cidade' | 'Oculto';
  corpoOuTrecho?: string | null;
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
  privacidadeDoLocal: 'Exato' | 'Trecho' | 'CorpoAgua' | 'Cidade' | 'Oculto';
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
  comparacoes: Array<{
    dimensao: string;
    valor: string;
    diferencaPercentual: number;
    pescariasCom: number;
    capturasCom: number;
    pescariasSem: number;
    capturasSem: number;
  }>;
  pescarias: PescariaResumo[];
}

export interface LocalSalvo {
  id: string;
  nome: string;
  cidade: string | null;
  estado: string | null;
  latitude: number;
  longitude: number;
  ambiente: string;
  padrao: boolean;
  criadoEm: string;
}

export interface ResultadoDeBusca {
  nome: string;
  cidade: string | null;
  estado: string | null;
  latitude: number;
  longitude: number;
  pais: string | null;
}

export interface Condicoes {
  local: {
    nome: string;
    cidade: string | null;
    estado: string | null;
    latitude: number;
    longitude: number;
    ambiente: string;
  };
  atualizadoEm: string;
  fuso: string;
  fonte: string;
  temMare: boolean;
  mareAproximada: boolean;
  aviso: string | null;
  agora: {
    resumo: string;
    mareEstado: string | null;
    proximoEvento: string | null;
    proximoEventoEm: string | null;
  };
  melhorJanela: {
    inicio: string;
    fim: string;
    nota: number;
    forca: string;
    motivos: string[];
  } | null;
  dias: Array<{
    data: string;
    nota: number;
    forca: string;
    melhorInicio: string | null;
    melhorFim: string | null;
    resumo: string;
    nascerDoSol: string;
    porDoSol: string;
  }>;
  mare: {
    estado: string;
    datum: string;
    proximoEvento: {
      tipo: string;
      hora: string;
      alturaM: number;
      emHoras: number | null;
    } | null;
    eventos: Array<{
      tipo: string;
      hora: string;
      alturaM: number;
      emHoras: number | null;
    }>;
    curva: Array<{ hora: string; alturaM: number }>;
  } | null;
  solunar: {
    fase: string;
    faseNome: string;
    iluminacao: number;
    idadeDias: number;
    periodos: Array<{
      tipo: string;
      inicio: string;
      fim: string;
      motivo: string;
      atividade: string;
    }>;
  };
  tempo: {
    ventoKmh: number;
    rajadaKmh: number;
    direcaoGraus: number;
    direcao: string;
    chuvaProbabilidade: number;
    temperaturaC: number;
    sensacaoC: number;
    pressaoHpa: number;
    pressaoTendencia: string;
    pressaoAlerta: string | null;
    correnteKmh: number | null;
    correnteDirecaoGraus: number | null;
    correnteDirecao: string | null;
    temperaturaDaAguaC: number | null;
    ondaAlturaM: number | null;
    ondaPeriodoS: number | null;
  };
  linhaDoTempo: Array<{
    hora: string;
    alturaM: number | null;
    ventoKmh: number;
    chuvaProbabilidade: number;
    pressaoHpa: number;
    nota: number;
    solunar: boolean;
  }>;
}

export interface Comparacao {
  temDados: boolean;
  titulo: string;
  detalhe: string;
  pescariaId: string | null;
  shareToken: string | null;
  fatores: string[];
  pescariasConsideradas: number;
}

export interface PontoPrivado {
  id: string;
  nome: string;
  descricao: string | null;
  latitude: number;
  longitude: number;
  tipo: string;
  criadoEm: string;
  atualizadoEm: string;
}
