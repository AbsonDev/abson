export interface OrgaoAmbiental {
  uf: string;
  estado: string;
  orgao: string;
  site: string;
}

export const orgaosAmbientais: OrgaoAmbiental[] = [
  { uf: 'AC', estado: 'Acre', orgao: 'IMAC', site: 'https://www.ac.gov.br/' },
  { uf: 'AL', estado: 'Alagoas', orgao: 'IMA', site: 'https://www.al.gov.br/' },
  { uf: 'AP', estado: 'Amapá', orgao: 'SEMA', site: 'https://www.ap.gov.br/' },
  { uf: 'AM', estado: 'Amazonas', orgao: 'IPAAM', site: 'https://www.amazonas.am.gov.br/' },
  { uf: 'BA', estado: 'Bahia', orgao: 'INEMA', site: 'https://www.ba.gov.br/' },
  { uf: 'CE', estado: 'Ceará', orgao: 'SEMACE', site: 'https://www.ceara.gov.br/' },
  { uf: 'DF', estado: 'Distrito Federal', orgao: 'Brasília Ambiental (IBRAM)', site: 'https://www.df.gov.br/' },
  { uf: 'ES', estado: 'Espírito Santo', orgao: 'IEMA', site: 'https://www.es.gov.br/' },
  { uf: 'GO', estado: 'Goiás', orgao: 'SEMAD', site: 'https://www.goias.gov.br/' },
  { uf: 'MA', estado: 'Maranhão', orgao: 'SEMA', site: 'https://www.ma.gov.br/' },
  { uf: 'MT', estado: 'Mato Grosso', orgao: 'SEMA', site: 'https://www.mt.gov.br/' },
  { uf: 'MS', estado: 'Mato Grosso do Sul', orgao: 'IMASUL', site: 'https://www.ms.gov.br/' },
  { uf: 'MG', estado: 'Minas Gerais', orgao: 'SEMAD/IEF', site: 'https://www.mg.gov.br/' },
  { uf: 'PA', estado: 'Pará', orgao: 'SEMAS', site: 'https://www.pa.gov.br/' },
  { uf: 'PB', estado: 'Paraíba', orgao: 'SUDEMA', site: 'https://www.pb.gov.br/' },
  { uf: 'PR', estado: 'Paraná', orgao: 'IAT', site: 'https://www.pr.gov.br/' },
  { uf: 'PE', estado: 'Pernambuco', orgao: 'CPRH', site: 'https://www.pe.gov.br/' },
  { uf: 'PI', estado: 'Piauí', orgao: 'SEMAR', site: 'https://www.pi.gov.br/' },
  { uf: 'RJ', estado: 'Rio de Janeiro', orgao: 'INEA', site: 'https://www.rj.gov.br/' },
  { uf: 'RN', estado: 'Rio Grande do Norte', orgao: 'IDEMA', site: 'https://www.rn.gov.br/' },
  { uf: 'RS', estado: 'Rio Grande do Sul', orgao: 'SEMA', site: 'https://www.rs.gov.br/' },
  { uf: 'RO', estado: 'Rondônia', orgao: 'SEMAD', site: 'https://www.ro.gov.br/' },
  { uf: 'RR', estado: 'Roraima', orgao: 'FEMARH', site: 'https://www.rr.gov.br/' },
  { uf: 'SC', estado: 'Santa Catarina', orgao: 'IMA', site: 'https://www.sc.gov.br/' },
  { uf: 'SP', estado: 'São Paulo', orgao: 'SEMIL / CETESB', site: 'https://www.sp.gov.br/' },
  { uf: 'SE', estado: 'Sergipe', orgao: 'ADEMA', site: 'https://www.se.gov.br/' },
  { uf: 'TO', estado: 'Tocantins', orgao: 'NATURATINS', site: 'https://www.to.gov.br/' }
];

export const avisoDefeso =
  'O período de piracema, o tamanho mínimo e a cota de transporte variam por bacia, espécie e ano. ' +
  'Confirme a portaria vigente no órgão ambiental da sua UF antes de sair. O Fisga não substitui a legislação.';

export const checklistDefeso = [
  'Portaria ou instrução normativa do ano vigente.',
  'Período de defeso da bacia onde você vai pescar.',
  'Tamanho mínimo e cota de transporte da espécie-alvo.',
  'Licença ou registro de pescador amador/esportivo.',
  'Regras específicas da unidade de conservação, se houver.',
  'Restrições de petrechos e de captura por pessoa.'
];
