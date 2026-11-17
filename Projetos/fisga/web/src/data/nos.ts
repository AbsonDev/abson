export type CategoriaNo = 'fluor' | 'aco' | 'snap' | 'jig' | 'arremesso';

export interface NoDePesca {
  id: string;
  nome: string;
  categoria: CategoriaNo;
  uso: string;
  dificuldade: 'Fácil' | 'Média' | 'Avançada';
  diagrama: 'laco' | 'encontro' | 'aco' | 'carretel';
  passos: string[];
  dica: string;
}

export const categoriaNome: Record<CategoriaNo, string> = {
  fluor: 'Fluorcarbono',
  aco: 'Aço',
  snap: 'Snap e argola',
  jig: 'Jig head',
  arremesso: 'Arremesso'
};

export const nos: NoDePesca[] = [
  {
    id: 'clinch-melhorado',
    nome: 'Clinch melhorado',
    categoria: 'fluor',
    uso: 'Ligar o fluorcarbono a anzóis, jig heads e snaps.',
    dificuldade: 'Fácil',
    diagrama: 'laco',
    passos: [
      'Passe a ponta pelo olho do anzol e deixe cerca de 15 cm.',
      'Dê 5 a 7 voltas com a ponta em torno da linha principal.',
      'Passe a ponta pelo laço formado junto ao olho.',
      'Umedeça, puxe devagar e corte a sobra.'
    ],
    dica: 'No fluorcarbono, menos voltas e muita lubrificação evitam queimadura e perda de resistência.'
  },
  {
    id: 'palomar',
    nome: 'Palomar',
    categoria: 'fluor',
    uso: 'Conexão rápida e forte para anzóis e jig heads.',
    dificuldade: 'Fácil',
    diagrama: 'laco',
    passos: [
      'Dobre 20 cm da linha e passe o laço pelo olho.',
      'Faça um nó simples com a linha dobrada, sem apertar.',
      'Passe o anzol inteiro pelo laço grande.',
      'Umedeça, puxe as duas pontas e corte.'
    ],
    dica: 'É um dos nós mais confiáveis em fluor e multifilamento; confira se o laço não cruzou antes de apertar.'
  },
  {
    id: 'uni-uni',
    nome: 'Uni para uni',
    categoria: 'fluor',
    uso: 'Emendar leader de fluorcarbono na linha principal.',
    dificuldade: 'Média',
    diagrama: 'encontro',
    passos: [
      'Sobreponha 20 cm das duas linhas.',
      'Com uma ponta, faça um laço e dê 5 voltas por dentro dele.',
      'Umedeça e aperte formando o primeiro uni.',
      'Repita com a outra linha e puxe os dois nós até se encontrarem.'
    ],
    dica: 'Deixe as pontas opostas e corte rente. O nó passa melhor pelas guias.'
  },
  {
    id: 'no-de-sangue',
    nome: 'Nó de sangue',
    categoria: 'fluor',
    uso: 'Emenda fina entre linhas de diâmetros próximos.',
    dificuldade: 'Avançada',
    diagrama: 'encontro',
    passos: [
      'Sobreponha as linhas e cruze as pontas.',
      'Enrole uma ponta 5 vezes em torno da outra.',
      'Volte pelo mesmo caminho com 5 voltas no sentido oposto.',
      'Umedeça e puxe as duas linhas principais ao mesmo tempo.'
    ],
    dica: 'É discreto e passa bem pela guia, mas exige prática. Teste a resistência antes de arremessar.'
  },
  {
    id: 'empate-de-aco',
    nome: 'Empate de aço',
    categoria: 'aco',
    uso: 'Ligar leader de aço a anzóis e garatéias.',
    dificuldade: 'Média',
    diagrama: 'aco',
    passos: [
      'Passe o aço pelo olho e dobre 10 cm.',
      'Faça 5 a 7 voltas firmes com a ponta em torno do próprio aço.',
      'Puxe a ponta para dentro das voltas e aperte.',
      'Corte a sobra e alise a ponta para não machucar a linha.'
    ],
    dica: 'Use alicate de bico fino e não deixe ponta espetada. O aço não perdoa dobra errada.'
  },
  {
    id: 'laco-snap',
    nome: 'Laço para snap',
    categoria: 'snap',
    uso: 'Dar liberdade de movimento a iscas e snaps.',
    dificuldade: 'Média',
    diagrama: 'laco',
    passos: [
      'Passe a linha pelo olho e forme um laço.',
      'Dê 4 a 5 voltas em torno da linha principal.',
      'Passe a ponta pelo laço interno e depois pelo laço grande.',
      'Umedeça, ajuste o tamanho do laço e aperte.'
    ],
    dica: 'Laço muito grande perde ação; muito pequeno limita a isca. Teste o tamanho antes de fechar.'
  },
  {
    id: 'jig-head',
    nome: 'Laço para jig head',
    categoria: 'jig',
    uso: 'Manter o jig livre para trabalhar no fundo.',
    dificuldade: 'Fácil',
    diagrama: 'laco',
    passos: [
      'Passe a linha pelo olho do jig.',
      'Faça um laço e dê 4 voltas por dentro.',
      'Passe a ponta pelo olho novamente e volte pelo laço.',
      'Umedeça e aperte devagar, mantendo o laço aberto.'
    ],
    dica: 'Em pedra e estrutura, revise o nó a cada enrosco. O laço aberto é o primeiro a sofrer.'
  },
  {
    id: 'arbor',
    nome: 'Nó de arbor',
    categoria: 'arremesso',
    uso: 'Fixar a linha no carretel.',
    dificuldade: 'Fácil',
    diagrama: 'carretel',
    passos: [
      'Passe a linha pelo furo do carretel e dê uma volta.',
      'Faça um nó simples em volta da linha principal.',
      'Puxe até firmar no carretel.',
      'Enrole a linha sob tensão para evitar cabeleira.'
    ],
    dica: 'Linha frouxa no carretel vira cabeleira no primeiro arremesso. Enrole com pressão constante.'
  }
];
