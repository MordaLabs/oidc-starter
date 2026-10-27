export const englishCopy = {
  language: 'Language',
  english: 'English',
  polish: 'Polish',
  heroEyebrow: 'Provider-neutral BFF authentication',
} as const;

export type Locale = 'en' | 'pl';
export type Translation = { [Key in keyof typeof englishCopy]: string };

const polishCopy = {
  language: 'Język',
  english: 'Angielski',
  polish: 'Polski',
  heroEyebrow: 'Uwierzytelnianie BFF niezależne od dostawcy',
} satisfies Translation;

export const localeCatalog: Record<Locale, Translation> = {
  en: englishCopy,
  pl: polishCopy,
};
