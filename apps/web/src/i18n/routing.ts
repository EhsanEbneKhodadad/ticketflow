import { defineRouting } from "next-intl/routing";

export const routing = defineRouting({
  locales: ["en", "fr", "es", "fa-IR"],
  defaultLocale: "en",
});

export type Locale = (typeof routing.locales)[number];

export const localeMessageKeys: Record<
  Locale,
  "english" | "french" | "spanish" | "persian"
> = {
  en: "english",
  fr: "french",
  es: "spanish",
  "fa-IR": "persian",
};

export const isLocale = (value: string): value is Locale =>
  routing.locales.some((locale) => locale === value);
