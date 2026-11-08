type Dict = Record<string, string>;

function txt(value: unknown): string {
  if (value == null) return "";
  return typeof value === "string" ? value.trim() : String(value);
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (char) => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#39;",
  }[char] || char));
}

function put(dict: Dict, key: string, value: unknown): void {
  const text = txt(value);
  if (text) dict[key] = text;
}

function brandMarkup(brand: string): string {
  const words = (brand || "Abson Developer").split(/\s+/).filter(Boolean);
  const last = words.pop() || brand;
  const rest = words.map(escapeHtml).join(" ");
  return rest ? `${rest} <em>${escapeHtml(last)}</em>` : `<em>${escapeHtml(last)}</em>`;
}

function rowBy(rows: any[], ...needles: string[]): any {
  return (rows || []).find((item: any) => {
    const label = txt(item?.label).toLowerCase();
    return needles.some((needle) => label.includes(needle));
  });
}

function htmlWithNote(value: unknown, note: unknown): string {
  const clean = escapeHtml(txt(value));
  const cleanNote = txt(note);
  if (!cleanNote) return clean;
  return `${clean} <span class="od-nowrap">${escapeHtml(cleanNote)}</span>`;
}

export function buildCmsI18n(content: any): Dict {
  const dict: Dict = {};
  const seo = content?.seo ?? {};
  const profile = content?.profile ?? {};
  const workCard = content?.workCard ?? {};
  const about = content?.about ?? {};
  const skills = content?.skills ?? {};
  const experience = content?.experience ?? {};
  const projects = content?.projects ?? {};
  const education = content?.education ?? {};
  const process = content?.process ?? {};
  const contact = content?.contact ?? {};
  const rows = workCard.rows ?? [];
  const headline = profile.headline ?? {};

  put(dict, "meta.title", seo.title);
  put(dict, "meta.description", seo.description);
  put(dict, "meta.imageAlt", seo.title ? `Cartão de ${txt(profile.brand) || "Abson Developer"}` : "");

  put(dict, "ui.available", profile.availability);
  put(dict, "ui.availableShort", profile.availability);
  put(dict, "ui.homeLabel", `${txt(profile.brand) || "Abson Developer"} — início`);
  put(dict, "brand.role", profile.role);
  put(dict, "footer.role", profile.role);

  put(dict, "hero.titleHtml", `${escapeHtml(txt(headline.before))}${txt(headline.emphasis) ? `<em>${escapeHtml(txt(headline.emphasis))}</em>` : ""}${escapeHtml(txt(headline.after))}`);
  put(dict, "hero.lead", profile.lead);
  put(dict, "hero.metaCity", profile.location);

  const roleRow = rowBy(rows, "atua");
  const focusRow = rowBy(rows, "foco");
  const mobileRow = rowBy(rows, "mobile");
  const webRow = rowBy(rows, "web");
  const baseRow = rowBy(rows, "base");
  put(dict, "card.title", workCard.title);
  put(dict, "card.roleLabel", roleRow?.label || "Atuação");
  put(dict, "card.role", roleRow?.value || profile.role);
  put(dict, "card.focusLabel", focusRow?.label || "Foco");
  put(dict, "card.focus", focusRow?.value);
  put(dict, "card.mobileLabel", mobileRow?.label || "Mobile");
  put(dict, "card.mobileHtml", mobileRow ? htmlWithNote(mobileRow.value, mobileRow.note) : "");
  put(dict, "card.webLabel", webRow?.label || "Web");
  put(dict, "card.webHtml", webRow ? htmlWithNote(webRow.value, webRow.note) : "");
  put(dict, "card.baseLabel", baseRow?.label || "Base");
  put(dict, "card.base", baseRow?.value || profile.location);

  put(dict, "about.title", about.title);
  put(dict, "about.lead", about.lead);
  (about.paragraphs ?? []).slice(0, 3).forEach((paragraph: unknown, index: number) => {
    put(dict, `about.p${index + 1}`, paragraph);
  });
  (about.principles ?? []).slice(0, 3).forEach((principle: any, index: number) => {
    put(dict, `about.principle${index + 1}Title`, principle?.title);
    put(dict, `about.principle${index + 1}Text`, principle?.description);
  });

  put(dict, "cap.title", skills.title);
  put(dict, "cap.lead", skills.lead);
  (skills.groups ?? []).slice(0, 4).forEach((group: any, groupIndex: number) => {
    put(dict, `cap.${groupIndex + 1}title`, group?.title);
    (group?.items ?? []).slice(0, 3).forEach((item: unknown, itemIndex: number) => {
      put(dict, `cap.${groupIndex + 1}i${itemIndex + 1}`, item);
    });
  });

  put(dict, "exp.title", experience.title);
  put(dict, "exp.lead", experience.lead);
  (experience.jobs ?? []).slice(0, 6).forEach((job: any, index: number) => {
    const n = index + 1;
    put(dict, `exp.j${n}Period`, job?.period);
    put(dict, `exp.j${n}Role`, [txt(job?.role), txt(job?.location)].filter(Boolean).join(" · "));
    (job?.bullets ?? []).slice(0, 4).forEach((bullet: unknown, bulletIndex: number) => {
      put(dict, `exp.j${n}p${bulletIndex + 1}`, bullet);
    });
  });

  put(dict, "proj.title", projects.title);
  put(dict, "proj.lead", projects.lead);
  put(dict, "proj.note", projects.note);
  const cases = projects.cases ?? [];

  function mapCase(slug: string, prefix: string): void {
    const item = cases.find((candidate: any) => candidate?.slug === slug);
    if (!item) return;
    put(dict, `proj.${prefix}Kicker`, item.kicker);
    put(dict, `proj.${prefix}Title`, item.title);
    put(dict, `proj.${prefix}Summary`, item.summary);
    (item.tags ?? []).slice(0, 3).forEach((tag: unknown, index: number) => {
      put(dict, `proj.${prefix}Tag${index + 1}`, tag);
    });
    put(dict, `case.${slug}.kicker`, item.kicker);
    put(dict, `case.${slug}.title`, item.title);
    put(dict, `case.${slug}.summary`, item.summary);
    put(dict, `case.${slug}.context`, item.context);
    put(dict, `case.${slug}.decision`, item.decision);
    put(dict, `case.${slug}.result`, item.result);
    put(dict, `case.${slug}.stack`, (item.stack ?? []).join(", "));
  }

  mapCase("quiosque", "q");
  mapCase("flavors", "f");
  mapCase("api", "a");

  put(dict, "edu.title", education.title);
  put(dict, "edu.lead", education.lead);
  put(dict, "edu.academicLabel", education.degree?.label);
  put(dict, "edu.degree", education.degree?.degree);
  put(dict, "edu.period", education.degree?.period);
  put(dict, "edu.certLabel", education.certificationsLabel);
  (education.certifications ?? []).slice(0, 5).forEach((cert: unknown, index: number) => {
    put(dict, `edu.cert${index + 1}`, cert);
  });

  put(dict, "proc.title", process.title);
  put(dict, "proc.lead", process.lead);
  (process.steps ?? []).slice(0, 4).forEach((step: any, index: number) => {
    const n = index + 1;
    put(dict, `proc.s${n}t`, step?.title);
    put(dict, `proc.s${n}d`, step?.description);
    put(dict, `proc.s${n}o`, step?.output);
  });

  put(dict, "contact.title", contact.title);
  put(dict, "contact.lead", contact.lead);
  put(dict, "contact.baseText", profile.location ? `${txt(profile.location)} · atendimento remoto` : "");
  put(dict, "contact.openLabel", profile.availability);

  const subjectKeys: Record<string, string> = {
    novo: "form.optNew",
    melhoria: "form.optImprove",
    integracao: "form.optIntegration",
    consultoria: "form.optConsult",
  };
  (contact.subjects ?? []).forEach((subject: any) => {
    const key = subjectKeys[subject?.value];
    if (key) put(dict, key, subject?.label);
  });

  const navKeys: Record<string, string> = {
    "#sobre": "nav.sobre",
    "#competencias": "nav.competencias",
    "#experiencia": "nav.experiencia",
    "#projetos": "nav.projetos",
    "#contato": "nav.contato",
  };
  (content?.navigation ?? []).forEach((item: any) => {
    const key = navKeys[item?.href];
    if (key) put(dict, key, item?.label);
  });

  return dict;
}

export function buildExtraI18n(content: any): Record<string, Dict> {
  const profile = content?.profile ?? {};
  const workCard = content?.workCard ?? {};
  const education = content?.education ?? {};
  const contact = content?.contact ?? {};
  const jobs = content?.experience?.jobs ?? [];

  const neutral: Dict = {
    "brand.nameHtml": brandMarkup(txt(profile.brand) || "Abson Developer"),
    "hero.eyebrow": txt(profile.name) || "Abson Dutra Galvão",
    "footer.meta": txt(profile.footerNote) || "© 2026 · Aracaju, SE",
    "card.caption": txt(workCard.caption) || "Abson Developer · 2026",
  };

  put(neutral, "edu.school", education.degree?.institution || "UNINASSAU");
  jobs.slice(0, 6).forEach((job: any, index: number) => {
    put(neutral, `exp.j${index + 1}Company`, job?.company);
  });

  const ptForm: Dict = {
    "form.submit": "Enviar mensagem",
    "form.note": txt(contact.formNote) || "Seus dados são enviados com segurança e usados apenas para responder ao contato.",
    "form.statusTitle": txt(contact.successTitle) || "Mensagem enviada com sucesso.",
    "form.statusText": txt(contact.successText) || "Obrigado pelo contato. Responderei pelo e-mail informado em breve.",
    "form.sending": "Enviando...",
    "form.sendError": "Não foi possível enviar agora. Tente novamente.",
    "form.sendErrorTitle": "Falha ao enviar",
  };

  const enForm: Dict = {
    "form.submit": "Send message",
    "form.note": "Your data is sent securely and used only to respond to your message.",
    "form.statusTitle": "Message sent successfully.",
    "form.statusText": "Thank you for reaching out. I'll reply to the email you provided shortly.",
    "form.sending": "Sending...",
    "form.sendError": "Could not send right now. Please try again.",
    "form.sendErrorTitle": "Send failed",
  };

  const esForm: Dict = {
    "form.submit": "Enviar mensaje",
    "form.note": "Tus datos se envían de forma segura y se usan solo para responder a tu mensaje.",
    "form.statusTitle": "Mensaje enviado con éxito.",
    "form.statusText": "Gracias por escribir. Responderé al correo indicado en breve.",
    "form.sending": "Enviando...",
    "form.sendError": "No se pudo enviar ahora. Inténtalo de nuevo.",
    "form.sendErrorTitle": "Error al enviar",
  };

  return {
    pt: { ...neutral, ...ptForm },
    en: { ...neutral, ...enForm },
    es: { ...neutral, ...esForm },
  };
}
