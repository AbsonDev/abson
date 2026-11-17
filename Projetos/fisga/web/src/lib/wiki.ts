export type ColecaoWiki = 'especies' | 'tecnicas' | 'iscas' | 'locais';

export type IconeWiki = 'peixe' | 'var' | 'anzol' | 'mapa' | 'escudo' | 'livro' | 'busca';

export interface CategoriaWiki {
  slug: ColecaoWiki;
  titulo: string;
  singular: string;
  descricao: string;
  icone: IconeWiki;
}

export const CATEGORIAS: CategoriaWiki[] = [
  {
    slug: 'especies',
    titulo: 'Espécies',
    singular: 'Espécie',
    descricao: 'Como identificar, onde encontrar, iscas, técnicas e manejo.',
    icone: 'peixe'
  },
  {
    slug: 'tecnicas',
    titulo: 'Técnicas',
    singular: 'Técnica',
    descricao: 'Arremesso, fly, baitcasting, fundo, corrico e praia.',
    icone: 'var'
  },
  {
    slug: 'iscas',
    titulo: 'Iscas',
    singular: 'Isca',
    descricao: 'Naturais e artificiais: quando usar e como montar.',
    icone: 'anzol'
  },
  {
    slug: 'locais',
    titulo: 'Regiões',
    singular: 'Região',
    descricao: 'Guias por estado, bioma, ambientes e épocas.',
    icone: 'mapa'
  }
];

export function categoriaPorSlug(slug: ColecaoWiki): CategoriaWiki {
  return CATEGORIAS.find((categoria) => categoria.slug === slug) ?? CATEGORIAS[0];
}

export function formatarData(iso: string): string {
  if (!iso) return '';
  const [ano, mes, dia] = iso.split('-').map(Number);
  if (!ano || !mes || !dia) return iso;

  return new Intl.DateTimeFormat('pt-BR', {
    day: '2-digit',
    month: 'long',
    year: 'numeric'
  }).format(new Date(ano, mes - 1, dia));
}

export function jsonLdBreadcrumb(itens: Array<{ nome: string; url?: string }>) {
  return {
    '@type': 'BreadcrumbList',
    itemListElement: itens.map((item, indice) => ({
      '@type': 'ListItem',
      position: indice + 1,
      name: item.nome,
      ...(item.url ? { item: item.url } : {})
    }))
  };
}

export function jsonLdArtigo(opcoes: {
  titulo: string;
  descricao: string;
  url: string;
  imagem: string;
  publicadoEm: string;
  revisadoEm: string;
  faq?: Array<{ pergunta: string; resposta: string }>;
  trilha: Array<{ nome: string; url?: string }>;
}) {
  const grafo: unknown[] = [
    {
      '@type': 'Article',
      headline: opcoes.titulo,
      description: opcoes.descricao,
      image: opcoes.imagem,
      datePublished: opcoes.publicadoEm,
      dateModified: opcoes.revisadoEm,
      inLanguage: 'pt-BR',
      mainEntityOfPage: opcoes.url,
      author: { '@type': 'Organization', name: 'Fisga' },
      publisher: {
        '@type': 'Organization',
        name: 'Fisga',
        url: 'https://fisga.abson.tech'
      }
    },
    jsonLdBreadcrumb(opcoes.trilha)
  ];

  if (opcoes.faq && opcoes.faq.length > 0) {
    grafo.push({
      '@type': 'FAQPage',
      mainEntity: opcoes.faq.map((item) => ({
        '@type': 'Question',
        name: item.pergunta,
        acceptedAnswer: {
          '@type': 'Answer',
          text: item.resposta
        }
      }))
    });
  }

  return JSON.stringify({ '@context': 'https://schema.org', '@graph': grafo }).replace(/</g, '\\u003c');
}

export function jsonLdColecao(opcoes: {
  titulo: string;
  descricao: string;
  url: string;
  itens: Array<{ nome: string; url: string }>;
}) {
  return JSON.stringify({
    '@context': 'https://schema.org',
    '@type': 'CollectionPage',
    name: opcoes.titulo,
    description: opcoes.descricao,
    url: opcoes.url,
    inLanguage: 'pt-BR',
    hasPart: opcoes.itens.map((item) => ({
      '@type': 'Article',
      name: item.nome,
      url: item.url
    }))
  }).replace(/</g, '\\u003c');
}
