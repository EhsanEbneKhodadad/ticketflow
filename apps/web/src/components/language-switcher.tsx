"use client";

import { useLocale, useTranslations } from "next-intl";
import { usePathname, useRouter } from "@/i18n/navigation";
import { localeMessageKeys, routing, type Locale } from "@/i18n/routing";

export function LanguageSwitcher() {
  const locale = useLocale() as Locale;
  const t = useTranslations("Language");
  const pathname = usePathname();
  const router = useRouter();

  function changeLocale(nextLocale: string) {
    const query = window.location.search;
    const hash = window.location.hash;
    router.replace(`${pathname}${query}${hash}`, {
      locale: nextLocale as Locale,
    });
  }

  return (
    <label className="language-switcher">
      <span className="sr-only">{t("label")}</span>
      <select
        aria-label={t("label")}
        value={locale}
        onChange={(event) => changeLocale(event.target.value)}
      >
        {routing.locales.map((option) => (
          <option key={option} value={option}>
            {t(localeMessageKeys[option])}
          </option>
        ))}
      </select>
    </label>
  );
}
