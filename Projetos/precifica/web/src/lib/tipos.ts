export interface MaterialSugerido {
  descricao: string;
  quantidade: number;
  unidade: string;
  custoUnitario: number;
}

export interface CustoExtraSugerido {
  descricao: string;
  valor: number;
}

export interface Nicho {
  nicho: string;
  nome: string;
  emoji: string;
  descricao: string;
  tituloExemplo: string;
  quantidadeExemplo: number;
  minutosDeTrabalho: number;
  valorHoraSugerido: number;
  custosFixosMensaisSugeridos: number;
  horasProdutivasPorMes: number;
  perdaDeMaterialPercentual: number;
  impostoPercentual: number;
  taxaCartaoPercentual: number;
  margemSugerida: number;
  materiais: MaterialSugerido[];
  custosExtras: CustoExtraSugerido[];
  dicaDePreco: string;
}

export interface EntradaMaterial {
  descricao: string;
  quantidade: number;
  unidade: string;
  custoUnitario: number;
}

export interface EntradaCustoExtra {
  descricao: string;
  valor: number;
}

export interface EntradaCalculo {
  nicho: string;
  titulo: string;
  quantidadeProduzida: number;
  materiais: EntradaMaterial[];
  custosExtras: EntradaCustoExtra[];
  perdaDeMaterialPercentual: number;
  minutosDeTrabalho: number;
  valorHora: number;
  custosFixosMensais: number;
  horasProdutivasPorMes: number;
  impostoPercentual: number;
  taxaCartaoPercentual: number;
  taxaFixaPorVenda: number;
  margemDesejadaPercentual: number;
  arredondamento: string;
}

export interface Resultado {
  custoDeMateriais: number;
  perdaDeMaterial: number;
  custoDeMaoDeObra: number;
  custoFixoRateado: number;
  custosExtras: number;
  custoTotal: number;
  impostos: number;
  taxaDoCartao: number;
  taxaFixa: number;
  precoSugerido: number;
  precoMinimo: number;
  lucroLiquido: number;
  margemEfetivaPercentual: number;
  precoSugeridoPorUnidade: number;
  precoMinimoPorUnidade: number;
  custoTotalPorUnidade: number;
  quantidadeProduzida: number;
}

export interface CalculoDetalhado {
  id: string;
  shareToken: string;
  titulo: string;
  nicho: string;
  nichoNome: string;
  quantidadeProduzida: number;
  criadoEm: string;
  atualizadoEm: string;
  entrada: EntradaCalculo;
  resultado: Resultado;
}

export interface Acesso {
  pdfAvulsoPago: boolean;
  assinaturaAtiva: boolean;
  premium: boolean;
}

export interface CalculoSalvo {
  id: string;
  shareToken: string;
  editToken: string;
  url: string;
}

export interface NichoResumo {
  nicho: string;
  nome: string;
  emoji: string;
  descricao: string;
}
