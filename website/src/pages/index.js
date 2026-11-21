import React from 'react';
import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';

const features = [
  {
    label: 'Applications',
    title: 'Find the services you already run',
    text: 'Review discovered .NET apps and manage approved systemd services with clear results.',
    href: '/docs/applications/',
  },
  {
    label: 'Diagnostics',
    title: 'See what changed',
    text: 'Check journal entries, memory, CPU time, and public website health from the same dashboard.',
    href: '/docs/logs/',
  },
  {
    label: 'Infrastructure',
    title: 'Manage shared services carefully',
    text: 'Validate Nginx before applying changes and confirm the impact before restarting SQL Server.',
    href: '/features/',
  },
];

const steps = [
  {title: 'Open it on your PC', text: 'Build and run the .NET source preview, then open the local dashboard.'},
  {title: 'Verify your Ubuntu server', text: 'Enter your SSH address and compare its fingerprint before signing in.'},
  {title: 'Review your first app', text: 'Find a .NET service, review the details and add it to the dashboard.'},
];

function ConnectionDiagram() {
  return (
    <figure className="connection-figure" aria-labelledby="connection-title">
      <div className="connection-heading">
        <h2 id="connection-title">How it connects</h2>
        <span className="connection-status">Local dashboard</span>
      </div>
      <div className="connection-node">
        <span className="connection-icon" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
            <rect x="3" y="4" width="18" height="13" rx="2" />
            <path d="M12 17v4m-5 0h10" />
          </svg>
        </span>
        <div><strong>Your Windows PC</strong><span>Browser and Server App Manager</span></div>
      </div>
      <div className="connection-hop"><span>Existing SSH connection</span></div>
      <div className="connection-node">
        <span className="connection-icon" aria-hidden="true">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.6">
            <rect x="3" y="4" width="18" height="7" rx="2" />
            <rect x="3" y="13" width="18" height="7" rx="2" />
            <path d="M6 7.5h2m-2 9h2" />
          </svg>
        </span>
        <div><strong>Your Ubuntu server</strong><span>systemd, journal and existing hosting</span></div>
      </div>
      <figcaption>Install the dashboard on your PC. Connect to the SSH service already on Ubuntu.</figcaption>
    </figure>
  );
}

export default function Home() {
  return (
    <Layout
      title="Manage Ubuntu apps over SSH"
      description="Monitor Ubuntu apps, read systemd logs and manage approved services over SSH from your own Windows PC.">
      <header className="site-hero">
        <div className="container site-hero-grid">
          <div className="hero-copy">
            <p className="site-eyebrow">Ubuntu service management <span>Source preview</span></p>
            <h1>Manage your Ubuntu apps from your PC.</h1>
            <p className="hero-summary">
              See service status, logs, memory and website health. Manage your apps over the SSH
              connection you already use.
            </p>
            <div className="site-actions">
              <Link className="button button--primary button--lg" to="/docs/quickstart/">Read the quickstart</Link>
              <Link className="button button--outline button--secondary button--lg" to="/demo/">See the demo</Link>
            </div>
          </div>
          <ConnectionDiagram />
        </div>
      </header>
      <main>
        <section className="container home-section" aria-labelledby="features-title">
          <h2 id="features-title">Your apps, logs and health checks in one place</h2>
          <div className="feature-grid">
            {features.map(({label, title, text, href}) => (
              <article className="feature-card" key={label}>
                <p className="feature-label">{label}</p>
                <h3><Link to={href}>{title}</Link></h3>
                <p>{text}</p>
              </article>
            ))}
          </div>
        </section>
        <section className="start-section" aria-labelledby="start-title">
          <div className="container home-section">
            <h2 id="start-title">Connect to your first server</h2>
            <ol className="steps-grid">
              {steps.map(({title, text}, index) => (
                <li key={title}><span className="step-number" aria-hidden="true">0{index + 1}</span>
                  <h3>{title}</h3><p>{text}</p>
                </li>
              ))}
            </ol>
            <Link to="/docs/compatibility/">Check the preview requirements</Link>
          </div>
        </section>
      </main>
    </Layout>
  );
}
