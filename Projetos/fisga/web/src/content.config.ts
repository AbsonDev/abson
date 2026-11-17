import { defineCollection } from 'astro:content';
import { z } from 'astro/zod';
import { glob } from 'astro/loaders';

const fonte = z.object({
  titulo: z.string(),
  url: z.url()
});

const dataIso = z.preprocess(
  (valor) => (valor instanceof Date ? valor.toISOString().slice(0, 10) : valor),
  z.string()
);

const faq = z.object({
  pergunta: z.string(),
  resposta: z.string()
});

const especies = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/especies' }),
  schema: z.object({
    titulo: z.string(),
    nomeCientifico: z.string(),
    familia: z.string(),
    resumo: z.string(),
    habitat: z.array(z.string()),
    distribuicao: z.string(),
    tamanho: z.string(),
    epoca: z.string(),
    dificuldade: z.enum(['Fácil', 'Média', 'Alta']),
    iscas: z.array(z.string()).default([]),
    tecnicas: z.array(z.string()).default([]),
    tags: z.array(z.string()).default([]),
    faq: z.array(faq).default([]),
    destaque: z.boolean().default(false),
    ordem: z.number().default(100),
    revisadoEm: dataIso,
    fontes: z.array(fonte).default([])
  })
});

const tecnicas = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/tecnicas' }),
  schema: z.object({
    titulo: z.string(),
    resumo: z.string(),
    dificuldade: z.enum(['Fácil', 'Média', 'Alta']),
    equipamento: z.array(z.string()).default([]),
    especies: z.array(z.string()).default([]),
    iscas: z.array(z.string()).default([]),
    passos: z.array(z.string()).default([]),
    erros: z.array(z.string()).default([]),
    seguranca: z.array(z.string()).default([]),
    faq: z.array(faq).default([]),
    destaque: z.boolean().default(false),
    ordem: z.number().default(100),
    revisadoEm: dataIso,
    fontes: z.array(fonte).default([])
  })
});

const iscas = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/iscas' }),
  schema: z.object({
    titulo: z.string(),
    tipo: z.enum(['Natural', 'Artificial']),
    resumo: z.string(),
    especies: z.array(z.string()).default([]),
    quandoUsar: z.string(),
    comoMontar: z.string(),
    dicas: z.array(z.string()).default([]),
    faq: z.array(faq).default([]),
    ordem: z.number().default(100),
    revisadoEm: dataIso,
    fontes: z.array(fonte).default([])
  })
});

const locais = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/locais' }),
  schema: z.object({
    titulo: z.string(),
    uf: z.string(),
    bioma: z.string(),
    resumo: z.string(),
    ambientes: z.array(z.string()).default([]),
    especies: z.array(z.string()).default([]),
    melhoresEpocas: z.string(),
    licenca: z.string(),
    seguranca: z.array(z.string()).default([]),
    faq: z.array(faq).default([]),
    ordem: z.number().default(100),
    revisadoEm: dataIso,
    fontes: z.array(fonte).default([])
  })
});

export const collections = { especies, tecnicas, iscas, locais };
