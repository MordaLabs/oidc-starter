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
const favicon = read('src/frontend/public/favicon.svg');
const visuals = read('src/frontend/public/premium/visuals.svg');
const nginx = read('deploy/public-demo/nginx.conf');
const deploymentGuide = read('deploy/public-demo/README.md');

test('Premium pages use the local OSS favicon and local decorative visuals', () => {
  assert.match(favicon, /<svg\b/);
  assert.match(visuals, /<svg\b/);
  for (const page of [overview, sessions, demo]) {
    assert.match(page, /<link rel="icon" type="image\/svg\+xml" href="\/favicon\.svg" \/>/);
  }
  for (const page of [overview, sessions]) {
    assert.match(page, /aria-hidden="true"/);
    assert.match(page, /\/premium\/visuals\.svg#/);
  }
});

test('Premium pages share the global header and mark their product and subnavigation state', () => {
  for (const page of [overview, sessions, demo]) {
    assert.match(page, /<div class="premium-navigation-shell">\s*<header class="site-header">/);
    assert.match(page, /<nav class="premium-subnav" aria-label="Premium navigation">[\s\S]*?<\/nav>\s*<\/div>\s*<main id="main-content">/);
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
    assert.match(page, /<a href="\/premium" aria-current="page">Premium<\/a>/);
    assert.match(page, /<nav class="premium-subnav" aria-label="Premium navigation">/);
    assert.doesNotMatch(page, /class="breadcrumb"|>Sign in<\/a>/);
  }
  assert.match(overview, /<a href="\/premium" aria-current="page">Overview<\/a>/);
  assert.match(sessions, /<a href="\/premium\/sessions" aria-current="page">Sessions<\/a>/);
});

test('Premium overview leads to demo and anchored modules, while Sessions leads to demo', () => {
  assert.match(overview, /<a class="button" href="\/premium\/demo">Try the Premium demo<\/a>/);
  assert.match(overview, /<a class="button button-secondary" href="#modules">Explore modules<\/a>/);
  assert.match(overview, /<section id="modules"/);
  assert.match(overview, /<a class="text-link" href="\/premium\/sessions">Learn more<\/a>/);
  assert.match(overview, /<h3>Next Premium module<\/h3>/);
  assert.match(overview, /Under discovery/);
  assert.match(overview, /No capability or release date is announced/);
  assert.match(sessions, /<a class="button" href="\/premium\/demo">Try live demo<\/a>/);
  assert.match(sessions, /<h2 id="live-demo-title">See Premium Sessions in the live demo<\/h2>/);
  assert.match(sessions, /mailto:premium@mordalabs.com\?subject=Premium%20Sessions%20evaluation/);
  assert.match(overview, /mailto:premium@mordalabs.com\?subject=OIDC%20Starter%20Premium%20evaluation/);
  assert.doesNotMatch(overview, /coming soon|User Management|Audit &amp; Compliance|Multi-Tenancy|Step-Up|Policies|CAEP/i);
});

test('logical demo route has a safe static fallback and host-relative canonicalization', () => {
  assert.match(nginx, /absolute_redirect off;/);
  assert.match(nginx, /location = \/premium\/demo\s*\{[\s\S]*?try_files \/premium\/demo\.html =404;/);
  for (const path of ['/premium', '/premium/sessions', '/premium/demo']) {
    assert.ok(nginx.includes(`return 308 ${path};`));
  }
  assert.doesNotMatch(nginx, /return 308 https:\/\//);
  assert.match(demo, /The public Premium demo is being prepared/);
  assert.match(demo, /mailto:premium@mordalabs.com/);
  assert.match(deploymentGuide, /redir @premiumDemo https:\/\/premium-demo\.yaroo\.ovh\/ 302/);
  assert.doesNotMatch(nginx, /demo\.oidc-starter\.mordalabs\.com/);
});
