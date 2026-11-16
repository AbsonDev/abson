export interface ModeloDeCanal {
  tipo: string;
  nome: string;
  descricao: string;
  comissaoPercentual: number;
  taxaFixaPorPedido: number;
  ativo: boolean;
}

export interface IngredienteSugerido {
  descricao: string;
  quantidade: number;
  unidade: string;
  precoUnitario: number;
  rendimentoPercentual: number;
}

export interface EmbalagemSugerida {
  descricao: string;
  custoPorPorcao: number;
}

export interface ModeloDeFicha {
  nomeDoPrato: string;
  estabelecimento: string;
  categoria: string;
  rendimentoEmPorcoes: number;
  porcoesVendidasPorMes: number;
  minutosDeProducao: number;
  valorHoraMaoDeObra: number;
  custosFixosMensais: number;
  impostoPercentual: number;
  cmvAlvoPercentual: number;
  ingredientes: IngredienteSugerido[];
  embalagens: EmbalagemSugerida[];
  canais: ModeloDeCanal[];
  arredondamento: string;
}

export interface EntradaIngrediente {
  descricao: string;
  quantidade: number;
  unidade: string;
  precoUnitario: number;
  rendimentoPercentual: number;
}

export interface EntradaEmbalagem {
  descricao: string;
  custoPorPorcao: number;
}

export interface EntradaCanal {
  tipo: string;
  comissaoPercentual: number;
  taxaFixaPorPedido: number;
  ativo: boolean;
}

export interface EntradaFicha {
  nomeDoPrato: string;
  estabelecimento: string;
  categoria: string;
  rendimentoEmPorcoes: number;
  porcoesVendidasPorMes: number;
  ingredientes: EntradaIngrediente[];
  embalagens: EntradaEmbalagem[];
  minutosDeProducao: number;
  valorHoraMaoDeObra: number;
  custosFixosMensais: number;
  impostoPercentual: number;
  cmvAlvoPercentual: number;
  canais: EntradaCanal[];
  arredondamento: string;
}

export interface ResultadoCanal {
  tipo: string;
  nome: string;
  comissaoPercentual: number;
  taxaFixaPorPedido: number;
  precoPorCmv: number;
  precoMinimo: number;
  precoSugerido: number;
  impostos: number;
  comissaoValor: number;
  taxaFixaAplicada: number;
  cmvRealizadoPercentual: number;
  margemContribuicao: number;
  margemContribuicaoPercentual: number;
  lucroOperacional: number;
  ajustadoPeloCustoTotal: boolean;
}

export interface ResultadoFicha {
  custoIngredientesLote: number;
  custoIngredientesPorcao: number;
  custoEmbalagemPorcao: number;
  custoMaoDeObraPorcao: number;
  custoFixoPorcao: number;
  custoTotalPorcao: number;
  cmvAlvoPercentual: number;
  rendimentoEmPorcoes: number;
  canais: ResultadoCanal[];
}

export interface FichaDetalhada {
  id: string;
  shareToken: string;
  nomeDoPrato: string;
  estabelecimento: string | null;
  categoria: string | null;
  criadoEm: string;
  atualizadoEm: string;
  entrada: EntradaFicha;
  resultado: ResultadoFicha;
}

export interface FichaSalva {
  id: string;
  shareToken: string;
  editToken: string;
  url: string;
}
