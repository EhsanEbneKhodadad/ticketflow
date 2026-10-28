import Link from "next/link";

const workflow = [
  {
    step: "01",
    title: "Start a conversation",
    description: "A customer describes a problem and opens a ticket.",
  },
  {
    step: "02",
    title: "Find the right person",
    description:
      "An agent picks up the request, adds context, and moves it forward.",
  },
  {
    step: "03",
    title: "Close the loop",
    description:
      "Resolve the issue with a clear conversation and a traceable history.",
  },
];

export default function Home() {
  return (
    <>
      <a className="skip-link" href="#main">
        Skip to content
      </a>
      <header className="site-header">
        <Link href="/" className="brand" aria-label="TicketFlow home">
          <span className="brand-mark" aria-hidden="true">
            t
          </span>
          TicketFlow
        </Link>
        <nav aria-label="Main navigation">
          <a href="#workflow">Workflow</a>
          <Link href="/login">Sign in</Link>
        </nav>
        <span className="version">v0.1 / local development</span>
      </header>
      <main id="main">
        <section className="hero" aria-labelledby="hero-title">
          <div>
            <p className="eyebrow">
              <span className="dot" /> Milestone 3 · Files & public search
            </p>
            <h1 id="hero-title">A clear path from question to resolution.</h1>
            <p className="hero-copy">
              Support should feel like a conversation. TicketFlow is a workspace
              for the people asking for help and the teams making things right.
            </p>
            <Link className="primary-link" href="/tickets">
              Open your workspace <span aria-hidden="true">↗</span>
            </Link>
            <a className="public-workflow-link" href="#workflow">
              Explore the workflow
            </a>
          </div>
          <aside className="foundation-card" aria-labelledby="foundation-title">
            <div className="card-heading">
              <span className="eyebrow">PROJECT NOTES / 001</span>
              <span aria-hidden="true">↗</span>
            </div>
            <h2 id="foundation-title">
              Good support starts with a clear conversation.
            </h2>
            <p>
              Create a request, share files, and find answers across public
              conversations.
            </p>
            <ul className="foundation-list">
              <li>
                <span>01</span> A private workspace for every customer.
              </li>
              <li>
                <span>02</span> A shared queue for your support team.
              </li>
              <li>
                <span>03</span> A complete, traceable activity history.
              </li>
            </ul>
            <div className="card-footer">
              <span>BUILT TO BE UNDERSTOOD</span>
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
            <p className="eyebrow">THE WORKFLOW</p>
            <h2 id="workflow-title">Every request has a way forward.</h2>
            <p>From the first question to the final reply.</p>
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
            <p className="eyebrow">UNDER THE SURFACE</p>
            <h2 id="architecture-title">
              A complete system,
              <br />
              with intentional boundaries.
            </h2>
          </div>
          <div className="stack-list">
            <p>
              <strong>Next.js + TypeScript</strong>
              <span>The customer and agent workspace</span>
            </p>
            <p>
              <strong>ASP.NET Core</strong>
              <span>Business rules, permissions, and API</span>
            </p>
            <p>
              <strong>PostgreSQL</strong>
              <span>The source of truth</span>
            </p>
            <p>
              <strong>Separate worker</strong>
              <span>Reconciles the public search index</span>
            </p>
          </div>
        </section>
      </main>
      <footer>
        <span>TicketFlow</span>
        <span>Thoughtful support. From the ground up.</span>
        <span>Support workspace · 2026</span>
      </footer>
    </>
  );
}
