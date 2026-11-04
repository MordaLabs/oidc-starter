import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { test } from 'node:test';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const read = (path) => readFileSync(resolve(root, path), 'utf8');
const overview = read('src/frontend/public/premium/index.html');
const sessions = read('src/frontend/public/premium/sessions/index.html');
const demo = read('src/frontend/public/premium/demo.html');
const plOverview = read('src/frontend/public/premium/pl/index.html');
const plSessions = read('src/frontend/public/premium/pl/sessions/index.html');
const plDemo = read('src/frontend/public/premium/pl/demo.html');
const favicon = read('src/frontend/public/favicon.svg');
const visuals = read('src/frontend/public/premium/visuals.svg');
const nginx = read('deploy/public-demo/nginx.conf');
const deploymentGuide = read('deploy/public-demo/README.md');

test('Premium pages use the local OSS favicon and local decorative visuals', () => {
  assert.match(favicon, /<svg\b/);
  assert.match(visuals, /<svg\b/);
  for (const page of [overview, sessions, demo, plOverview, plSessions, plDemo]) {
    assert.match(page, /<link rel="icon" type="image\/svg\+xml" href="\/favicon\.svg" \/>/);
  }
  for (const page of [overview, sessions, plOverview, plSessions]) {
    assert.match(page, /aria-hidden="true"/);
    assert.match(page, /\/premium\/visuals\.svg#/);
  }
});

test('Premium pages share the global header and mark their product and subnavigation state', () => {
  for (const page of [overview, sessions, demo, plOverview, plSessions, plDemo]) {
    assert.match(page, /<div class="premium-navigation-shell">\s*<header class="site-header">/);
    assert.match(page, /<nav class="premium-subnav" aria-label="(?:Premium navigation|Nawigacja Premium)">[\s\S]*?<\/nav>\s*<\/div>\s*<main id="main-content">/);
  }
  for (const page of [overview, sessions]) {
    for (const [href, label] of [
      ['/#overview', 'Overview'], ['/#providers', 'Providers'],
      ['/#how-it-works', 'How it works'], ['/#demo', 'Live demo'],
      ['/#security', 'Security'], ['/#get-started', 'Get started'],
      ['/#github', 'GitHub'],
    ]) {
      assert.ok(page.includes(`<a href="${href}">${label}</a>`));
    }
    assert.match(page, /<a href="\/premium\/" aria-current="page">Premium<\/a>/);
    assert.match(page, /<nav class="premium-subnav" aria-label="Premium navigation">/);
    assert.doesNotMatch(page, /class="breadcrumb"|>Sign in<\/a>/);
  }
  assert.match(overview, /<a href="\/premium\/" aria-current="page">Overview<\/a>/);
  assert.match(sessions, /<a href="\/premium\/sessions\/" aria-current="page">Sessions<\/a>/);
  assert.match(plOverview, /<a href="\/premium\/pl\/" aria-current="page">Przegląd<\/a>/);
  assert.match(plSessions, /<a href="\/premium\/pl\/sessions\/" aria-current="page">Sesje<\/a>/);
  for (const [en, pl, enPath, plPath] of [
    [overview, plOverview, '/premium/', '/premium/pl/'],
    [sessions, plSessions, '/premium/sessions/', '/premium/pl/sessions/'],
    [demo, plDemo, '/premium/demo.html', '/premium/pl/demo.html'],
  ]) {
    for (const page of [en, pl]) {
      assert.ok(page.includes(`href="${enPath}" lang="en" hreflang="en"`));
      assert.ok(page.includes(`href="${plPath}" lang="pl" hreflang="pl"`));
    }
  }
});

test('EN and PL landing and Sessions CTAs lead to their demo gateways', () => {
  assert.match(overview, /<a class="button" href="\/premium\/demo\.html">Try the Premium demo<\/a>/);
  assert.match(sessions, /<a class="button" href="\/premium\/demo\.html">Try live demo<\/a>/);
  assert.match(plOverview, /<a class="button" href="\/premium\/pl\/demo\.html">Wypróbuj demo Premium<\/a>/);
  assert.match(plSessions, /<a class="button" href="\/premium\/pl\/demo\.html">Wypróbuj demo na żywo<\/a>/);
  for (const page of [overview, sessions]) assert.match(page, /href="\/premium\/demo\.html"/);
  for (const page of [plOverview, plSessions]) assert.match(page, /href="\/premium\/pl\/demo\.html"/);
  assert.match(overview, /<a class="button button-secondary" href="#modules">Explore modules<\/a>/);
  assert.match(overview, /<section id="modules"/);
  assert.match(overview, /<a class="text-link" href="\/premium\/sessions\/">Learn more<\/a>/);
  assert.match(overview, /<h3>Next Premium module<\/h3>/);
  assert.match(overview, /Under discovery/);
  assert.match(overview, /No capability or release date is announced/);
  assert.match(sessions, /<h2 id="live-demo-title">See Premium Sessions in the live demo<\/h2>/);
  assert.match(sessions, /mailto:premium@mordalabs.com\?subject=Premium%20Sessions%20evaluation/);
  assert.match(overview, /mailto:premium@mordalabs.com\?subject=OIDC%20Starter%20Premium%20evaluation/);
  assert.doesNotMatch(overview, /coming soon|User Management|Audit &amp; Compliance|Multi-Tenancy|Step-Up|Policies|CAEP/i);
});

test('EN and PL gateways link to the live demo and describe public URL and account access', () => {
  assert.match(demo, /<a class="button" href="https:\/\/premium-demo\.yaroo\.ovh\/">Open live demo<\/a>/);
  assert.match(plDemo, /<a class="button" href="https:\/\/premium-demo\.yaroo\.ovh\/">Otwórz demo na żywo<\/a>/);
  assert.match(demo, /URL is public.*approved test account/);
  assert.match(plDemo, /Adres demo Premium na żywo jest publiczny.*zatwierdzonego konta testowego/);
  assert.match(demo, /Request demo access: coming soon\./);
  assert.match(plDemo, /Prośba o dostęp do demo: wkrótce\./);
  for (const page of [demo, plDemo]) {
    assert.doesNotMatch(page, /Explore Premium Sessions|Poznaj Premium Sessions|mailto:|<form\b/i);
  }
  assert.match(demo, /href="\/premium\/pl\/demo\.html"[^>]*>PL<\/a>/);
  assert.match(plDemo, /href="\/premium\/demo\.html"[^>]*>EN<\/a>/);
});

test('logical demo route serves the static gateway locally', () => {
  assert.match(nginx, /absolute_redirect off;/);
  assert.match(nginx, /location = \/premium\/demo\s*\{[\s\S]*?try_files \/premium\/demo\.html =404;/);
  assert.match(nginx, /location = \/premium\/demo\/\s*\{[\s\S]*?return 308 \/premium\/demo;/);
  assert.doesNotMatch(nginx, /return 308 https:\/\//);
  assert.match(deploymentGuide, /redir @premiumDemo https:\/\/premium-demo\.yaroo\.ovh\/ 302/);
  assert.doesNotMatch(nginx, /demo\.oidc-starter\.mordalabs\.com/);
});
