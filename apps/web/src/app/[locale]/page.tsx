import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { LanguageSwitcher } from "@/components/language-switcher";

export default function Home() {
  const t = useTranslations("Home");
  const workflow = [
    { step: "01", title: t("startTitle"), description: t("startDescription") },
    { step: "02", title: t("findTitle"), description: t("findDescription") },
    { step: "03", title: t("closeTitle"), description: t("closeDescription") },
  ];
  return (
    <>
      <a className="skip-link" href="#main">
        {t("skip")}
      </a>
      <header className="site-header">
        <Link href="/" className="brand" aria-label={t("brandHome")}>
          <span className="brand-mark" aria-hidden="true">
            t
          </span>
          TicketFlow
        </Link>
        <nav aria-label={t("nav")}>
          <a href="#workflow">{t("workflow")}</a>
          <Link href="/login">{t("signIn")}</Link>
        </nav>
        <LanguageSwitcher />
        <span className="version">{t("version")}</span>
      </header>
      <main id="main">
        <section className="hero" aria-labelledby="hero-title">
          <div>
            <p className="eyebrow">
              <span className="dot" /> {t("milestone")}
            </p>
            <h1 id="hero-title">{t("title")}</h1>
            <p className="hero-copy">{t("intro")}</p>
            <Link className="primary-link" href="/tickets">
              {t("openWorkspace")} <span aria-hidden="true">↗</span>
            </Link>
            <a className="public-workflow-link" href="#workflow">
              {t("exploreWorkflow")}
            </a>
          </div>
          <aside className="foundation-card" aria-labelledby="foundation-title">
            <div className="card-heading">
              <span className="eyebrow">{t("projectNotes")}</span>
              <span aria-hidden="true">↗</span>
            </div>
            <h2 id="foundation-title">{t("cardTitle")}</h2>
            <p>{t("cardDescription")}</p>
            <ul className="foundation-list">
              <li>
                <span>01</span> {t("customerSpace")}
              </li>
              <li>
                <span>02</span> {t("teamQueue")}
              </li>
              <li>
                <span>03</span> {t("history")}
              </li>
            </ul>
            <div className="card-footer">
              <span>{t("built")}</span>
              <span aria-hidden="true">+</span>
            </div>
          </aside>
        </section>
        <section
          id="workflow"
          className="workflow"
          aria-labelledby="workflow-title"
        >
          <div className="section-intro">
            <p className="eyebrow">{t("workflowKicker")}</p>
            <h2 id="workflow-title">{t("workflowTitle")}</h2>
            <p>{t("workflowIntro")}</p>
          </div>
          <div className="workflow-grid">
            {workflow.map((item) => (
              <article key={item.step}>
                <span className="step-number">{item.step}</span>
                <h3>{item.title}</h3>
                <p>{item.description}</p>
              </article>
            ))}
          </div>
        </section>
        <section
          id="architecture"
          className="architecture"
          aria-labelledby="architecture-title"
        >
          <div>
            <p className="eyebrow">{t("architectureKicker")}</p>
            <h2 id="architecture-title">{t("architectureTitle")}</h2>
          </div>
          <div className="stack-list">
            <p>
              <strong>Next.js + TypeScript</strong>
              <span>{t("webStack")}</span>
            </p>
            <p>
              <strong>ASP.NET Core</strong>
              <span>{t("apiStack")}</span>
            </p>
            <p>
              <strong>PostgreSQL</strong>
              <span>{t("databaseStack")}</span>
            </p>
            <p>
              <strong>{t("workerName")}</strong>
              <span>{t("workerStack")}</span>
            </p>
          </div>
        </section>
      </main>
      <footer>
        <span>TicketFlow</span>
        <span>{t("tagline")}</span>
        <span>{t("copyright")}</span>
      </footer>
    </>
  );
}
