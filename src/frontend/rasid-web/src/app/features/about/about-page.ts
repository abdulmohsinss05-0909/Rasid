import { ChangeDetectionStrategy, Component } from '@angular/core';

/** Explains the engagement method and how Rasid complies with X's rules — inside the product. */
@Component({
  selector: 'rasid-about-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="r-page">
      <header class="r-page-header">
        <div>
          <h1>Method &amp; compliance</h1>
          <p>How Rasid collects data, calculates engagement and respects X's rules.</p>
        </div>
      </header>

      <div class="grid">
        <section class="r-card">
          <h2 class="r-card__title"><span class="material-symbols-rounded">functions</span> Engagement indicators</h2>
          <dl>
            <dt>Interactions</dt>
            <dd><code>Likes + Reposts + Replies + Quotes</code></dd>
            <dt>Post engagement rate</dt>
            <dd><code>Interactions ÷ Views × 100</code></dd>
            <dt>Topic engagement rate</dt>
            <dd><code>Σ Interactions ÷ Σ Views</code>, over posts that have view counts. Weighted by views, so one low-view post cannot distort the figure.</dd>
            <dt>Missing views</dt>
            <dd>When X provides no view count the rate is shown as <strong>N/A</strong>. It is never estimated.</dd>
            <dt>Engagement level</dt>
            <dd>High &gt; 3% · Medium 1–3% · Low &lt; 1%. These thresholds are configurable benchmarks, not an X standard.</dd>
          </dl>
        </section>

        <section class="r-card">
          <h2 class="r-card__title"><span class="material-symbols-rounded">sync</span> Data collection</h2>
          <ul>
            <li>Official <strong>X API v2</strong> only: Recent Search for discovery and Post lookup for metric refresh.</li>
            <li>Incremental: each run asks only for posts newer than the last one collected.</li>
            <li>Metrics refresh more often for young posts (15 min) and less for older ones (2 h, then daily).</li>
            <li>A quota manager keeps usage within the purchased X plan and slows low-priority topics first.</li>
            <li>Every screen shows the data source and when it was last updated.</li>
          </ul>
        </section>

        <section class="r-card wide">
          <h2 class="r-card__title"><span class="material-symbols-rounded">verified_user</span> Compliance with X's rules</h2>
          <ol>
            <li><strong>Official API only.</strong> No scraping, browser automation, unofficial endpoints or circumvention of access controls.</li>
            <li><strong>Rate limits respected.</strong> A rate-limited request is not retried until the reset time X reports.</li>
            <li><strong>Public content only</strong>, and only the fields needed for the stated purpose.</li>
            <li><strong>Deletion sync.</strong> Posts that are deleted or made unavailable on X are removed from Rasid.</li>
            <li><strong>Retention limits.</strong> Post content expires after the configured period. Only anonymous aggregates are kept longer.</li>
            <li><strong>No sensitive inference.</strong> Rasid does not derive health, political, religious or similar characteristics of individuals.</li>
            <li><strong>Topics, not people.</strong> Rasid monitors conversations around topics and is not designed for surveillance of individuals.</li>
            <li><strong>Display requirements.</strong> Author name, username, time and a link to the original post are shown, and the text is not altered.</li>
            <li><strong>No redistribution</strong> of raw X content. Credentials are kept in a secret store, never in code or the browser.</li>
            <li><strong>Transparency.</strong> Sample data is fictitious, clearly labelled and never presented as real.</li>
          </ol>
        </section>
      </div>
    </div>
  `,
  styles: `
    .grid { display: grid; gap: 16px; grid-template-columns: repeat(auto-fit, minmax(min(100%, 380px), 1fr)); }
    .wide { grid-column: 1 / -1; }
    .r-card__title { justify-content: flex-start; }
    .r-card__title .material-symbols-rounded { color: var(--r-brand); }
    dl { margin: 0; display: grid; gap: 4px 0; }
    dt { font-weight: 650; margin-top: 10px; }
    dd { margin: 0; color: var(--r-text-muted); line-height: 1.5; }
    code { font-family: var(--r-font-mono); font-size: 0.82rem; color: var(--r-text); background: var(--r-surface-2); padding: 2px 6px; border-radius: 6px; }
    ul, ol { margin: 0; padding-left: 20px; display: grid; gap: 10px; color: var(--r-text-muted); line-height: 1.5; }
    strong { color: var(--r-text); }
  `,
})
export class AboutPage {}
